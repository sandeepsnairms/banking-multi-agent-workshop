using MultiAgentCopilot.Common.Models.Configuration;
using MultiAgentCopilot.ChatInfrastructure.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Azure.Identity;

namespace MultiAgentCopilot.ChatInfrastructure.Factories
{
    public class CosmosDBClientFactory(
        IOptions<CosmosDBSettings> settings) : ICosmosDBClientFactory
    {
        private readonly CosmosDBSettings _settings = settings.Value;

        private readonly CosmosClient _client = new CosmosClient(
            settings.Value.CosmosUri,
            new DefaultAzureCredential(),
        new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway
        });

        public CosmosClient Client => _client;

        public string DatabaseName => _settings.Database;
    }
}
