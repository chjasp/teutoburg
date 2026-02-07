using System;
using UnityEngine;

namespace Axiom.Phygital
{
    /// <summary>
    /// Legacy response model kept for backwards compatibility with existing serialized references.
    /// </summary>
    [Serializable]
    public class FoodAnalysisResponse
    {
        public float score;
        public string category;
        public string reasoning;
    }

    /// <summary>
    /// Legacy compatibility shim. Food image analysis is intentionally disabled.
    /// </summary>
    [DisallowMultipleComponent]
    public class FoodAnalysisAPI : MonoBehaviour
    {
        private static FoodAnalysisAPI _instance;

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
                Destroy(this);
            }
        }

        public void AnalyzeFood(Texture2D texture, Action<FoodAnalysisResponse> onComplete)
        {
            onComplete?.Invoke(null);
        }

        public void AnalyzeFoodFromWebCam(WebCamTexture webCamTexture, Action<FoodAnalysisResponse> onComplete)
        {
            onComplete?.Invoke(null);
        }

        public void TestConnection(Action<bool> onComplete)
        {
            onComplete?.Invoke(false);
        }
    }
}
