using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Linq;
using PhoenixNet.Models;
using Serilog;
using PhoenixNet.Logging;

namespace  PhoenixNet
{
    public class Socket
    {
        private readonly string endPoint;
        private readonly SocketOptions options;
        private ClientWebSocket conn;
        private readonly List<Channel> channels;
        private readonly List<Action> sendBuffer;
        private int refCount;
        private Timer heartbeatTimer;
        private string pendingHeartbeatRef;
        private readonly Timer reconnectTimer;
        private readonly Dictionary<string, List<Action<object>>> stateChangeCallbacks;
        private readonly ILoggerAdapter _logger;

        public Socket(string endPoint, SocketOptions options = null)
        {
            this.endPoint = endPoint;
            this.options = options ?? new SocketOptions();
            this.channels = new List<Channel>();
            this.sendBuffer = new List<Action>();
            this.refCount = 0;
            this.stateChangeCallbacks = new Dictionary<string, List<Action<object>>>
            {
                {"open", new List<Action<object>>()},
                {"close", new List<Action<object>>()},
                {"error", new List<Action<object>>()},
                {"message", new List<Action<object>>()}
            };

            this.reconnectTimer = new Timer(async _ =>
            {
                await DisconnectAsync();
                await ConnectAsync();
            }, null, Timeout.Infinite, Timeout.Infinite);
        }

        public Socket(string endPoint, ILoggerAdapter logger, SocketOptions options)
        : this(endPoint, options)  // Call the original constructor first
        {
            _logger = logger;
        }
        public async Task ConnectAsync()
        {
            if (conn != null) return;
            conn = new ClientWebSocket();
            var uri = new UriBuilder(endPoint);

            var query = HttpUtility.ParseQueryString(uri.Query);
            query["vsn"] = Constants.VSN;
            foreach (var param in options.Params)
            {
                query[param.Key] = param.Value?.ToString();
            }
            uri.Query = query.ToString();

            

            try
            {
                await conn.ConnectAsync(uri.Uri, CancellationToken.None);
                OnConnOpen();
                await StartReceiveLoop();
            }
            catch (Exception ex)
            {
                OnConnError(ex);
            }
        }
        public async Task DisconnectAsync(int? code = null, string reason = null)
        {
            _logger.Debug(code.HasValue ? $"Disconnecting with code: {code}" : "Disconnecting...");
           
            if (conn == null) return;

            if (code.HasValue)
            {
                await conn.CloseAsync((WebSocketCloseStatus)code, reason ?? string.Empty, CancellationToken.None);
            }
            else
            {
                await conn.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            }

            conn = null;
        }

        public void Remove(Channel channel)
        {
            channels.RemoveAll(c => c.Topic == channel.Topic);
        }


        public Channel Channel(string topic, Dictionary<string, object> parameters = null)
        {
            var chan = new Channel(topic, parameters ?? new Dictionary<string, object>(), this);
            channels.Add(chan);
            return chan;
        }

        public string MakeRef()
        {
            var newRef = refCount + 1;
            refCount = newRef == refCount ? 0 : newRef;
            return refCount.ToString();
        }

        public async Task PushAsync(object payload)
        {
            if (IsConnected())
            {
                var json = JsonSerializer.Serialize(payload);
                var bytes = Encoding.UTF8.GetBytes(json);
                await conn.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                sendBuffer.Add(async () => await PushAsync(payload));
            }
        }


        private async Task StartReceiveLoop()
        {
            try
            {
                _logger.Debug("Starting receive loop...");
                while (conn?.State == WebSocketState.Open)
                {
                    _logger.Debug("Waiting for message...");
                    
                    // Create a memory stream to accumulate message parts
                    using var messageStream = new System.IO.MemoryStream();
                    var buffer = new byte[16 * 1024]; // 16KB buffer for each read
                    WebSocketReceiveResult result;
                    
                    // Keep receiving until we get a complete message
                    do
                    {
                        result = await conn.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        
                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            await messageStream.WriteAsync(buffer, 0, result.Count);
                        }
                    } 
                    while (!result.EndOfMessage);
                    
                    _logger.Debug($"Received message type: {result.MessageType}");

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // Get full message bytes
                        var messageBytes = messageStream.ToArray();
                        var message = Encoding.UTF8.GetString(messageBytes);
                        _logger.Debug($"Raw message received (length: {message.Length}): {message}");
                        OnConnMessage(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.Debug("Received close message");
                        OnConnClose(null);
                        break;
                    }
                }
                _logger.Debug($"Receive loop ended. Connection state: {conn?.State}");
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error in receive loop: {ex.Message}");
                OnConnError(ex);
            }
        }

        private void OnConnOpen()
        {
            FlushSendBuffer();
            ResetReconnectTimer();
            StartHeartbeat();
            foreach (var callback in stateChangeCallbacks["open"])
            {
                callback(null);
            }
        }

        private void OnConnClose(object ev)
        {
            TriggerChanError();
            ClearHeartbeat();
            reconnectTimer.Change(options.ReconnectAfterMs(1), Timeout.Infinite);
            foreach (var callback in stateChangeCallbacks["close"])
            {
                callback(ev);
            }
        }

        private void OnConnError(object error)
        {
            TriggerChanError();
            foreach (var callback in stateChangeCallbacks["error"])
            {
                callback(error);
            }
        }

        private void OnConnMessage(string rawMessage)
        {
            var message = JsonSerializer.Deserialize<Message>(rawMessage);

            _logger.Debug(rawMessage);

            if (message.Ref == pendingHeartbeatRef)
            {
                pendingHeartbeatRef = null;
            }

            channels.Where(channel => channel.IsMember(message.Topic))
                   .ToList()
                   .ForEach(channel => channel.Trigger(message.Event, message.Payload, message.Ref));

            foreach (var callback in stateChangeCallbacks["message"])
            {
                callback(message);
            }
        }

        public bool IsConnected()
        {
            var isConnected = conn?.State == WebSocketState.Open;
            _logger.Debug($"Connection state check: {conn?.State}, IsConnected: {isConnected}");
            return isConnected;
        }

        private void FlushSendBuffer()
        {
            if (IsConnected() && sendBuffer.Any())
            {
                foreach (var callback in sendBuffer)
                {
                    callback();
                }
                sendBuffer.Clear();
            }
        }
        private void StartHeartbeat()
        {
            if (options.HeartbeatIntervalMs > 0)
            {
                heartbeatTimer = new Timer(async _ => await SendHeartbeat(),
                    null,
                    options.HeartbeatIntervalMs,
                    options.HeartbeatIntervalMs);
            }
        }

        public void OnOpen(Action<object> callback)
        {
            stateChangeCallbacks["open"].Add(callback);
        }

        public void OnClose(Action<object> callback)
        {
            stateChangeCallbacks["close"].Add(callback);
        }

        public void OnError(Action<object> callback)
        {
            stateChangeCallbacks["error"].Add(callback);
        }

        public void OnMessage(Action<object> callback)
        {
            stateChangeCallbacks["message"].Add(callback);
        }

        private async Task SendHeartbeat()
        {
            if (!IsConnected()) return;

            if (pendingHeartbeatRef != null)
            {
                _logger.Debug("Heartbeat timeout...");
                pendingHeartbeatRef = null;
                await conn.CloseAsync(WebSocketCloseStatus.NormalClosure, "heartbeat timeout", CancellationToken.None);
                return;
            }

            _logger.Debug("Sending heartbeat...");
            pendingHeartbeatRef = MakeRef();
            _logger.Debug($"Pending heartbeat ref: {pendingHeartbeatRef}");
            await PushAsync(new
            {
                topic = "phoenix",
                @event = "heartbeat",
                payload = new { },
                @ref = pendingHeartbeatRef
            });
        }

        private void ClearHeartbeat() => heartbeatTimer?.Dispose();
        private void ResetReconnectTimer() => reconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
        private void TriggerChanError() => channels.ForEach(channel => channel.Trigger(Constants.ChannelEvents.Error, null, null));
    }

    public class SocketOptions
    {
        public int HeartbeatIntervalMs { get; set; } = Constants.DefaultHeartbeatInterval;
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();

        public Func<int, int> ReconnectAfterMs { get; set; } = tries =>
        {
            return new[] { 1000, 2000, 5000, 10000 }[Math.Min(tries - 1, 3)];
        };
    }
}