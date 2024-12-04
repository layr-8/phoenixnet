using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using  PhoenixNet.Models;
using Serilog;

namespace  PhoenixNet
{
    public class Channel
    {
        private readonly string topic;
        private readonly Dictionary<string, object> parameters;
        private readonly Socket socket;
        private readonly List<Binding> bindings;
        private readonly int timeout;
        private string state;
        private bool joinedOnce;
        private readonly Push joinPush;
        private readonly List<Push> pushBuffer;
        private readonly Timer rejoinTimer;
        private readonly ILogger _logger = Log.ForContext<Socket>();

        public string Topic => topic;
        public Socket Socket => socket;

        public Channel(string topic, Dictionary<string, object> parameters, Socket socket)
        {
            this.topic = topic;
            this.parameters = parameters;
            this.socket = socket;
            this.bindings = new List<Binding>();
            this.timeout = Constants.DefaultTimeout;
            this.state = Constants.ChannelStates.Closed;
            this.joinedOnce = false;
            this.pushBuffer = new List<Push>();

            this.joinPush = new Push(this, Constants.ChannelEvents.Join, parameters, timeout);
            
            this.rejoinTimer = new Timer(async _ =>
            {
                if (socket.IsConnected())
                {
                    await Rejoin();
                }
            }, null, Timeout.Infinite, Timeout.Infinite);

            SetupDefaultHooks();
        }

       

        public string ReplyEventName(string @ref)
        {
            return $"chan_reply_{@ref}";
        }

       

        private void SetupDefaultHooks()
        {
            joinPush.Receive("ok", _ =>
            {
                state = Constants.ChannelStates.Joined;
                rejoinTimer.Change(Timeout.Infinite, Timeout.Infinite);
                pushBuffer.ForEach(push => push.Send());
                pushBuffer.Clear();
            });

            OnClose(() =>
            {
                rejoinTimer.Change(Timeout.Infinite, Timeout.Infinite);
                state = Constants.ChannelStates.Closed;
                socket.Remove(this);
            });

            OnError(reason =>
            {
                if (IsLeaving() || IsClosed()) return;
                state = Constants.ChannelStates.Errored;
                rejoinTimer.Change(timeout, Timeout.Infinite);
            });
        }

        public async Task<Push> Join(int? timeout = null)
        {
            if (joinedOnce)
            {
                throw new Exception("tried to join multiple times");
            }
            
            joinedOnce = true;
            await Rejoin(timeout ?? this.timeout);
            return joinPush;
        }

        public void OnClose(Action callback) => On(Constants.ChannelEvents.Close, _ => callback());
        public void OnError(Action<object> callback) => On(Constants.ChannelEvents.Error, payload => callback(payload));
        public void On(string @event, Action<object> callback) => bindings.Add(new Binding { Event = @event, Callback = callback });
        public void Off(string @event) => bindings.RemoveAll(bind => bind.Event == @event);

        public bool IsMember(string topic) => this.topic == topic;
       
        public void Trigger(string @event, object payload, string @ref)
        {
            var handledPayload = OnMessage(@event, payload, @ref);
            if (payload != null && handledPayload == null)
            {
                throw new Exception("channel onMessage callbacks must return the payload, modified or unmodified");
            }

            bindings.Where(bind => bind.Event == @event)
                   .ToList()
                   .ForEach(bind => bind.Callback(handledPayload));
        }

        protected virtual object OnMessage(string @event, object payload, string @ref) => payload;

        private bool IsClosed() => state == Constants.ChannelStates.Closed;
        private bool IsErrored() => state == Constants.ChannelStates.Errored;
        private bool IsJoined() => state == Constants.ChannelStates.Joined;
        private bool IsJoining() => state == Constants.ChannelStates.Joining;
        private bool IsLeaving() => state == Constants.ChannelStates.Leaving;

        private async Task Rejoin(int? timeout = null)
        {
            if (IsLeaving()) return;
            await SendJoin(timeout ?? this.timeout);
        }

        private async Task SendJoin(int timeout)
        {
            state = Constants.ChannelStates.Joining;
            await joinPush.Resend(timeout);
        }
    }
}

