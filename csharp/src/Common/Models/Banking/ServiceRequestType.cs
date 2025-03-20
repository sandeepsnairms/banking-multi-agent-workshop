using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Banking
{
    public enum ServiceRequestType
    {
        Complaint = 0,
        FundTransfer = 1,
        Fulfilment = 2,
        TeleBankerCallBack = 3
    }
}
