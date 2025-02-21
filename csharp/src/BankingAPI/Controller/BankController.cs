using BankingAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using MultiAgentCopilot.Common.Models.Banking;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace BankingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BankController : ControllerBase
    {
        private readonly IBankDBService _bankDBService;

        public BankController(IBankDBService bankDBService)
        {
            _bankDBService = bankDBService;
        }

        [HttpGet("user/{tenantId}/{userId}")]
        public async Task<ActionResult<BankUser>> GetUserAsync(string tenantId, string userId)
        {
            var user = await _bankDBService.GetUserAsync(tenantId, userId);
            if (user == null)
            {
                return NotFound();
            }
            return Ok(user);
        }

        [HttpGet("account/{tenantId}/{userId}")]
        public async Task<ActionResult<BankAccount>> GetAccountDetailsAsync(string tenantId, string userId,string accountId)
        {
            var account = await _bankDBService.GetAccountDetailsAsync(tenantId, userId, accountId);
            if (account == null)
            {
                return NotFound();
            }
            return Ok(account);
        }

        [HttpGet("transactions/{tenantId}/{accountId}")]
        public async Task<ActionResult<List<BankTransaction>>> GetTransactionsAsync(string tenantId, string accountId, DateTime startDate, DateTime endDate)
        {
            var transactions = await _bankDBService.GetTransactionsAsync(tenantId, accountId, startDate, endDate);
            return Ok(transactions);
        }

        [HttpPost("fund-transfer")]
        public async Task<ActionResult<ServiceRequest>> CreateFundTransferRequestAsync([FromBody] FundTransferRequest request)
        {
            var serviceRequest = await _bankDBService.CreateFundTransferRequestAsync(request.TenantId, request.AccountId, request.UserId, request.RequestAnnotation, request.RecipientEmail, request.RecipientPhone, request.DebitAmount);
            return Ok(serviceRequest);
        }

        [HttpPost("tele-banker")]
        public async Task<ActionResult<ServiceRequest>> CreateTeleBankerRequestAsync([FromBody] TeleBankerRequest request)
        {
            var serviceRequest = await _bankDBService.CreateTeleBankerRequestAsync(request.TenantId, request.AccountId, request.UserId, request.RequestAnnotation, request.ScheduledDateTime);
            return Ok(serviceRequest);
        }

        [HttpPost("complaint")]
        public async Task<ActionResult<ServiceRequest>> CreateComplaintAsync([FromBody] ComplaintRequest request)
        {
            var serviceRequest = await _bankDBService.CreateComplaintAsync(request.TenantId, request.AccountId, request.UserId, request.RequestAnnotation);
            return Ok(serviceRequest);
        }

        [HttpPost("fulfilment")]
        public async Task<ActionResult<ServiceRequest>> CreateFulfilmentRequestAsync([FromBody] FulfilmentRequest request)
        {
            var serviceRequest = await _bankDBService.CreateFulfilmentRequestAsync(request.TenantId, request.AccountId, request.UserId, request.RequestAnnotation, request.FulfilmentDetails);
            return Ok(serviceRequest);
        }

        [HttpGet("service-requests/{tenantId}/{accountId}")]
        public async Task<ActionResult<List<ServiceRequest>>> GetServiceRequestsAsync(string tenantId, string accountId, string? userId = null, ServiceRequestType? SRType = null)
        {
            var serviceRequests = await _bankDBService.GetServiceRequestsAsync(tenantId, accountId, userId, SRType);
            return Ok(serviceRequests);
        }

        [HttpPost("service-request-description")]
        public async Task<ActionResult<bool>> AddServiceRequestDescriptionAsync([FromBody] ServiceRequestDescription request)
        {
            var result = await _bankDBService.AddServiceRequestDescriptionAsync(request.TenantId, request.AccountId, request.RequestId, request.AnnotationToAdd);
            return Ok(result);
        }

        [HttpGet("user-accounts/{tenantId}/{userId}")]
        public async Task<ActionResult<List<BankAccount>>> GetUserRegisteredAccountsAsync(string tenantId, string userId)
        {
            var accounts = await _bankDBService.GetUserRegisteredAccountsAsync(tenantId, userId);
            return Ok(accounts);
        }

        [HttpGet("offers/{tenantId}/{accountType}")]
        public async Task<ActionResult<List<Offer>>> GetOffersAsync(string tenantId, AccountType accountType)
        {
            var offers = await _bankDBService.GetOffersAsync(tenantId, accountType);
            return Ok(offers);
        }

        [HttpGet("tele-banker-availability/{tenantId}/{accountType}")]
        public async Task<ActionResult<List<string>>> GetTeleBankerAvailabilityAsync(string tenantId, AccountType accountType)
        {
            var availability = await _bankDBService.GetTeleBankerAvailabilityAsync(tenantId, accountType);
            return Ok(availability);
        }
    }

    public class FundTransferRequest
    {
        public string TenantId { get; set; }
        public string AccountId { get; set; }
        public string UserId { get; set; }
        public string RequestAnnotation { get; set; }
        public string RecipientEmail { get; set; }
        public string RecipientPhone { get; set; }
        public decimal DebitAmount { get; set; }
    }

    public class TeleBankerRequest
    {
        public string TenantId { get; set; }
        public string AccountId { get; set; }
        public string UserId { get; set; }
        public string RequestAnnotation { get; set; }
        public DateTime ScheduledDateTime { get; set; }
    }

    public class ComplaintRequest
    {
        public string TenantId { get; set; }
        public string AccountId { get; set; }
        public string UserId { get; set; }
        public string RequestAnnotation { get; set; }
    }

    public class FulfilmentRequest
    {
        public string TenantId { get; set; }
        public string AccountId { get; set; }
        public string UserId { get; set; }
        public string RequestAnnotation { get; set; }
        public Dictionary<string, string> FulfilmentDetails { get; set; }
    }

    public class ServiceRequestDescription
    {
        public string TenantId { get; set; }
        public string RequestId { get; set; }
        public string AccountId { get; set; }
        public string AnnotationToAdd { get; set; }
    }
}
