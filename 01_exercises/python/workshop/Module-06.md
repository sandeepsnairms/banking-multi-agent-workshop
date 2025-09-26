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

- **Performance Cost**: Additional protocol overhead (typically 10-50ms per tool call)
- **Architectural Benefit**: Loose coupling, standardization, maintainability, team autonomy
- **Maintenance Benefit**: Independent development and deployment cycles for different teams
- **Strategic Value**: Long-term maintainability and ecosystem compatibility over raw speed

This is a "USB-C for AI" decision - accepting slight performance overhead for massive interoperability benefits.

## Learning Objectives

By the end of this module, you will:
- Understand the architectural benefits that can drive MCP adoption over native tools
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

Open the empty file `src/app/tools/mcp_client.py` and copy/paste the complete code below:

<details>
<summary><strong>Complete mcp_client.py code (click to expand)</strong></summary>

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
            print(f"🔐 REMOTE MCP: Authenticating with server at {self.base_url}")
            
            # Get token from auth endpoint (in production, use proper OAuth2 flow)
            response = await self.http_client.post(f"{self.base_url}/auth/token")
            response.raise_for_status()
            
            token_data = response.json()
            self.access_token = token_data.get("access_token")
            
            if not self.access_token:
                print("❌ REMOTE MCP: No access token received")
                return False
                
            print("✅ REMOTE MCP: Successfully authenticated")
            return True
            
        except Exception as e:
            print(f"❌ HTTP MCP: Authentication failed: {e}")
            return False
    
    async def connect_to_server(self) -> bool:
        """Connect to the HTTP MCP server"""
        try:
            print(f"🔌 REMOTE MCP: Connecting to server at {self.base_url}")
            
            # Authenticate first
            if not await self.authenticate():
                return False
            
            # Test connection with health check
            headers = {"Authorization": f"Bearer {self.access_token}"}
            response = await self.http_client.get(f"{self.base_url}/health", headers=headers)
            response.raise_for_status()
            
            health_data = response.json()
            print(f"✅ REMOTE MCP: Connected to server (status: {health_data.get('status')})")
            
            # Get available tools
            response = await self.http_client.get(f"{self.base_url}/tools", headers=headers)
            response.raise_for_status()
            
            tools_data = response.json()
            self.tools_cache = tools_data
            
            print(f"🛠️  REMOTE MCP: Retrieved {len(self.tools_cache)} tools")
            for tool in self.tools_cache:
                print(f"   - {tool.get('name', 'unknown')}")
            
            return True
            
        except Exception as e:
            print(f"❌ HTTP MCP: Failed to connect to server: {e}")
            return False
    
    async def call_tool(self, tool_name: str, arguments: Dict[str, Any]) -> Any:
        """Call a tool via HTTP API"""
        if not self.access_token:
            if not await self.authenticate():
                raise Exception("Could not authenticate with HTTP MCP server")
        
        call_start = time.time()
        print(f"📞 REMOTE MCP: Calling tool '{tool_name}' via HTTP")
        print(f"🔧 DEBUG: Tool arguments: {arguments}")
        
        # Inject context information
        context = get_mcp_context()
        
        request_data = {
            "tool_name": tool_name,
            "arguments": arguments,
            "tenant_id": context.get('tenantId'),
            "user_id": context.get('userId'),
            "thread_id": context.get('thread_id')
        }
        
        print(f"🔧 DEBUG REMOTE CLIENT: Making request to {self.base_url}/tools/call")
        print(f"🔧 DEBUG REMOTE CLIENT: Request data: {request_data}")
        
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
                raise Exception(error_msg)
                
        except Exception as e:
            call_time = (time.time() - call_start) * 1000
            print(f"❌ REMOTE MCP: Tool call failed in {call_time:.2f}ms: {e}")
            raise
    
    async def get_tools(self) -> List[Dict[str, Any]]:
        """Get available tools from HTTP server"""
        if not self.tools_cache:
            if not await self.connect_to_server():
                raise Exception("Could not connect to Remote MCP server to get tools")
        
        # Convert HTTP server tool format to expected MCP format
        mcp_tools = []
        for tool in self.tools_cache:
            mcp_tool = {
                'name': tool.get('name'),
                'description': tool.get('description'),
                'input_schema': tool.get('input_schema', {}),
                'parameters': tool.get('parameters', {})  # ← PRESERVE parameters for SharedMCP client
            }
            mcp_tools.append(mcp_tool)
        
        return mcp_tools
    
    async def cleanup(self):
        """Clean up HTTP client"""
        print("🔄 REMOTE MCP: Cleaning up Remote MCP client...")
        if self.http_client:
            await self.http_client.aclose()
        self.access_token = None
        self.tools_cache = None

class SharedMCPClient:
    """Enhanced MCP client with Remote and Local server support"""
    
    def __init__(self):
        self.remote_client: Optional[RemoteMCPClient] = None
        self.local_server = None
        self.use_remote = os.getenv("USE_REMOTE_MCP_SERVER", "false").lower() == "true"
        self.tools_cache: Optional[List] = None
        
        # Legacy stdio support
        self.server_process: Optional[subprocess.Popen] = None
        self.client: Optional[MultiServerMCPClient] = None
        self.server_ready = False
        
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
            print("🔗 ENHANCED MCP: Using Local MCP server")
            try:
                from src.app.tools.mcp_server import get_cached_server_instance
                self.local_server = await get_cached_server_instance()
                
                if not self.local_server:
                    print("❌ ENHANCED MCP: No Local server instance available")
                    return False
                
                tools_info = self.local_server.get_available_tools()
                self.tools_cache = tools_info if isinstance(tools_info, list) else []
                
                print(f"🛠️  ENHANCED MCP: Connected to Local server with {len(self.tools_cache)} tools")
                return True
                
            except Exception as e:
                print(f"❌ ENHANCED MCP: Failed to connect to Local server: {e}")
                return False
    
    async def get_tools(self):
        """Get LangChain-compatible tools from the shared MCP server."""
        try:
            if self.use_remote and self.remote_client:
                print("🔧 ENHANCED MCP: Getting tools from Remote server")
                tools_list = await self.remote_client.get_tools()
            else:
                print("🔧 ENHANCED MCP: Getting tools from Local server")
                from src.app.tools.mcp_server import get_cached_server_instance
                server_instance = await get_cached_server_instance()
                tools_list = server_instance.get_available_tools()
            
            if not tools_list:
                print("❌ ENHANCED MCP: No tools available from Local server")
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
                            print(f"🔧 DEBUG: INITIAL - args type: {type(args)}, len: {len(args)}")
                            print(f"🔧 DEBUG: INITIAL - kwargs type: {type(kwargs)}, keys: {list(kwargs.keys()) if kwargs else 'None'}")
                            print(f"🔧 DEBUG: INITIAL - kwargs content: {kwargs}")
                            
                            # Special case: if LangGraph passes parameters directly as kwargs (which it should)
                            if captured_tool_name == "bank_transfer" and not args and kwargs:
                                # Check if we have the expected bank_transfer parameters directly in kwargs
                                expected_params = ["fromAccount", "toAccount", "amount"]
                                has_direct_params = any(param in kwargs for param in expected_params)
                                print(f"🔧 DEBUG: bank_transfer direct kwargs check - has_direct_params: {has_direct_params}")
                                if has_direct_params:
                                    print(f"🔧 DEBUG: bank_transfer using direct kwargs - parameters already correct")
                                    # Parameters are already in the right place, no mapping needed
                                    pass
                            
                            # Enhanced parameter mapping for tools with specific parameter needs
                            if captured_tool_name == "bank_balance" and args and not kwargs.get("account_number"):
                                kwargs["account_number"] = args[0]
                                print(f"🔧 DEBUG: Fixed bank_balance args - account_number: {args[0]}")
                            
                            elif captured_tool_name == "get_offer_information" and (
                                (args and not kwargs) or 
                                (kwargs.get('args') and len(args) == 0)
                            ):
                                # Map positional args to expected parameters
                                actual_args = args if args else kwargs.get('args', ())
                                if len(actual_args) >= 1:
                                    kwargs["prompt"] = actual_args[0]
                                if len(actual_args) >= 2:
                                    kwargs["type"] = actual_args[1]
                                # Remove the 'args' key if it exists
                                kwargs.pop('args', None)
                                print(f"🔧 DEBUG: Fixed get_offer_information args - prompt: {kwargs.get('prompt', 'N/A')}, type: {kwargs.get('type', 'N/A')}")
                            
                            elif captured_tool_name == "bank_transfer" and (
                                (args and not kwargs) or 
                                (kwargs.get('args') and len(args) == 0)
                            ):
                                # Map positional args to bank transfer parameters
                                actual_args = args if args else kwargs.get('args', ())
                                param_names = ["fromAccount", "toAccount", "amount", "tenantId", "userId", "thread_id"]
                                for i, arg in enumerate(actual_args[:len(param_names)]):
                                    kwargs[param_names[i]] = arg
                                # Remove the 'args' key if it exists
                                kwargs.pop('args', None)
                                print(f"🔧 DEBUG: Fixed bank_transfer args - mapped {len(actual_args)} parameters")
                            
                            elif captured_tool_name == "bank_transfer" and not args and (not kwargs or not any(k for k in kwargs.keys() if k != 'args')):
                                # Handle case where bank_transfer is called with no meaningful arguments
                                print(f"🔧 DEBUG: bank_transfer called with no arguments - this suggests the agent isn't providing required parameters")
                                print(f"🔧 DEBUG: The agent should provide: fromAccount, toAccount, amount, tenantId, userId, thread_id")
                                print(f"🔧 DEBUG: This is likely a prompt/instruction issue with the LangGraph agent")
                                # We can't fix this here - the agent needs to provide the transfer details
                                # Let it fall through to call_tool which will return an appropriate error
                            
                            elif captured_tool_name == "get_transaction_history" and (
                                (args and not kwargs) or 
                                (kwargs.get('args') and len(args) == 0)
                            ):
                                # Map positional args to transaction history parameters
                                actual_args = args if args else kwargs.get('args', ())
                                param_names = ["account_number", "start_date", "end_date", "tenantId", "userId"]
                                for i, arg in enumerate(actual_args[:len(param_names)]):
                                    kwargs[param_names[i]] = arg
                                # Remove the 'args' key if it exists
                                kwargs.pop('args', None)
                                print(f"🔧 DEBUG: Fixed get_transaction_history args - mapped {len(actual_args)} parameters")
                            
                            elif captured_tool_name == "calculate_monthly_payment" and (
                                (args and not kwargs) or 
                                (kwargs.get('args') and len(args) == 0)
                            ):
                                # Map positional args to loan calculation parameters
                                actual_args = args if args else kwargs.get('args', ())
                                if len(actual_args) >= 1:
                                    kwargs["loan_amount"] = actual_args[0]
                                if len(actual_args) >= 2:
                                    kwargs["years"] = actual_args[1]
                                # Remove the 'args' key if it exists
                                kwargs.pop('args', None)
                                print(f"🔧 DEBUG: Fixed calculate_monthly_payment args - loan_amount: {kwargs.get('loan_amount')}, years: {kwargs.get('years')}")
                            
                            elif captured_tool_name == "create_account" and (
                                (args and not kwargs) or 
                                (kwargs.get('args') and len(args) == 0)
                            ):
                                # Map positional args to create account parameters
                                actual_args = args if args else kwargs.get('args', ())
                                param_names = ["account_holder", "balance", "tenantId", "userId"]
                                for i, arg in enumerate(actual_args[:len(param_names)]):
                                    kwargs[param_names[i]] = arg
                                # Remove the 'args' key if it exists
                                kwargs.pop('args', None)
                                print(f"🔧 DEBUG: Fixed create_account args - mapped {len(actual_args)} parameters")
                            
                            elif captured_tool_name == "service_request" and (
                                (args and not kwargs) or 
                                (kwargs.get('args') and len(args) == 0)
                            ):
                                # Map positional args to service request parameters
                                actual_args = args if args else kwargs.get('args', ())
                                param_names = ["recipientPhone", "recipientEmail", "requestSummary", "tenantId", "userId"]
                                for i, arg in enumerate(actual_args[:len(param_names)]):
                                    kwargs[param_names[i]] = arg
                                # Remove the 'args' key if it exists
                                kwargs.pop('args', None)
                                print(f"🔧 DEBUG: Fixed service_request args - mapped {len(actual_args)} parameters")
                            
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
                    print(f"🔧 DEBUG: Processing tool {tool_name}, parameters: {parameters}")
                    
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
                        print(f"🔧 DEBUG: Creating WRAPPER FUNCTION for bank_transfer tool with parameters: {parameters}")
                        # Bank transfer tool with required parameters - create proper function signature
                        # Use closure to capture self reference
                        def create_bank_transfer_wrapper(client_ref):
                            async def bank_transfer_wrapper(fromAccount: str, toAccount: str, amount: float, tenantId: str, userId: str, thread_id: str):
                                """Bank transfer wrapper with proper Pydantic signature"""
                                print(f"🚀 BANK_TRANSFER_WRAPPER: Called with fromAccount={fromAccount}, toAccount={toAccount}, amount={amount}")
                                kwargs = {
                                    "fromAccount": fromAccount,
                                    "toAccount": toAccount, 
                                    "amount": amount,
                                    "tenantId": tenantId,
                                    "userId": userId,
                                    "thread_id": thread_id
                                }
                                try:
                                    print(f"🔧 BANK_TRANSFER_WRAPPER: Calling client_ref.call_tool with kwargs: {kwargs}")
                                    result = await client_ref.call_tool("bank_transfer", kwargs)
                                    print(f"🔧 BANK_TRANSFER_WRAPPER: Got result: {result}")
                                    return result
                                except Exception as e:
                                    print(f"❌ BANK_TRANSFER_WRAPPER: Exception occurred: {e}")
                                    raise
                            return bank_transfer_wrapper
                        
                        # Create wrapper with proper client reference
                        bank_transfer_func = create_bank_transfer_wrapper(self)
                        
                        from pydantic import BaseModel, Field
                        
                        class BankTransferInput(BaseModel):
                            fromAccount: str = Field(description="Source account number for the transfer")
                            toAccount: str = Field(description="Destination account number for the transfer")
                            amount: float = Field(description="Amount to transfer (positive number)")
                            tenantId: str = Field(description="Tenant ID for the transaction")
                            userId: str = Field(description="User ID for the transaction")
                            thread_id: str = Field(description="Thread ID for the transaction")
                        
                        structured_tool = StructuredTool.from_function(
                            coroutine=bank_transfer_func,
                            name=tool_name,
                            description=tool_dict.get('description', f'Execute {tool_name}'),
                            args_schema=BankTransferInput
                        )
                    
                    elif tool_name == "get_transaction_history" and parameters:
                        # Transaction history tool with proper wrapper function
                        def create_transaction_history_wrapper(client_ref):
                            async def transaction_history_wrapper(account_number: str, start_date: str, end_date: str, tenantId: str, userId: str):
                                """Transaction history wrapper with proper Pydantic signature"""
                                print(f"🚀 TRANSACTION_HISTORY_WRAPPER: Called with account_number={account_number}, start_date={start_date}, end_date={end_date}")
                                kwargs = {
                                    "account_number": account_number,
                                    "start_date": start_date,
                                    "end_date": end_date,
                                    "tenantId": tenantId,
                                    "userId": userId
                                }
                                return await client_ref.call_tool("get_transaction_history", kwargs)
                            return transaction_history_wrapper
                        
                        transaction_history_func = create_transaction_history_wrapper(self)
                        
                        from pydantic import BaseModel, Field
                        
                        class TransactionHistoryInput(BaseModel):
                            account_number: str = Field(description="Account number to get transaction history for")
                            start_date: str = Field(description="Start date for transaction history (YYYY-MM-DD format)")
                            end_date: str = Field(description="End date for transaction history (YYYY-MM-DD format)")
                            tenantId: str = Field(description="Tenant ID")
                            userId: str = Field(description="User ID")
                        
                        structured_tool = StructuredTool.from_function(
                            coroutine=transaction_history_func,
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
                        # Service request tool with proper wrapper function
                        def create_service_request_wrapper(client_ref):
                            async def service_request_wrapper(recipientPhone: str, recipientEmail: str, requestSummary: str, tenantId: str, userId: str):
                                """Service request wrapper with proper Pydantic signature"""
                                print(f"🚀 SERVICE_REQUEST_WRAPPER: Called with recipientPhone={recipientPhone}, recipientEmail={recipientEmail}")
                                kwargs = {
                                    "recipientPhone": recipientPhone,
                                    "recipientEmail": recipientEmail,
                                    "requestSummary": requestSummary,
                                    "tenantId": tenantId,
                                    "userId": userId
                                }
                                return await client_ref.call_tool("service_request", kwargs)
                            return service_request_wrapper
                        
                        service_request_func = create_service_request_wrapper(self)
                        
                        from pydantic import BaseModel, Field
                        
                        class ServiceRequestInput(BaseModel):
                            recipientPhone: str = Field(description="Phone number of the recipient for the service request")
                            recipientEmail: str = Field(description="Email address of the recipient for the service request")
                            requestSummary: str = Field(description="Summary description of the service request")
                            tenantId: str = Field(description="Tenant ID for the request")
                            userId: str = Field(description="User ID for the request")
                        
                        structured_tool = StructuredTool.from_function(
                            coroutine=service_request_func,
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
                        print(f"🔧 DEBUG: Using GENERIC tool creation for {tool_name} (no specific schema)")
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
        """Call a tool via HTTP or direct connection"""
        if self.use_remote:
            if not self.remote_client:
                if not await self.connect_to_server():
                    raise Exception("Could not connect to Remote MCP server")
            return await self.remote_client.call_tool(tool_name, arguments)
        else:
            if not self.local_server:
                if not await self.connect_to_server():
                    raise Exception("Could not connect to Local MCP server")
            
            # Inject context for Local calls
            context = get_mcp_context()
            tools_needing_context = ['bank_balance', 'bank_transfer', 'get_transaction_history', 'create_account', 'service_request']
            
            if tool_name in tools_needing_context:
                if context.get('tenantId') and 'tenantId' not in arguments:
                    arguments['tenantId'] = context['tenantId']
                if context.get('userId') and 'userId' not in arguments:
                    arguments['userId'] = context['userId']
                if context.get('thread_id') and 'thread_id' not in arguments:
                    arguments['thread_id'] = context['thread_id']
            
            return await self.local_server.call_tool_directly(tool_name, arguments)
    
    async def cleanup(self):
        """Clean up all connections"""
        print("🔄 ENHANCED MCP: Cleaning up MCP client...")
        
        if self.remote_client:
            await self.remote_client.cleanup()
            self.remote_client = None
        
        if self.local_server:
            self.local_server = None
        
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

```
</details>

### Step 2: Create Local MCP Server

Now open `src/app/tools/mcp_server.py` and replace the entire contents with the complete MCP server code.

> **Note**: The MCP server is quite large (1400+ lines) and includes all banking tools, Azure service connections, and performance optimizations. For this workshop, the complete file will be provided. Here's an overview of what it contains:

**MCP Server Features**:
- **11 Banking Tools**: balance, transfer, history, account creation, service requests, etc.
- **Agent Transfer Tools**: Seamless handoff between coordinator, customer support, transactions, and sales agents
- **Azure Integration**: Cached connections to Cosmos DB and OpenAI for optimal performance  
- **Multi-tenant Security**: All operations require tenantId and userId
- **Performance Monitoring**: Execution timing and logging for all operations
- **Dual Mode Support**: Can run embedded (local) or standalone (HTTP server)

**The complete local MCP server code** is already provided in the `src/app/tools/mcp_server.py` file.

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
#from src.app.services.local_model import model  # Use local model for testing
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

# � Global persistent MCP client and cache
_persistent_mcp_client: Optional[MultiServerMCPClient] = None
_mcp_tools_cache: Optional[List] = None
_native_tools_fallback_enabled = True  # 🚀 NEW: Enable native fallback for performance

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
                
                async def close(self):
                    await self.shared_client.cleanup()
            
            _persistent_mcp_client = SharedMCPClientWrapper(_shared_mcp_client)
            
            setup_duration = (time.time() - start_time) * 1000
            print(f"✅ SHARED MCP client initialized in {setup_duration:.2f}ms")
            print(f"🛠️  Cached {len(_mcp_tools_cache)} tools for reuse")
            
            # Log cached tools
            print("[DEBUG] Cached tools from SHARED MCP server:")
            for tool in _mcp_tools_cache:
                tool_name = tool.get('name') if isinstance(tool, dict) else getattr(tool, 'name', 'unknown')
                print("  -", tool_name)
                
        except Exception as e:
            print(f"❌ Failed to initialize SHARED MCP client: {e}")
            raise Exception("Failed to initialize MCP client")
    
    return _persistent_mcp_client, _mcp_tools_cache

async def setup_agents():
    global coordinator_agent, customer_support_agent, transactions_agent, sales_agent
    
    print("🔧 SETUP: Setting up agents with fresh MCP client...")
    
    # Clear all caches when explicitly recreating (e.g., after module reload)
    global _persistent_mcp_client, _shared_mcp_client
    _persistent_mcp_client = None
    _shared_mcp_client = None
    _mcp_tools_cache = None
    coordinator_agent = None
    customer_support_agent = None
    
    # 🔧 CRITICAL FIX: Also clear the SharedMCPClient's global cache
    from src.app.tools.mcp_client import cleanup_shared_mcp_client
    await cleanup_shared_mcp_client()
    print("🔧 SETUP: Cleared SharedMCPClient global cache")  
    transactions_agent = None
    sales_agent = None
    print("🔧 CLEARED: All agent and MCP client caches cleared for fresh setup")

    print("Setting up agents with persistent MCP client...")
    
    # Get persistent client and cached tools
    _shared_mcp_client = None
    print("🔧 DEBUG: Cleared MCP client cache - forcing tool regeneration")
    
    mcp_client, all_tools = await get_persistent_mcp_client()

    # Assign tools to agents based on tool name prefix
    coordinator_tools = filter_tools_by_prefix(all_tools, ["transfer_to_"])
    support_tools = filter_tools_by_prefix(all_tools, ["service_request", "get_branch_location", "transfer_to_sales_agent", "transfer_to_transactions_agent"])
    sales_tools = filter_tools_by_prefix(all_tools, ["get_offer_information", "create_account", "calculate_monthly_payment", "transfer_to_customer_support_agent", "transfer_to_transactions_agent"])
    transactions_tools = filter_tools_by_prefix(all_tools, ["bank_transfer", "get_transaction_history", "bank_balance", "transfer_to_customer_support_agent"])

    # Debug: Print tool information for transactions agent
    print(f"🔧 DEBUG: Transactions agent tools ({len(transactions_tools)} total):")
    for tool in transactions_tools:
        tool_name = getattr(tool, 'name', 'UNKNOWN')
        tool_args = getattr(tool, 'args_schema', None)
        if tool_args:
            print(f"  - {tool_name}: {tool_args.__name__} schema with fields {list(tool_args.__fields__.keys())}")
        else:
            print(f"  - {tool_name}: No args schema")

    # Create agents with their respective tools
    coordinator_agent = create_react_agent(model, coordinator_tools, state_modifier=load_prompt("coordinator_agent"))
    customer_support_agent = create_react_agent(model, support_tools, state_modifier=load_prompt("customer_support_agent"))
    sales_agent = create_react_agent(model, sales_tools, state_modifier=load_prompt("sales_agent"))
    transactions_agent = create_react_agent(model, transactions_tools, state_modifier=load_prompt("transactions_agent"))

@traceable(run_type="llm")
async def call_coordinator_agent(state: MessagesState, config) -> Command[Literal["coordinator_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")

    print(f"Calling coordinator agent with Thread ID: {thread_id}")

    try:
        activeAgent = chat_container.read_item(item=thread_id, partition_key=[tenantId, userId, thread_id]).get(
            'activeAgent', 'unknown')
    except Exception as e:
        logging.debug(f"No active agent found: {e}")
        activeAgent = None

    if activeAgent is None:
        if local_interactive_mode:
            update_chat_container({
                "id": thread_id,
                "tenantId": "cli-test",
                "userId": "cli-test",
                "sessionId": thread_id,
                "name": "cli-test",
                "age": "cli-test",
                "address": "cli-test",
                "activeAgent": "unknown",
                "ChatName": "cli-test",
                "messages": []
            })

    print(f"Active agent from point lookup: {activeAgent}")

    if activeAgent not in [None, "unknown", "coordinator_agent"]:
        print(f"Routing straight to last active agent: {activeAgent}")
        return Command(update=state, goto=activeAgent)
    else:
        response = await coordinator_agent.ainvoke(state)
        
        # Check if any tool responses indicate a transfer request
        transfer_target = None
        print(f"🔧 DEBUG: Checking response for transfer requests")
        print(f"🔧 DEBUG: Response type: {type(response)}")
        print(f"🔧 DEBUG: Response contents: {response}")
        
        # Check if this is a LangGraph AddableValuesDict response
        if isinstance(response, dict):
            print(f"🔧 DEBUG: Response is dict with keys: {list(response.keys())}")
            # Look for messages in the response
            if 'messages' in response and response['messages']:
                print(f"🔧 DEBUG: Found {len(response['messages'])} messages in response dict")
                for i, message in enumerate(response['messages']):
                    print(f"🔧 DEBUG: Message {i}: type={type(message)}")
                    if hasattr(message, 'content'):
                        print(f"🔧 DEBUG: Message {i} content: {message.content}")
                        if isinstance(message.content, str) and message.content.strip():
                            try:
                                import json
                                # Try to parse JSON response
                                content_data = json.loads(message.content)
                                if content_data.get("goto"):
                                    transfer_target = content_data["goto"]
                                    print(f"🔄 COORDINATOR: Found JSON transfer in message content: {transfer_target}")
                                    break
                            except (json.JSONDecodeError, TypeError, AttributeError):
                                # Check for old format
                                if message.content.startswith("TRANSFER_REQUEST:"):
                                    transfer_target = message.content.split(":", 1)[1]
                                    print(f"� COORDINATOR: Found legacy transfer: {transfer_target}")
                                    break
                    
                    # Check if message has tool_calls
                    if hasattr(message, 'tool_calls') and message.tool_calls:
                        print(f"🔧 DEBUG: Message {i} has {len(message.tool_calls)} tool calls")
                        for j, tool_call in enumerate(message.tool_calls):
                            print(f"� DEBUG: Tool call {j}: {tool_call}")
            
            # Also check for any other relevant keys in response
            for key, value in response.items():
                if key != 'messages':
                    print(f"� DEBUG: Response[{key}]: {value} (type: {type(value)})")
        
        else:
            print(f"🔧 DEBUG: Response is not a dict")
        
        if transfer_target:
            return Command(update=response, goto=transfer_target)
        else:
            return Command(update=response, goto="human")


@traceable(run_type="llm")
async def call_customer_support_agent(state: MessagesState, config) -> Command[Literal["customer_support_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    if local_interactive_mode:
        patch_active_agent("cli-test", "cli-test", thread_id, "customer_support_agent")
    
    from langchain_core.messages import SystemMessage
    
    # Add system message with tenant/user context for the LLM to use when calling tools
    system_message = SystemMessage(content=f"When calling service_request tool, always include these parameters: tenantId='{tenantId}', userId='{userId}'")
    state["messages"].append(system_message)
    
    response = await customer_support_agent.ainvoke(state)
    
    # Remove the system message added above from response
    if isinstance(response, dict) and "messages" in response:
        response["messages"] = [
            msg for msg in response["messages"]
            if not isinstance(msg, SystemMessage)
        ]
    
    return Command(update=response, goto="human")


@traceable(run_type="llm")
async def call_sales_agent(state: MessagesState, config) -> Command[Literal["sales_agent", "human"]]:
    start_time = time.time()
    print("⏱️  LANGGRAPH: Starting sales agent execution")
    
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    if local_interactive_mode:
        patch_active_agent("cli-test", "cli-test", thread_id, "sales_agent")
    
    from langchain_core.messages import SystemMessage
    
    # Add system message with tenant/user context for the LLM to use when calling tools
    system_message = SystemMessage(content=f"When calling create_account tool, always include these parameters: tenantId='{tenantId}', userId='{userId}'")
    state["messages"].append(system_message)
    
    agent_start_time = time.time()
    response = await sales_agent.ainvoke(state, config)
    agent_duration_ms = (time.time() - agent_start_time) * 1000
    
    # Remove the system message added above from response
    if isinstance(response, dict) and "messages" in response:
        response["messages"] = [
            msg for msg in response["messages"]
            if not isinstance(msg, SystemMessage)
        ]
    
    total_duration_ms = (time.time() - start_time) * 1000
    print(f"⏱️  LANGGRAPH: Sales agent invoke took {agent_duration_ms:.2f}ms")
    print(f"⏱️  LANGGRAPH: Total sales agent call took {total_duration_ms:.2f}ms")
    
    return Command(update=response, goto="human")


@traceable(run_type="llm")
async def call_transactions_agent(state: MessagesState, config) -> Command[Literal["transactions_agent", "human"]]:
    # 🔧 SMART REFRESH: Only recreate if module was reloaded (--reload detected)
    global _agents_setup_version, _last_setup_time
    if _agents_setup_version != _module_load_time:
        print(f"🔧 DETECTED RELOAD: Module reloaded, refreshing agents (setup_version={_agents_setup_version}, module_load_time={_module_load_time})")
        # Clear SharedMCP cache before recreating agents
        from src.app.tools.mcp_client import cleanup_shared_mcp_client
        await cleanup_shared_mcp_client()
        await setup_agents()
        _agents_setup_version = _module_load_time
        _last_setup_time = time.time()
    
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    if local_interactive_mode:
        patch_active_agent("cli-test", "cli-test", thread_id, "transactions_agent")
    
    # Add system message with tenant/user context for the LLM to use when calling tools
    from langchain_core.messages import SystemMessage

    system_msg_content = f"IMPORTANT: When calling the bank_balance, bank_transfer, or get_transaction_history tools, you MUST always include these exact parameters: tenantId='{tenantId}', userId='{userId}', thread_id='{thread_id}'. Do not call these tools without all required parameters."
    print(f"🔧 DEBUG: Adding system message to transactions agent: {system_msg_content}")

    
    # Add as proper SystemMessage object 
    system_message = SystemMessage(content=system_msg_content)
    state["messages"].append(system_message)
    
    response = await transactions_agent.ainvoke(state, config)
    # explicitly remove the system message added above from response
    if isinstance(response, dict) and "messages" in response:
        response["messages"] = [
            msg for msg in response["messages"]
            if not isinstance(msg, SystemMessage)
        ]
    
    return Command(update=response, goto="human")


@traceable
def human_node(state: MessagesState, config) -> None:
    interrupt(value="Ready for user input.")
    return None

def get_active_agent(state: MessagesState, config) -> str:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    # print("DEBUG: get_active_agent called with state:", state)

    activeAgent = None

    # Search for last ToolMessage and try to extract `goto`
    for message in reversed(state['messages']):
        if isinstance(message, ToolMessage):
            try:
                content_json = json.loads(message.content)
                activeAgent = content_json.get("goto")
                if activeAgent:
                    print(f"DEBUG: Extracted activeAgent from ToolMessage: {activeAgent}")
                    break
            except Exception as e:
                print(f"DEBUG: Failed to parse ToolMessage content: {e}")

    # Fallback: Cosmos DB lookup if needed
    if not activeAgent:
        try:
            thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
            print(f"DEBUG: thread_id in get_active_agent: {thread_id}")
            activeAgent = chat_container.read_item(
                item=thread_id,
                partition_key=[tenantId, userId, thread_id]
            ).get('activeAgent', 'unknown')
            print(f"Active agent from DB fallback: {activeAgent}")
        except Exception as e:
            print(f"Error retrieving active agent from DB: {e}")
            activeAgent = "unknown"

    return activeAgent


builder = StateGraph(MessagesState)
builder.add_node("coordinator_agent", call_coordinator_agent)
builder.add_node("customer_support_agent", call_customer_support_agent)
builder.add_node("sales_agent", call_sales_agent)
builder.add_node("transactions_agent", call_transactions_agent)
builder.add_node("human", human_node)

builder.add_edge(START, "coordinator_agent")

builder.add_conditional_edges(
    "coordinator_agent",
    get_active_agent,
    {
        "sales_agent": "sales_agent",
        "transactions_agent": "transactions_agent",
        "customer_support_agent": "customer_support_agent",
        "coordinator_agent": "coordinator_agent",  # fallback
    }
)

checkpointer = CosmosDBSaver(database_name=DATABASE_NAME, container_name=checkpoint_container)
graph = builder.compile(checkpointer=checkpointer)

async def cleanup_persistent_mcp_client():
    """Properly shutdown the persistent MCP client and shared MCP client"""
    global _persistent_mcp_client, _shared_mcp_client
    
    print("🔄 Shutting down MCP clients...")
    
    # Clean up shared MCP client first (higher priority)
    if _shared_mcp_client:
        try:
            await _shared_mcp_client.cleanup()
            _shared_mcp_client = None
            print("✅ Shared MCP client cleaned up")
        except Exception as e:
            print(f"⚠️ Error cleaning up shared MCP client: {e}")
    
    # Then clean up persistent MCP client
    if _persistent_mcp_client:
        try:
            if hasattr(_persistent_mcp_client, 'close'):
                await _persistent_mcp_client.close()
            elif hasattr(_persistent_mcp_client, '__aenter__'):
                # If it's an async context manager, we might need different handling
                pass
            print("✅ Persistent MCP client shutdown complete")
        except Exception as e:
            print(f"⚠️  Error during MCP client shutdown: {e}")
        finally:
            _persistent_mcp_client = None


def interactive_chat():
    thread_config = {"configurable": {"thread_id": str(uuid.uuid4()), "userId": "Mark", "tenantId": "Contoso"}}
    global local_interactive_mode
    local_interactive_mode = True
    print("Welcome to the interactive multi-agent shopping assistant.")
    print("Type 'exit' to end the conversation.\n")

    user_input = input("You: ")

    while user_input.lower() != "exit":
        input_message = {"messages": [{"role": "user", "content": user_input}]}
        response_found = False

        for update in graph.stream(input_message, config=thread_config, stream_mode="updates"):
            for node_id, value in update.items():
                if isinstance(value, dict) and value.get("messages"):
                    last_message = value["messages"][-1]
                    if isinstance(last_message, AIMessage):
                        print(f"{node_id}: {last_message.content}\n")
                        response_found = True

        if not response_found:
            print("DEBUG: No AI response received.")

        user_input = input("You: ")


if __name__ == "__main__":
    if sys.platform == "win32":
        print("Setting up Windows-specific event loop policy...")
        # Set the event loop to ProactorEventLoop on Windows
        asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())
    asyncio.run(setup_agents())
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

Start the FastAPI web server:

```bash
# Activate the virtual environment and start the FastAPI server
source .venv/bin/activate
uvicorn src.app.banking_agents_api:app --reload --host 0.0.0.0 --port 63280
```

You should see MCP initialization output:

```
🚀 Initializing MCP Banking System...
🔧 ENHANCED MCP: Initializing in LOCAL mode
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
INFO:     Uvicorn running on http://0.0.0.0:63280 (Press CTRL+C to quit)
```

### Step 3: Start the Frontend Application

To test the complete system with the web interface, open a **new terminal** and start the Angular frontend:

```bash
# Navigate to the frontend directory
cd ../frontend

# Install dependencies (if not already done)
npm install

# Start the Angular development server
ng serve
```

The frontend will be available at: **http://localhost:4200**

> **Note**: The frontend automatically connects to the backend API on port 63280 through the proxy configuration.

### Step 4: Test Banking Operations

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

**2. MCP server `.env` file (`02_completed/mcpserver/.env`):**
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

The Remote MCP server is provided in the `02_completed/mcpserver/` directory. Open a **new terminal** and start it:

```bash
# Navigate to the mcpserver directory
cd ../../02_completed/mcpserver

# Activate virtual environment and install dependencies (if not already done)
source .venv/bin/activate
pip install -r requirements.txt

# Start the Remote MCP HTTP server using uvicorn with proper PYTHONPATH
PYTHONPATH=src python -m uvicorn src.mcp_http_server:app --host 0.0.0.0 --port 8080
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

In your **original terminal** (where you ran the application), start the FastAPI web server:

```bash
# Activate the virtual environment and start the FastAPI server
source .venv/bin/activate
uvicorn src.app.banking_agents_api:app --reload --host 0.0.0.0 --port 63280
```

You should see Remote MCP initialization output:

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
INFO:     Uvicorn running on http://0.0.0.0:63280 (Press CTRL+C to quit)
```

### Step 4: Start the Frontend Application

To test the complete system with the web interface, open a **third terminal** and start the Angular frontend:

```bash
# Navigate to the frontend directory (from the python directory)
cd ../frontend

# Install dependencies (if not already done)
npm install

# Start the Angular development server
ng serve
```

The frontend will be available at: **http://localhost:4200**

> **Note**: The frontend automatically connects to the backend API on port 63280 through the proxy configuration.

### Step 5: Test Remote MCP Operations

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