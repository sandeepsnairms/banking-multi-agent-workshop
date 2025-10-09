
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using MCPServer.Tools;
using MCPServer.Middleware;
using MCPServer.Services;
using MCPServer.Models.Configuration;
using Banking.Services;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

// DIAGNOSTIC: This should appear in logs if changes are applied
Console.WriteLine("🚀 STARTING MCP SERVER - UPDATED VERSION 4.0 with Simplified Architecture");
Console.WriteLine($"🕐 Startup Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

var builder = WebApplication.CreateBuilder(args);

// Add configuration sources
builder.Configuration
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

// Configure Cosmos DB settings
builder.Services.Configure<CosmosDBSettings>(
    builder.Configuration.GetSection("CosmosDBSettings"));

// Register Cosmos DB service
builder.Services.AddSingleton<CosmosDBService>();

// Register the banking service with real Cosmos DB containers
builder.Services.AddScoped<Banking.Services.BankingDataService>(serviceProvider =>
{
    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    var cosmosDBService = serviceProvider.GetRequiredService<CosmosDBService>();
    var logger = loggerFactory.CreateLogger<Program>();
    
    logger.LogInformation("Initializing BankingDataService with real Cosmos DB containers");
    
    return new Banking.Services.BankingDataService(
        database: cosmosDBService.Database,
        accountData: cosmosDBService.AccountDataContainer,
        userData: cosmosDBService.UserDataContainer,
        requestData: cosmosDBService.RequestDataContainer,
        offerData: cosmosDBService.OfferDataContainer,
        loggerFactory);
});

// Configure JSON serialization for banking models (use reflection-based)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.WriteIndented = true;
    // Use default reflection-based serialization for complex types
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
});

// Configure MCP Server with BankingTools
Console.WriteLine("🔧 DEBUG: Configuring MCP Server with BankingTools...");
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<BankingTools>();
Console.WriteLine("✅ DEBUG: MCP Server configured successfully");

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("*")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(b => b.AddMeter("*")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithLogging()
    .UseOtlpExporter();

var app = builder.Build();
Console.WriteLine("🔧 DEBUG: WebApplication built successfully");

// Add API key authentication middleware
Console.WriteLine("🔧 DEBUG: Adding API key authentication middleware...");
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

// Add a health check endpoint
Console.WriteLine("🔧 DEBUG: Adding health check endpoint...");
app.MapGet("/health", () => Results.Ok(new { 
    Status = "Healthy", 
    Timestamp = DateTime.UtcNow,
    Version = "4.0"
}));

Console.WriteLine("🔧 DEBUG: Mapping MCP endpoints...");
app.MapMcp();
Console.WriteLine("✅ DEBUG: MCP endpoints mapped successfully");

Console.WriteLine("🚀 MCP Server started successfully");
Console.WriteLine("📍 MCP endpoint available at /mcp");
Console.WriteLine("🔐 API key authentication enabled (configure in appsettings)");
Console.WriteLine("📊 OpenTelemetry tracing and metrics enabled");
Console.WriteLine("💾 Cosmos DB integration configured");
Console.WriteLine("🏦 Banking tools available for MCP clients");

// DEBUG: Try to verify service registration
using (var scope = app.Services.CreateScope())
{
    try
    {
        var bankingTools = scope.ServiceProvider.GetService<BankingTools>();
        Console.WriteLine($"🔧 DEBUG: BankingTools service resolved: {bankingTools != null}");
        
        var bankingService = scope.ServiceProvider.GetService<Banking.Services.BankingDataService>();
        Console.WriteLine($"🔧 DEBUG: BankingDataService resolved: {bankingService != null}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ DEBUG: Error checking service registration: {ex.Message}");
    }
}

Console.WriteLine("Press Ctrl+C to stop the server");

app.Run();