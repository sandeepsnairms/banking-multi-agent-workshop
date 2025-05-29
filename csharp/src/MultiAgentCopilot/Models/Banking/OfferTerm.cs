using Microsoft.Extensions.VectorData;

namespace MultiAgentCopilot.Models.Banking
{
    public class OfferTerm
    {

        [VectorStoreKey]
        public required string Id { get; set; }

        [VectorStoreData]
        public required string TenantId { get; set; }

        [VectorStoreData]
        public required string OfferId { get; set; }

        [VectorStoreData]
        public required string Name { get; set; }

        [VectorStoreData]
        public required string Text { get; set; }

        [VectorStoreData]
        public required string Type { get; set; }

        [VectorStoreData]
        public required string AccountType { get; set; }

        [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind: IndexKind.QuantizedFlat)]
        public ReadOnlyMemory<float>? Vector { get; set; }

    }
}
