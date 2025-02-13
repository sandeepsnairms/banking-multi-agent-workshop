namespace MultiAgentCopilot.Common.Models.Configuration
{
    public record CosmosDBSettings
    {
        public required string CosmosUri { get; init; }

        public string CosmosKey { get; init; }

        public required string Database { get; init; }

        public bool EnableTracing { get; init; }

        public required string Container { get; init; }

        //public required string MonitoredContainers { get; init; }

        //public required string ChangeFeedLeaseContainer { get; init; }

        //public required string ChangeFeedSourceContainer { get; init; }
    }
}
