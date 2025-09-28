using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore;
using MCPServer.Tools;
using MCPServer.Models;
using MCPServer.Services;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.ComponentModel;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Authentication;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Configure URLs - let ASP.NET Core handle port assignment, but prefer 5000
builder.WebHost.UseUrls("http://localhost:5000", "http://localhost:0");

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

// Configure Authentication with custom OAuth scheme
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "CustomOAuth";
    options.DefaultChallengeScheme = "CustomOAuth";
})
.AddScheme<CustomOAuthSchemeOptions, CustomOAuthHandler>("CustomOAuth", options => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("McpTools", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", "mcp:tools"));
});

// Add controllers for OAuth endpoints and MCP endpoints
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddHttpContextAccessor();

// Register MCP tools for dependency injection
builder.Services.AddSingleton<IBankingService, MockBankingService>();
builder.Services.AddSingleton<BankingTools>();

// Add API Explorer and Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Banking MCP Server API", 
        Version = "v1",
        Description = "A Model Context Protocol (MCP) Server for banking operations with OAuth 2.0 JWT Bearer authentication."
    });
    
    c.AddSecurityDefinition("OAuth2", new OpenApiSecurityScheme
    {
        Description = "OAuth 2.0 Bearer token. Get token from /oauth/token endpoint.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "OAuth2"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Banking MCP Server API V1");
        c.RoutePrefix = string.Empty;
        c.DocumentTitle = "Banking MCP Server API Documentation";
        c.DefaultModelsExpandDepth(-1);
    });
}

// Disable HTTPS redirection for OAuth testing
// app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Map controllers (including OAuth endpoints and the new MCP controller)
app.MapControllers();

// Add health check endpoint
app.MapGet("/health", () => new 
{ 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    server_name = McpProtocol.Name,
    server_version = McpProtocol.ServerVersion,
    implementation = McpProtocol.Implementation,
    description = McpProtocol.Description
})
.WithOpenApi()
.WithTags("Health")
.WithSummary("Server health check");

// Display startup information
Console.WriteLine("Starting Banking MCP server with OAuth 2.0 authentication...");
Console.WriteLine("OAuth 2.0 endpoints:");
Console.WriteLine("  - POST /oauth/token - Get OAuth access token");
Console.WriteLine("");
Console.WriteLine("MCP Protocol endpoints:");
Console.WriteLine("  - POST /mcp/tools/list - List available MCP tools");
Console.WriteLine("  - POST /mcp/tools/call - Execute MCP tools");
Console.WriteLine("  - All endpoints require valid OAuth Bearer token");
Console.WriteLine("");
Console.WriteLine("Available tools:");
Console.WriteLine("  - BankingTools: Search offers, get account details, transaction history, service requests");
Console.WriteLine("");

// Start the server and display the actual URLs
app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
    if (addresses != null)
    {
        Console.WriteLine("Server is running on:");
        foreach (var address in addresses)
        {
            Console.WriteLine($"  - {address}");
            Console.WriteLine($"  - Swagger UI: {address}");
        }
    }
    Console.WriteLine("");
    Console.WriteLine("Press Ctrl+C to stop the server");
});

app.Run();

// Custom OAuth Authentication Handler Options
public class CustomOAuthSchemeOptions : AuthenticationSchemeOptions
{
}

// Custom OAuth Authentication Handler
public class CustomOAuthHandler : AuthenticationHandler<CustomOAuthSchemeOptions>
{
    private readonly ILogger<CustomOAuthHandler> _logger;

    public CustomOAuthHandler(IOptionsMonitor<CustomOAuthSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _logger = logger.CreateLogger<CustomOAuthHandler>();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            // Extract token from Authorization header
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            
            if (string.IsNullOrEmpty(authHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
            
            if (!authHeader.StartsWith("Bearer "))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var token = authHeader.Substring("Bearer ".Length);
            
            if (ValidateOAuthToken(token, out var tokenData))
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, tokenData.ClientId),
                    new("client_id", tokenData.ClientId),
                    new("tenant_id", "default-tenant"),
                    new("scope", string.Join(" ", tokenData.Scopes))
                };
                
                // Add individual scope claims
                foreach (var scope in tokenData.Scopes)
                {
                    claims.Add(new("scope", scope));
                }
                
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                
                _logger.LogInformation("Token validated for client: {ClientId}", tokenData.ClientId);
                
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            else
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid token"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation failed");
            return Task.FromResult(AuthenticateResult.Fail("Token validation failed"));
        }
    }

    private bool ValidateOAuthToken(string token, out TokenData tokenData)
    {
        tokenData = null!;
        
        try
        {
            var tokenBytes = Convert.FromBase64String(token);
            var tokenJson = System.Text.Encoding.UTF8.GetString(tokenBytes);
            var jsonData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

            var expiresAt = jsonData.GetProperty("expires_at").GetInt64();
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var isExpired = currentTime > expiresAt;
            
            if (!isExpired)
            {
                tokenData = new TokenData
                {
                    ClientId = jsonData.GetProperty("client_id").GetString() ?? "",
                    Scopes = jsonData.GetProperty("scopes").EnumerateArray()
                        .Select(s => s.GetString() ?? "").ToArray(),
                    IssuedAt = jsonData.GetProperty("issued_at").GetInt64(),
                    ExpiresAt = jsonData.GetProperty("expires_at").GetInt64()
                };
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation error");
            return false;
        }
    }
}

public class TokenData
{
    public string ClientId { get; set; } = "";
    public string[] Scopes { get; set; } = Array.Empty<string>();
    public long IssuedAt { get; set; }
    public long ExpiresAt { get; set; }
}