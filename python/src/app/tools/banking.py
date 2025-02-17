from langchain_core.tools import tool

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