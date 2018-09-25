using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using GrpcDemo.Core;

namespace GrpcDemo.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            const string host = "localhost";
            const int port = 50000;
            
            Console.WriteLine("Starting a server...");
            
            var server = new Grpc.Core.Server
            {
                Services = {DemoService.BindService(new DemoServiceHost())},
                Ports = {new ServerPort(host, port, ServerCredentials.Insecure)}
            };
            server.Start();
            
            Console.WriteLine($"Listening on port: {port}");
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();
            
            await server.ShutdownAsync();
        }
    }

    class DemoServiceHost : DemoService.DemoServiceBase
    {
        private readonly ConcurrentQueue<Event> _events = new ConcurrentQueue<Event>();

        public override async Task<Empty> Send(Message request, ServerCallContext context)
        {
            Console.WriteLine(GetMessageAndClientText(request, context));

            return await Task.FromResult(new Empty());
        }

        public override async Task<Message> SendWithReply(Message request, ServerCallContext context)
        {
            Console.WriteLine(GetMessageAndClientText(request, context));
            Console.WriteLine($"+ Sending a reply. {GetClientText(context)} +");

            return await Task.FromResult(CreateMessage());
        }

        public override async Task<Empty> SendStream(IAsyncStreamReader<Message> requestStream,
            ServerCallContext context)
        {
            Console.WriteLine($"* Started messages stream. {GetClientText(context)} *");

            while (await requestStream.MoveNext(CancellationToken.None))
            {
                var message = requestStream.Current;
                Console.WriteLine($"- {GetMessageText(message)} -");
            }

            Console.WriteLine($"* Completed messages stream. {GetClientText(context)} *");

            return new Empty();
        }

        public override async Task SendStreamWithReplyStream(IAsyncStreamReader<Message> requestStream,
            IServerStreamWriter<Message> responseStream,
            ServerCallContext context)
        {
            Console.WriteLine($"* Started messages stream with reply stream. {GetClientText(context)} *");

            while (await requestStream.MoveNext(CancellationToken.None))
            {
                var message = requestStream.Current;
                Console.WriteLine($"- {GetMessageText(message)} -");
                await responseStream.WriteAsync(CreateMessage());
                Console.WriteLine("+ Message in stream sent +");
            }

            Console.WriteLine($"* Completed messages stream with reply stream. {GetClientText(context)} *");
        }

        public override async Task<Empty> Push(Event request, ServerCallContext context)
        {
            _events.Enqueue(request);
            Console.WriteLine($"- Saved event -> id: '{request.Id}', type: '{request.Type}'. {GetClientText(context)} -");

            return await Task.FromResult(new Empty());
        }

        //Beware of 4MB message size!
        public override async Task<Empty> PushManyStream(IAsyncStreamReader<Event> requestStream, ServerCallContext context)
        {
            Console.WriteLine($"- Saving events from stream. {GetClientText(context)} -");

            var eventsCount = 0;
            var timer = new Stopwatch();
            timer.Start();

            while (await requestStream.MoveNext())
            {
                var @event = requestStream.Current;
                _events.Enqueue(@event);
                eventsCount++;
            }

            timer.Stop();

            Console.WriteLine($"- Saved {eventsCount} events from stream in {timer.ElapsedMilliseconds} ms. {GetClientText(context)} -");

            return new Empty();
        }

        public override async Task<SingleEvent> Pull(Empty request, ServerCallContext context)
        {
            await Task.CompletedTask;

            if (_events.IsEmpty)
            {
                Console.WriteLine("* No events in queue *");

                return new SingleEvent();
            }

            _events.TryDequeue(out var @event);

            Console.WriteLine($"+ Sent event -> id: '{@event.Id}', type: '{@event.Type}'. {GetClientText(context)} +");

            return new SingleEvent
            {
                Event = @event
            };
        }

        public override async Task<ManyEvents> PullMany(Empty request, ServerCallContext context)
        {
            if (_events.IsEmpty)
            {
                Console.WriteLine("* No events in queue *");

                return new ManyEvents
                {
                    Events = { }
                };
            }

            var events = new List<Event>();

            while (!_events.IsEmpty)
            {
                _events.TryDequeue(out var @event);
                events.Add(@event);
            }

            Console.WriteLine($"+ Sent {events.Count} events. {GetClientText(context)}. {GetClientText(context)} +");

            return await Task.FromResult(new ManyEvents
            {
                Events = {events}
            });
        }

        public override async Task PullManyStream(Empty request, IServerStreamWriter<Event> responseStream, ServerCallContext context)
        {
            if (_events.IsEmpty)
            {
                Console.WriteLine("* No events in queue *");

                return;
            }

            var eventsCount = _events.Count;
            var timer = new Stopwatch();
            timer.Start();
            
            while (!_events.IsEmpty)
            {
                _events.TryDequeue(out var @event);
                await responseStream.WriteAsync(@event);
            }
            
            timer.Stop();

            Console.WriteLine($"+ Sent {eventsCount} events as stream in {timer.ElapsedMilliseconds} ms. {GetClientText(context)} +");
        }

        private static string GetMessageAndClientText(Message message, ServerCallContext context)
            => $"- {GetMessageText(message)}. {GetClientText(context)} -";

        private static string GetClientText(ServerCallContext context)
            => $"Client: '{context.Peer}'";

        private static string GetMessageText(Message message)
            => $"Received message -> id: '{message.Id}', user: '{message.User}', content: '{message.Content}'";

        private static Message CreateMessage()
            => new Message
            {
                User = "Server",
                Content = "Hello from server!",
                Id = Guid.NewGuid().ToString("N")
            };
    }
}
