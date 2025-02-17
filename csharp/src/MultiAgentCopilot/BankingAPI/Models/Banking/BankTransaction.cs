using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Models.Banking
{
    public class BankTransaction
    {
        string Id { get; set; }
        int DebitAmount { get; set; }
        int CreditAmount { get; set;}
        long AccountBalance { get; set; }
        string Details { get; set; }
    }
}
