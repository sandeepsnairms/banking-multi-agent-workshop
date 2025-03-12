import logging
import os
from azure.identity import DefaultAzureCredential, ManagedIdentityCredential
from langchain_openai import AzureChatOpenAI

key = os.getenv("AZURE_OPENAI_API_KEY")


# Use DefaultAzureCredential to get a token
def get_azure_ad_token():
    try:
        credential = DefaultAzureCredential()
        token = credential.get_token("https://cognitiveservices.azure.com/.default")

        print("[DEBUG] Retrieved Azure AD token successfully using DefaultAzureCredential.")
    except Exception as e:
        print(f"[ERROR] Failed to retrieve Azure AD token: {e}")
        raise e
    return token.token


# Fetch AD Token
azure_ad_token = get_azure_ad_token()

try:
    azure_openai_api_version = "2023-05-15"
    azure_deployment_name = "gpt-4o"
    if not key:
        model = AzureChatOpenAI(
            azure_deployment=azure_deployment_name,
            api_version=azure_openai_api_version,
            temperature=0,
            azure_ad_token=azure_ad_token  # Pass the token dynamically
        )
    else:
        model = AzureChatOpenAI(
            azure_deployment=azure_deployment_name,
            api_version=azure_openai_api_version,
            temperature=0,  # Pass the token dynamically
        )

    print("[DEBUG] Azure OpenAI model initialized successfully.")
except Exception as e:
    print(f"[ERROR] Error initializing Azure OpenAI model: {e}, falling back to key auth.")
    raise e
