using Microsoft.SemanticKernel;
using SKMultiAgent.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SKMultiAgent.Model;

namespace SKMultiAgent.KernelPlugins
{
    public class BasicOperations
    {

        [KernelFunction]
        [Description("Returns the current logged-in user")]
        public static User GetUser()
        {
            // Dummy implementation for getting the current user ID
            return new User { Id = "User123", Name="John" }; // Simulate returning a hardcoded user
        }

        [KernelFunction]
        [Description("This method queries the user collection in Cosmos DB to retrieve a list of services the user has enrolled or registered for. Each service is represented by a unique account number (Id) and may include other details like the account name.")]
        public static List<Account> GetUserAccounts(string userId)
        {
            Debug.WriteLine($"Fetching accounts for User ID: {userId}");
            return new List<Account>
            {
                new Account { Id = "Acc123", Name = "Savings Account" },
                new Account { Id = "Acc456", Name = "Credit Card" },
            };
        }

    }
}
