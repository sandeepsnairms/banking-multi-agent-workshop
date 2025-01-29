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
    public class BankingOperations : BasicOperations
    {

        [KernelFunction]
        [Description("This method adds a new transaction request to the TransactionRequest collection in Cosmos DB. The method takes several parameters: the user ID, the debit account number, the transaction amount, a debit note, and an optional recipient phone number or email ID.")]
        public static void PutTransactionRequest(
            string userId,
            string debitAccountNumber,
            decimal amount,
            string debitNote,
            string recipientPhoneNumber = null,
            string recipientEmailId = null)
        {
            Debug.WriteLine($"Adding transaction request for User ID: {userId}, Debit Account: {debitAccountNumber}");
            Debug.WriteLine($"Amount: {amount}, Note: {debitNote}");
            if (!string.IsNullOrEmpty(recipientPhoneNumber))
                Debug.WriteLine($"Recipient Phone: {recipientPhoneNumber}");
            if (!string.IsNullOrEmpty(recipientEmailId))
                Debug.WriteLine($"Recipient Email: {recipientEmailId}");

            Debug.WriteLine("Transaction request added to the database (simulated).");
        }

        [KernelFunction]
        [Description("This method retrieves the transaction history for a specified account within a given date range. It returns the records sorted in descending order of transaction date. Each record includes the date, credit amount, debit amount, account balance, and optional debit/credit notes.")]
        public static List<Transaction> GetTransactionHistory(string userId, string accountNumber, DateTime startDate, DateTime endDate)
        {
            Debug.WriteLine($"Fetching transaction history for User ID: {userId}, Account: {accountNumber}, From: {startDate} To: {endDate}");
            return new List<Transaction>
            {
                new Transaction
                {
                    Date = new DateTime(2023, 11, 15),
                    DebitAmount = 200,
                    CreditAmount = 0,
                    AccountBalance = 1800,
                    DebitNote = "Grocery Shopping",
                    CreditNote = null
                },
                new Transaction
                {
                    Date = new DateTime(2023, 11, 1),
                    DebitAmount = 0,
                    CreditAmount = 1500,
                    AccountBalance = 2000,
                    DebitNote = null,
                    CreditNote = "Salary"
                }
            };
        }

    }
}
