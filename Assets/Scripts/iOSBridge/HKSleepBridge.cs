// HKSleepBridge.cs
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Teutoburg.Core;

namespace Teutoburg.Health
{
	// Persistent singleton receiver for iOS HealthKit sleep analysis (yesterday's slept hours)
	public class HKSleepBridge : MonoBehaviour
	{
		private const string GameObjectName = "HKSleepBridge";
		private const string CallbackMethod = nameof(OnSleepHoursReceived);

		private static HKSleepBridge _instance;

		// Last retrieved sleep duration in hours for yesterday. Negative means unknown/not fetched.
		public static float YesterdaySleepHours { get; private set; } = -1f;

		// Simple status string for debugging UI/logs
		public static string LastStatus { get; private set; } = "NotRequested";

#if UNITY_IOS && !UNITY_EDITOR
		[DllImport("__Internal")]
		private static extern void HKSleep_RequestYesterdayHours(string gameObjectName, string callbackMethod);
#else
		private static void HKSleep_RequestYesterdayHours(string gameObjectName, string callbackMethod)
		{
			// Editor/other platforms: simulate 7.5 hours for easy testing
			Debug.Log($"[HKSleepBridge] Simulating sleep hours in editor for {gameObjectName}.{callbackMethod}");
			var go = GameObject.Find(gameObjectName);
			go?.SendMessage(callbackMethod, "7.5");
		}
#endif

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Bootstrap()
		{
			if (_instance != null) return;
			var go = new GameObject(GameObjectName);
			DontDestroyOnLoad(go);
			_instance = go.AddComponent<HKSleepBridge>();
		}

		public static void RequestYesterdaySleepHours()
		{
			LastStatus = "Requesting";
			HKSleep_RequestYesterdayHours(GameObjectName, CallbackMethod);
		}

		private void Awake()
		{
			// Trigger an early authorization/request so the system prompt appears at startup
			if (YesterdaySleepHours < 0f && LastStatus == "NotRequested")
			{
				RequestYesterdaySleepHours();
			}
		}

		// Called by native iOS plugin via UnitySendMessage
		private void OnSleepHoursReceived(string message)
		{
			if (string.IsNullOrEmpty(message))
			{
				LastStatus = "EmptyMessage";
				return;
			}

			if (message.StartsWith("ERROR:"))
			{
				LastStatus = message;
				Debug.LogWarning("[HKSleepBridge] " + message);
				return;
			}

			if (float.TryParse(message, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hours))
			{
				YesterdaySleepHours = hours;
				LastStatus = "OK";
				Debug.Log($"[HKSleepBridge] Yesterday sleep hours: {YesterdaySleepHours}");

				// Update PlayerStats (convert hours to seconds)
				if (PlayerStats.Instance != null)
				{
					float seconds = hours * 3600f;
					PlayerStats.Instance.UpdateSleep(seconds);
				}
			}
			else
			{
				LastStatus = "ParseError:" + message;
				Debug.LogWarning($"[HKSleepBridge] Could not parse sleep hours: '{message}'");
			}
		}
	}
}
