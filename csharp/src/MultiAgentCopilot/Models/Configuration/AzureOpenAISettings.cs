using System.ComponentModel.DataAnnotations;

namespace MultiAgentCopilot.Models.Configuration
{
    public record AzureOpenAISettings
    {
        [Required]
        public required string Endpoint { get; init; }

        public string? Key { get; init; }           

        [Required]
        public required string CompletionsDeployment { get; init; }

        [Required]
        public required string EmbeddingsDeployment { get; init; }
        
        public string? UserAssignedIdentityClientID { get; init; }
    }
}
