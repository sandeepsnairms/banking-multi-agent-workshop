namespace MultiAgentCopilot.Monitoring
{
    /// <summary>
    /// Represents a conversation session being monitored
    /// </summary>
    public class ConversationSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime LastActivity { get; set; }
        public List<string> AvailableAgents { get; set; } = new();
        public List<OrchestrationEvent> Events { get; set; } = new();
        public SessionMetrics Metrics { get; set; } = new();
    }

    /// <summary>
    /// Metrics collected during a conversation session
    /// </summary>
    public class SessionMetrics
    {
        public int TotalSelections { get; set; }
        public int TotalResponses { get; set; }
        public int TotalTerminationChecks { get; set; }
        public int ToolCallsExecuted { get; set; }
        public int SuccessfulToolCalls { get; set; }
        public int FailedToolCalls { get; set; }
        public int TotalResponseLength { get; set; }
        public TimeSpan AverageSelectionTime { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public Dictionary<string, int> AgentUsageCount { get; set; } = new();
    }

    /// <summary>
    /// Individual orchestration event
    /// </summary>
    public class OrchestrationEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public OrchestrationEventType EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public string? AgentName { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan? Duration { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }

    /// <summary>
    /// Types of orchestration events
    /// </summary>
    public enum OrchestrationEventType
    {
        SessionStarted,
        SessionEnded,
        AgentSelected,
        AgentResponse,
        ToolExecuted,
        ToolError,
        ContinuationDecision,
        TerminationDecision,
        Error
    }

    /// <summary>
    /// Real-time analytics for a session
    /// </summary>
    public class SessionAnalytics
    {
        public string SessionId { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public bool IsActive { get; set; }
        public SessionMetrics Metrics { get; set; } = new();
        public List<string> AgentFlow { get; set; } = new();
        public List<OrchestrationEvent> RecentEvents { get; set; } = new();
        public PerformanceMetrics PerformanceMetrics { get; set; } = new();
    }

    /// <summary>
    /// Performance metrics for analysis
    /// </summary>
    public class PerformanceMetrics
    {
        public double ResponsesPerMinute { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public TimeSpan AverageSelectionTime { get; set; }
        public double ToolCallSuccessRate { get; set; }
    }

    /// <summary>
    /// Overall orchestration statistics
    /// </summary>
    public class OrchestrationStatistics
    {
        public int ActiveSessionCount { get; set; }
        public int TotalEventsLogged { get; set; }
        public TimeSpan AverageSessionDuration { get; set; }
        public string MostActiveAgent { get; set; } = string.Empty;
        public Dictionary<OrchestrationEventType, int> EventTypeCounts { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }
}