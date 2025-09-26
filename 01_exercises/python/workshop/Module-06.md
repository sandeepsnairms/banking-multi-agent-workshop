# Module 06 - Converting to Model Context Protocol (MCP)

[< Lessons Learned, Agent Futures, Q&A](./Module-05.md) - **[Home](Home.md)**

## Overview

In this module, you'll learn how to convert your multi-agent banking application from native LangChain tool integration to use **Model Context Protocol (MCP)**. This conversion addresses specific architectural and performance challenges that emerge in production multi-agent systems.

## Understanding the Implementation Rationale

### Why MCP Was Implemented: Architectural Benefits Over Performance

A decision to implement MCP in this banking system would **not be driven by performance concerns**. In fact, native LangChain tools often perform as well as or better than MCP in terms of raw speed. The real drivers would be **architectural and organizational benefits**:

| Architectural Aspect | LangChain @tool Functions | MCP Approach | Benefit |
|---------------------|--------------------------|-------------|----------|
| **Coupling** | AI models tightly coupled to specific Python tool implementations | AI models interact through standardized protocol | Tools can be updated, replaced, scaled independently |
| **Standardization** | Each AI application defines its own tool interfaces | Uses industry-standard protocol (JSON-RPC 2.0) | Tools work with any MCP-compatible AI system |
| **Separation of Concerns** | Banking logic mixed with AI agent code | Clear separation between AI orchestration and business logic | Different teams can own different layers |
| **Deployment Flexibility** | Tools must run in same process as AI application | Tools can run locally (embedded) or remotely (microservices) | Choose deployment strategy based on operational needs |
| **Team Autonomy** | AI and domain teams must coordinate releases | Independent development and deployment cycles | Faster iteration and reduced coordination overhead |
| **Technology Stack** | Limited to Python + LangChain ecosystem | Server can use any programming language | Technology choice freedom for domain teams |

### The Real Trade-off: Performance vs Architecture

MCP implementation represents this trade-off:
- **Performance Cost**: Additional protocol overhead (typically 10-50ms per tool call)
- **Architectural Benefit**: Loose coupling, standardization, maintainability, team autonomy
- **Strategic Value**: Long-term maintainability and ecosystem compatibility over raw speed

This is a "USB-C for AI" decision - accepting slight performance overhead for massive interoperability benefits.

## Architecture Trade-offs Analysis

### LangChain Tools vs MCP: The Decision Matrix

| Aspect | LangChain @tool Functions | Local MCP | Remote MCP |
|--------|-------------|-----------|------------|
| **Development Speed** | ✅ Fastest to implement | ⚠️ Moderate setup | ❌ Complex initial setup |
| **Raw Performance** | ✅ Fastest (no protocol overhead) | ⚠️ Slight overhead (process communication) | ❌ Higher overhead (HTTP + serialization) |
| **Loose Coupling** | ❌ Tight coupling to Python runtime | ✅ Protocol-based separation | ✅ Complete decoupling |
| **Standardization** | ❌ Application-specific interfaces | ✅ Standard MCP protocol | ✅ Standard MCP protocol |
| **Team Autonomy** | ❌ AI and domain teams must coordinate | ✅ Independent development cycles | ✅ Full team independence |
| **Deployment Flexibility** | ❌ Must run with AI application | ✅ Embedded or separate process | ✅ Independent scaling and deployment |
| **Tool Reusability** | ❌ Tied to specific application | ⚠️ Reusable with other MCP clients | ✅ Ecosystem interoperability |
| **Maintenance** | ❌ Monolithic updates required | ⚠️ Moderate coordination needed | ✅ Independent maintenance cycles |
| **Technology Stack** | ❌ Limited to Python + LangChain | ⚠️ MCP server can use any language | ✅ Complete technology independence |
| **Operational Complexity** | ✅ Simple (single process) | ⚠️ Process management | ❌ Distributed system complexity |

### When to Choose Each Approach

**Choose LangChain @tool Functions when:**
- Building prototypes, demos, or simple applications
- Single team owns both AI logic and domain tools
- Performance is the absolute top priority
- Simple deployment requirements (single process)
- No need for tool reusability across different AI systems

**Choose Local MCP when:**
- Want loose coupling without operational complexity
- Multiple teams work on the same application
- Need protocol standardization within your organization
- Tools may be reused by other internal applications
- Acceptable to trade slight performance for better architecture

**Choose Remote MCP when:**
- Building enterprise systems with multiple AI applications
- Different teams own AI and domain logic
- Need independent deployment and scaling of tools
- Want ecosystem interoperability with other MCP-compatible systems
- Willing to accept operational complexity for maximum architectural benefits
- Building microservices architecture

### Performance vs Complexity Trade-off

This implementation represents a classic engineering trade-off:

- **Performance Gain**: 25-75x faster tool execution
- **Complexity Cost**: Custom MCP protocol implementation
- **Maintenance Burden**: Non-standard architecture requiring specialized knowledge
- **Innovation Benefit**: Enables real-time banking operations that weren't possible before

## Learning Objectives

By the end of this module, you will:
- Understand the architectural benefits that drove MCP adoption over native tools
- Analyze trade-offs between tight coupling (LangChain tools) and loose coupling (MCP)
- Implement both Local and Remote MCP deployment modes
- Experience the protocol standardization benefits of MCP
- Make informed decisions about when architectural benefits justify performance costs
- Understand MCP's role in building maintainable, multi-team AI systems

## Module Exercises

1. [Activity 1: Analyzing MCP Architecture and Trade-offs](#activity-1-analyzing-mcp-architecture-and-trade-offs)
2. [Activity 2: Implementing MCP Client-Server Architecture](#activity-2-implementing-mcp-client-server-architecture)
3. [Activity 3: Replace Banking Agent Files](#activity-3-replace-banking-agent-files)
4. [Activity 4: Test Local MCP Mode](#activity-4-test-local-mcp-mode)
5. [Activity 5: Test Remote MCP Mode](#activity-5-test-remote-mcp-mode)

## Activity 1: Analyzing MCP Architecture and Trade-offs

Before implementing MCP, let's understand the architectural decisions and performance considerations that led to this custom implementation.

### The Performance Problem with Standard MCP

Standard MCP implementations using `@mcp.tool()` decorators face several issues in production:

```python
# Standard MCP approach - SLOW in production
@mcp.tool()
def bank_balance(account_number: str) -> str:
    # Problem: New Azure connection on EVERY call
    cosmos_client = CosmosClient(endpoint, key)  # ~1-2 seconds
    openai_client = AzureOpenAI(endpoint, key)   # ~1-2 seconds
    # Tool logic...
    return result
    # Total: 2-4 seconds per tool call
```

With multiple agents making frequent tool calls, this becomes unsustainable:
- **10 agents × 5 tools/min × 3 seconds = 2.5 minutes of blocking time per minute**
- **Memory usage**: Each agent maintains separate Azure connections
- **Resource contention**: Database connection pool exhaustion

### Custom MCP Solution Architecture

Our implementation solves these issues through **shared resource management**:

```python
# Our MCP approach - FAST in production  
class MCPServer:
    def __init__(self):
        # Initialize connections ONCE per server
        self.cosmos_client = CosmosClient(endpoint, key)  # Cached
        self.openai_client = AzureOpenAI(endpoint, key)   # Cached
        
    def bank_balance(self, account_number: str) -> str:
        # Reuse existing connections
        # Tool logic with cached clients...
        return result
        # Total: 40-80ms per tool call
```

### Architecture Comparison

#### **LangChain @tool Functions Architecture**
```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   Agent 1   │    │   Agent 2   │    │   Agent 3   │
│ ┌─────────┐ │    │ ┌─────────┐ │    │ ┌─────────┐ │
│ │ Tools   │ │    │ │ Tools   │ │    │ │ Tools   │ │
│ │ Azure   │ │    │ │ Azure   │ │    │ │ Azure   │ │
│ │ Conns   │ │    │ │ Conns   │ │    │ │ Conns   │ │
│ └─────────┘ │    │ └─────────┘ │    │ └─────────┘ │
└─────────────┘    └─────────────┘    └─────────────┘
      ↓                   ↓                   ↓
    Cosmos              Cosmos              Cosmos
   (3 conns)           (3 conns)           (3 conns)

Problems: 9 total connections, repeated initialization
```

#### **Local MCP Architecture**  
```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   Agent 1   │    │   Agent 2   │    │   Agent 3   │
│ ┌─────────┐ │    │ ┌─────────┐ │    │ ┌─────────┐ │
│ │MCPClient│ │    │ │MCPClient│ │    │ │MCPClient│ │
│ └─────────┘ │    │ └─────────┘ │    │ └─────────┘ │
└─────┬───────┘    └─────┬───────┘    └─────┬───────┘
      │                  │                  │
      └─────────┬────────┴─────────┬────────┘
                │      stdio       │
         ┌─────────────────────────────────┐
         │      Shared Local MCP Server    │
         │ ┌─────────┐  ┌─────────┐        │
         │ │ Tools   │  │ Azure   │        │
         │ │ (11)    │  │ Conns   │        │
         │ └─────────┘  └─────────┘        │
         └─────────────────┬───────────────┘
                           ↓
                        Cosmos
                       (1 conn)

Benefits: 1 total connection, shared initialization
```

#### **Remote MCP Architecture**
```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│ Banking     │    │ Banking     │    │ Other       │
│ App 1       │    │ App 2       │    │ App N       │
│ ┌─────────┐ │    │ ┌─────────┐ │    │ ┌─────────┐ │
│ │MCPClient│ │    │ │MCPClient│ │    │ │MCPClient│ │
│ └─────────┘ │    │ └─────────┘ │    │ └─────────┘ │
└─────┬───────┘    └─────┬───────┘    └─────┬───────┘
      │                  │                  │
      └─────────┬────────┴─────────┬────────┘
                │    HTTP/JWT      │
      ┌─────────────────────────────────────────┐
      │         Remote MCP Server               │
      │ ┌─────────┐  ┌─────────┐  ┌─────────┐   │
      │ │ Tools   │  │ Azure   │  │Security │   │
      │ │ (11)    │  │ Pool    │  │& Audit  │   │
      │ └─────────┘  └─────────┘  └─────────┘   │
      └─────────────────┬───────────────────────┘
                        ↓
                   Azure Services
                  (Optimized Pool)

Benefits: Multi-client, enterprise security, horizontal scaling
```

### Performance Implications

### Architecture Benefits vs Performance Impact

| Metric | LangChain @tool Functions | Local MCP | Remote MCP |
|--------|-------------|-----------|------------|
| **Tool Call Latency** | ~10-50ms (baseline) | ~20-80ms (+protocol overhead) | ~50-150ms (+HTTP overhead) |
| **Development Coupling** | Tight (shared codebase) | Loose (process boundaries) | Very Loose (network boundaries) |
| **Team Independence** | Low (coordinated releases) | Medium (some coordination) | High (independent deployments) |
| **Technology Flexibility** | Python + LangChain only | Server can use any language | Complete technology independence |
| **Tool Reusability** | Application-specific | Limited reusability | Full ecosystem compatibility |
| **Operational Complexity** | Simple (single process) | Moderate (process management) | Complex (distributed system) |
| **Ecosystem Compatibility** | None | MCP-compatible clients only | Full MCP ecosystem |

### When Each Approach Makes Sense

**LangChain @tool Functions** are appropriate when:
- Building prototypes, demos, or simple applications
- Single team owns both AI logic and domain tools
- Performance is the absolute top priority
- Simple deployment requirements (single process)
- No need for tool reusability across different AI systems

**Local MCP** is ideal when:
- Want loose coupling without operational complexity
- Multiple teams work on the same application
- Need protocol standardization within your organization
- Tools may be reused by other internal applications
- Acceptable to trade slight performance for better architecture

**Remote MCP** is required when:
- Building enterprise systems with multiple AI applications
- Different teams own AI and domain logic
- Need independent deployment and scaling of tools
- Want ecosystem interoperability with other MCP-compatible systems
- Willing to accept operational complexity for maximum architectural benefits
- Building microservices architecture

### Performance vs Architecture Trade-off

This implementation represents a classic engineering decision:

- **Performance**: LangChain tools are typically 10-50ms faster per call
- **Architecture**: MCP provides loose coupling, standardization, and maintainability
- **Strategic Choice**: Accept performance overhead for long-term architectural benefits
- **Ecosystem Benefits**: Compatibility with growing MCP ecosystem (like choosing USB-C over proprietary connectors)

### When Each Approach Makes Sense

**LangChain @tool Functions** are appropriate when:
- Prototyping or educational environments
- Tool calls are rare (<1 per minute) so connection overhead is acceptable
- Development simplicity is the top priority
- Very simple tools that don't require external service connections

**Local MCP** is ideal when:
- Need to eliminate connection overhead while maintaining simplicity
- Want MCP benefits without operational overhead  
- Have 1-20 concurrent agents making frequent tool calls
- Single-tenant deployment with moderate performance requirements

**Remote MCP** is required when:
- Multiple applications share banking tools
- Need enterprise security (JWT, audit trails)
- Require horizontal scaling (50+ agents)
- Building microservices architecture
- Multi-tenant SaaS deployment with high performance requirements

## Activity 2: Implementing MCP Client-Server Architecture

In this activity, we'll examine the custom MCP architecture that provides the architectural benefits identified in Activity 1. This is a **code review and explanation** activity - you'll implement the actual files in Activity 3.

### Step 1: Understanding MCP Client Architecture (`mcp_client.py`)

Let's examine the MCP client architecture that will be implemented in `src/app/tools/mcp_client.py`. This client implements our dual-mode MCP architecture:

> **Note**: This section explains the architecture and key code patterns. The implementation begins later in this activity.

#### **Key Architecture Decisions:**

**1. Dual Mode Support**
```python
class EnhancedMCPClient:
    def __init__(self, use_remote_server=False):
        self.use_remote_server = use_remote_server
        if use_remote_server:
            self._init_remote_client()  # HTTP-based
        else:
            self._init_local_client()   # Process-based
```

**2. Connection Caching Strategy**  
```python
# Local Mode: Shared subprocess with cached connections
self.shared_server_process = subprocess.Popen([...])
self.stdio_transport = StdioClientTransport(...)

# Remote Mode: HTTP session with connection pooling  
self.session = requests.Session()
self.session.headers.update({"Authorization": f"Bearer {jwt_token}"})
```

**3. Context Injection**
```python
def call_mcp_tool(self, tool_name, arguments, context):
    # Automatically inject banking context
    enriched_args = {
        **arguments,
        "tenantId": context.get("tenantId", "Contoso"),
        "userId": context.get("userId", "Mark"),  
        "thread_id": context.get("thread_id", "hardcoded-thread-id-01")
    }
```

**Critical Implementation Details:**
- **Process Management**: Local mode manages MCP server subprocess lifecycle
- **Error Recovery**: Automatic reconnection for both Local and Remote modes
- **Performance Monitoring**: Built-in timing and logging for optimization
- **Security**: JWT authentication and multi-tenant isolation for Remote mode

### Step 2: Understanding MCP Server Architecture (`mcp_server.py`)

Let's examine the MCP server architecture that will be implemented in `src/app/tools/mcp_server.py`. This server implements the cached resource pattern:

> **Note**: This section explains the server architecture and key patterns. The implementation begins later in this activity.

#### **Resource Caching Strategy**

The critical performance improvement comes from connection caching:

```python
class MCPServer:
    def __init__(self):
        # Initialize Azure connections ONCE per server instance
        self._init_cosmos_client()  # Cached Cosmos DB connection
        self._init_openai_client()  # Cached OpenAI connection
        self._init_logging()        # Centralized logging
        
    async def bank_balance(self, arguments):
        # Reuse cached connections - NO initialization overhead
        account_number = arguments["account_number"]
        tenant_id = arguments["tenantId"]
        
        # Fast query using cached client
        query = f"SELECT * FROM c WHERE c.accountNumber = '{account_number}'"
        items = list(self.cosmos_client.query_items(
            query=query, enable_cross_partition_query=True))
        
        return {"result": f"Balance: ${items[0]['balance']:.2f}"}
```

**vs Native Approach (slow):**
```python
# Native tool - reconnects every time
def bank_balance(account_number: str):
    # NEW connection on every call - 1-2 seconds overhead
    cosmos_client = CosmosClient(endpoint, credential)
    # Tool logic...
    # Connection destroyed after function exits
```

#### **Tool Implementation Strategy**

Our MCP server implements 11 banking tools with these optimizations:

**1. Connection Reuse**
- Single Cosmos DB client shared across all tools
- Single OpenAI client for AI-powered operations
- Persistent connections eliminate connection setup time

**2. Context-Aware Operations**  
- All tools receive `tenantId`, `userId`, `thread_id`
- Multi-tenant data isolation at the database query level
- Audit trail generation for compliance

**3. Error Handling**
- Graceful degradation when Azure services are unavailable
- Retry logic with exponential backoff
- Detailed error logging for production debugging

**Banking Tools Implemented:**
- **Account Operations**: `bank_balance`, `create_account`
- **Transaction Operations**: `bank_transfer`, `get_transaction_history`
- **Customer Service**: `service_request`, `get_branch_location`
- **Financial Calculations**: `calculate_monthly_payment`
- **Marketing**: `get_offer_information`
- **Agent Routing**: `transfer_to_*_agent` (3 tools)

### Why This Custom Implementation Was Necessary

Standard MCP libraries (`mcp` package) don't provide:
- **Connection caching** across tool calls
- **Process management** for embedded servers
- **Context injection** for multi-tenant applications
- **Performance monitoring** and optimization
- **Dual deployment modes** (Local/Remote)

Our implementation fills these gaps, delivering production-ready MCP for banking applications.

> **Ready to implement?** In the next activity, you'll copy the complete MCP client and server files to your project and see this architecture in action.

### Step 1: Create MCP Client

Your current `tools` folder has empty `mcp_client.py` and `mcp_server.py` files. Let's start with the MCP client.

Open `src/app/tools/mcp_client.py` and replace the entire contents with:

```python
"""
🚀 ENHANCED MCP CLIENT - Remote and Local Server Support
This client manages connections to both Remote MCP servers (HTTP) and Local server instances
"""
import asyncio
import subprocess
import time
import os
import signal
import contextvars
import httpx
import jwt
from typing import Optional, Dict, Any, List
from langchain_mcp_adapters.client import MultiServerMCPClient
from langchain_core.tools import StructuredTool
from dotenv import load_dotenv

load_dotenv(override=False)

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

class RemoteMCPClient:
    """Remote MCP client for connecting to HTTP-based MCP servers"""
    
    def __init__(self, base_url: str = None):
        self.base_url = base_url or os.getenv("MCP_SERVER_ENDPOINT", "http://localhost:8080")
        self.access_token = None
        self.tools_cache: Optional[List] = None
        self.http_client = httpx.AsyncClient(timeout=30.0)

    async def authenticate(self) -> bool:
        """Authenticate with the HTTP MCP server"""
        try:
            auth_data = {
                "username": os.getenv("MCP_USERNAME", "banking_user"),
                "password": os.getenv("MCP_PASSWORD", "secure_password")
            }
            
            response = await self.http_client.post(f"{self.base_url}/auth/login", json=auth_data)
            response.raise_for_status()
            
            auth_result = response.json()
            if auth_result.get("success"):
                self.access_token = auth_result.get("access_token")
                print("🔐 REMOTE MCP: Successfully authenticated")
                return True
            else:
                print(f"❌ REMOTE MCP: Authentication failed: {auth_result.get('error')}")
                return False
                
        except Exception as e:
            print(f"❌ REMOTE MCP: Authentication error: {e}")
            return False

    async def connect_to_server(self) -> bool:
        """Connect to the remote MCP HTTP server"""
        print(f"🌐 REMOTE MCP: Connecting to {self.base_url}")
        
        try:
            # Test connection with health check
            response = await self.http_client.get(f"{self.base_url}/health")
            response.raise_for_status()
            
            health_data = response.json()
            print(f"✅ REMOTE MCP: Server health check passed - {health_data.get('status', 'unknown')}")
            
            # Authenticate
            if not await self.authenticate():
                return False
            
            # Get available tools
            headers = {"Authorization": f"Bearer {self.access_token}"}
            tools_response = await self.http_client.get(f"{self.base_url}/tools/list", headers=headers)
            tools_response.raise_for_status()
            
            tools_data = tools_response.json()
            if tools_data.get("success"):
                self.tools_cache = tools_data.get("tools", [])
                print(f"🔧 REMOTE MCP: Retrieved {len(self.tools_cache)} tools:")
                for tool in self.tools_cache:
                    print(f"   - {tool.get('name', 'unknown')}")
                
                return True
                
        except Exception as e:
            print(f"❌ REMOTE MCP: Failed to connect to server: {e}")
            return False
    
    async def call_tool(self, tool_name: str, arguments: Dict[str, Any]) -> Any:
        """Call a tool via HTTP API"""
        if not self.access_token:
            if not await self.authenticate():
                raise Exception("Could not authenticate with HTTP MCP server")
        
        call_start = time.time()
        print(f"📞 REMOTE MCP: Calling tool '{tool_name}' via HTTP")
        
        # Inject context information
        context = get_mcp_context()
        
        request_data = {
            "tool_name": tool_name,
            "arguments": arguments,
            "tenant_id": context.get('tenantId'),
            "user_id": context.get('userId'),
            "thread_id": context.get('thread_id')
        }
        
        try:
            headers = {"Authorization": f"Bearer {self.access_token}"}
            response = await self.http_client.post(
                f"{self.base_url}/tools/call",
                json=request_data,
                headers=headers
            )
            response.raise_for_status()
            
            result_data = response.json()
            call_time = (time.time() - call_start) * 1000
            
            if result_data.get("success"):
                print(f"✅ REMOTE MCP: Tool call completed in {call_time:.2f}ms")
                return result_data.get("result")
            else:
                error_msg = result_data.get("error", "Unknown error")
                print(f"❌ REMOTE MCP: Tool call failed in {call_time:.2f}ms: {error_msg}")
                raise Exception(f"Tool call failed: {error_msg}")
                
        except httpx.HTTPStatusError as e:
            call_time = (time.time() - call_start) * 1000
            print(f"❌ REMOTE MCP: HTTP error in {call_time:.2f}ms: {e.response.status_code}")
            raise
        except Exception as e:
            call_time = (time.time() - call_start) * 1000
            print(f"❌ REMOTE MCP: Tool call error in {call_time:.2f}ms: {e}")
            raise

class SharedMCPClient:
    """Enhanced MCP client supporting both Remote (HTTP) and Local (embedded) servers"""
    
    def __init__(self, use_remote: bool = None):
        # Determine mode from environment or parameter
        if use_remote is None:
            use_remote = os.getenv("USE_REMOTE_MCP_SERVER", "false").lower() == "true"
        
        self.use_remote = use_remote
        self.remote_client: Optional[RemoteMCPClient] = None
        self.local_client: Optional[MultiServerMCPClient] = None
        self.server_process = None
        self.server_ready = False
        self.server_start_time = None
        
        print(f"🔧 ENHANCED MCP: Initializing in {'REMOTE' if use_remote else 'LOCAL'} mode")

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
        """Connect to either HTTP or direct server based on configuration"""
        if self.use_remote:
            print("🌐 ENHANCED MCP: Using Remote MCP server (HTTP)")
            self.remote_client = RemoteMCPClient()
            return await self.remote_client.connect_to_server()
        else:
            print("🏠 ENHANCED MCP: Using Local MCP server (embedded)")
            
            # Start the shared server process
            if not await self.start_shared_server():
                return False
            
            try:
                # Connect to the shared server via stdio
                print("🔄 ENHANCED MCP: Connecting to local shared server...")
                
                server_config = {
                    "name": "shared_banking_server",
                    "command": "python3",
                    "args": [os.path.join(os.path.dirname(__file__), "mcp_server.py")],
                    "cwd": os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..")),
                }
                
                self.local_client = MultiServerMCPClient()
                await self.local_client.add_server(server_config)
                
                print("✅ ENHANCED MCP: Successfully connected to local server")
                return True
                
            except Exception as e:
                print(f"❌ ENHANCED MCP: Failed to connect to local server: {e}")
                return False

    async def get_langchain_tools(self) -> List[StructuredTool]:
        """Get LangChain-compatible tools from the connected MCP server"""
        if self.use_remote:
            if not self.remote_client or not self.remote_client.tools_cache:
                raise Exception("Remote MCP client not connected or no tools available")
            
            # Convert remote tools to LangChain tools
            langchain_tools = []
            
            for tool_info in self.remote_client.tools_cache:
                tool_name = tool_info.get("name")
                if not tool_name:
                    continue
                    
                # Create a wrapper function for the remote tool call
                def create_tool_executor(captured_tool_name):
                    async def tool_executor(*args, **kwargs):
                        call_start = time.time()
                        print(f"🔧 REMOTE MCP TOOL: Executing {captured_tool_name}")
                        print(f"🔧 DEBUG: args={args}, kwargs={kwargs}")
                        
                        # Handle different argument patterns from LangGraph
                        if captured_tool_name == "bank_balance":
                            if args and not kwargs:
                                # Convert positional args to named parameters
                                if len(args) >= 1:
                                    kwargs["account_number"] = args[0]
                                context = get_mcp_context()
                                kwargs.update({
                                    "tenantId": context.get('tenantId'),
                                    "userId": context.get('userId')
                                })
                                print(f"🔧 DEBUG: Fixed bank_balance args - account_number: {kwargs.get('account_number')}")
                        
                        # Additional argument handling patterns...
                        
                        try:
                            result = await self.remote_client.call_tool(captured_tool_name, kwargs)
                            call_time = (time.time() - call_start) * 1000
                            print(f"✅ REMOTE MCP TOOL: {captured_tool_name} completed in {call_time:.2f}ms")
                            return result
                        except Exception as e:
                            call_time = (time.time() - call_start) * 1000
                            print(f"❌ REMOTE MCP TOOL: {captured_tool_name} failed in {call_time:.2f}ms: {e}")
                            raise
                    
                    return tool_executor
                
                # Create StructuredTool for each remote tool
                langchain_tool = StructuredTool.from_function(
                    func=create_tool_executor(tool_name),
                    name=tool_name,
                    description=tool_info.get("description", f"Execute {tool_name} via Remote MCP"),
                    return_direct=False
                )
                
                langchain_tools.append(langchain_tool)
            
            print(f"🔧 ENHANCED MCP: Converted {len(langchain_tools)} remote tools to LangChain format")
            return langchain_tools
        
        else:
            if not self.local_client:
                raise Exception("Local MCP client not connected")
            
            try:
                # Get tools from local MCP server
                langchain_tools = await self.local_client.get_langchain_tools()
                
                # Wrap tools with performance monitoring and context injection
                enhanced_tools = []
                
                for tool in langchain_tools:
                    original_func = tool.func
                    
                    def create_enhanced_wrapper(original_tool_name, original_tool_func):
                        async def enhanced_wrapper(*args, **kwargs):
                            call_start = time.time()
                            print(f"📞 LOCAL MCP: Calling tool '{original_tool_name}' via shared server")
                            print(f"🔧 DEBUG: Tool arguments: {kwargs}")
                            
                            # Inject context automatically
                            context = get_mcp_context()
                            if context.get('tenantId') and 'tenantId' not in kwargs:
                                kwargs['tenantId'] = context.get('tenantId')
                            if context.get('userId') and 'userId' not in kwargs:
                                kwargs['userId'] = context.get('userId')
                            if context.get('thread_id') and 'thread_id' not in kwargs:
                                kwargs['thread_id'] = context.get('thread_id')
                            
                            try:
                                result = await original_tool_func(*args, **kwargs)
                                call_time = (time.time() - call_start) * 1000
                                print(f"✅ LOCAL MCP: Tool call completed in {call_time:.2f}ms")
                                return result
                            except Exception as e:
                                call_time = (time.time() - call_start) * 1000
                                print(f"❌ LOCAL MCP: Tool call failed in {call_time:.2f}ms: {e}")
                                raise
                        
                        return enhanced_wrapper
                    
                    # Create enhanced tool with monitoring
                    enhanced_tool = StructuredTool.from_function(
                        func=create_enhanced_wrapper(tool.name, original_func),
                        name=tool.name,
                        description=tool.description,
                        return_direct=False
                    )
                    
                    enhanced_tools.append(enhanced_tool)
                
                print(f"🔧 ENHANCED MCP: Enhanced {len(enhanced_tools)} local tools with monitoring")
                return enhanced_tools
                
            except Exception as e:
                print(f"❌ ENHANCED MCP: Error getting tools from local server: {e}")
                raise

    def cleanup(self):
        """Clean up resources"""
        if self.server_process:
            try:
                # Terminate the process group (includes all child processes)
                os.killpg(os.getpgid(self.server_process.pid), signal.SIGTERM)
                
                # Wait for graceful shutdown
                try:
                    self.server_process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    # Force kill if it doesn't shut down gracefully
                    os.killpg(os.getpgid(self.server_process.pid), signal.SIGKILL)
                
                print("🔄 ENHANCED MCP: Shared server stopped")
                
            except Exception as e:
                print(f"⚠️ ENHANCED MCP: Error stopping server: {e}")
        
        if self.local_client:
            try:
                asyncio.create_task(self.local_client.close())
            except Exception as e:
                print(f"⚠️ ENHANCED MCP: Error closing local client: {e}")

# Global client instance
_global_mcp_client: Optional[SharedMCPClient] = None

async def get_mcp_client() -> SharedMCPClient:
    """Get or create the global MCP client"""
    global _global_mcp_client
    
    if _global_mcp_client is None:
        _global_mcp_client = SharedMCPClient()
        success = await _global_mcp_client.connect_to_server()
        if not success:
            raise Exception("Failed to connect to MCP server")
    
    return _global_mcp_client

# Cleanup function for graceful shutdown
import atexit

def cleanup_mcp():
    """Cleanup function called on exit"""
    global _global_mcp_client
    if _global_mcp_client:
        _global_mcp_client.cleanup()

atexit.register(cleanup_mcp)
```

### Step 2: Create MCP Server

Now open `src/app/tools/mcp_server.py` and replace the entire contents with the complete MCP server code.

> **Note**: The MCP server is quite large (1400+ lines) and includes all banking tools, Azure service connections, and performance optimizations. For this workshop, the complete file will be provided. Here's an overview of what it contains:

**MCP Server Features**:
- **11 Banking Tools**: balance, transfer, history, account creation, service requests, etc.
- **Agent Transfer Tools**: Seamless handoff between coordinator, customer support, transactions, and sales agents
- **Azure Integration**: Cached connections to Cosmos DB and OpenAI for optimal performance  
- **Multi-tenant Security**: All operations require tenantId and userId
- **Performance Monitoring**: Execution timing and logging for all operations
- **Dual Mode Support**: Can run embedded (local) or standalone (HTTP server)

**Copy the complete MCP server code** from the current project files into your `src/app/tools/mcp_server.py` file.

The server includes these key banking tools:
```python
# Sample of key tools (full implementation in complete file)
@mcp.tool()
def bank_balance(account_number: str, tenantId: str, userId: str) -> dict:
    """Get account balance for a customer"""
    
@mcp.tool()
def bank_transfer(fromAccount: str, toAccount: str, amount: float, 
                 tenantId: str, userId: str, thread_id: str) -> dict:
    """Transfer money between accounts"""
    
@mcp.tool()
def create_account(account_holder: str, balance: float, 
                  tenantId: str, userId: str) -> dict:
    """Create a new bank account"""

@mcp.tool()
def get_transaction_history(account_number: str, start_date: str, end_date: str,
                           tenantId: str, userId: str) -> dict:
    """Get transaction history for an account"""

# Plus 7 more banking and agent transfer tools...
```

## Activity 3: Replace Banking Agent Files

Now we'll replace your existing banking agent files with MCP-enabled versions.

### Step 1: Replace banking_agents.py

Open `src/app/banking_agents.py` and replace the **entire contents** with the following MCP-enabled version:

<details>
<summary><strong>Complete banking_agents.py code (click to expand)</strong></summary>

```python
import logging
import os
import sys
import time
import uuid
import asyncio
import json
from langchain_core.messages import ToolMessage, SystemMessage
from langchain.schema import AIMessage
from langchain_mcp_adapters.client import MultiServerMCPClient
from typing import Literal, Optional, List
from langgraph.graph import StateGraph, START, MessagesState
from langgraph.prebuilt import create_react_agent
from langgraph.types import Command, interrupt
from langgraph_checkpoint_cosmosdb import CosmosDBSaver
from langgraph.checkpoint.memory import MemorySaver
from langsmith import traceable
from src.app.services.azure_open_ai import model
from src.app.services.azure_cosmos_db import DATABASE_NAME, checkpoint_container, chat_container, \
    update_chat_container, patch_active_agent

# 🔄 Global persistent MCP client and cache
_persistent_mcp_client: Optional[MultiServerMCPClient] = None
_mcp_tools_cache: Optional[List] = None
_native_tools_fallback_enabled = False  # 🚀 Using shared MCP server for optimal performance
_shared_mcp_client = None  # 🚀 Enhanced shared client

# 🔧 Tool version tracking for cache invalidation
import time
_module_load_time = time.time()
_agents_setup_version = None
_last_setup_time = None

print(f"🔧 MODULE LOAD: banking_agents module loaded at {_module_load_time}")

local_interactive_mode = False

logging.basicConfig(level=logging.DEBUG)

PROMPT_DIR = os.path.join(os.path.dirname(__file__), 'prompts')


def load_prompt(agent_name):
    file_path = os.path.join(PROMPT_DIR, f"{agent_name}.prompty")
    print(f"Loading prompt for {agent_name} from {file_path}")
    try:
        with open(file_path, "r", encoding="utf-8") as file:
            return file.read().strip()
    except FileNotFoundError:
        print(f"Prompt file not found for {agent_name}, using default placeholder.")
        return "You are an AI banking assistant."


# Tool filtering utility
def filter_tools_by_prefix(tools, prefixes):
    filtered = []
    for tool in tools:
        # Handle both dict and object formats for compatibility
        if isinstance(tool, dict):
            tool_name = tool.get('name', '')
        else:
            tool_name = getattr(tool, 'name', '')
        
        if any(tool_name.startswith(prefix) for prefix in prefixes):
            filtered.append(tool)
    return filtered


async def get_persistent_mcp_client():
    """Get or create a persistent MCP client that is reused across all tool calls"""
    global _persistent_mcp_client, _mcp_tools_cache, _shared_mcp_client
    
    if _persistent_mcp_client is None:
        print("🔧 MCP CLIENT: Creating new MCP client and tools")
        print("🔄 Initializing SHARED MCP client (high-performance setup)...")
        start_time = time.time()
        
        try:
            # 🚀 Use the new shared MCP client for optimal performance
            from src.app.tools.mcp_client import get_shared_mcp_client, set_mcp_context, get_mcp_context
            _shared_mcp_client = await get_shared_mcp_client()
            
            # Get tools from shared client
            _mcp_tools_cache = await _shared_mcp_client.get_tools()
            
            # For compatibility, create a wrapper that looks like MultiServerMCPClient
            class SharedMCPClientWrapper:
                def __init__(self, shared_client):
                    self.shared_client = shared_client
                
                async def get_tools(self):
                    return await self.shared_client.get_tools()
                
                async def call_tool(self, tool_name: str, arguments: dict):
                    return await self.shared_client.call_tool(tool_name, arguments)
            
            _persistent_mcp_client = SharedMCPClientWrapper(_shared_mcp_client)
            
            setup_time = (time.time() - start_time) * 1000
            print(f"✅ SHARED MCP CLIENT: Setup completed in {setup_time:.2f}ms")
            
            if _mcp_tools_cache:
                print(f"✅ MCP TOOLS: Loaded {len(_mcp_tools_cache)} tools from shared server:")
                for tool in _mcp_tools_cache[:5]:  # Show first 5
                    tool_name = tool.name if hasattr(tool, 'name') else tool.get('name', 'unknown')
                    print(f"   - {tool_name}")
                if len(_mcp_tools_cache) > 5:
                    print(f"   ... and {len(_mcp_tools_cache) - 5} more tools")
        
        except Exception as e:
            print(f"❌ SHARED MCP CLIENT ERROR: {e}")
            if _native_tools_fallback_enabled:
                print("🔄 FALLBACK: Switching to native tools due to MCP client error")
                _persistent_mcp_client = None
                _mcp_tools_cache = None
            else:
                print("❌ CRITICAL: MCP client initialization failed and fallback is disabled")
                raise e
    
    return _persistent_mcp_client, _mcp_tools_cache


# Define agent helper functions
def get_coordinator_tools():
    """Get tools specific to the coordinator agent"""
    try:
        client, tools = asyncio.run(get_persistent_mcp_client())
        if tools:
            # Coordinator gets all transfer tools to route between agents
            return filter_tools_by_prefix(tools, ['transfer_to_'])
        return []
    except Exception as e:
        print(f"❌ ERROR getting coordinator tools: {e}")
        return []


def get_customer_support_tools():
    """Get tools specific to customer support agent"""
    try:
        client, tools = asyncio.run(get_persistent_mcp_client())
        if tools:
            # Customer support gets balance, service requests, and branch info
            return filter_tools_by_prefix(tools, ['bank_balance', 'service_request', 'get_branch_location', 'transfer_to_'])
        return []
    except Exception as e:
        print(f"❌ ERROR getting customer support tools: {e}")
        return []


def get_transactions_tools():
    """Get tools specific to transactions agent"""
    try:
        client, tools = asyncio.run(get_persistent_mcp_client())
        if tools:
            # Transactions agent gets transfer and history tools
            return filter_tools_by_prefix(tools, ['bank_transfer', 'get_transaction_history', 'transfer_to_'])
        return []
    except Exception as e:
        print(f"❌ ERROR getting transactions tools: {e}")
        return []


def get_sales_tools():
    """Get tools specific to sales agent"""
    try:
        client, tools = asyncio.run(get_persistent_mcp_client())
        if tools:
            # Sales agent gets account creation, offers, and loan calculation tools
            return filter_tools_by_prefix(tools, ['create_account', 'get_offer_information', 'calculate_monthly_payment', 'transfer_to_'])
        return []
    except Exception as e:
        print(f"❌ ERROR getting sales tools: {e}")
        return []


# Set up agents with MCP tools
async def setup_agents():
    """Initialize all agents with MCP tools"""
    global _agents_setup_version, _last_setup_time
    
    current_time = time.time()
    print(f"🚀 AGENT SETUP: Starting agent initialization at {current_time}")
    
    try:
        # Initialize MCP client first
        client, tools = await get_persistent_mcp_client()
        
        if not tools:
            print("❌ AGENT SETUP: No MCP tools available, cannot set up agents")
            return None
        
        print(f"✅ AGENT SETUP: Using {len(tools)} MCP tools for agent initialization")
        
        # Set up context for banking operations
        from src.app.tools.mcp_client import set_mcp_context
        set_mcp_context(
            tenantId="Contoso",
            userId="Mark",
            thread_id="hardcoded-thread-id-01"
        )
        
        # Create agents with their specific tool sets
        coordinator_agent = create_react_agent(
            model,
            tools=get_coordinator_tools(),
            state_modifier=load_prompt("coordinator_agent"),
        )
        
        customer_support_agent = create_react_agent(
            model,
            tools=get_customer_support_tools(),
            state_modifier=load_prompt("customer_support_agent"),
        )
        
        transactions_agent = create_react_agent(
            model,
            tools=get_transactions_tools(),
            state_modifier=load_prompt("transactions_agent"),
        )
        
        sales_agent = create_react_agent(
            model,
            tools=get_sales_tools(),
            state_modifier=load_prompt("sales_agent"),
        )
        
        # Define the router function
        def route_to_agent(state: MessagesState) -> Literal["coordinator_agent", "customer_support_agent", "transactions_agent", "sales_agent"]:
            """Route to the appropriate agent based on the last message"""
            last_message = state['messages'][-1]
            
            # Check for specific routing commands in the message
            if isinstance(last_message, ToolMessage):
                if "transfer_to_customer_support_agent" in str(last_message):
                    return "customer_support_agent"
                elif "transfer_to_transactions_agent" in str(last_message):
                    return "transactions_agent"
                elif "transfer_to_sales_agent" in str(last_message)
                    return "sales_agent"
            
            # Default to coordinator for user messages
            return "coordinator_agent"
        
        # Build the state graph
        workflow = StateGraph(MessagesState)
        
        # Add nodes
        workflow.add_node("coordinator_agent", coordinator_agent)
        workflow.add_node("customer_support_agent", customer_support_agent)
        workflow.add_node("transactions_agent", transactions_agent)
        workflow.add_node("sales_agent", sales_agent)
        
        # Add edges
        workflow.add_edge(START, "coordinator_agent")
        workflow.add_conditional_edges(
            "coordinator_agent",
            route_to_agent,
            {
                "coordinator_agent": "coordinator_agent",
                "customer_support_agent": "customer_support_agent",
                "transactions_agent": "transactions_agent",
                "sales_agent": "sales_agent"
            }
        )
        
        # Set up checkpointer for conversation persistence
        checkpointer = CosmosDBSaver(
            database_name=DATABASE_NAME,
            container_name=checkpoint_container.id
        )
        
        # Compile the graph
        graph = workflow.compile(checkpointer=checkpointer)
        
        _agents_setup_version = current_time
        _last_setup_time = current_time
        
        setup_time = (time.time() - current_time) * 1000
        print(f"✅ AGENT SETUP: All agents initialized successfully in {setup_time:.2f}ms")
        
        return graph
        
    except Exception as e:
        print(f"❌ AGENT SETUP ERROR: {e}")
        import traceback
        traceback.print_exc()
        return None


# Initialize graph and checkpointer
graph = None
checkpointer = None


async def initialize_banking_system():
    """Initialize the complete banking system with MCP integration"""
    global graph, checkpointer
    
    print("🚀 Initializing MCP Banking System...")
    
    try:
        # Set up agents with MCP tools
        graph = await setup_agents()
        
        if graph is None:
            raise Exception("Failed to initialize agents")
        
        # Set up checkpointer
        checkpointer = CosmosDBSaver(
            database_name=DATABASE_NAME,
            container_name=checkpoint_container.id
        )
        
        print("✅ MCP Banking System initialized successfully!")
        return True
        
    except Exception as e:
        print(f"❌ Banking System initialization failed: {e}")
        return False


def run_banking_conversation():
    """Run the interactive banking conversation"""
    global graph, local_interactive_mode
    
    if graph is None:
        print("❌ Banking system not initialized. Please run initialize_banking_system() first.")
        return
    
    # Set interactive mode flag
    local_interactive_mode = True
    
    print("\n" + "="*60)
    print("🏦 Welcome to the MCP-Enabled Banking Assistant!")
    print("🔧 Now powered by Model Context Protocol")
    print("="*60)
    print("\nAvailable agents:")
    print("  • Customer Support (balance, service requests)")
    print("  • Transactions (transfers, history)")
    print("  • Sales (new accounts, loans)")
    print("\nType 'exit' to end the conversation.\n")
    
    config = {
        "configurable": {
            "thread_id": "hardcoded-thread-id-01",
            "tenantId": "Contoso",
            "userId": "Mark",
        }
    }
    
    try:
        while True:
            user_input = input("You: ").strip()
            
            if user_input.lower() in ['exit', 'quit', 'bye']:
                print("👋 Thank you for using our banking system!")
                break
            
            if not user_input:
                continue
            
            # Process the message through the agent graph
            try:
                messages = [{"role": "user", "content": user_input}]
                
                print("\n🤔 Processing your request...")
                
                # Stream the response
                for event in graph.stream({"messages": messages}, config, stream_mode="values"):
                    if "messages" in event:
                        last_message = event["messages"][-1]
                        if hasattr(last_message, 'content') and last_message.content:
                            # Only print if it's a new AI response
                            if last_message.content != user_input:
                                print(f"\nAgent: {last_message.content}\n")
                        
            except KeyboardInterrupt:
                print("\n\n⏹️  Conversation interrupted by user.")
                break
            except Exception as e:
                print(f"\n❌ Error processing request: {e}")
                print("Please try again.\n")
                
    except KeyboardInterrupt:
        print("\n\n👋 Goodbye!")
    except Exception as e:
        print(f"\n❌ Conversation error: {e}")


if __name__ == "__main__":
    print("🔧 Starting banking agents module...")
    
    # Initialize the system
    async def main():
        success = await initialize_banking_system()
        if success:
            run_banking_conversation()
        else:
            print("❌ Failed to initialize banking system")
    
    # Run the main function
    asyncio.run(main())
```
</details>

### Step 2: Replace banking_agents_api.py  

Open `src/app/banking_agents_api.py` and replace the **entire contents** with the following MCP-enabled version:

<details>
<summary><strong>Complete banking_agents_api.py code (click to expand)</strong></summary>

```python
import os
import uuid
import fastapi

from dotenv import load_dotenv

from datetime import datetime
from fastapi import BackgroundTasks
from azure.monitor.opentelemetry import configure_azure_monitor


from azure.cosmos.exceptions import CosmosHttpResponseError

from fastapi import Depends, HTTPException, Body
from langchain_core.messages import HumanMessage, ToolMessage
from pydantic import BaseModel
from typing import List, Dict, Optional
from datetime import datetime
from enum import IntEnum
from src.app.services.azure_open_ai import model
from langgraph_checkpoint_cosmosdb import CosmosDBSaver
from langgraph.graph.state import CompiledStateGraph
from starlette.middleware.cors import CORSMiddleware
from src.app.banking_agents import graph, checkpointer
from src.app.tools.mcp_client import set_mcp_context  # Enhanced MCP context
from src.app.services.azure_cosmos_db import update_chat_container, patch_active_agent, \
    fetch_chat_container_by_tenant_and_user, \
    fetch_chat_container_by_session, delete_userdata_item, debug_container, update_users_container, \
    update_account_container, update_offers_container, store_chat_history, update_active_agent_in_latest_message, \
    chat_container, fetch_chat_history_by_session, delete_chat_history_by_session, \
    fetch_accounts_by_user, fetch_transactions_by_account_id, fetch_service_requests_by_tenant
import logging

import asyncio
from src.app.banking_agents import setup_agents



# Setup logging
logging.basicConfig(level=logging.ERROR)

load_dotenv(override=False)

configure_azure_monitor()


endpointTitle = "ChatEndpoints"
dataLoadTitle = "DataLoadEndpoints"

# Mapping for agent function names to standardized names
agent_mapping = {
    "coordinator_agent": "Coordinator",
    "customer_support_agent": "CustomerSupport",
    "transactions_agent": "Transactions",
    "sales_agent": "Sales"
}


def get_compiled_graph():
    return graph


app = fastapi.FastAPI(title="Cosmos DB Multi-Agent Banking API", openapi_url="/cosmos-multi-agent-api.json")

@app.on_event("startup")
async def initialize_agents():
    await setup_agents()

@app.get("/")
def health_check():
    return {"status": "MCP agent system is up"}
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


class DebugLog(BaseModel):
    id: str
    sessionId: str
    tenantId: str
    userId: str
    details: str


class Session(BaseModel):
    id: str
    type: str = "session"
    sessionId: str
    tenantId: str
    userId: str
    tokensUsed: int = 0
    name: str
    messages: List


class MessageModel(BaseModel):
    id: str
    type: str
    sessionId: str
    tenantId: str
    userId: str
    timeStamp: str
    sender: str
    senderRole: str
    text: str
    debugLogId: str
    tokensUsed: int
    rating: bool
    completionPromptId: str


class DebugLog(BaseModel):
    id: str
    messageId: str
    type: str
    sessionId: str
    tenantId: str
    userId: str
    timeStamp: str
    propertyBag: list


# Banking data models based on Swagger schema
class AccountType(IntEnum):
    CHECKING = 0
    SAVINGS = 1
    CREDIT = 2


class AccountStatus(IntEnum):
    ACTIVE = 0
    INACTIVE = 1
    SUSPENDED = 2
    CLOSED = 3
    PENDING = 4
    FROZEN = 5
    OVERDRAFT = 6
    LIMITED = 7


class CardType(IntEnum):
    DEBIT = 0
    CREDIT = 1
    PREPAID = 2
    CORPORATE = 3
    VIRTUAL = 4
    REWARDS = 5
    STUDENT = 6
    BUSINESS = 7
    PREMIUM = 8


class ServiceRequestType(IntEnum):
    COMPLAINT = 0
    FUND_TRANSFER = 1
    FULFILMENT = 2
    TELE_BANKER_CALLBACK = 3


class BankAccount(BaseModel):
    id: str
    tenantId: str
    name: str
    accountType: AccountType
    cardNumber: Optional[int] = None
    accountStatus: Optional[AccountStatus] = None
    cardType: Optional[CardType] = None
    balance: Optional[int] = None
    limit: Optional[int] = None
    interestRate: Optional[int] = None
    shortDescription: str


class BankTransaction(BaseModel):
    id: str
    tenantId: str
    accountId: str
    debitAmount: int
    creditAmount: int
    accountBalance: int
    details: str
    transactionDateTime: datetime


class ServiceRequest(BaseModel):
    id: Optional[str] = None
    tenantId: Optional[str] = None
    userId: Optional[str] = None
    type: Optional[str] = None
    requestedOn: Optional[datetime] = None
    scheduledDateTime: Optional[datetime] = None
    accountId: Optional[str] = None
    srType: Optional[ServiceRequestType] = None
    recipientEmail: Optional[str] = None
    recipientPhone: Optional[str] = None
    debitAmount: Optional[float] = None
    isComplete: bool = False
    requestAnnotations: Optional[List[str]] = None
    fulfilmentDetails: Optional[Dict[str, str]] = None


def store_debug_log(sessionId, tenantId, userId, response_data):
    """Stores detailed debug log information in Cosmos DB."""
    debug_log_id = str(uuid.uuid4())
    message_id = str(uuid.uuid4())
    timestamp = datetime.utcnow().isoformat()

    # Extract relevant debug details
    agent_selected = "Unknown"
    previous_agent = "Unknown"
    finish_reason = "Unknown"
    model_name = "Unknown"
    system_fingerprint = "Unknown"
    input_tokens = 0
    output_tokens = 0
    total_tokens = 0
    cached_tokens = 0
    transfer_success = False
    tool_calls = []
    logprobs = None
    content_filter_results = {}

    for entry in response_data:
        for agent, details in entry.items():
            if "messages" in details:
                for msg in details["messages"]:
                    if hasattr(msg, "response_metadata"):
                        metadata = getattr(msg, "response_metadata", None)
                        if metadata:
                            finish_reason = metadata.get("finish_reason", finish_reason)
                            model_name = metadata.get("model_name", model_name)
                            system_fingerprint = metadata.get("system_fingerprint", system_fingerprint)

                            token_usage = metadata.get("token_usage", {}) or {}
                            input_tokens = token_usage.get("prompt_tokens", input_tokens)
                            output_tokens = token_usage.get("completion_tokens", output_tokens)
                            total_tokens = token_usage.get("total_tokens", total_tokens)

                            prompt_details = token_usage.get("prompt_tokens_details", {}) or {}
                            cached_tokens = prompt_details.get("cached_tokens", cached_tokens)

                            logprobs = metadata.get("logprobs", logprobs)
                            content_filter_results = metadata.get("content_filter_results", content_filter_results)

                            if "tool_calls" in msg.additional_kwargs:
                                tool_calls.extend(msg.additional_kwargs["tool_calls"])
                                transfer_success = any(
                                    call.get("name", "").startswith("transfer_to_") for call in tool_calls)
                                previous_agent = agent_selected
                                agent_selected = tool_calls[-1].get("name", "").replace("transfer_to_", "") if tool_calls else agent_selected

    property_bag = [
        {"key": "agent_selected", "value": agent_selected, "timeStamp": timestamp},
        {"key": "previous_agent", "value": previous_agent, "timeStamp": timestamp},
        {"key": "finish_reason", "value": finish_reason, "timeStamp": timestamp},
        {"key": "model_name", "value": model_name, "timeStamp": timestamp},
        {"key": "system_fingerprint", "value": system_fingerprint, "timeStamp": timestamp},
        {"key": "input_tokens", "value": input_tokens, "timeStamp": timestamp},
        {"key": "output_tokens", "value": output_tokens, "timeStamp": timestamp},
        {"key": "total_tokens", "value": total_tokens, "timeStamp": timestamp},
        {"key": "cached_tokens", "value": cached_tokens, "timeStamp": timestamp},
        {"key": "transfer_success", "value": transfer_success, "timeStamp": timestamp},
        {"key": "tool_calls", "value": str(tool_calls), "timeStamp": timestamp},
        {"key": "logprobs", "value": str(logprobs), "timeStamp": timestamp},
        {"key": "content_filter_results", "value": str(content_filter_results), "timeStamp": timestamp}
    ]

    debug_entry = {
        "id": debug_log_id,
        "messageId": message_id,
        "type": "debug_log",
        "sessionId": sessionId,
        "tenantId": tenantId,
        "userId": userId,
        "timeStamp": timestamp,
        "propertyBag": property_bag
    }

    debug_container.create_item(debug_entry)
    return debug_log_id




def create_thread(tenantId: str, userId: str):
    sessionId = str(uuid.uuid4())
    name = userId
    age = 30
    address = "123 Main St"
    activeAgent = "unknown"
    ChatName = "New Chat"
    messages = []
    update_chat_container({
        "id": sessionId,
        "tenantId": tenantId,
        "userId": userId,
        "sessionId": sessionId,
        "name": name,
        "age": age,
        "address": address,
        "activeAgent": activeAgent,
        "ChatName": ChatName,
        "messages": messages
    })
    return Session(id=sessionId, sessionId=sessionId, tenantId=tenantId, userId=userId, name=name, age=age,
                   address=address, activeAgent=activeAgent, ChatName=ChatName, messages=messages)


@app.get("/status", tags=[endpointTitle], description="Gets the service status", operation_id="GetServiceStatus",
         response_description="Success",
         response_model=str)
def get_service_status():
    return "CosmosDBService: initializing"


# Note: cosmos db checkpointer store is used internally by LangGraph for "memory": to maintain end-to-end state of each
# conversation thread as contextual input to the OpenAI model.
# However, this function is dead code, as we no longer retrieve chat history from the cosmos db checkpointer store to return in the API.
# Abandoned this approach as the checkpointer store does not natively keep a record of which agent responded to the last message.
# Also, retrieving messages from the checkpointer store is not efficient as it requires scanning more records than necessary for chat history.
# Instead, we are now storing chat history in a separate custom cosmos db session container. Keeping this code for reference.
def _fetch_messages_for_session(sessionId: str, tenantId: str, userId: str) -> List[MessageModel]:
    messages = []
    config = {
        "configurable": {
            "thread_id": sessionId,
            "checkpoint_ns": ""
        }
    }

    logging.debug(f"Fetching messages for sessionId: {sessionId} with config: {config}")
    checkpoints = list(checkpointer.list(config))
    logging.debug(f"Number of checkpoints retrieved: {len(checkpoints)}")

    if checkpoints:
        last_checkpoint = checkpoints[-1]
        for key, value in last_checkpoint.checkpoint.items():
            if key == "channel_values" and "messages" in value:
                messages.extend(value["messages"])

    selected_human_index = None
    for i in range(len(messages) - 1):
        if isinstance(messages[i], HumanMessage) and not isinstance(messages[i + 1], HumanMessage):
            selected_human_index = i
            break

    messages = messages[selected_human_index:] if selected_human_index is not None else []

    return [
        MessageModel(
            id=str(uuid.uuid4()),
            type="ai_response",
            sessionId=sessionId,
            tenantId=tenantId,
            userId=userId,
            timeStamp=msg.response_metadata.get("timestamp", "") if hasattr(msg, "response_metadata") else "",
            sender="User" if isinstance(msg, HumanMessage) else "Coordinator",
            senderRole="User" if isinstance(msg, HumanMessage) else "Assistant",
            text=msg.content if hasattr(msg, "content") else msg.get("content", ""),
            debugLogId=str(uuid.uuid4()),
            tokensUsed=msg.response_metadata.get("token_usage", {}).get("total_tokens", 0) if hasattr(msg,
                                                                                                      "response_metadata") else 0,
            rating=True,
            completionPromptId=""
        )
        for msg in messages
        if msg.content
    ]


@app.get("/tenant/{tenantId}/user/{userId}/sessions",
         description="Retrieves sessions from the given tenantId and userId", tags=[endpointTitle],
         response_model=List[Session])
def get_chat_sessions(tenantId: str, userId: str):
    items = fetch_chat_container_by_tenant_and_user(tenantId, userId)
    sessions = []

    for item in items:
        sessionId = item["sessionId"]
        messages = fetch_chat_history_by_session(sessionId)

        session = {
            "id": sessionId,
            "type": "Session",
            "sessionId": sessionId,
            "tenantId": item["tenantId"],
            "userId": item["userId"],
            "tokensUsed": item.get("tokensUsed", 0),
            "name": item.get("ChatName", "New Chat"),
            "messages": messages
        }
        sessions.append(session)

    return sessions


@app.get("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/messages",
         description="Retrieves messages from the sessionId", tags=[endpointTitle], response_model=List[MessageModel])
def get_chat_session(tenantId: str, userId: str, sessionId: str):
    return fetch_chat_history_by_session(sessionId)


# to be implemented
@app.post("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/message/{messageId}/rate",
          description="Not yet implemented", tags=[endpointTitle],
          operation_id="RateMessage", response_description="Success", response_model=MessageModel)
def rate_message(tenantId: str, userId: str, sessionId: str, messageId: str, rating: bool):
    return {
        "id": messageId,
        "type": "ai_response",
        "sessionId": sessionId,
        "tenantId": tenantId,
        "userId": userId,
        "timeStamp": "2023-01-01T00:00:00Z",
        "sender": "assistant",
        "senderRole": "agent",
        "text": "This is a rated message",
        "debugLogId": str(uuid.uuid4()),
        "tokensUsed": 0,
        "rating": rating,
        "completionPromptId": ""
    }


@app.get("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/completiondetails/{debuglogId}",
         description="Retrieves debug information for chat completions", tags=[endpointTitle],
         operation_id="GetChatCompletionDetails", response_model=DebugLog)
def get_chat_completion_details(tenantId: str, userId: str, sessionId: str, debuglogId: str):
    try:
        debug_log = debug_container.read_item(item=debuglogId, partition_key=sessionId)
        return debug_log
    except Exception:
        raise HTTPException(status_code=404, detail="Debug log not found")


# create a post function that renames the ChatName in the user data container
@app.post("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/rename", description="Renames the chat session",
          tags=[endpointTitle], response_model=Session)
def rename_chat_session(tenantId: str, userId: str, sessionId: str, newChatSessionName: str):
    items = fetch_chat_container_by_session(tenantId, userId, sessionId)
    if not items:
        raise HTTPException(status_code=404, detail="Session not found")

    item = items[0]
    item["ChatName"] = newChatSessionName
    update_chat_container(item)

    return Session(id=item["sessionId"], sessionId=item["sessionId"], tenantId=item["tenantId"], userId=item["userId"],
                   name=item["ChatName"], age=item["age"],
                   address=item["address"], activeAgent=item["activeAgent"], ChatName=newChatSessionName,
                   messages=item["messages"])


def delete_all_thread_records(cosmos_saver: CosmosDBSaver, thread_id: str) -> None:
    """
    Deletes all records related to a given thread in CosmosDB by first identifying all partition keys
    and then deleting every record under each partition key.
    """

    # Step 1: Identify all partition keys related to the thread
    query = "SELECT DISTINCT c.partition_key FROM c WHERE CONTAINS(c.partition_key, @thread_id)"
    parameters = [{"name": "@thread_id", "value": thread_id}]

    partition_keys = list(cosmos_saver.container.query_items(
        query=query, parameters=parameters, enable_cross_partition_query=True
    ))

    if not partition_keys:
        print(f"No records found for thread: {thread_id}")
        return

    print(f"Found {len(partition_keys)} partition keys related to the thread.")

    # Step 2: Delete all records under each partition key
    for partition in partition_keys:
        partition_key = partition["partition_key"]

        # Query all records under the current partition
        record_query = "SELECT c.id FROM c WHERE c.partition_key=@partition_key"
        record_parameters = [{"name": "@partition_key", "value": partition_key}]

        records = list(cosmos_saver.container.query_items(
            query=record_query, parameters=record_parameters, enable_cross_partition_query=True
        ))

        for record in records:
            record_id = record["id"]
            try:
                cosmos_saver.container.delete_item(record_id, partition_key=partition_key)
                print(f"Deleted record: {record_id} from partition: {partition_key}")
            except CosmosHttpResponseError as e:
                print(f"Error deleting record {record_id} (HTTP {e.status_code}): {e.message}")

    print(f"Successfully deleted all records for thread: {thread_id}")


# deletes the session user data container and all messages in the checkpointer store
@app.delete("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}", tags=[endpointTitle], )
def delete_chat_session(tenantId: str, userId: str, sessionId: str, background_tasks: BackgroundTasks):
    delete_userdata_item(tenantId, userId, sessionId)

    # Delete all messages in the checkpointer store
    config = {
        "configurable": {
            "thread_id": sessionId,
            "checkpoint_ns": ""  # Ensure this matches the stored data
        }
    }
    delete_chat_history_by_session(sessionId)

    # Schedule the delete_all_thread_records function as a background task
    background_tasks.add_task(delete_all_thread_records, checkpointer, sessionId)

    return {"message": "Session deleted successfully"}


@app.post("/tenant/{tenantId}/user/{userId}/sessions", tags=[endpointTitle], response_model=Session)
def create_chat_session(tenantId: str, userId: str):
    return create_thread(tenantId, userId)


def extract_relevant_messages(debug_lod_id, last_active_agent, response_data, tenantId, userId, sessionId):
    # Convert last_active_agent to its mapped value
    last_active_agent = agent_mapping.get(last_active_agent, last_active_agent)

    debug_lod_id = debug_lod_id
    if not response_data:
        return []

    last_agent_node = None
    last_agent_name = "unknown"
    for i in range(len(response_data) - 1, -1, -1):
        if "__interrupt__" in response_data[i]:
            if i > 0:
                last_agent_node = response_data[i - 1]
                last_agent_name = list(last_agent_node.keys())[0]
            break

    print(f"Last active agent: {last_agent_name}")
    # storing the last active agent in the session container so that we can retrieve it later
    # and deterministically route the incoming message directly to the agent that asked the question.
    patch_active_agent(tenantId, userId, sessionId, last_agent_name)

    if not last_agent_node:
        return []

    messages = []
    for key, value in last_agent_node.items():
        if isinstance(value, dict) and "messages" in value:
            messages.extend(value["messages"])

    last_user_index = -1
    for i in range(len(messages) - 1, -1, -1):
        if isinstance(messages[i], HumanMessage):
            last_user_index = i
            break

    if last_user_index == -1:
        return []

    filtered_messages = [msg for msg in messages[last_user_index:] if not isinstance(msg, ToolMessage)]

    return [
        MessageModel(
            id=str(uuid.uuid4()),
            type="ai_response",
            sessionId=sessionId,
            tenantId=tenantId,
            userId=userId,
            timeStamp=msg.response_metadata.get("timestamp", "") if hasattr(msg, "response_metadata") else "",
            sender="User" if isinstance(msg, HumanMessage) else last_active_agent,
            senderRole="User" if isinstance(msg, HumanMessage) else "Assistant",
            text=msg.content if hasattr(msg, "content") else msg.get("content", ""),
            debugLogId=debug_lod_id,
            tokensUsed=msg.response_metadata.get("token_usage", {}).get("total_tokens", 0) if hasattr(msg,
                                                                                                      "response_metadata") else 0,
            rating=True,
            completionPromptId=""
        )
        for msg in filtered_messages
        if msg.content
    ]


def process_messages(messages, userId, tenantId, sessionId):
    for message in messages:
        item = {
            "id": message.id,
            "type": message.type,
            "sessionId": message.sessionId,
            "tenantId": message.tenantId,
            "userId": message.userId,
            "timeStamp": message.timeStamp,
            "sender": message.sender,
            "senderRole": message.senderRole,
            "text": message.text,
            "debugLogId": message.debugLogId,
            "tokensUsed": message.tokensUsed,
            "rating": message.rating,
            "completionPromptId": message.completionPromptId
        }
        store_chat_history(item)

    partition_key = [tenantId, userId, sessionId]
    # Get the active agent from Cosmos DB with a point lookup
    activeAgent = chat_container.read_item(item=sessionId, partition_key=partition_key).get('activeAgent', 'unknown')

    last_active_agent = agent_mapping.get(activeAgent, activeAgent)
    update_active_agent_in_latest_message(sessionId, last_active_agent)


@app.post("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/completion", tags=[endpointTitle],
          response_model=List[MessageModel])
async def get_chat_completion(
        tenantId: str,
        userId: str,
        sessionId: str,
        background_tasks: BackgroundTasks,
        request_body: str = Body(..., media_type="application/json"),
        workflow: CompiledStateGraph = Depends(get_compiled_graph),

):
    if not request_body.strip():
        raise HTTPException(status_code=400, detail="Request body cannot be empty")

    # 🚀 SET MCP CONTEXT - Fix LLM parameter issues with automatic injection
    set_mcp_context(
        tenantId=tenantId,
        userId=userId,
        thread_id=sessionId
    )
    print(f"🔧 MCP CONTEXT SET: tenantId='{tenantId}', userId='{userId}', thread_id='{sessionId}'")

    # Retrieve last checkpoint
    config = {"configurable": {"thread_id": sessionId, "checkpoint_ns": "", "userId": userId, "tenantId": tenantId}}
    checkpoints = list(checkpointer.list(config))
    last_active_agent = "coordinator_agent"  # Default fallback

    if not checkpoints:
        # No previous state, start fresh
        new_state = {"messages": [{"role": "user", "content": request_body}]}
        response_data = await workflow.ainvoke(new_state, config, stream_mode="updates")
    else:
        # Resume from last checkpoint
        last_checkpoint = checkpoints[-1]
        last_state = last_checkpoint.checkpoint

        if "messages" not in last_state:
            last_state["messages"] = []

        last_state["messages"].append({"role": "user", "content": request_body})

        if "channel_versions" in last_state:
            for key in reversed(last_state["channel_versions"].keys()):
                if "agent" in key:
                    last_active_agent = key.split(":")[1]
                    break

        last_state["langgraph_triggers"] = [f"resume:{last_active_agent}"]
        response_data = await workflow.ainvoke(last_state, config, stream_mode="updates")

    debug_log_id = store_debug_log(sessionId, tenantId, userId, response_data)

    messages = extract_relevant_messages(debug_log_id, last_active_agent, response_data, tenantId, userId, sessionId)

    partition_key = [tenantId, userId, sessionId]
    # Get the active agent from Cosmos DB with a point lookup
    activeAgent = chat_container.read_item(item=sessionId, partition_key=partition_key).get('activeAgent', 'unknown')

    # update last sender in messages to the active agent
    messages[-1].sender = agent_mapping.get(activeAgent, activeAgent)

    # Schedule storing chat history and updating correct agent in last message as a background task
    # to avoid blocking the API response as this is not needed unless retrieving the message history later.
    background_tasks.add_task(process_messages, messages, userId, tenantId, sessionId)

    return messages


@app.post("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/summarize-name", tags=[endpointTitle],
          operation_id="SummarizeChatSessionName", response_description="Success", response_model=str)
def summarize_chat_session_name(tenantId: str, userId: str, sessionId: str,
                                request_body: str = Body(..., media_type="application/json")):
    """
    Generates a summarized name for a chat session based on the chat text provided.
    """
    try:
        prompt = (
            "Given the following chat transcript, generate a short, meaningful name for the conversation.\n\n"
            f"Chat Transcript:\n{request_body}\n\n"
            "Summary Name:"
        )

        response = model.invoke(prompt)
        summarized_name = response.content.strip()

        return summarized_name

    except Exception as e:
        return {"error": f"Failed to generate chat session name: {str(e)}"}


@app.post("/tenant/{tenantId}/user/{userId}/semanticcache/reset", tags=[endpointTitle],
          operation_id="ResetSemanticCache", response_description="Success",
          description="Semantic cache reset - not yet implemented", )
def reset_semantic_cache(tenantId: str, userId: str):
    return {"message": "Semantic cache reset not yet implemented"}


@app.put("/userdata", tags=[dataLoadTitle], description="Inserts or updates a single user data record in Cosmos DB")
async def put_userdata(data: Dict):
    try:
        update_users_container(data)
        return {"message": "Inserted user record successfully", "id": data.get("id")}
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to insert user data: {str(e)}")


@app.put("/accountdata", tags=[dataLoadTitle],
         description="Inserts or updates a single account data record in Cosmos DB")
async def put_accountdata(data: Dict):
    try:
        update_account_container(data)
        return {"message": "Inserted account record successfully", "id": data.get("id")}
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to insert account data: {str(e)}")


@app.put("/offerdata", tags=[dataLoadTitle], description="Inserts or updates a single offer data record in Cosmos DB")
async def put_offerdata(data: Dict):
    try:
        update_offers_container(data)
        return {"message": "Inserted offer record successfully", "id": data.get("id")}
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to insert offer data: {str(e)}")


# New Banking API endpoints
@app.get("/tenant/{tenantId}/user/{userId}/accounts", 
         tags=[endpointTitle], 
         description="Retrieves all bank accounts for a specific user", 
         operation_id="GetAccountDetailsAsync",
         response_model=List[BankAccount])
def get_user_accounts(tenantId: str, userId: str):
    """
    Get all bank accounts associated with a specific user.
    
    :param tenantId: The tenant identifier
    :param userId: The user identifier  
    :return: List of bank accounts belonging to the user
    """
    try:
        accounts_data = fetch_accounts_by_user(tenantId, userId)
        
        # Convert raw data to BankAccount models with flexible mapping
        accounts = []
        for account_data in accounts_data:
            try:
                # More flexible field mapping - handle different field names and formats
                account_id = account_data.get("id") or account_data.get("accountId") or ""
                account_name = account_data.get("name") or account_data.get("accountName") or account_data.get("accountHolder") or "Unknown Account"
                
                # Handle accountType with fallback to 0 (CHECKING) for any invalid values
                try:
                    account_type_value = account_data.get("accountType", 0)
                    if isinstance(account_type_value, str):
                        # Try to map string values to enum
                        account_type_mapping = {"checking": 0, "savings": 1, "credit": 2}
                        account_type_value = account_type_mapping.get(account_type_value.lower(), 0)
                    account_type = AccountType(int(account_type_value))
                except (ValueError, TypeError):
                    account_type = AccountType.CHECKING  # Default fallback
                
                # Handle optional enum fields with better error handling
                card_number = account_data.get("cardNumber")
                if isinstance(card_number, str) and card_number.isdigit():
                    card_number = int(card_number)
                elif not isinstance(card_number, (int, type(None))):
                    card_number = None
                
                # Account status with fallback
                try:
                    account_status_value = account_data.get("accountStatus")
                    account_status = AccountStatus(int(account_status_value)) if account_status_value is not None else None
                except (ValueError, TypeError):
                    account_status = None
                
                # Card type with fallback  
                try:
                    card_type_value = account_data.get("cardType")
                    card_type = CardType(int(card_type_value)) if card_type_value is not None else None
                except (ValueError, TypeError):
                    card_type = None
                
                # Handle balance - could be string or number
                balance = account_data.get("balance", 0)
                if isinstance(balance, str):
                    try:
                        balance = int(float(balance))
                    except (ValueError, TypeError):
                        balance = 0
                
                # Handle limit - could be string or number
                limit = account_data.get("limit")
                if isinstance(limit, str):
                    try:
                        limit = int(float(limit))
                    except (ValueError, TypeError):
                        limit = None
                
                # Handle interest rate
                interest_rate = account_data.get("interestRate")
                if isinstance(interest_rate, str):
                    try:
                        interest_rate = int(float(interest_rate))
                    except (ValueError, TypeError):
                        interest_rate = None
                
                short_description = account_data.get("shortDescription") or account_data.get("description") or "Bank Account"
                
                # Create BankAccount with flexible mapping
                account = BankAccount(
                    id=account_id,
                    tenantId=account_data.get("tenantId", tenantId),
                    name=account_name,
                    accountType=account_type,
                    cardNumber=card_number,
                    accountStatus=account_status,
                    cardType=card_type,
                    balance=balance,
                    limit=limit,
                    interestRate=interest_rate,
                    shortDescription=short_description
                )
                accounts.append(account)
                
            except Exception as account_error:
                # Log the error but continue processing other accounts
                print(f"[WARNING] Failed to process account {account_data.get('id', 'unknown')}: {account_error}")
                # Create a minimal account entry for failed parsing
                minimal_account = BankAccount(
                    id=account_data.get("id") or account_data.get("accountId") or "unknown",
                    tenantId=account_data.get("tenantId", tenantId),
                    name=account_data.get("name") or "Unknown Account",
                    accountType=AccountType.CHECKING,
                    shortDescription="Account data parsing failed"
                )
                accounts.append(minimal_account)
            
        print(f"[DEBUG] Successfully processed {len(accounts)} accounts for user {userId}")
        return accounts
        
    except Exception as e:
        print(f"[ERROR] Failed to retrieve accounts for user {userId}: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Failed to retrieve accounts: {str(e)}")


@app.get("/tenant/{tenantId}/user/{userId}/accounts/{accountId}/transactions",
         tags=[endpointTitle],
         description="Retrieves transaction history for a specific account",
         operation_id="GetAccountTransactions", 
         response_model=List[BankTransaction])
def get_account_transactions(tenantId: str, userId: str, accountId: str):
    """
    Get all transactions for a specific bank account.
    
    :param tenantId: The tenant identifier
    :param userId: The user identifier (for authorization/context)
    :param accountId: The account identifier
    :return: List of transactions for the specified account
    """
    try:
        transactions_data = fetch_transactions_by_account_id(tenantId, accountId)
        
        # Convert raw data to BankTransaction models
        transactions = []
        for transaction_data in transactions_data:
            # Parse the transaction date 
            transaction_date_str = transaction_data.get("transactionDateTime", "")
            try:
                # Handle different datetime formats
                if transaction_date_str.endswith('Z'):
                    transaction_date = datetime.fromisoformat(transaction_date_str.replace('Z', '+00:00'))
                else:
                    transaction_date = datetime.fromisoformat(transaction_date_str)
            except (ValueError, TypeError):
                transaction_date = datetime.now()  # Fallback to current time
            
            transaction = BankTransaction(
                id=transaction_data.get("id", ""),
                tenantId=transaction_data.get("tenantId", ""),
                accountId=transaction_data.get("accountId", ""),
                debitAmount=transaction_data.get("debitAmount", 0),
                creditAmount=transaction_data.get("creditAmount", 0),
                accountBalance=transaction_data.get("accountBalance", 0),
                details=transaction_data.get("details", ""),
                transactionDateTime=transaction_date
            )
            transactions.append(transaction)
            
        return transactions
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to retrieve transactions: {str(e)}")


@app.get("/tenant/{tenantId}/servicerequests",
         tags=[endpointTitle],
         description="Retrieves service requests for a tenant, optionally filtered by user",
         operation_id="GetServiceRequests",
         response_model=List[ServiceRequest])
def get_service_requests(tenantId: str, userId: str = None):
    """
    Get all service requests for a tenant, with optional user filtering.
    
    :param tenantId: The tenant identifier
    :param userId: Optional user identifier to filter service requests by specific user
    :return: List of service requests for the tenant (and user if specified)
    """
    try:
        service_requests_data = fetch_service_requests_by_tenant(tenantId, userId)
        
        # Convert raw data to ServiceRequest models
        service_requests = []
        for request_data in service_requests_data:
            # Parse dates
            requested_on = None
            scheduled_date = None
            
            if request_data.get("requestedOn"):
                try:
                    requested_on_str = request_data.get("requestedOn")
                    if isinstance(requested_on_str, str):
                        if requested_on_str.endswith('Z'):
                            requested_on = datetime.fromisoformat(requested_on_str.replace('Z', '+00:00'))
                        else:
                            requested_on = datetime.fromisoformat(requested_on_str)
                    elif isinstance(requested_on_str, datetime):
                        requested_on = requested_on_str
                except (ValueError, TypeError):
                    pass
                    
            if request_data.get("scheduledDateTime"):
                try:
                    scheduled_date_str = request_data.get("scheduledDateTime") 
                    if isinstance(scheduled_date_str, str):
                        if scheduled_date_str.endswith('Z'):
                            scheduled_date = datetime.fromisoformat(scheduled_date_str.replace('Z', '+00:00'))
                        else:
                            scheduled_date = datetime.fromisoformat(scheduled_date_str)
                    elif isinstance(scheduled_date_str, datetime):
                        scheduled_date = scheduled_date_str
                except (ValueError, TypeError):
                    pass
            
            service_request = ServiceRequest(
                id=request_data.get("id"),
                tenantId=request_data.get("tenantId"),
                userId=request_data.get("userId"),
                type=request_data.get("type"),
                requestedOn=requested_on,
                scheduledDateTime=scheduled_date,
                accountId=request_data.get("accountId"),
                srType=ServiceRequestType(request_data.get("srType", 0)) if request_data.get("srType") is not None else None,
                recipientEmail=request_data.get("recipientEmail"),
                recipientPhone=request_data.get("recipientPhone"),
                debitAmount=request_data.get("debitAmount"),
                isComplete=request_data.get("isComplete", False),
                requestAnnotations=request_data.get("requestAnnotations"),
                fulfilmentDetails=request_data.get("fulfilmentDetails")
            )
            service_requests.append(service_request)
            
        return service_requests
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to retrieve service requests: {str(e)}")
```
</details>

## Activity 4: Test Local MCP Mode  

Now let's test your MCP-enabled banking application in Local mode!

### Step 1: Configure Local MCP Mode

For **Local MCP mode**, your `python/.env` file should contain:

```bash
# Azure Services Configuration
COSMOSDB_ENDPOINT="https://your-cosmos-account.documents.azure.com:443/"
AZURE_OPENAI_ENDPOINT="https://your-openai-account.openai.azure.com/"
AZURE_OPENAI_EMBEDDINGDEPLOYMENTID="text-embedding-3-small"
AZURE_OPENAI_COMPLETIONSDEPLOYMENTID="gpt-4o"
APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=your-key;IngestionEndpoint=https://your-region.in.applicationinsights.azure.com/..."
AZURE_OPENAI_API_VERSION="2024-02-15-preview"

# No MCP server configuration needed for Local mode
```

> **Note**: Local MCP mode uses embedded MCP integration - no separate server needed.

### Step 2: Run the Application

In your IDE terminal, run the MCP-enabled application:

```bash
python -m src.app.banking_agents
```

You should see MCP initialization output:

```
� Initializing MCP Banking System...
� ENHANCED MCP: Initializing in LOCAL mode
🚀 ENHANCED MCP: Starting shared server process...
✅ ENHANCED MCP: Shared server started in 1247.32ms (PID: 12345)
🔄 ENHANCED MCP: Connecting to local shared server...
✅ ENHANCED MCP: Successfully connected to local server
✅ Loaded 11 MCP tools:
   - bank_balance
   - bank_transfer
   - get_transaction_history
   - create_account
   - service_request
   - get_branch_location
   - calculate_monthly_payment
   - get_offer_information
   - transfer_to_customer_support_agent
   - transfer_to_transactions_agent
   - transfer_to_sales_agent

============================================================
🏦 Welcome to the MCP-Enabled Banking Assistant!
🔧 Now powered by Model Context Protocol  
============================================================

Available agents:
  • Customer Support (balance, service requests)
  • Transactions (transfers, history)
  • Sales (new accounts, loans)

Type 'exit' to end the conversation.

You:
```

### Step 3: Test Banking Operations

Try these test scenarios to verify MCP integration:

#### Test 1: Account Balance (Customer Support Agent)
```
You: What's the balance for account A123?
```

Expected output:
```
📞 LOCAL MCP: Calling tool 'bank_balance' via shared server
🔧 DEBUG: Tool arguments: {'account_number': 'A123', 'tenantId': 'Contoso', 'userId': 'Mark'}
✅ LOCAL MCP: Tool call completed in 45.23ms
Agent: The current balance for account A123 is $2,150.75.
```

#### Test 2: Money Transfer (Transactions Agent)
```
You: I want to transfer $100 from account A123 to account A456
```

Expected output:
```
transfer_to_transactions_agent...
📞 LOCAL MCP: Calling tool 'bank_transfer' via shared server  
🔧 DEBUG: Tool arguments: {'fromAccount': 'A123', 'toAccount': 'A456', 'amount': 100.0, 'tenantId': 'Contoso', 'userId': 'Mark', 'thread_id': 'hardcoded-thread-id-01'}
✅ LOCAL MCP: Tool call completed in 67.89ms
transactions_agent: Transfer completed successfully! $100 has been transferred from account A123 to account A456. Transaction ID: TXN_789123
```

#### Test 3: Create Account (Sales Agent)
```
You: I'd like to open a savings account with $1000
```

Expected output:
```
transfer_to_sales_agent...
📞 LOCAL MCP: Calling tool 'create_account' via shared server
🔧 DEBUG: Tool arguments: {'account_holder': 'Mark', 'balance': 1000.0, 'tenantId': 'Contoso', 'userId': 'Mark'}
✅ LOCAL MCP: Tool call completed in 123.45ms
sales_agent: Great! I've successfully created a new savings account for you with an initial balance of $1,000. Your new account number is A789. Welcome to our banking family!
```

### Step 4: Analyze Performance Improvements

Measure the performance improvements and understand why they occur:

#### **Performance Metrics to Observe:**

```
📞 LOCAL MCP: Calling tool 'bank_balance' via shared server
🔧 DEBUG: Tool arguments: {'account_number': 'A123', 'tenantId': 'Contoso', 'userId': 'Mark'}
✅ LOCAL MCP: Tool call completed in 45.23ms  ← Key metric
```

#### **Why These Performance Gains Occur:**

**1. Connection Caching Effect**
- **Before**: New Cosmos DB connection per tool call (~1-2 seconds)
- **After**: Reused connection from MCP server cache (~5-10ms)
- **Improvement**: 100-400x faster database operations

**2. Process Reuse Benefit**
- **Before**: Python import overhead per agent initialization
- **After**: Shared MCP server process across all agents
- **Improvement**: Reduced memory usage and startup time

**3. Context Optimization**
- **Before**: Manual context passing, potential errors
- **After**: Automatic context injection with validation
- **Improvement**: Consistent security and reduced code complexity

#### **Expected Performance Ranges:**
- **Local MCP Tool Calls**: 40-80ms (cached connections)
- **Remote MCP Tool Calls**: 60-120ms (HTTP + cached connections)
- **Native Tool Calls**: 2000-4000ms (new connections each time)

#### **Resource Usage Comparison:**
Monitor your system resources during testing:
- **Memory Usage**: Should decrease as agents share MCP server resources
- **Database Connections**: Should drop from N×agents to 1 total
- **CPU Usage**: Lower due to reduced connection overhead

This performance improvement is why the custom MCP implementation was necessary - standard MCP couldn't deliver the sub-100ms response times required for real-time banking operations.

## Activity 5: Test Remote MCP Mode

Now let's test the Remote MCP mode using the dedicated HTTP server.

### Step 1: Configure Remote MCP Mode

For **Remote MCP mode**, you need two `.env` files:

**1. Python client `.env` file (`python/.env`):**
```bash
# Azure Services Configuration
COSMOSDB_ENDPOINT="https://your-cosmos-account.documents.azure.com:443/"
AZURE_OPENAI_ENDPOINT="https://your-openai-account.openai.azure.com/"
AZURE_OPENAI_EMBEDDINGDEPLOYMENTID="text-embedding-3-small"
AZURE_OPENAI_COMPLETIONSDEPLOYMENTID="gpt-4o"
APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=your-key;IngestionEndpoint=https://your-region.in.applicationinsights.azure.com/..."
AZURE_OPENAI_API_VERSION="2024-02-15-preview"

# Remote MCP Server Configuration
MCP_SERVER_ENDPOINT="http://localhost:8080"
USE_REMOTE_MCP_SERVER="true"
```

**2. MCP server `.env` file (`mcpserver/.env`):**
```bash
# Azure Services Configuration (same as client)
COSMOSDB_ENDPOINT="https://your-cosmos-account.documents.azure.com:443/"
AZURE_OPENAI_ENDPOINT="https://your-openai-account.openai.azure.com/"
AZURE_OPENAI_EMBEDDINGDEPLOYMENTID="text-embedding-3-small"
AZURE_OPENAI_COMPLETIONSDEPLOYMENTID="gpt-4o"
APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=your-key;IngestionEndpoint=https://your-region.in.applicationinsights.azure.com/..."
AZURE_OPENAI_API_VERSION="2024-02-15-preview"

# MCP Server Authentication
MCP_AUTH_SECRET_KEY="your-mcp-server-jwt-secret-key"
```

### Step 2: Start the Remote MCP Server

The Remote MCP server is provided in the `mcpserver/` directory. Open a **new terminal** and start it:

```bash
# Navigate to the mcpserver directory
cd mcpserver

# Install dependencies (if not already done)
pip install -r requirements.txt

# Start the Remote MCP HTTP server
python -m src.mcp_http_server
```

You should see:

```
🚀 MCP HTTP SERVER: Starting on http://localhost:8080
🔐 JWT_SECRET loaded: eyJ0eXAiOiJKV1QiLCJhbGc...
🔧 AZURE CONFIG: OpenAI endpoint configured
🔧 AZURE CONFIG: Cosmos DB endpoint configured  
✅ MCP HTTP SERVER: Ready to accept connections
   • 11 banking tools available
   • JWT authentication enabled
   • Multi-tenant security active
INFO:     Uvicorn running on http://0.0.0.0:8080 (Press CTRL+C to quit)
```

### Step 3: Run Application in Remote Mode

In your **original terminal** (where you ran the application), run it again:

```bash
python -m src.app.banking_agents
```

You should see Remote MCP initialization:

```
🚀 Initializing MCP Banking System...  
🔧 ENHANCED MCP: Initializing in REMOTE mode
🌐 ENHANCED MCP: Using Remote MCP server (HTTP)
🌐 REMOTE MCP: Connecting to http://localhost:8080
✅ REMOTE MCP: Server health check passed - healthy
🔐 REMOTE MCP: Successfully authenticated
🔧 REMOTE MCP: Retrieved 11 tools:
   - bank_balance
   - bank_transfer
   - get_transaction_history
   - create_account
   - service_request
   - get_branch_location
   - calculate_monthly_payment
   - get_offer_information
   - transfer_to_customer_support_agent
   - transfer_to_transactions_agent
   - transfer_to_sales_agent
✅ Loaded 11 MCP tools
============================================================
🏦 Welcome to the MCP-Enabled Banking Assistant!
🔧 Now powered by Model Context Protocol (REMOTE MODE)
============================================================
```

### Step 4: Test Remote MCP Operations

Try the same banking operations as before:

#### Test Remote Account Balance:
```
You: Check balance for account A123
```

Expected output:
```
📞 REMOTE MCP: Calling tool 'bank_balance' via HTTP
🔧 DEBUG REMOTE CLIENT: Making request to http://localhost:8080/tools/call
🔧 DEBUG REMOTE CLIENT: Request data: {'tool_name': 'bank_balance', 'arguments': {'account_number': 'A123'}, 'tenant_id': 'Contoso', 'user_id': 'Mark', 'thread_id': 'hardcoded-thread-id-01'}
✅ REMOTE MCP: Tool call completed in 89.12ms
Agent: The current balance for account A123 is $2,150.75.
```

Notice the difference:
- **Local MCP**: Direct process communication (`LOCAL MCP: Calling tool via shared server`)
- **Remote MCP**: HTTP API calls (`REMOTE MCP: Calling tool via HTTP`)

### Validation Checklist

Your MCP conversion is successful if:

- **Local MCP Mode**: Application starts with embedded MCP server (PID shown)
- **Remote MCP Mode**: Application connects to external HTTP MCP server  
- **Tool Loading**: All 11 MCP tools load successfully in both modes
- **Banking Operations**: Balance, transfer, account creation work correctly
- **Agent Transfers**: Coordinator routes to customer support, transactions, sales
- **Performance Logging**: Tool execution times are displayed (40-150ms typical)
- **Context Injection**: tenantId, userId, thread_id automatically included
- **Authentication**: Remote mode shows successful JWT authentication
- **Error Handling**: Graceful error messages if servers are unavailable

## What You've Accomplished

Congratulations! You have successfully implemented a custom MCP solution that addresses real-world performance challenges in multi-agent systems. Here's what you achieved:

### ✅ **Performance Architecture Mastery**:
- **Problem Solved**: Eliminated 2-4 second tool call latencies that made standard MCP impractical
- **Solution Implemented**: Custom MCP with connection caching achieving 40-120ms tool calls
- **Trade-off Managed**: Increased implementation complexity in exchange for 25-75x performance improvement
- **Result**: Production-ready banking system capable of real-time operations

### ✅ **Architectural Decision Framework**:
- **Native Tools**: Understand when simplicity trumps performance (prototypes, low-frequency usage)
- **Local MCP**: Grasp embedded server benefits for single-application deployments
- **Remote MCP**: Comprehend microservices architecture for multi-client, enterprise scenarios
- **Decision Matrix**: Can evaluate implementation approaches based on performance, complexity, and operational requirements

### ✅ **Custom MCP Implementation**:
- **Connection Caching**: Implemented shared Azure service connections across all agents
- **Dual Mode Support**: Built flexible architecture supporting both Local and Remote deployment
- **Context Injection**: Automated multi-tenant security context for all tool operations
- **Performance Monitoring**: Integrated timing and logging for production optimization
- **Error Resilience**: Built robust error handling with reconnection and retry logic

### ✅ **Production Engineering Skills**:
- **Performance Bottleneck Analysis**: Identified and measured connection overhead as primary constraint
- **Resource Optimization**: Reduced memory usage and database connections through sharing
- **Scalability Planning**: Understood scaling characteristics of different architectural approaches
- **Operational Trade-offs**: Balanced implementation complexity against performance requirements

### ✅ **Real-World Problem Solving**:
- **Standard Solution Inadequate**: Recognized when off-the-shelf MCP wasn't sufficient
- **Custom Solution Design**: Architected specialized solution for specific performance requirements
- **Implementation Justification**: Can explain why custom implementation was necessary
- **Maintenance Awareness**: Understand ongoing complexity costs of custom solutions

### Key Insights Gained

**1. Performance vs Complexity Trade-off**
You've experienced firsthand how performance requirements can drive architectural complexity. The custom MCP implementation requires more maintenance but enables use cases (real-time banking) that weren't possible with standard approaches.

**2. Architecture Decision Framework**  
You now have concrete criteria for choosing between Native Tools, Local MCP, and Remote MCP based on performance requirements, operational complexity, and scalability needs.

**3. Production Engineering Mindset**
You've learned to measure performance bottlenecks, design solutions for specific constraints, and understand the operational implications of architectural choices.

Your banking application now demonstrates modern AI architecture patterns with custom MCP implementation, making it a reference example for high-performance multi-agent systems!

---

**[< Lessons Learned, Agent Futures, Q&A](./Module-05.md)** - **[Home](Home.md)**