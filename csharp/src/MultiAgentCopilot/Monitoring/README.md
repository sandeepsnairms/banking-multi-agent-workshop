# OrchestrationMonitor - Multi-Agent Banking System Monitoring

## Overview

The OrchestrationMonitor is a comprehensive monitoring solution for the multi-agent banking system, following the Microsoft Agent Framework patterns. It provides real-time insights into agent selection, response times, tool execution, and overall system performance.

## Features

### ?? **Real-Time Monitoring**
- **Agent Selection Tracking**: Monitor which agents are selected and why
- **Response Time Analysis**: Track agent response times and performance
- **Tool Execution Monitoring**: Monitor tool calls, success rates, and execution times
- **Session Analytics**: Comprehensive session-level metrics and insights

### ?? **Key Metrics Tracked**
- Agent selection frequency and reasoning
- Response times per agent and overall
- Tool execution success/failure rates
- Session duration and activity patterns
- Agent usage distribution

### ?? **Monitoring Capabilities**
- Session-level analytics with real-time updates
- Historical event tracking and analysis
- Performance metrics and bottleneck identification
- Agent workflow visualization

## API Endpoints

### Session Analytics
```http
GET /api/orchestrationmonitor/session/{sessionId}/analytics
```
Returns detailed analytics for a specific session including:
- Duration and activity status
- Agent usage metrics
- Recent events and performance data
- Agent flow visualization

### Overall Statistics
```http
GET /api/orchestrationmonitor/statistics
```
Returns comprehensive system-wide statistics:
- Active session count
- Total events logged
- Average session duration
- Most active agent
- Event type distribution

### Performance Metrics
```http
GET /api/orchestrationmonitor/performance
```
Returns performance summary for dashboards:
- Active sessions count
- Event breakdown
- Health status
- Last updated timestamp

### Health Check
```http
GET /api/orchestrationmonitor/health
```
Returns monitoring system health status:
- Service status (Healthy/Unhealthy)
- Active sessions count
- Service initialization status

### Agent Activity
```http
GET /api/orchestrationmonitor/agents/activity
```
Returns agent usage and activity summary:
- Most active agent
- Event distribution by type
- Activity patterns

## Usage Examples

### Basic Monitoring Integration

The OrchestrationMonitor is automatically integrated into the multi-agent system when enabled in dependency injection:

```csharp
// In DependencyInjection.cs
builder.Services.AddSingleton<OrchestrationMonitor>();
```

### Accessing Session Analytics

```csharp
// Get real-time session analytics
var analytics = orchestrationService.GetSessionAnalytics(sessionId);
if (analytics != null)
{
    Console.WriteLine($"Session Duration: {analytics.Duration}");
    Console.WriteLine($"Agent Flow: {string.Join(" -> ", analytics.AgentFlow)}");
    Console.WriteLine($"Response Rate: {analytics.PerformanceMetrics.ResponsesPerMinute:F2}/min");
}
```

### Monitoring API Usage

```bash
# Get session analytics
curl GET "https://your-api/api/orchestrationmonitor/session/session-123/analytics"

# Get overall statistics
curl GET "https://your-api/api/orchestrationmonitor/statistics"

# Health check
curl GET "https://your-api/api/orchestrationmonitor/health"
```

## Event Types Tracked

| Event Type | Description |
|------------|-------------|
| `SessionStarted` | New conversation session initiated |
| `SessionEnded` | Conversation session completed |
| `AgentSelected` | Agent selected by orchestration logic |
| `AgentResponse` | Agent provided response to user |
| `ToolExecuted` | Agent tool/plugin executed successfully |
| `ToolError` | Agent tool/plugin execution failed |
| `ContinuationDecision` | Decision to continue conversation |
| `TerminationDecision` | Decision to terminate conversation |
| `Error` | System error occurred |

## Performance Metrics

### Session-Level Metrics
- **Total Selections**: Number of agent selections
- **Total Responses**: Number of agent responses
- **Tool Calls Executed**: Number of successful tool executions
- **Average Response Time**: Mean time for agent responses
- **Average Selection Time**: Mean time for agent selection

### System-Level Metrics
- **Active Sessions**: Currently active conversation sessions
- **Events Per Minute**: Rate of orchestration events
- **Agent Usage Distribution**: Which agents are used most frequently
- **Tool Success Rate**: Percentage of successful tool executions

## Integration with Banking Agents

### Automatic Monitoring
When enabled, the OrchestrationMonitor automatically tracks:

1. **Sales Agent Activities**: Account registration, offer searches, product recommendations
2. **Transaction Agent Activities**: Money transfers, transaction history queries, payment processing
3. **Customer Support Agent Activities**: Service requests, complaints, telebanker scheduling
4. **Coordinator Agent Activities**: Account inquiries, user information retrieval, general coordination

### Tool Execution Monitoring
Each banking plugin method execution is tracked:
- Execution time measurement
- Success/failure status
- Error details when applicable
- Parameter validation tracking

## Configuration

### Environment Setup
The OrchestrationMonitor requires minimal configuration and is automatically registered in the DI container:

```csharp
// Automatic registration in Program.cs via DependencyInjection
builder.AddSemanticKernelService(); // Includes OrchestrationMonitor
```

### Memory Management
- Events are automatically purged to maintain optimal memory usage (keeps last 1000 events)
- Session data is cleaned up when sessions end
- Monitoring overhead is minimal and non-blocking

## Dashboard Integration

The monitoring APIs are designed for easy integration with monitoring dashboards:

### Grafana/PowerBI Integration
```json
{
  "activeSessionCount": 5,
  "totalEventsLogged": 1247,
  "averageSessionDuration": "00:03:45",
  "mostActiveAgent": "Transactions",
  "eventBreakdown": {
    "AgentSelected": 89,
    "AgentResponse": 89,
    "ToolExecuted": 156,
    "TerminationDecision": 45
  },
  "healthStatus": "Active"
}
```

### Real-Time Updates
Use the session analytics endpoint for real-time session monitoring and the statistics endpoint for system-wide dashboard updates.

## Troubleshooting

### Common Issues

1. **No monitoring data**: Ensure OrchestrationMonitor is registered in DI
2. **Missing session analytics**: Verify sessionId is being passed correctly
3. **Performance impact**: Monitor CPU usage; the system is designed to be lightweight

### Debug Logging
Enable debug logging to see detailed monitoring operations:
```json
{
  "Logging": {
    "LogLevel": {
      "MultiAgentCopilot.Monitoring": "Debug"
    }
  }
}
```

## Architecture

The OrchestrationMonitor follows the Microsoft Agent Framework patterns and integrates seamlessly with:
- **BankingAgentOrchestration**: Main orchestration engine
- **AgentOrchestrationService**: Service layer coordination  
- **Individual Banking Agents**: Sales, Transactions, CustomerSupport, Coordinator
- **Plugin System**: Tool execution monitoring

This monitoring solution provides comprehensive insights into multi-agent conversations while maintaining high performance and minimal overhead.