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

```mermaid
%%{init: {'theme': 'base', 'themeVariables': { 'primaryColor': '#f0f0f0', 'primaryTextColor': '#333', 'secondaryColor': '#e0e0e0', 'tertiaryColor': '#d0d0d0' }}}%%
flowchart TB
    client[Client Application<br>Application consuming real-time notifications]
    notificationSystem[amorphie.signalr<br>Real-time notification delivery system]
    externalSystem[External Systems<br>Message producers]
    database[(Database<br>Stores messages and user data)]

    client -->|Receives notifications<br>SignalR/WebSocket/LongPolling| notificationSystem
    externalSystem -->|Sends notifications<br>HTTP/REST| notificationSystem
    notificationSystem -->|Stores/Retrieves<br>Entity Framework Core| database
```

`

### Container Diagram

```mermaid
%%{init: {'theme': 'base', 'themeVariables': { 'primaryColor': '#f0f0f0', 'primaryTextColor': '#333', 'secondaryColor': '#e0e0e0', 'tertiaryColor': '#d0d0d0' }}}%%
flowchart TB
    client[Client Application<br>Application consuming real-time notifications]
    externalSystem[External Systems<br>Message producers]

    subgraph notificationSystem[amorphie.signalr]
        api[API Application<br>ASP.NET Core<br>Handles HTTP requests]
        signalR[NotificationHub<br>SignalR Hub<br>Manages real-time connections]
        messageService[MessageService<br>C# Service<br>Message processing and persistence]
        retryService[MessageRetryService<br>Background Service<br>Handles message retries]
        database[(Database<br>In-Memory/SQL<br>Stores messages and state)]
    end

    client -->|Sends/Receives via<br>HTTP/REST| api
    client -->|Connects via<br>WebSocket/LongPolling| signalR
    externalSystem -->|Sends messages via<br>HTTP/REST| api
    api -->|Uses| messageService
    signalR -->|Uses| messageService
    messageService -->|Reads/Writes| database
    retryService -->|Monitors| database
    retryService -->|Triggers redelivery| signalR

```

### Component Diagram

```mermaid
%%{init: {'theme': 'base', 'themeVariables': { 'primaryColor': '#f0f0f0', 'primaryTextColor': '#333', 'secondaryColor': '#e0e0e0', 'tertiaryColor': '#d0d0d0' }}}%%
flowchart TB
    subgraph api[API Application]
        notificationHub[NotificationHub<br>SignalR Hub<br>Manages real-time connections and message delivery]
        messageEndpoints[Message Endpoints<br>ASP.NET Core<br>HTTP endpoints for message operations]
        messageService[MessageService<br>C# Service<br>Handles message processing and persistence]
        retryService[MessageRetryService<br>Background Service<br>Handles message retries and expiration]
        dbContext[ApplicationDbContext<br>EF Core<br>Data access layer]

        inMemoryDb[(In-Memory Database<br>EF Core InMemory<br>Stores messages and state)]
    end

    messageEndpoints -->|Uses| messageService
    notificationHub -->|Uses| messageService
    messageService -->|Uses| dbContext
    retryService -->|Uses| dbContext
    dbContext -->|Reads/Writes| inMemoryDb
    retryService -->|Triggers redelivery| notificationHub

```

### Message Flow Diagram

```mermaid
%%{init: {'theme': 'base', 'themeVariables': { 'primaryColor': '#f0f0f0', 'primaryTextColor': '#333', 'secondaryColor': '#e0e0e0', 'tertiaryColor': '#d0d0d0' }}}%%
sequenceDiagram
    participant Client
    participant API
    participant NotificationHub as Hub
    participant MessageService as Service
    participant Database as DB

    Client ->>+ API: Send Message
    API ->>+ Service: Process Message
    Service ->>+ DB: Store Message
    Service ->>+ Hub: Attempt Delivery
    Hub ->>+ Client: Deliver Message
    Client ->>+ API: Acknowledge Message
    API ->>+ Service: Process Acknowledgment
    Service ->>+ DB: Update Message Status

    box LightBlue "Background Process"
        participant RetryService as Retry
    end

    Retry ->>+ DB: Find Unacknowledged Messages
    DB ->>+ Retry: Return Messages
    loop for each unacknowledged message
        Retry ->>+ Hub: Attempt Redelivery
        Hub ->>+ Client: Deliver Message
        alt success
            Client ->>+ API: Acknowledge Message
            API ->>+ Service: Process Acknowledgment
            Service ->>+ DB: Update Message Status
        else timeout
            Retry ->>+ DB: Update Retry Count
        end
    end

```

### Message State Diagram

```mermaid
%%{init: {'theme': 'base', 'themeVariables': { 'primaryColor': '#f0f0f0', 'primaryTextColor': '#333', 'secondaryColor': '#e0e0e0', 'tertiaryColor': '#d0d0d0' }}}%%
stateDiagram
    [*] --> Created: Message Created
    Created --> Delivered: Delivery Attempted
    Delivered --> Acknowledged: Client Acknowledges
    Delivered --> RetryQueue: Delivery Failed
    RetryQueue --> Delivered: Retry Attempt
    RetryQueue --> Expired: Max Retries Exceeded or Timeout Reached
    Acknowledged --> [*]
    Expired --> [*]

    state RetryQueue {
        [*] --> Waiting
        Waiting --> RetryingDelivery: User Connected
        RetryingDelivery --> Waiting: Delivery Failed
    }

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

## License

This project is licensed under the MIT License - see the LICENSE file for details.
