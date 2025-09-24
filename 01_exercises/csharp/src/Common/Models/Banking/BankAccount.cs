using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Banking
{
    public class BankAccount
    {
        public required string Id { get; set; } = string.Empty;
        public required string TenantId { get; set; } = string.Empty;
        public required string Name { get; set; } = string.Empty;
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public required AccountType AccountType { get; set; }
        public long Balance { get; set; }
        public long Limit { get; set; }
        public int InterestRate { get; set; }
        public required string ShortDescription { get; set; }
    }
}