from langchain_core.tools import tool
from langgraph.graph import MessagesState

from src.app.services.azure_cosmos_db import fetch_latest_account_number, create_account_record


@tool
def create_account(account_holder: str, balance: float) -> str:
    """
    Create a new bank account for a user.

    This function retrieves the latest account number, increments it, and creates a new account record
    in CosmosDB associated with a specific user and tenant.
    """
    print(f"Creating account for {account_holder}")

    max_attempts = 10
    account_number = fetch_latest_account_number()

    print(f"Latest account number: {account_number}")
    if account_number is None:
        account_number = 1
    else:
        account_number += 1

    for attempt in range(max_attempts):
        account_data = {
            "id": f"a{account_number}",
            "accountId": f"a{account_number}",
            "type": "SavingsAccount",
            "accountName": account_holder,
            "balance": balance,
            "startDate": "01-01-2025",
            "accountDescription": "Some description here",
            "accountProperties": {
                "key1": "Value1",
                "key2": "Value2"
            }
        }
        try:
            print(f"Creating account record: {account_data}")
            create_account_record(account_data)
            return f"Successfully created account {account_number} for {account_holder} with a balance of ${balance}"
        except Exception as e:
            account_number += 1
            if attempt == max_attempts - 1:
                return f"Failed to create account after {max_attempts} attempts: {e}"

    return f"Failed to create account after {max_attempts} attempts"



@tool
def bank_transfer(account_number: str, amount: float):
    """Transfer to bank agent"""
    return f"Successfully transferred ${amount} to account number {account_number}"


@tool
def bank_balance(account_number: str):
    """Transfer to bank agent"""
    return f"The balance for account number {account_number} is $1000"


@tool
def calculate_monthly_payment(loan_amount: float, years: int) -> float:
    """Calculate the monthly payment for a loan."""
    interest_rate = 0.05  # Hardcoded annual interest rate (5%)
    monthly_rate = interest_rate / 12  # Convert annual rate to monthly
    total_payments = years * 12  # Total number of monthly payments

    if monthly_rate == 0:
        return loan_amount / total_payments  # If interest rate is 0, simple division

    monthly_payment = (loan_amount * monthly_rate * (1 + monthly_rate) ** total_payments) / \
                      ((1 + monthly_rate) ** total_payments - 1)

    return round(monthly_payment, 2)  # Rounded to 2 decimal places
