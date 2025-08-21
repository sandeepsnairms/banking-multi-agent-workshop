using System.Collections.Concurrent;
using System.Text.Json;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Models.ChatInfoFormats;
using MultiAgentCopilot.Models.Debug;

namespace MultiAgentCopilot.Monitoring
{
    /// <summary>
    /// OrchestrationMonitor provides comprehensive monitoring and analytics for multi-agent conversations
    /// following the Microsoft Agent Framework pattern for orchestration monitoring.
    /// </summary>
    public class OrchestrationMonitor
    {
        private readonly ILogger<OrchestrationMonitor> _logger;
        private readonly ConcurrentDictionary<string, ConversationSession> _activeSessions;
        private readonly ConcurrentQueue<OrchestrationEvent> _eventHistory;
        private readonly object _lockObject = new();

        public OrchestrationMonitor(ILogger<OrchestrationMonitor> logger)
        {
            _logger = logger;
            _activeSessions = new ConcurrentDictionary<string, ConversationSession>();
            _eventHistory = new ConcurrentQueue<OrchestrationEvent>();
        }

        /// <summary>
        /// Start monitoring a new conversation session
        /// </summary>
        public void StartSession(string sessionId, string tenantId, string userId, List<string> availableAgents)
        {
            var session = new ConversationSession
            {
                SessionId = sessionId,
                TenantId = tenantId,
                UserId = userId,
                StartTime = DateTime.UtcNow,
                AvailableAgents = availableAgents,
                Events = new List<OrchestrationEvent>(),
                Metrics = new SessionMetrics()
            };

            _activeSessions.TryAdd(sessionId, session);
            
            var startEvent = new OrchestrationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                EventType = OrchestrationEventType.SessionStarted,
                Timestamp = DateTime.UtcNow,
                Message = $"Session started with {availableAgents.Count} available agents: {string.Join(", ", availableAgents)}"
            };

            LogEvent(startEvent);
            _logger.LogInformation("OrchestrationMonitor: Started monitoring session {SessionId} for user {UserId}", 
                sessionId, userId);
        }

        /// <summary>
        /// Log agent selection event
        /// </summary>
        public void LogAgentSelection(string sessionId, ContinuationInfo selectionInfo, TimeSpan selectionTime)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return;

            var selectionEvent = new OrchestrationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                EventType = OrchestrationEventType.AgentSelected,
                Timestamp = DateTime.UtcNow,
                AgentName = selectionInfo.AgentName,
                Message = selectionInfo.Reason,
                Duration = selectionTime,
                Details = new Dictionary<string, object>
                {
                    ["SelectionReason"] = selectionInfo.Reason,
                    ["SelectedAgent"] = selectionInfo.AgentName,
                    ["SelectionTimeMs"] = selectionTime.TotalMilliseconds
                }
            };

            LogEvent(selectionEvent);
            
            // Update session metrics
            lock (_lockObject)
            {
                session.Metrics.TotalSelections++;
                session.Metrics.AverageSelectionTime = CalculateAverageSelectionTime(session);
                session.LastActivity = DateTime.UtcNow;
                
                if (!session.Metrics.AgentUsageCount.ContainsKey(selectionInfo.AgentName))
                    session.Metrics.AgentUsageCount[selectionInfo.AgentName] = 0;
                session.Metrics.AgentUsageCount[selectionInfo.AgentName]++;
            }

            _logger.LogDebug("OrchestrationMonitor: Agent {AgentName} selected for session {SessionId} in {SelectionTime}ms", 
                selectionInfo.AgentName, sessionId, selectionTime.TotalMilliseconds);
        }

        /// <summary>
        /// Log agent response event
        /// </summary>
        public void LogAgentResponse(string sessionId, string agentName, string response, TimeSpan responseTime, bool hasToolCalls = false)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return;

            var responseEvent = new OrchestrationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                EventType = OrchestrationEventType.AgentResponse,
                Timestamp = DateTime.UtcNow,
                AgentName = agentName,
                Message = $"Agent response ({response.Length} chars)",
                Duration = responseTime,
                Details = new Dictionary<string, object>
                {
                    ["ResponseLength"] = response.Length,
                    ["ResponseTimeMs"] = responseTime.TotalMilliseconds,
                    ["HasToolCalls"] = hasToolCalls,
                    ["ResponsePreview"] = response.Length > 100 ? response.Substring(0, 100) + "..." : response
                }
            };

            LogEvent(responseEvent);
            
            // Update session metrics
            lock (_lockObject)
            {
                session.Metrics.TotalResponses++;
                session.Metrics.AverageResponseTime = CalculateAverageResponseTime(session);
                session.Metrics.TotalResponseLength += response.Length;
                session.LastActivity = DateTime.UtcNow;
                
                if (hasToolCalls)
                    session.Metrics.ToolCallsExecuted++;
            }

            _logger.LogDebug("OrchestrationMonitor: Agent {AgentName} responded for session {SessionId} in {ResponseTime}ms", 
                agentName, sessionId, responseTime.TotalMilliseconds);
        }

        /// <summary>
        /// Log termination decision event
        /// </summary>
        public void LogTerminationDecision(string sessionId, TerminationInfo terminationInfo, TimeSpan decisionTime)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return;

            var terminationEvent = new OrchestrationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                EventType = terminationInfo.ShouldContinue ? OrchestrationEventType.ContinuationDecision : OrchestrationEventType.TerminationDecision,
                Timestamp = DateTime.UtcNow,
                Message = terminationInfo.Reason,
                Duration = decisionTime,
                Details = new Dictionary<string, object>
                {
                    ["ShouldContinue"] = terminationInfo.ShouldContinue,
                    ["TerminationReason"] = terminationInfo.Reason,
                    ["DecisionTimeMs"] = decisionTime.TotalMilliseconds
                }
            };

            LogEvent(terminationEvent);
            
            // Update session metrics
            lock (_lockObject)
            {
                session.Metrics.TotalTerminationChecks++;
                session.LastActivity = DateTime.UtcNow;
            }

            _logger.LogDebug("OrchestrationMonitor: Termination decision for session {SessionId}: Continue={ShouldContinue}", 
                sessionId, terminationInfo.ShouldContinue);
        }

        /// <summary>
        /// Log tool execution event
        /// </summary>
        public void LogToolExecution(string sessionId, string agentName, string toolName, TimeSpan executionTime, bool successful, string? errorMessage = null)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return;

            var toolEvent = new OrchestrationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                EventType = successful ? OrchestrationEventType.ToolExecuted : OrchestrationEventType.ToolError,
                Timestamp = DateTime.UtcNow,
                AgentName = agentName,
                Message = successful ? $"Tool {toolName} executed successfully" : $"Tool {toolName} failed: {errorMessage}",
                Duration = executionTime,
                Details = new Dictionary<string, object>
                {
                    ["ToolName"] = toolName,
                    ["ExecutionTimeMs"] = executionTime.TotalMilliseconds,
                    ["Successful"] = successful,
                    ["ErrorMessage"] = errorMessage ?? string.Empty
                }
            };

            LogEvent(toolEvent);
            
            // Update session metrics
            lock (_lockObject)
            {
                if (successful)
                    session.Metrics.SuccessfulToolCalls++;
                else
                    session.Metrics.FailedToolCalls++;
                session.LastActivity = DateTime.UtcNow;
            }

            _logger.LogDebug("OrchestrationMonitor: Tool {ToolName} execution for agent {AgentName} in session {SessionId}: {Status}", 
                toolName, agentName, sessionId, successful ? "Success" : "Failed");
        }

        /// <summary>
        /// End monitoring session
        /// </summary>
        public ConversationSession? EndSession(string sessionId)
        {
            if (!_activeSessions.TryRemove(sessionId, out var session))
                return null;

            session.EndTime = DateTime.UtcNow;
            session.Duration = session.EndTime.Value - session.StartTime;

            var endEvent = new OrchestrationEvent
            {
                EventId = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                EventType = OrchestrationEventType.SessionEnded,
                Timestamp = DateTime.UtcNow,
                Message = $"Session ended after {session.Duration.TotalMinutes:F2} minutes with {session.Metrics.TotalResponses} responses"
            };

            LogEvent(endEvent);
            
            _logger.LogInformation("OrchestrationMonitor: Ended session {SessionId} - Duration: {Duration}, Responses: {ResponseCount}", 
                sessionId, session.Duration, session.Metrics.TotalResponses);

            return session;
        }

        /// <summary>
        /// Get real-time session analytics
        /// </summary>
        public SessionAnalytics? GetSessionAnalytics(string sessionId)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return null;

            var currentTime = DateTime.UtcNow;
            var sessionDuration = currentTime - session.StartTime;

            return new SessionAnalytics
            {
                SessionId = sessionId,
                Duration = sessionDuration,
                IsActive = true,
                Metrics = session.Metrics,
                AgentFlow = GetAgentFlow(sessionId),
                RecentEvents = GetRecentEvents(sessionId, 10),
                PerformanceMetrics = new PerformanceMetrics
                {
                    ResponsesPerMinute = sessionDuration.TotalMinutes > 0 ? session.Metrics.TotalResponses / sessionDuration.TotalMinutes : 0,
                    AverageResponseTime = session.Metrics.AverageResponseTime,
                    AverageSelectionTime = session.Metrics.AverageSelectionTime,
                    ToolCallSuccessRate = session.Metrics.ToolCallsExecuted > 0 ? 
                        (double)session.Metrics.SuccessfulToolCalls / session.Metrics.ToolCallsExecuted : 0
                }
            };
        }

        /// <summary>
        /// Get overall orchestration statistics
        /// </summary>
        public OrchestrationStatistics GetOverallStatistics()
        {
            var allEvents = _eventHistory.ToArray();
            var activeSessions = _activeSessions.Values.ToArray();

            return new OrchestrationStatistics
            {
                ActiveSessionCount = activeSessions.Length,
                TotalEventsLogged = allEvents.Length,
                AverageSessionDuration = activeSessions.Any() ? 
                    TimeSpan.FromTicks((long)activeSessions.Average(s => (DateTime.UtcNow - s.StartTime).Ticks)) : TimeSpan.Zero,
                MostActiveAgent = GetMostActiveAgent(activeSessions),
                EventTypeCounts = allEvents.GroupBy(e => e.EventType).ToDictionary(g => g.Key, g => g.Count()),
                LastUpdated = DateTime.UtcNow
            };
        }

        #region Private Methods

        private void LogEvent(OrchestrationEvent orchestrationEvent)
        {
            if (_activeSessions.TryGetValue(orchestrationEvent.SessionId, out var session))
            {
                session.Events.Add(orchestrationEvent);
            }
            
            _eventHistory.Enqueue(orchestrationEvent);
            
            // Keep only last 1000 events to prevent memory issues
            while (_eventHistory.Count > 1000)
            {
                _eventHistory.TryDequeue(out _);
            }
        }

        private TimeSpan CalculateAverageSelectionTime(ConversationSession session)
        {
            var selectionEvents = session.Events.Where(e => e.EventType == OrchestrationEventType.AgentSelected).ToList();
            if (!selectionEvents.Any()) return TimeSpan.Zero;
            
            return TimeSpan.FromTicks((long)selectionEvents.Average(e => e.Duration?.Ticks ?? 0));
        }

        private TimeSpan CalculateAverageResponseTime(ConversationSession session)
        {
            var responseEvents = session.Events.Where(e => e.EventType == OrchestrationEventType.AgentResponse).ToList();
            if (!responseEvents.Any()) return TimeSpan.Zero;
            
            return TimeSpan.FromTicks((long)responseEvents.Average(e => e.Duration?.Ticks ?? 0));
        }

        private string GetMostActiveAgent(ConversationSession[] sessions)
        {
            var agentCounts = new Dictionary<string, int>();
            
            foreach (var session in sessions)
            {
                foreach (var kvp in session.Metrics.AgentUsageCount)
                {
                    agentCounts.TryGetValue(kvp.Key, out var count);
                    agentCounts[kvp.Key] = count + kvp.Value;
                }
            }
            
            return agentCounts.Any() ? agentCounts.OrderByDescending(kvp => kvp.Value).First().Key : "None";
        }

        private List<string> GetAgentFlow(string sessionId)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return new List<string>();

            return session.Events
                .Where(e => e.EventType == OrchestrationEventType.AgentSelected)
                .OrderBy(e => e.Timestamp)
                .Select(e => e.AgentName ?? "Unknown")
                .ToList();
        }

        private List<OrchestrationEvent> GetRecentEvents(string sessionId, int count)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return new List<OrchestrationEvent>();

            return session.Events
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
        }

        #endregion
    }
}