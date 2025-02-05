using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SKMultiAgent.Model;
using SKMultiAgent.Helper;
using System.Text.Json;

namespace SKMultiAgent.KernelPlugins
{
    public class SupportOperations 
    {
        [KernelFunction("GetUserRegisteredAccounts")]
        [Description("Get accounts registered accounts")]
        public static List<Account> GetUserRegisteredAccounts(
            [Description("Users Login Id")]
                string userId
            )
        {
            Helper.Logger.LogMessage($"Fetching accounts for User ID: {userId}");
            var accounts = new List<Account>
            {
                new Account { Id = "Acc123", Name = "Savings Account" },
                new Account { Id = "Acc456", Name = "Credit Card" },
            };
            return accounts;
            //return JsonSerializer.Serialize(accounts);
        }

        [KernelFunction("IsAccountRegisteredToUser")]
        [Description("Check if account is registered to user")]
        public static bool IsAccountRegisteredToUser(
             [Description("Account Id Of User")]
                    string accountId,
              [Description("Users Login Id")]
                    string userId
        )
        {
            Helper.Logger.LogMessage($"Validating account for User ID: {userId}- {accountId}");
            return true;
        }

        [KernelFunction("CheckPendingServiceRequests")]
        [Description("Search the database for pending requests")]
        public static List<ServiceRequest> CheckPendingServiceRequests(
            [Description("Users Login Id")]
                string userId,
            [Description("Account Id Of User")]
                string accountId,
            [Description("Natural language description of the request")]
            string requestDescription
        )
        {
            Helper.Logger.LogMessage($"Searching database for matching requests for User: {userId}, Account: {accountId}");
            // Simulated vector search

            ServiceRequest sr1= new ServiceRequest { AccountId="acc123", RequestDescription="abcd", ResolutionETA= DateTime.Now.AddDays(2), UserId= "User123",RequestId ="SR1234" , IsResolved=false,RequestDate = DateTime.Now.AddDays(-2) };
            ServiceRequest sr2 = new ServiceRequest { AccountId = "acc123", RequestDescription = "abcd", ResolutionETA = DateTime.Now.AddDays(2), UserId = "User123", RequestId = "SR1234", IsResolved = false, RequestDate = DateTime.Now.AddDays(-2) };

            var retObj = new List<ServiceRequest> { sr1, sr2 }; // Dummy matching requests

            return retObj;
        }
                         
        [KernelFunction]
        [Description("Adds a telebanker callback request for the specified account.")]
        public static string AddTeleBankerRequest(
             [Description("")] 
                string userId,
             [Description("")]
                string accountId
        )
        {
            Helper.Logger.LogMessage($"Adding Tele Banker request for User: {userId}, account: {accountId}");
            // Simulated callback time
            return "15 minutes"; // Dummy callback time
        }

        [KernelFunction]
        [Description("Checks the availability of telebankers for a specific account type and provides the estimated time for contact.")]
        public static string IsTeleBankerAvailable(
            [Description("")]
                string accountType
        )
        {
            Helper.Logger.LogMessage($"Checking availability for Tele Banker for account: {accountType}");
            // Simulated availability check
            return "Next available Tele Banker in 10 minutes"; // Dummy availability time
        }
       

        [KernelFunction]
        [Description("Create new service request")]
        public static string CreateNewServiceRequest(
            [Description("")]
                string userId,
            [Description("")]
                string accountId,
            [Description("")]
                string requestDetails,
            [Description("")]
                string requestDescription
        )
        {
            Helper.Logger.LogMessage($"Adding new service request for User: {userId}, Account: {accountId}");
            Helper.Logger.LogMessage($"Request Details: {requestDetails}");
            Helper.Logger.LogMessage($"Request Description: {requestDescription}");
            // Simulated service request ID
            return "SR12345"; // Dummy request ID
        }

        [KernelFunction]
        [Description("Updates an existing service request with additional details")]
        public static void UpdateExistingServiceRequest(
            [Description("")]
                string userId,
            [Description("")] 
                 string accountId,
            [Description("")]
                string requestDetails,
            [Description("")]
                string requestDescription
        )
        {
            Helper.Logger.LogMessage($"Updating service request for User: {userId}, Account: {accountId}");
            Helper.Logger.LogMessage($"Request Details: {requestDetails}");
            Helper.Logger.LogMessage($"Request Description: {requestDescription}");
            // Simulated update
            Helper.Logger.LogMessage("Service request updated successfully (simulated).");
        }

        /*
        [KernelFunction]
        [Description("Summarizes the JSON into a natural language description.")]
        public static string ConvertJSONToNaturalLanguage(
            [Description("")]
                string requestJSON
        )
        {
            Helper.Logger.LogMessage($"Using LLM to convert request details to natural language: {requestJSON}");
            // Simulated natural language generation
            return $"Please {requestJSON} for my account."; // Dummy generated description
        }
        */


    }
}
