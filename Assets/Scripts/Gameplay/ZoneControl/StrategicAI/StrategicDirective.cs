using System;
using UnityEngine;

public enum StrategicOrderType
{
    Reinforce,
    Redistribute,
    Recapture,
    Hold,
    Feint,
    Invalid
}

[Serializable]
public sealed class StrategicDirective
{
    public string order = "hold";
    public string directive = string.Empty;

    public string target_zone = string.Empty;
    public int squad_size = 0;
    public string priority = string.Empty;

    public string from_zone = string.Empty;
    public string to_zone = string.Empty;
    public int count = 0;

    public string decoy_zone = string.Empty;
    public int decoy_size = 0;
    public string real_target_zone = string.Empty;
    public int real_size = 0;

    public string reasoning = string.Empty;

    /// <summary>
    /// Converts the textual order field into an enum value.
    /// </summary>
    public StrategicOrderType GetOrderType()
    {
        string normalized = NormalizeToken(order);
        switch (normalized)
        {
            case "reinforce":
                return StrategicOrderType.Reinforce;
            case "redistribute":
                return StrategicOrderType.Redistribute;
            case "recapture":
                return StrategicOrderType.Recapture;
            case "hold":
                return StrategicOrderType.Hold;
            case "feint":
                return StrategicOrderType.Feint;
            default:
                return StrategicOrderType.Invalid;
        }
    }

    /// <summary>
    /// Normalizes directive text fields to stable lowercase tokens and trimmed values.
    /// </summary>
    public void Normalize()
    {
        order = NormalizeToken(order);
        directive = NormalizeToken(directive);
        if (string.IsNullOrWhiteSpace(order) && !string.IsNullOrWhiteSpace(directive))
        {
            order = directive;
        }
        else if (string.IsNullOrWhiteSpace(directive) && !string.IsNullOrWhiteSpace(order))
        {
            directive = order;
        }

        target_zone = NormalizeZoneToken(target_zone);
        from_zone = NormalizeZoneToken(from_zone);
        to_zone = NormalizeZoneToken(to_zone);
        decoy_zone = NormalizeZoneToken(decoy_zone);
        real_target_zone = NormalizeZoneToken(real_target_zone);
        priority = NormalizeToken(priority);
        reasoning = string.IsNullOrWhiteSpace(reasoning) ? string.Empty : reasoning.Trim();

        squad_size = Mathf.Max(0, squad_size);
        count = Mathf.Max(0, count);
        decoy_size = Mathf.Max(0, decoy_size);
        real_size = Mathf.Max(0, real_size);
    }

    /// <summary>
    /// Attempts to parse a zone token (alpha/bravo/charlie) into ZoneId.
    /// </summary>
    public static bool TryParseZoneId(string zoneToken, out ZoneId zoneId)
    {
        zoneId = ZoneId.Alpha;
        if (string.IsNullOrWhiteSpace(zoneToken))
        {
            return false;
        }

        string normalized = NormalizeZoneToken(zoneToken);
        switch (normalized)
        {
            case "alpha":
                zoneId = ZoneId.Alpha;
                return true;
            case "bravo":
                zoneId = ZoneId.Bravo;
                return true;
            case "charlie":
                zoneId = ZoneId.Charlie;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Creates a hold directive for safe fallback behavior.
    /// </summary>
    public static StrategicDirective CreateHold(string fallbackReasoning)
    {
        return new StrategicDirective
        {
            order = "hold",
            reasoning = string.IsNullOrWhiteSpace(fallbackReasoning)
                ? "All positions stable. Monitoring target movement."
                : fallbackReasoning.Trim()
        };
    }

    /// <summary>
    /// Serializes the directive for debug output.
    /// </summary>
    public string ToDebugJson()
    {
        return JsonUtility.ToJson(this, true);
    }

    private static string NormalizeToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeZoneToken(string value)
    {
        string token = NormalizeToken(value);
        if (token == "zone_alpha")
        {
            return "alpha";
        }

        if (token == "zone_bravo")
        {
            return "bravo";
        }

        if (token == "zone_charlie")
        {
            return "charlie";
        }

        return token;
    }
}
