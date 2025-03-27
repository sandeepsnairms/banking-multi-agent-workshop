using MultiAgentCopilot.ChatInfrastructure.Models;
using static MultiAgentCopilot.ChatInfrastructure.StructuredFormats.ChatResponseFormatBuilder;

internal static class SystemPromptFactory
{
    //Replace from here
    public static string GetAgentName(AgentType agentType)
    {
        string name = string.Empty;
        switch (agentType)
        {
            case AgentType.Sales:
                name = "Sales";
                break;
            case AgentType.Transactions:
                name = "Transactions";
                break;
            case AgentType.CustomerSupport:
                name = "CustomerSupport";
                break;
            case AgentType.Coordinator:
                name = "Coordinator";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null);
        }

        return name;//.ToUpper();
    }

    public static string GetAgentPrompts(AgentType agentType)
    {
        string promptFile = string.Empty;
        switch (agentType)
        {
            case AgentType.Sales:
                promptFile = "Sales.prompty";
                break;
            case AgentType.Transactions:
                promptFile = "Transactions.prompty";
                break;
            case AgentType.CustomerSupport:
                promptFile = "CustomerSupport.prompty";
                break;
            case AgentType.Coordinator:
                promptFile = "Coordinator.prompty";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null);
        }

        string prompt = $"{File.ReadAllText("Prompts/" + promptFile)}{File.ReadAllText("Prompts/CommonAgentRules.prompty")}";

        return prompt;
    }
    //end replace


    public static string GetStrategyPrompts(ChatResponseStrategy strategyType)
    {
        string prompt = string.Empty;
        switch (strategyType)
        {
            case ChatResponseStrategy.Continuation:
                prompt = File.ReadAllText("Prompts/SelectionStrategy.prompty");
                break;
            case ChatResponseStrategy.Termination:
                prompt = File.ReadAllText("Prompts/TerminationStrategy.prompty");
                break;

        }
        return prompt;
    }

}