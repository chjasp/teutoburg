using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class WhoopClient : MonoBehaviour
{
    [SerializeField] private string brokerUrl;
    [SerializeField] private string apiKey;
    [SerializeField] private string userId;

    void Start()
    {
        StartCoroutine(GetSleepPerformance());
    }

    IEnumerator GetSleepPerformance()
    {
        // Get access token
        string tokenUrl = $"{brokerUrl}/access-token?app_user_id={userId}";
        using (UnityWebRequest request = UnityWebRequest.Get(tokenUrl))
        {
            request.SetRequestHeader("X-Api-Key", apiKey);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Token error: {request.error}");
                yield break;
            }

            string token = request.downloadHandler.text;

            // Get yesterday's sleep data
            DateTime yesterday = DateTime.UtcNow.AddDays(-1);
            string start = yesterday.ToString("yyyy-MM-ddT00:00:00Z");
            string end = DateTime.UtcNow.ToString("yyyy-MM-ddT00:00:00Z");
            
            string sleepUrl = $"https://api.prod.whoop.com/developer/v2/activity/sleep?start={start}&end={end}";
            
            using (UnityWebRequest sleepRequest = UnityWebRequest.Get(sleepUrl))
            {
                sleepRequest.SetRequestHeader("Authorization", $"Bearer {token}");
                yield return sleepRequest.SendWebRequest();

                if (sleepRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Sleep data error: {sleepRequest.error}");
                    yield break;
                }

                // Parse JSON to get sleep_performance_percentage
                string json = sleepRequest.downloadHandler.text;
                SleepResponse response = JsonUtility.FromJson<SleepResponse>(json);
                
                if (response.records.Length > 0)
                {
                    float performance = response.records[0].score.sleep_performance_percentage;
                    Debug.Log($"Sleep Performance: {performance}%");
                }
                else
                {
                    Debug.Log("No sleep records found");
                }
            }
        }
    }
}

[Serializable]
public class SleepResponse
{
    public SleepRecord[] records;
}

[Serializable]
public class SleepRecord
{
    public Score score;
}

[Serializable]
public class Score
{
    public float sleep_performance_percentage;
}

