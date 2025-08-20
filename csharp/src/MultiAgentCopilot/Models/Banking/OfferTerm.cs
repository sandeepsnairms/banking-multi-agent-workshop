namespace MultiAgentCopilot.Models.Banking
{
    public class OfferTerm
    {
        public required string Id { get; set; }

        public required string TenantId { get; set; }

        public required string OfferId { get; set; }

        public required string Name { get; set; }

        public required string Text { get; set; }

        public required string Type { get; set; }

        public required string AccountType { get; set; }

        // Vector will be handled differently in the future when we implement proper vector search
        public ReadOnlyMemory<float>? Vector { get; set; }
    }
}
