using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using MultiAgentCopilot.MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Models.MCP;
namespace MultiAgentCopilot.Services.MCP
{

    /// <summary>
    /// Implementation of IMcpClientService that provides MCP functionality using direct HTTP transport
    /// </summary>
    public class McpClientService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<McpClientService> _logger;
        private readonly MCPSettings.MCPServerSettings _settings;
        private bool _isInitialized = false;
        private OAuthHttpMcpTransport? _transport;

        public McpClientService(
            IHttpClientFactory httpClientFactory,
            ILoggerFactory loggerFactory,
            MCPSettings.MCPServerSettings settings)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<McpClientService>();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return true;

            try
            {
                _logger.LogInformation("Initializing MCP client service...");

                // Create HTTP client
                var httpClient = _httpClientFactory.CreateClient("MCPClient");

                // Create the MCP transport
                var endpoint = new Uri(new Uri(_settings.BaseUrl), "/mcp");
                var transportLogger = _loggerFactory.CreateLogger<OAuthHttpMcpTransport>();
                _transport = new OAuthHttpMcpTransport(httpClient, endpoint, transportLogger);

                // Test connectivity with a ping
                await _transport.SendRequestAsync("ping", null, cancellationToken);

                _isInitialized = true;
                _logger.LogInformation("MCP client service initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize MCP client service");
                _isInitialized = false;
                return false;
            }
        }

        public async Task<List<AIFunction>> GetMcpClientToolsAsync(CancellationToken cancellationToken = default)
        {
            if (!_isInitialized || _transport == null)
            {
                throw new InvalidOperationException("MCP client not initialized. Call InitializeAsync first.");
            }

            try
            {
                var response = await _transport.SendRequestAsync("tools/list", null, cancellationToken);

                var tools = new List<AIFunction>();

                if (response.TryGetProperty("tools", out var toolsArray))
                {
                    foreach (var toolElement in toolsArray.EnumerateArray())
                    {
                        try
                        {
                            var name = toolElement.GetProperty("name").GetString() ?? "";
                            var description = toolElement.TryGetProperty("description", out var descProp)
                                ? descProp.GetString() ?? ""
                                : "";

                            JsonElement inputSchema;
                            if (toolElement.TryGetProperty("inputSchema", out var schemaProp))
                            {
                                inputSchema = schemaProp;
                            }
                            else
                            {
                                inputSchema = JsonSerializer.SerializeToElement(new
                                {
                                    type = "object",
                                    properties = new { },
                                    additionalProperties = true
                                });
                            }

                            // For certain tools that require context, modify the schema to include required parameters
                            if (name == "GetUserAccounts" || name == "GetLoggedInUser")
                            {
                                inputSchema = EnhanceSchemaWithContext(inputSchema, name);
                            }

                            _logger.LogDebug("Creating MCP tool {ToolName} with schema: {Schema}", name, inputSchema.ToString());

                            var tool = new Tool
                            {
                                Name = name,
                                Description = description,
                                InputSchema = inputSchema
                            };

                            var aiFunction = new McpToolFunction(tool, _transport, _logger);
                            tools.Add(aiFunction);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to create tool from element: {ToolElement}", toolElement);
                            continue;
                        }
                    }
                }

                return tools;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get MCP tools");
                throw;
            }
        }

        private JsonElement EnhanceSchemaWithContext(JsonElement originalSchema, string toolName)
        {
            try
            {
                // Parse the original schema
                var schemaDict = JsonSerializer.Deserialize<Dictionary<string, object>>(originalSchema.GetRawText()) 
                    ?? new Dictionary<string, object>();

                // Ensure we have properties
                if (!schemaDict.ContainsKey("properties"))
                {
                    schemaDict["properties"] = new Dictionary<string, object>();
                }

                var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    JsonSerializer.Serialize(schemaDict["properties"])) ?? new Dictionary<string, object>();

                // Add userId parameter if not present (required for GetUserAccounts and GetLoggedInUser)
                if ((toolName == "GetUserAccounts" || toolName == "GetLoggedInUser") && !properties.ContainsKey("userId"))
                {
                    properties["userId"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The ID of the user"
                    };
                }

                // Add tenantId parameter if not present
                if ((toolName == "GetUserAccounts" || toolName == "GetLoggedInUser") && !properties.ContainsKey("tenantId"))
                {
                    properties["tenantId"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The tenant ID"
                    };
                }

                schemaDict["properties"] = properties;

                // Ensure required array includes the necessary parameters
                var required = new List<string>();
                if (schemaDict.ContainsKey("required"))
                {
                    var existingRequired = JsonSerializer.Deserialize<List<string>>(
                        JsonSerializer.Serialize(schemaDict["required"])) ?? new List<string>();
                    required.AddRange(existingRequired);
                }

                if ((toolName == "GetUserAccounts" || toolName == "GetLoggedInUser") && !required.Contains("userId"))
                {
                    required.Add("userId");
                }

                if ((toolName == "GetUserAccounts" || toolName == "GetLoggedInUser") && !required.Contains("tenantId"))
                {
                    required.Add("tenantId");
                }

                schemaDict["required"] = required;

                // Convert back to JsonElement
                var enhancedSchemaJson = JsonSerializer.Serialize(schemaDict);
                return JsonSerializer.Deserialize<JsonElement>(enhancedSchemaJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enhance schema for tool {ToolName}, using original schema", toolName);
                return originalSchema;
            }
        }

        public async Task<McpToolResponse> CallToolAsync(string toolName, Dictionary<string, object?>? arguments = null)
        {
            if (!_isInitialized || _transport == null)
            {
                throw new InvalidOperationException("MCP client not initialized. Call InitializeAsync first.");
            }

            try
            {
                _logger.LogInformation("Calling MCP tool: {ToolName}", toolName);

                var parameters = new
                {
                    name = toolName,
                    arguments = arguments ?? new Dictionary<string, object?>()
                };

                var response = await _transport.SendRequestAsync("tools/call", parameters);

                // Parse the JSON response to extract content
                string content = "";
                if (response.ValueKind == JsonValueKind.Object)
                {
                    if (response.TryGetProperty("content", out var contentProp))
                    {
                        if (contentProp.ValueKind == JsonValueKind.Array)
                        {
                            var items = new List<string>();
                            foreach (var item in contentProp.EnumerateArray())
                            {
                                if (item.TryGetProperty("text", out var textProp))
                                {
                                    items.Add(textProp.GetString() ?? "");
                                }
                            }
                            content = string.Join("\n", items);
                        }
                        else
                        {
                            content = contentProp.ToString();
                        }
                    }
                    else
                    {
                        content = response.ToString();
                    }
                }
                else
                {
                    content = response.ToString();
                }

                return new McpToolResponse
                {
                    IsSuccess = true,
                    Content = content,
                    RawResponse = response.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call MCP tool: {ToolName}", toolName);
                return new McpToolResponse
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    RawResponse = ex.ToString()
                };
            }
        }

        public async Task<McpResponse> PingAsync()
        {
            if (!_isInitialized || _transport == null)
            {
                return new McpResponse
                {
                    IsSuccess = false,
                    StatusCode = System.Net.HttpStatusCode.ServiceUnavailable,
                    Content = "MCP client not initialized"
                };
            }

            try
            {
                await _transport.SendRequestAsync("ping", null);
                return new McpResponse
                {
                    IsSuccess = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = "Pong"
                };
            }
            catch (Exception ex)
            {
                return new McpResponse
                {
                    IsSuccess = false,
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Content = ex.Message
                };
            }
        }    
              

        public void Dispose()
        {
            _transport?.Dispose();
        }
    }
}