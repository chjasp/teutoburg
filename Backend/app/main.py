"""
Sacrifice Food Analysis API
FastAPI server that analyzes food images using Gemini Vision.
"""

import base64
from typing import Optional

from fastapi import FastAPI, HTTPException, Header, UploadFile, File
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

from .config import get_settings
from .gemini_analyzer import get_analyzer


# Initialize FastAPI app
app = FastAPI(
    title="Sacrifice Food Analysis API",
    description="Analyzes food images and returns a health score for the Axiom game.",
    version="1.0.0"
)

# Add CORS middleware for local development
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Restrict in production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# Request/Response Models
class AnalyzeRequest(BaseModel):
    """Request body for image analysis via base64."""
    image_base64: str
    mime_type: str = "image/jpeg"


class AnalyzeResponse(BaseModel):
    """Response from food analysis."""
    score: float  # 0.0 (unhealthy) to 1.0 (healthy)
    category: str  # excellent, good, moderate, poor, unhealthy
    reasoning: str  # Brief explanation


class HealthResponse(BaseModel):
    """Health check response."""
    status: str
    version: str


def verify_api_key(authorization: Optional[str] = Header(None)) -> bool:
    """Verify the API key from Authorization header."""
    settings = get_settings()
    
    # If no API key is configured, allow all requests (dev mode)
    if not settings.api_key:
        return True
    
    if not authorization:
        raise HTTPException(status_code=401, detail="Missing Authorization header")
    
    # Expect "Bearer <token>" format
    parts = authorization.split(" ")
    if len(parts) != 2 or parts[0].lower() != "bearer":
        raise HTTPException(status_code=401, detail="Invalid Authorization format. Use: Bearer <token>")
    
    if parts[1] != settings.api_key:
        raise HTTPException(status_code=401, detail="Invalid API key")
    
    return True


@app.get("/health", response_model=HealthResponse)
async def health_check():
    """Health check endpoint for Cloud Run."""
    return HealthResponse(status="healthy", version="1.0.0")


@app.post("/analyze", response_model=AnalyzeResponse)
async def analyze_food_base64(
    request: AnalyzeRequest,
    authorization: Optional[str] = Header(None)
):
    """
    Analyze a food image sent as base64.
    
    Returns a health score from 0.0 (unhealthy) to 1.0 (healthy).
    """
    verify_api_key(authorization)
    
    try:
        # Decode base64 image
        image_bytes = base64.b64decode(request.image_base64)
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Invalid base64 image: {str(e)}")
    
    # Analyze the image
    analyzer = get_analyzer()
    result = await analyzer.analyze_image(image_bytes, request.mime_type)
    
    return AnalyzeResponse(**result)


@app.post("/analyze/upload", response_model=AnalyzeResponse)
async def analyze_food_upload(
    file: UploadFile = File(...),
    authorization: Optional[str] = Header(None)
):
    """
    Analyze a food image uploaded as multipart form data.
    
    Returns a health score from 0.0 (unhealthy) to 1.0 (healthy).
    """
    verify_api_key(authorization)
    
    # Validate file type
    if file.content_type not in ["image/jpeg", "image/png", "image/jpg"]:
        raise HTTPException(
            status_code=400, 
            detail=f"Invalid file type: {file.content_type}. Use JPEG or PNG."
        )
    
    # Read image bytes
    image_bytes = await file.read()
    
    if len(image_bytes) == 0:
        raise HTTPException(status_code=400, detail="Empty file uploaded")
    
    if len(image_bytes) > 10 * 1024 * 1024:  # 10MB limit
        raise HTTPException(status_code=400, detail="File too large. Maximum 10MB.")
    
    # Analyze the image
    analyzer = get_analyzer()
    result = await analyzer.analyze_image(image_bytes, file.content_type)
    
    return AnalyzeResponse(**result)


if __name__ == "__main__":
    import uvicorn
    settings = get_settings()
    uvicorn.run(
        "app.main:app",
        host=settings.host,
        port=settings.port,
        reload=settings.debug
    )
