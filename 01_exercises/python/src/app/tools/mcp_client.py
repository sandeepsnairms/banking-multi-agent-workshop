"""
🚀 ENHANCED MCP CLIENT - Optimized client for shared MCP server
This client manages connections to the shared server efficiently
"""
import asyncio
import subprocess
import time
import os
import signal
import contextvars
from typing import Optional, Dict, Any, List
from langchain_mcp_adapters.client import MultiServerMCPClient
from langchain_core.tools import StructuredTool
from src.app.tools.mcp_server import get_cached_server_instance

# Context variables for tenant/user information
TENANT_CONTEXT = contextvars.ContextVar('tenant_context', default=None)
USER_CONTEXT = contextvars.ContextVar('user_context', default=None) 
THREAD_CONTEXT = contextvars.ContextVar('thread_context', default=None)

def set_mcp_context(tenantId: Optional[str], userId: Optional[str], thread_id: Optional[str]):
    """Set the MCP context for automatic parameter injection"""
    if tenantId:
        TENANT_CONTEXT.set(tenantId)
    if userId:
        USER_CONTEXT.set(userId)
    if thread_id:
        THREAD_CONTEXT.set(thread_id)
    print(f"🔧 MCP CONTEXT: Set context - tenantId='{tenantId}', userId='{userId}', thread_id='{thread_id}'")

def get_mcp_context():
    """Get the current MCP context"""
    return {
        'tenantId': TENANT_CONTEXT.get(),
        'userId': USER_CONTEXT.get(),
        'thread_id': THREAD_CONTEXT.get()
    }

class SharedMCPClient:
    """Enhanced MCP client with connection pooling and management"""
    
    def __init__(self):
        self.server_process: Optional[subprocess.Popen] = None
        self.client: Optional[MultiServerMCPClient] = None
        self.tools_cache: Optional[List] = None
        self.server_ready = False
        self.server_start_time = 0
        
    async def start_shared_server(self) -> bool:
        """Start the shared MCP server as a background process"""
        if self.server_process and self.server_process.poll() is None:
            print("🔄 ENHANCED MCP: Shared server already running")
            return True
            
        print("🚀 ENHANCED MCP: Starting shared server process...")
        self.server_start_time = time.time()
        
        try:
            # Start the shared server with correct working directory
            project_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
            server_path = os.path.join(os.path.dirname(__file__), "mcp_server.py")
            
            print(f"🚀 ENHANCED MCP: Starting server from {project_root}")
            
            self.server_process = subprocess.Popen(
                ["python3", server_path],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                bufsize=1,
                universal_newlines=True,
                cwd=project_root,  # Set correct working directory
                preexec_fn=os.setsid  # Create new process group for clean shutdown
            )
            
            # Wait for server to be ready (give it time to initialize)
            await asyncio.sleep(2.0)  # Allow server startup time
            
            if self.server_process.poll() is None:
                startup_time = (time.time() - self.server_start_time) * 1000
                print(f"✅ ENHANCED MCP: Shared server started in {startup_time:.2f}ms (PID: {self.server_process.pid})")
                self.server_ready = True
                return True
            else:
                stdout, stderr = self.server_process.communicate()
                print(f"❌ ENHANCED MCP: Server failed to start")
                print(f"   stdout: {stdout}")
                print(f"   stderr: {stderr}")
                return False
                
        except Exception as e:
            print(f"❌ ENHANCED MCP: Failed to start shared server: {e}")
            return False
    
    async def connect_to_server(self) -> bool:
        """Connect to the direct shared server (no subprocess)"""
        try:
            print("🔌 ENHANCED MCP: Connecting to DIRECT shared server functions...")
            
            # Direct import and connection to shared server
            from src.app.tools.mcp_server import get_cached_server_instance
            
            # Get the cached server instance (this is async)
            self.direct_server = await get_cached_server_instance()
            
            if not self.direct_server:
                print("❌ ENHANCED MCP: No server instance received")
                return False
            
            # Get available tools from direct server
            try:
                tools_info = self.direct_server.get_available_tools()  # This is synchronous, not async
                self.tools_cache = tools_info if isinstance(tools_info, list) else []
            except Exception as e:
                print(f"⚠️  ENHANCED MCP: Error getting tools: {e}")
                self.tools_cache = []
                
            if not self.tools_cache:
                print("❌ ENHANCED MCP: No tools available from direct server")
                return False
                
            print(f"🛠️  ENHANCED MCP: Connected to direct server with {len(self.tools_cache)} tools:")
            for tool in self.tools_cache:
                name = tool.get('name', 'unknown') if isinstance(tool, dict) else str(tool)
                print(f"   - {name}")
            
            return True
            
        except Exception as e:
            print(f"❌ ENHANCED MCP: Failed to connect to direct server: {e}")
            return False
    
    async def get_tools(self):
        """Get LangChain-compatible tools from the shared MCP server."""
        try:
            print("🔧 ENHANCED MCP: Getting tools from direct server")
            server_instance = await get_cached_server_instance()
            tools_list = server_instance.get_available_tools()
            
            if not tools_list:
                print("❌ ENHANCED MCP: No tools available from direct server")
                return []
            
            # Convert list tools to LangChain compatible tools
            langchain_tools = []
            
            for tool_dict in tools_list:
                try:
                    tool_name = tool_dict.get('name')
                    if not tool_name:
                        print("❌ ENHANCED MCP: Tool missing name, skipping")
                        continue
                        
                    print(f"🔄 ENHANCED MCP: Converting tool {tool_name}")
                    
                    # Create a proper tool function with closure to capture tool_name and self reference
                    def create_tool_function(captured_tool_name, client_instance):
                        async def tool_execution(*args, **kwargs):
                            """Execute tool through context-aware call_tool method."""
                            print(f"🚀 TOOL EXECUTION: Calling {captured_tool_name} with args={args}, kwargs={kwargs}")
                            
                            # Enhanced parameter mapping for tools with specific parameter needs
                            if captured_tool_name == "bank_balance" and args and not kwargs.get("account_number"):
                                kwargs["account_number"] = args[0]
                                print(f"🔧 DEBUG: Fixed bank_balance args - account_number: {args[0]}")
                            
                            elif captured_tool_name == "get_offer_information" and args and not kwargs:
                                # Map positional args to expected parameters
                                if len(args) >= 1:
                                    kwargs["prompt"] = args[0]
                                if len(args) >= 2:
                                    kwargs["type"] = args[1] 
                                print(f"🔧 DEBUG: Fixed get_offer_information args - prompt: {kwargs.get('prompt', 'N/A')}, type: {kwargs.get('type', 'N/A')}")
                            
                            elif captured_tool_name == "bank_transfer" and args and not kwargs:
                                # Map positional args to bank transfer parameters
                                param_names = ["fromAccount", "toAccount", "amount", "tenantId", "userId", "thread_id"]
                                for i, arg in enumerate(args[:len(param_names)]):
                                    kwargs[param_names[i]] = arg
                                print(f"🔧 DEBUG: Fixed bank_transfer args - mapped {len(args)} parameters")
                            
                            elif captured_tool_name == "get_transaction_history" and args and not kwargs:
                                # Map positional args to transaction history parameters
                                param_names = ["account_number", "start_date", "end_date", "tenantId", "userId"]
                                for i, arg in enumerate(args[:len(param_names)]):
                                    kwargs[param_names[i]] = arg
                                print(f"🔧 DEBUG: Fixed get_transaction_history args - mapped {len(args)} parameters")
                            
                            # Generic fallback for other tools with single parameter
                            elif args and not kwargs:
                                if len(args) == 1 and captured_tool_name not in ["transfer_to_sales_agent", "transfer_to_customer_support_agent", "transfer_to_transactions_agent", "create_account", "health_check"]:
                                    kwargs["input"] = args[0]
                                    print(f"🔧 DEBUG: Generic parameter mapping for {captured_tool_name} - input: {args[0]}")
                            
                            print(f"🔧 DEBUG: Final arguments passed: {kwargs}")
                            
                            # 🔧 CRITICAL FIX: Use context-aware call_tool instead of direct server call
                            result = await client_instance.call_tool(captured_tool_name, kwargs)
                            print(f"🔧 DEBUG: tool_execution received result: {result} (type: {type(result)})")
                            return result
                        
                        # Set proper name attribute for LangChain compatibility
                        tool_execution.__name__ = captured_tool_name
                        return tool_execution
                    
                    # Create the actual tool function with self reference for context injection
                    tool_func = create_tool_function(tool_name, self)
                    
                    # Get parameter schema if available
                    parameters = tool_dict.get('parameters', {})
                    
                    # Create StructuredTool with proper parameter schema for tools that need parameters
                    if tool_name == "bank_balance" and parameters:
                        # Bank balance tool with account_number parameter
                        from pydantic import BaseModel, Field
                        
                        class BankBalanceInput(BaseModel):
                            account_number: str = Field(description="The account number to check balance for (e.g., 'Acc001', '123', 'ABC123')")
                        
                        structured_tool = StructuredTool.from_function(
                            coroutine=tool_func,
                            name=tool_name,
                            description=tool_dict.get('description', f'Execute {tool_name}'),
                            args_schema=BankBalanceInput
                        )
                    
                    elif tool_name == "get_offer_information" and parameters:
                        # Offer information tool with prompt and type parameters
                        from pydantic import BaseModel, Field
                        
                        class OfferInfoInput(BaseModel):
                            prompt: str = Field(description="The user's query about banking offers and products")
                            type: str = Field(default="", description="Type of offer (optional, e.g., 'credit_card', 'loan', 'savings')")
                        
                        structured_tool = StructuredTool.from_function(
                            coroutine=tool_func,
                            name=tool_name,
                            description=tool_dict.get('description', f'Execute {tool_name}'),
                            args_schema=OfferInfoInput
                        )
                    
                    elif tool_name == "bank_transfer" and parameters:
                        # Bank transfer tool with required parameters
                        from pydantic import BaseModel, Field
                        
                        class BankTransferInput(BaseModel):
                            fromAccount: str = Field(description="Source account number for the transfer")
                            toAccount: str = Field(description="Destination account number for the transfer")
                            amount: float = Field(description="Amount to transfer (positive number)")
                            tenantId: str = Field(description="Tenant ID for the transaction")
                            userId: str = Field(description="User ID for the transaction")
                            thread_id: str = Field(description="Thread ID for the transaction")
                        
                        structured_tool = StructuredTool.from_function(
                            coroutine=tool_func,
                            name=tool_name,
                            description=tool_dict.get('description', f'Execute {tool_name}'),
                            args_schema=BankTransferInput
                        )
                    
                    elif tool_name == "get_transaction_history" and parameters:
                        # Transaction history tool with date parameters
                        from pydantic import BaseModel, Field
                        
                        class TransactionHistoryInput(BaseModel):
                            account_number: str = Field(description="Account number to get transaction history for")
                            start_date: str = Field(description="Start date for transaction history (YYYY-MM-DD format)")
                            end_date: str = Field(description="End date for transaction history (YYYY-MM-DD format)")
                            tenantId: str = Field(description="Tenant ID")
                            userId: str = Field(description="User ID")
                        
                        structured_tool = StructuredTool.from_function(
                            coroutine=tool_func,
                            name=tool_name,
                            description=tool_dict.get('description', f'Execute {tool_name}'),
                            args_schema=TransactionHistoryInput
                        )
                    
                    elif tool_name == "calculate_monthly_payment" and parameters:
                        from pydantic import BaseModel, Field
                        
                        class MonthlyPaymentInput(BaseModel):
                            loan_amount: float = Field(description="The total loan amount in dollars")
                            years: int = Field(description="The loan term in years")
                        
                        structured_tool = StructuredTool.from_function(
                            coroutine=tool_func,
                            name=tool_name,
                            description=tool_dict.get('description', f'Execute {tool_name}'),
                            args_schema=MonthlyPaymentInput
                        )
                    
                    elif tool_name == "service_request" and parameters:
                        from pydantic import BaseModel, Field
                        
                        class ServiceRequestInput(BaseModel):
                            recipientPhone: str = Field(description="Phone number of the recipient for the service request")
                            recipientEmail: str = Field(description="Email address of the recipient for the service request")
                            requestSummary: str = Field(description="Summary description of the service request")
                            tenantId: str = Field(description="Tenant ID for the request")
                            userId: str = Field(description="User ID for the request")
                        
                        structured_tool = StructuredTool.from_function(
                            coroutine=tool_func,
                            name=tool_name,
                            description=tool_dict.get('description', f'Execute {tool_name}'),
                            args_schema=ServiceRequestInput
                        )
                    
                    elif tool_name == "get_branch_location" and parameters:
                        from pydantic import BaseModel, Field
                        
                        class BranchLocationInput(BaseModel):
                            state: str = Field(description="State name to get branch locations for (e.g., 'California', 'Texas')")
                        
                        structured_tool = StructuredTool.from_function(
                            coroutine=tool_func,
                            name=tool_name,
                            description=tool_dict.get('description', f'Execute {tool_name}'),
                            args_schema=BranchLocationInput
                        )
                    
                    else:
                        # Standard tool creation for transfer tools and other parameter-less tools
                        structured_tool = StructuredTool.from_function(
                            coroutine=tool_func,
                            name=tool_name,
                            description=tool_dict.get('description', f'Execute {tool_name}')
                        )
                    
                    langchain_tools.append(structured_tool)
                    print(f"✅ ENHANCED MCP: Successfully converted {tool_name} to LangChain tool")
                    if tool_name in ["bank_balance", "get_offer_information", "bank_transfer", "get_transaction_history", "calculate_monthly_payment", "service_request", "get_branch_location"]:
                        print(f"🔧 ENHANCED MCP: {tool_name} schema: {structured_tool.args_schema if hasattr(structured_tool, 'args_schema') else 'No schema'}")
                    
                except Exception as e:
                    print(f"❌ ENHANCED MCP: Failed to convert tool {tool_dict}: {e}")
                    import traceback
                    traceback.print_exc()
                    continue
            
            print(f"🛠️ ENHANCED MCP: Converted {len(langchain_tools)} tools to LangChain format")
            
            # Verify tools are proper LangChain objects
            for tool in langchain_tools:
                if hasattr(tool, 'name'):
                    print(f"✅ VERIFIED: Tool {tool.name} has proper LangChain structure")
                else:
                    print(f"❌ ERROR: Tool {tool} missing LangChain structure")
            
            return langchain_tools
            
        except Exception as e:
            print(f"❌ ENHANCED MCP: Failed to get tools: {e}")
            import traceback
            traceback.print_exc()
            return []
    
    async def call_tool(self, tool_name: str, arguments: Dict[str, Any]) -> Any:
        """Call a tool with ZERO subprocess overhead - direct function execution"""
        if not self.direct_server:
            if not await self.connect_to_server():
                raise Exception("Could not connect to direct shared server")
        
        call_start = time.time()
        print(f"📞 ENHANCED MCP: DIRECT calling tool '{tool_name}' (zero subprocess overhead)")
        print(f"🔧 DEBUG: Tool arguments received: {arguments}")
        
        # 🚀 AUTOMATIC CONTEXT INJECTION - Fix the LLM parameter issue
        context = get_mcp_context()
        print(f"🔧 CONTEXT INJECTION: Retrieved context = {context}")
        tools_needing_context = ['bank_balance', 'bank_transfer', 'get_transaction_history', 'create_account', 'service_request']
        
        if tool_name in tools_needing_context:
            print(f"🔧 CONTEXT INJECTION: Tool '{tool_name}' needs context parameters")
            print(f"🔧 CONTEXT INJECTION: Current arguments before injection: {arguments}")
            
            # Inject missing context parameters if available
            if context.get('tenantId') and 'tenantId' not in arguments:
                arguments['tenantId'] = context['tenantId']
                print(f"🔧 CONTEXT INJECTION: Added tenantId='{context['tenantId']}'")
            else:
                print(f"🔧 CONTEXT INJECTION: tenantId not injected - context has: '{context.get('tenantId')}', args has: {'tenantId' in arguments}")
            
            if context.get('userId') and 'userId' not in arguments:
                arguments['userId'] = context['userId']  
                print(f"🔧 CONTEXT INJECTION: Added userId='{context['userId']}'")
            else:
                print(f"🔧 CONTEXT INJECTION: userId not injected - context has: '{context.get('userId')}', args has: {'userId' in arguments}")
            
            if context.get('thread_id') and 'thread_id' not in arguments and tool_name in ['bank_balance', 'bank_transfer', 'get_transaction_history']:
                arguments['thread_id'] = context['thread_id']
                print(f"🔧 CONTEXT INJECTION: Added thread_id='{context['thread_id']}'")
            
            print(f"🔧 CONTEXT INJECTION: Final arguments with context: {arguments}")
        else:
            print(f"🔧 CONTEXT INJECTION: Tool '{tool_name}' does not need context parameters")
        
        try:
            # 🚀 BREAKTHROUGH: Direct tool execution with cached services
            result = await self.direct_server.call_tool_directly(tool_name, arguments)
            call_time = (time.time() - call_start) * 1000
            print(f"✅ ENHANCED MCP: DIRECT tool call completed in {call_time:.2f}ms")
            return result
        except Exception as e:
            call_time = (time.time() - call_start) * 1000
            print(f"❌ ENHANCED MCP: DIRECT tool call failed in {call_time:.2f}ms: {e}")
            raise
    
    async def cleanup(self):
        """Clean shutdown of direct server connection"""
        print("🔄 ENHANCED MCP: Cleaning up DIRECT server connection...")
        
        # No subprocess cleanup needed for direct calls!
        self.direct_server = None
        self.tools_cache = None
        
        # Legacy subprocess cleanup (if fallback was used)
        if self.client:
            try:
                await self.client.close()
            except:
                pass
            self.client = None
        
        if self.server_process and self.server_process.poll() is None:
            try:
                # Send SIGTERM to process group for clean shutdown
                os.killpg(os.getpgid(self.server_process.pid), signal.SIGTERM)
                self.server_process.wait(timeout=5)
                print("✅ ENHANCED MCP: Server shut down cleanly")
            except subprocess.TimeoutExpired:
                print("⚠️  ENHANCED MCP: Server didn't shut down cleanly, forcing...")
                os.killpg(os.getpgid(self.server_process.pid), signal.SIGKILL)
            except Exception as e:
                print(f"⚠️  ENHANCED MCP: Error during server shutdown: {e}")
        
        self.server_process = None
        self.server_ready = False
        self.tools_cache = None

# Global instance for reuse
_shared_mcp_client: Optional[SharedMCPClient] = None

async def get_shared_mcp_client() -> SharedMCPClient:
    """Get or create the shared MCP client"""
    global _shared_mcp_client
    
    if _shared_mcp_client is None:
        print("🔄 ENHANCED MCP: Initializing shared client...")
        _shared_mcp_client = SharedMCPClient()
        
        if not await _shared_mcp_client.connect_to_server():
            raise Exception("Failed to initialize shared MCP client")
    
    return _shared_mcp_client

async def cleanup_shared_mcp_client():
    """Clean up the shared client"""
    global _shared_mcp_client
    
    if _shared_mcp_client:
        await _shared_mcp_client.cleanup()
        _shared_mcp_client = None
