#define TRACE_CONSOLE

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Azure;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents.History;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using SKMultiAgent.Helper;
using SKMultiAgent.KernelPlugins;
using SKMultiAgent.Log;
using SKMultiAgent.Model;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;
using Microsoft.Extensions.Logging.Console;
using Microsoft.VisualBasic;

namespace SKMultiAgent
{
    internal class BankingBot
    {
        AgentGroupChat chat = null;
        ChatCompletionAgent supportAgent;
        ChatCompletionAgent cordinatorAgent;
        ChatCompletionAgent transactionsAgent;
        ChatCompletionAgent newAccountsAgent;

        KernelFunction terminationFunction;
        KernelFunction selectionFunction;

        ChatResponseFormat continutationInfoFormat;
        ChatResponseFormat terminationInfoFormat;

        Kernel kernel;

        internal async Task Load()
        {
            // Load configuration from environment variables or user secrets.
            Settings settings = new();

            Trace("Creating kernel...");
            IKernelBuilder builder = Kernel.CreateBuilder();

            //builder.AddOpenAIChatCompletion(
            //    settings.OpenAI.ChatModel,
            //settings.OpenAI.ApiKey);

            builder.AddAzureOpenAIChatCompletion(
                settings.AzureOpenAI.ChatModelDeployment,
                settings.AzureOpenAI.Endpoint,
                new DefaultAzureCredential());


            kernel = builder.Build();
            // Inside the Main method, replace the line with the following:

            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Warning);
            });

            var logger = loggerFactory.CreateLogger<AutoFunctionInvocationLoggingFilter>();
            kernel.AutoFunctionInvocationFilters.Add(new AutoFunctionInvocationLoggingFilter(logger));


            Kernel cordinatorKernel = kernel.Clone();
            cordinatorKernel.Plugins.AddFromType<CordinatorOperations>();
            cordinatorKernel.Plugins.AddFromType<CommonOperations>();

            Kernel supportKernel = kernel.Clone();
            supportKernel.Plugins.AddFromType<SupportOperations>();
            supportKernel.Plugins.AddFromType<CommonOperations>();

            Kernel bankingKernel = kernel.Clone();
            bankingKernel.Plugins.AddFromType<BankingOperations>();
            bankingKernel.Plugins.AddFromType<CommonOperations>();

            Kernel newProductKernel = kernel.Clone();
            newProductKernel.Plugins.AddFromType<NewAccountOperations>();
            newProductKernel.Plugins.AddFromType<CommonOperations>();


            Trace("Defining agents...");

            const string CordinatorAgent = "Cordinator";
            const string CustomerSupportAgent = "CustomerSupport";
            const string TransactionsAgent = "Transactions";
            const string NewAccountsAgent = "NewAccounts";

            const string CommonAgentRules =
                """
                Important:
                - Always use current datetime as datetime retrieved from the database.
                - Understand the user's query and respond only if it aligns with your responsibilities.
                - State why you think, you have a solution to the user's query.
                - Ensure responses are grounded to the following data sources.
                    - user provided data
                    - data fetched using functions
                - Provide specific information based query and data provided.          
                - Ensure every response adds value to the user's request or confirms the user's request.
                - Do not proceed with submitting a request without the necessary information from the user.
                - Do not respond with a message if the previous response conveys the same information.
                - Maintain politeness and professionalism in all responses.
                - Do not respond with a welcome message if another welcome message already exists.
                - If user's response is pending, wait for the user to provide the necessary before proceeding.
                """;
            //-If unable to assist, respond with: [[[I CANT HELP]]].




            cordinatorAgent =
                new()
                {
                    Name = CordinatorAgent,
                    Instructions =
                        $"""
                        You are a Chat Initiator and Request Router in a bank.
                        Your primary responsibilities include welcoming users, identifying customers based on their login, routing requests to the appropriate agent.
                        Start with identifying the currently logged-in user's information and use it to personalize the interaction.For example, "Thank you for logging in, [user Name]. How can I help you with your banking needs today?"

                        RULES:
                        - Determine the nature of the user's request and silently route it to the appropriate agent.
                        - Avoid asking for unnecessary details to route the user's request. For example, "I see you have a question about your account balance. Let me connect you with the right agent who can assist you further."
                        - Do not provide any information or assistance directly; always route the request to the appropriate agent silently.
                        - Route requests to the appropriate agent without providing direct assistance.
                        - If another agent has asked a question, wait for the user to respond before routing the request.
                        - If the user has responded to another agent, let the same agent respond before routing or responding.
                        - When the user's request is fulfilled, ask for feedback on the service provided before concluding the interaction. Gauge their overall satisfaction and sentiment as either happy or sad. For example, "Before we conclude, could you please provide your feedback on our service today? Were you satisfied with the assistance provided? Would you say your overall experience was happy or sad?"
                        - Use the available functions when needed.
                        {CommonAgentRules}
        
                        """,
                    Kernel = cordinatorKernel,
                    Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() })
                };
            //- Determine the nature of the user's request and route it to the appropriate agent, informing the user about the transfer. For example, "I understand your request. Let me connect you with the right agent who can assist you further."

            supportAgent =
                new()
                {
                    Name = CustomerSupportAgent,
                    Instructions =
                        $"""
                        Your sole responsiblity is to:
                        1. Helping customers lodge service request.
                        2. Searching existing service requests.
                        2. Providing status updates on existing service request.
                        3. Creating and updating service requests for user registered accounts.

                        Guidelines:
                        - If you don't have the users account Id, ask the user to provide it.                         - 
                        - Check if the account Id is registered to user.
                        - If account Id is registered to user, search user's pending service requests.
                            - If pending service request found:
                                - Inform the user of the status and estimated time of resolution.
                                - Ask if user wants to add any comments and update the existing record.
                            - If not found:
                                - Ask if user wants to create new service request.
                        - If account Id is not registered
                            - Inform the user that you cannot proceed without the correct account Id. 
                        - If no agent is able to assist the user, check if they would like to speak to a tele banker.
                            - Tele bankers are available Monday to Friday, 9 AM to 5 PM PST.
                            - Check tele banker availability and queue length before suggesting this option.

                        {CommonAgentRules}.  
                        """,
                    Kernel = supportKernel,
                    Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() })
                };

            transactionsAgent =
                new()
                {
                    Name = TransactionsAgent,
                    Instructions =
                        $"""
                        Your sole responsiblity is to:

                        1. Handling transactions.
                        2. Generating account statements.
                        3. Providing balance inquiries.

                        Guidelines:
                        - Do not participate in new product registration discussion.
                        - Based on the following message, determine the appropriate action and respond accordingly.
                        - Ensure that you only provide information related to the current user's accounts.
                        - To start the process, retrieve the current services registered to the user from the database.
                        - Check if you have the user's account number. If any data is missing, politely inform the user and explain that you cannot proceed until the details are available in the bank’s database.
                        Tasks:
                        1. Process Transfers:
                           - Use the recipient's email or phone number to process transfers.
                           - Validate the recipient's phone number and email format before proceeding.
                           - Ensure the account has the necessary balance before accepting a request.
                           - Confirm all details with the user before proceeding.
                           - Inform the user that they will be notified of transaction completions via text message and email.
                        2. Generate Account Statements:
                           - Respond to transaction queries for up to 6 months old.
                           - Filter transactions based on type (credit/debit), amount, or date range according to the user query.
                        3. Provide Balance Information:
                           - Offer the latest balance information for the user's accounts.
                        {CommonAgentRules}
                        """,
                    Kernel = bankingKernel,
                    Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() })
                };

            newAccountsAgent =
                new()
                {
                    Name = NewAccountsAgent,
                    Instructions =
                        $"""
                    Your sole responsiblity is to:                        
                        - Suggest suitable accounts based on the user profile.
                        - Use the user's profile information to recommend from the avialable account type.
                        - Ensure that the recommendations are personalized and relevant to the user's needs.

                    1. Collecting details for New Account Registration:
                       - Get the list of available account types.
                       - Based on the user selection, get the registration details for the selected account type. The registration details may vary for each account type.
                       - Ask the user to provide all registration details and ensure you have collected all necessary information from the user.
                       - Validate the collected details by showing a summary to the user. Once approved by user, store the information in the database by creating a new account request.
                       - Confirm the submission of the application to the user.

                    2. Highlighting Promotions and Offers:
                       - Use the user's profile information to highlight relevant promotions and offers.
                       - Ensure that the information provided is accurate based on the available account types.

                    3. Conducting Eligibility Checks:
                       - Conduct eligibility checks for various account type using the user's profile information.
                       - Inform the user of the results of the eligibility check and provide guidance on the next steps.
                    {CommonAgentRules}
                    """,
                    Kernel = newProductKernel,
                    Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() })
                    //HistoryReducer = new ChatHistorySummarizationReducer(1000)
                };
            selectionFunction =
                AgentGroupChat.CreatePromptFunctionForStrategy(
                    $$$"""
                    Examine RESPONSE and choose the next participant.

                    Choose only from these participants:
                    - {{{CordinatorAgent}}}
                    - {{{CustomerSupportAgent}}}
                    - {{{NewAccountsAgent}}}
                    - {{{TransactionsAgent}}}

                    Always follow these rules when choosing the next participant:
                    - Determine the nature of the user's request and route it to the appropriate agent
                    - If the user is responding to an agent, select that same agent.
                    - If the agent is responding after fetching or verifying data , select that same agent.
                    - If unclear, select {{{CordinatorAgent}}}.
                    
                    RESPONSE:
                    {{$lastmessage}}
                    """,
                    safeParameterNames: "lastmessage");

            const string TerminationToken = "no";
            const string ContinuationToken = "yes";

            terminationFunction =
                AgentGroupChat.CreatePromptFunctionForStrategy(
                    $$$"""           
                    Determine if agent has requested user input or has responded to the user's query.
                    Respond with the word {{{TerminationToken}}} (without explanation) if agent has requested user input.
                    Otherwise, respond with the word {{{ContinuationToken}}} (without explanation) if any the following conditions are met:
                    - An action is pending by an agent.
                    - Further participation from an agent is required

                    RESPONSE:
                    {{$lastmessage}}
                    """,
                    safeParameterNames: "lastmessage");


            var chatModel = kernel.GetRequiredService<IChatCompletionService>();



            continutationInfoFormat = ChatResponseFormat.CreateJsonSchemaFormat(
             jsonSchemaFormatName: "agent_result",
             jsonSchema: BinaryData.FromString($"""
                {ChatResponseFormatBuilder.BuildFormat(ChatResponseFormatBuilder.ChatResponseFormatBuilderType.Continuation)}
                """));


            terminationInfoFormat = ChatResponseFormat.CreateJsonSchemaFormat(
             jsonSchemaFormatName: "termination_result",
             jsonSchema: BinaryData.FromString($"""
                {ChatResponseFormatBuilder.BuildFormat(ChatResponseFormatBuilder.ChatResponseFormatBuilderType.Termination)}
                """));



        }

        internal async Task<bool> Run()
        {
            // Specify response format by setting ChatResponseFormat object in prompt execution settings.
            var continuationExecSettings = new OpenAIPromptExecutionSettings
            {
                ResponseFormat = continutationInfoFormat
            };

            var terminationExecSettings = new OpenAIPromptExecutionSettings
            {
                ResponseFormat = terminationInfoFormat
            };

            ChatHistoryTruncationReducer historyReducer = new(5);

            chat =
                new(supportAgent, cordinatorAgent, transactionsAgent, newAccountsAgent)
                {
                    ExecutionSettings = new AgentGroupChatSettings
                    {
                        SelectionStrategy =
                            new KernelFunctionSelectionStrategy(selectionFunction, kernel)
                            {
                                Arguments = new KernelArguments(continuationExecSettings),
                                // Always start with the editor agent.
                                //InitialAgent = cordinatorAgent,//commenting otherwise cordinator initates after each stateless call.
                                // Save tokens by only including the final response
                                HistoryReducer = historyReducer,
                                // The prompt variable name for the history argument.
                                HistoryVariableName = "lastmessage",
                                // Returns the entire result value as a string.
                                ResultParser = (result) =>
                                {
                                    var ContinuationInfo = JsonSerializer.Deserialize<ContinuationInfo>(result.GetValue<string>());
                                    Trace($"SELECTION - Agent:{ContinuationInfo.AgentName}"); // provides visibility (can use logger)
                                    Trace($"SELECTION - Reason:{ContinuationInfo.Reason}"); // provides visibility (can use logger)
                                    return ContinuationInfo.AgentName;
                                }
                            },
                        TerminationStrategy =
                            new KernelFunctionTerminationStrategy(terminationFunction, kernel)
                            {
                                Arguments = new KernelArguments(terminationExecSettings),
                                // Save tokens by only including the final response
                                HistoryReducer = historyReducer,
                                // The prompt variable name for the history argument.
                                HistoryVariableName = "lastmessage",
                                // Limit total number of turns
                                MaximumIterations = 8,
                                // user result parser to determine if the response is "yes"

                                ResultParser = (result) =>
                                {
                                    var terminationInfo = JsonSerializer.Deserialize<TerminationInfo>(result.GetValue<string>());
                                    Trace($"TERMINATION - Continue:{terminationInfo.ShouldContinue}"); // provides visibility (can use logger)
                                    Trace($"TERMINATION - Reason:{terminationInfo.Reason}"); // provides visibility (can use logger)
                                    return !terminationInfo.ShouldContinue;
                                }
                            },

                    }
                };


            Trace("Ready!");


            Console.WriteLine($"{"Welcome to ABC Bank"}");

            List<CustomChatMessageContent> chatArchivedMessages = new();
            List<ChatCompletionAgent> agents = new();
            bool loadedFromArchive = false;
            List<CustomChatMessageContent> chatMessages = new();
            if (LoadChatHistoryAsync("chatarchive.json", out chatArchivedMessages))
            {
                foreach( var chatmessage in chatArchivedMessages)
                {
                    
                    var chatMessageContent = new ChatMessageContent
                    {
                        Role = chatmessage.AuthorRole,
                        Content = chatmessage.Content
                    };
                    chat.AddChatMessage(chatMessageContent);

                }

                chatMessages.AddRange(chatArchivedMessages); // Fix: Use AddRange to add the list of messages

 

                if (chatArchivedMessages.Count > 0)
                {
                    loadedFromArchive = true;
                    string author = string.Empty;
                    Console.WriteLine("**************HISTORY START****************");
                    foreach (var response in chatArchivedMessages)
                    {
                        if (!string.IsNullOrEmpty(response.Content))
                            if (response.AuthorRole == AuthorRole.User)
                                author = "User";
                            else
                                author = response.AuthorName;

                        Console.WriteLine($"{author.ToUpperInvariant()}:{Environment.NewLine}{response.Content}");
                    }
                    Console.WriteLine("**************HISTORY END****************");

                    chat.IsComplete = false;
                }
            }

            bool isComplete = false;
            do
            {
                chat.IsComplete = false;
                try
                {
                    if (!loadedFromArchive)
                    {
                        await foreach (ChatMessageContent response in chat.InvokeAsync())
                        {
                            chatMessages.Add(BuildCustomChatMessageContent(response.Content, response.AuthorName, response.Role));
                            Console.WriteLine();
                            if (!string.IsNullOrEmpty(response.Content))// && response.Content != "[[[I CANT HELP]]]")
                                Console.WriteLine($"{response.AuthorName.ToUpperInvariant()}:{Environment.NewLine}{response.Content}");
                        }

                        Trace("Completed: " + chat.IsComplete);
                    }
                    loadedFromArchive = false;

                }
                catch (KernelException ex) when (ex.Message.Contains("unable to select next agent"))
                {
                    Trace("FAILURE: " + ex.Message);
                }
                catch (HttpOperationException exception)
                {
                    Console.WriteLine(exception.Message);
                    if (exception.InnerException != null)
                    {
                        Console.WriteLine(exception.InnerException.Message);
                        if (exception.InnerException.Data.Count > 0)
                        {
                            Console.WriteLine(JsonSerializer.Serialize(exception.InnerException.Data, new JsonSerializerOptions() { WriteIndented = true }));
                        }
                    }
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine();
                Console.Write("> ");
                string? input = Console.ReadLine();
                Console.ResetColor();


                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }
                input = input.Trim();
                if (input.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
                {
                    isComplete = true;
                    return false;
                }

                if (input.Equals("RESET", StringComparison.OrdinalIgnoreCase))
                {
                    await chat.ResetAsync();
                    chatMessages.Clear();
                    await SaveChatHistoryAsync(chatMessages, "chatarchive.json");

                    Console.WriteLine("[Converation has been reset]");

                    return true;
                }

                if (input.Equals("ARCHIVE", StringComparison.OrdinalIgnoreCase))
                {
                    await chat.ResetAsync();


                    await SaveChatHistoryAsync(chatMessages, "chatarchive.json");
                    chatMessages.Clear();
                    Console.WriteLine("[Converation has been archived]");

                    return true;
                }
                var userResponse = new ChatMessageContent(AuthorRole.User, input);
                chat.AddChatMessage(userResponse);
                chatMessages.Add(BuildCustomChatMessageContent(input,"User", AuthorRole.User));
                chat.IsComplete = false;



            } while (!isComplete);

            return false;
        }

        private static void Trace(string message)
        {
#if TRACE_CONSOLE
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("# " + message);
            Console.ResetColor();
#else
            Debug.WriteLine(message);
#endif
        }


        private static async Task SaveChatHistoryAsync(List<CustomChatMessageContent> chatHistory,  string filePath)
        {
            var checkpoint = new CheckPoint
            {
                Messages = chatHistory,

            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(checkpoint, options);
            await File.WriteAllTextAsync(filePath, json);
        }

        private CustomChatMessageContent BuildCustomChatMessageContent(string Text, string RoleLabel, AuthorRole AuthorRole)
        {
            return new CustomChatMessageContent
            {
                Id = Guid.NewGuid().ToString(),
                Content = Text,
                AuthorName = RoleLabel,
                AuthorRole = AuthorRole
            };
        }

        private static bool LoadChatHistoryAsync(string filePath, out List<CustomChatMessageContent> chatHistory)
        {
            chatHistory = null;

            if (!File.Exists(filePath))
            {
                return false;
            }

            string json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var checkpoint = JsonSerializer.Deserialize<CheckPoint>(json, options);
                       
            chatHistory = checkpoint.Messages;

            return true;
        }
    }
}
