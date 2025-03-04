namespace MultiAgentCopilot.Common.Models.Configuration
{
    public record CosmosDBSettings
    {
        public required string CosmosUri { get; init; }

        public string? CosmosKey { get; init; }

        public required string Database { get; init; }

        public bool EnableTracing { get; init; }

        public required string ChatDataContainer { get; init; }

        public required string UserDataContainer { get; init; }

        public required string UserAssignedIdentityClientID { get; init; }
    }
}
