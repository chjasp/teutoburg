using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public sealed class LLMApiClientConfig
{
    public string ApiEndpoint = "http://127.0.0.1:8080/swarm/strategize";
    public string ApiKeyFilePath = "UserSettings/swarm_api_key.txt";
    public string Model = "gemini-3-flash";
    public int MaxTokens = 300;
    public float Temperature = 0.7f;
    public float TimeoutSeconds = 20f;
}

public sealed class LLMApiResult
{
    public bool Success;
    public string ErrorMessage = string.Empty;
    public string RawResponseJson = string.Empty;
    public string RawDirectiveText = string.Empty;
    public string Model = string.Empty;
    public string Provider = string.Empty;
    public int LatencyMs = 0;
}

public sealed class LLMApiClient
{
    [Serializable]
    private sealed class SwarmStrategizeRequestDto
    {
        public string system_prompt;
        public string snapshot_json;
        public string model;
        public int max_tokens;
        public float temperature;
    }

    [Serializable]
    private sealed class SwarmStrategizeResponseDto
    {
        public string raw_text;
        public string model;
        public string provider;
        public int latency_ms;
    }

    private readonly LLMApiClientConfig _config;
    private readonly string _systemPrompt;

    public LLMApiClient(LLMApiClientConfig config, string systemPrompt)
    {
        _config = config ?? new LLMApiClientConfig();
        _systemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? "You are the Swarm Commander. Return only JSON."
            : systemPrompt.Trim();

        NormalizeRuntimeConfig();
    }

    /// <summary>
    /// Sends a battlefield snapshot to the backend strategist endpoint and returns the raw directive text.
    /// </summary>
    public IEnumerator RequestDirective(BattlefieldSnapshot snapshot, Action<LLMApiResult> onComplete)
    {
        var result = new LLMApiResult();

        if (snapshot == null)
        {
            result.Success = false;
            result.ErrorMessage = "Snapshot was null.";
            onComplete?.Invoke(result);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(_config.ApiEndpoint))
        {
            result.Success = false;
            result.ErrorMessage = "LLM endpoint is not configured.";
            onComplete?.Invoke(result);
            yield break;
        }

        string snapshotJson = snapshot.ToJson();

        var requestDto = new SwarmStrategizeRequestDto
        {
            system_prompt = _systemPrompt,
            snapshot_json = snapshotJson,
            model = string.IsNullOrWhiteSpace(_config.Model) ? "gemini-3-flash" : _config.Model,
            max_tokens = Mathf.Max(16, _config.MaxTokens),
            temperature = Mathf.Clamp(_config.Temperature, 0f, 2f)
        };

        string requestJson = JsonUtility.ToJson(requestDto);
        byte[] body = Encoding.UTF8.GetBytes(requestJson);

        using (var request = new UnityWebRequest(_config.ApiEndpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            string apiKey = ResolveApiKey();
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            }

            request.timeout = Mathf.Max(1, Mathf.CeilToInt(_config.TimeoutSeconds));

            float startedAt = Time.realtimeSinceStartup;
            yield return request.SendWebRequest();
            int elapsedMs = Mathf.Max(0, Mathf.RoundToInt((Time.realtimeSinceStartup - startedAt) * 1000f));

            result.RawResponseJson = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

            if (request.result != UnityWebRequest.Result.Success)
            {
                result.Success = false;
                long statusCode = request.responseCode;
                string timeoutDetails = request.error != null && request.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                    ? $" (timeout={Mathf.CeilToInt(_config.TimeoutSeconds)}s, elapsed={elapsedMs}ms)"
                    : $" (elapsed={elapsedMs}ms)";
                result.ErrorMessage = $"HTTP error: {request.error}; status={statusCode}; endpoint={_config.ApiEndpoint}{timeoutDetails}";
                onComplete?.Invoke(result);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(result.RawResponseJson))
            {
                result.Success = false;
                result.ErrorMessage = "Empty response body.";
                onComplete?.Invoke(result);
                yield break;
            }

            SwarmStrategizeResponseDto responseDto;
            try
            {
                responseDto = JsonUtility.FromJson<SwarmStrategizeResponseDto>(result.RawResponseJson);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Response JSON parse failed: {ex.Message}";
                onComplete?.Invoke(result);
                yield break;
            }

            if (responseDto == null)
            {
                result.Success = false;
                result.ErrorMessage = "Response JSON was null.";
                onComplete?.Invoke(result);
                yield break;
            }

            result.RawDirectiveText = string.IsNullOrWhiteSpace(responseDto.raw_text)
                ? string.Empty
                : responseDto.raw_text.Trim();
            result.Model = string.IsNullOrWhiteSpace(responseDto.model)
                ? requestDto.model
                : responseDto.model;
            result.Provider = string.IsNullOrWhiteSpace(responseDto.provider)
                ? "vertex_gemini"
                : responseDto.provider;
            result.LatencyMs = Mathf.Max(0, responseDto.latency_ms);

            if (string.IsNullOrWhiteSpace(result.RawDirectiveText))
            {
                result.Success = false;
                result.ErrorMessage = "Response missing raw_text directive payload.";
                onComplete?.Invoke(result);
                yield break;
            }

            result.Success = true;
            onComplete?.Invoke(result);
        }
    }

    private void NormalizeRuntimeConfig()
    {
        if (!string.IsNullOrWhiteSpace(_config.ApiEndpoint)
            && _config.ApiEndpoint.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase))
        {
            _config.ApiEndpoint = "http://127.0.0.1" + _config.ApiEndpoint.Substring("http://localhost".Length);
        }

        if (_config.TimeoutSeconds < 10f)
        {
            Debug.LogWarning(
                $"[LLMApiClient] TimeoutSeconds={_config.TimeoutSeconds:0.##} is too low for online strategist calls. " +
                "Raising runtime timeout to 20s.");
            _config.TimeoutSeconds = 20f;
        }
    }

    private string ResolveApiKey()
    {
        string envKey = Environment.GetEnvironmentVariable("ARETE_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return envKey.Trim();
        }

        envKey = Environment.GetEnvironmentVariable("SACRIFICE_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return envKey.Trim();
        }

        envKey = Environment.GetEnvironmentVariable("ARETE_SWARM_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return envKey.Trim();
        }

        if (string.IsNullOrWhiteSpace(_config.ApiKeyFilePath))
        {
            return string.Empty;
        }

        string fullPath = _config.ApiKeyFilePath;
        if (!Path.IsPathRooted(fullPath))
        {
            fullPath = Path.Combine(Directory.GetCurrentDirectory(), fullPath);
        }

        if (!File.Exists(fullPath))
        {
            return string.Empty;
        }

        try
        {
            string fileContent = File.ReadAllText(fullPath);
            return string.IsNullOrWhiteSpace(fileContent) ? string.Empty : fileContent.Trim();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LLMApiClient] Failed to read API key from '{fullPath}': {ex.Message}");
            return string.Empty;
        }
    }
}
