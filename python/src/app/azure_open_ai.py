# Initialize the AzureChatOpenAI model
import os

from langchain_openai import AzureChatOpenAI

try:
    azure_openai_api_version = "2023-05-15"
    azure_deployment_name = os.getenv("AZURE_OPENAI_CHATDEPLOYMENTID")
    model = AzureChatOpenAI(
        azure_deployment=azure_deployment_name,
        api_version=azure_openai_api_version,
        temperature=0,
    )
    print("[DEBUG] Azure OpenAI model initialized successfully.")
except Exception as e:
    print(f"[ERROR] Error initializing Azure OpenAI model: {e}")
    raise e