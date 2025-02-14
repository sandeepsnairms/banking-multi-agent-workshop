using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.BusinessDomain
{
    public class BankUser
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
    public class Account
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class AccountTransaction
    {
        public DateTime Date { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal AccountBalance { get; set; }
        public string? DebitNote { get; set; }
        public string? CreditNote { get; set; }
    }

    public class AccountType
    {
        public string AccountId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string EligibilityCriteria { get; set; } = string.Empty;
        public string RegistrationDetails { get; set; } = string.Empty;
    }

    public class ServiceRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public string userId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string RequestDescription { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public DateTime ResolutionETA { get; set; }
        public DateTime ResolutionDate { get; set; }
        public bool IsResolved { get; set; }
    }
}
