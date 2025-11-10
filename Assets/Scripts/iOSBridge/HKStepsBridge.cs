// HKStepsBridge.cs
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Teutoburg.Health
{
    // Persistent singleton receiver for iOS HealthKit step count.
    public class HKStepsBridge : MonoBehaviour
    {
        private const string GameObjectName = "HKStepsBridge";
        private const string CallbackMethod = nameof(OnStepsReceived);

        private static HKStepsBridge _instance;

        // Last retrieved step count (yesterday). -1 means unknown/not fetched.
        public static long YesterdaySteps { get; private set; } = -1;

        // Simple status string for debugging UI/logs
        public static string LastStatus { get; private set; } = "NotRequested";

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void HKSteps_RequestYesterdaySteps(string gameObjectName, string callbackMethod);
#else
        private static void HKSteps_RequestYesterdaySteps(string gameObjectName, string callbackMethod)
        {
            // Editor/other platforms: simulate 5000 steps for easy testing
            Debug.Log($"[HKStepsBridge] Simulating steps in editor for {gameObjectName}.{callbackMethod}");
            var go = GameObject.Find(gameObjectName);
            go?.SendMessage(callbackMethod, "3000");
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject(GameObjectName);
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HKStepsBridge>();
        }

        public static void RequestYesterdaySteps()
        {
            LastStatus = "Requesting";
            HKSteps_RequestYesterdaySteps(GameObjectName, CallbackMethod);
        }

		private void Awake()
		{
			// Trigger an early authorization/request so the system prompt appears at startup
			if (YesterdaySteps < 0 && LastStatus == "NotRequested")
			{
				RequestYesterdaySteps();
			}
		}

        // Called by native iOS plugin via UnitySendMessage
        private void OnStepsReceived(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                LastStatus = "EmptyMessage";
                return;
            }

            if (message.StartsWith("ERROR:"))
            {
                LastStatus = message;
                Debug.LogWarning("[HKStepsBridge] " + message);
                return;
            }

            if (long.TryParse(message, out var steps))
            {
                YesterdaySteps = steps;
                LastStatus = "OK";
                Debug.Log($"[HKStepsBridge] Yesterday steps: {YesterdaySteps}");
            }
            else
            {
                LastStatus = "ParseError:" + message;
                Debug.LogWarning($"[HKStepsBridge] Could not parse steps: '{message}'");
            }
        }
    }
}


