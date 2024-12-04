namespace  PhoenixNet
{
    public static class Constants
    {
        public const string VSN = "1.0.0";
        
        public static class ChannelEvents
        {
            public const string Close = "phx_close";
            public const string Error = "phx_error";
            public const string Join = "phx_join";
            public const string Reply = "phx_reply";
            public const string Leave = "phx_leave";
        }

        public static class ChannelStates
        {
            public const string Closed = "closed";
            public const string Errored = "errored";
            public const string Joined = "joined";
            public const string Joining = "joining";
            public const string Leaving = "leaving";
        }

        public const int DefaultTimeout = 10000;
        public const int DefaultHeartbeatInterval = 5000;
    }
}
