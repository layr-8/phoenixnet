# PhoenixNet

A .NET client implementation for Phoenix Channels. This library enables .NET applications to communicate with Phoenix Channel servers using WebSockets.

## Features

- Full Phoenix Channels protocol support
- WebSocket connection management
- Channel presence tracking
- Automatic reconnection handling
- Heartbeat management
- Structured logging support

## Installation

Install the package via NuGet:

```bash
dotnet add package PhoenixNet
```

## Quick Start

```csharp
using PhoenixNet;
using Serilog;

// Setup logging
var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

// Initialize socket connection
var socket = new Socket("ws://your-server/socket", new SocketOptions
{
    HeartbeatIntervalMs = 30000,
    ApiKey = "your-api-key"
}, PhoenixNet.Configuration.CreateLogger(logger));

// Connect to socket
await socket.ConnectAsync();

// Join a channel
var channel = socket.Channel("room:123", new Dictionary<string, object>
{
    ["user_id"] = "user_123"
});

// Listen for messages
channel.On("new_message", response =>
{
    Console.WriteLine($"Received message: {response}");
});

// Join the channel
var join = await channel.Join();

// Send a message
var push = new Push(channel, "new_message", new { text = "Hello!" }, Constants.DefaultTimeout);
await push.Send();
```

## Features

### Socket Connection

- Automatic reconnection with configurable backoff
- Heartbeat monitoring
- Custom header support
- Query parameter support

### Channel Management

- Join/leave channels
- Message pushing and receiving
- Error handling
- Channel-specific event callbacks

### Presence Tracking

- Track online users
- Sync state between clients
- Join/leave events

## Configuration

### Socket Options

```csharp
var options = new SocketOptions
{
    HeartbeatIntervalMs = 30000,
    ApiKey = "your-api-key",
    Params = new Dictionary<string, object>
    {
        ["user_id"] = "123"
    }
};
```

### Logging

The library supports structured logging through Serilog:

```csharp
var logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

var socketLogger = PhoenixNet.Configuration.CreateLogger(logger);
```

## Requirements

- .NET 6.0 or higher
- Serilog (for logging)

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.