# Multi Agent Workshop

Welcome to our multi-agent samples repository showcasing a retail banking scenario using 

- Agent Framework Agents in C#
- LangGraph in Python

This folder contains the completed files for the exercises. Follow the step-by-step instructions to deploy the resources and run the sample application.

Use the final code and artifacts to run the demo:

- [LangGraph (Python)](python/langgraph/README.md)  
- [Agent Framework (C#)](csharp/README.md)

If you prefer to begin with the minimal scaffolding code and follow the step-by-step instructions to complete each exercise [go to the excercises](../01_exercises/README.md)

## Important Security Notice

This template, the application code and configuration it contains, has been built to showcase Microsoft Azure specific services and tools. We strongly advise our customers not to make this code part of their production environments without implementing or enabling additional security features.

## Guidance

### Region Availability

This template uses gpt-4.1-mini and text-embedding-3-small models which may not be available in all Azure regions. Check for [up-to-date region availability](https://learn.microsoft.com/azure/ai-services/openai/concepts/models#standard-deployment-model-availability) and select a region during deployment accordingly.

### Costs

You can estimate the cost of this project's architecture with [Azure's pricing calculator](https://azure.microsoft.com/pricing/calculator/)

As an example in US dollars, here's how the sample is currently built:

Average Monthly Cost:

- Azure Cosmos DB Serverless ($0.25 USD per 1M RU/s): $0.25
- Azure OpenAI (gpt-4.1-mini 10 million input tokens + 5 million output tokens in a month): $12 
- Azure OpenAI (text-embedding-3-small): < $0.01 (Sample uses 5K tokens)


## Resources

To learn more about the services and features demonstrated in this sample, see the following:

- [Azure Cosmos DB for NoSQL Vector Search announcement](https://aka.ms/CosmosDBDiskANNBlog/)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Agent Framework](https://learn.microsoft.com/en-us/agent-framework/)

