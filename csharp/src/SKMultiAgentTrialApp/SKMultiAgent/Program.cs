// Copyright (c) Microsoft. All rights reserved.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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

            Console.WriteLine("Creating kernel...");
            IKernelBuilder builder = Kernel.CreateBuilder();


            builder.AddAzureOpenAIChatCompletion(
                settings.AzureOpenAI.ChatModelDeployment,
                settings.AzureOpenAI.Endpoint,new DefaultAzureCredential());


            Kernel kernel = builder.Build();


            Kernel cordinatorKernel = kernel.Clone();
            cordinatorKernel.Plugins.AddFromType<BasicOperations>();
            cordinatorKernel.Plugins.AddFromType<CordinatorOperations>();

            Kernel bankingKernel = kernel.Clone();
            bankingKernel.Plugins.AddFromType<BankingOperations>();

            Kernel supportKernel = kernel.Clone();
            supportKernel.Plugins.AddFromType<SupportOperations>();

            Kernel newProductKernel = kernel.Clone();
            bankingKernel.Plugins.AddFromType<NewProductOperations>();


            Console.WriteLine("Defining agents...");

            const string CordinatorAgent = "Cordinator";
            const string CustomerSupportAgent = "CustomerSupport";
            const string TransactionsAgent = "Transactions";
            const string NewProductsAgent = "NewProducts";

            const string CommonAgentRules = @"Important:
                   - Understand the user's query and respond only if it aligns with your responsibilities.
                   - State why you think, you have a solution to the user's query.
                   - Provide specific information based query and data provided.
                   - Ensure responses are grounded in the provided data.               
                   - Ensure every response adds value to the user's request or confirms the user's request.
                   - If unable to assist, respond with [[[I CANT HELP]]].
                   - Do not proceed with submitting a request without the necessary information from the user.
                   - Do not respond with a message if the previous response conveys the same information.
                   - Maintain politeness and professionalism in all responses.  
                    ";

            const string GlobalRules = @"
                   - If user's response is pending, wait for the user to provide the necessary before proceeding.                   
                   - Do not respond with a welcome message if another welcome message already exists.
                    ";
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            ChatCompletionAgent cordinatorAgent =
                new()
                {
                    Name = CordinatorAgent,
                    Instructions =
                        $"""
                    You are a Chat Initiator and Request Router in a bank.
                    Your primary responsibilities include welcoming users, identifying customers based on their login, routing requests to the appropriate agent.
                    Start by greeting the user warmly and asking how you can assist them today.

                    RULES:
                    - Identify the user based on their login information and use their name to personalize the interaction. For example, "Thank you for logging in, [user Name]. How can I help you with your banking needs today?"
                    - Determine the nature of the user's request and route it to the appropriate agent, informing the user about the transfer. For example, "I understand your request. Let me connect you with the right agent who can assist you further."
                    - Avoid asking for unnecessary details to route the user's request. For example, "I see you have a question about your account balance. Let me connect you with the right agent who can assist you further."
                    - Do not provide any information or assistance directly; always route the request to the appropriate agent silently.
                    - Route requests to the appropriate agent without providing direct assistance.
                    - If another agent has asked a question, wait for the user to respond before routing the request.
                    - If the user has responded to another agent, let the same agent respond before routing or responding.
                    - When the user's request is fulfilled, ask for feedback on the service provided before concluding the interaction. Gauge their overall satisfaction and sentiment as either happy or sad. For example, "Before we conclude, could you please provide your feedback on our service today? Were you satisfied with the assistance provided? Would you say your overall experience was happy or sad?"             
                    {CommonAgentRules}
                    {GlobalRules}
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
                    1. Helping customers lodge complaints.
                    2. Providing status updates on existing complaints.
                    3. Taking requests for account details updates, check book requests, and card replacements.

                    Guidelines:
                    - Only entertain requests related user-enrolled services/accounts.
                    - For new product enquiries respond with [[[I CANT HELP]]].
                    - Retrieve the services the user has registered/enrolled from the database.
                    - If no agent is able to assist the user, check if they would like to speak to a tele banker. Tele bankers are available Monday to Friday, 9 AM to 5 PM PST. Check tele banker availability and queue length before suggesting this option.
                    - When taking a new service request or complaint, always ask the user to provide their account ID. Validate that the account ID is part of the enrolled accounts.
                    - Check if there is already a pending service request or complaint matching the current request. If found, inform the user of the status and estimated time of resolution. Ask if the user would like to add any comments and update the existing record with new request comments.
                    - If no match is found, create a new service request or complaint.
                    {CommonAgentRules}.  
                    """,
                    Kernel = supportKernel,
                };

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
                    Kernel = newProductKernel,
                };
            KernelFunction selectionFunction =
                AgentGroupChat.CreatePromptFunctionForStrategy(
                    $$$"""
                Examine the provided RESPONSE and choose the next participant.
                State only the name of the chosen participant without explanation.
                Never choose the participant named in the RESPONSE.

                Choose only from these participants:
                - {{{CordinatorAgent}}}
                - {{{CustomerSupportAgent}}}
                - {{{TransactionsAgent}}}
                - {{{NewProductsAgent}}}

                Always follow these rules when choosing the next participant:
                - Start the chat with {{{CordinatorAgent}}}.
                - {{{CordinatorAgent}}} invokes the next participant only after identifying the user's login.
                - Determine the nature of the user's request and route it to the appropriate agent
                RESPONSE:
                {{$lastmessage}}
                """,
                    safeParameterNames: "lastmessage");

            const string TerminationToken = "yes";

            KernelFunction terminationFunction =
                AgentGroupChat.CreatePromptFunctionForStrategy(
                    $$$"""
                Examine the RESPONSE and determine whether the content has been deemed satisfactory.
                If content is satisfactory, respond with a single word without explanation: {{{TerminationToken}}}.
                If specific suggestions are being provided, it is not satisfactory.
                If no correction is suggested, it is satisfactory.

                RESPONSE:
                {{$lastmessage}}
                """,
                    safeParameterNames: "lastmessage");


            var chatModel = kernel.GetRequiredService<IChatCompletionService>();

            // Set up ChatHistoryTruncationReducer to summarize older chat messages
            var historyReducer = new ChatHistorySummarizationReducer(chatModel, 1000);

            //ChatHistoryTruncationReducer historyReducer = new(1);

            ChatResponseFormat chatResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
             jsonSchemaFormatName: "agent_result",
             jsonSchema: BinaryData.FromString($"""
                {ChatResponseFormatBuilder.BuildFormat(ChatResponseFormatBuilder.ChatResponseFormatBuilderType.Defaut)}
                """));

   

            AgentGroupChat chat =
                new(supportAgent, cordinatorAgent, transactionsAgent, newProductsAgent)
                {
                    ExecutionSettings = new AgentGroupChatSettings
                    {
                        SelectionStrategy =
                            new KernelFunctionSelectionStrategy(selectionFunction, kernel)
                            {
                                // Always start with the editor agent.
                                InitialAgent = cordinatorAgent,
                                // Save tokens by only including the final response
                                HistoryReducer = historyReducer,
                                // The prompt variable name for the history argument.
                                HistoryVariableName = "lastmessage",
                                // Returns the entire result value as a string.
                                ResultParser = (result) => {
                                    Console.WriteLine($"Selection Result:{result}"); // provides visibility (can use logger)
                                    return result.GetValue<string>() ?? cordinatorAgent.Name; // will accept a breakpoint
                                }
                            },
                        TerminationStrategy =
                            new KernelFunctionTerminationStrategy(terminationFunction, kernel)
                            {
                                // Only evaluate for editor's response
                                Agents = [supportAgent],
                                // Save tokens by only including the final response
                                HistoryReducer = historyReducer,
                                // The prompt variable name for the history argument.
                                HistoryVariableName = "lastmessage",
                                // Limit total number of turns
                                MaximumIterations = 2,
                                // user result parser to determine if the response is "yes"
                                //ResultParser = (result) => result.GetValue<string>()?.Contains(TerminationToken, StringComparison.OrdinalIgnoreCase) ?? false
                                ResultParser = (result) => {
                                    Console.WriteLine($"Termination Result:{result}"); // provides visibility (can use logger)
                                    return result.GetValue<string>()?.Contains(TerminationToken, StringComparison.OrdinalIgnoreCase) ?? false;
                                }
                            },
 
                    }
                };


            Console.WriteLine("Ready!");


            chat.AddChatMessage(new ChatMessageContent(AuthorRole.Assistant, "Welcome to ABC Bank"));
            Console.WriteLine($"{"Welcome to ABC Bank"}");
            bool isComplete = false;
            do
            {               

                try
                {
                    await foreach (ChatMessageContent response in chat.InvokeAsync())
                    {
                        Console.WriteLine();
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                        if (!string.IsNullOrEmpty(response.Content))// && response.Content != "[[[I CANT HELP]]]")
                            Console.WriteLine($"{response.AuthorName.ToUpperInvariant()}:{Environment.NewLine}{response.Content}");
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    }

                   

                }
                catch(Exception ex) when(ex.Message.Contains("unable to select next agent"))
                {
                    Debug.WriteLine(ex.Message);
                    chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, "Who else can help?"));

                    chat.IsComplete = false;
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

                Console.WriteLine();
                Console.Write("> ");
                string input = Console.ReadLine();


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
                    continue;
                }

                chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));

                chat.IsComplete = false;

            } while (!isComplete);
        }





    }
}