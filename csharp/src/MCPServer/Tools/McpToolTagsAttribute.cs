namespace MCPServer.Tools
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class McpToolTagsAttribute : Attribute
    {
        public string[] Tags { get; }

        public McpToolTagsAttribute(params string[] tags)
        {
            Tags = tags;
        }
    }
}
