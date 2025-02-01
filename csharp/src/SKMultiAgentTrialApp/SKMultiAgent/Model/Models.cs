using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKMultiAgent.Model
{
    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
    public class Account
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class Transaction
    {
        public DateTime Date { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal AccountBalance { get; set; }
        public string DebitNote { get; set; }
        public string CreditNote { get; set; }
    }

    public class Product
    {
        public string ProductId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string EligibilityCriteria { get; set; }
        public string RegistrationDetails { get; set; }
    }
}
