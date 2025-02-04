// Copyright (c) Microsoft. All rights reserved.

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
using SKMultiAgent.Model;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

namespace SKMultiAgent
{
    public static class Program
    {
        public static async Task Main()
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


            Kernel kernel = builder.Build();


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
            newProductKernel.Plugins.AddFromType<NewProductOperations>();
            newProductKernel.Plugins.AddFromType<CommonOperations>();


            Trace("Defining agents...");

            const string CordinatorAgent = "Cordinator";
            const string CustomerSupportAgent = "CustomerSupport";
            const string TransactionsAgent = "Transactions";
            const string NewProductsAgent = "NewProducts";

            const string CommonAgentRules =
                """
                Important:
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

            //const string GlobalRules = 
            //    """                                   
                
            //    """;
            //- If user's response is pending, wait for the user to provide the necessary before proceeding.

            ChatCompletionAgent cordinatorAgent =
                new()
                {
                    Name = CordinatorAgent,
                    Instructions =
                        $"""
                        You are a Chat Initiator and Request Router in a bank.
                        Your primary responsibilities include welcoming users, identifying customers based on their login, routing requests to the appropriate agent.
                        Start with identifying the currently logged-in user's information and use it to personalize the interaction.For example, "Thank you for logging in, [user Name]. How can I help you with your banking needs today?"

                        RULES:
                        - Determine the nature of the user's request and route it to the appropriate agent, informing the user about the transfer. For example, "I understand your request. Let me connect you with the right agent who can assist you further."
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


            ChatCompletionAgent supportAgent =
                new()
                {
                    Name = CustomerSupportAgent,
                    Instructions =
                        $"""
                        Your sole responsiblity is to:
                        1. Helping customers lodge service request.
                        2. Lookup on existing service request.
                        2. Providing status updates on existing service request.
                        3. Taking requests for account details updates, check book requests, and card replacements.

                        Guidelines:
                        - Let user know you can submit a service request for them to address a complain.
                        - Execute the below steps in order to proces Support Request                           
                            1. Ask the user to provide their account ID, if you don't have the users account ID.                        - 
                            1. Start by verifying  the account ID against database.
                            2. If account is verified, search the database for pending service requests matching the current request.
                                - If pending service request is found, inform the user of the status and estimated time of resolution. Ask if the user would like to add any comments and update the existing record with new request comments.
                                - If no matching pending service request found, create a new service request.
                            3. If account details are not available, inform the user that you cannot proceed without the necessary information.
                        - If no agent is able to assist the user, check if they would like to speak to a tele banker. Tele bankers are available Monday to Friday, 9 AM to 5 PM PST. Check tele banker availability and queue length before suggesting this option.

                        {CommonAgentRules}.  
                        """,
                    Kernel = supportKernel,
                };
            /*
            ChatCompletionAgent transactionsAgent =
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
                };
 
            ChatCompletionAgent newProductsAgent =
                new()
                {
                    Name = NewProductsAgent,
                    Instructions =
                        $"""
                    Your sole responsiblity is to suggest suitable products based on user profiles. Use the user's profile information to recommend products such as credit cards, loans, deposits, and lockers. Ensure that the recommendations are personalized and relevant to the user's needs.

                    1. Collecting Details for New Products:
                       - Gather all necessary details required to register for a new product. The required details may vary for each product.
                       - Retrieve the application fields from the database and ensure you have collected all necessary information from the user.
                       - Validate the collected details by showing a summary to the user. Once approved, store the information in the database by creating a new product request.
                       - Confirm the submission of the application to the user.

                    2. Highlighting Promotions and Offers:
                       - Use the user's profile information to highlight relevant promotions and offers.
                       - Ensure that the information provided is accurate and up-to-date.

                    3. Conducting Eligibility Checks:
                       - Conduct eligibility checks for various products using the user's profile information.
                       - Determine the user's eligibility for products such as credit cards, loans, deposits, and lockers.
                       - Inform the user of the results of the eligibility check and provide guidance on the next steps.
                    {CommonAgentRules}
                    """,
                    Kernel = newProductKernel
                    //HistoryReducer = new ChatHistorySummarizationReducer(1000)
                };*/
            KernelFunction selectionFunction =
                AgentGroupChat.CreatePromptFunctionForStrategy(
                    $$$"""
                    Examine RESPONSE and choose the next participant.

                    Choose only from these participants:
                    - {{{CordinatorAgent}}}
                    - {{{CustomerSupportAgent}}}

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

            KernelFunction terminationFunction =
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

            // Set up ChatHistoryTruncationReducer to summarize older chat messages
            //var historyReducer = new ChatHistorySummarizationReducer(chatModel, 1000);

            ChatHistoryTruncationReducer historyReducer = new(5);

            ChatResponseFormat continutationInfoFormat = ChatResponseFormat.CreateJsonSchemaFormat(
             jsonSchemaFormatName: "agent_result",
             jsonSchema: BinaryData.FromString($"""
                {ChatResponseFormatBuilder.BuildFormat(ChatResponseFormatBuilder.ChatResponseFormatBuilderType.Continuation)}
                """));


            ChatResponseFormat terminationInfoFormat = ChatResponseFormat.CreateJsonSchemaFormat(
             jsonSchemaFormatName: "termination_result",
             jsonSchema: BinaryData.FromString($"""
                {ChatResponseFormatBuilder.BuildFormat(ChatResponseFormatBuilder.ChatResponseFormatBuilderType.Termination)}
                """));


            // Specify response format by setting ChatResponseFormat object in prompt execution settings.
            var continuationExecSettings = new OpenAIPromptExecutionSettings
            {
                ResponseFormat = continutationInfoFormat
            };

            var terminationExecSettings = new OpenAIPromptExecutionSettings
            {
                ResponseFormat = terminationInfoFormat
            };



            AgentGroupChat chat =
                new(supportAgent, cordinatorAgent)
                {
                    ExecutionSettings = new AgentGroupChatSettings
                    {
                        SelectionStrategy =
                            new KernelFunctionSelectionStrategy(selectionFunction, kernel)
                            {
                                Arguments = new KernelArguments(continuationExecSettings),
                                // Always start with the editor agent.
                                InitialAgent = cordinatorAgent,
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

            //var chatHistory=await LoadChatHistoryAsync("chatHistory.json");

            //List< ChatMessageContent> chatMessageList = new();
            //if (chatHistory!=null)
            //{
            //    chat.AddChatMessages((IReadOnlyList<ChatMessageContent>)chatHistory);
            //}

            bool isComplete = false;
            do
            {
                chat.IsComplete = false;
                try
                {
                    await foreach (ChatMessageContent response in chat.InvokeAsync())
                    {
                        //chatMessageList.Add(response);
                        Console.WriteLine();
                        if (!string.IsNullOrEmpty(response.Content))// && response.Content != "[[[I CANT HELP]]]")
                            Console.WriteLine($"{response.AuthorName.ToUpperInvariant()}:{Environment.NewLine}{response.Content}");
                    }

                    Trace("Completed: " + chat.IsComplete);

                }
                catch (KernelException ex) when (ex.Message.Contains("unable to select next agent"))
                {
                    Trace("FAILURE: " + ex.Message);
                    chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, "Who else can help?"));
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
                    break;
                }

                if (input.Equals("RESET", StringComparison.OrdinalIgnoreCase))
                {
                    await chat.ResetAsync();
                    
                    Console.WriteLine("[Converation has been reset]");
                    //await SaveChatHistoryAsync(chatMessageList,chat.Agents ,"chatHistory.json");
                    continue;
                }

                chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));

                chat.IsComplete = false;



            } while (!isComplete);
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
               

        //private static async Task SaveChatHistoryAsync(List<ChatMessageContent> chatHistory, List<ChatCompletionAgent> agents,string filePath)
        //{
        //    var checkpoint = new
        //    {
        //        Messages = chatHistory,
        //        AgentNames = agents.ConvertAll(a => a.Name) // Save agent identifiers
        //    };


        //    var options = new JsonSerializerOptions { WriteIndented = true };
        //    string json = JsonSerializer.Serialize(checkpoint, options);
        //    await File.WriteAllTextAsync(filePath, json);
        //}


        


        //private static async Task<List<ChatMessageContent>> LoadChatHistoryAsync(string filePath)
        //{
        //    if (!File.Exists(filePath))
        //    {
        //        return null;
        //        //throw new FileNotFoundException("The specified file does not exist.", filePath);
        //    }

        //    string json = await File.ReadAllTextAsync(filePath);
        //    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        //    return JsonSerializer.Deserialize<List<ChatMessageContent>>(json, options) ?? new List<ChatMessageContent>();
        //}


    }
}
