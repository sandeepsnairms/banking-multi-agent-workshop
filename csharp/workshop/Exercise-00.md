# Exercise 00 - Prerequisites - Deployment and Setup

**[Home](../README.md)** - [Next Exercise >](./Exercise-01.md)

## Introduction

Thank you for participating in the Banking Multi-Agent Workshop. Over this series of exercises you'll learn about how to build a multi-agent AI application.

---set up business scenario

--set up the technical scenario. highlight the steps of what users will do to 

## Learning Path

The workshop follows a progressive learning path with the following Exercises

Original TOC
- Creating an agent
- Creating Multiple agents
- RAG and Semantic Cache
- Build Agent Functions
- Putting it all together


-[Exercise 0: Deploy a Multi-Agent System to Azure](Exercise-00.md)
-[Exercise 1: Creating Your First Agent](Exercise-01.md)
-[Exercise 2: Implementing a Multi-Agent System](Exercise-02.md)
-[Exercise 3: Implementing Core Banking Operations](Exercise-03.md)
-[Exercise 4: Implementing Vector Search](Exercise-04.md)
-[Exercise 5: Multi-tenant API Implementation](Exercise-05.md)

1. Start with core concepts
2. Build foundational components
3. Add advanced features
4. Integrate components
5. Deploy production-ready system


## Prerequisites

- Azure Subscription
- Windows, MacOS, or Linux development machine on which you have **administrator rights**.
- A GitHub account with access to [GitHub Codespaces](https://github.com/features/codespaces)


## Get Started

To complete this workshop, you can set up the pre-requisite developer tools on your local workstation, or you can use GitHub Codespaces.

A GitHub Codespace is a development environment that is hosted in the cloud that you access via a browser. All of the pre-requisite developer tools are pre-installed and available in the codespace.

### Launch from GitHub Codespace

You must have a GitHub account to use GitHub Codespaces. If you do not have a GitHub account, you can [Sign Up Here](https://github.com/signup)!

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/AzureCosmosDB/banking-multi-agent-workshop)


### Setup Locally
<details>
  <summary>If you prefer to run this locally on your machine, open this section and install these additional tools.</summary>

<br>

  Install these prerequisites:

  - .NET 9 SDK or Python 3.12
  - Docker Desktop
  - Azure CLI ([v2.51.0 or greater](https://docs.microsoft.com/cli/azure/install-azure-cli))
  - [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
  - Git installed
  - Visual Studio Code
  - Python 3.9 or higher

  Then download the starter solution and workshop guide for completing the steps in the workshop:

    ```bash
    azd init -t AzureCosmosDB/banking-multi-agent-workshop -b start
    ```
</details>

### Deploy Azure Resources

1. From the terminal, navigate to the /infra directory in this solution.

1. Log in to AZD.

   ```bash
   azd auth login
   ```

1. Provision the Azure services, build your local solution container, and deploy the application.

   ```bash
   azd up
   ```

1. Enter the information when prompted

> [!IMPORTANT]
> If you encounter any errors during the deployment, rerun `azd up` to continue the deployment from where it left off. This will not create duplicate resources, and tends to resolve most issues.


### Deployment Validation

Use the steps below to validate that the solution was deployed successfully.

- [ ] All Azure resources are deployed successfully
- [ ] Validation script runs without errors
- [ ] You can compile the solution in CodeSpaces or locally
- [ ] You can start the project and it runs without errors
- [ ] You can access Azure OpenAI service
- [ ] You can connect to Azure Cosmos DB


> [!NOTE]
> It takes several minutes until all imported data is vectorized and indexed.

### Common Issues and Troubleshooting

1. Azure OpenAI deployment issues:

  - Ensure your subscription has access to Azure OpenAI
  - Check regional availability

2. Cosmos DB connectivity:

  - Verify connection string in local.settings.json
  - Check firewall settings

3. Python environment issues:
  - Ensure correct Python version
  - Verify all dependencies are installed


## Success Criteria

To complete this exercise successfully, you should be able to:

- Verify that all services have been deployed successfully.
- Have an open IDE or CodeSpaces session with the source code and environment variables loaded.

## Next Steps

Once you've completed this setup, you're ready to move on to Lab 1, where you'll create your first AI agent.

## Resources

- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Azure Cosmos DB Documentation](https://learn.microsoft.com/azure/cosmos-db/)
- [azd Command Reference](https://learn.microsoft.com/azure/developer/azure-developer-cli/reference)

Proceed to [Exercise 1](./Exercise-01.md)