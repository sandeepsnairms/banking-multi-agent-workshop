using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace MultiAgentCopilot.Services.MCP;

/// <summary>
/// AIFunction implementation for MCP tools that uses the MCP transport
/// </summary>
public class McpToolFunction : AIFunction
{
    private readonly Tool _tool;
    private readonly OAuthHttpMcpTransport _transport;
    private readonly ILogger _logger;

    /// <summary>Additional properties exposed from tools.</summary>
    private static readonly ReadOnlyDictionary<string, object?> s_additionalProperties =
        new(new Dictionary<string, object?>()
        {
            ["Strict"] = false, // some MCP schemas may not meet "strict" requirements
        });

    public McpToolFunction(Tool tool, OAuthHttpMcpTransport transport, ILogger logger)
    {
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the protocol <see cref="Tool"/> type for this instance.
    /// </summary>
    public Tool ProtocolTool => _tool;

    /// <inheritdoc/>
    public override string Name => _tool.Name;

    /// <summary>Gets the tool's title.</summary>
    public string? Title => _tool.Title ?? _tool.Annotations?.Title;

    /// <inheritdoc/>
    public override string Description => _tool.Description ?? string.Empty;

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _tool.InputSchema;

    /// <inheritdoc/>
    public override JsonElement? ReturnJsonSchema => _tool.OutputSchema;

    /// <inheritdoc/>
    public override JsonSerializerOptions JsonSerializerOptions => new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object?> AdditionalProperties => s_additionalProperties;

    /// <inheritdoc/>
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        try
        {
            // Convert AIFunctionArguments to Dictionary
            var argumentDict = new Dictionary<string, object?>();
            if (arguments != null)
            {
                foreach (var kvp in arguments)
                {
                    argumentDict[kvp.Key] = kvp.Value;
                }
            }

            // For certain tools, automatically inject required context parameters if they're missing
            await InjectMissingContextParameters(argumentDict, arguments);

            _logger.LogDebug("Calling MCP tool {ToolName} with {ArgumentCount} arguments: {Arguments}", 
                _tool.Name, argumentDict.Count, string.Join(", ", argumentDict.Keys));

            // Create the parameters for MCP call
            var parameters = new
            {
                name = _tool.Name,
                arguments = argumentDict
            };

            // Call the tool via MCP transport
            var response = await _transport.SendRequestAsync("tools/call", parameters, cancellationToken);

            // Return the result 
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call MCP tool {ToolName}", _tool.Name);
            throw;
        }
    }

    private async Task InjectMissingContextParameters(Dictionary<string, object?> argumentDict, AIFunctionArguments? originalArguments)
    {
        // For certain tools, automatically inject required context parameters if they're missing
        if ((_tool.Name == "GetUserAccounts" || _tool.Name == "GetLoggedInUser") && !argumentDict.ContainsKey("userId"))
        {
            // Try to get userId from various sources
            var userId = await TryGetContextValue("userId", originalArguments) ?? "Mark"; // fallback
            argumentDict["userId"] = userId;
            _logger.LogDebug("Injected userId parameter: {UserId}", userId);
        }

        // Similar handling for tenantId if needed
        if ((_tool.Name == "GetUserAccounts" || _tool.Name == "GetLoggedInUser") && !argumentDict.ContainsKey("tenantId"))
        {
            var tenantId = await TryGetContextValue("tenantId", originalArguments) ?? "Contoso"; // fallback
            argumentDict["tenantId"] = tenantId;
            _logger.LogDebug("Injected tenantId parameter: {TenantId}", tenantId);
        }
    }

    private async Task<string?> TryGetContextValue(string key, AIFunctionArguments? arguments)
    {
        // Try to get from arguments first
        if (arguments != null && arguments.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }

        // Try common context keys
        var contextKeys = new[] { key, $"context_{key}", $"session_{key}" };
        if (arguments != null)
        {
            foreach (var contextKey in contextKeys)
            {
                if (arguments.TryGetValue(contextKey, out var contextValue))
                {
                    return contextValue?.ToString();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a new instance of the tool but modified to return the specified name from its <see cref="Name"/> property.
    /// </summary>
    public McpToolFunction WithName(string name)
    {
        var newTool = new Tool 
        { 
            Name = name, 
            Description = _tool.Description,
            InputSchema = _tool.InputSchema,
            OutputSchema = _tool.OutputSchema,
            Title = _tool.Title,
            Annotations = _tool.Annotations
        };
        return new McpToolFunction(newTool, _transport, _logger);
    }

    /// <summary>
    /// Creates a new instance of the tool but modified to return the specified description from its <see cref="Description"/> property.
    /// </summary>
    public McpToolFunction WithDescription(string description)
    {
        var newTool = new Tool 
        { 
            Name = _tool.Name, 
            Description = description,
            InputSchema = _tool.InputSchema,
            OutputSchema = _tool.OutputSchema,
            Title = _tool.Title,
            Annotations = _tool.Annotations
        };
        return new McpToolFunction(newTool, _transport, _logger);
    }

    /// <summary>
    /// Call the tool directly and return the MCP response
    /// </summary>
    public async Task<JsonElement> CallAsync(
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            name = _tool.Name,
            arguments = arguments?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object?>()
        };

        return await _transport.SendRequestAsync("tools/call", parameters, cancellationToken);
    }
}