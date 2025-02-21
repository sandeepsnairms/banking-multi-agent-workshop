using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Configuration
{
    public record AzureOpenAISettings
    {
        public required string Endpoint { get; init; }

        public required string Key { get; init; }           

        public required string CompletionsDeployment { get; init; } 
    }
}
