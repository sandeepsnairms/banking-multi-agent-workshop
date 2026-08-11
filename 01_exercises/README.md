# Multi-Agent Workshop with Azure DocumentDB

Welcome to our multi-agent samples repository showcasing a retail banking scenario using 

- Agent Framework Agents in C#
- LangGraph in Python
- Azure DocumentDB with Microsoft Entra ID passwordless authentication

This folder contains the starter files for the exercises. Begin with the minimal scaffolding code and follow the step-by-step instructions to complete each exercise.

Work through the exercises to build the application step by step:

- [LangGraph (Python)](python/langgraph/workshop/Module-0.md)
- [Agent Framework (C#)](csharp/workshop/Module-0.md)


If you prefer to skip the exercises and go to the final code and [artifacts for running as demo](../02_completed/README.md).

## Important Security Notice

This template, the application code and configuration it contains, has been built to showcase Microsoft Azure specific services and tools. We strongly advise our customers not to make this code part of their production environments without implementing or enabling additional security features.

### DocumentDB Firewall

The workshop deployment permits DocumentDB connections from `0.0.0.0` through `255.255.255.255` so exercises can be tested from any network. Microsoft Entra authentication remains required, but the cluster network boundary is globally reachable.

After the workshop, set `documentDbFirewallStartIpAddress` and `documentDbFirewallEndIpAddress` in your deployment parameters to approved IPv4 addresses and redeploy. Set both values to the same public IPv4 address to allow one workstation. Production deployments should use private networking and remove the allow-all rule.

### Region Availability

This template uses gpt-4.1-mini and text-embedding-3-small models which may not be available in all Azure regions. Check for [up-to-date region availability](https://learn.microsoft.com/azure/ai-services/openai/concepts/models#standard-deployment-model-availability) and select a region during deployment accordingly.

### Costs

You can estimate the cost of this project's architecture with [Azure's pricing calculator](https://azure.microsoft.com/pricing/calculator/)

The sample provisions an Azure DocumentDB M30 cluster and Azure OpenAI model deployments. Use the pricing calculator for current regional pricing before deployment.


## Resources

To learn more about the services and features demonstrated in this sample, see the following:

- [Azure DocumentDB documentation](https://learn.microsoft.com/azure/documentdb/)
- [Azure DocumentDB vector search](https://learn.microsoft.com/azure/documentdb/vector-search)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Agent Framework](https://learn.microsoft.com/en-us/agent-framework/)

