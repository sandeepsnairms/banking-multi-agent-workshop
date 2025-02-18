using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Banking
{
    public class BankUser
    {
        public string Id { get; set; } = string.Empty;
        public string TenanatId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public List<BankAccount> Accounts { get; set; }
        public Dictionary<string,string> Attributes { get; set; }
    }
}
