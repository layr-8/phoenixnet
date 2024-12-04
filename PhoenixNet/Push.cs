using Serilog;

namespace  PhoenixNet
{
    public class Push
    {
        private readonly Channel channel;
        private readonly string @event;
        private readonly object payload;
        private int timeout;
        private Timer timeoutTimer;
        private readonly List<ReceiverHook> recHooks;
        private bool sent;
        private string refEvent;
        private string @ref;
        private ReceivedResponse receivedResp;
        private readonly ILogger _logger = Log.ForContext<Socket>();

        public Push(Channel channel, string @event, object payload, int timeout)
        {
            this.channel = channel;
            this.@event = @event;
            this.payload = payload ?? new { };
            this.timeout = timeout;
            this.timeoutTimer = null;
            this.recHooks = new List<ReceiverHook>();
            this.sent = false;
            this.receivedResp = null;
        }

        public async Task Resend(int timeout)
        {
            this.timeout = timeout;
            CancelRefEvent();
            this.@ref = null;
            this.refEvent = null;
            this.receivedResp = null;
            this.sent = false;
            await Send();
        }

        public async Task Send()
        {
            if (HasReceived("timeout")) return;

            StartTimeout();
            sent = true;
            await channel.Socket.PushAsync(new
            {
                topic = channel.Topic,
                @event,
                payload,
                @ref = this.@ref
            });
        }

        public Push Receive(string status, Action<object> callback)
        {
            if (HasReceived(status))
            {
                callback(receivedResp.Response);
            }

            recHooks.Add(new ReceiverHook
            {
                Status = status,
                Callback = callback
            });

            return this;
        }

        private void MatchReceive(ReceivedResponse response)
        {
            recHooks.Where(h => h.Status == response.Status)
                   .ToList()
                   .ForEach(h => h.Callback(response.Response));
        }

        private void CancelRefEvent()
        {
            if (string.IsNullOrEmpty(refEvent)) return;
            channel.Off(refEvent);
        }

        private void CancelTimeout()
        {
            timeoutTimer?.Dispose();
            timeoutTimer = null;
        }

        private void StartTimeout()
        {
            if (timeoutTimer != null) return;

            this.@ref = channel.Socket.MakeRef();
            this.refEvent = channel.ReplyEventName(this.@ref);

            channel.On(refEvent, payload =>
            {
                CancelRefEvent();
                CancelTimeout();
                this.receivedResp = payload as ReceivedResponse;
                MatchReceive(this.receivedResp);
            });

            timeoutTimer = new Timer(_ =>
            {
                Trigger("timeout", new { });
            }, null, timeout, Timeout.Infinite);
        }

        private bool HasReceived(string status)
        {
            return receivedResp != null && receivedResp.Status == status;
        }

        public void Trigger(string status, object response)
        {
            channel.Trigger(refEvent, new ReceivedResponse
            {
                Status = status,
                Response = response
            }, this.@ref);
        }
    }

    public class ReceiverHook
    {
        public string Status { get; set; }
        public Action<object> Callback { get; set; }
    }

    public class ReceivedResponse
    {
        public string Status { get; set; }
        public object Response { get; set; }
    }

   
   
}