using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using GrpcDemo.Core;
using GrpcDemo.Core.Events;
using Utf8Json;

namespace GrpcDemo.Client
{
    class Program
    {
        private static readonly Random Random = new Random();
        private static DemoService.DemoServiceClient _client;

        private static readonly IDictionary<string, Func<Task>> Actions = new Dictionary<string, Func<Task>>
        {
            ["1"] = SendAsync,
            ["2"] = SendWithReplyAsync,
            ["3"] = SendStreamAsync,
            ["4"] = SendStreamWithReplyStreamAsync,
            ["5"] = PushAsync,
            ["6"] = PullAsync,
            ["7"] = PushManyAsStreamAsync,
            ["8"] = PullManyAsync,
            ["9"] = PullManyStreamAsync
        };

        static async Task Main(string[] args)
        {
            const string host = "localhost";
            const int port = 50000;

            Console.WriteLine("Starting a client...");

            var channel = new Channel($"{host}:{port}", ChannelCredentials.Insecure);
            _client = new DemoService.DemoServiceClient(channel);

            Console.WriteLine("Client has started.");

            await MenuAsync();
        }

        static async Task MenuAsync()
        {
            const string message = "\nOptions (1-9):\n1. send\n2. send with reply\n" +
                                   "3. send as stream\n4. send as stream with reply as stream\n" +
                                   "5. push\n6. pull\n7. push many as stream\n" +
                                   "8. pull many (beware of 4MB message size limit)\n" +
                                   "9. pull many as stream\nType 'q' to quit.\n";

            var option = string.Empty;
            while (option != "q")
            {
                Console.WriteLine(message);
                Console.Write("> ");
                option = Console.ReadLine();
                Console.WriteLine();
                if (Actions.ContainsKey(option))
                {
                    await Actions[option]();
                    continue;
                }

                Console.WriteLine($"Invalid option: {option}");
            }
        }

        static async Task SendAsync()
        {
            await _client.SendAsync(CreateMessage());
            Console.WriteLine("+ Message sent +");
        }

        static async Task SendWithReplyAsync()
        {
            var message = await _client.SendWithReplyAsync(CreateMessage());
            Console.WriteLine($"+ Message with reply sent +\n{GetMessageText(message)}");
        }

        static async Task SendStreamAsync()
        {
            const int limit = 10;
            Console.WriteLine("* Sending messages stream *");
            var stream = _client.SendStream();
            var requestStream = stream.RequestStream;
            for (var i = 0; i < limit; i++)
            {
                Console.WriteLine("+ Message in stream sent +");
                await requestStream.WriteAsync(CreateMessage());
                await Task.Delay(200);
            }

            Console.WriteLine("* Messages stream sent *");
        }

        static async Task SendStreamWithReplyStreamAsync()
        {
            const int limit = 10;
            var stream = _client.SendStreamWithReplyStream();
            var requestStream = stream.RequestStream;
            var responseStream = stream.ResponseStream;

            Console.WriteLine("* Sending messages stream with reply stream *");

            for (var i = 0; i < limit; i++)
            {
                Console.WriteLine("+ Message in stream sent +");

                await requestStream.WriteAsync(CreateMessage());
                await Task.Delay(200);
                if (!await responseStream.MoveNext())
                {
                    continue;
                }

                var message = responseStream.Current;
                Console.WriteLine(GetMessageText(message));
            }

            await requestStream.CompleteAsync();

            Console.WriteLine("* Messages stream with reply stream completed *");
        }
        
        static async Task PushAsync()
        {
            var @event = CreateRandomEvent();

            Console.WriteLine($"+ Pushing an event of type '{@event.Type}', with id: '{@event.Id}' +");

            await _client.PushAsync(@event);
            
            Console.WriteLine($"+ Pushed an event of type '{@event.Type}', with id: '{@event.Id}'+");
        }

        //Beware of 4MB message size!
        static async Task PullAsync()
        {
            Console.WriteLine($"- Pulling an event -");

            var @event = await _client.PullAsync(new Empty());
            if (@event.Event == null)
            {
                Console.WriteLine("* No events available *");

                return;
            }

            Console.WriteLine($"- Pulled an event of type '{@event.Event.Type}' -");
            
            ProcessEvent(@event.Event);
        }

        static async Task PushManyAsStreamAsync()
        {
            const int limit = 10000;
            var stream = _client.PushManyStream();
            var requestStream = stream.RequestStream;

            Console.WriteLine($"+ Pushing {limit} event(s) as stream +");

            var timer = new Stopwatch();
            timer.Start();

            for (var i = 0; i < limit; i++)
            {
                await requestStream.WriteAsync(CreateRandomEvent());
            }

            await requestStream.CompleteAsync();

            timer.Stop();

            Console.WriteLine($"+ Pushed {limit} event(s) as stream in {timer.ElapsedMilliseconds} ms +");
        }

        static async Task PullManyAsync()
        {
            Console.WriteLine($"- Pulling events -");
            
            var timer = new Stopwatch();
            timer.Start();
            
            var events = await _client.PullManyAsync(new Empty());
            if (!events.Events.Any())
            {
                Console.WriteLine("* No events available *");
                
                return;
            }
            
            timer.Stop();
            
            Console.WriteLine($"- Pulled {events.Events.Count} events in {timer.ElapsedMilliseconds} ms -");
        }
        
        static async Task PullManyStreamAsync()
        {
            Console.WriteLine($"- Pulling events as stream -");

            var events = new List<Event>();
            var timer = new Stopwatch();
            timer.Start();
            
            var stream = _client.PullManyStream(new Empty());
            var responseStream = stream.ResponseStream;
            
            while (await responseStream.MoveNext())
            {
                var @event = responseStream.Current;
                events.Add(@event);
            }

            if (!events.Any())
            {
                Console.WriteLine("* No events available *");
                
                return;
            }

            timer.Stop();
            
            Console.WriteLine($"- Pulled {events.Count} events as stream in {timer.ElapsedMilliseconds} ms -");
        }

        static void ProcessEvent(Event @event)
        {
            Console.WriteLine($"- Processing event: '{@event.Type}' -");
            switch (@event.Type)
            {
                case "message_sent":
                    var messageSent = JsonSerializer.Deserialize<MessageSent>(@event.Data.ToByteArray());
                    Console.WriteLine($"- Data -> id: '{messageSent.Id}', content: '{messageSent.Content}' -");
                    break;

                case "user_created":
                    var userCreated = JsonSerializer.Deserialize<UserCreated>(@event.Data.ToByteArray());
                    Console.WriteLine($"- Data -> id: '{userCreated.Id}', content: '{userCreated.Name}' -");
                    break;

                default:
                    Console.WriteLine($"Invalid event type: '{@event.Type}'");
                    break;
            }
        }

        private static Event CreateRandomEvent()
        {
            if (Random.Next(0, 2) == 0)
            {
                return new Event
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Type = "message_sent",
                    Data = ByteString.CopyFrom(JsonSerializer.Serialize(MessageSent()))
                };
            }

            return new Event
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = "user_created",
                Data = ByteString.CopyFrom(JsonSerializer.Serialize(UserCreated()))
            };
        }

        private static MessageSent MessageSent()
            => new MessageSent
            {
                Id = Guid.NewGuid(),
                From = "sender",
                To = "receiver",
                Content = "hello",
                CreatedAt = DateTime.UtcNow
            };
        
        private static UserCreated UserCreated()
            => new UserCreated
            {
                Id = Guid.NewGuid(),
                Name = "user",
                Email = "user@user.com"
            };

        private static string GetMessageText(Message message)
            => $"- Received message -> id: '{message.Id}', user: '{message.User}', content: '{message.Content}' -";

        private static Message CreateMessage()
            => new Message
            {
                User = "User #1",
                Content = "Hello from client!",
                Id = Guid.NewGuid().ToString("N")
            };
    }
}
