import logging
from datetime import datetime
from typing import List, Dict

from langchain_core.runnables import RunnableConfig
from langchain_core.tools import tool
from langgraph.graph import MessagesState

from src.app.services.azure_cosmos_db import fetch_latest_account_number, create_account_record, \
    fetch_latest_transaction_number, update_account_container, fetch_account_by_number, create_transaction_record, \
    patch_account_record, fetch_transactions_by_date_range


@tool
def create_account(account_holder: str, balance: float, config: RunnableConfig) -> str:
    """
    Create a new bank account for a user.

    This function retrieves the latest account number, increments it, and creates a new account record
    in Cosmos DB associated with a specific user and tenant.
    """
    print(f"Creating account for {account_holder}")
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    print(f"In create_account tool, thread ID: {thread_id}")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    print(f"In create_account tool, user ID: {userId}")
    print(f"In create_account tool, tenant ID: {tenantId}")

    max_attempts = 10
    account_number = fetch_latest_account_number()

    print(f"Latest account number: {account_number}")
    if account_number is None:
        account_number = 1
    else:
        account_number += 1

    for attempt in range(max_attempts):
        account_data = {
            "id": f"{account_number}",
            "accountId": f"A{account_number}",
            "tenantId": tenantId,
            "userId": userId,
            "name": "Account",
            "type": "BankAccount",
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
def bank_transfer(config: RunnableConfig, toAccount: str, fromAccount: str, amount: float) -> str:
    """Wrapper function to handle the transfer of funds between two accounts."""
    # Debit the amount from the fromAccount
    debit_result = bank_transaction(config, fromAccount, amount, credit_account=0, debit_account=amount)
    if "Failed" in debit_result:
        return f"Failed to debit amount from {fromAccount}: {debit_result}"

    # Credit the amount to the toAccount
    credit_result = bank_transaction(config, toAccount, amount, credit_account=amount, debit_account=0)
    if "Failed" in credit_result:
        return f"Failed to credit amount to {toAccount}: {credit_result}"

    return f"Successfully transferred ${amount} from account {fromAccount} to account {toAccount}"

def bank_transaction(config: RunnableConfig, account_number: str, amount: float, credit_account: float,
                  debit_account: float) -> str:
    """Transfer to bank agent"""
    global new_balance
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")

    # Fetch the account record
    account = fetch_account_by_number(account_number, tenantId, userId)
    if not account:
        return f"Account {account_number} not found for tenant {tenantId} and user {userId}"

    max_attempts = 5
    for attempt in range(max_attempts):
        try:
            # Fetch the latest transaction number for the account
            latest_transaction_number = fetch_latest_transaction_number(account_number)
            transaction_id = f"{account_number}-{latest_transaction_number + 1}"

            # Calculate the new account balance
            new_balance = account["balance"] + credit_account - debit_account

            # Create the transaction record
            transaction_data = {
                "id": transaction_id,
                "tenantId": tenantId,
                "accountId": account["accountId"],
                "type": "BankTransaction",
                "debitAmount": debit_account,
                "creditAmount": credit_account,
                "accountBalance": new_balance,
                "details": "Bank Transfer",
                "transactionDateTime": datetime.utcnow().isoformat() + "Z"
            }

            create_transaction_record(transaction_data)
            print(f"Successfully transferred ${amount} to account number {account_number}")
            break  # Stop retrying after a successful attempt
        except Exception as e:
            logging.error(f"Attempt {attempt + 1} failed: {e}")
            if attempt == max_attempts - 1:
                return f"Failed to create transaction record after {max_attempts} attempts: {e}"

    # Update the account balance
    patch_account_record(account["accountId"], new_balance)
    return f"Successfully transferred ${amount} to account number {account_number}"

@tool
def get_transaction_history(accountId: str, startDate: datetime, endDate: datetime) -> List[Dict]:
    """
    Retrieve the transaction history for a specific account between two dates.

    :param accountId: The ID of the account to retrieve transactions for.
    :param startDate: The start date for the transaction history.
    :param endDate: The end date for the transaction history.
    :return: A list of transactions within the specified date range.
    """
    try:
        transactions = fetch_transactions_by_date_range(accountId, startDate, endDate)
        return transactions
    except Exception as e:
        logging.error(f"Error fetching transaction history for account {accountId}: {e}")
        return []

@tool
def bank_balance(config: RunnableConfig, account_number: str) -> str:
    """Retrieve the balance for a specific bank account."""
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")

    # Fetch the account record
    account = fetch_account_by_number(account_number, tenantId, userId)
    if not account:
        return f"Account {account_number} not found for tenant {tenantId} and user {userId}"

    balance = account.get("balance", 0)
    return f"The balance for account number {account_number} is ${balance}"


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
