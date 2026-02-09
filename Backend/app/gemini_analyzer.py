"""
Gemini Vision integration for analyzing food images.
Uses GCP Vertex AI with project-based authentication (Application Default Credentials).
Returns a health score from 0.0 (unhealthy) to 1.0 (healthy).
"""

import json
import logging
import re
from typing import Any, Optional

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

DEFAULT_HOLD_REASONING = "Comms degraded. Holding sectors and observing target movement."
NON_THINKING_RESCUE_MODEL = "gemini-2.0-flash"

SWARM_DIRECTIVE_SCHEMA = types.Schema(
    type=types.Type.OBJECT,
    required=["order", "reasoning"],
    property_ordering=[
        "order",
        "target_zone",
        "squad_size",
        "priority",
        "from_zone",
        "to_zone",
        "count",
        "decoy_zone",
        "decoy_size",
        "real_target_zone",
        "real_size",
        "reasoning",
    ],
    properties={
        "order": types.Schema(
            type=types.Type.STRING,
            enum=["reinforce", "redistribute", "recapture", "hold", "feint"],
        ),
        "target_zone": types.Schema(type=types.Type.STRING),
        "squad_size": types.Schema(type=types.Type.INTEGER),
        "priority": types.Schema(type=types.Type.STRING),
        "from_zone": types.Schema(type=types.Type.STRING),
        "to_zone": types.Schema(type=types.Type.STRING),
        "count": types.Schema(type=types.Type.INTEGER),
        "decoy_zone": types.Schema(type=types.Type.STRING),
        "decoy_size": types.Schema(type=types.Type.INTEGER),
        "real_target_zone": types.Schema(type=types.Type.STRING),
        "real_size": types.Schema(type=types.Type.INTEGER),
        "reasoning": types.Schema(type=types.Type.STRING),
    },
)


class FoodAnalyzer:
    """Analyzes food images using Google Gemini via Vertex AI."""

    def __init__(self):
        settings = get_settings()
        self.settings = settings
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

        property_text = ""
        try:
            text = getattr(response, "text", None)
            if text:
                property_text = str(text).strip()
        except Exception:
            logger.debug("Failed to read response.text", exc_info=True)

        try:
            candidates = getattr(response, "candidates", None)
            if candidates:
                first_candidate = candidates[0]
                content = getattr(first_candidate, "content", None)
                parts = getattr(content, "parts", None) if content is not None else None
                if parts:
                    collected_parts = []
                    for part in parts:
                        part_text = getattr(part, "text", None)
                        if part_text:
                            collected_parts.append(str(part_text))

                    if collected_parts:
                        merged_text = "".join(collected_parts).strip()
                        if len(merged_text) >= len(property_text):
                            return merged_text
        except Exception:
            logger.debug("Failed to parse candidates content", exc_info=True)

        # Some JSON-mode responses expose a parsed object rather than text.
        try:
            parsed = getattr(response, "parsed", None)
            if parsed is not None:
                if isinstance(parsed, (dict, list)):
                    return json.dumps(parsed, separators=(",", ":"))

                return str(parsed).strip()
        except Exception:
            logger.debug("Failed to read response.parsed", exc_info=True)

        return property_text or None

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

    async def strategize_swarm(
        self,
        system_prompt: str,
        snapshot_json: str,
        model_name: Optional[str] = None,
        max_tokens: int = 16000,
        temperature: float = 0.7,
    ) -> dict:
        selected_model = model_name or self.settings.swarm_model or self.model_name
        retry_model = self.settings.swarm_retry_model or NON_THINKING_RESCUE_MODEL
        retry_count = max(0, min(1, int(self.settings.swarm_retry_count)))
        base_temperature = max(0.0, min(2.0, float(temperature)))

        user_prompt = (
            "Battlefield snapshot (JSON):\n"
            f"{snapshot_json}\n\n"
            "Return exactly one minified JSON directive object.\n"
            "Output must start with '{' and end with '}'.\n"
            "Do not use markdown/code fences or preamble text.\n"
            "Keep reasoning concise (<= 140 chars)."
        )

        attempts: list[dict[str, Any]] = [
            {
                "name": "primary",
                "model": selected_model,
                "temperature": base_temperature,
            }
        ]

        if retry_count > 0:
            attempts.append(
                {
                    "name": "retry",
                    "model": retry_model,
                    "temperature": 0.0,
                }
            )

        last_outcome: Optional[dict[str, Any]] = None
        last_exception: Optional[Exception] = None

        for attempt_index, attempt in enumerate(attempts, start=1):
            attempt_model = str(attempt["model"]).strip() or selected_model
            attempt_temperature = max(0.0, min(2.0, float(attempt["temperature"])))
            response = None
            schema_mode = "schema"
            schema_max_tokens = _effective_max_tokens_for_model(attempt_model, max_tokens)

            try:
                response = self.client.models.generate_content(
                    model=attempt_model,
                    contents=[user_prompt],
                    config=self._build_swarm_generate_config(
                        system_prompt=system_prompt,
                        temperature=attempt_temperature,
                        max_tokens=schema_max_tokens,
                        use_schema=True,
                    ),
                )
                print("--------------------------------")
                print("response")
                print(response)
                print("--------------------------------")
                print("response.text")
                print(response.text)
                print("--------------------------------")
                print("response.candidates")
                print(response.candidates)
            except Exception as exc:
                if _is_schema_parse_none_text_error(exc):
                    logger.warning(
                        "Swarm strategize attempt %s/%s hit SDK schema parse bug (model=%s, temp=%.2f). "
                        "Retrying same attempt without response_schema.",
                        attempt_index,
                        len(attempts),
                        attempt_model,
                        attempt_temperature,
                    )
                    try:
                        response = self.client.models.generate_content(
                            model=attempt_model,
                            contents=[user_prompt],
                            config=self._build_swarm_generate_config(
                                system_prompt=system_prompt,
                                temperature=attempt_temperature,
                                max_tokens=_effective_max_tokens_for_model(attempt_model, max_tokens),
                                use_schema=False,
                            ),
                        )
                        schema_mode = "json_mime_no_schema"
                    except Exception as no_schema_exc:
                        last_exception = no_schema_exc
                        logger.exception(
                            "Swarm strategize attempt %s/%s failed after no-schema retry (model=%s, temp=%.2f)",
                            attempt_index,
                            len(attempts),
                            attempt_model,
                            attempt_temperature,
                        )
                        continue
                else:
                    last_exception = exc
                    logger.exception(
                        "Swarm strategize attempt %s/%s failed (model=%s, temp=%.2f)",
                        attempt_index,
                        len(attempts),
                        attempt_model,
                        attempt_temperature,
                    )
                    continue

            candidates = self._extract_response_candidates(response)
            diagnostics = _extract_response_diagnostics(response)

            # --- diagnostic: log every candidate source (helpful when no_json_object) ---
            if logger.isEnabledFor(logging.DEBUG):
                for ci, c in enumerate(candidates):
                    logger.debug(
                        "Swarm attempt %s candidate[%s] source=%s len=%s text=%.300s",
                        attempt_index, ci, c.get("source"), len(c.get("text", "")),
                        c.get("text", ""),
                    )

            outcome = _select_candidate_outcome(candidates)
            last_outcome = outcome

            logger.info(
                "Swarm strategize attempt %s/%s name=%s model=%s temp=%.2f candidate_count=%s "
                "schema_mode=%s chosen_source=%s normalize_status=%s model_valid=%s prompt_block=%s finish_reasons=%s "
                "prompt_tokens=%s candidates_tokens=%s thoughts_tokens=%s",
                attempt_index,
                len(attempts),
                attempt["name"],
                attempt_model,
                attempt_temperature,
                len(candidates),
                schema_mode,
                outcome["source"],
                outcome["status"],
                outcome["model_valid"],
                diagnostics.get("prompt_block_reason", ""),
                diagnostics.get("finish_reasons", ""),
                diagnostics.get("prompt_token_count", ""),
                diagnostics.get("candidates_token_count", ""),
                diagnostics.get("thoughts_token_count", ""),
            )

            logger.info(
                "Swarm strategize attempt %s chosen raw candidate (len=%s):\n%s",
                attempt_index,
                len(outcome["raw"]),
                outcome["raw"],
            )
            logger.info(
                "Swarm strategize attempt %s normalized directive output:\n%s",
                attempt_index,
                outcome["normalized_json"],
            )

            if outcome["model_valid"]:
                return {
                    "raw_text": outcome["normalized_json"],
                    "model": attempt_model,
                    "provider": "vertex_gemini",
                }

            if attempt_index < len(attempts):
                next_attempt = attempts[attempt_index]
                if _should_force_non_thinking_rescue(outcome, diagnostics):
                    if _is_likely_thinking_model(str(next_attempt.get("model", ""))):
                        next_attempt["model"] = NON_THINKING_RESCUE_MODEL
                        next_attempt["temperature"] = 0.0
                        logger.warning(
                            "Swarm strategize forcing non-thinking rescue model=%s due to MAX_TOKENS/no_json on prior attempt.",
                            NON_THINKING_RESCUE_MODEL,
                        )

                logger.warning(
                    "Swarm strategize attempt %s produced non-valid directive (status=%s, prompt_block=%s, finish_reasons=%s). "
                    "Retrying with model=%s temp=%.2f. Raw prefix=%r",
                    attempt_index,
                    outcome["status"],
                    diagnostics.get("prompt_block_reason", ""),
                    diagnostics.get("finish_reasons", ""),
                    next_attempt["model"],
                    float(next_attempt["temperature"]),
                    (outcome["raw"] or "")[:200],
                )

        hold_json = _to_json(_build_hold_directive(DEFAULT_HOLD_REASONING))
        if last_outcome is None and last_exception is not None:
            logger.error(
                "Swarm strategize all attempts failed with exceptions. Emitting canonical hold. "
                "last_exception_type=%s last_exception=%s",
                type(last_exception).__name__,
                str(last_exception),
            )

        logger.warning(
            "Swarm strategize all attempts invalid. Emitting canonical hold. last_status=%s last_source=%s",
            last_outcome["status"] if last_outcome else "no_outcome",
            last_outcome["source"] if last_outcome else "<none>",
        )
        logger.info("Swarm strategize final emitted directive (forced hold):\n%s", hold_json)

        return {
            "raw_text": hold_json,
            "model": str(attempts[-1]["model"]).strip() or selected_model,
            "provider": "vertex_gemini",
        }

    def _build_swarm_generate_config(
        self,
        system_prompt: str,
        temperature: float,
        max_tokens: int,
        use_schema: bool,
    ) -> types.GenerateContentConfig:
        generate_config_fields = getattr(types.GenerateContentConfig, "model_fields", {}) or {}
        config_kwargs: dict[str, Any] = {
            "system_instruction": system_prompt,
            "temperature": max(0.0, min(2.0, float(temperature))),
            "max_output_tokens": max(32, int(max_tokens)),
            "response_mime_type": "application/json",
        }

        # Keep swarm directives deterministic and reduce "thought" content.
        thinking_config = _build_compat_thinking_config()
        if thinking_config is not None and "thinking_config" in generate_config_fields:
            config_kwargs["thinking_config"] = thinking_config

        safety_settings = _build_swarm_safety_settings()
        if safety_settings and "safety_settings" in generate_config_fields:
            config_kwargs["safety_settings"] = safety_settings

        if use_schema:
            config_kwargs["response_schema"] = SWARM_DIRECTIVE_SCHEMA
        return types.GenerateContentConfig(**config_kwargs)

    def _extract_response_candidates(self, response) -> list[dict[str, str]]:
        """Extract candidate texts from a Gemini response.

        Thinking models (gemini-2.5-flash, gemini-3-flash etc.) return
        ``thought`` parts alongside content parts.  We must skip thought parts
        to avoid merging reasoning text into the JSON payload.
        """
        candidates: list[dict[str, str]] = []
        seen_texts: set[str] = set()

        # 1. response.text — SDK-level property that already filters thought
        #    parts and concatenates only non-thought text.  Best source for
        #    structured-output JSON.
        try:
            text = getattr(response, "text", None)
            if text:
                _append_candidate(candidates, seen_texts, "response.text", str(text).strip())
        except Exception:
            logger.debug("Failed to read response.text", exc_info=True)

        # 2. response.parsed — populated by the SDK from json.loads(response.text)
        #    when response_schema is a Schema/dict.
        try:
            parsed = getattr(response, "parsed", None)
            if parsed is not None:
                if isinstance(parsed, (dict, list)):
                    parsed_text = json.dumps(parsed, separators=(",", ":"))
                else:
                    parsed_text = str(parsed).strip()
                _append_candidate(candidates, seen_texts, "response.parsed", parsed_text)
        except Exception:
            logger.debug("Failed to read response.parsed", exc_info=True)

        # 3. Manual part iteration — skip thought parts (part.thought == True)
        #    so we don't merge model reasoning into the JSON candidate.
        try:
            response_candidates = getattr(response, "candidates", None) or []
            for candidate_index, candidate in enumerate(response_candidates):
                content = getattr(candidate, "content", None)
                parts = getattr(content, "parts", None) if content is not None else None
                if not parts:
                    continue

                part_texts: list[tuple[int, str]] = []
                all_part_texts: list[tuple[int, str]] = []
                for part_index, part in enumerate(parts):
                    part_text = getattr(part, "text", None)
                    if part_text:
                        cleaned_text = str(part_text).strip()
                        if cleaned_text:
                            all_part_texts.append((part_index, cleaned_text))
                            # Prefer non-thought text first.
                            if getattr(part, "thought", False) is not True:
                                part_texts.append((part_index, cleaned_text))

                    # Handle function-call style payloads if a model emits args instead of plain text.
                    function_call = getattr(part, "function_call", None)
                    if function_call is not None:
                        args = getattr(function_call, "args", None)
                        if args is not None:
                            try:
                                if isinstance(args, (dict, list)):
                                    args_text = json.dumps(args, separators=(",", ":"))
                                else:
                                    args_text = str(args).strip()
                                _append_candidate(
                                    candidates,
                                    seen_texts,
                                    f"candidate[{candidate_index}].part[{part_index}].function_call.args",
                                    args_text,
                                )
                            except Exception:
                                logger.debug("Failed to serialize function_call args", exc_info=True)

                if not part_texts:
                    if not all_part_texts:
                        continue

                if part_texts:
                    merged_text = "".join(text for _, text in part_texts).strip()
                    _append_candidate(
                        candidates,
                        seen_texts,
                        f"candidate[{candidate_index}].parts_merged",
                        merged_text,
                    )

                for part_index, text in part_texts:
                    _append_candidate(
                        candidates,
                        seen_texts,
                        f"candidate[{candidate_index}].part[{part_index}]",
                        text,
                    )

                # Final fallback: include merged thought+non-thought text if no cleaner candidate validated.
                if all_part_texts:
                    merged_all_text = "".join(text for _, text in all_part_texts).strip()
                    _append_candidate(
                        candidates,
                        seen_texts,
                        f"candidate[{candidate_index}].all_parts_merged",
                        merged_all_text,
                    )
        except Exception:
            logger.debug("Failed to parse candidates content", exc_info=True)

        return candidates


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


def _is_schema_parse_none_text_error(exc: Exception) -> bool:
    if not isinstance(exc, TypeError):
        return False

    message = str(exc)
    return "JSON object must be str, bytes or bytearray, not NoneType" in message


def _build_compat_thinking_config() -> Optional[types.ThinkingConfig]:
    fields = getattr(types.ThinkingConfig, "model_fields", {}) or {}
    kwargs: dict[str, Any] = {}

    if "include_thoughts" in fields:
        kwargs["include_thoughts"] = False
    elif "includeThoughts" in fields:
        kwargs["includeThoughts"] = False

    if "thinking_budget" in fields:
        kwargs["thinking_budget"] = 0
    elif "thinkingBudget" in fields:
        kwargs["thinkingBudget"] = 0

    if not kwargs:
        return None

    try:
        return types.ThinkingConfig(**kwargs)
    except Exception:
        logger.warning("Failed to build ThinkingConfig with kwargs=%s", kwargs, exc_info=True)
        return None


def _build_swarm_safety_settings() -> list[types.SafetySetting]:
    # Strategic comms can include combat language; relax to block only high-risk output.
    threshold = types.HarmBlockThreshold.BLOCK_ONLY_HIGH
    categories = [
        types.HarmCategory.HARM_CATEGORY_DANGEROUS_CONTENT,
        types.HarmCategory.HARM_CATEGORY_HARASSMENT,
        types.HarmCategory.HARM_CATEGORY_HATE_SPEECH,
        types.HarmCategory.HARM_CATEGORY_SEXUALLY_EXPLICIT,
    ]

    return [types.SafetySetting(category=category, threshold=threshold) for category in categories]


def _effective_max_tokens_for_model(model_name: str, requested_max_tokens: int) -> int:
    token_budget = max(32, int(requested_max_tokens))
    if _is_likely_thinking_model(model_name):
        # Thinking models may consume output budget before JSON appears.
        return max(token_budget, 1024)
    return token_budget


def _is_likely_thinking_model(model_name: str) -> bool:
    token = model_name.strip().lower()
    return token.startswith("gemini-3") or token.startswith("gemini-2.5")


def _should_force_non_thinking_rescue(outcome: dict[str, Any], diagnostics: dict[str, str]) -> bool:
    status = str(outcome.get("status", "")).lower()
    if status != "no_json_object":
        return False

    finish_reasons = str(diagnostics.get("finish_reasons", "")).upper()
    return "MAX_TOKENS" in finish_reasons


def _extract_response_diagnostics(response) -> dict[str, str]:
    diagnostics: dict[str, str] = {
        "prompt_block_reason": "",
        "prompt_block_message": "",
        "finish_reasons": "",
        "prompt_token_count": "",
        "candidates_token_count": "",
        "thoughts_token_count": "",
    }
    if response is None:
        return diagnostics

    try:
        prompt_feedback = getattr(response, "prompt_feedback", None)
        if prompt_feedback is not None:
            block_reason = getattr(prompt_feedback, "block_reason", None)
            block_message = getattr(prompt_feedback, "block_reason_message", None)
            if block_reason:
                diagnostics["prompt_block_reason"] = str(block_reason)
            if block_message:
                diagnostics["prompt_block_message"] = str(block_message)
    except Exception:
        logger.debug("Failed to read prompt_feedback diagnostics", exc_info=True)

    try:
        response_candidates = getattr(response, "candidates", None) or []
        finish_reasons: list[str] = []
        for candidate_index, candidate in enumerate(response_candidates):
            finish_reason = getattr(candidate, "finish_reason", None)
            if finish_reason:
                finish_reasons.append(f"c{candidate_index}:{finish_reason}")
        diagnostics["finish_reasons"] = ",".join(finish_reasons)
    except Exception:
        logger.debug("Failed to read finish_reason diagnostics", exc_info=True)

    try:
        usage_metadata = getattr(response, "usage_metadata", None)
        if usage_metadata is not None:
            prompt_token_count = getattr(usage_metadata, "prompt_token_count", None)
            candidates_token_count = getattr(usage_metadata, "candidates_token_count", None)
            thoughts_token_count = getattr(usage_metadata, "thoughts_token_count", None)
            if prompt_token_count is not None:
                diagnostics["prompt_token_count"] = str(prompt_token_count)
            if candidates_token_count is not None:
                diagnostics["candidates_token_count"] = str(candidates_token_count)
            if thoughts_token_count is not None:
                diagnostics["thoughts_token_count"] = str(thoughts_token_count)
    except Exception:
        logger.debug("Failed to read usage_metadata diagnostics", exc_info=True)

    return diagnostics


def _is_actionable_directive(directive: dict) -> bool:
    order = _safe_str(directive.get("order")).lower()
    if order == "hold":
        return True

    if order in {"reinforce", "recapture"}:
        return bool(_safe_str(directive.get("target_zone"))) and _to_non_negative_int(directive.get("squad_size")) > 0

    if order == "redistribute":
        from_zone = _safe_str(directive.get("from_zone"))
        to_zone = _safe_str(directive.get("to_zone"))
        return bool(from_zone) and bool(to_zone) and from_zone != to_zone and _to_non_negative_int(directive.get("count")) > 0

    if order == "feint":
        decoy_zone = _safe_str(directive.get("decoy_zone"))
        real_target = _safe_str(directive.get("real_target_zone"))
        return (
            bool(decoy_zone)
            and bool(real_target)
            and decoy_zone != real_target
            and _to_non_negative_int(directive.get("decoy_size")) > 0
            and _to_non_negative_int(directive.get("real_size")) > 0
        )

    return False


def _append_candidate(
    candidates: list[dict[str, str]],
    seen_texts: set[str],
    source: str,
    text: str,
) -> None:
    if not text:
        return

    trimmed = text.strip()
    if not trimmed:
        return

    if trimmed in seen_texts:
        return

    seen_texts.add(trimmed)
    candidates.append({"source": source, "text": trimmed})


def _select_candidate_outcome(candidates: list[dict[str, str]]) -> dict[str, Any]:
    best_recoverable: Optional[dict[str, Any]] = None
    first_invalid: Optional[dict[str, Any]] = None

    for candidate in candidates:
        raw_text = candidate.get("text", "")
        normalized_json, is_model_valid, normalize_status = _normalize_swarm_directive(raw_text)
        outcome = {
            "source": candidate.get("source", "unknown"),
            "raw": raw_text,
            "status": normalize_status,
            "model_valid": is_model_valid,
            "normalized_json": normalized_json,
        }

        if is_model_valid:
            return outcome

        if normalize_status in {"loose_tokens", "json_decode_loose_tokens"} and best_recoverable is None:
            best_recoverable = outcome

        if first_invalid is None:
            first_invalid = outcome

    if best_recoverable is not None:
        return best_recoverable

    if first_invalid is not None:
        return first_invalid

    return {
        "source": "<none>",
        "raw": "",
        "status": "no_candidates",
        "model_valid": False,
        "normalized_json": _to_json(_build_hold_directive(DEFAULT_HOLD_REASONING)),
    }


def _normalize_swarm_directive(raw_text: str) -> tuple[str, bool, str]:
    payload = _extract_json_payload(raw_text)
    if payload is None:
        loose = _extract_from_loose_text(raw_text)
        if loose is not None:
            is_actionable = _is_actionable_directive(loose)
            return _to_json(loose), is_actionable, "loose_tokens"
        return _to_json(_build_hold_directive(DEFAULT_HOLD_REASONING)), False, "no_json_object"

    try:
        parsed = json.loads(payload)
    except json.JSONDecodeError:
        loose = _extract_from_loose_text(raw_text)
        if loose is not None:
            is_actionable = _is_actionable_directive(loose)
            return _to_json(loose), is_actionable, "json_decode_loose_tokens"
        return _to_json(_build_hold_directive(DEFAULT_HOLD_REASONING)), False, "json_decode_failed"

    directive = _coerce_directive(parsed)
    if directive is None:
        return _to_json(_build_hold_directive(DEFAULT_HOLD_REASONING)), False, "directive_shape_invalid"

    return _to_json(directive), True, "model_valid"


def _coerce_directive(parsed: object) -> Optional[dict]:
    if not isinstance(parsed, dict):
        return None

    known_keys = {
        "order",
        "directive",
        "reasoning",
        "target_zone",
        "squad_size",
        "priority",
        "from_zone",
        "to_zone",
        "count",
        "decoy_zone",
        "decoy_size",
        "real_target_zone",
        "real_size",
    }
    if not any(key in parsed for key in known_keys):
        return None

    envelope_reasoning = _safe_str(parsed.get("reasoning", ""))
    source = parsed

    nested = parsed.get("directive")
    order_token_source: object = source.get("order")
    if isinstance(nested, dict):
        source = nested
        order_token_source = source.get("order")
    elif isinstance(nested, str) and "order" not in source:
        source = dict(source)
        source["order"] = nested
        order_token_source = source.get("order")

    order = _normalize_order(order_token_source)
    if order == "invalid":
        return None

    result = _build_hold_directive("")
    result["order"] = order
    result["priority"] = _safe_str(source.get("priority"))
    result["target_zone"] = _normalize_zone(source.get("target_zone"))
    result["from_zone"] = _normalize_zone(source.get("from_zone"))
    result["to_zone"] = _normalize_zone(source.get("to_zone"))
    result["decoy_zone"] = _normalize_zone(source.get("decoy_zone"))
    result["real_target_zone"] = _normalize_zone(source.get("real_target_zone"))
    result["squad_size"] = _to_non_negative_int(source.get("squad_size"))
    result["count"] = _to_non_negative_int(source.get("count"))
    result["decoy_size"] = _to_non_negative_int(source.get("decoy_size"))
    result["real_size"] = _to_non_negative_int(source.get("real_size"))

    reasoning = _safe_str(source.get("reasoning"))
    if not reasoning and envelope_reasoning:
        reasoning = envelope_reasoning
    result["reasoning"] = reasoning or DEFAULT_HOLD_REASONING

    return result


def _extract_from_loose_text(raw_text: str) -> Optional[dict]:
    order_match = re.search(r'"(?:order|directive)"\s*:\s*"?([A-Za-z_]+)"?', raw_text, flags=re.IGNORECASE)
    if not order_match:
        return None

    result = _build_hold_directive(DEFAULT_HOLD_REASONING)
    result["order"] = _normalize_order(order_match.group(1))
    if result["order"] == "invalid":
        result["order"] = "hold"

    zone_patterns = {
        "target_zone": r'"(?:target_zone|targetZone|target)"\s*:\s*"?([A-Za-z_]+)"?',
        "from_zone": r'"(?:from_zone|fromZone|from)"\s*:\s*"?([A-Za-z_]+)"?',
        "to_zone": r'"(?:to_zone|toZone|to)"\s*:\s*"?([A-Za-z_]+)"?',
        "decoy_zone": r'"(?:decoy_zone|decoyZone)"\s*:\s*"?([A-Za-z_]+)"?',
        "real_target_zone": r'"(?:real_target_zone|realTargetZone)"\s*:\s*"?([A-Za-z_]+)"?',
    }
    for key, pattern in zone_patterns.items():
        match = re.search(pattern, raw_text, flags=re.IGNORECASE)
        if match:
            result[key] = _normalize_zone(match.group(1))

    int_patterns = {
        "squad_size": r'"(?:squad_size|squadSize)"\s*:\s*(\d+)',
        "count": r'"count"\s*:\s*(\d+)',
        "decoy_size": r'"(?:decoy_size|decoySize)"\s*:\s*(\d+)',
        "real_size": r'"(?:real_size|realSize)"\s*:\s*(\d+)',
    }
    for key, pattern in int_patterns.items():
        match = re.search(pattern, raw_text, flags=re.IGNORECASE)
        if match:
            result[key] = _to_non_negative_int(match.group(1))

    reasoning_match = re.search(r'"reasoning"\s*:\s*"([^"]*)"', raw_text, flags=re.IGNORECASE)
    if reasoning_match:
        reasoning = _safe_str(reasoning_match.group(1))
        if reasoning:
            result["reasoning"] = reasoning

    return result


def _build_hold_directive(reasoning: str) -> dict:
    return {
        "order": "hold",
        "target_zone": "",
        "squad_size": 0,
        "priority": "",
        "from_zone": "",
        "to_zone": "",
        "count": 0,
        "decoy_zone": "",
        "decoy_size": 0,
        "real_target_zone": "",
        "real_size": 0,
        "reasoning": reasoning or DEFAULT_HOLD_REASONING,
    }


def _normalize_order(value: object) -> str:
    token = _safe_str(value).lower()
    if token in {"reinforce", "redistribute", "recapture", "hold", "feint"}:
        return token
    return "invalid"


def _normalize_zone(value: object) -> str:
    token = _safe_str(value).lower()
    mapping = {
        "zone_alpha": "alpha",
        "zone_bravo": "bravo",
        "zone_charlie": "charlie",
    }
    return mapping.get(token, token)


def _to_non_negative_int(value: object) -> int:
    try:
        return max(0, int(value))
    except (TypeError, ValueError):
        return 0


def _safe_str(value: object) -> str:
    if value is None:
        return ""
    return str(value).strip()


def _to_json(data: dict) -> str:
    return json.dumps(data, separators=(",", ":"))


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
