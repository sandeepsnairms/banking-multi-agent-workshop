using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using MultiAgentCopilot.ChatInfrastructure.Services;




namespace MultiAgentCopilot.ChatInfrastructure.Factories
{
    internal static class SystemPromptFactory
    {
        public static string GetAgentName()
        {
            string name = "BasicAgent";
            return name;
        }


        public static string GetAgentPrompts()
        {
            string prompt =File.ReadAllText("Prompts/CommonAgentRules.prompty");
            return prompt;
        }

        
    }
}
