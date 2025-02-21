namespace BankingAPI.Models.Configuration
{
    public record BankingCosmosDBSettings
    {
        public required string CosmosUri { get; init; }

        public string CosmosKey { get; init; }

        public required string Database { get; init; }

        public bool EnableTracing { get; init; }

        public required string AccountsContainer { get; init; }

        public required string UserDataContainer { get; init; }

        public required string RequestDataContainer { get; init; }

        public required string OfferDataContainer { get; init; }        

    }
}
