using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Banking
{
    public class OfferTerm
    {
        public string Id { get; set; }
        public string TenantId { get; set; }
        public string OfferId { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string Type { get; set; }
        public string AccountType { get; set; }
        public float[] Vector { get; set; }
        
    }


    public class OfferTermBasic
    {
        public string Id { get; set; }
        public string OfferId { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
    }



}
