using UnityEngine;

[DisallowMultipleComponent]
public class EnemySpawnPoint : MonoBehaviour
{
	[Tooltip("Random offset radius added around this point when spawning.")]
	public float randomRadius = 0f;

	void OnDrawGizmosSelected()
	{
		Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
		Gizmos.DrawSphere(transform.position, 0.25f);
		if (randomRadius > 0f)
		{
			Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
			Gizmos.DrawWireSphere(transform.position, randomRadius);
		}
	}
}


