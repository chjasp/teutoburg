using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Axiom.Phygital
{
    /// <summary>
    /// Response from the food analysis API.
    /// </summary>
    [Serializable]
    public class FoodAnalysisResponse
    {
        public float score;      // 0.0 (unhealthy) to 1.0 (healthy)
        public string category;  // excellent, good, moderate, poor, unhealthy
        public string reasoning; // Brief explanation from LLM
    }

    /// <summary>
    /// Request body for the food analysis API.
    /// </summary>
    [Serializable]
    internal class FoodAnalysisRequest
    {
        public string image_base64;
        public string mime_type;
    }

    /// <summary>
    /// HTTP client for communicating with the Sacrifice Food Analysis API.
    /// Sends food images to a backend that uses Gemini Vision to analyze health score.
    /// </summary>
    [DisallowMultipleComponent]
    public class FoodAnalysisAPI : MonoBehaviour
    {
        [Header("API Configuration")]
        [Tooltip("Base URL of the food analysis API (e.g., http://localhost:8080 or Cloud Run URL)")]
        [SerializeField] private string _apiBaseUrl = "http://localhost:8080";

        [Tooltip("API key for authentication. Leave empty for development without auth.")]
        [SerializeField] private string _apiKey = "";

        [Header("Settings")]
        [Tooltip("Request timeout in seconds.")]
        [SerializeField] private float _timeout = 30f;

        [Tooltip("JPEG quality for image compression (0-100). Lower = smaller file.")]
        [Range(10, 100)]
        [SerializeField] private int _jpegQuality = 75;

        private static FoodAnalysisAPI _instance;

        /// <summary>
        /// Singleton instance for easy access.
        /// </summary>
        public static FoodAnalysisAPI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<FoodAnalysisAPI>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Debug.LogWarning("[FoodAnalysisAPI] Duplicate instance destroyed.");
                Destroy(this);
            }
        }

        /// <summary>
        /// Analyzes a food image and returns a health score.
        /// </summary>
        /// <param name="texture">The captured camera texture.</param>
        /// <param name="onComplete">Callback with the analysis result. Null if failed.</param>
        public void AnalyzeFood(Texture2D texture, Action<FoodAnalysisResponse> onComplete)
        {
            StartCoroutine(AnalyzeFoodCoroutine(texture, onComplete));
        }

        /// <summary>
        /// Analyzes a food image from raw WebCamTexture pixels.
        /// </summary>
        /// <param name="webCamTexture">The webcam texture to capture from.</param>
        /// <param name="onComplete">Callback with the analysis result. Null if failed.</param>
        public void AnalyzeFoodFromWebCam(WebCamTexture webCamTexture, Action<FoodAnalysisResponse> onComplete)
        {
            if (webCamTexture == null)
            {
                Debug.LogError("[FoodAnalysisAPI] WebCamTexture is null.");
                onComplete?.Invoke(null);
                return;
            }

            // Create a Texture2D from the WebCamTexture
            Texture2D texture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
            texture.SetPixels(webCamTexture.GetPixels());
            texture.Apply();

            StartCoroutine(AnalyzeFoodCoroutine(texture, (result) =>
            {
                // Clean up the temporary texture
                Destroy(texture);
                onComplete?.Invoke(result);
            }));
        }

        private IEnumerator AnalyzeFoodCoroutine(Texture2D texture, Action<FoodAnalysisResponse> onComplete)
        {
            if (texture == null)
            {
                Debug.LogError("[FoodAnalysisAPI] Texture is null.");
                onComplete?.Invoke(null);
                yield break;
            }

            // Encode texture to JPEG
            byte[] imageBytes = texture.EncodeToJPG(_jpegQuality);
            if (imageBytes == null || imageBytes.Length == 0)
            {
                Debug.LogError("[FoodAnalysisAPI] Failed to encode texture to JPEG.");
                onComplete?.Invoke(null);
                yield break;
            }

            Debug.Log($"[FoodAnalysisAPI] Encoded image: {imageBytes.Length / 1024}KB");

            // Convert to base64
            string base64Image = Convert.ToBase64String(imageBytes);

            // Create request body
            var requestBody = new FoodAnalysisRequest
            {
                image_base64 = base64Image,
                mime_type = "image/jpeg"
            };

            string jsonBody = JsonUtility.ToJson(requestBody);

            // Create the request
            string url = $"{_apiBaseUrl.TrimEnd('/')}/analyze";
            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                // Add authorization header if API key is set
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
                }

                request.timeout = Mathf.RoundToInt(_timeout);

                Debug.Log($"[FoodAnalysisAPI] Sending request to {url}...");
                float startTime = Time.realtimeSinceStartup;

                yield return request.SendWebRequest();

                float elapsed = Time.realtimeSinceStartup - startTime;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[FoodAnalysisAPI] Request failed: {request.error}");
                    Debug.LogError($"[FoodAnalysisAPI] Response: {request.downloadHandler?.text}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                // Parse response
                string responseText = request.downloadHandler.text;
                Debug.Log($"[FoodAnalysisAPI] Response received in {elapsed:F2}s: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<FoodAnalysisResponse>(responseText);
                    if (response != null)
                    {
                        Debug.Log($"[FoodAnalysisAPI] Analysis complete - Score: {response.score:F2}, Category: {response.category}");
                        onComplete?.Invoke(response);
                    }
                    else
                    {
                        Debug.LogError("[FoodAnalysisAPI] Failed to parse response.");
                        onComplete?.Invoke(null);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FoodAnalysisAPI] JSON parse error: {e.Message}");
                    onComplete?.Invoke(null);
                }
            }
        }

        /// <summary>
        /// Tests the connection to the API by calling the health endpoint.
        /// </summary>
        public void TestConnection(Action<bool> onComplete)
        {
            StartCoroutine(TestConnectionCoroutine(onComplete));
        }

        private IEnumerator TestConnectionCoroutine(Action<bool> onComplete)
        {
            string url = $"{_apiBaseUrl.TrimEnd('/')}/health";
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                bool success = request.result == UnityWebRequest.Result.Success;
                Debug.Log($"[FoodAnalysisAPI] Health check: {(success ? "OK" : request.error)}");
                onComplete?.Invoke(success);
            }
        }
    }
}
