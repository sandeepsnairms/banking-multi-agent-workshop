using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Banking
{
    public class Offer
    {
        public required string Id { get; set; }
        public required string TenantId { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public required AccountType AccountType { get; set; }
        public required Dictionary<string, string> EligibilityConditions { get; set; }
        public required Dictionary<string, string> PrerequsiteSubmissions { get; set; }
    }



}
