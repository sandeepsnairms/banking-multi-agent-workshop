using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace  MultiAgentCopilot.Models.Configuration
{
    public record OllamaSettings
    {
        public required string Endpoint { get; init; }

        public required string CompletionsDeployment { get; init; }

        public required string EmbeddingsDeployment { get; init; }
        
    }
}
