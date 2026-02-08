"""
Sacrifice Food Analysis API.
FastAPI server that analyzes food images using Gemini Vision.
"""

import base64
import binascii
import logging
import time
from typing import Optional

from fastapi import Depends, FastAPI, File, Header, HTTPException, UploadFile
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

from .config import get_settings
from .gemini_analyzer import get_analyzer

logger = logging.getLogger(__name__)


app = FastAPI(
    title="Sacrifice Food Analysis API",
    description="Analyzes food images and returns a health score for the Axiom game.",
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


class AnalyzeRequest(BaseModel):
    """Request body for image analysis via base64."""

    image_base64: str
    mime_type: str = "image/jpeg"


class AnalyzeResponse(BaseModel):
    """Response from food analysis."""

    score: float
    category: str
    reasoning: str


class HealthResponse(BaseModel):
    """Health check response."""

    status: str
    version: str


class SwarmStrategizeRequest(BaseModel):
    """Request body for swarm strategist LLM calls."""

    system_prompt: str
    snapshot_json: str
    model: Optional[str] = None
    max_tokens: int = 300
    temperature: float = 0.7


class SwarmStrategizeResponse(BaseModel):
    """Response body for swarm strategist LLM calls."""

    raw_text: str
    model: str
    provider: str
    latency_ms: int


def _verify_api_key_header(authorization: Optional[str]) -> None:
    settings = get_settings()

    # Development mode: auth disabled when key is not configured.
    if not settings.api_key:
        return

    if not authorization:
        raise HTTPException(status_code=401, detail="Missing Authorization header")

    parts = authorization.split(" ", 1)
    if len(parts) != 2 or parts[0].lower() != "bearer":
        raise HTTPException(status_code=401, detail="Invalid Authorization format. Use: Bearer <token>")

    if parts[1] != settings.api_key:
        raise HTTPException(status_code=401, detail="Invalid API key")


async def require_api_key(authorization: Optional[str] = Header(None)) -> None:
    _verify_api_key_header(authorization)


def _parse_base64_image(payload: str) -> bytes:
    try:
        return base64.b64decode(payload)
    except (binascii.Error, ValueError) as exc:
        raise HTTPException(status_code=400, detail=f"Invalid base64 image: {exc}") from exc


def _validate_upload(upload_file: UploadFile) -> None:
    if upload_file.content_type not in {"image/jpeg", "image/jpg", "image/png"}:
        raise HTTPException(
            status_code=400,
            detail=f"Invalid file type: {upload_file.content_type}. Use JPEG or PNG.",
        )


async def _run_analysis(image_bytes: bytes, mime_type: str) -> AnalyzeResponse:
    analyzer = get_analyzer()
    result = await analyzer.analyze_image(image_bytes, mime_type)
    return AnalyzeResponse(**result)


@app.get("/health", response_model=HealthResponse)
async def health_check() -> HealthResponse:
    return HealthResponse(status="healthy", version="1.0.0")


@app.post("/analyze", response_model=AnalyzeResponse)
async def analyze_food_base64(
    request: AnalyzeRequest,
    _auth: None = Depends(require_api_key),
) -> AnalyzeResponse:
    image_bytes = _parse_base64_image(request.image_base64)
    return await _run_analysis(image_bytes, request.mime_type)


@app.post("/analyze/upload", response_model=AnalyzeResponse)
async def analyze_food_upload(
    file: UploadFile = File(...),
    _auth: None = Depends(require_api_key),
) -> AnalyzeResponse:
    _validate_upload(file)

    image_bytes = await file.read()
    if len(image_bytes) == 0:
        raise HTTPException(status_code=400, detail="Empty file uploaded")

    if len(image_bytes) > 10 * 1024 * 1024:
        raise HTTPException(status_code=400, detail="File too large. Maximum 10MB.")

    return await _run_analysis(image_bytes, file.content_type)


@app.post("/swarm/strategize", response_model=SwarmStrategizeResponse)
async def strategize_swarm(
    request: SwarmStrategizeRequest,
    _auth: None = Depends(require_api_key),
) -> SwarmStrategizeResponse:
    analyzer = get_analyzer()

    started_at = time.perf_counter()
    try:
        result = await analyzer.strategize_swarm(
            system_prompt=request.system_prompt,
            snapshot_json=request.snapshot_json,
            model_name=request.model,
            max_tokens=request.max_tokens,
            temperature=request.temperature,
        )
    except Exception as exc:
        logger.exception("Swarm strategize endpoint failed")
        raise HTTPException(status_code=502, detail=f"Swarm strategize failed: {exc}") from exc

    latency_ms = int((time.perf_counter() - started_at) * 1000)
    return SwarmStrategizeResponse(
        raw_text=result.get("raw_text", ""),
        model=result.get("model", request.model or get_settings().swarm_model),
        provider=result.get("provider", "vertex_gemini"),
        latency_ms=max(0, latency_ms),
    )


if __name__ == "__main__":
    import uvicorn

    settings = get_settings()
    logger.info("Starting API server on %s:%s", settings.host, settings.port)
    uvicorn.run(
        "app.main:app",
        host=settings.host,
        port=settings.port,
        reload=settings.debug,
    )
