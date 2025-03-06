# Exercise 03 - Implementing Core Banking Operations (Ditch this exercise)

[< Previous Exercise](./Exercise-02.md) - **[Home](../README.md)** - [Next Exercise >](./Exercise-04.md)

## Introduction

The content of this exercise is not part of the learning objectives for this workshop. I'm filling in the contents here but this exercise needs to be rewritten as something else.

In this lab, you'll implement real banking operations with proper data persistence using Cosmos DB. You'll create robust account management and transaction processing systems with proper validation and error handling.


## Description


## Learning Objectives

None provided

#### Presentation (15 mins)

- Tool creation patterns
- Integration best practices
- Error handling strategies

#### Exercise (30 mins)

## Steps (30 mins)

  - [Create Banking Account Tools](#step-1-create-banking-operations)
  - [Create Banking Product Tools](#step-2-update-banking-tools)
  - [Create Agent Transfer Tool](#step-3-create-the-tests)
  - [Create the Banking Agents](#step-4-testing-the-implementation)

//Old Steps
1. Add Cosmos DB containers for:
   - Account management
   - Transaction processing
   - Product recommendations
2. Implement:
   - Error handling
   - Validation logic
   - Complex scenario testing

### Step 1: Create Banking Operations

TBD need overview and explanation for each step

Create `src/app/banking_operations.py`:

```python
from typing import Dict, Any, Optional, List
from datetime import datetime
import uuid
from decimal import Decimal
from .services.azure_cosmos_db import cosmos_db

class BankingOperations:
    @staticmethod
    def _validate_amount(amount: float) -> bool:
        """Validate if amount is positive and has valid decimal places."""
        try:
            if amount <= 0:
                return False
            # Ensure no more than 2 decimal places
            decimal_amount = Decimal(str(amount))
            return decimal_amount.as_tuple().exponent >= -2
        except:
            return False

    @staticmethod
    async def create_account(
        holder_name: str,
        account_type: str,
        initial_deposit: float
    ) -> Dict[str, Any]:
        """Create a new bank account."""
        try:
            if not BankingOperations._validate_amount(initial_deposit):
                return {"status": "error", "message": "Invalid initial deposit amount"}

            account_id = str(uuid.uuid4())
            account = {
                "id": account_id,
                "account_id": account_id,
                "holder_name": holder_name,
                "account_type": account_type.lower(),
                "balance": initial_deposit,
                "status": "active",
                "created_at": datetime.utcnow().isoformat(),
                "updated_at": datetime.utcnow().isoformat()
            }

            await cosmos_db.accounts.create_item(body=account)
            return {
                "status": "success",
                "account_id": account_id,
                "message": f"Account created successfully for {holder_name}"
            }
        except Exception as e:
            return {"status": "error", "message": f"Error creating account: {str(e)}"}

    @staticmethod
    async def get_account_balance(account_id: str) -> Dict[str, Any]:
        """Get account balance and details."""
        try:
            account = await cosmos_db.accounts.read_item(
                item=account_id,
                partition_key=account_id
            )
            return {
                "status": "success",
                "balance": account["balance"],
                "account_type": account["account_type"],
                "holder_name": account["holder_name"]
            }
        except Exception as e:
            return {"status": "error", "message": "Account not found"}

    @staticmethod
    async def process_transfer(
        from_account: str,
        to_account: str,
        amount: float,
        description: Optional[str] = None
    ) -> Dict[str, Any]:
        """Process a transfer between accounts."""
        try:
            # Validate amount
            if not BankingOperations._validate_amount(amount):
                return {"status": "error", "message": "Invalid transfer amount"}

            # Get source account
            try:
                source = await cosmos_db.accounts.read_item(
                    item=from_account,
                    partition_key=from_account
                )
            except:
                return {"status": "error", "message": "Source account not found"}

            # Check sufficient funds
            if source["balance"] < amount:
                return {"status": "error", "message": "Insufficient funds"}

            # Get destination account
            try:
                dest = await cosmos_db.accounts.read_item(
                    item=to_account,
                    partition_key=to_account
                )
            except:
                return {"status": "error", "message": "Destination account not found"}

            # Create transaction record
            transaction_id = str(uuid.uuid4())
            transaction = {
                "id": transaction_id,
                "transaction_id": transaction_id,
                "from_account": from_account,
                "to_account": to_account,
                "amount": amount,
                "description": description or "Transfer",
                "status": "completed",
                "timestamp": datetime.utcnow().isoformat()
            }

            # Update account balances
            source["balance"] -= amount
            dest["balance"] += amount
            source["updated_at"] = datetime.utcnow().isoformat()
            dest["updated_at"] = datetime.utcnow().isoformat()

            # Save all changes
            await cosmos_db.transactions.create_item(body=transaction)
            await cosmos_db.accounts.replace_item(
                item=source["id"],
                body=source
            )
            await cosmos_db.accounts.replace_item(
                item=dest["id"],
                body=dest
            )

            return {
                "status": "success",
                "transaction_id": transaction_id,
                "new_balance": source["balance"],
                "message": f"Successfully transferred ${amount} to account {to_account}"
            }
        except Exception as e:
            return {"status": "error", "message": f"Error processing transfer: {str(e)}"}

    @staticmethod
    async def get_transaction_history(
        account_id: str,
        limit: int = 10
    ) -> Dict[str, Any]:
        """Get transaction history for an account."""
        try:
            query = """
                SELECT * FROM c
                WHERE c.from_account = @account_id OR c.to_account = @account_id
                ORDER BY c.timestamp DESC
                OFFSET 0 LIMIT @limit
            """
            parameters = [
                {"name": "@account_id", "value": account_id},
                {"name": "@limit", "value": limit}
            ]

            transactions = []
            async for transaction in cosmos_db.transactions.query_items(
                query=query,
                parameters=parameters,
                enable_cross_partition_query=True
            ):
                transactions.append(transaction)

            return {
                "status": "success",
                "transactions": transactions,
                "count": len(transactions)
            }
        except Exception as e:
            return {"status": "error", "message": f"Error fetching transactions: {str(e)}"}
```

### Step 2: Update Banking Tools

TBD need overview and explanation for each step

Update `src/app/tools/banking.py`:

```python
from typing import Dict, Any
from ..banking_operations import BankingOperations

async def create_account(holder_name: str, account_type: str, initial_deposit: float) -> Dict[str, Any]:
    """Create a new bank account."""
    return await BankingOperations.create_account(
        holder_name=holder_name,
        account_type=account_type,
        initial_deposit=initial_deposit
    )

async def get_account_balance(account_id: str) -> Dict[str, Any]:
    """Get account balance and details."""
    return await BankingOperations.get_account_balance(account_id)

async def process_transfer(from_account: str, to_account: str, amount: float) -> Dict[str, Any]:
    """Process a transfer between accounts."""
    return await BankingOperations.process_transfer(
        from_account=from_account,
        to_account=to_account,
        amount=amount
    )

async def get_transaction_history(account_id: str, limit: int = 10) -> Dict[str, Any]:
    """Get transaction history for an account."""
    return await BankingOperations.get_transaction_history(
        account_id=account_id,
        limit=limit
    )
```

### Step 3: Create the Tests

TBD need overview and explanation for each step

Create `test/test_banking_operations.py`:

```python
import asyncio
import sys
sys.path.append("../src/app")

from banking_operations import BankingOperations
from services.azure_cosmos_db import cosmos_db

async def test_banking_operations():
    # Initialize Cosmos DB
    print("Initializing Cosmos DB...")
    await cosmos_db.initialize()

    # Test account creation
    print("\nCreating test accounts...")
    account1 = await BankingOperations.create_account(
        "John Doe",
        "checking",
        1000.0
    )
    print(f"Account 1 creation: {account1}")

    account2 = await BankingOperations.create_account(
        "Jane Doe",
        "savings",
        500.0
    )
    print(f"Account 2 creation: {account2}")

    if account1["status"] == "success" and account2["status"] == "success":
        account1_id = account1["account_id"]
        account2_id = account2["account_id"]

        # Test balance check
        print("\nChecking balances...")
        balance1 = await BankingOperations.get_account_balance(account1_id)
        balance2 = await BankingOperations.get_account_balance(account2_id)
        print(f"Account 1 balance: {balance1}")
        print(f"Account 2 balance: {balance2}")

        # Test transfer
        print("\nProcessing transfer...")
        transfer = await BankingOperations.process_transfer(
            account1_id,
            account2_id,
            200.0,
            "Test transfer"
        )
        print(f"Transfer result: {transfer}")

        # Check updated balances
        print("\nChecking updated balances...")
        balance1 = await BankingOperations.get_account_balance(account1_id)
        balance2 = await BankingOperations.get_account_balance(account2_id)
        print(f"Account 1 new balance: {balance1}")
        print(f"Account 2 new balance: {balance2}")

        # Get transaction history
        print("\nGetting transaction history...")
        history = await BankingOperations.get_transaction_history(account1_id)
        print(f"Transaction history: {history}")

        # Test error cases
        print("\nTesting error cases...")
        # Invalid amount
        invalid_transfer = await BankingOperations.process_transfer(
            account1_id,
            account2_id,
            -100.0
        )
        print(f"Invalid amount transfer: {invalid_transfer}")

        # Insufficient funds
        large_transfer = await BankingOperations.process_transfer(
            account1_id,
            account2_id,
            10000.0
        )
        print(f"Insufficient funds transfer: {large_transfer}")

        # Invalid account
        invalid_account = await BankingOperations.get_account_balance("invalid_id")
        print(f"Invalid account balance check: {invalid_account}")

if __name__ == "__main__":
    asyncio.run(test_banking_operations())
```

### Step 4: Testing the implementation

TBD need overview and explanation for each step

1. Run the test script:

```bash
python test/test_banking_operations.py
```

2. Expected Output:

```
Initializing Cosmos DB...

Creating test accounts...
Account 1 creation: {'status': 'success', 'account_id': '...', 'message': 'Account created successfully for John Doe'}
Account 2 creation: {'status': 'success', 'account_id': '...', 'message': 'Account created successfully for Jane Doe'}

Checking balances...
Account 1 balance: {'status': 'success', 'balance': 1000.0, 'account_type': 'checking', 'holder_name': 'John Doe'}
Account 2 balance: {'status': 'success', 'balance': 500.0, 'account_type': 'savings', 'holder_name': 'Jane Doe'}

Processing transfer...
Transfer result: {'status': 'success', 'transaction_id': '...', 'new_balance': 800.0, 'message': 'Successfully transferred $200.0 to account ...'}

Checking updated balances...
Account 1 new balance: {'status': 'success', 'balance': 800.0, 'account_type': 'checking', 'holder_name': 'John Doe'}
Account 2 new balance: {'status': 'success', 'balance': 700.0, 'account_type': 'savings', 'holder_name': 'Jane Doe'}

Getting transaction history...
Transaction history: {'status': 'success', 'transactions': [...], 'count': 1}

Testing error cases...
Invalid amount transfer: {'status': 'error', 'message': 'Invalid transfer amount'}
Insufficient funds transfer: {'status': 'error', 'message': 'Insufficient funds'}
Invalid account balance check: {'status': 'error', 'message': 'Account not found'}
```

## Validation Checklist

- [ ] Cosmos DB containers created successfully
- [ ] Account creation works with validation
- [ ] Balance queries return accurate information
- [ ] Transfers process with proper validation
- [ ] Transaction history is properly recorded
- [ ] Error handling works for all cases
- [ ] Decimal amounts are properly handled

## Common Issues and Solutions

1. Cosmos DB Operations:

   - Check connection strings
   - Verify container creation
   - Monitor request units consumption

2. Transaction Processing:

   - Verify concurrent operation handling
   - Check error handling for insufficient funds
   - Validate account existence before transfers

3. Data Consistency:
   - Monitor balance updates
   - Verify transaction records
   - Check timestamp ordering

## Next Steps

In [Exercise 4](./Exercise-04.md), we will:

- Implement vector search for products
- Add semantic caching
- Create product recommendation logic

Proceed to [Exercise 4](./Exercise-04.md)
