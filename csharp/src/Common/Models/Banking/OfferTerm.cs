using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Banking
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
        public required float[] Vector { get; set; }
        
    }


    public class OfferTermBasic
    {
        public required string Id { get; set; }
        public required string OfferId { get; set; }
        public required string Name { get; set; }
        public required string Text { get; set; }
    }



}
