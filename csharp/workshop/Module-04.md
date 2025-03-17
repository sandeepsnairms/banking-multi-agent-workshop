# Module 04 - Multi-Agent Orchestration

[< Agent Specialization](./Module-03.md) - **[Home](Home.md)** - [Lessons Learned, Agent Futures, Q&A >](./Module-05.md)

## Introduction

In this Module you'll learn how to implement the multi-agent orchestration to tie all of the agents you have created so far together into a single system. You'll also learn how to test the system as a whole is working correctly and how to debug and monitor the agents performance and behavior and troubleshoot them.

## Learning Objectives

- Learn how to write prompts for agents
- Define agent routing
- Learn how to define API contracts for a multi-agent system
- Learn how to test and debug agents, monitoring

## Module Exercises

1. [Activity 1: Session on Multi-Agent Architectures](#activity-1-session-on-multi-agent-architectures)
1. [Activity 2: Define Agents and Roles](#activity-2-define-agents-and-roles)
1. [Activity 3: Session on Testing and Monitoring](#activity-3-session-on-testing-and-monitoring)
1. [Activity 4: Implement Agent Tracing and Monitoring](#activity-4-implement-agent-tracing-and-monitoring)
1. [Activity 5: Test your Work](#activity-5-test-your-work)

## Activity 1: Session on Multi-Agent Architectures

In this session you will learn how this all comes together and get insights into how the multi-agent orchestration works and coordindates across all of the defined agents for your system.

## Activity 2: Define Agents and Roles

In this hands-on exercise, you will learn how to write prompts for agents and define agent routing.

### Define Agents
Add ChatInfrastructure\Models\AgentTypes.cs

### Write Agent Prompts
- Update ChatInfrastructure\Factories\SystemPromptFactory.cs
- Create folder ChatAPI\Prompts
- Add ChatAPI\Prompts\Coordinator.prompty
- Add ChatAPI\Prompts\CustomerSupport.prompty
- Add ChatAPI\Prompts\Sales.prompty
- Add ChatAPI\Prompts\Transactions.prompty
- Update ChatInfrastructure\Factories\ChatFactory.cs

### Create Agent Specific Plugins
- Update ChatInfrastructure\Factories\PluginFactory.cs
- Add CoordinatorPlugin.cs
- Add CustomerSupportPlugin.cs
- Add SalesPlugin.cs
- Add TransactionPlugin.cs
- Update ChatInfrastructure\Factories\ChatFactory.cs

### Create Structure formats
- Add folder ChatInfrastructure\StructuredFormats
- Add file ChatInfrastructure\StructuredFormats\ChatResponseFormat.cs
- Update ChatInfrastructure\Factories\ChatFactory.cs

### Create Termination and Selection Strategy
- Add ChatAPI\Prompts\TerminationStratergy.prompty
- Add ChatAPI\Prompts\SelectionStratergy.prompty

- Add ChatInfrastructure\Models\ChatInfoFormats\ContinuationInfo.cs
- Add ChatInfrastructure\Models\ChatInfoFormats\TerminationInfo.cs
- Update ChatInfrastructure\Factories\ChatFactory.cs

### Create Agent Group Chat
- Update ChatInfrastructure\Services\SemanticKernelService.cs

### Use Agent Group Chat Response
- Update ChatInfrastructure\Services\SemanticKernelService.cs

## Activity 3: Session on Testing and Monitoring

In this session you will learn about how to architect the service layer for a multi-agent system and how to configure and coduct testing and debugging and monitoring for these systems.

## Activity 4: Implement Agent Tracing and Monitoring

In this hands-on exercise, you will learn how to define an API service layer for a multi-agent backend and learn how to configure tracing and monitoring to enable testing and debugging for agents.

**TBD - this needs langauge specific instructions**

## Activity 5: Test your Work

With the hands-on exercises complete it is time to test your work.

**TBD - this needs langauge specific instructions**

### Validation Checklist

- [ ] item 1
- [ ] item 2
- [ ] item 3

### Common Issues and Solutions

1. Item 1:

   - Sub item 1
   - Sub item 2
   - Sub item 3

1. Item 2:

   - Sub item 1
   - Sub item 2
   - Sub item 3

3. Item 3:

   - Sub item 1
   - Sub item 2
   - Sub item 3

### Module Solution

<details>
  <summary>If you are encounting errors or issues with your code for this module, please refer to the following code.</summary>

<br>

Explanation for code and where it goes. Multiple sections of these if necessary.
```python
# Your code goes here

```
</details>

## Next Steps

Proceed to [Lessons Learned, Agent Futures, Q&A](./Module-04.md)

## Resources

- [Semantic Kernel Agent Framework](https://learn.microsoft.com/semantic-kernel/frameworks/agent)
- [LangGraph](https://langchain-ai.github.io/langgraph/concepts/)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Azure Cosmos DB Vector Database](https://learn.microsoft.com/azure/cosmos-db/vector-database)

