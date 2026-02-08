using System;
using System.Collections.Generic;
using System.Text;

[Serializable]
public sealed class ZoneSnapshot
{
    public string id = string.Empty;
    public string owner = "ai";
    public int defenders_count = 0;
    public float capture_progress = 0f;

    public bool has_seconds_since_captured = false;
    public float seconds_since_captured = 0f;
}

[Serializable]
public sealed class PlayerSnapshot
{
    public string current_zone = "none";
    public float health_percent = 1f;
    public string last_attack_style = "none";
    public int zones_captured_count = 0;
}

[Serializable]
public sealed class AiResourcesSnapshot
{
    public int total_drones_alive = 0;
    public int reinforcement_squads_available = 0;
    public float reinforcement_cooldown_seconds = 0f;
}

[Serializable]
public sealed class BattlefieldSnapshot
{
    public List<ZoneSnapshot> zones = new List<ZoneSnapshot>(3);
    public PlayerSnapshot player = new PlayerSnapshot();
    public AiResourcesSnapshot ai_resources = new AiResourcesSnapshot();
    public float match_time_seconds = 0f;
    public List<string> recent_events = new List<string>(10);

    /// <summary>
    /// Finds a zone snapshot by id.
    /// </summary>
    public ZoneSnapshot FindZone(ZoneId zoneId)
    {
        string token = zoneId.ToString().ToLowerInvariant();

        for (int i = 0; i < zones.Count; i++)
        {
            ZoneSnapshot zone = zones[i];
            if (zone != null && string.Equals(zone.id, token, StringComparison.OrdinalIgnoreCase))
            {
                return zone;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds a stable JSON payload for LLM requests.
    /// </summary>
    public string ToJson()
    {
        var sb = new StringBuilder(2048);
        sb.Append('{');

        sb.Append("\"zones\":[");
        for (int i = 0; i < zones.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            ZoneSnapshot zone = zones[i] ?? new ZoneSnapshot();
            sb.Append('{');
            AppendStringField(sb, "id", zone.id);
            sb.Append(',');
            AppendStringField(sb, "owner", zone.owner);
            sb.Append(',');
            AppendNumberField(sb, "defenders_count", zone.defenders_count);
            sb.Append(',');
            AppendFloatField(sb, "capture_progress", zone.capture_progress);
            sb.Append(',');
            sb.Append("\"seconds_since_captured\":");
            if (zone.has_seconds_since_captured)
            {
                AppendFloatValue(sb, zone.seconds_since_captured);
            }
            else
            {
                sb.Append("null");
            }

            sb.Append('}');
        }

        sb.Append(']');
        sb.Append(',');

        sb.Append("\"player\":{");
        AppendStringField(sb, "current_zone", player.current_zone);
        sb.Append(',');
        AppendFloatField(sb, "health_percent", player.health_percent);
        sb.Append(',');
        AppendStringField(sb, "last_attack_style", player.last_attack_style);
        sb.Append(',');
        AppendNumberField(sb, "zones_captured_count", player.zones_captured_count);
        sb.Append('}');
        sb.Append(',');

        sb.Append("\"ai_resources\":{");
        AppendNumberField(sb, "total_drones_alive", ai_resources.total_drones_alive);
        sb.Append(',');
        AppendNumberField(sb, "reinforcement_squads_available", ai_resources.reinforcement_squads_available);
        sb.Append(',');
        AppendFloatField(sb, "reinforcement_cooldown_seconds", ai_resources.reinforcement_cooldown_seconds);
        sb.Append('}');
        sb.Append(',');

        AppendFloatField(sb, "match_time_seconds", match_time_seconds);
        sb.Append(',');

        sb.Append("\"recent_events\":[");
        for (int i = 0; i < recent_events.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            AppendQuotedValue(sb, recent_events[i]);
        }

        sb.Append(']');
        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendStringField(StringBuilder sb, string key, string value)
    {
        AppendQuotedValue(sb, key);
        sb.Append(':');
        AppendQuotedValue(sb, value);
    }

    private static void AppendNumberField(StringBuilder sb, string key, int value)
    {
        AppendQuotedValue(sb, key);
        sb.Append(':');
        sb.Append(value);
    }

    private static void AppendFloatField(StringBuilder sb, string key, float value)
    {
        AppendQuotedValue(sb, key);
        sb.Append(':');
        AppendFloatValue(sb, value);
    }

    private static void AppendFloatValue(StringBuilder sb, float value)
    {
        sb.Append(value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void AppendQuotedValue(StringBuilder sb, string value)
    {
        sb.Append('"');
        if (!string.IsNullOrEmpty(value))
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
        }

        sb.Append('"');
    }
}
