"""
Enhanced Security Configuration for Banking MCP Server

This module provides production-ready security enhancements including:
- Proper JWT token management with expiration
- Role-based access control (RBAC)
- Input validation and sanitization
- Rate limiting and audit logging
- Secure CORS configuration
"""

import os
import secrets
import hashlib
import time
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Any
from enum import Enum
import re
from pydantic import BaseModel, Field, validator

class UserRole(str, Enum):
    """User roles for RBAC"""
    ADMIN = "admin"
    CUSTOMER = "customer"
    AGENT = "agent"
    READ_ONLY = "read_only"

class SecurityConfig:
    """Security configuration settings"""
    
    # JWT Configuration
    JWT_SECRET = os.getenv("JWT_SECRET", "development-secret-key-change-in-production")
    JWT_ALGORITHM = "HS256"
    JWT_EXPIRATION_HOURS = int(os.getenv("JWT_EXPIRATION_HOURS", "2"))  # 2 hours for clock sync testing
    JWT_REFRESH_EXPIRATION_HOURS = int(os.getenv("JWT_REFRESH_EXPIRATION_HOURS", "24"))
    
    # Rate Limiting
    RATE_LIMIT_REQUESTS = int(os.getenv("RATE_LIMIT_REQUESTS", "100"))
    RATE_LIMIT_WINDOW_SECONDS = int(os.getenv("RATE_LIMIT_WINDOW_SECONDS", "60"))
    
    # CORS Settings
    ALLOWED_ORIGINS = os.getenv("ALLOWED_ORIGINS", "http://localhost:4200,http://localhost:3000,http://localhost:8000,http://localhost:8080").split(",")
    
    # Audit Logging
    ENABLE_AUDIT_LOGGING = os.getenv("ENABLE_AUDIT_LOGGING", "true").lower() == "true"
    
    # Input Validation
    MAX_STRING_LENGTH = int(os.getenv("MAX_STRING_LENGTH", "1000"))
    ALLOWED_ACCOUNT_NUMBER_PATTERN = r"^(Acc[0-9]+|[0-9]{10,20})$"
    ALLOWED_AMOUNT_PATTERN = r"^[0-9]+(\.[0-9]{1,2})?$"

class TokenData(BaseModel):
    """JWT token payload structure"""
    user_id: str
    tenant_id: str
    roles: List[UserRole]
    exp: int
    iat: int
    jti: str  # JWT ID for token revocation

class RefreshTokenRequest(BaseModel):
    """Refresh token request model"""
    refresh_token: str

class LoginRequest(BaseModel):
    """Login request model with validation"""
    username: str = Field(..., min_length=3, max_length=100)
    password: str = Field(..., min_length=8, max_length=128)
    tenant_id: str = Field(..., min_length=1, max_length=100)
    
    @validator('username')
    def validate_username(cls, v):
        # Allow alphanumeric, underscore, dot, hyphen
        if not re.match(r'^[a-zA-Z0-9._-]+$', v):
            raise ValueError('Username contains invalid characters')
        return v
    
    @validator('tenant_id')
    def validate_tenant_id(cls, v):
        # UUID format or alphanumeric
        if not re.match(r'^[a-zA-Z0-9-]+$', v):
            raise ValueError('Tenant ID contains invalid characters')
        return v

class SecureToolCallRequest(BaseModel):
    """Enhanced tool call request with security validation"""
    tool_name: str = Field(..., min_length=1, max_length=100)
    arguments: Dict[str, Any] = Field(...)
    tenant_id: Optional[str] = Field(None, max_length=100)
    user_id: Optional[str] = Field(None, max_length=100)
    thread_id: Optional[str] = Field(None, max_length=100)
    
    @validator('tool_name')
    def validate_tool_name(cls, v):
        # Only allow alphanumeric and underscore
        if not re.match(r'^[a-zA-Z0-9_]+$', v):
            raise ValueError('Tool name contains invalid characters')
        return v
    
    @validator('arguments')
    def validate_arguments(cls, v):
        # Sanitize arguments
        return sanitize_dict(v)

def sanitize_string(value: str) -> str:
    """Sanitize string input to prevent injection attacks"""
    if not isinstance(value, str):
        return str(value)
    
    # Remove potentially dangerous characters
    # Allow alphanumeric, spaces, and common punctuation
    sanitized = re.sub(r'[<>"\';\\]', '', value)
    
    # Limit length
    if len(sanitized) > SecurityConfig.MAX_STRING_LENGTH:
        sanitized = sanitized[:SecurityConfig.MAX_STRING_LENGTH]
    
    return sanitized.strip()

def sanitize_dict(data: Dict[str, Any]) -> Dict[str, Any]:
    """Recursively sanitize dictionary values"""
    sanitized = {}
    for key, value in data.items():
        # Sanitize key
        clean_key = sanitize_string(key)
        
        if isinstance(value, str):
            sanitized[clean_key] = sanitize_string(value)
        elif isinstance(value, dict):
            sanitized[clean_key] = sanitize_dict(value)
        elif isinstance(value, list):
            sanitized[clean_key] = [
                sanitize_string(item) if isinstance(item, str) 
                else sanitize_dict(item) if isinstance(item, dict)
                else item
                for item in value
            ]
        else:
            sanitized[clean_key] = value
    
    return sanitized

def validate_account_number(account_number: str) -> bool:
    """Validate account number format"""
    return bool(re.match(SecurityConfig.ALLOWED_ACCOUNT_NUMBER_PATTERN, account_number))

def validate_amount(amount: str) -> bool:
    """Validate monetary amount format"""
    return bool(re.match(SecurityConfig.ALLOWED_AMOUNT_PATTERN, amount))

def hash_password(password: str) -> str:
    """Hash password using PBKDF2 with salt"""
    salt = secrets.token_hex(32)
    pwdhash = hashlib.pbkdf2_hmac('sha256', password.encode('utf-8'), salt.encode('utf-8'), 100000)
    return salt + pwdhash.hex()

def verify_password(password: str, hashed: str) -> bool:
    """Verify password against hash"""
    salt = hashed[:64]
    stored_hash = hashed[64:]
    pwdhash = hashlib.pbkdf2_hmac('sha256', password.encode('utf-8'), salt.encode('utf-8'), 100000)
    return pwdhash.hex() == stored_hash

class RateLimiter:
    """Simple in-memory rate limiter"""
    
    def __init__(self):
        self.requests = {}
    
    def is_allowed(self, client_id: str) -> bool:
        """Check if request is allowed based on rate limits"""
        now = time.time()
        window_start = now - SecurityConfig.RATE_LIMIT_WINDOW_SECONDS
        
        # Clean old entries
        if client_id in self.requests:
            self.requests[client_id] = [
                req_time for req_time in self.requests[client_id] 
                if req_time > window_start
            ]
        else:
            self.requests[client_id] = []
        
        # Check if under limit
        if len(self.requests[client_id]) >= SecurityConfig.RATE_LIMIT_REQUESTS:
            return False
        
        # Add current request
        self.requests[client_id].append(now)
        return True

class AuditLogger:
    """Audit logging for security events"""
    
    @staticmethod
    def log_authentication(user_id: str, tenant_id: str, success: bool, ip_address: str = None):
        """Log authentication attempt"""
        if not SecurityConfig.ENABLE_AUDIT_LOGGING:
            return
            
        event = {
            "timestamp": datetime.utcnow().isoformat(),
            "event_type": "authentication",
            "user_id": user_id,
            "tenant_id": tenant_id,
            "success": success,
            "ip_address": ip_address
        }
        print(f"🔍 AUDIT: {event}")
    
    @staticmethod
    def log_tool_call(user_id: str, tenant_id: str, tool_name: str, success: bool, ip_address: str = None):
        """Log tool call"""
        if not SecurityConfig.ENABLE_AUDIT_LOGGING:
            return
            
        event = {
            "timestamp": datetime.utcnow().isoformat(),
            "event_type": "tool_call",
            "user_id": user_id,
            "tenant_id": tenant_id,
            "tool_name": tool_name,
            "success": success,
            "ip_address": ip_address
        }
        print(f"🔍 AUDIT: {event}")
    
    @staticmethod
    def log_security_event(event_type: str, details: Dict[str, Any], ip_address: str = None):
        """Log security event"""
        if not SecurityConfig.ENABLE_AUDIT_LOGGING:
            return
            
        event = {
            "timestamp": datetime.utcnow().isoformat(),
            "event_type": f"security_{event_type}",
            "details": details,
            "ip_address": ip_address
        }
        print(f"🚨 SECURITY: {event}")

# Tool permissions mapping
TOOL_PERMISSIONS = {
    "bank_balance": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN],
    "bank_transfer": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN],
    "get_transaction_history": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN],
    "get_offer_information": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN, UserRole.READ_ONLY],
    "service_request": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN],
    "get_branch_location": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN, UserRole.READ_ONLY],
    "calculate_monthly_payment": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN, UserRole.READ_ONLY],
    "create_account": [UserRole.AGENT, UserRole.ADMIN],
    "transfer_to_sales_agent": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN, UserRole.READ_ONLY],
    "transfer_to_customer_support_agent": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN, UserRole.READ_ONLY],
    "transfer_to_transactions_agent": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN, UserRole.READ_ONLY],
    # Legacy tool names for backward compatibility
    "account_summary": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN],
    "transaction_history": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN],
    "product_search": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN, UserRole.READ_ONLY],
    "get_account_info": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN],
    "freeze_account": [UserRole.AGENT, UserRole.ADMIN],
    "unfreeze_account": [UserRole.AGENT, UserRole.ADMIN],
    "update_contact_info": [UserRole.CUSTOMER, UserRole.AGENT, UserRole.ADMIN],
}

def check_tool_permission(tool_name: str, user_roles: List[UserRole]) -> bool:
    """Check if user has permission to call the tool"""
    required_roles = TOOL_PERMISSIONS.get(tool_name, [])
    if not required_roles:
        return False
    
    return any(role in required_roles for role in user_roles)