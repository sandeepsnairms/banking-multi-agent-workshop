using Microsoft.SemanticKernel;
using SKMultiAgent.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SKMultiAgent.Helper;

namespace SKMultiAgent.KernelPlugins
{
    public class NewAccountOperations
    {

        [KernelFunction("GetUserProfile")]
        [Description("Get a user's detailed profile")]
        public static string GetUserProfile(string userId)
        {
               
            Helper.Logger.LogMessage($"Get User Profile: {userId}");
            // Dummy implementation for getting the current user ID
            return " User is a high networth individual with a credit score of 800+ and a salary of $100,000 per year.";
        }

        [KernelFunction]
        [Description("List all account types available.")]
        public static List<AccountType> GetAccountTypes()
        {
            Helper.Logger.LogMessage($"Fetching accountTypes");
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
        public static string RegisterAccount(string userId, AccountType accType, string AccountDetailsJson)
        {
            Helper.Logger.LogMessage($"Registering Account. User ID: {userId}, Account Type: {accType}");
            Helper.Logger.LogMessage($"Account Details JSON: {AccountDetailsJson}");
            string registrationId = Guid.NewGuid().ToString();
            Helper.Logger.LogMessage($"Generated Registration ID: {registrationId}");
            return registrationId;
        }



        [KernelFunction]
        [Description("Details required to register a new account")]
        public static List<string> GetAccounRegistrationRequestDetails(
            [Description("Type of Account")]
                 AccountType accType
        )
        {
            Helper.Logger.LogMessage($"Fetching service request details for account type: {accType}");
            // Simulated service request details
            return new List<string> { "Document Verification", "ID Proof", "Address Proof" }; // Dummy details
        }


    }
}
