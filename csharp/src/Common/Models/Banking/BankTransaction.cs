using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Banking
{

    public class BankTransaction
    {
        public string Id { get; set; }
        public string TenantId { get; set; }
        public string AccountId { get; set; }
        public int DebitAmount { get; set; }
        public int CreditAmount { get; set; }
        public long AccountBalance { get; set; }
        public string Details { get; set; }
        public DateTime TransactionDateTime { get; set; }
    }
}
