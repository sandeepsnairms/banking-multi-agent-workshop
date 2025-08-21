# Objective 
- Replace Semantic Kernel with Agent Framework

# Project Dependency and Packages
- New Dependencies have been already added
- Remove the unnecessary packages  after migrations

# Sucess Criteria
- No functionality is lost
- Code is simple and clean
- No reference to Semantic Kernel post migration
- Optimized for performance

# Mapping
- Replace AgentGroupChat to GroupChatOrchestration , Use OrchestrationMonitor (refer to agent-framework/dotnet/samples/GettingStarted/Orchestration/GroupChatOrchestration_With_AIManager.cs at main · microsoft/agent-framework)
- Use SelectionStrategy and TerminationStrategy to  support AIGroupChatManager  (refer to agent-framework/dotnet/samples/GettingStarted/Orchestration/GroupChatOrchestration_With_AIManager.cs at main · microsoft/agent-framework)
- Convert the files in C:\Work\GitHub\repos\banking-multi-agent-workshop\csharp\src\MultiAgentCopilot\AgentPlugins\ to   Agent Tools ( refer to MenuTools class in  agent-framework/dotnet/samples/GettingStarted/Steps/Step02_ChatClientAgent_UsingFunctionTools.cs at main · microsoft/agent-framework)
- Use Structured Output as described in agent-framework/dotnet/samples/GettingStarted/Steps/Step06_ChatClientAgent_StructuredOutputs.cs at main · microsoft/agent-framework
- Use agent-framework/dotnet/samples/GettingStarted/Steps/Step09_ChatClientAgent_3rdPartyThreadStorage.cs at main · microsoft/agent-framework to store and restore a chat from history.