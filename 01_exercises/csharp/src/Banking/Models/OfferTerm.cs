namespace Banking.Models
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
        public ReadOnlyMemory<float>? Vector { get; set; }
    }
}
