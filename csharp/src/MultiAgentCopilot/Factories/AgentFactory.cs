using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Models.ChatInfoFormats;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.StructuredFormats;
using MultiAgentCopilot.Plugins;
using MultiAgentCopilot.Monitoring;
using OpenAI.Chat;
using System.Text.Json;
using System.Reflection;
using System.ComponentModel;
using static MultiAgentCopilot.StructuredFormats.ChatResponseFormatBuilder;

namespace MultiAgentCopilot.Factories
{
    public class AgentFactory
    {
        public delegate void LogCallback(string key, string value);

        private string GetAgentName(AgentType agentType)
        {
            return agentType.ToString();
        }

        public string GetAgentPrompts(AgentType agentType)
        {
            string promptFile = agentType switch
            {
                AgentType.Sales => "Sales.prompty",
                AgentType.Transactions => "Transactions.prompty",
                AgentType.CustomerSupport => "CustomerSupport.prompty",
                AgentType.Coordinator => "Coordinator.prompty",
                _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
            };

            string prompt = $"{File.ReadAllText("Prompts/" + promptFile)}{File.ReadAllText("Prompts/CommonAgentRules.prompty")}";
            return prompt;
        }

        // Create an AI agent wrapper that uses plugins
        public BankingAgent BuildAgent(AzureOpenAIClient aoClient, string deploymentName, AgentType agentType, ILoggerFactory loggerFactory, BankingDataService bankService, string tenantId, string userId)
        {
            var chatClient = aoClient.GetChatClient(deploymentName);
            
            // Create the appropriate plugin for this agent type
            BasePlugin plugin = CreatePluginForAgentType(agentType, loggerFactory, bankService, tenantId, userId);

            return new BankingAgent(
                chatClient, 
                GetAgentName(agentType), 
                GetAgentPrompts(agentType),
                agentType,
                loggerFactory.CreateLogger<BankingAgent>(),
                plugin
            );
        }

        private BasePlugin CreatePluginForAgentType(AgentType agentType, ILoggerFactory loggerFactory, BankingDataService bankService, string tenantId, string userId)
        {
            var logger = loggerFactory.CreateLogger<BasePlugin>();
            
            return agentType switch
            {
                AgentType.Sales => new SalesPlugin(logger, bankService, tenantId, userId),
                AgentType.Transactions => new TransactionPlugin(logger, bankService, tenantId, userId),
                AgentType.CustomerSupport => new CustomerSupportPlugin(logger, bankService, tenantId, userId),
                AgentType.Coordinator => new CoordinatorPlugin(logger, bankService, tenantId, userId),
                _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
            };
        }

        // Build the multi-agent orchestration system with optional monitoring
        public BankingAgentOrchestration BuildAgentOrchestration(AzureOpenAIClient aoClient, string deploymentName, ILoggerFactory loggerFactory, LogCallback logCallback, BankingDataService bankService, string tenantId, string userId, OrchestrationMonitor? monitor = null)
        {
            var orchestration = new BankingAgentOrchestration(aoClient, deploymentName, logCallback, monitor);

            // Add all agents based on AgentType enum
            foreach (AgentType agentType in Enum.GetValues<AgentType>())
            {
                var agent = BuildAgent(aoClient, deploymentName, agentType, loggerFactory, bankService, tenantId, userId);
                orchestration.AddAgent(agent);
            }

            return orchestration;
        }
    }

    // Plugin-aware agent wrapper with monitoring support
    public class BankingAgent
    {
        private readonly ChatClient _chatClient;
        private readonly ILogger<BankingAgent> _logger;
        private readonly BasePlugin _plugin;

        public string Name { get; }
        public string Instructions { get; }
        public AgentType AgentType { get; }

        public BankingAgent(ChatClient chatClient, string name, string instructions, AgentType agentType, ILogger<BankingAgent> logger, BasePlugin plugin)
        {
            _chatClient = chatClient;
            Name = name;
            Instructions = instructions;
            AgentType = agentType;
            _logger = logger;
            _plugin = plugin;
        }

        public async Task<string> ProcessAsync(List<Microsoft.Extensions.AI.ChatMessage> messages, OrchestrationMonitor? monitor = null, string? sessionId = null)
        {
            // Convert Microsoft.Extensions.AI.ChatMessage to OpenAI.Chat format
            var openAIMessages = new List<OpenAI.Chat.ChatMessage>();
            
            // Add system message with instructions
            openAIMessages.Add(OpenAI.Chat.ChatMessage.CreateSystemMessage(Instructions));
            
            foreach (var msg in messages)
            {
                var roleString = msg.Role.ToString().ToLower();
                switch (roleString)
                {
                    case "system":
                        openAIMessages.Add(OpenAI.Chat.ChatMessage.CreateSystemMessage(msg.Text));
                        break;
                    case "user":
                        openAIMessages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage(msg.Text));
                        break;
                    case "assistant":
                        openAIMessages.Add(OpenAI.Chat.ChatMessage.CreateAssistantMessage(msg.Text));
                        break;
                }
            }

            // Add tools from plugin
            var chatCompletionOptions = new ChatCompletionOptions();
            AddToolsFromPlugin(chatCompletionOptions);

            var response = await _chatClient.CompleteChatAsync(openAIMessages, chatCompletionOptions);
            
            // Handle tool calls if present
            if (response.Value.ToolCalls.Count > 0)
            {
                return await HandleToolCallsAsync(response.Value.ToolCalls, openAIMessages, monitor, sessionId);
            }

            return response.Value.Content[0].Text ?? string.Empty;
        }

        // Backward compatibility method
        public async Task<string> ProcessAsync(List<Microsoft.Extensions.AI.ChatMessage> messages)
        {
            return await ProcessAsync(messages, null, null);
        }

        private void AddToolsFromPlugin(ChatCompletionOptions options)
        {
            // Use reflection to discover methods with Description attributes
            var pluginType = _plugin.GetType();
            var methods = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<DescriptionAttribute>() != null)
                .ToList();

            foreach (var method in methods)
            {
                var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? method.Name;
                var tool = CreateToolFromMethod(method, description);
                options.Tools.Add(tool);
            }
        }

        private ChatTool CreateToolFromMethod(MethodInfo method, string description)
        {
            var parameters = method.GetParameters();
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            foreach (var param in parameters)
            {
                var paramName = param.Name!;
                var paramType = param.ParameterType;
                
                // Handle different parameter types
                var propDef = new Dictionary<string, object>
                {
                    ["type"] = GetJsonSchemaType(paramType),
                    ["description"] = GetParameterDescription(param, paramType)
                };

                // Handle enums with better descriptions
                if (paramType.IsEnum)
                {
                    var enumNames = Enum.GetNames(paramType);
                    var enumValues = Enum.GetValues(paramType);
                    propDef["enum"] = enumNames;
                    
                    // Add enum value descriptions
                    var enumDescriptions = new List<string>();
                    for (int i = 0; i < enumNames.Length; i++)
                    {
                        var enumValue = enumValues.GetValue(i);
                        enumDescriptions.Add($"{enumNames[i]} ({(int)enumValue!})");
                    }
                    propDef["description"] += $". Valid values: {string.Join(", ", enumDescriptions)}";
                }

                // Handle nullable and optional parameters
                if (!param.HasDefaultValue && !IsNullableType(paramType))
                {
                    required.Add(paramName);
                }

                properties[paramName] = propDef;
            }

            var schema = new
            {
                type = "object",
                properties = properties,
                required = required.ToArray()
            };

            return ChatTool.CreateFunctionTool(
                method.Name,
                description,
                BinaryData.FromString(JsonSerializer.Serialize(schema))
            );
        }

        private static string GetParameterDescription(ParameterInfo param, Type paramType)
        {
            var baseName = param.Name ?? "parameter";
            var typeName = paramType.IsEnum ? $"{paramType.Name} enum" : paramType.Name;
            
            if (param.HasDefaultValue)
            {
                return $"{baseName} ({typeName}, optional, default: {param.DefaultValue})";
            }
            
            if (IsNullableType(paramType))
            {
                return $"{baseName} ({typeName}, optional)";
            }
            
            return $"{baseName} ({typeName}, required)";
        }

        private static string GetJsonSchemaType(Type type)
        {
            // Handle nullable types
            if (IsNullableType(type))
            {
                type = Nullable.GetUnderlyingType(type) ?? type;
            }

            return Type.GetTypeCode(type) switch
            {
                TypeCode.Boolean => "boolean",
                TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or 
                TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => "integer",
                TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "number",
                TypeCode.DateTime => "string",
                TypeCode.String => "string",
                _ when type.IsEnum => "string",
                _ when type == typeof(Dictionary<string, string>) => "object",
                _ => "string"
            };
        }

        private static bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        private async Task<string> HandleToolCallsAsync(IReadOnlyList<ChatToolCall> toolCalls, List<OpenAI.Chat.ChatMessage> messages, OrchestrationMonitor? monitor = null, string? sessionId = null)
        {
            var toolMessages = new List<OpenAI.Chat.ChatMessage>();

            foreach (var toolCall in toolCalls)
            {
                var toolStopwatch = System.Diagnostics.Stopwatch.StartNew();
                string toolResult = string.Empty;
                bool toolSuccessful = false;
                string? toolError = null;

                try
                {
                    toolResult = await ExecutePluginMethodAsync(toolCall);
                    toolSuccessful = true;
                    toolMessages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage($"Tool result: {toolResult}"));
                }
                catch (Exception ex)
                {
                    toolError = ex.Message;
                    _logger.LogError(ex, "Error executing tool call {ToolName}", toolCall.FunctionName);
                    toolMessages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage($"Tool error: {ex.Message}"));
                }
                finally
                {
                    toolStopwatch.Stop();
                    
                    // Log tool execution to monitor
                    if (monitor != null && !string.IsNullOrEmpty(sessionId))
                    {
                        monitor.LogToolExecution(sessionId, Name, toolCall.FunctionName, toolStopwatch.Elapsed, toolSuccessful, toolError);
                    }
                }
            }

            // Add tool call and results to conversation
            messages.AddRange(toolMessages);

            // Get final response
            var response = await _chatClient.CompleteChatAsync(messages);
            return response.Value.Content[0].Text ?? string.Empty;
        }

        private async Task<string> ExecutePluginMethodAsync(ChatToolCall toolCall)
        {
            var methodName = toolCall.FunctionName;
            
            try
            {
                var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(toolCall.FunctionArguments);

                _logger.LogTrace("Executing plugin method {MethodName} with arguments: {Arguments}", 
                    methodName, toolCall.FunctionArguments);

                // Find the method on the plugin
                var method = _plugin.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                if (method == null)
                {
                    throw new InvalidOperationException($"Method {methodName} not found on plugin {_plugin.GetType().Name}");
                }

                // Convert arguments to the expected types
                var parameters = method.GetParameters();
                var args = new object?[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    var paramName = param.Name!;
                    
                    try
                    {
                        if (arguments.TryGetValue(paramName, out var argValue))
                        {
                            _logger.LogTrace("Converting parameter {ParamName} of type {ParamType} from JSON: {JsonValue}", 
                                paramName, param.ParameterType.Name, argValue.GetRawText());
                            
                            args[i] = ConvertJsonElementToType(argValue, param.ParameterType);
                        }
                        else if (param.HasDefaultValue)
                        {
                            args[i] = param.DefaultValue;
                        }
                        else if (IsNullableType(param.ParameterType))
                        {
                            args[i] = null;
                        }
                        else
                        {
                            throw new ArgumentException($"Required parameter {paramName} not provided");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to convert parameter {ParamName} of type {ParamType}. JSON value: {JsonValue}", 
                            paramName, param.ParameterType.Name, arguments.TryGetValue(paramName, out var val) ? val.GetRawText() : "missing");
                        throw;
                    }
                }

                // Invoke the method
                _logger.LogTrace("Invoking method {MethodName} on {PluginType}", methodName, _plugin.GetType().Name);
                var result = method.Invoke(_plugin, args);
                
                // Handle async methods
                if (result is Task task)
                {
                    await task;
                    
                    // Get the result if it's a Task<T>
                    if (task.GetType().IsGenericType)
                    {
                        var property = task.GetType().GetProperty("Result");
                        result = property?.GetValue(task);
                    }
                    else
                    {
                        result = "Operation completed successfully";
                    }
                }

                var jsonResult = JsonSerializer.Serialize(result);
                _logger.LogTrace("Method {MethodName} executed successfully, result length: {ResultLength}", 
                    methodName, jsonResult.Length);
                
                return jsonResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute plugin method {MethodName}. Arguments: {Arguments}", 
                    methodName, toolCall.FunctionArguments);
                throw;
            }
        }

        private static object? ConvertJsonElementToType(JsonElement element, Type targetType)
        {
            try
            {
                if (IsNullableType(targetType))
                {
                    if (element.ValueKind == JsonValueKind.Null)
                        return null;
                    targetType = Nullable.GetUnderlyingType(targetType)!;
                }

                // Handle enums specially - they can come as strings or numbers
                if (targetType.IsEnum)
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        var stringValue = element.GetString()!;
                        // Try to parse as enum name first
                        if (Enum.TryParse(targetType, stringValue, true, out var enumResult))
                            return enumResult;
                        
                        // If that fails, check if it's a number in string format
                        if (int.TryParse(stringValue, out var intValue))
                            return Enum.ToObject(targetType, intValue);
                        
                        throw new ArgumentException($"Cannot convert '{stringValue}' to enum {targetType.Name}");
                    }
                    else if (element.ValueKind == JsonValueKind.Number)
                    {
                        return Enum.ToObject(targetType, element.GetInt32());
                    }
                    else
                    {
                        throw new ArgumentException($"Enum values must be string or number, got {element.ValueKind}");
                    }
                }

                return Type.GetTypeCode(targetType) switch
                {
                    TypeCode.Boolean => element.ValueKind == JsonValueKind.String ? 
                        bool.Parse(element.GetString()!) : element.GetBoolean(),
                    TypeCode.Byte => element.ValueKind == JsonValueKind.String ? 
                        byte.Parse(element.GetString()!) : element.GetByte(),
                    TypeCode.SByte => element.ValueKind == JsonValueKind.String ? 
                        sbyte.Parse(element.GetString()!) : element.GetSByte(),
                    TypeCode.Int16 => element.ValueKind == JsonValueKind.String ? 
                        short.Parse(element.GetString()!) : element.GetInt16(),
                    TypeCode.UInt16 => element.ValueKind == JsonValueKind.String ? 
                        ushort.Parse(element.GetString()!) : element.GetUInt16(),
                    TypeCode.Int32 => element.ValueKind == JsonValueKind.String ? 
                        int.Parse(element.GetString()!) : element.GetInt32(),
                    TypeCode.UInt32 => element.ValueKind == JsonValueKind.String ? 
                        uint.Parse(element.GetString()!) : element.GetUInt32(),
                    TypeCode.Int64 => element.ValueKind == JsonValueKind.String ? 
                        long.Parse(element.GetString()!) : element.GetInt64(),
                    TypeCode.UInt64 => element.ValueKind == JsonValueKind.String ? 
                        ulong.Parse(element.GetString()!) : element.GetUInt64(),
                    TypeCode.Single => element.ValueKind == JsonValueKind.String ? 
                        float.Parse(element.GetString()!) : element.GetSingle(),
                    TypeCode.Double => element.ValueKind == JsonValueKind.String ? 
                        double.Parse(element.GetString()!) : element.GetDouble(),
                    TypeCode.Decimal => element.ValueKind == JsonValueKind.String ? 
                        decimal.Parse(element.GetString()!) : element.GetDecimal(),
                    TypeCode.DateTime => element.ValueKind == JsonValueKind.String ? 
                        DateTime.Parse(element.GetString()!) : element.GetDateTime(),
                    TypeCode.String => element.GetString(),
                    _ when targetType == typeof(Dictionary<string, string>) => 
                        JsonSerializer.Deserialize<Dictionary<string, string>>(element.GetRawText()),
                    _ => element.GetString()
                };
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to convert JSON element of type {element.ValueKind} to {targetType.Name}. Value: {element.GetRawText()}", ex);
            }
        }
    }

    // Multi-agent orchestration system with integrated monitoring
    public class BankingAgentOrchestration
    {
        private readonly AzureOpenAIClient _aoClient;
        private readonly string _deploymentName;
        private readonly AgentFactory.LogCallback _logCallback;
        private readonly List<BankingAgent> _agents;
        private readonly ChatClient _orchestratorClient;
        private readonly OrchestrationMonitor? _monitor;

        public BankingAgentOrchestration(AzureOpenAIClient aoClient, string deploymentName, AgentFactory.LogCallback logCallback, OrchestrationMonitor? monitor = null)
        {
            _aoClient = aoClient;
            _deploymentName = deploymentName;
            _logCallback = logCallback;
            _agents = new List<BankingAgent>();
            _orchestratorClient = aoClient.GetChatClient(deploymentName);
            _monitor = monitor;
        }

        public void AddAgent(BankingAgent agent)
        {
            _agents.Add(agent);
            _logCallback("AGENT_ADDED", $"Added {agent.Name} agent with {agent.AgentType} capabilities");
        }

        public List<string> GetAvailableAgents()
        {
            return _agents.Select(a => a.Name).ToList();
        }

        // Enhanced to return agent responses with their names and debug information
        public async Task<(List<(string Response, string AgentName)> Responses, List<ContinuationInfo> SelectionInfo, List<TerminationInfo> TerminationInfo)> ProcessConversationAsync(List<Microsoft.Extensions.AI.ChatMessage> messages, string? sessionId = null)
        {
            var responses = new List<(string Response, string AgentName)>();
            var selectionInfo = new List<ContinuationInfo>();
            var terminationInfo = new List<TerminationInfo>();
            var conversationHistory = new List<Microsoft.Extensions.AI.ChatMessage>(messages);
            var maxIterations = 8;
            var iteration = 0;

            // Start monitoring if sessionId is provided
            if (_monitor != null && !string.IsNullOrEmpty(sessionId))
            {
                _monitor.StartSession(sessionId, "default", "default", GetAvailableAgents());
            }

            while (iteration < maxIterations)
            {
                iteration++;

                // Select next agent and capture selection info with timing
                var selectionStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var (selectedAgent, selectionDetails) = await SelectNextAgentWithDetailsAsync(conversationHistory);
                selectionStopwatch.Stop();

                if (selectedAgent == null)
                {
                    _logCallback("SELECTION", "No agent selected");
                    break;
                }

                // Store selection information
                if (selectionDetails != null)
                {
                    selectionInfo.Add(selectionDetails);
                    
                    // Log to monitor
                    if (_monitor != null && !string.IsNullOrEmpty(sessionId))
                    {
                        _monitor.LogAgentSelection(sessionId, selectionDetails, selectionStopwatch.Elapsed);
                    }
                }

                _logCallback("SELECTION - Agent", selectedAgent.Name);

                // Get response from selected agent with timing
                var responseStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await selectedAgent.ProcessAsync(conversationHistory, _monitor, sessionId);
                responseStopwatch.Stop();
                
                // Check if response includes tool calls (simplified detection)
                bool hasToolCalls = response.Contains("Tool result:") || response.Contains("Tool error:");
                
                // Log to monitor
                if (_monitor != null && !string.IsNullOrEmpty(sessionId))
                {
                    _monitor.LogAgentResponse(sessionId, selectedAgent.Name, response, responseStopwatch.Elapsed, hasToolCalls);
                }
                
                // Track both response and agent name
                responses.Add((response, selectedAgent.Name));

                // Add response to history
                conversationHistory.Add(new Microsoft.Extensions.AI.ChatMessage(
                    Microsoft.Extensions.AI.ChatRole.Assistant,
                    response
                ));

                // Check if conversation should terminate and capture termination info with timing
                var terminationStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var (shouldTerminate, terminationDetails) = await ShouldTerminateWithDetailsAsync(conversationHistory);
                terminationStopwatch.Stop();

                if (terminationDetails != null)
                {
                    terminationInfo.Add(terminationDetails);
                    
                    // Log to monitor
                    if (_monitor != null && !string.IsNullOrEmpty(sessionId))
                    {
                        _monitor.LogTerminationDecision(sessionId, terminationDetails, terminationStopwatch.Elapsed);
                    }
                }

                if (shouldTerminate)
                {
                    _logCallback("TERMINATION", "Conversation complete");
                    break;
                }
            }

            // End monitoring session if it was started
            if (_monitor != null && !string.IsNullOrEmpty(sessionId))
            {
                var sessionData = _monitor.EndSession(sessionId);
                if (sessionData != null)
                {
                    _logCallback("MONITORING", $"Session completed: {sessionData.Metrics.TotalResponses} responses, {sessionData.Duration.TotalSeconds:F1}s duration");
                }
            }

            return (responses, selectionInfo, terminationInfo);
        }

        private async Task<(BankingAgent?, ContinuationInfo?)> SelectNextAgentWithDetailsAsync(List<Microsoft.Extensions.AI.ChatMessage> conversationHistory)
        {
            var selectionPrompt = File.ReadAllText("Prompts/SelectionStrategy.prompty");
            var historyText = string.Join("\n", conversationHistory.TakeLast(5).Select(m => $"{m.Role}: {m.Text}"));

            var selectionMessages = new List<OpenAI.Chat.ChatMessage>
            {
                OpenAI.Chat.ChatMessage.CreateSystemMessage(selectionPrompt),
                OpenAI.Chat.ChatMessage.CreateUserMessage($"Chat history:\n{historyText}")
            };

            try
            {
                var response = await _orchestratorClient.CompleteChatAsync(selectionMessages, new ChatCompletionOptions
                {
                    ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
                        "agent_selection",
                        BinaryData.FromString(ChatResponseFormatBuilder.BuildFormat(ChatResponseStrategy.Continuation))
                    )
                });

                var selectionResult = JsonSerializer.Deserialize<ContinuationInfo>(response.Value.Content[0].Text!);
                _logCallback("SELECTION - Reason", selectionResult!.Reason);

                var selectedAgent = _agents.FirstOrDefault(a => 
                    string.Equals(a.Name, selectionResult.AgentName, StringComparison.OrdinalIgnoreCase));

                return (selectedAgent, selectionResult);
            }
            catch (Exception ex)
            {
                _logCallback("SELECTION - Error", ex.Message);
                // Fallback to Coordinator
                var fallbackAgent = _agents.FirstOrDefault(a => a.AgentType == AgentType.Coordinator);
                var fallbackInfo = new ContinuationInfo 
                { 
                    AgentName = fallbackAgent?.Name ?? "Coordinator", 
                    Reason = $"Fallback due to selection error: {ex.Message}" 
                };
                return (fallbackAgent, fallbackInfo);
            }
        }

        private async Task<(bool, TerminationInfo?)> ShouldTerminateWithDetailsAsync(List<Microsoft.Extensions.AI.ChatMessage> conversationHistory)
        {
            var terminationPrompt = File.ReadAllText("Prompts/TerminationStrategy.prompty");
            var historyText = string.Join("\n", conversationHistory.TakeLast(3).Select(m => $"{m.Role}: {m.Text}"));

            var terminationMessages = new List<OpenAI.Chat.ChatMessage>
            {
                OpenAI.Chat.ChatMessage.CreateSystemMessage(terminationPrompt),
                OpenAI.Chat.ChatMessage.CreateUserMessage($"Chat history:\n{historyText}")
            };

            try
            {
                var response = await _orchestratorClient.CompleteChatAsync(terminationMessages, new ChatCompletionOptions
                {
                    ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
                        "termination_decision",
                        BinaryData.FromString(ChatResponseFormatBuilder.BuildFormat(ChatResponseStrategy.Termination))
                    )
                });

                var terminationResult = JsonSerializer.Deserialize<TerminationInfo>(response.Value.Content[0].Text!);
                _logCallback("TERMINATION - Reason", terminationResult!.Reason);
                _logCallback("TERMINATION - Continue", terminationResult.ShouldContinue.ToString());

                return (!terminationResult.ShouldContinue, terminationResult);
            }
            catch (Exception ex)
            {
                _logCallback("TERMINATION - Error", ex.Message);
                var errorInfo = new TerminationInfo 
                { 
                    ShouldContinue = true, 
                    Reason = $"Error in termination check: {ex.Message}" 
                };
                // Default to continue if there's an error
                return (false, errorInfo);
            }
        }

        // Legacy method for backwards compatibility
        private async Task<BankingAgent?> SelectNextAgentAsync(List<Microsoft.Extensions.AI.ChatMessage> conversationHistory)
        {
            var (agent, _) = await SelectNextAgentWithDetailsAsync(conversationHistory);
            return agent;
        }

        // Legacy method for backwards compatibility  
        private async Task<bool> ShouldTerminateAsync(List<Microsoft.Extensions.AI.ChatMessage> conversationHistory)
        {
            var (shouldTerminate, _) = await ShouldTerminateWithDetailsAsync(conversationHistory);
            return shouldTerminate;
        }

        /// <summary>
        /// Get current session analytics if monitoring is enabled
        /// </summary>
        public SessionAnalytics? GetSessionAnalytics(string sessionId)
        {
            return _monitor?.GetSessionAnalytics(sessionId);
        }

        /// <summary>
        /// Get overall orchestration statistics if monitoring is enabled
        /// </summary>
        public OrchestrationStatistics? GetOrchestrationStatistics()
        {
            return _monitor?.GetOverallStatistics();
        }
    }
}

