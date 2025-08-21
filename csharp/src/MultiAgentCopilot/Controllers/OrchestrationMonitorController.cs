using Microsoft.AspNetCore.Mvc;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.Monitoring;

namespace MultiAgentCopilot.Controllers
{
    /// <summary>
    /// API Controller for OrchestrationMonitor data following Microsoft Agent Framework patterns
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class OrchestrationMonitorController : ControllerBase
    {
        private readonly AgentOrchestrationService _orchestrationService;
        private readonly ILogger<OrchestrationMonitorController> _logger;

        public OrchestrationMonitorController(
            AgentOrchestrationService orchestrationService,
            ILogger<OrchestrationMonitorController> logger)
        {
            _orchestrationService = orchestrationService;
            _logger = logger;
        }

        /// <summary>
        /// Get real-time analytics for a specific session
        /// </summary>
        /// <param name="sessionId">The session ID to get analytics for</param>
        /// <returns>Session analytics or 404 if session not found</returns>
        [HttpGet("session/{sessionId}/analytics")]
        [ProducesResponseType(typeof(SessionAnalytics), 200)]
        [ProducesResponseType(404)]
        public ActionResult<SessionAnalytics> GetSessionAnalytics(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return BadRequest("Session ID is required");
            }

            var analytics = _orchestrationService.GetSessionAnalytics(sessionId);
            if (analytics == null)
            {
                return NotFound($"Session {sessionId} not found or not being monitored");
            }

            return Ok(analytics);
        }

        /// <summary>
        /// Get overall orchestration statistics
        /// </summary>
        /// <returns>Comprehensive orchestration statistics</returns>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(OrchestrationStatistics), 200)]
        public ActionResult<OrchestrationStatistics> GetOrchestrationStatistics()
        {
            var statistics = _orchestrationService.GetOrchestrationStatistics();
            return Ok(statistics);
        }

        /// <summary>
        /// Get performance metrics for monitoring dashboard
        /// </summary>
        /// <returns>Performance metrics summary</returns>
        [HttpGet("performance")]
        [ProducesResponseType(typeof(PerformanceMetrics), 200)]
        public ActionResult<object> GetPerformanceMetrics()
        {
            var statistics = _orchestrationService.GetOrchestrationStatistics();
            
            var performanceData = new
            {
                ActiveSessions = statistics.ActiveSessionCount,
                TotalEvents = statistics.TotalEventsLogged,
                AverageSessionDuration = statistics.AverageSessionDuration,
                MostActiveAgent = statistics.MostActiveAgent,
                EventBreakdown = statistics.EventTypeCounts,
                LastUpdated = statistics.LastUpdated,
                HealthStatus = statistics.ActiveSessionCount > 0 ? "Active" : "Idle"
            };

            return Ok(performanceData);
        }

        /// <summary>
        /// Get monitoring status and health check
        /// </summary>
        /// <returns>Monitoring system health status</returns>
        [HttpGet("health")]
        [ProducesResponseType(200)]
        public ActionResult<object> GetMonitoringHealth()
        {
            var statistics = _orchestrationService.GetOrchestrationStatistics();
            var isHealthy = _orchestrationService.IsInitialized;

            var healthData = new
            {
                Status = isHealthy ? "Healthy" : "Unhealthy",
                Timestamp = DateTime.UtcNow,
                ActiveSessions = statistics.ActiveSessionCount,
                TotalEventsLogged = statistics.TotalEventsLogged,
                ServiceInitialized = _orchestrationService.IsInitialized,
                MonitoringEnabled = true
            };

            return Ok(healthData);
        }

        /// <summary>
        /// Get agent activity summary
        /// </summary>
        /// <returns>Summary of agent usage and activity</returns>
        [HttpGet("agents/activity")]
        [ProducesResponseType(200)]
        public ActionResult<object> GetAgentActivity()
        {
            var statistics = _orchestrationService.GetOrchestrationStatistics();
            
            var agentActivity = new
            {
                MostActiveAgent = statistics.MostActiveAgent,
                EventDistribution = statistics.EventTypeCounts,
                LastUpdated = statistics.LastUpdated,
                TotalEvents = statistics.TotalEventsLogged
            };

            return Ok(agentActivity);
        }
    }
}