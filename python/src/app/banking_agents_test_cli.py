import requests

API_URL = "http://127.0.0.1:8000/conversation"  # Update to the correct endpoint if hosted elsewhere.


def main():
    print("Interactive Agent Shell")
    print("Type 'exit' to end the conversation.")

    conversation_id = None  # Keeps track of the current conversation ID.

    while True:
        user_message = input("You: ")
        if user_message.lower() == "exit":
            print("Exiting the conversation. Goodbye!")
            break

        # Build the payload for the API call.
        payload = {
            "conversation_id": conversation_id,
            "user_message": user_message
        }

        try:
            # Call the API.
            response = requests.post(API_URL, json=payload)
            response_data = response.json()

            if response.status_code == 200:
                # Extract conversation ID and responses.
                conversation_id = response_data.get("conversation_id")
                responses = response_data.get("responses", [])

                # Display responses with active agent annotation.
                for message in responses:
                    active_agent = message.get("role")
                    content = message.get("content", "No content received.")
                    if active_agent == "assistant":
                        print(f"{active_agent}: {content}")
            else:
                print(f"Error: {response_data.get('detail', 'Unknown error occurred.')}")
        except Exception as e:
            print(f"An error occurred: {e}")


if __name__ == "__main__":
    main()
