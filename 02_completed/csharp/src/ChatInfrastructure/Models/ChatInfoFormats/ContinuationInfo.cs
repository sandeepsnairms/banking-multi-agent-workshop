using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.ChatInfrastructure.Models.ChatInfoFormats
{
    public class ContinuationInfo
    {
        public string AgentName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
