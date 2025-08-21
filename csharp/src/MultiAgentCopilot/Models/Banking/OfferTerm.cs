using System.Text.Json.Serialization;

namespace MultiAgentCopilot.Models.Banking
{
    public class OfferTerm
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("tenantId")]
        public required string TenantId { get; set; }

        [JsonPropertyName("offerId")]
        public required string OfferId { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("text")]
        public required string Text { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("accountType")]
        public required string AccountType { get; set; }

        [JsonPropertyName("vector")]
        public ReadOnlyMemory<float>? Vector { get; set; }
    }
}
