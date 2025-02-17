using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Models.Banking
{
    public class BankAccount
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }
        public long Balance { get; set; }
        public long Limit { get; set; }
        public int InterestRate { get; set; }
        public string ShortDescription { get; set; }
    }
}
