using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using MultiAgentCopilot.Common.Models.BusinessDomain;

namespace MultiAgentCopilot.ChatInfrastructure.Plugins
{
    internal class SalesPlugin
    {

        private readonly ILogger<SalesPlugin> _logger;

        public SalesPlugin(ILogger<SalesPlugin> logger)
        {
            _logger = logger;
        }


        [KernelFunction("GetUserProfile")]
        [Description("Get a user's detailed profile")]
        public string GetUserProfile(string userId)
        {

            _logger.LogTrace($"Get User Profile: {userId}");
            // Dummy implementation for getting the current user ID
            return " User is a high networth individual with a credit score of 800+ and a salary of $100,000 per year.";
        }

        [KernelFunction]
        [Description("List all account types available.")]
        public List<AccountType> GetAccountTypes()
        {
            _logger.LogTrace($"Fetching accountTypes");
            return new List<AccountType>
            {
                new AccountType
                {
                    AccountId = "Prod004",
                    Name = "Savings Account",
                    Description = "A savings account.",
                    EligibilityCriteria = "Minimum salary of $5,000 per year.",
                    RegistrationDetails = "Proof of income, ID, Proof of Age."
                },
                new AccountType
                {
                    AccountId = "Prod001",
                    Name = "Personal Loan",
                    Description = "A loan for personal use.",
                    EligibilityCriteria = "Minimum salary of $30,000 per year.",
                    RegistrationDetails = "ID, SSN"
                },
                new AccountType
                {
                    AccountId = "Prod002",
                    Name = "Credit Card",
                    Description = "A card with flexible credit limits.",
                    EligibilityCriteria = "Credit score of 200+.",
                    RegistrationDetails = "Proof of income, credit score, and ID."
                },
                new AccountType
                {
                    AccountId = "Prod002",
                    Name = "Premium Credit Card",
                    Description = "A card with flexible credit limits.",
                    EligibilityCriteria = "Credit score of 700+.",
                    RegistrationDetails = "Proof of income, credit score, and ID."
                },
                 new AccountType
                {
                    AccountId = "Prod003",
                    Name = "Locker",
                    Description = "A secure locker.",
                    EligibilityCriteria = "Credit score of 700+.",
                    RegistrationDetails = "Proof of income, credit score, and ID."
                }
            };
        }

        [KernelFunction]
        [Description("Register a new account.")]
        public string RegisterAccount(string userId, AccountType accType, string AccountDetailsJson)
        {
            _logger.LogTrace($"Registering Account. User ID: {userId}, Account Type: {accType}");
            _logger.LogTrace($"Account Details JSON: {AccountDetailsJson}");
            string registrationId = Guid.NewGuid().ToString();
            _logger.LogTrace($"Generated Registration ID: {registrationId}");
            return registrationId;
        }



        [KernelFunction]
        [Description("Details required to register a new account")]
        public List<string> GetAccounRegistrationRequestDetails(
            [Description("Type of Account")]
                 AccountType accType
        )
        {
            _logger.LogTrace($"Fetching service request details for account type: {accType}");
            // Simulated service request details
            return new List<string> { "Document Verification", "ID Proof", "Address Proof" }; // Dummy details
        }

    }
}
