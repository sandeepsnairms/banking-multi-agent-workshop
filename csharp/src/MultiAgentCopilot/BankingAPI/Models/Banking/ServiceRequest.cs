using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Models.Banking
{
    public class ServiceRequest
    {
        public string Id { get; set; }
        public string TenantId { get; set; }
        public string UserId { get; set; }
        public DateTime RequestedOn { get; set; }
        public DateTime ScheduledDateTime { get; set; }
        public string AccountId { get; set; }
        public ServiceRequestType  SRType { get; set; }
        public string? RecipientEmail { get; set; }
        public string? RecipientPhone { get; set; }
        public decimal? DebitAmount { get; set; }
        public bool IsComplete { get; set; }
        public List<string> RequestAnnotations  { get; set; }
        public Dictionary<string,string> FulfilmentDetails { get; set; }

        internal ServiceRequest(ServiceRequestType serviceRequestType, string tenantId, string accountId, string userId, string requestAnnotation, string recipientEmail, string recipientPhone, decimal debitAmount,  DateTime scheduledDateTime, Dictionary<string,string>? fulfilmentDetails)
        {
            Id = Guid.NewGuid().ToString();
            TenantId = tenantId;
            SRType = serviceRequestType;
            RequestedOn = DateTime.Now;
            AccountId = accountId;
            UserId = userId;
            RequestAnnotations  = new List<string> { requestAnnotation };
            RecipientEmail = recipientEmail;
            RecipientPhone = recipientPhone;
            DebitAmount = debitAmount;
            if (scheduledDateTime != DateTime.MinValue)
                ScheduledDateTime = scheduledDateTime;
            IsComplete = false;
            if(fulfilmentDetails != null)
                FulfilmentDetails = fulfilmentDetails;

        }

        

    }
}



