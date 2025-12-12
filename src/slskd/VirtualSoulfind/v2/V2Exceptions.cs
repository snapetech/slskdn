// Error types for v2
namespace slskd.VirtualSoulfind.v2
{
    using System;

    public class V2Exception : Exception
    {
        public V2Exception(string message) : base(message) { }
        public V2Exception(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class PlanningException : V2Exception
    {
        public PlanningException(string message) : base(message) { }
    }

    public sealed class MatchException : V2Exception  
    {
        public MatchException(string message) : base(message) { }
    }

    public sealed class BackendException : V2Exception
    {
        public BackendException(string message) : base(message) { }
        public BackendException(string message, Exception inner) : base(message, inner) { }
    }
}
