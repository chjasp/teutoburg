using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Phygital
{
    /// <summary>
    /// Manages the in-game camera UI for the "Sacrifice" mechanic.
    /// Displays live rear-camera feed, captures photo, and auto-closes after confirmation.
    /// </summary>
    [DisallowMultipleComponent]
    public class SacrificeCameraUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("The full-screen panel containing the camera UI.")]
        [SerializeField] private GameObject _cameraPanel;

        [Tooltip("RawImage that displays the live camera feed.")]
        [SerializeField] private RawImage _cameraFeed;

        [Tooltip("Button to capture the photo.")]
        [SerializeField] private Button _captureButton;

        [Tooltip("Optional: Button to close the camera without capturing.")]
        [SerializeField] private Button _closeButton;

        [Header("Processing UI")]
        [Tooltip("Optional: Text to show while analyzing the image.")]
        [SerializeField] private GameObject _processingIndicator;

        [Tooltip("Optional: Text to show the result.")]
        [SerializeField] private TMPro.TextMeshProUGUI _resultText;

        [Header("Settings")]
        [Tooltip("Time in seconds before auto-closing after capture.")]
        [SerializeField] private float _autoCloseDelay = 2f;

        [Tooltip("Target resolution width for the camera feed.")]
        [SerializeField] private int _requestedWidth = 1280;

        [Tooltip("Target resolution height for the camera feed.")]
        [SerializeField] private int _requestedHeight = 720;

        [Tooltip("Target framerate for the camera feed.")]
        [SerializeField] private int _requestedFPS = 30;

        [Header("Integration")]
        [Tooltip("Reference to the FoodAnalysisAPI. Auto-finds if not set.")]
        [SerializeField] private FoodAnalysisAPI _foodAnalysisAPI;

        [Tooltip("Reference to the SacrificeBuffEffect. Auto-finds if not set.")]
        [SerializeField] private SacrificeBuffEffect _buffEffect;

        private WebCamTexture _webCamTexture;
        private Action _onCloseCallback;
        private bool _isCapturing;

        /// <summary>
        /// Returns true if the camera panel is currently open.
        /// </summary>
        public bool IsOpen => _cameraPanel != null && _cameraPanel.activeSelf;

        private void Awake()
        {
            // Ensure panel starts hidden
            if (_cameraPanel != null)
            {
                _cameraPanel.SetActive(false);
            }

            // Hide processing indicator
            if (_processingIndicator != null)
            {
                _processingIndicator.SetActive(false);
            }

            // Auto-find dependencies if not set
            if (_foodAnalysisAPI == null)
            {
                _foodAnalysisAPI = FindAnyObjectByType<FoodAnalysisAPI>();
            }
            if (_buffEffect == null)
            {
                _buffEffect = FindAnyObjectByType<SacrificeBuffEffect>();
            }

            // Hook up buttons
            if (_captureButton != null)
            {
                _captureButton.onClick.AddListener(OnCaptureClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(() => CloseCamera());
            }
        }

        private void OnDestroy()
        {
            StopCamera();

            if (_captureButton != null)
            {
                _captureButton.onClick.RemoveListener(OnCaptureClicked);
            }
        }

        /// <summary>
        /// Opens the camera UI and starts the camera feed.
        /// </summary>
        /// <param name="onClose">Callback invoked when the camera UI is closed.</param>
        public void OpenCamera(Action onClose = null)
        {
            _onCloseCallback = onClose;
            _isCapturing = false;

            if (_cameraPanel == null)
            {
                Debug.LogError("[SacrificeCameraUI] Camera panel is not assigned!");
                return;
            }

            _cameraPanel.SetActive(true);

            // Enable capture button
            if (_captureButton != null)
            {
                _captureButton.interactable = true;
            }

            StartCoroutine(InitializeCameraCoroutine());
        }

        /// <summary>
        /// Closes the camera UI and stops the camera.
        /// </summary>
        public void CloseCamera()
        {
            StopCamera();

            if (_cameraPanel != null)
            {
                _cameraPanel.SetActive(false);
            }

            _onCloseCallback?.Invoke();
            _onCloseCallback = null;
        }

        private IEnumerator InitializeCameraCoroutine()
        {
            // Request camera permission on iOS
#if UNITY_IOS && !UNITY_EDITOR
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.LogError("[SacrificeCameraUI] Camera permission was denied by user.");
                CloseCamera();
                yield break;
            }
#endif

            // Find rear-facing camera
            WebCamDevice? rearCamera = null;
            WebCamDevice[] devices = WebCamTexture.devices;

            if (devices.Length == 0)
            {
                Debug.LogError("[SacrificeCameraUI] No camera devices found.");
                CloseCamera();
                yield break;
            }

            // Prefer rear-facing camera
            foreach (var device in devices)
            {
                if (!device.isFrontFacing)
                {
                    rearCamera = device;
                    break;
                }
            }

            // Fallback to first available camera if no rear camera found
            if (!rearCamera.HasValue)
            {
                Debug.LogWarning("[SacrificeCameraUI] No rear camera found, using front camera.");
                rearCamera = devices[0];
            }

            // Create and start WebCamTexture
            _webCamTexture = new WebCamTexture(
                rearCamera.Value.name,
                _requestedWidth,
                _requestedHeight,
                _requestedFPS
            );

            _webCamTexture.Play();

            // Wait for camera to initialize
            float timeout = 5f;
            float elapsed = 0f;
            while (!_webCamTexture.didUpdateThisFrame && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!_webCamTexture.isPlaying)
            {
                Debug.LogError("[SacrificeCameraUI] Failed to start camera.");
                CloseCamera();
                yield break;
            }

            // Assign texture to RawImage
            if (_cameraFeed != null)
            {
                _cameraFeed.texture = _webCamTexture;

                // Adjust aspect ratio
                float aspectRatio = (float)_webCamTexture.width / _webCamTexture.height;
                var fitter = _cameraFeed.GetComponent<AspectRatioFitter>();
                if (fitter != null)
                {
                    fitter.aspectRatio = aspectRatio;
                }

                // Handle rotation for mobile devices
                AdjustCameraRotation();
            }

            Debug.Log($"[SacrificeCameraUI] Camera started: {rearCamera.Value.name} @ {_webCamTexture.width}x{_webCamTexture.height}");
        }

        private void AdjustCameraRotation()
        {
            if (_cameraFeed == null || _webCamTexture == null) return;

            // WebCamTexture on mobile devices may be rotated
            int angle = -_webCamTexture.videoRotationAngle;
            _cameraFeed.rectTransform.localEulerAngles = new Vector3(0, 0, angle);

            // Handle mirrored video (front camera)
            bool mirrored = _webCamTexture.videoVerticallyMirrored;
            _cameraFeed.rectTransform.localScale = new Vector3(1, mirrored ? -1 : 1, 1);
        }

        private void OnCaptureClicked()
        {
            if (_isCapturing) return;
            if (_webCamTexture == null || !_webCamTexture.isPlaying) return;

            _isCapturing = true;
            StartCoroutine(CaptureAndCloseCoroutine());
        }

        private IEnumerator CaptureAndCloseCoroutine()
        {
            // Capture current frame info
            int width = _webCamTexture.width;
            int height = _webCamTexture.height;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Pause the camera feed (visual feedback)
            _webCamTexture.Pause();

            // Disable capture button to prevent double-tap
            if (_captureButton != null)
            {
                _captureButton.interactable = false;
            }

            // Log the sacrifice acceptance
            Debug.Log($"SACRIFICE ACCEPTED: {timestamp} - Resolution {width}x{height}");

            // Show processing indicator
            if (_processingIndicator != null)
            {
                _processingIndicator.SetActive(true);
            }
            UpdateResultText("Analyzing offering...");

            // Create texture from webcam for API
            Texture2D capturedTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            capturedTexture.SetPixels(_webCamTexture.GetPixels());
            capturedTexture.Apply();

            // Send to API if available
            FoodAnalysisResponse analysisResult = null;
            bool apiComplete = false;

            if (_foodAnalysisAPI != null)
            {
                _foodAnalysisAPI.AnalyzeFood(capturedTexture, (response) =>
                {
                    analysisResult = response;
                    apiComplete = true;
                });

                // Wait for API response (with timeout)
                float timeout = 30f;
                float elapsed = 0f;
                while (!apiComplete && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!apiComplete)
                {
                    Debug.LogWarning("[SacrificeCameraUI] API request timed out.");
                }
            }
            else
            {
                Debug.LogWarning("[SacrificeCameraUI] FoodAnalysisAPI not found, skipping analysis.");
                apiComplete = true;
            }

            // Clean up captured texture
            Destroy(capturedTexture);

            // Hide processing indicator
            if (_processingIndicator != null)
            {
                _processingIndicator.SetActive(false);
            }

            // Process the result
            if (analysisResult != null)
            {
                Debug.Log($"[SacrificeCameraUI] Analysis result - Score: {analysisResult.score:F2}, Category: {analysisResult.category}, Reasoning: {analysisResult.reasoning}");
                
                // Show result to user
                UpdateResultText($"{GetCategorySymbol(analysisResult.category)} {analysisResult.category.ToUpper()}\n{analysisResult.reasoning}");

                // Trigger visual buff effect
                if (_buffEffect != null)
                {
                    _buffEffect.TriggerEffect(analysisResult.score, analysisResult.category);
                }
            }
            else
            {
                UpdateResultText("Could not analyze offering.\nPlease try again.");
            }

            // Wait before auto-closing
            yield return new WaitForSeconds(_autoCloseDelay);

            // Close the camera UI
            CloseCamera();
        }

        private void UpdateResultText(string message)
        {
            if (_resultText != null)
            {
                _resultText.text = message;
            }
        }

        private string GetCategorySymbol(string category)
        {
            // Using ASCII-compatible symbols that work with standard fonts
            return category?.ToLower() switch
            {
                "excellent" => "[+++]",
                "good" => "[++]",
                "moderate" => "[+]",
                "poor" => "[-]",
                "unhealthy" => "[--]",
                "error" => "[!]",
                _ => "[?]"
            };
        }

        private void StopCamera()
        {
            if (_webCamTexture != null)
            {
                if (_webCamTexture.isPlaying)
                {
                    _webCamTexture.Stop();
                }
                Destroy(_webCamTexture);
                _webCamTexture = null;
            }

            if (_cameraFeed != null)
            {
                _cameraFeed.texture = null;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // Handle app going to background
            if (pauseStatus && _webCamTexture != null && _webCamTexture.isPlaying)
            {
                _webCamTexture.Pause();
            }
            else if (!pauseStatus && _webCamTexture != null && IsOpen && !_isCapturing)
            {
                _webCamTexture.Play();
            }
        }
    }
}
