namespace  MultiAgentCopilot.Models.Chat;

public record Message
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; set; }

    public string Type { get; set; }
    /// <summary>
    /// Partition key
    /// </summary>
    public string SessionId { get; set; }

    public string TenantId { get; set; }

    public string UserId { get; set; }

    public DateTime TimeStamp { get; set; }

    public string Sender { get; set; }

    public string SenderRole { get; set; }

    public string Text { get; set; }

    public string? DebugLogId { get; set; }

    public bool? Rating { get; set; }


    public Message(string tenantId, string userId,string sessionId, string author, string authorRole, string textContent, string? id = null, string? debugLogId=null)
    {
        SessionId = sessionId;
        TenantId = tenantId;
        UserId = userId;
        Id = id ?? Guid.NewGuid().ToString();
        if (debugLogId != null)
            DebugLogId = debugLogId;
        Type = nameof(Message);
        Sender = author;
        SenderRole = authorRole;
        Text = textContent;
        TimeStamp = DateTime.UtcNow; 
    }
}
