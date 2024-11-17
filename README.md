# amorphie.signalr - Real-time Notification System

## Overview

amorphie.signalr is a real-time notification system built using ASP.NET Core SignalR. It provides reliable message delivery with automatic retries, acknowledgment tracking, and fallback mechanisms.

## Architecture

### Core Components

1. **NotificationHub**

   - Manages real-time connections
   - Handles user connection/disconnection
   - Tracks connected users
   - Manages message delivery

2. **MessageService**

   - Handles message persistence
   - Manages message acknowledgments
   - Provides message retrieval
   - Implements retry logic

3. **MessageRetryService**

   - Background service for message redelivery
   - Handles expired messages
   - Implements retry policies
   - Monitors message states

4. **Data Model**
   - Messages with tracking capabilities
   - User management
   - Connection state management

### Key Features

- Real-time message delivery
- Automatic fallback to long polling
- Message persistence
- Delivery acknowledgment
- Automatic retry mechanism
- Message expiration handling
- Connection state management

### Technical Stack

- ASP.NET Core 8.0
- SignalR
- Entity Framework Core
- In-Memory Database (configurable)
- xUnit for testing

## C4 Diagrams

### System Context Diagram

```plantuml

```

### Container Diagram

```plantuml
@startuml C4_Container
!include https://raw.githubusercontent.com/plantuml-stdlib/C4-PlantUML/master/C4_Container.puml

LAYOUT_WITH_LEGEND()

Person(client, "Client Application", "Application consuming real-time notifications")
System_Ext(externalSystem, "External Systems", "Message producers")

System_Boundary(notificationSystem, "amorphie.signalr") {
    Container(api, "API Application", "ASP.NET Core", "Handles HTTP requests")
    Container(signalR, "NotificationHub", "SignalR Hub", "Manages real-time connections")
    Container(messageService, "MessageService", "C# Service", "Message processing and persistence")
    Container(retryService, "MessageRetryService", "Background Service", "Handles message retries")
    ContainerDb(database, "Database", "In-Memory/SQL", "Stores messages and state")
}

Rel(client, api, "Sends/Receives via", "HTTP/REST")
Rel(client, signalR, "Connects via", "WebSocket/LongPolling")
Rel(externalSystem, api, "Sends messages via", "HTTP/REST")
Rel(api, messageService, "Uses")
Rel(signalR, messageService, "Uses")
Rel(messageService, database, "Reads/Writes")
Rel(retryService, database, "Monitors")
Rel(retryService, signalR, "Triggers redelivery")

@enduml
```

### Component Diagram

```plantuml
@startuml C4_Component
!include https://raw.githubusercontent.com/plantuml-stdlib/C4-PlantUML/master/C4_Component.puml

LAYOUT_WITH_LEGEND()

Container_Boundary(api, "API Application") {
    Component(notificationHub, "NotificationHub", "SignalR Hub", "Manages real-time connections and message delivery")
    Component(messageEndpoints, "Message Endpoints", "ASP.NET Core", "HTTP endpoints for message operations")
    Component(messageService, "MessageService", "C# Service", "Handles message processing and persistence")
    Component(retryService, "MessageRetryService", "Background Service", "Handles message retries and expiration")
    Component(dbContext, "ApplicationDbContext", "EF Core", "Data access layer")

    ComponentDb(inMemoryDb, "In-Memory Database", "EF Core InMemory", "Stores messages and state")
}

Rel(messageEndpoints, messageService, "Uses")
Rel(notificationHub, messageService, "Uses")
Rel(messageService, dbContext, "Uses")
Rel(retryService, dbContext, "Uses")
Rel(dbContext, inMemoryDb, "Reads/Writes")
Rel(retryService, notificationHub, "Triggers redelivery")

@enduml
```

### Message Flow Diagram

```plantuml
@startuml Message_Flow
!include https://raw.githubusercontent.com/plantuml-stdlib/C4-PlantUML/master/C4_Sequence.puml

actor Client
participant "API" as API
participant "NotificationHub" as Hub
participant "MessageService" as Service
database "Database" as DB

== Message Sending ==
Client -> API: Send Message
API -> Service: Process Message
Service -> DB: Store Message
Service -> Hub: Attempt Delivery
Hub -> Client: Deliver Message
Client -> API: Acknowledge Message
API -> Service: Process Acknowledgment
Service -> DB: Update Message Status

== Message Retry ==
box "Background Process" #LightBlue
    participant "RetryService" as Retry
end box

Retry -> DB: Find Unacknowledged Messages
DB -> Retry: Return Messages
loop for each unacknowledged message
    Retry -> Hub: Attempt Redelivery
    Hub -> Client: Deliver Message
    alt success
        Client -> API: Acknowledge Message
        API -> Service: Process Acknowledgment
        Service -> DB: Update Message Status
    else timeout
        Retry -> DB: Update Retry Count
    end
end

@enduml
```

### Message State Diagram

```plantuml
@startuml Message_State
!include https://raw.githubusercontent.com/plantuml-stdlib/C4-PlantUML/master/C4_State.puml

[*] --> Created: Message Created
Created --> Delivered: Delivery Attempted
Delivered --> Acknowledged: Client Acknowledges
Delivered --> RetryQueue: Delivery Failed
RetryQueue --> Delivered: Retry Attempt
RetryQueue --> Expired: Max Retries Exceeded\nor Timeout Reached
Acknowledged --> [*]
Expired --> [*]

state RetryQueue {
    [*] --> Waiting
    Waiting --> RetryingDelivery: User Connected
    RetryingDelivery --> Waiting: Delivery Failed
}

@enduml
```

## Implementation Details

### Message Configuration

```json
{
	"MessageSettings": {
		"DefaultMaxRetryAttempts": 3,
		"DefaultMessageTimeout": "24:00:00",
		"RetryInterval": "00:01:00"
	}
}
```

### Message Properties

```csharp
public class Message
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string Content { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public int RetryAttempts { get; set; }
    public int MaxRetryAttempts { get; set; }
    public TimeSpan MessageTimeout { get; set; }
    public bool IsExpired => !IsAcknowledged && DateTime.UtcNow > Timestamp.Add(MessageTimeout);
}
```

## Usage

### Client Connection

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://your-server/hubs/notification")
    .WithAutomaticReconnect()
    .Build();

connection.On<string, string>("ReceiveMessage", (messageId, content) => {
    Console.WriteLine($"Message received: {content}");
    // Acknowledge message
    await connection.InvokeAsync("AcknowledgeMessage", messageId);
});

await connection.StartAsync();
```

### Sending Messages

```csharp
// Via HTTP
await httpClient.PostAsJsonAsync("/messages/send", new {
    UserId = "user123",
    Content = "Hello!"
});

// Via Hub
await hubConnection.InvokeAsync("SendMessage", "user123", "Hello!");
```

### Message Acknowledgment

```csharp
// Via HTTP
await httpClient.PostAsync($"/messages/acknowledge/{messageId}", null);

// Via Hub
await hubConnection.InvokeAsync("AcknowledgeMessage", messageId);
```

## Testing

The project includes comprehensive tests covering:

- Connection scenarios
- Message delivery
- Retry mechanisms
- Fallback behavior
- Error handling

Run tests using:

```bash
dotnet test
```

## Error Handling

The system implements robust error handling:

- Connection failures trigger automatic reconnection
- Failed deliveries are retried based on configuration
- Messages expire after configured timeout
- All operations are logged for monitoring
- Fallback to long polling when WebSocket fails

## Monitoring

The system provides comprehensive monitoring capabilities:

- Detailed logging of all operations
- Connection state tracking
- Message delivery status
- Retry attempt monitoring
- Message expiration tracking

## Performance Considerations

- Uses async/await for all I/O operations
- Implements efficient message queuing
- Optimized database queries
- Configurable retry intervals
- Connection pooling
- Message batching where appropriate

## Security

- User authentication via headers
- Connection validation
- Message ownership verification
- Secure WebSocket connections
- Input validation
- Rate limiting capabilities

## Deployment

1. Configure settings in appsettings.json
2. Set up your database
3. Deploy using standard ASP.NET Core deployment procedures
4. Configure monitoring and logging
5. Set up health checks

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.
