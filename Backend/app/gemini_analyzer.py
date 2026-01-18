"""
Gemini Vision integration for analyzing food images.
Uses GCP Vertex AI with project-based authentication (Application Default Credentials).
Returns a health score from 0.0 (unhealthy) to 1.0 (healthy).
"""

import json
import re
from typing import Optional

from google import genai
from google.genai import types

from .config import get_settings


# System prompt for food health analysis
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
        
        # Initialize client with Vertex AI mode (uses Application Default Credentials)
        # No API key needed - billing goes through the GCP project
        self.client = genai.Client(
            vertexai=True,
            project=settings.gcp_project_id,
            location=settings.gcp_location
        )
        self.model_name = settings.gemini_model
        
        print(f"[FoodAnalyzer] Initialized with project={settings.gcp_project_id}, "
              f"location={settings.gcp_location}, model={self.model_name}")
    
    async def analyze_image(self, image_bytes: bytes, mime_type: str = "image/jpeg") -> dict:
        """
        Analyze a food image and return a health assessment.
        
        Args:
            image_bytes: Raw image data
            mime_type: MIME type of the image (image/jpeg or image/png)
            
        Returns:
            dict with keys: score (float), category (str), reasoning (str)
        """
        try:
            # Validate input
            if not image_bytes:
                print("[FoodAnalyzer] Error: image_bytes is empty or None")
                return {
                    "score": 0.5,
                    "category": "error",
                    "reasoning": "No image data received"
                }
            
            print(f"[FoodAnalyzer] Processing image: {len(image_bytes)} bytes, mime_type={mime_type}")
            
            # Create image part for Gemini using inline_data format
            image_part = types.Part.from_bytes(data=image_bytes, mime_type=mime_type)
            
            # Generate response using the client
            response = self.client.models.generate_content(
                model=self.model_name,
                contents=[ANALYSIS_PROMPT, image_part],
                config=types.GenerateContentConfig(
                    temperature=0.1,  # Low temperature for consistent scoring
                    max_output_tokens=1024,  # Increased to ensure complete response
                )
            )
            
            # Debug: print the full response structure
            print(f"[FoodAnalyzer] Response type: {type(response)}")
            print(f"[FoodAnalyzer] Response: {response}")
            
            # Extract text from response - try multiple access patterns
            response_text = self._extract_response_text(response)
            
            if not response_text:
                print("[FoodAnalyzer] Error: Could not extract text from response")
                return {
                    "score": 0.5,
                    "category": "error",
                    "reasoning": "Empty response from Gemini API"
                }
            
            print(f"[FoodAnalyzer] Extracted text: {response_text[:200]}")
            
            # Parse the JSON response
            return self._parse_response(response_text)
            
        except Exception as e:
            # Return a safe fallback on error
            import traceback
            print(f"[FoodAnalyzer] Error: {e}")
            print(f"[FoodAnalyzer] Traceback: {traceback.format_exc()}")
            return {
                "score": 0.5,
                "category": "error",
                "reasoning": f"Analysis failed: {str(e)}"
            }
    
    def _extract_response_text(self, response) -> Optional[str]:
        """Extract text content from the Gemini response object."""
        # Try the .text property first (convenience accessor)
        try:
            if hasattr(response, 'text') and response.text:
                return response.text
        except Exception as e:
            print(f"[FoodAnalyzer] response.text failed: {e}")
        
        # Try accessing via candidates -> content -> parts -> text
        try:
            if hasattr(response, 'candidates') and response.candidates:
                candidate = response.candidates[0]
                if hasattr(candidate, 'content') and candidate.content:
                    if hasattr(candidate.content, 'parts') and candidate.content.parts:
                        for part in candidate.content.parts:
                            if hasattr(part, 'text') and part.text:
                                return part.text
        except Exception as e:
            print(f"[FoodAnalyzer] candidates access failed: {e}")
        
        # Try direct parts access
        try:
            if hasattr(response, 'parts') and response.parts:
                for part in response.parts:
                    if hasattr(part, 'text') and part.text:
                        return part.text
        except Exception as e:
            print(f"[FoodAnalyzer] parts access failed: {e}")
        
        # Try string conversion as last resort
        try:
            response_str = str(response)
            if response_str and response_str != 'None':
                print(f"[FoodAnalyzer] Using string conversion: {response_str[:100]}")
                return response_str
        except Exception as e:
            print(f"[FoodAnalyzer] string conversion failed: {e}")
        
        return None
    
    def _parse_response(self, response_text: str) -> dict:
        """Parse the LLM response into a structured result."""
        try:
            # Try to extract JSON from the response
            # Handle cases where the model might wrap it in markdown code blocks
            json_match = re.search(r'\{[^{}]*\}', response_text, re.DOTALL)
            if json_match:
                result = json.loads(json_match.group())
            else:
                result = json.loads(response_text)
            
            # Validate and clamp score
            score = float(result.get("score", 0.5))
            score = max(0.0, min(1.0, score))  # Clamp to 0-1
            
            return {
                "score": score,
                "category": str(result.get("category", "unknown")),
                "reasoning": str(result.get("reasoning", ""))
            }
            
        except (json.JSONDecodeError, ValueError, KeyError) as e:
            # Fallback if parsing fails
            print(f"[FoodAnalyzer] JSON parse error: {e}")
            return {
                "score": 0.5,
                "category": "parse_error",
                "reasoning": f"Could not parse LLM response: {response_text[:100]}"
            }


# Singleton instance
_analyzer: Optional[FoodAnalyzer] = None


def get_analyzer() -> FoodAnalyzer:
    """Get or create the food analyzer singleton."""
    global _analyzer
    if _analyzer is None:
        _analyzer = FoodAnalyzer()
    return _analyzer
