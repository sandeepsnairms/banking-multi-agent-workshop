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
    public class CommonOperations
    {

        [KernelFunction]
        [Description("Returns the current logged-in user")]
        public static User GetLoggedInUser()
        {
            var usr = new User { Id = "User123", Name = "John" }; // Simulate returning a hardcoded user            
            Helper.Logger.LogMessage($"Get User: {usr.Id}");
            // Dummy implementation for getting the current user ID
            return usr;// new User { Id = "User123", Name = "John" }; // Simulate returning a hardcoded user
        }


        [KernelFunction]
        [Description("Returns list of enrolled accounts. A account is identified by a unique Id.")]
        public static List<Account> GetUserEnrolledAccounts(string userId)
        {
            Helper.Logger.LogMessage($"Fetching accounts for User ID: {userId}");
            return new List<Account>
            {
                new Account { Id = "Acc123", Name = "Savings Account" },
                new Account { Id = "Acc456", Name = "Credit Card" },
            };
        }

        [KernelFunction]
        [Description("Checks if account is valid, pass account Id and user login Id")]
        public static bool VerifyAccount(string accountId, string userId)
        {
            Helper.Logger.LogMessage($"Validating account for User ID: {userId}- {accountId}");
            return true;
        }


        [KernelFunction]
        [Description("Searches the database for pending requests, use natural language description of the service requested.")]
        public static List<string> ServiceRequest_CheckPendingRequests(string userId, string accountId, string requestDescription)
        {
            Helper.Logger.LogMessage($"Searching database for matching requests for User: {userId}, Account: {accountId}");
            // Simulated vector search
            return new List<string> { "Request1", "Request2" }; // Dummy matching requests
        }


        

    }
}
