"""
Configuration management for the Food Analysis API.
Loads settings from environment variables.
"""

from functools import lru_cache
from pydantic import AliasChoices, Field
from pydantic_settings import BaseSettings
from pydantic_settings import SettingsConfigDict


class Settings(BaseSettings):
    """Application settings loaded from environment variables."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    # API Security
    api_key: str = Field(
        default="",
        validation_alias=AliasChoices("ARETE_API_KEY", "SACRIFICE_API_KEY"),
    )

    # GCP / Vertex AI Configuration
    gcp_project_id: str = Field(
        default="steam-378309",
        validation_alias=AliasChoices("ARETE_GCP_PROJECT_ID", "SACRIFICE_GCP_PROJECT_ID"),
    )
    gcp_location: str = Field(
        default="europe-west2",
        validation_alias=AliasChoices("ARETE_GCP_LOCATION", "SACRIFICE_GCP_LOCATION"),
    )
    gemini_model: str = Field(
        default="gemini-2.5-flash",
        validation_alias=AliasChoices("ARETE_GEMINI_MODEL", "SACRIFICE_GEMINI_MODEL"),
    )
    swarm_model: str = Field(
        default="gemini-3-flash",
        validation_alias=AliasChoices("ARETE_SWARM_MODEL", "SACRIFICE_SWARM_MODEL"),
    )
    swarm_retry_model: str = Field(
        default="gemini-2.0-flash",
        validation_alias=AliasChoices("ARETE_SWARM_RETRY_MODEL", "SACRIFICE_SWARM_RETRY_MODEL"),
    )
    swarm_retry_count: int = Field(
        default=1,
        validation_alias=AliasChoices("ARETE_SWARM_RETRY_COUNT", "SACRIFICE_SWARM_RETRY_COUNT"),
    )
    swarm_max_tokens: int = Field(
        default=300,
        validation_alias=AliasChoices("ARETE_SWARM_MAX_TOKENS", "SACRIFICE_SWARM_MAX_TOKENS"),
    )
    swarm_temperature: float = Field(
        default=0.7,
        validation_alias=AliasChoices("ARETE_SWARM_TEMPERATURE", "SACRIFICE_SWARM_TEMPERATURE"),
    )

    # Server Configuration
    host: str = Field(
        default="0.0.0.0",
        validation_alias=AliasChoices("ARETE_HOST", "SACRIFICE_HOST"),
    )
    port: int = Field(
        default=8080,
        validation_alias=AliasChoices("ARETE_PORT", "SACRIFICE_PORT"),
    )
    debug: bool = Field(
        default=False,
        validation_alias=AliasChoices("ARETE_DEBUG", "SACRIFICE_DEBUG"),
    )


@lru_cache()
def get_settings() -> Settings:
    """Get cached settings instance."""
    return Settings()
