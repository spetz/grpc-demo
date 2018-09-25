using System;

namespace GrpcDemo.Core.Events
{
    public class MessageSent
    {
        public Guid Id { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}