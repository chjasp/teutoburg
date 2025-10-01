// DamageText.cs
using TMPro;
using UnityEngine;

public class DamageText : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;   // how long it stays
    [SerializeField] private float riseSpeed = 0.6f; // how fast it floats upward

    private float timer;
    private Transform cam;
    private TextMeshPro tmp;

    void Awake()
    {
        tmp = GetComponent<TextMeshPro>();
        cam = Camera.main ? Camera.main.transform : null; // needs MainCamera tag on your camera
    }

    public void Init(int amount)
    {
        if (tmp == null) tmp = GetComponent<TextMeshPro>();
        tmp.text = amount.ToString();
    }

    void Update()
    {
        // Rise a bit
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        // Face the camera (billboard)
        if (cam != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cam.position);
        }

        // Lifetime
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
