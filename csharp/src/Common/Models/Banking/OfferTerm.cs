using Microsoft.Extensions.VectorData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Banking
{
    public class OfferTerm
    {
        [VectorStoreRecordKey]
        public required string Id { get; set; }

        [VectorStoreRecordData]
        public required string TenantId { get; set; }

        [VectorStoreRecordData]
        public required string OfferId { get; set; }

        [VectorStoreRecordData]
        public required string Name { get; set; }

        [VectorStoreRecordData]
        public required string Text { get; set; }

        [VectorStoreRecordData]
        public required string Type { get; set; }

        [VectorStoreRecordData]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public required AccountType AccountType { get; set; }

        [VectorStoreRecordVector(Dimensions: 1536, DistanceFunction: Microsoft.Extensions.VectorData.DistanceFunction.CosineSimilarity, IndexKind: Microsoft.Extensions.VectorData.IndexKind.QuantizedFlat)]
        public ReadOnlyMemory<float>? Vector { get; set; }

    }

}
