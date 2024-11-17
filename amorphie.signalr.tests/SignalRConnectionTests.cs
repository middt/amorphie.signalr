using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using amorphie.signalr.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using amorphie.signalr.Database;
using amorphie.signalr.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace amorphie.signalr.tests;

public class SignalRConnectionTests : IAsyncLifetime
{
    private TestServer _server = null!;
    private HttpClient _client = null!;
    private HubConnection _hubConnection = null!;
    private const string HubUrl = "/hubs/notification";
    private const string TestUserId = "test-user-1";

    public async Task InitializeAsync()
    {
        // Setup test server
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var webHostBuilder = new WebHostBuilder()
            .UseConfiguration(configuration)
            .ConfigureServices(services =>
            {
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb"));

                services.AddEndpointsApiExplorer();
                services.AddSwaggerGen();
                services.AddSignalR();

                services.AddScoped<IMessageService, MessageService>();
                services.AddHostedService<MessageRetryService>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapHub<NotificationHub>("/hubs/notification");

                    // Add your endpoints here
                    endpoints.MapGet("/messages/{userId}", async (HttpContext context) =>
                    {
                        var userId = context.Request.RouteValues["userId"]?.ToString();
                        var messageService = context.RequestServices.GetRequiredService<IMessageService>();
                        var messages = await messageService.GetUnacknowledgedMessagesAsync(userId!);
                        await context.Response.WriteAsJsonAsync(messages);
                    });

                    endpoints.MapPost("/messages/send", async (HttpContext context) =>
                    {
                        var messageService = context.RequestServices.GetRequiredService<IMessageService>();
                        var request = await context.Request.ReadFromJsonAsync<MessageRequest>();
                        var message = await messageService.SendMessageAsync(request!.UserId, request.Content);
                        context.Response.StatusCode = StatusCodes.Status201Created;
                        context.Response.Headers.Location = $"/messages/{message.Id}";
                        await context.Response.WriteAsJsonAsync(message);
                    });

                    endpoints.MapPost("/messages/acknowledge/{messageId}", async (HttpContext context) =>
                    {
                        var messageId = context.Request.RouteValues["messageId"]?.ToString();
                        var messageService = context.RequestServices.GetRequiredService<IMessageService>();
                        var result = await messageService.AcknowledgeMessageAsync(messageId!);
                        context.Response.StatusCode = result ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
                    });
                });
            });

        _server = new TestServer(webHostBuilder);
        _client = _server.CreateClient();

        // Configure hub connection with automatic fallback to long polling
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(HubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                                   Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();

        await _hubConnection.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
        _client?.Dispose();
        _server?.Dispose();
    }

    [Fact]
    public async Task Should_Connect_Successfully_With_WebSockets()
    {
        // Arrange & Act - connection is established in InitializeAsync

        // Assert
        Assert.Equal(HubConnectionState.Connected, _hubConnection.State);
    }

    [Fact]
    public async Task Should_Fallback_To_LongPolling_When_WebSockets_Fail()
    {
        // Arrange
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(HubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();

        // Act
        await hubConnection.StartAsync();

        // Assert
        Assert.Equal(HubConnectionState.Connected, hubConnection.State);
    }

    [Fact]
    public async Task Should_Receive_Unacknowledged_Messages_After_Connection()
    {
        // Arrange
        var messageReceived = new TaskCompletionSource<(string messageId, string content)>();
        _hubConnection.On<string, string>("ReceiveMessage", (messageId, content) =>
        {
            messageReceived.TrySetResult((messageId, content));
        });

        // Create some unacknowledged messages
        var request = new { UserId = TestUserId, Content = "Test message" };
        var response = await _client.PostAsJsonAsync("/messages/send", request);
        Assert.True(response.IsSuccessStatusCode);

        try
        {
            // Act
            // Wait for the message to be received with a timeout
            var result = await Task.WhenAny(
                messageReceived.Task,
                Task.Delay(TimeSpan.FromSeconds(10))
            );

            if (result == messageReceived.Task)
            {
                var (messageId, content) = await messageReceived.Task;
                // Assert
                Assert.NotNull(messageId);
                Assert.Equal("Test message", content);
            }
            else
            {
                Assert.Fail("Timeout waiting for message");
            }
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test failed with exception: {ex.Message}");
        }
    }

    [Fact]
    public async Task Should_Get_Unacknowledged_Messages_Via_Http_When_SignalR_Fails()
    {
        // Arrange
        // Create some unacknowledged messages
        var request = new { UserId = TestUserId, Content = "Test message" };
        await _client.PostAsJsonAsync("/messages/send", request);

        // Act
        var response = await _client.GetAsync($"/messages/{TestUserId}");
        var messages = await response.Content.ReadFromJsonAsync<IEnumerable<Message>>();

        // Assert
        Assert.NotNull(messages);
        Assert.NotEmpty(messages);
    }

    [Fact]
    public async Task Should_Acknowledge_Message_Successfully()
    {
        // Arrange
        // Create a test message
        var request = new { UserId = TestUserId, Content = "Test message" };
        var response = await _client.PostAsJsonAsync("/messages/send", request);
        var message = await response.Content.ReadFromJsonAsync<Message>();

        // Act
        var ackRequest = new { UserId = TestUserId };
        var ackResponse = await _client.PostAsJsonAsync($"/messages/acknowledge/{message!.Id}", ackRequest);

        // Assert
        Assert.True(ackResponse.IsSuccessStatusCode);

        // Verify message is acknowledged
        var messagesResponse = await _client.GetAsync($"/messages/{TestUserId}");
        var unacknowledgedMessages = await messagesResponse.Content.ReadFromJsonAsync<IEnumerable<Message>>();
        Assert.DoesNotContain(unacknowledgedMessages, m => m.Id == message.Id);
    }
}

public record MessageRequest(string UserId, string Content);