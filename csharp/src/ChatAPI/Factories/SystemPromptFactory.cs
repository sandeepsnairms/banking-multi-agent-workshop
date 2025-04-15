﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.StructuredFormats;
using static MultiAgentCopilot.StructuredFormats.ChatResponseFormatBuilder;
using MultiAgentCopilot.Models;


namespace MultiAgentCopilot.Factories
{
    internal static class SystemPromptFactory
    {
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

        public static string GetStratergyPrompts(ChatResponseStrategy strategyType)
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
}
