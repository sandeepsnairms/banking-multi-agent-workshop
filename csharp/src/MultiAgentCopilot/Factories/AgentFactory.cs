using OpenAI.Chat;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.Tools;
using System.Text.Json;
using MultiAgentCopilot.Models.Banking;
using Microsoft.Extensions.AI;

namespace MultiAgentCopilot.Factories
{
    public class AgentFactory
    {
        private readonly ChatClient _chatClient;
        private readonly ILoggerFactory _loggerFactory;

        public AgentFactory(ChatClient chatClient, ILoggerFactory loggerFactory)
        {
            _chatClient = chatClient;
            _loggerFactory = loggerFactory;
        }

        //public async Task<string> InvokeAgentAsync(
        //    AgentType agentType, 
        //    IList<OpenAI.Chat.ChatMessage> messages, 
        //    BankingDataService bankService, 
        //    string tenantId, 
        //    string userId)
        //{
        //    var prompt = GetAgentPrompt(agentType);
        //    var tools = GetAgentTools(agentType, bankService, tenantId, userId);
        //    var aiFunctionMap = CreateAIFunctionMap(agentType, bankService, tenantId, userId);
        //    var logger = _loggerFactory.CreateLogger<AgentFactory>();

        //    var systemMessage = new SystemChatMessage(prompt);
        //    var allMessages = new List<OpenAI.Chat.ChatMessage> { systemMessage };
        //    allMessages.AddRange(messages);

        //    var chatOptions = new ChatCompletionOptions();
            
        //    // Add tools to the options
        //    foreach (var tool in tools)
        //    {
        //        chatOptions.Tools.Add(tool);
        //    }

        //    var result = await _chatClient.CompleteChatAsync(allMessages, chatOptions);
            
        //    // Handle function calls if any
        //    var response = result.Value;
        //    var responseMessages = new List<OpenAI.Chat.ChatMessage>(allMessages);
        //    responseMessages.Add(new AssistantChatMessage(response));

        //    // Check if the model wants to call functions
        //    while (response.FinishReason == OpenAI.Chat.ChatFinishReason.ToolCalls && response.ToolCalls?.Count > 0)
        //    {
        //        // Execute each function call
        //        foreach (var toolCall in response.ToolCalls)
        //        {
        //            if (toolCall is ChatToolCall functionCall)
        //            {
        //                try
        //                {
        //                    var functionResult = await ExecuteFunctionAsync(functionCall.FunctionName, functionCall.FunctionArguments.ToString(), aiFunctionMap, logger);
        //                    responseMessages.Add(new ToolChatMessage(toolCall.Id, functionResult));
        //                }
        //                catch (Exception ex)
        //                {
        //                    logger.LogError(ex, "Error executing function {FunctionName}", functionCall.FunctionName);
        //                    responseMessages.Add(new ToolChatMessage(toolCall.Id, $"Error: {ex.Message}"));
        //                }
        //            }
        //        }

        //        // Get another response with the function results
        //        result = await _chatClient.CompleteChatAsync(responseMessages, chatOptions);
        //        response = result.Value;
        //        responseMessages.Add(new AssistantChatMessage(response));
        //    }

        //    return response.Content.FirstOrDefault()?.Text ?? string.Empty;
        //}

        //public string GetAgentName(AgentType agentType)
        //{
        //    return agentType switch
        //    {
        //        AgentType.Sales => "Sales",
        //        AgentType.Transactions => "Transactions",
        //        AgentType.CustomerSupport => "CustomerSupport",
        //        AgentType.Coordinator => "Coordinator",
        //        _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
        //    };
        //}

        //private string GetAgentPrompt(AgentType agentType)
        //{
        //    string promptFile = agentType switch
        //    {
        //        AgentType.Sales => "Sales.prompty",
        //        AgentType.Transactions => "Transactions.prompty",
        //        AgentType.CustomerSupport => "CustomerSupport.prompty",
        //        AgentType.Coordinator => "Coordinator.prompty",
        //        _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
        //    };

        //    string prompt = $"{File.ReadAllText($"Prompts/{promptFile}")}{File.ReadAllText("Prompts/CommonAgentRules.prompty")}";
        //    return prompt;
        //}

        //private List<ChatTool> GetAgentTools(AgentType agentType, BankingDataService bankService, string tenantId, string userId)
        //{
        //    // Create the appropriate tools class based on agent type
        //    BaseTools toolsClass = agentType switch
        //    {
        //        AgentType.Sales => new SalesTools(_loggerFactory.CreateLogger<SalesTools>(), bankService, tenantId, userId),
        //        AgentType.Transactions => new TransactionTools(_loggerFactory.CreateLogger<TransactionTools>(), bankService, tenantId, userId),
        //        AgentType.CustomerSupport => new CustomerSupportTools(_loggerFactory.CreateLogger<CustomerSupportTools>(), bankService, tenantId, userId),
        //        AgentType.Coordinator => new CoordinatorTools(_loggerFactory.CreateLogger<CoordinatorTools>(), bankService, tenantId, userId),
        //        _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
        //    };

        //    // Get AIFunctions from the tools class
        //    var aiFunctions = toolsClass.GetTools();
            
        //    // Convert AIFunctions to ChatTools
        //    var chatTools = new List<ChatTool>();
        //    foreach (var aiFunction in aiFunctions)
        //    {
        //        var chatTool = ConvertAIFunctionToChatTool(aiFunction);
        //        if (chatTool != null)
        //        {
        //            chatTools.Add(chatTool);
        //        }
        //    }

        //    return chatTools;
        //}

        //private Dictionary<string, AIFunction> CreateAIFunctionMap(AgentType agentType, BankingDataService bankService, string tenantId, string userId)
        //{
        //    var map = new Dictionary<string, AIFunction>();
            
        //    // Create the appropriate tools class based on agent type
        //    BaseTools toolsClass = agentType switch
        //    {
        //        AgentType.Sales => new SalesTools(_loggerFactory.CreateLogger<SalesTools>(), bankService, tenantId, userId),
        //        AgentType.Transactions => new TransactionTools(_loggerFactory.CreateLogger<TransactionTools>(), bankService, tenantId, userId),
        //        AgentType.CustomerSupport => new CustomerSupportTools(_loggerFactory.CreateLogger<CustomerSupportTools>(), bankService, tenantId, userId),
        //        AgentType.Coordinator => new CoordinatorTools(_loggerFactory.CreateLogger<CoordinatorTools>(), bankService, tenantId, userId),
        //        _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
        //    };

        //    var functions = toolsClass.GetTools();
        //    foreach (var function in functions)
        //    {
        //        map[function.Name] = function;
        //    }

        //    return map;
        //}

        //private ChatTool? ConvertAIFunctionToChatTool(AIFunction aiFunction)
        //{
        //    try
        //    {
        //        var functionName = aiFunction.Name;
        //        var functionDescription = aiFunction.Description ?? string.Empty;
                
        //        // Generate specific parameter schemas for known functions
        //        var parametersSchema = GenerateParametersSchemaForFunction(functionName);
                
        //        return ChatTool.CreateFunctionTool(
        //            functionName: functionName,
        //            functionDescription: functionDescription,
        //            functionParameters: BinaryData.FromString(parametersSchema)
        //        );
        //    }
        //    catch (Exception ex)
        //    {
        //        _loggerFactory.CreateLogger<AgentFactory>().LogWarning(ex, "Failed to convert AIFunction {FunctionName} to ChatTool", aiFunction.Name);
        //        return null;
        //    }
        //}

        //private string GenerateParametersSchemaForFunction(string functionName)
        //{
        //    object schema = functionName switch
        //    {
        //        "GetTeleBankerSlots" => new
        //        {
        //            type = "object",
        //            properties = new Dictionary<string, object>
        //            {
        //                ["accountType"] = new
        //                {
        //                    type = "string",
        //                    description = "The type of account",
        //                    @enum = new[] { "Checking", "Savings", "CreditCard", "Mortgage", "Investment" }
        //                }
        //            },
        //            required = new[] { "accountType" }
        //        },
        //        "SearchOfferTerms" => new
        //        {
        //            type = "object",
        //            properties = new Dictionary<string, object>
        //            {
        //                ["accountType"] = new
        //                {
        //                    type = "string",
        //                    description = "The type of account",
        //                    @enum = new[] { "Checking", "Savings", "CreditCard", "Mortgage", "Investment" }
        //                },
        //                ["requirementDescription"] = new
        //                {
        //                    type = "string",
        //                    description = "Description of requirements to search for"
        //                }
        //            },
        //            required = new[] { "accountType", "requirementDescription" }
        //        },
        //        "RegisterAccount" => new
        //        {
        //            type = "object",
        //            properties = new Dictionary<string, object>
        //            {
        //                ["userId"] = new { type = "string", description = "User ID" },
        //                ["accType"] = new
        //                {
        //                    type = "string",
        //                    description = "Account type",
        //                    @enum = new[] { "Checking", "Savings", "CreditCard", "Mortgage", "Investment" }
        //                },
        //                ["fulfilmentDetails"] = new
        //                {
        //                    type = "object",
        //                    description = "Fulfilment details",
        //                    additionalProperties = new { type = "string" }
        //                }
        //            },
        //            required = new[] { "userId", "accType", "fulfilmentDetails" }
        //        },
        //        "IsAccountRegisteredToUser" => new
        //        {
        //            type = "object",
        //            properties = new Dictionary<string, object>
        //            {
        //                ["accountId"] = new { type = "string", description = "Account ID to check" }
        //            },
        //            required = new[] { "accountId" }
        //        },
        //        "CheckPendingServiceRequests" => new
        //        {
        //            type = "object",
        //            properties = new Dictionary<string, object>
        //            {
        //                ["accountId"] = new { type = "string", description = "Account ID (optional)" },
        //                ["srType"] = new
        //                {
        //                    type = "string",
        //                    description = "Service request type (optional)",
        //                    @enum = new[] { "FundTransfer", "TeleBankerCallBack", "Complaint", "Fulfilment" }
        //                }
        //            }
        //        },
        //        "AddTeleBankerRequest" => new
        //        {
        //            type = "object",
        //            properties = new Dictionary<string, object>
        //            {
        //                ["accountId"] = new { type = "string", description = "Account ID" },
        //                ["requestAnnotation"] = new { type = "string", description = "Request annotation" },
        //                ["callbackTime"] = new { type = "string", description = "Callback time in ISO format" }
        //            },
        //            required = new[] { "accountId", "requestAnnotation", "callbackTime" }
        //        },
        //        "CreateComplaint" => new
        //        {
        //            type = "object",
        //            properties = new Dictionary<string, object>
        //            {
        //                ["accountId"] = new { type = "string", description = "Account ID" },
        //                ["requestAnnotation"] = new { type = "string", description = "Complaint details" }
        //            },
        //            required = new[] { "accountId", "requestAnnotation" }
        //        },
        //        "UpdateExistingServiceRequest" => new
        //        {
        //            type = "object",
        //            properties = new Dictionary<string, object>
        //            {
        //                ["requestId"] = new { type = "string", description = "Service request ID" },
        //                ["accountId"] = new { type = "string", description = "Account ID" },
        //                ["requestAnnotation"] = new { type = "string", description = "Additional details" }
        //            },
        //            required = new[] { "requestId", "accountId", "requestAnnotation" }
        //        },
        //        "AddFunTransferRequest" => new
        //        {
        //            type = "object",
        //            properties = new Dictionary<string, object>
        //            {
        //                ["debitAccountId"] = new { type = "string", description = "Debit account ID" },
        //                ["amount"] = new { type = "number", description = "Transfer amount" },
        //                ["requestAnnotation"] = new { type = "string", description = "Transfer details" },
        //                ["recipientPhoneNumber"] = new { type = "string", description = "Recipient phone (optional)" },
        //                ["recipientEmailId"] = new { type = "string", description = "Recipient email (optional)" }
        //            },
        //            required = new[] { "debitAccountId", "amount", "requestAnnotation" }
        //        },
        //        "GetTransactionHistory" => new
        //        {
        //            type = "object",
        //            properties = new Dictionary<string, object>
        //            {
        //                ["accountId"] = new { type = "string", description = "Account ID" },
        //                ["startDate"] = new { type = "string", description = "Start date in ISO format" },
        //                ["endDate"] = new { type = "string", description = "End date in ISO format" }
        //            },
        //            required = new[] { "accountId", "startDate", "endDate" }
        //        },
        //        "GetOfferDetails" => new
        //        {
        //            type = "object",
        //            properties = new Dictionary<string, object>
        //            {
        //                ["offerId"] = new { type = "string", description = "Offer ID" }
        //            },
        //            required = new[] { "offerId" }
        //        },
        //        _ => new
        //        {
        //            type = "object",
        //            properties = new Dictionary<string, object>(),
        //            additionalProperties = true
        //        }
        //    };

        //    var options = new JsonSerializerOptions
        //    {
        //        WriteIndented = false,
        //        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        //    };

        //    return JsonSerializer.Serialize(schema, options);
        //}

        //private async Task<string> ExecuteFunctionAsync(string functionName, string functionArguments, Dictionary<string, AIFunction> aiFunctionMap, ILogger logger)
        //{
        //    if (aiFunctionMap.TryGetValue(functionName, out var aiFunction))
        //    {
        //        try
        //        {
        //            // Parse arguments and convert to proper types
        //            var argumentsDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(functionArguments);
        //            var aiArguments = new AIFunctionArguments();
                    
        //            if (argumentsDict != null)
        //            {
        //                foreach (var kvp in argumentsDict)
        //                {
        //                    aiArguments[kvp.Key] = ConvertJsonElementToObject(kvp.Value);
        //                }
        //            }
                    
        //            var result = await aiFunction.InvokeAsync(aiArguments);
        //            return JsonSerializer.Serialize(result);
        //        }
        //        catch (Exception ex)
        //        {
        //            logger.LogError(ex, "Failed to invoke AIFunction {FunctionName}", functionName);
        //            return JsonSerializer.Serialize(new { error = ex.Message });
        //        }
        //    }

        //    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
        //}

        //private object? ConvertJsonElementToObject(JsonElement element)
        //{
        //    return element.ValueKind switch
        //    {
        //        JsonValueKind.String => element.GetString(),
        //        JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : 
        //                               element.TryGetInt64(out var longVal) ? longVal :
        //                               element.TryGetDecimal(out var decVal) ? decVal : 
        //                               element.GetDouble(),
        //        JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
        //        JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToObject).ToArray(),
        //        JsonValueKind.Object => element.EnumerateObject().ToDictionary(
        //            prop => prop.Name, 
        //            prop => ConvertJsonElementToObject(prop.Value)),
        //        JsonValueKind.Null => null,
        //        _ => element.GetRawText()
        //    };
        //}
    }
}