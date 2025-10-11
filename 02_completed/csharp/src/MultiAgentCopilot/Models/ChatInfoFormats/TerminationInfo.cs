
namespace MultiAgentCopilot.Models.ChatInfoFormats
{
    // Make public to resolve CS0050
    public class TerminationInfo
    {
        public bool ShouldContinue { get; set; }
        public string Reason { get; set; }
    }
}
