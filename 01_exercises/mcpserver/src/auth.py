"""
Enhanced JWT Token Management

Provides secure JWT token creation, validation, and refresh functionality
with proper expiration handling and token revocation support.
"""

import jwt
import secrets
import time
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Set
from jwt import PyJWTError
from fastapi import HTTPException, status

from security import SecurityConfig, UserRole, TokenData, AuditLogger

class TokenManager:
    """Manages JWT tokens with security best practices"""
    
    def __init__(self):
        self.revoked_tokens: Set[str] = set()  # In production, use Redis or database
        self.refresh_tokens: Dict[str, Dict] = {}  # In production, use secure storage
    
    def create_access_token(self, user_id: str, tenant_id: str, roles: List[UserRole]) -> str:
        """Create a JWT access token with expiration"""
        now = datetime.utcnow()
        expiration = now + timedelta(hours=SecurityConfig.JWT_EXPIRATION_HOURS)
        
        # Generate unique JWT ID for revocation
        jti = secrets.token_urlsafe(32)
        
        payload = {
            "user_id": user_id,
            "tenant_id": tenant_id,
            "roles": [role.value for role in roles],
            "exp": int(expiration.timestamp()),
            "iat": int(now.timestamp()),
            "jti": jti,
            "token_type": "access"
        }
        
        token = jwt.encode(payload, SecurityConfig.JWT_SECRET, algorithm=SecurityConfig.JWT_ALGORITHM)
        return token
    
    def create_refresh_token(self, user_id: str, tenant_id: str) -> str:
        """Create a refresh token for token renewal"""
        now = datetime.utcnow()
        expiration = now + timedelta(hours=SecurityConfig.JWT_REFRESH_EXPIRATION_HOURS)
        
        refresh_token = secrets.token_urlsafe(64)
        
        # Store refresh token (in production, use secure database)
        self.refresh_tokens[refresh_token] = {
            "user_id": user_id,
            "tenant_id": tenant_id,
            "expires_at": expiration,
            "created_at": now
        }
        
        return refresh_token
    
    def verify_token(self, token: str) -> TokenData:
        """Verify and decode JWT token"""
        try:
            # Decode without verification first to get JTI
            unverified_payload = jwt.decode(token, options={"verify_signature": False})
            jti = unverified_payload.get("jti")
            
            # Check if token is revoked
            if jti and jti in self.revoked_tokens:
                raise HTTPException(
                    status_code=status.HTTP_401_UNAUTHORIZED,
                    detail="Token has been revoked",
                    headers={"WWW-Authenticate": "Bearer"},
                )
            
            # Verify token signature and expiration (with clock skew tolerance)
            payload = jwt.decode(
                token, 
                SecurityConfig.JWT_SECRET, 
                algorithms=[SecurityConfig.JWT_ALGORITHM],
                leeway=3600  # Allow 1 hour of clock skew
            )
            
            # Validate token type
            if payload.get("token_type") != "access":
                raise HTTPException(
                    status_code=status.HTTP_401_UNAUTHORIZED,
                    detail="Invalid token type",
                    headers={"WWW-Authenticate": "Bearer"},
                )
            
            # Create TokenData object
            token_data = TokenData(
                user_id=payload["user_id"],
                tenant_id=payload["tenant_id"],
                roles=[UserRole(role) for role in payload["roles"]],
                exp=payload["exp"],
                iat=payload["iat"],
                jti=payload["jti"]
            )
            
            return token_data
            
        except jwt.ExpiredSignatureError:
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Token has expired",
                headers={"WWW-Authenticate": "Bearer"},
            )
        except PyJWTError as e:
            AuditLogger.log_security_event("invalid_token", {"error": str(e)})
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Invalid authentication credentials",
                headers={"WWW-Authenticate": "Bearer"},
            )
    
    def refresh_access_token(self, refresh_token: str) -> tuple[str, str]:
        """Refresh access token using refresh token"""
        # Check if refresh token exists and is valid
        if refresh_token not in self.refresh_tokens:
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Invalid refresh token"
            )
        
        token_data = self.refresh_tokens[refresh_token]
        
        # Check if refresh token is expired
        if datetime.utcnow() > token_data["expires_at"]:
            # Clean up expired token
            del self.refresh_tokens[refresh_token]
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Refresh token has expired"
            )
        
        # Get user roles (in production, fetch from database)
        user_roles = [UserRole.CUSTOMER]  # Default role, should be fetched from user service
        
        # Create new access token
        new_access_token = self.create_access_token(
            token_data["user_id"],
            token_data["tenant_id"],
            user_roles
        )
        
        # Create new refresh token and invalidate old one
        new_refresh_token = self.create_refresh_token(
            token_data["user_id"],
            token_data["tenant_id"]
        )
        
        # Remove old refresh token
        del self.refresh_tokens[refresh_token]
        
        return new_access_token, new_refresh_token
    
    def revoke_token(self, jti: str):
        """Revoke a token by adding its JTI to revoked set"""
        self.revoked_tokens.add(jti)
        AuditLogger.log_security_event("token_revoked", {"jti": jti})
    
    def cleanup_expired_tokens(self):
        """Clean up expired refresh tokens (should be run periodically)"""
        now = datetime.utcnow()
        expired_tokens = [
            token for token, data in self.refresh_tokens.items()
            if now > data["expires_at"]
        ]
        
        for token in expired_tokens:
            del self.refresh_tokens[token]
        
        if expired_tokens:
            AuditLogger.log_security_event("tokens_cleaned", {"count": len(expired_tokens)})

# Global token manager instance
token_manager = TokenManager()