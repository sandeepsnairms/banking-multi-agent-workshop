using Microsoft.SemanticKernel;
using SKMultiAgent.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKMultiAgent.KernelPlugins
{
    public class BasicOperations
    {

        //[KernelFunction]
        //[Description("Returns the current logged-in user")]
        //public static User GetUser()
        //{
        //    // Dummy implementation for getting the current user ID
        //    var usr =new User { Id = "User123", Name = "John" }; // Simulate returning a hardcoded user

        //    LogMessage($"Get User: {usr.Id}");
        //    return usr;
        //}

        [KernelFunction]
        [Description("Returns the current logged-in user")]
        public static User GetUser()
        {
            // Dummy implementation for getting the current user ID
            return new User { Id = "User123", Name = "John" }; // Simulate returning a hardcoded user
        }


        [KernelFunction]
        [Description("Queries the user collection to retrieve list of enrolled services. A service is identified by a unique Id.")]
        public static List<Account> GetUserAccounts(string userId)
        {
            LogMessage($"Fetching accounts for User ID: {userId}");
            return new List<Account>
            {
                new Account { Id = "Acc123", Name = "Savings Account" },
                new Account { Id = "Acc456", Name = "Credit Card" },
            };
        }

        public static void LogMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine("#[PuginCall] " + message);
            Console.ResetColor();
        }

    }
}
