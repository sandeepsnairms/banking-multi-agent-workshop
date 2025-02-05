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
using System.Text.Json;

namespace SKMultiAgent.KernelPlugins
{
    public class CommonOperations
    {

        [KernelFunction("GetLoggedInUser")]
        [Description("Get the current logged-in user")]
        public static async Task<User> GetLoggedInUser()
        {
            var usr = new User { Id = "User123", Name = "John" }; // Simulate returning a hardcoded user            
            Helper.Logger.LogMessage($"Get User: {usr.Id}");
            // Dummy implementation for getting the current user ID
            return usr;// new User { Id = "User123", Name = "John" }; // Simulate returning a hardcoded user
        }


        [KernelFunction("GetCurrentDateTime")]
        [Description("Get the current date time in UTC")]
        public static DateTime GetCurrentDateTime()
        {
                   
            Helper.Logger.LogMessage($"Get Datetime: {System.DateTime.Now.ToUniversalTime()}");
            // Dummy implementation for getting the current user ID
            return System.DateTime.Now.ToUniversalTime();// new User { Id = "User123", Name = "John" }; // Simulate returning a hardcoded user
        }

    }
}
