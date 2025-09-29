using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using System.Text;
using MCPServer.Tools;
using MCPServer.Models;
using System.ComponentModel;
using System.Reflection;

namespace MCPServer.Controllers;

/// <summary>
/// MCP SSE Controller providing Server-Sent Events support for MCP protocol
/// Also provides REST API endpoints for backward compatibility
/// </summary>
[ApiController]
[Route("mcp")]
public class McpSseController : ControllerBase
{
    private readonly BankingTools _bankingTools;
    private readonly ILogger<McpSseController> _logger;

    public McpSseController(BankingTools bankingTools, ILogger<McpSseController> logger)
    {
        _bankingTools = bankingTools;
        _logger = logger;
    }

    /// <summary>
    /// MCP Server-Sent Events endpoint for real-time communication
    /// </summary>
    [HttpGet("sse")]
    [Produces("text/event-stream")]
    public async Task GetSseStream()
    {
        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("Connection", "keep-alive");
        Response.Headers.Add("Access-Control-Allow-Origin", "*");
        Response.Headers.Add("Access-Control-Allow-Headers", "Cache-Control");

        var writer = new StreamWriter(Response.Body);
        
        try
        {
            // OAuth authentication check
            if (!IsAuthenticated(out var clientId, out var scopes))
            {
                await writer.WriteAsync("event: error\n");
                await writer.WriteAsync("data: {\"error\": \"Authentication required\"}\n\n");
                await writer.FlushAsync();
                return;
            }

            _logger.LogInformation("SSE connection established for client: {ClientId}", clientId);

            // Send initial handshake
            var handshake = new
            {
                jsonrpc = "2.0",
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new { },
                        logging = new { }
                    },
                    serverInfo = new
                    {
                        name = "Banking MCP Server",
                        version = "1.0.0"
                    }
                }
            };

            await SendSseMessage(writer, "initialize", handshake);

            // Keep connection alive and handle incoming messages
            var cancellationToken = HttpContext.RequestAborted;
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
                
                // Send periodic heartbeat
                var heartbeat = new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                await SendSseMessage(writer, "heartbeat", heartbeat);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SSE connection closed by client");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SSE stream");
            await writer.WriteAsync("event: error\n");
            await writer.WriteAsync($"data: {{\"error\": \"{ex.Message}\"}}\n\n");
            await writer.FlushAsync();
        }
    }

    /// <summary>
    /// Handle MCP protocol messages via POST (JSON-RPC 2.0)
    /// </summary>
    [HttpPost("")]
    public async Task<IActionResult> HandleMcpMessage([FromBody] JsonElement message)
    {
        try
        {
            // OAuth authentication check
            if (!IsAuthenticated(out var clientId, out var scopes))
            {
                return Unauthorized(new { error = "Authentication required" });
            }

            _logger.LogInformation("Received MCP message from client: {ClientId}", clientId);

            // Parse the JSON-RPC message
            if (!message.TryGetProperty("method", out var methodElement))
            {
                return BadRequest(new { error = "Missing method in request" });
            }

            var method = methodElement.GetString();
            var hasId = message.TryGetProperty("id", out var idElement);
            var id = hasId ? idElement.GetString() : null;

            var response = method switch
            {
                "initialize" => HandleInitialize(id),
                "tools/list" => HandleToolsList(id),
                "tools/call" => await HandleToolsCall(message, id),
                _ => CreateErrorResponse(id, -32601, "Method not found")
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MCP message");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// REST API endpoint - Lists all available MCP banking tools
    /// Provided for backward compatibility with non-SSE clients
    /// </summary>
    [HttpPost("tools/list")]
    public IActionResult ListTools()
    {
        try
        {
            // OAuth authentication check
            if (!IsAuthenticated(out var clientId, out var scopes))
            {
                return Unauthorized(new { error = "Authentication required" });
            }

            _logger.LogInformation("REST API tools list requested - ClientId: {ClientId}", clientId);

            var tools = GetAvailableTools();

            _logger.LogInformation("Listed {ToolCount} MCP banking tools via REST API", tools.Count);
            return Ok(new { tools = tools.ToArray() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing MCP tools via REST API");
            return StatusCode(500, new { error = "Internal server error listing tools" });
        }
    }

    /// <summary>
    /// REST API endpoint - Executes a specific MCP banking tool
    /// Provided for backward compatibility with non-SSE clients
    /// </summary>
    [HttpPost("tools/call")]
    public async Task<IActionResult> CallTool([FromBody] ToolCallRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Name))
            {
                return BadRequest(new { error = "Tool name is required" });
            }

            // OAuth authentication check
            if (!IsAuthenticated(out var clientId, out var scopes))
            {
                return Unauthorized(new { error = "Authentication required" });
            }

            _logger.LogInformation("REST API calling tool: {ToolName} for client: {ClientId}", request.Name, clientId);

            var result = await ExecuteTool(request.Name, request.Arguments);

            if (result == null)
            {
                _logger.LogWarning("Banking tool not found: {ToolName}", request.Name);
                return NotFound(new { error = $"Tool '{request.Name}' not found" });
            }

            var response = new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = result is string ? result : JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };

            _logger.LogInformation("Successfully executed banking tool via REST API: {ToolName}", request.Name);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling MCP tool via REST API: {ToolName}", request.Name);
            return StatusCode(500, new 
            { 
                error = "Internal server error executing tool",
                isError = true,
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error: {ex.Message}"
                    }
                }
            });
        }
    }

    private object HandleInitialize(string? id)
    {
        return new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { },
                    logging = new { }
                },
                serverInfo = new
                {
                    name = "Banking MCP Server",
                    version = "1.0.0"
                }
            }
        };
    }

    private object HandleToolsList(string? id)
    {
        var tools = GetAvailableTools();

        return new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                tools = tools.ToArray()
            }
        };
    }

    private async Task<object> HandleToolsCall(JsonElement message, string? id)
    {
        try
        {
            if (!message.TryGetProperty("params", out var paramsElement))
            {
                return CreateErrorResponse(id, -32602, "Missing params");
            }

            if (!paramsElement.TryGetProperty("name", out var nameElement))
            {
                return CreateErrorResponse(id, -32602, "Missing tool name");
            }

            var toolName = nameElement.GetString();
            var arguments = new Dictionary<string, object>();

            if (paramsElement.TryGetProperty("arguments", out var argsElement))
            {
                foreach (var prop in argsElement.EnumerateObject())
                {
                    arguments[prop.Name] = prop.Value;
                }
            }

            var result = await ExecuteTool(toolName, arguments);

            return new
            {
                jsonrpc = "2.0",
                id,
                result = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = result is string ? result : JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling tool");
            return CreateErrorResponse(id, -32603, $"Tool execution error: {ex.Message}");
        }
    }

    /// <summary>
    /// Shared method to get available tools for both SSE and REST endpoints
    /// </summary>
    private List<object> GetAvailableTools()
    {
        var tools = new List<object>();

        // Discover tools from BankingTools
        var bankingToolsMethods = typeof(BankingTools).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<DescriptionAttribute>() != null);

        foreach (var method in bankingToolsMethods)
        {
            var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
            var parameters = method.GetParameters();
            var tagsAttribute = method.GetCustomAttribute<McpToolTagsAttribute>();
            var tags = tagsAttribute?.Tags ?? new[] { "banking" };

            var inputSchema = CreateInputSchema(parameters);

            // Create the tool object with tags for filtering
            var toolObject = new Dictionary<string, object>
            {
                ["name"] = method.Name,
                ["description"] = description,
                ["inputSchema"] = inputSchema
            };

            // Add tags as additional properties for filtering
            if (tags.Length > 0)
            {
                toolObject["McpToolTags"] = string.Join(",", tags);
            }

            tools.Add(toolObject);
        }

        return tools;
    }

    /// <summary>
    /// Shared method to execute tools for both SSE and REST endpoints
    /// </summary>
    private async Task<object?> ExecuteTool(string? toolName, Dictionary<string, object>? arguments)
    {
        if (string.IsNullOrEmpty(toolName))
            return null;

        // Find and invoke the tool method
        var bankingMethod = typeof(BankingTools).GetMethod(toolName, BindingFlags.Public | BindingFlags.Instance);
        if (bankingMethod == null)
        {
            return null;
        }

        var parameters = ConvertArgumentsToParameters(bankingMethod, arguments ?? new Dictionary<string, object>());
        var task = bankingMethod.Invoke(_bankingTools, parameters);
        
        object? result = null;
        if (task is Task taskResult)
        {
            await taskResult;
            if (taskResult.GetType().IsGenericType)
            {
                result = taskResult.GetType().GetProperty("Result")?.GetValue(taskResult);
            }
            else
            {
                result = "Operation completed successfully";
            }
        }
        else
        {
            result = task;
        }

        return result;
    }

    private object CreateErrorResponse(string? id, int code, string message)
    {
        return new
        {
            jsonrpc = "2.0",
            id,
            error = new
            {
                code,
                message
            }
        };
    }

    private static async Task SendSseMessage(StreamWriter writer, string eventType, object data)
    {
        await writer.WriteAsync($"event: {eventType}\n");
        await writer.WriteAsync($"data: {JsonSerializer.Serialize(data)}\n\n");
        await writer.FlushAsync();
    }

    private bool IsAuthenticated(out string clientId, out string[] scopes)
    {
        clientId = "";
        scopes = Array.Empty<string>();

        try
        {
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            
            if (string.IsNullOrEmpty(authHeader))
            {
                Request.Headers.TryGetValue("Authorization", out var authValues);
                authHeader = authValues.FirstOrDefault();
            }

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return false;
            }

            var token = authHeader.Substring("Bearer ".Length);
            return ValidateOAuthToken(token, out clientId, out scopes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication check failed");
            return false;
        }
    }

    private static bool ValidateOAuthToken(string token, out string clientId, out string[] scopes)
    {
        clientId = "";
        scopes = Array.Empty<string>();

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
                clientId = jsonData.GetProperty("client_id").GetString() ?? "";
                scopes = jsonData.GetProperty("scopes").EnumerateArray()
                    .Select(s => s.GetString() ?? "").ToArray();
                
                return scopes.Contains("mcp:tools");
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static object CreateInputSchema(ParameterInfo[] parameters)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in parameters)
        {
            var description = param.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
            var paramType = param.ParameterType;

            var isOptional = param.HasDefaultValue || 
                           (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>)) ||
                           paramType.Name.EndsWith("?");

            var baseType = Nullable.GetUnderlyingType(paramType) ?? paramType;

            string schemaType;
            if (baseType == typeof(string))
                schemaType = "string";
            else if (baseType == typeof(int) || baseType == typeof(long))
                schemaType = "integer";
            else if (baseType == typeof(decimal) || baseType == typeof(double) || baseType == typeof(float))
                schemaType = "number";
            else if (baseType == typeof(bool))
                schemaType = "boolean";
            else if (baseType == typeof(DateTime))
                schemaType = "string";
            else
                schemaType = "object";

            properties[param.Name!] = new
            {
                type = schemaType,
                description
            };

            if (!isOptional)
            {
                required.Add(param.Name!);
            }
        }

        return new
        {
            type = "object",
            properties,
            required = required.ToArray()
        };
    }

    private static object?[] ConvertArgumentsToParameters(MethodInfo method, Dictionary<string, object> arguments)
    {
        var parameters = method.GetParameters();
        var result = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramName = param.Name!;

            if (arguments.TryGetValue(paramName, out var value))
            {
                try
                {
                    result[i] = ConvertValue(value, param.ParameterType);
                }
                catch
                {
                    result[i] = param.HasDefaultValue ? param.DefaultValue : GetDefaultValue(param.ParameterType);
                }
            }
            else
            {
                result[i] = param.HasDefaultValue ? param.DefaultValue : GetDefaultValue(param.ParameterType);
            }
        }

        return result;
    }

    private static object? ConvertValue(object value, Type targetType)
    {
        if (value == null)
            return null;

        if (targetType.IsAssignableFrom(value.GetType()))
            return value;

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is JsonElement jsonElement)
        {
            if (underlyingType == typeof(string))
                return jsonElement.GetString();
            else if (underlyingType == typeof(int))
                return jsonElement.GetInt32();
            else if (underlyingType == typeof(long))
                return jsonElement.GetInt64();
            else if (underlyingType == typeof(decimal))
                return jsonElement.GetDecimal();
            else if (underlyingType == typeof(double))
                return jsonElement.GetDouble();
            else if (underlyingType == typeof(float))
                return jsonElement.GetSingle();
            else if (underlyingType == typeof(bool))
                return jsonElement.GetBoolean();
            else if (underlyingType == typeof(DateTime))
                return jsonElement.GetDateTime();
        }

        return Convert.ChangeType(value, underlyingType);
    }

    private static object? GetDefaultValue(Type type)
    {
        if (type.IsValueType)
            return Activator.CreateInstance(type);
        return null;
    }
}

/// <summary>
/// Request model for MCP tool execution via REST API
/// </summary>
public class ToolCallRequest
{
    public string Name { get; set; } = "";
    public Dictionary<string, object>? Arguments { get; set; }
}