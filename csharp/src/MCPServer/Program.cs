
using MCPServer.Models;
using MCPServer.Authentication;
using MCPServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Banking.Services;
using MCPServer.Tools;
using System.ComponentModel;
using System.Reflection;
using MCPServer.Models.Configuration;

// DIAGNOSTIC: This should appear in logs if changes are applied
Console.WriteLine("🚀 STARTING MCP SERVER - UPDATED VERSION 3.0 with Real BankingDataService and Cosmos DB");
Console.WriteLine($"🕐 Startup Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

var builder = WebApplication.CreateBuilder(args);

// Configure Cosmos DB settings
builder.Services.Configure<CosmosDBSettings>(
    builder.Configuration.GetSection("CosmosDBSettings"));

// Register Cosmos DB service
builder.Services.AddSingleton<CosmosDBService>();

// Get the actual server URL from Kestrel configuration or default
var serverUrl = builder.Configuration["Kestrel:Endpoints:Http:Url"] ?? "http://localhost:5000";
if (!serverUrl.EndsWith("/"))
    serverUrl += "/";

var inMemoryOAuthServerUrl = "https://localhost:7029";

// Configure OAuth settings
builder.Services.Configure<OAuthSettings>(
    builder.Configuration.GetSection("OAuthSettings"));


// Register the banking service with real Cosmos DB containers (same pattern as ChatService.cs)
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

// Register BankingTools which depends on BankingDataService
builder.Services.AddScoped<BankingTools>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Configure to validate tokens from our in-memory OAuth server
    options.Authority = inMemoryOAuthServerUrl;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudience = serverUrl, // Validate that the audience matches the resource metadata as suggested in RFC 8707
        ValidIssuer = inMemoryOAuthServerUrl,
        NameClaimType = "name",
        RoleClaimType = "roles"
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var name = context.Principal?.Identity?.Name ?? "unknown";
            var email = context.Principal?.FindFirstValue("preferred_username") ?? "unknown";
            Console.WriteLine($"JWT Token validated for: {name} ({email})");
            
            // Add auth_type claim to distinguish from server-to-server
            var claims = new List<Claim> { new("auth_type", "user_jwt") };
            var identity = new ClaimsIdentity(claims);
            context.Principal?.AddIdentity(identity);
            
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            // Check if the token looks like a Base64 JSON token (not a JWT)
            var token = context.Token;
            if (!string.IsNullOrEmpty(token) && !token.Contains('.'))
            {
                // This is likely a Base64 JSON token, skip JWT validation
                Console.WriteLine("Detected non-JWT token, skipping JWT validation");
                context.NoResult();
                return Task.CompletedTask;
            }
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"JWT Authentication failed: {context.Exception.Message}");
            context.NoResult();
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine($"Challenging client to authenticate with Entra ID");
            return Task.CompletedTask;
        }
    };
})
.AddScheme<ServerToServerAuthenticationOptions, ServerToServerAuthenticationHandler>(
    ServerToServerAuthenticationOptions.DefaultScheme, options => { })
.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        Resource = new Uri(serverUrl),
        ResourceDocumentation = new Uri("https://docs.example.com/api/weather"),
        AuthorizationServers = { new Uri(inMemoryOAuthServerUrl) },
        ScopesSupported = ["mcp:tools"],
    };
});

builder.Services.AddAuthorization(options =>
{
    // Policy for server-to-server authentication only
    options.AddPolicy(AuthorizationPolicies.ServerToServerPolicy, policy =>
    {
        policy.AddAuthenticationSchemes(ServerToServerAuthenticationOptions.DefaultScheme);
        policy.AddRequirements(new ServerToServerRequirement());
    });

    // Policy for both user JWT and server-to-server authentication
    options.AddPolicy(AuthorizationPolicies.UserOrServerPolicy, policy =>
    {
        policy.AddAuthenticationSchemes(
            JwtBearerDefaults.AuthenticationScheme,
            ServerToServerAuthenticationOptions.DefaultScheme);
        policy.AddRequirements(new UserOrServerRequirement());
    });

    // Policy requiring mcp:tools scope
    options.AddPolicy(AuthorizationPolicies.McpToolsScope, policy =>
    {
        policy.AddAuthenticationSchemes(
            JwtBearerDefaults.AuthenticationScheme,
            ServerToServerAuthenticationOptions.DefaultScheme);
        policy.AddRequirements(new McpToolsScopeRequirement());
    });

    // Default policy uses both authentication schemes
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(
            JwtBearerDefaults.AuthenticationScheme,
            ServerToServerAuthenticationOptions.DefaultScheme)
        .AddRequirements(new UserOrServerRequirement())
        .Build();
});

// Register authorization handlers
builder.Services.AddScoped<IAuthorizationHandler, ServerToServerAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, UserOrServerAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, McpToolsScopeAuthorizationHandler>();

builder.Services.AddHttpContextAccessor();

// Banking service is already registered above

// Add HTTP client for server-to-server token service
builder.Services.AddHttpClient<IServerToServerTokenService, ServerToServerTokenService>();
builder.Services.AddScoped<IServerToServerTokenService, ServerToServerTokenService>();

// Add controllers for OAuth endpoints
builder.Services.AddControllers();

// Add MCP server with explicit configuration (SINGLE REGISTRATION)
Console.WriteLine("=== Configuring MCP Server ===");
try 
{
    // Try alternative registration approach
    var mcpBuilder = builder.Services.AddMcpServer();
    mcpBuilder.WithTools<BankingTools>();
    mcpBuilder.WithHttpTransport();
    
    Console.WriteLine("✓ MCP Server configured successfully with BankingTools");
    Console.WriteLine("✓ HTTP transport enabled");
    
    // Try to register MCP services manually as fallback
    Console.WriteLine("🔍 Attempting manual MCP service registration...");
    
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error configuring MCP Server: {ex.Message}");
    Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
}
Console.WriteLine("================================");

var app = builder.Build();

// Validate MCP services are registered
Console.WriteLine("=== MCP Service Validation ===");
using (var scope = app.Services.CreateScope())
{
    try
    {
        var bankingTools = scope.ServiceProvider.GetService<BankingTools>();
        Console.WriteLine($"BankingTools registration: {(bankingTools != null ? "✓ Success" : "❌ Failed")}");
        
        var bankingService = scope.ServiceProvider.GetService<Banking.Services.BankingDataService>();
        Console.WriteLine($"BankingDataService registration: {(bankingService != null ? "✓ Success" : "❌ Failed")}");
        Console.WriteLine($"BankingDataService implementation: {bankingService?.GetType().Name ?? "N/A"}");
        
        // Try to test a banking tools method
        if (bankingTools != null)
        {
            // Test reflection-based tool discovery
            var toolsInfo = GetBankingToolsInfo();
            Console.WriteLine($"Banking tools discovery: {(toolsInfo.Length > 0 ? "✓ Success" : "❌ Failed")} - Found {toolsInfo.Length} tools");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error validating services: {ex.Message}");
        Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
    }
}
Console.WriteLine("==============================");

// Add detailed logging to see what's happening BEFORE other middleware
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp"))
    {
        Console.WriteLine($"=== MCP Request Debug ===");
        Console.WriteLine($"Path: {context.Request.Path}");
        Console.WriteLine($"Method: {context.Request.Method}");
        Console.WriteLine($"Content-Type: {context.Request.ContentType}");
        Console.WriteLine($"Accept: {context.Request.Headers.Accept.FirstOrDefault() ?? "None"}");
        Console.WriteLine($"Authorization Header: {context.Request.Headers.Authorization.FirstOrDefault() ?? "None"}");
        Console.WriteLine($"User Authenticated: {context.User?.Identity?.IsAuthenticated ?? false}");
        
        // Read and log the request body
        if (context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering();
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            context.Request.Body.Position = 0;
            Console.WriteLine($"Request Body: {body}");
        }
        Console.WriteLine($"========================");
    }
    
    await next();
    
    // Log response details
    if (context.Request.Path.StartsWithSegments("/mcp"))
    {
        Console.WriteLine($"=== MCP Response Debug ===");
        Console.WriteLine($"Status Code: {context.Response.StatusCode}");
        Console.WriteLine($"Content-Type: {context.Response.ContentType ?? "None"}");
        Console.WriteLine($"==========================");
    }
});

app.UseAuthentication();
app.UseAuthorization();

// Map OAuth controllers
app.MapControllers();

// Add a test endpoint to verify MCP framework
app.MapGet("/mcp-test", () => {
    return Results.Ok(new { 
        Status = "MCP Server is running",
        Timestamp = DateTime.Now,
        Framework = "ModelContextProtocol.AspNetCore"
    });
});

// Configure MCP endpoints with explicit path - temporarily remove auth for testing
// app.MapMcp("/mcp"); // COMMENTED OUT - Using custom endpoint instead

// Add a custom JSON-RPC endpoint as fallback for MCP
app.MapPost("/mcp", async (HttpContext context) =>
{
    Console.WriteLine("🔍 Custom MCP endpoint hit!");
    
    try
    {
        // Read the request body
        using var reader = new StreamReader(context.Request.Body);
        var requestBody = await reader.ReadToEndAsync();
        Console.WriteLine($"📥 Request: {requestBody}");
        
        // Parse JSON-RPC request
        var request = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(requestBody);
        var method = request.GetProperty("method").GetString();
        var id = request.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 1;
        
        Console.WriteLine($"🎯 Method: {method}, ID: {id}");
        
        // Handle different MCP methods
        object response = method switch
        {
            "tools/list" => new
            {
                jsonrpc = "2.0",
                id = id,
                result = new
                {
                    tools = GetBankingToolsInfo()
                }
            },
            "tools/call" => await HandleToolCall(request, context),
            "ping" => new { jsonrpc = "2.0", id = id, result = new { } },
            _ => new { jsonrpc = "2.0", id = id, error = new { code = -32601, message = $"Method '{method}' is not available." } }
        };
        
        // Send response
        context.Response.ContentType = "application/json";
        var responseJson = System.Text.Json.JsonSerializer.Serialize(response);
        Console.WriteLine($"📤 Response: {responseJson}");
        await context.Response.WriteAsync(responseJson);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error in custom MCP endpoint: {ex.Message}");
        var errorResponse = new
        {
            jsonrpc = "2.0",
            id = 1,
            error = new { code = -32603, message = "An error occurred." }
        };
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
    }
});

Console.WriteLine($"Starting MCP server with authorization at {serverUrl}");
Console.WriteLine($"Using in-memory OAuth server at {inMemoryOAuthServerUrl}");
Console.WriteLine($"Protected Resource Metadata URL: {serverUrl}.well-known/oauth-protected-resource");
Console.WriteLine($"OAuth Token Endpoint: {serverUrl}oauth/token");
Console.WriteLine($"MCP Endpoint: {serverUrl}mcp");
Console.WriteLine($"Server-to-Server Authentication: Enabled");
Console.WriteLine("Supported authentication methods:");
Console.WriteLine("  1. JWT Bearer tokens from Entra ID (user authentication)");
Console.WriteLine("  2. Server-to-server OAuth tokens (client credentials flow)");
Console.WriteLine("Supported transport types:");
Console.WriteLine("  - HTTP Transport (POST to /mcp endpoint)");
Console.WriteLine("Press Ctrl+C to stop the server");
Console.WriteLine();

// Helper method to get tool information from BankingTools class using reflection
object[] GetBankingToolsInfo()
{
    var toolsList = new List<object>();
    var bankingToolsType = typeof(BankingTools);
    
    foreach (var method in bankingToolsType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
    {
        // Skip inherited methods from Object class
        if (method.DeclaringType != bankingToolsType)
            continue;
            
        var descriptionAttribute = method.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .FirstOrDefault() as DescriptionAttribute;
            
        if (descriptionAttribute != null)
        {
            var parameters = method.GetParameters()
                .Where(p => p.GetCustomAttributes(typeof(DescriptionAttribute), false).Any())
                .Select(p => new
                {
                    name = p.Name,
                    type = GetParameterTypeName(p.ParameterType),
                    description = (p.GetCustomAttributes(typeof(DescriptionAttribute), false)
                        .FirstOrDefault() as DescriptionAttribute)?.Description ?? "",
                    required = !p.HasDefaultValue
                })
                .ToArray();
                
            toolsList.Add(new
            {
                name = method.Name,
                description = descriptionAttribute.Description,
                inputSchema = new
                {
                    type = "object",
                    properties = parameters.ToDictionary(
                        p => p.name,
                        p => new { type = p.type, description = p.description }
                    ),
                    required = parameters.Where(p => p.required).Select(p => p.name).ToArray()
                }
            });
        }
    }
    
    return toolsList.ToArray();
}

// Helper method to map .NET types to JSON schema types
string GetParameterTypeName(Type type)
{
    // Handle nullable reference types by checking if it's a string
    if (type == typeof(string))
        return "string";
    if (type == typeof(int) || type == typeof(int?) || type == typeof(long) || type == typeof(long?))
        return "integer";
    if (type == typeof(bool) || type == typeof(bool?))
        return "boolean";
    if (type == typeof(double) || type == typeof(double?) || type == typeof(decimal) || type == typeof(decimal?) || type == typeof(float) || type == typeof(float?))
        return "number";
    if (type == typeof(DateTime) || type == typeof(DateTime?))
        return "string"; // ISO date string
    
    return "string"; // Default to string for complex types
}

// Helper method to handle tool calls
async Task<object> HandleToolCall(JsonElement request, HttpContext context)
{
    var id = request.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 1;
    
    try
    {
        var parameters = request.GetProperty("params");
        var toolName = parameters.GetProperty("name").GetString();
        var arguments = parameters.TryGetProperty("arguments", out var argsProp) ? argsProp : new JsonElement();
        
        Console.WriteLine($"🛠️ Calling tool: {toolName}");
        
        // Get the banking tools from DI
        using var scope = app.Services.CreateScope();
        var bankingTools = scope.ServiceProvider.GetRequiredService<BankingTools>();
        
        // Use reflection to find and invoke the method
        var method = typeof(BankingTools).GetMethod(toolName);
        if (method == null)
        {
            return new
            {
                jsonrpc = "2.0",
                id = id,
                error = new { code = -32601, message = $"Tool '{toolName}' not found." }
            };
        }
        
        // Prepare method parameters
        var methodParams = method.GetParameters();
        var args = new object[methodParams.Length];
        
        for (int i = 0; i < methodParams.Length; i++)
        {
            var param = methodParams[i];
            var paramName = param.Name;
            
            if (arguments.TryGetProperty(paramName, out var argValue))
            {
                // Convert the JSON value to the expected parameter type
                args[i] = ConvertJsonToType(argValue, param.ParameterType);
            }
            else if (param.HasDefaultValue)
            {
                args[i] = param.DefaultValue;
            }
            else if (param.ParameterType.IsGenericType && param.ParameterType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                args[i] = null;
            }
            else
            {
                // Required parameter missing
                return new
                {
                    jsonrpc = "2.0",
                    id = id,
                    error = new { code = -32602, message = $"Missing required parameter: {paramName}" }
                };
            }
        }
        
        // Invoke the method
        var result = method.Invoke(bankingTools, args);
        
        // Handle async methods
        if (result is Task task)
        {
            await task;
            
            // Get the result from Task<T>
            if (task.GetType().IsGenericType)
            {
                var resultProperty = task.GetType().GetProperty("Result");
                result = resultProperty?.GetValue(task);
            }
            else
            {
                result = null; // Task without return value
            }
        }
        
        // Serialize the result to JSON string
        string resultText = result switch
        {
            null => "No result",
            string str => str,
            _ => JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
        };
        
        return new
        {
            jsonrpc = "2.0",
            id = id,
            result = new { content = new[] { new { type = "text", text = resultText } } }
        };
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Tool call error: {ex.Message}");
        return new
        {
            jsonrpc = "2.0",
            id = id,
            error = new { code = -32603, message = $"Error calling tool: {ex.Message}" }
        };
    }
}

// Helper method to convert JSON values to .NET types
object? ConvertJsonToType(JsonElement jsonValue, Type targetType)
{
    try
    {
        // Handle nullable types
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            if (jsonValue.ValueKind == JsonValueKind.Null)
                return null;
            targetType = Nullable.GetUnderlyingType(targetType)!;
        }
        
        return targetType.Name switch
        {
            nameof(String) => jsonValue.GetString(),
            nameof(Int32) => jsonValue.GetInt32(),
            nameof(Int64) => jsonValue.GetInt64(),
            nameof(Boolean) => jsonValue.GetBoolean(),
            nameof(Double) => jsonValue.GetDouble(),
            nameof(Decimal) => jsonValue.GetDecimal(),
            nameof(Single) => jsonValue.GetSingle(),
            nameof(DateTime) => jsonValue.GetDateTime(),
            _ => JsonSerializer.Deserialize(jsonValue, targetType)
        };
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Warning: Could not convert JSON value to {targetType.Name}: {ex.Message}");
        return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
    }
}

app.Run();