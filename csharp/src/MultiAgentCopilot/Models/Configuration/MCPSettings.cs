namespace MultiAgentCopilot.MultiAgentCopilot.Models.Configuration
{
    public class MCPSettings
    {
        public string JWTTokenSecret { get; set; } = string.Empty;
        public MCPConnectionType ConnectionType { get; set; } = MCPConnectionType.STDIO;

        public string CordinatorEndpointUrl { get; set; } = string.Empty;
        public string CustomerEndpointUrl { get; set; } = string.Empty;
        public string SalesEndpointUrl { get; set; } = string.Empty;
        public string TransactionsEndpointUrl { get; set; } = string.Empty;


        public string CordinatorToolTags { get; set; } = string.Empty;
        public string CustomerToolTags { get; set; } = string.Empty;
        public string SalesToolTags { get; set; } = string.Empty;
        public string TransactionsToolTags { get; set; } = string.Empty;

    }

    public enum MCPConnectionType
    {
        HTTP,
        STDIO
    }
}
