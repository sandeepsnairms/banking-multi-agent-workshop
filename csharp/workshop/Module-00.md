# Module 00 - Deployment and Setup

## Introduction

In this Module, you'll confirm the deployment of Azure Services needed to run this workshop.



### Running the ChatAPI and Frontend App

#### 1. Start the ChatAPI

##### If running on Codespaces

1. Navigate to `src/ChatAPI`.
2. Run the following command to trust the development certificate:

   ```sh
   dotnet dev-certs https --trust
   ```

3. Start the application:

   ```sh
   dotnet run
   ```

4. In the **Ports** tab, right-click and select the **Port Visibility** option to set port **63280** as **Public**.
5. Copy the URL for **63280** port.

   ![Ports Dialog for CodeSpaces](./media/module-00/ports-dialog.png)

##### If running locally on Visual Studio or VS Code

1. Navigate to `src\ChatAPI`.
2. Press **F5** or select **Run** to start the application.
3. Copy the URL from the browser window that opens.

#### 2. Run the Frontend App

1. Open a new terminal. Navigate to the `frontend` folder.
1. Copy and run the following:

   ```sh
   npm install
   npm start
   ```

##### If running locally

1. Open your browser and navigate to <http://localhost:4200/>.



#### 3. Start a Conversation

1. Open the frontend app.
1. Start a new conversation.
1. Send the message:

   ```text
   Hello, how are you?
   ```

1. You should see something like the output below.

   ![Test output](./media/module-00/test-output.png)

#### 4. Stop the Application

- In the frontend terminal, press **Ctrl + C** to stop the application.
- In your IDE press **Shift + F5** or stop the debugger.
- If you are in CodeSpaces, go to each terminal and press **Ctrl + C**.

### Deployment Validation

Use the steps below to validate that the solution was deployed successfully.

- [ ] All Azure resources are deployed successfully
- [ ] You can compile the solution in CodeSpaces or locally
- [ ] You can start the project and it runs without errors
- [ ] You are able to launch the Chat Frontend app , create a new chat session, and get a reply when you send a message.

### Common Issues and Troubleshooting

1. Errors during azd deployment:
   - Service principal "not found" error.
   - Rerun `azd up`
1. Azure OpenAI deployment issues:
   - Ensure your subscription has access to Azure OpenAI
   - Check regional availability
1. Frontend issues:
   - If frontend doesn't fully start, navigate to `/frontend/src/environments/environment.ts` and update `apiUrl: 'https://localhost:63279/'`
   - Frontend will restart
   - In CodeSpaces, if frontend displays the spinning icon when starting up, double-check you have made port `ChatAPI (63280)` public. Then restart the front end.
1. Connecting to backend running CodeSpaces

   - If you cannot get the front end to connect to the backend service when running in Codespaces try the following

     - Navigate to the /src/ChatAPI folder in the Terminal
     - Run the following command to trust the development certificate:

       ```sh
       dotnet dev-certs https --trust
       ```

     - Then start the application:

       ```sh
       dotnet run
       ```

   - Copy the URL from the **Ports** tab and use this for the environments.ts file

## Success Criteria

To complete this Module successfully, you should be able to:

- Verify that all services have been deployed successfully.
- Have an open IDE or CodeSpaces session with the source code and environment variables loaded.
- Be able to compile and run the application with no warnings or errors.

## Next Steps

Proceed to [Creating Your First Agent](./Module-01.md)

## Resources

- [azd Command Reference](https://learn.microsoft.com/azure/developer/azure-developer-cli/reference)
- [Semantic Kernel Agent Framework](https://learn.microsoft.com/semantic-kernel/frameworks/agent)
- [LangGraph](https://langchain-ai.github.io/langgraph/concepts/)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Azure Cosmos DB Vector Database](https://learn.microsoft.com/azure/cosmos-db/vector-database)
