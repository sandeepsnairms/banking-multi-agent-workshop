using Microsoft.Azure.Cosmos;

namespace MultiAgentCopilot.ChatInfrastructure.Interfaces
{
    public interface ICosmosDBClientFactory
    {
        public string DatabaseName { get; }

        public CosmosClient Client { get; }
    }
}
