using System;

namespace GrpcDemo.Core.Events
{
    public class UserCreated
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }
}