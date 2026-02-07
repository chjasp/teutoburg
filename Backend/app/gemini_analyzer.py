"""
Gemini Vision integration for analyzing food images.
Uses GCP Vertex AI with project-based authentication (Application Default Credentials).
Returns a health score from 0.0 (unhealthy) to 1.0 (healthy).
"""

import json
import logging
from typing import Optional

from google import genai
from google.genai import types

from .config import get_settings

logger = logging.getLogger(__name__)


ANALYSIS_PROMPT = """You are a nutritionist AI that analyzes food images.

Analyze the food shown in this image and rate it on a health scale.

Return ONLY a JSON object with this exact structure:
{
    "score": <float between 0.0 and 1.0>,
    "category": "<one of: excellent, good, moderate, poor, unhealthy>",
    "reasoning": "<brief 1-2 sentence explanation>"
}

Scoring guidelines:
- 0.9-1.0 (excellent): Fresh vegetables, fruits, lean proteins, whole grains, salads
- 0.7-0.89 (good): Balanced meals, home-cooked food, moderate portions
- 0.5-0.69 (moderate): Some processed elements, restaurant food, mixed nutrition
- 0.3-0.49 (poor): Fast food, fried items, high sugar/salt content
- 0.0-0.29 (unhealthy): Heavily processed, junk food, excessive portions

If no food is visible in the image, return:
{
    "score": 0.0,
    "category": "invalid",
    "reasoning": "No food detected in the image."
}

Remember: Return ONLY the JSON object, no additional text."""


class FoodAnalyzer:
    """Analyzes food images using Google Gemini via Vertex AI."""

    def __init__(self):
        settings = get_settings()
        self.client = genai.Client(
            vertexai=True,
            project=settings.gcp_project_id,
            location=settings.gcp_location,
        )
        self.model_name = settings.gemini_model

        logger.info(
            "FoodAnalyzer initialized (project=%s, location=%s, model=%s)",
            settings.gcp_project_id,
            settings.gcp_location,
            self.model_name,
        )

    async def analyze_image(self, image_bytes: bytes, mime_type: str = "image/jpeg") -> dict:
        if not image_bytes:
            return _error_result("No image data received")

        try:
            image_part = types.Part.from_bytes(data=image_bytes, mime_type=mime_type)
            response = self.client.models.generate_content(
                model=self.model_name,
                contents=[ANALYSIS_PROMPT, image_part],
                config=types.GenerateContentConfig(
                    temperature=0.1,
                    max_output_tokens=1024,
                ),
            )

            response_text = self._extract_response_text(response)
            if not response_text:
                return _error_result("Empty response from Gemini API")

            return self._parse_response(response_text)
        except Exception as exc:
            logger.exception("Food analysis failed")
            return _error_result(f"Analysis failed: {exc}")

    def _extract_response_text(self, response) -> Optional[str]:
        if response is None:
            return None

        try:
            text = getattr(response, "text", None)
            if text:
                return text
        except Exception:
            logger.debug("Failed to read response.text", exc_info=True)

        try:
            candidates = getattr(response, "candidates", None)
            if candidates:
                first_candidate = candidates[0]
                content = getattr(first_candidate, "content", None)
                parts = getattr(content, "parts", None) if content is not None else None
                if parts:
                    for part in parts:
                        part_text = getattr(part, "text", None)
                        if part_text:
                            return part_text
        except Exception:
            logger.debug("Failed to parse candidates content", exc_info=True)

        return None

    def _parse_response(self, response_text: str) -> dict:
        json_payload = _extract_json_payload(response_text)
        if json_payload is None:
            return _error_result("Could not parse LLM response", category="parse_error")

        try:
            parsed = json.loads(json_payload)
        except json.JSONDecodeError:
            logger.warning("Invalid JSON payload from LLM: %s", response_text[:240])
            return _error_result("Could not parse LLM response", category="parse_error")

        try:
            score = float(parsed.get("score", 0.5))
        except (TypeError, ValueError):
            score = 0.5

        score = max(0.0, min(1.0, score))
        category = str(parsed.get("category", "unknown"))
        reasoning = str(parsed.get("reasoning", ""))

        return {
            "score": score,
            "category": category,
            "reasoning": reasoning,
        }


def _extract_json_payload(raw_text: str) -> Optional[str]:
    if not raw_text:
        return None

    text = raw_text.strip()

    # If the model returned markdown code fences, strip them first.
    if text.startswith("```"):
        first_newline = text.find("\n")
        if first_newline >= 0:
            text = text[first_newline + 1 :]
        fence_end = text.rfind("```")
        if fence_end >= 0:
            text = text[:fence_end].strip()

    # Fast path: whole payload is JSON.
    if text.startswith("{") and text.endswith("}"):
        return text

    # Fallback: capture first balanced JSON object.
    start = text.find("{")
    if start < 0:
        return None

    depth = 0
    for idx in range(start, len(text)):
        ch = text[idx]
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return text[start : idx + 1]

    return None


def _error_result(reasoning: str, category: str = "error") -> dict:
    return {
        "score": 0.5,
        "category": category,
        "reasoning": reasoning,
    }


_analyzer: Optional[FoodAnalyzer] = None


def get_analyzer() -> FoodAnalyzer:
    """Get or create the food analyzer singleton."""
    global _analyzer
    if _analyzer is None:
        _analyzer = FoodAnalyzer()
    return _analyzer
