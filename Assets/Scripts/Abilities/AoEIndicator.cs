using UnityEngine;

[DisallowMultipleComponent]
public class AoEIndicator : MonoBehaviour
{
	[SerializeField] private LineRenderer lineRenderer;
	[SerializeField] private int segments = 48;
	[SerializeField] private float yOffset = 0.05f;

	public void ShowCircle(Vector3 center, float radius)
	{
		if (lineRenderer == null)
		{
			lineRenderer = GetComponent<LineRenderer>();
			if (lineRenderer == null)
			{
				lineRenderer = gameObject.AddComponent<LineRenderer>();
				lineRenderer.loop = true;
				lineRenderer.useWorldSpace = true;
				lineRenderer.positionCount = segments;
				lineRenderer.widthMultiplier = 0.05f;
				lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
				lineRenderer.startColor = new Color(0.8f, 0.9f, 1f, 0.9f);
				lineRenderer.endColor = new Color(0.8f, 0.9f, 1f, 0.9f);
			}
		}

		if (segments < 3) segments = 3;
		lineRenderer.positionCount = segments;

		float angleStep = Mathf.PI * 2f / segments;
		for (int i = 0; i < segments; i++)
		{
			float a = angleStep * i;
			float x = Mathf.Cos(a) * radius;
			float z = Mathf.Sin(a) * radius;
			lineRenderer.SetPosition(i, new Vector3(center.x + x, center.y + yOffset, center.z + z));
		}
	}
}


