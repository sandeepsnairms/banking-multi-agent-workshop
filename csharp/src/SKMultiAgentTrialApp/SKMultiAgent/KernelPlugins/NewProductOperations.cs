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
    public class NewProductOperations
    {

        [KernelFunction]
        [Description("This method retrieves a list of all products available in the bank. The list can optionally be filtered by a specific type, such as \"Loans\" or \"Credit Cards.\" Each product is represented by its unique product ID, along with details such as the product's name, description, eligibility criteria, and registration requirements.")]
        public static List<Product> GetProducts(string type = null)
        {
            Debug.WriteLine($"Fetching products. Filter Type: {type}");
            return new List<Product>
            {
                new Product
                {
                    ProductId = "Prod001",
                    Name = "Personal Loan",
                    Description = "A loan for personal use.",
                    EligibilityCriteria = "Minimum salary of $30,000 per year.",
                    RegistrationDetails = "Proof of income, ID, and application form."
                },
                new Product
                {
                    ProductId = "Prod002",
                    Name = "Credit Card",
                    Description = "A card with flexible credit limits.",
                    EligibilityCriteria = "Credit score of 700+.",
                    RegistrationDetails = "Proof of income, credit score, and ID."
                },
                 new Product
                {
                    ProductId = "Prod003",
                    Name = "Locker",
                    Description = "A secure locker.",
                    EligibilityCriteria = "Credit score of 700+.",
                    RegistrationDetails = "Proof of income, credit score, and ID."
                }
            };
        }

        [KernelFunction]
        [Description("This method allows a user to register for any of the products. It takes the user ID, product ID, and product registration details as a JSON property bag. The details contain field values specific to the product. The method stores the registration request in the database and returns a unique registration ID, which can be used for tracking the status of the request.")]
        public static string RegisterProduct(string userId, string productId, string productDetailsJson)
        {
            Debug.WriteLine($"Registering Product. User ID: {userId}, Product ID: {productId}");
            Debug.WriteLine($"Product Details JSON: {productDetailsJson}");
            string registrationId = Guid.NewGuid().ToString();
            Debug.WriteLine($"Generated Registration ID: {registrationId}");
            return registrationId;
        }



    }
}
