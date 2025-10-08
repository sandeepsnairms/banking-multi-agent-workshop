namespace MultiAgentCopilot.Models.MCP
{
       
    /// <summary>
    /// Response from calling an MCP tool
    /// </summary>
    public class McpToolResponse
    {
        public bool IsSuccess { get; set; }
        public string Content { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string RawResponse { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from MCP server
    /// </summary>
    public class McpResponse
    {
        public bool IsSuccess { get; set; }
        public System.Net.HttpStatusCode StatusCode { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}