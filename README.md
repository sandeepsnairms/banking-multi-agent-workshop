# banking-multi-agent-workshop
A multi-agent sample and workshop for a retail banking scenario. Implemented in both C# using Semantic Kernel Agents and Python using LangGraph. 

## Using dev containers for development

This repository is [setup to use dev containers](./.devcontainer/devcontainer.json) for development. This means that you can use Visual Studio Code to open the repository in a container that has all the dependencies installed. This is useful if you don't want to install all the dependencies on your local machine.

There are two ways you can use dev containers:

1. With [GitHub Codespaces](https://docs.github.com/en/codespaces/overview) - development environment that's hosted in the cloud
2. Or locally with the [Visual Studio Code Dev Containers extension](https://code.visualstudio.com/docs/devcontainers/containers), if you cannot use GitHub Codespaces for some reason.

### Option 1: Using GitHub Codespaces in the browser

This is the easiest way to get started with the development environment. You can use GitHub Codespaces to create a development environment in the cloud and start developing right away.

1. Fork this repository
2. Navigate to the main page of the repository you forked, and select the **main** branch from the branch dropdown menu.
3. Click the  **Code** button, then click the **Codespaces** tab.

For details, follow the instructions in the [GitHub Codespaces documentation](https://docs.github.com/en/codespaces/developing-in-a-codespace/creating-a-codespace-for-a-repository#creating-a-codespace-for-a-repository) on how to create a new codespace.

### Using GitHub Codespaces in Visual Studio Code

You can develop with GitHub Codespaces directly in VS Code as well. Although the development environment is hosted in the cloud, you can use your local VS Code editor to connect to the Codespace.

1. Clone this GitHub repository to your local machine - `git clone https://github.com/AzureCosmosDB/banking-multi-agent-workshop`
2. Install and sign into the [GitHub Codespaces extension](https://marketplace.visualstudio.com/items?itemName=GitHub.codespaces) with your GitHub credentials.
3. In VS Code, in the **Activity Bar**, click the **Remote Explorer** icon and choose **GitHub Codespaces** from the dropdown.
4. Hover over the "Remote Explorer" side bar and click **+**.
5. In the text box, type the name of the repository you want to develop in, then select it.
6. Choose the **main** branch and the dev container configuration file.

For detailed walkthrough follow the GitHub Codespaces documentation on the [Prerequisites](https://github.com/AzureCosmosDB/banking-multi-agent-workshop) and [Creating a codespace in VS Code](https://docs.github.com/en/codespaces/developing-in-a-codespace/using-github-codespaces-in-visual-studio-code#creating-a-codespace-in-vs-code)    

### Using Visual Studio Code Dev Containers extension

If you cannot use GitHub Codespaces, you can use the Visual Studio Code Dev Containers extension to open the repository in a container, and develop locally. The main pre-requisite is to have Docker and Visual Studio Code installed on your local machine.

1. Install the [Visual Studio Code Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
2. Clone this GitHub repository to your local machine (`git clone https://github.com/AzureCosmosDB/banking-multi-agent-workshop`) and open it in Visual Studio Code.
3. Choose **Dev Containers: Reopen in Container** command from the Command Palette in Visual Studio Code to open the repository in a container.
4. Once the container opens, you can start developing your application.

Follow the instructions in the Visual Studio Code Dev Containers documentation on [how to install](https://code.visualstudio.com/docs/devcontainers/containers#_installation) and [open a repository in a dev container](https://code.visualstudio.com/docs/devcontainers/create-dev-container#_create-a-devcontainerjson-file) for more details on how to use the extension.