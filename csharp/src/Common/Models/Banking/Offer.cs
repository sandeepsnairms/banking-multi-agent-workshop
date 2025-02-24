using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Banking
{
    public class Offer
    {
        public string Id { get; set; }
        public string TenantId { get; set; }
        string Name { get; set; }
        public string Description { get; set; }
        public AccountType AccountType { get; set; }
        public Dictionary<string, string> EligibilityConditions { get; set; }
        public Dictionary<string, string> PrerequsiteSubmissions { get; set; }
    }



}
