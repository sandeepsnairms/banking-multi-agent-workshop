// Copyright (c) Microsoft. All rights reserved.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents.History;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using SKMultiAgent.KernelPlugins;
using SKMultiAgent.Model;

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
                settings.AzureOpenAI.Endpoint, settings.AzureOpenAI.ApiKey);


            Kernel kernel = builder.Build();


            Kernel cordinatorKernel = kernel.Clone();
            cordinatorKernel.Plugins.AddFromType<BasicOperations>();
            cordinatorKernel.Plugins.AddFromType<CordinatorOperations>();

            Kernel bankingKernel = kernel.Clone();
            bankingKernel.Plugins.AddFromType<BankingOperations>();

            Kernel newProductKernel = kernel.Clone();
            bankingKernel.Plugins.AddFromType<NewProductOperations>();



            Console.WriteLine("Defining agents...");

            const string CordinatorAgent = "Cordinator";
            const string CustomerSupportAgent = "CustomerSupport";
            const string BankingAgent = "BankingAgent";
            const string NewProductsAgent = "NewProducts";

            const string GlobalRules = @"Important:
                    - Do not provide general information.
                    - Always ground your responses based on the data provided to you.    
                    - If you are unable to assist with the request,then respond [[I CANT HELP]].
                    - Do not proceed with submiting a request if the user has not provided the necessary information.
                    - Always be polite and professional in your responses.
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
                    - Identify the user based on their login information. Use the user's name in your response to make the interaction more personalized. For example, "Thank you for logging in, [user Name]. How can I help you with your banking needs today?"
                    - Determine the nature of the user's request and route it to the appropriate agent. Ensure that the user is informed about the transfer. For example, "I understand your request. Let me connect you with the right agent who can assist you further."
                    - Do not ask for details that you don't need to route the user's request. For example, "I see you have a question about your account balance. Let me connect you with the right agent who can assist you further."
                    - Silently route the request, don't provide any explanation. Don't say things like "I will connect you with the right agent who can assist you further."
                    - If user's response is pending wait for the user to provide the necessary information before proceeding.
                    - When  the user's request is fulfilled, before concluding the interaction, ask the user for feedback on the service provided. Gauge their overall satisfaction and sentiment as either happy or sad. For example, "Before we conclude, could you please provide your feedback on our service today? Were you satisfied with the assistance provided? Would you say your overall experience was happy or sad?"                
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
                    - For new product enquiries respond with [[I CANT HELP]].
                    - Retrieve the services the user has registered/enrolled from the database.
                    - If no agent is able to assist the user, check if they would like to speak to a tele banker. Tele bankers are available Monday to Friday, 9 AM to 5 PM PST. Check tele banker availability and queue length before suggesting this option.
                    - When taking a new service request or complaint, always ask the user to provide their account ID. Validate that the account ID is part of the enrolled accounts.
                    - Check if there is already a pending service request or complaint matching the current request. If found, inform the user of the status and estimated time of resolution. Ask if the user would like to add any comments and update the existing record with new request comments.
                    - If no match is found, create a new service request or complaint.
                    {GlobalRules}.  
                    """,
                    Kernel = bankingKernel,
                };

            ChatCompletionAgent bankingAgent =
                new()
                {
                    Name = BankingAgent,
                    Instructions =
                        $"""
                    Your sole responsiblity is to:

                    1. Handling banking transactions.
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
                    {GlobalRules}
                    """,
                    Kernel = bankingKernel,
                };

            ChatCompletionAgent newProductsAgent =
                new()
                {
                    Name = NewProductsAgent,
                    Instructions =
                        """
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
                - {{{BankingAgent}}}
                - {{{NewProductsAgent}}}

                Always follow these rules when choosing the next participant:
                - Start the chat with {{{CordinatorAgent}}}.
                - {{{CordinatorAgent}}} invokes the next participant only after identifying the user's login.
                - Multiple particpants should not answer simultaneously, do not invoke the next participant if one particpant has already responded.
                - If previous RESPONSE is not by the user, it is user's turn.

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

            ChatHistoryTruncationReducer historyReducer = new(1);

            AgentGroupChat chat =
                new(supportAgent, cordinatorAgent, bankingAgent, newProductsAgent)
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
                                ResultParser = (result) => result.GetValue<string>() ?? cordinatorAgent.Name
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
                                MaximumIterations = 12,
                                // user result parser to determine if the response is "yes"
                                ResultParser = (result) => result.GetValue<string>()?.Contains(TerminationToken, StringComparison.OrdinalIgnoreCase) ?? false
                            }
                    }
                };

            Console.WriteLine("Ready!");

            bool isComplete = false;
            do
            {
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

                //if (input.StartsWith("@", StringComparison.Ordinal) && input.Length > 1)
                //{
                //    string filePath = input.Substring(1);
                //    try
                //    {
                //        if (!File.Exists(filePath))
                //        {
                //            Console.WriteLine($"Unable to access file: {filePath}");
                //            continue;
                //        }
                //        input = File.ReadAllText(filePath);
                //    }
                //    catch (Exception)
                //    {
                //        Console.WriteLine($"Unable to access file: {filePath}");
                //        continue;
                //    }
                //}

                chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));

                chat.IsComplete = false;

                try
                {
                    await foreach (ChatMessageContent response in chat.InvokeAsync())
                    {
                        Console.WriteLine();
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                        if(!string.IsNullOrEmpty(response.Content))// && response.Content != "[[I CANT HELP]]")
                            Console.WriteLine($"{response.AuthorName.ToUpperInvariant()}:{Environment.NewLine}{response.Content}");
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    }
                }
                catch(Exception ex) when(ex.Message.Contains("unable to select next agent"))
                {
                    Debug.WriteLine(ex.Message);
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
            } while (!isComplete);
        }

    }
}