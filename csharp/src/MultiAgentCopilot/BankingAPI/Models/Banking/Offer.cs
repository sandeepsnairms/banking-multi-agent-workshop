using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Models.Banking
{
    public class Offer
    {
        string Id { get; set; }
        string TenantId { get; set; }
        string Name { get; set; }
        string Description { get; set; }
        AccountType AccountType { get; set; }
        Dictionary <string, string> Tags { get; set; }
        Dictionary<string, string> PreRequsites { get; set; }
    }



}
