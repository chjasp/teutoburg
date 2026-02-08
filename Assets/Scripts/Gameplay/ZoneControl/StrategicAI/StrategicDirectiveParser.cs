using System;
using System.Text.RegularExpressions;
using UnityEngine;

public sealed class DirectiveValidationResult
{
    public bool IsValid;
    public StrategicDirective Directive;
    public string Status;
}

public static class StrategicDirectiveParser
{
    /// <summary>
    /// Parses the model raw text into a directive object.
    /// </summary>
    public static bool TryParse(string rawText, out StrategicDirective directive, out string error)
    {
        directive = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawText))
        {
            error = "Empty LLM response.";
            return false;
        }

        string normalizedText = NormalizeRawResponse(rawText);

        int searchIndex = 0;
        bool foundJsonObject = false;
        string lastJsonParseError = string.Empty;

        while (TryFindNextJsonObject(normalizedText, searchIndex, out string jsonCandidate, out int nextSearchIndex))
        {
            foundJsonObject = true;
            if (TryParseDirectiveFromJson(jsonCandidate, out directive, out string jsonError))
            {
                directive.Normalize();
                return true;
            }

            lastJsonParseError = jsonError;
            searchIndex = nextSearchIndex;
        }

        if (TryParseDirectiveFromLooseText(normalizedText, out directive, out string looseError))
        {
            directive.Normalize();
            return true;
        }

        if (!foundJsonObject)
        {
            error = "No JSON object found in LLM response.";
            return false;
        }

        error = string.IsNullOrWhiteSpace(lastJsonParseError)
            ? looseError
            : $"{lastJsonParseError} {looseError}";
        return false;
    }

    private static bool TryParseDirectiveFromJson(string json, out StrategicDirective directive, out string error)
    {
        directive = null;
        error = string.Empty;

        StrategicDirective parsed = null;
        try
        {
            parsed = JsonUtility.FromJson<StrategicDirective>(json);
        }
        catch (Exception ex)
        {
            error = $"Directive JSON parse failed: {ex.Message}";
        }

        if (parsed != null)
        {
            bool hasExplicitOrder = TryExtractOrderToken(json, out string extractedOrder);
            if (hasExplicitOrder)
            {
                parsed.order = extractedOrder;
            }

            parsed.Normalize();
            if (HasRecognizableFields(parsed) || hasExplicitOrder)
            {
                directive = parsed;
                return true;
            }
        }

        try
        {
            DirectiveEnvelope envelope = JsonUtility.FromJson<DirectiveEnvelope>(json);
            if (envelope != null && envelope.directive != null)
            {
                StrategicDirective nested = envelope.directive;
                bool hasExplicitOrder = TryExtractOrderToken(json, out _);
                if (string.IsNullOrWhiteSpace(nested.reasoning) && !string.IsNullOrWhiteSpace(envelope.reasoning))
                {
                    nested.reasoning = envelope.reasoning;
                }

                nested.Normalize();
                if (HasRecognizableFields(nested) || hasExplicitOrder)
                {
                    directive = nested;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            error = $"Directive envelope parse failed: {ex.Message}";
            return false;
        }

        error = "JSON object did not contain recognizable directive fields.";
        return false;
    }

    private static bool TryParseDirectiveFromLooseText(string text, out StrategicDirective directive, out string error)
    {
        directive = null;
        error = "No directive tokens recognized.";

        if (!TryExtractOrderToken(text, out string orderToken))
        {
            return false;
        }

        var parsed = new StrategicDirective
        {
            order = orderToken
        };

        if (TryExtractToken(
                text,
                "\"(?:target_zone|targetZone|target)\"\\s*:\\s*\"?([A-Za-z_]+)",
                out string targetZone))
        {
            parsed.target_zone = targetZone;
        }

        if (TryExtractToken(
                text,
                "\"(?:from_zone|fromZone|from)\"\\s*:\\s*\"?([A-Za-z_]+)",
                out string fromZone))
        {
            parsed.from_zone = fromZone;
        }

        if (TryExtractToken(
                text,
                "\"(?:to_zone|toZone|to)\"\\s*:\\s*\"?([A-Za-z_]+)",
                out string toZone))
        {
            parsed.to_zone = toZone;
        }

        if (TryExtractToken(
                text,
                "\"(?:decoy_zone|decoyZone)\"\\s*:\\s*\"?([A-Za-z_]+)",
                out string decoyZone))
        {
            parsed.decoy_zone = decoyZone;
        }

        if (TryExtractToken(
                text,
                "\"(?:real_target_zone|realTargetZone)\"\\s*:\\s*\"?([A-Za-z_]+)",
                out string realTargetZone))
        {
            parsed.real_target_zone = realTargetZone;
        }

        if (TryExtractInt(
                text,
                "\"(?:squad_size|squadSize)\"\\s*:\\s*(\\d+)",
                out int squadSize))
        {
            parsed.squad_size = squadSize;
        }

        if (TryExtractInt(
                text,
                "\"count\"\\s*:\\s*(\\d+)",
                out int count))
        {
            parsed.count = count;
        }

        if (TryExtractInt(
                text,
                "\"(?:decoy_size|decoySize)\"\\s*:\\s*(\\d+)",
                out int decoySize))
        {
            parsed.decoy_size = decoySize;
        }

        if (TryExtractInt(
                text,
                "\"(?:real_size|realSize)\"\\s*:\\s*(\\d+)",
                out int realSize))
        {
            parsed.real_size = realSize;
        }

        if (TryExtractToken(
                text,
                "\"reasoning\"\\s*:\\s*\"([^\"]+)",
                out string reasoning))
        {
            parsed.reasoning = reasoning;
        }

        parsed.Normalize();
        directive = parsed;
        error = string.Empty;
        return true;
    }

    private static string NormalizeRawResponse(string raw)
    {
        string text = raw.Trim();
        text = text.Replace("```json", string.Empty);
        text = text.Replace("```JSON", string.Empty);
        text = text.Replace("```Json", string.Empty);
        text = text.Replace("```", string.Empty);
        return text.Trim();
    }

    private static bool TryFindNextJsonObject(string text, int searchIndex, out string json, out int nextSearchIndex)
    {
        json = string.Empty;
        nextSearchIndex = -1;

        int start = text.IndexOf('{', Mathf.Max(0, searchIndex));
        while (start >= 0)
        {
            if (TryExtractBalancedJsonObject(text, start, out json, out int endIndex))
            {
                nextSearchIndex = endIndex + 1;
                return true;
            }

            start = text.IndexOf('{', start + 1);
        }

        return false;
    }

    private static bool TryExtractBalancedJsonObject(string text, int startIndex, out string json, out int endIndex)
    {
        json = string.Empty;
        endIndex = -1;

        if (startIndex < 0 || startIndex >= text.Length)
        {
            return false;
        }

        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = startIndex; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\')
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    endIndex = i;
                    json = text.Substring(startIndex, i - startIndex + 1);
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryExtractOrderToken(string text, out string orderToken)
    {
        return TryExtractToken(
            text,
            "\"(?:order|directive)\"\\s*:\\s*\"?([A-Za-z_]+)",
            out orderToken);
    }

    private static bool TryExtractToken(string text, string pattern, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success || match.Groups.Count < 2)
        {
            return false;
        }

        value = match.Groups[1].Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();
        return true;
    }

    private static bool TryExtractInt(string text, string pattern, out int value)
    {
        value = 0;
        if (!TryExtractToken(text, pattern, out string token))
        {
            return false;
        }

        return int.TryParse(token, out value);
    }

    private static bool HasRecognizableFields(StrategicDirective directive)
    {
        if (directive == null)
        {
            return false;
        }

        return
            !string.IsNullOrWhiteSpace(directive.directive) ||
            !string.IsNullOrWhiteSpace(directive.target_zone) ||
            !string.IsNullOrWhiteSpace(directive.from_zone) ||
            !string.IsNullOrWhiteSpace(directive.to_zone) ||
            !string.IsNullOrWhiteSpace(directive.decoy_zone) ||
            !string.IsNullOrWhiteSpace(directive.real_target_zone) ||
            !string.IsNullOrWhiteSpace(directive.reasoning) ||
            directive.squad_size > 0 ||
            directive.count > 0 ||
            directive.decoy_size > 0 ||
            directive.real_size > 0;
    }

    [Serializable]
    private sealed class DirectiveEnvelope
    {
        public StrategicDirective directive;
        public string reasoning = string.Empty;
    }
}

public static class StrategicDirectiveValidator
{
    /// <summary>
    /// Validates an LLM directive against runtime constraints and returns a safe directive.
    /// </summary>
    public static DirectiveValidationResult Validate(StrategicDirective candidate, BattlefieldSnapshot snapshot)
    {
        var result = new DirectiveValidationResult
        {
            IsValid = false,
            Directive = StrategicDirective.CreateHold("Directive rejected. Holding positions."),
            Status = "Directive rejected."
        };

        if (candidate == null)
        {
            result.Status = "Directive was null.";
            return result;
        }

        candidate.Normalize();
        if (string.IsNullOrWhiteSpace(candidate.reasoning))
        {
            candidate.reasoning = "All positions stable. Monitoring target movement.";
        }

        switch (candidate.GetOrderType())
        {
            case StrategicOrderType.Reinforce:
            case StrategicOrderType.Recapture:
                return ValidateReinforceLike(candidate, snapshot);

            case StrategicOrderType.Redistribute:
                return ValidateRedistribute(candidate, snapshot);

            case StrategicOrderType.Feint:
                return ValidateFeint(candidate, snapshot);

            case StrategicOrderType.Hold:
                result.IsValid = true;
                result.Directive = candidate;
                result.Status = "Hold directive accepted.";
                return result;

            default:
                result.Status = $"Unsupported directive order '{candidate.order}'.";
                return result;
        }
    }

    private static DirectiveValidationResult ValidateReinforceLike(StrategicDirective directive, BattlefieldSnapshot snapshot)
    {
        var result = CreateRejected();

        if (!StrategicDirective.TryParseZoneId(directive.target_zone, out _))
        {
            result.Status = "Missing or invalid target_zone.";
            return result;
        }

        if (directive.squad_size <= 0)
        {
            result.Status = "squad_size must be greater than zero.";
            return result;
        }

        if (!HasStrategicCapacity(snapshot, 1, directive.squad_size, out string reason))
        {
            result.Status = reason;
            return result;
        }

        result.IsValid = true;
        result.Directive = directive;
        result.Status = "Directive accepted.";
        return result;
    }

    private static DirectiveValidationResult ValidateRedistribute(StrategicDirective directive, BattlefieldSnapshot snapshot)
    {
        var result = CreateRejected();

        if (!StrategicDirective.TryParseZoneId(directive.from_zone, out ZoneId fromZone) ||
            !StrategicDirective.TryParseZoneId(directive.to_zone, out ZoneId toZone))
        {
            result.Status = "Missing from_zone or to_zone for redistribute.";
            return result;
        }

        if (fromZone == toZone)
        {
            result.Status = "redistribute requires different from_zone and to_zone.";
            return result;
        }

        if (directive.count <= 0)
        {
            result.Status = "redistribute count must be greater than zero.";
            return result;
        }

        ZoneSnapshot fromSnapshot = snapshot != null ? snapshot.FindZone(fromZone) : null;
        if (fromSnapshot == null)
        {
            result.Status = "Source zone is unavailable in snapshot.";
            return result;
        }

        if (fromSnapshot.defenders_count < directive.count)
        {
            result.Status = "Not enough defenders in source zone.";
            return result;
        }

        result.IsValid = true;
        result.Directive = directive;
        result.Status = "Directive accepted.";
        return result;
    }

    private static DirectiveValidationResult ValidateFeint(StrategicDirective directive, BattlefieldSnapshot snapshot)
    {
        var result = CreateRejected();

        if (!StrategicDirective.TryParseZoneId(directive.decoy_zone, out ZoneId decoyZone) ||
            !StrategicDirective.TryParseZoneId(directive.real_target_zone, out ZoneId realZone))
        {
            result.Status = "feint requires decoy_zone and real_target_zone.";
            return result;
        }

        if (decoyZone == realZone)
        {
            result.Status = "feint decoy_zone and real_target_zone cannot match.";
            return result;
        }

        if (directive.decoy_size <= 0 || directive.real_size <= 0)
        {
            result.Status = "feint sizes must be greater than zero.";
            return result;
        }

        int totalSize = directive.decoy_size + directive.real_size;
        if (!HasStrategicCapacity(snapshot, 2, totalSize, out string reason))
        {
            result.Status = reason;
            return result;
        }

        result.IsValid = true;
        result.Directive = directive;
        result.Status = "Directive accepted.";
        return result;
    }

    private static bool HasStrategicCapacity(BattlefieldSnapshot snapshot, int requiredSquads, int requestedDroneCount, out string reason)
    {
        reason = string.Empty;

        if (snapshot == null)
        {
            return true;
        }

        if (snapshot.ai_resources.reinforcement_squads_available < requiredSquads)
        {
            reason = "No strategic squads available.";
            return false;
        }

        if (snapshot.ai_resources.reinforcement_cooldown_seconds > 0.05f)
        {
            reason = "Strategic reinforcement cooldown active.";
            return false;
        }

        if (requestedDroneCount > Mathf.Max(1, snapshot.ai_resources.total_drones_alive))
        {
            reason = "Directive requests more drones than available.";
            return false;
        }

        return true;
    }

    private static DirectiveValidationResult CreateRejected()
    {
        return new DirectiveValidationResult
        {
            IsValid = false,
            Directive = StrategicDirective.CreateHold("Directive rejected. Holding positions."),
            Status = "Directive rejected."
        };
    }
}
