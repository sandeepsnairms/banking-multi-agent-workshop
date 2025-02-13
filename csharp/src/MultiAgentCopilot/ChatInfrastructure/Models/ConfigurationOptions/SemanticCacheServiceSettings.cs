namespace MultiAgentCopilot.ChatInfrastructure.Models.ConfigurationOptions
{
    public record SemanticCacheServiceSettings
    {
        public int ConversationContextMaxTokens { get; set; }

        public int EmbeddingDimensions { get; set; }
    }
}
