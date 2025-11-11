// HKExerciseBridge.cs
using System.Runtime.InteropServices;
using UnityEngine;

namespace Teutoburg.Health
{
	// Persistent singleton receiver for iOS HealthKit exercise minutes (yesterday's total).
	public class HKExerciseBridge : MonoBehaviour
	{
		private const string GameObjectName = "HKExerciseBridge";
		private const string CallbackMethod = nameof(OnExerciseMinutesReceived);

		private static HKExerciseBridge _instance;

		// Last retrieved exercise minutes for yesterday. Negative means unknown/not fetched.
		public static float YesterdayExerciseMinutes { get; private set; } = -1f;

		// Simple status string for debugging UI/logs
		public static string LastStatus { get; private set; } = "NotRequested";

#if UNITY_IOS && !UNITY_EDITOR
		[DllImport("__Internal")]
		private static extern void HKExercise_RequestYesterdayMinutes(string gameObjectName, string callbackMethod);
#else
		private static void HKExercise_RequestYesterdayMinutes(string gameObjectName, string callbackMethod)
		{
			// Editor/other platforms: simulate 25 minutes for easy testing
			Debug.Log($"[HKExerciseBridge] Simulating exercise minutes in editor for {gameObjectName}.{callbackMethod}");
			var go = GameObject.Find(gameObjectName);
			go?.SendMessage(callbackMethod, "25.0");
		}
#endif

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Bootstrap()
		{
			if (_instance != null) return;
			var go = new GameObject(GameObjectName);
			DontDestroyOnLoad(go);
			_instance = go.AddComponent<HKExerciseBridge>();
		}

		public static void RequestYesterdayExerciseMinutes()
		{
			LastStatus = "Requesting";
			HKExercise_RequestYesterdayMinutes(GameObjectName, CallbackMethod);
		}

		private void Awake()
		{
			// Trigger an early authorization/request so the system prompt appears at startup
			if (YesterdayExerciseMinutes < 0f && LastStatus == "NotRequested")
			{
				RequestYesterdayExerciseMinutes();
			}
		}

		// Called by native iOS plugin via UnitySendMessage
		private void OnExerciseMinutesReceived(string message)
		{
			if (string.IsNullOrEmpty(message))
			{
				LastStatus = "EmptyMessage";
				return;
			}

			if (message.StartsWith("ERROR:"))
			{
				LastStatus = message;
				Debug.LogWarning("[HKExerciseBridge] " + message);
				return;
			}

			if (float.TryParse(message, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var minutes))
			{
				YesterdayExerciseMinutes = minutes;
				LastStatus = "OK";
				Debug.Log($"[HKExerciseBridge] Yesterday exercise minutes: {YesterdayExerciseMinutes}");
			}
			else
			{
				LastStatus = "ParseError:" + message;
				Debug.LogWarning($"[HKExerciseBridge] Could not parse exercise minutes: '{message}'");
			}
		}
	}
}


