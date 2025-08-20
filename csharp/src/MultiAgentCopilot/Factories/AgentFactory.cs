using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Models.ChatInfoFormats;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.StructuredFormats;
using MultiAgentCopilot.Plugins;
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

        // Build the multi-agent orchestration system
        public BankingAgentOrchestration BuildAgentGroupChat(AzureOpenAIClient aoClient, string deploymentName, ILoggerFactory loggerFactory, LogCallback logCallback, BankingDataService bankService, string tenantId, string userId)
        {
            var orchestration = new BankingAgentOrchestration(aoClient, deploymentName, logCallback);

            // Add all agents based on AgentType enum
            foreach (AgentType agentType in Enum.GetValues<AgentType>())
            {
                var agent = BuildAgent(aoClient, deploymentName, agentType, loggerFactory, bankService, tenantId, userId);
                orchestration.AddAgent(agent);
            }

            return orchestration;
        }
    }

    // Plugin-aware agent wrapper
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

        public async Task<string> ProcessAsync(List<Microsoft.Extensions.AI.ChatMessage> messages)
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
                return await HandleToolCallsAsync(response.Value.ToolCalls, openAIMessages);
            }

            return response.Value.Content[0].Text ?? string.Empty;
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
                    ["description"] = $"Parameter {paramName} of type {paramType.Name}"
                };

                // Handle enums
                if (paramType.IsEnum)
                {
                    propDef["enum"] = Enum.GetNames(paramType);
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

        private async Task<string> HandleToolCallsAsync(IReadOnlyList<ChatToolCall> toolCalls, List<OpenAI.Chat.ChatMessage> messages)
        {
            var toolMessages = new List<OpenAI.Chat.ChatMessage>();

            foreach (var toolCall in toolCalls)
            {
                try
                {
                    var result = await ExecutePluginMethodAsync(toolCall);
                    toolMessages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage($"Tool result: {result}"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing tool call {ToolName}", toolCall.FunctionName);
                    toolMessages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage($"Tool error: {ex.Message}"));
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
            var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(toolCall.FunctionArguments);

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
                
                if (arguments.TryGetValue(paramName, out var argValue))
                {
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

            // Invoke the method
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

            return JsonSerializer.Serialize(result);
        }

        private static object? ConvertJsonElementToType(JsonElement element, Type targetType)
        {
            if (IsNullableType(targetType))
            {
                if (element.ValueKind == JsonValueKind.Null)
                    return null;
                targetType = Nullable.GetUnderlyingType(targetType)!;
            }

            return Type.GetTypeCode(targetType) switch
            {
                TypeCode.Boolean => element.GetBoolean(),
                TypeCode.Byte => element.GetByte(),
                TypeCode.SByte => element.GetSByte(),
                TypeCode.Int16 => element.GetInt16(),
                TypeCode.UInt16 => element.GetUInt16(),
                TypeCode.Int32 => element.GetInt32(),
                TypeCode.UInt32 => element.GetUInt32(),
                TypeCode.Int64 => element.GetInt64(),
                TypeCode.UInt64 => element.GetUInt64(),
                TypeCode.Single => element.GetSingle(),
                TypeCode.Double => element.GetDouble(),
                TypeCode.Decimal => element.GetDecimal(),
                TypeCode.DateTime => element.GetDateTime(),
                TypeCode.String => element.GetString(),
                _ when targetType.IsEnum => Enum.Parse(targetType, element.GetString()!),
                _ when targetType == typeof(Dictionary<string, string>) => 
                    JsonSerializer.Deserialize<Dictionary<string, string>>(element.GetRawText()),
                _ => element.GetString()
            };
        }
    }

    // Multi-agent orchestration system (unchanged from previous implementation)
    public class BankingAgentOrchestration
    {
        private readonly AzureOpenAIClient _aoClient;
        private readonly string _deploymentName;
        private readonly AgentFactory.LogCallback _logCallback;
        private readonly List<BankingAgent> _agents;
        private readonly ChatClient _orchestratorClient;

        public BankingAgentOrchestration(AzureOpenAIClient aoClient, string deploymentName, AgentFactory.LogCallback logCallback)
        {
            _aoClient = aoClient;
            _deploymentName = deploymentName;
            _logCallback = logCallback;
            _agents = new List<BankingAgent>();
            _orchestratorClient = aoClient.GetChatClient(deploymentName);
        }

        public void AddAgent(BankingAgent agent)
        {
            _agents.Add(agent);
        }

        public async Task<List<string>> ProcessConversationAsync(List<Microsoft.Extensions.AI.ChatMessage> messages)
        {
            var responses = new List<string>();
            var conversationHistory = new List<Microsoft.Extensions.AI.ChatMessage>(messages);
            var maxIterations = 8;
            var iteration = 0;

            while (iteration < maxIterations)
            {
                iteration++;

                // Select next agent
                var selectedAgent = await SelectNextAgentAsync(conversationHistory);
                if (selectedAgent == null)
                {
                    _logCallback("SELECTION", "No agent selected");
                    break;
                }

                _logCallback("SELECTION - Agent", selectedAgent.Name);

                // Get response from selected agent
                var response = await selectedAgent.ProcessAsync(conversationHistory);
                responses.Add(response);

                // Add response to history
                conversationHistory.Add(new Microsoft.Extensions.AI.ChatMessage(
                    Microsoft.Extensions.AI.ChatRole.Assistant,
                    response
                ));

                // Check if conversation should terminate
                var shouldTerminate = await ShouldTerminateAsync(conversationHistory);
                if (shouldTerminate)
                {
                    _logCallback("TERMINATION", "Conversation complete");
                    break;
                }
            }

            return responses;
        }

        private async Task<BankingAgent?> SelectNextAgentAsync(List<Microsoft.Extensions.AI.ChatMessage> conversationHistory)
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

                return _agents.FirstOrDefault(a => 
                    string.Equals(a.Name, selectionResult.AgentName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logCallback("SELECTION - Error", ex.Message);
                // Fallback to Coordinator
                return _agents.FirstOrDefault(a => a.AgentType == AgentType.Coordinator);
            }
        }

        private async Task<bool> ShouldTerminateAsync(List<Microsoft.Extensions.AI.ChatMessage> conversationHistory)
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

                return !terminationResult.ShouldContinue;
            }
            catch (Exception ex)
            {
                _logCallback("TERMINATION - Error", ex.Message);
                // Default to continue if there's an error
                return false;
            }
        }
    }
}

