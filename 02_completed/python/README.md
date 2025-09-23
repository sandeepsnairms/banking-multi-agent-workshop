# Multi Agent Workshop using LangGraph in Python

This module contains the completed files for the exercises. To run the multi-agent application using Azure Cosmos DB and LangGraph in Python, follow these steps:

1. Deploy the required Azure resources.
2. Run the application locally and view the demo.

If you prefer to begin with the minimal scaffolding code and follow the step-by-step instructions to complete each exercise go to [exercises](../../01_exercises/README.md).

## Deployment and Setup

### Git Clone

Let's clone the repository to download the files to your machine.

1. Create a working directory on your machine, for example: `C:\repos\HOL_SKandLangGraph`.
2. Open PowerShell from the Start menu.
3. Navigate to the `C:\repos\HOL_SKandLangGraph` folder.
4. Clone the GitHub repository by running the following command:

```shell
git clone --branch HOL_SKandLangGraph https://github.com/AzureCosmosDB/banking-multi-agent-workshop.git C:\repos\HOL_SKandLangGraph
```

### Resource Provisioning

Let's deploy the Azure Services needed to run the application.

1. Open the PowerShell terminal on the Start Bar and navigate to the multi-agent-hol folder.

```shell
cd C:\repos\HOL_SKandLangGraph\
```

1. Authenticate  yourself

```shell
azd auth login
```

1. Deploy the Azure services using `azd up`

```shell
azd up
```

1. For environment name enter: `agenthol`
1. Press enter to select the subscription listed.
1. Press enter to select the default region listed.

1. Return to the Azure Portal and refresh the list of resource groups. You may need to refresh a few times.
1. Select the *rg-agenthol* resource group.
1. Find the collapsed *Essentials* section at the top of the page and expand.
1. Click on the Deployments and watch until the status of all deployed resources shows as Succeeded.
1. Your screen should appear as below.

![deployments](./media/deployments.png)


## Run the solution

### Configure Environment Variables

When you deploy this solution it automatically injects endpoints and configuration values for the required resources into a `.env` file at root (python) folder.

But you will still need to install dependencies to run the solution locally.

1. Open VS Code from the desktop.
1. From the menu, select File, Open Folder, then select the *"C:\repos\HOL_SKandLangGraph\"* folder.
1. From the menu, select Terminal, New Terminal, then open a new PowerShell terminal and navigate to the python HOL folder.

```shell
cd C:\repos\HOL_SKandLangGraph\python
```

1. Create a virtual environment *(If prompted, create the environement for the workspace folder.)*

```shell
python -m venv .venv
```

1. Activate the virtual environment

```shell
.venv\Scripts\Activate.ps1
```

1. Install the required dependencies for the project.

```shell
pip install -r .\src\app\requirements.txt
```

### Start the Backend App

1. Remain in the terminal in the python folder.
2. Start the fastapi server.

```shell
uvicorn src.app.banking_agents_api:app --reload --host 0.0.0.0 --port 63280
```

**Note:** If prompted, allow Python to allow public and private network access to this app.

The API will be available at <http://localhost:63280/docs>. This has been pre-built with boilerplate code that will create chat sessions and store the chat history in Cosmos DB.

### Start the Frontend App

1. In VS Code, open a new PowerShell terminal.
1. Navigate to the *multi-agent-hol\frontend* folder

```shell
cd C:\repos\HOL_SKandLangGraph\frontend
```

1. Run the following to install npm and start the application:

```shell
npm install
npm start
```

### Start a Conversation

1. Open your browser and navigate to <http://localhost:4200/>.
1. In the Login dialog, select a user and company and click, Login.
1. Send the message: