using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using MCPServer.Models;
using MCPServer.Tools;
using System.Security.Claims;

namespace MCPServer.Controllers;

/// <summary>
/// MCP Controller providing Model Context Protocol endpoints for banking operations
/// with OAuth 2.0 authentication
/// </summary>
[ApiController]
[Route("mcp")]
public class McpController : ControllerBase
{
    private readonly BankingTools _bankingTools;
    private readonly ILogger<McpController> _logger;

    public McpController(BankingTools bankingTools, ILogger<McpController> logger)
    {
        _bankingTools = bankingTools;
        _logger = logger;
    }

    /// <summary>
    /// Lists all available MCP banking tools
    /// </summary>
    /// <returns>Array of available banking tools with their schemas</returns>
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

            _logger.LogInformation("MCP tools list requested - ClientId: {ClientId}, Scopes: {Scopes}", 
                clientId, string.Join(", ", scopes));

            var tools = new List<object>();

            // Discover tools from BankingTools
            var bankingToolsMethods = typeof(BankingTools).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<DescriptionAttribute>() != null);

            foreach (var method in bankingToolsMethods)
            {
                var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                var parameters = method.GetParameters();
                var tags = method.GetCustomAttribute<McpToolTagsAttribute>()?.Tags ?? new[] { "banking" };

                var inputSchema = CreateInputSchema(parameters);

                tools.Add(new
                {
                    name = method.Name,
                    description,
                    tags,
                    inputSchema
                });
            }

            _logger.LogInformation("Listed {ToolCount} MCP banking tools", tools.Count);
            return Ok(new
            {
                tools = tools.ToArray()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing MCP tools");
            return StatusCode(500, new { error = "Internal server error listing tools" });
        }
    }

    /// <summary>
    /// Executes a specific MCP banking tool
    /// </summary>
    /// <param name="request">Tool execution request</param>
    /// <returns>Tool execution result</returns>
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

            _logger.LogInformation("Calling MCP tool: {ToolName} for client: {ClientId}", request.Name, clientId);

            object? result = null;

            // Try to find and invoke the tool method in BankingTools
            var bankingMethod = typeof(BankingTools).GetMethod(request.Name, BindingFlags.Public | BindingFlags.Instance);
            if (bankingMethod != null)
            {
                var parameters = ConvertArgumentsToParameters(bankingMethod, request.Arguments);
                var task = bankingMethod.Invoke(_bankingTools, parameters);
                
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
            }

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

            _logger.LogInformation("Successfully executed banking tool: {ToolName}", request.Name);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling MCP tool {ToolName}", request.Name);
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

    /// <summary>
    /// Validates OAuth authentication for the current request
    /// </summary>
    /// <param name="clientId">Extracted client ID</param>
    /// <param name="scopes">Extracted token scopes</param>
    /// <returns>True if authenticated with valid scope</returns>
    private bool IsAuthenticated(out string clientId, out string[] scopes)
    {
        clientId = "";
        scopes = Array.Empty<string>();

        try
        {
            // Get the authorization header
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            
            // Fallback: try getting from raw headers collection
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
            _logger.LogError(ex, "Authentication check failed: {ExceptionMessage}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Validates OAuth token and extracts client information
    /// </summary>
    /// <param name="token">Base64-encoded JSON token</param>
    /// <param name="clientId">Extracted client ID</param>
    /// <param name="scopes">Extracted token scopes</param>
    /// <returns>True if token is valid and has required scope</returns>
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
                
                // Check if the required scope is present
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

            // Handle nullable types
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

    private static object?[] ConvertArgumentsToParameters(MethodInfo method, Dictionary<string, object>? arguments)
    {
        arguments ??= new Dictionary<string, object>();
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

        // Handle nullable types
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
/// Request model for MCP tool execution
/// </summary>
public class ToolCallRequest
{
    public string Name { get; set; } = "";
    public Dictionary<string, object>? Arguments { get; set; }
}