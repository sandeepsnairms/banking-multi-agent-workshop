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
    public class BankingOperations 
    {

        [KernelFunction]
        [Description("Adds a new transaction request")]
        public static void AddTransactionRequest(
            string userId,
            string debitAccountNumber,
            decimal amount,
            string debitNote,
            string? recipientPhoneNumber = null,
            string? recipientEmailId = null)
        {
             Helper.Logger.LogMessage($"Adding transaction request for User ID: {userId}, Debit Account: {debitAccountNumber}");
             Helper.Logger.LogMessage($"Amount: {amount}, Note: {debitNote}");
            if (!string.IsNullOrEmpty(recipientPhoneNumber))
                 Helper.Logger.LogMessage($"Recipient Phone: {recipientPhoneNumber}");
            if (!string.IsNullOrEmpty(recipientEmailId))
                 Helper.Logger.LogMessage($"Recipient Email: {recipientEmailId}");

             Helper.Logger.LogMessage("Transaction request added to the database (simulated).");
        }

        [KernelFunction]
        [Description("Get the account balance")]
        public static long GetAccountBalance(string accountId)
        {
            Helper.Logger.LogMessage($"Fetching balance for Account: {accountId}, Balance: 1234");
            return 1234;
        }

        [KernelFunction]
        [Description("Get the transactions history between 2 dates")]
        public static List<Transaction> GetTransactionHistory(string accountId, DateTime startDate, DateTime endDate)
        {
            Helper.Logger.LogMessage($"Fetching transaction history for Account: {accountId}, From: {startDate} To: {endDate}");
            return new List<Transaction>
            {
                new Transaction
                {
                    Date = DateTime.Now.AddDays(-10),
                    DebitAmount = 200,
                    CreditAmount = 0,
                    AccountBalance = 1800,
                    DebitNote = "Grocery Shopping",
                    CreditNote = null
                },
                new Transaction
                {
                    Date = DateTime.Now.AddDays(-5),
                    DebitAmount = 0,
                    CreditAmount = 1500,
                    AccountBalance = 2000,
                    DebitNote = null,
                    CreditNote = "Salary"
                },
                new Transaction
                {
                    Date = DateTime.Now.AddDays(-6),
                    DebitAmount = 0,
                    CreditAmount = 500,
                    AccountBalance = 2500,
                    DebitNote = null,
                    CreditNote = "Interest"
                },
                new Transaction
                {
                    Date = DateTime.Now.AddDays(-2),
                    DebitAmount = 60,
                    CreditAmount = 0,
                    AccountBalance = 2440,
                    DebitNote = "Card Payment",
                    CreditNote =null
                }
            };
        }

    }
}
