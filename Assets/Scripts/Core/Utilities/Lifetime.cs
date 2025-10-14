using UnityEngine;

public class Lifetime : MonoBehaviour
{
    [SerializeField] private float seconds = 10f;
    private float timer;
    private bool active;

    public void SetLifetime(float s)
    {
        seconds = Mathf.Max(0f, s);
        active = seconds > 0f;
        timer = 0f;
    }

    void Awake()
    {
        active = seconds > 0f;
    }

    void Update()
    {
        if (!active) return;
        timer += Time.deltaTime;
        if (timer >= seconds)
        {
            Destroy(gameObject);
        }
    }
}


