using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MultiAgentCopilot.Services.MCP;

/// <summary>
/// Simple HTTP transport for MCP that supports OAuth authentication
/// </summary>
public class OAuthHttpMcpTransport : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly ILogger<OAuthHttpMcpTransport> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private int _nextId = 1;

    public OAuthHttpMcpTransport(HttpClient httpClient, Uri endpoint, ILogger<OAuthHttpMcpTransport> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<JsonElement> SendRequestAsync(
        string method,
        object? parameters = null, 
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var requestId = Interlocked.Increment(ref _nextId);
            
            // Create JSON-RPC request
            var jsonRpcRequest = new
            {
                jsonrpc = "2.0",
                id = requestId,
                method = method,
                @params = parameters ?? new { }
            };

            var jsonContent = JsonSerializer.Serialize(jsonRpcRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogDebug("Sending MCP request to {Endpoint}: {Content}", _endpoint, jsonContent);

            // Log parameter details for debugging
            if (parameters != null)
            {
                var paramType = parameters.GetType();
                _logger.LogDebug("Parameter type: {ParameterType}", paramType.Name);
                
                if (paramType.GetProperty("arguments") != null)
                {
                    var arguments = paramType.GetProperty("arguments")?.GetValue(parameters);
                    if (arguments != null)
                    {
                        var argJson = JsonSerializer.Serialize(arguments);
                        _logger.LogDebug("Arguments being sent: {Arguments}", argJson);
                    }
                    else
                    {
                        _logger.LogWarning("Arguments property is null");
                    }
                }
            }

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = content
            };
            
            httpRequest.Headers.Accept.Clear();
            httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("Received MCP response: {Content}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"HTTP request failed with status {response.StatusCode}: {responseContent}");
            }

            // Parse JSON-RPC response
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            
            if (jsonResponse.TryGetProperty("error", out var error))
            {
                var errorMessage = error.TryGetProperty("message", out var msgProp) 
                    ? msgProp.GetString() ?? "Unknown error"
                    : "Unknown error";
                throw new InvalidOperationException($"MCP request failed: {errorMessage}");
            }

            if (jsonResponse.TryGetProperty("result", out var result))
            {
                return result;
            }

            throw new InvalidOperationException("Invalid JSON-RPC response format");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}