using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Models.Banking
{
    public enum ServiceRequestType
    {
        Complaint,
        FundTransfer,
        Fulfilment,
        TeleBankerCallBack
    }
}
