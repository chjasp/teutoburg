"""
Configuration management for the Food Analysis API.
Loads settings from environment variables.
"""

import os
from functools import lru_cache
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    """Application settings loaded from environment variables."""
    
    # API Security
    api_key: str = ""  # Bearer token for authenticating Unity requests
    
    # GCP / Vertex AI Configuration
    gcp_project_id: str = "steam-378309"  # Your GCP project for billing
    gcp_location: str = "europe-west2"  # Vertex AI region
    gemini_model: str = "gemini-2.5-flash"  # Vertex AI model name
    
    # Server Configuration
    host: str = "0.0.0.0"
    port: int = 8080
    debug: bool = False
    
    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"
        # Prefix for environment variables (e.g., SACRIFICE_API_KEY)
        env_prefix = "SACRIFICE_"


@lru_cache()
def get_settings() -> Settings:
    """Get cached settings instance."""
    return Settings()
