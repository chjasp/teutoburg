// HKExerciseBridge.cs
using System.Runtime.InteropServices;
using UnityEngine;
using Teutoburg.Core;

namespace Teutoburg.Health
{
	// Persistent singleton receiver for iOS HealthKit Active Energy (Calories)
	public class HKExerciseBridge : MonoBehaviour
	{
		private const string GameObjectName = "HKExerciseBridge";
		private const string CallbackMethod = nameof(OnCaloriesReceived);

		private static HKExerciseBridge _instance;

		// Last retrieved active energy (Calories) for yesterday. Negative means unknown/not fetched.
		public static float YesterdayCalories { get; private set; } = -1f;

		// Simple status string for debugging UI/logs
		public static string LastStatus { get; private set; } = "NotRequested";

#if UNITY_IOS && !UNITY_EDITOR
		[DllImport("__Internal")]
		private static extern void HKHealth_RequestActiveEnergy(string gameObjectName, string callbackMethod);
#else
		private static void HKHealth_RequestActiveEnergy(string gameObjectName, string callbackMethod)
		{
			// Editor/other platforms: simulate 500 calories for easy testing
			Debug.Log($"[HKExerciseBridge] Simulating active energy in editor for {gameObjectName}.{callbackMethod}");
			var go = GameObject.Find(gameObjectName);
			go?.SendMessage(callbackMethod, "500.0");
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

		public static void RequestYesterdayCalories()
		{
			LastStatus = "Requesting";
			HKHealth_RequestActiveEnergy(GameObjectName, CallbackMethod);
		}

		private void Awake()
		{
			// Trigger an early authorization/request so the system prompt appears at startup
			if (YesterdayCalories < 0f && LastStatus == "NotRequested")
			{
				RequestYesterdayCalories();
			}
		}

		// Called by native iOS plugin via UnitySendMessage
		private void OnCaloriesReceived(string message)
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

			if (float.TryParse(message, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var calories))
			{
				YesterdayCalories = calories;
				LastStatus = "OK";
				Debug.Log($"[HKExerciseBridge] Yesterday active energy (calories): {YesterdayCalories}");
				
				// Update PlayerStats
				if (PlayerStats.Instance != null)
				{
					PlayerStats.Instance.UpdateCalories(calories);
				}
			}
			else
			{
				LastStatus = "ParseError:" + message;
				Debug.LogWarning($"[HKExerciseBridge] Could not parse calories: '{message}'");
			}
		}
	}
}
