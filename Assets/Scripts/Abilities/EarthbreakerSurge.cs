using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class EarthbreakerSurge : MonoBehaviour
{
	[Header("Setup")]
	[SerializeField] private Transform origin; // where the slam originates (defaults to this.transform)
    private Transform ownerRoot; // cached root to avoid self-damage

	[Header("Rings")]
	[SerializeField] private int ringCount = 3;           // how many ripples to spawn
	[SerializeField] private float ringStartInterval = 0.2f; // delay between ripples starting
	[SerializeField] private float ringSpeed = 12f;       // units/sec expansion speed of each ring
	[SerializeField] private float maxRadius = 18f;       // how far each ring travels
	[SerializeField] private float ringThickness = 1.2f;  // band thickness for hit detection
	[SerializeField] private LayerMask hitMask = ~0;      // who can be hit

	[Header("Damage")]
	[SerializeField] private int damagePerRing = 150;

	[Header("Visuals (optional)")]
	[SerializeField] private AoEIndicator ringIndicatorPrefab; // optional; created at runtime if not set
	[SerializeField] private DamageText damageTextPrefab;      // optional; if assigned, shows damage numbers

	void Awake()
	{
		if (origin == null) origin = transform;
		ownerRoot = origin.root;
	}

	// UI Button-friendly entry point
	public void CastEarthbreaker()
	{
		StartCoroutine(EarthbreakerRoutine());
	}

	// Input System callback-friendly wrapper (optional)
	public void OnCastEarthbreaker(InputAction.CallbackContext ctx)
	{
		if (!ctx.performed) return;
		CastEarthbreaker();
	}

	private IEnumerator EarthbreakerRoutine()
	{
		Vector3 center = origin != null ? origin.position : transform.position;
		center.y += 0.02f; // slight lift to avoid Z-fighting for visuals

		// Launch multiple rings with a small offset between them
		for (int i = 0; i < ringCount; i++)
		{
			StartCoroutine(RingRoutine(center, i * ringStartInterval));
		}
		yield break;
	}

	private IEnumerator RingRoutine(Vector3 center, float startDelay)
	{
		if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

		var alreadyHitThisRing = new HashSet<Collider>();
		AoEIndicator indicator = CreateIndicator();
		float radius = 0f;
		float halfThickness = Mathf.Max(0.01f, ringThickness * 0.5f);

		while (radius < maxRadius)
		{
			radius += ringSpeed * Time.deltaTime;

			// Update simple ring visual
			if (indicator != null)
			{
				indicator.ShowCircle(center, radius);
			}

			// Apply damage once as the band passes over targets
			float queryRadius = radius + halfThickness;
			Collider[] hits = Physics.OverlapSphere(center, queryRadius, hitMask, QueryTriggerInteraction.Ignore);
			for (int i = 0; i < hits.Length; i++)
			{
				var col = hits[i];
				if (alreadyHitThisRing.Contains(col)) continue;
				// Prevent self-damage by skipping any collider under the caster's root
				if (ownerRoot != null && col.transform.root == ownerRoot) continue;

				// Compute planar distance to approximate ground ring
				Vector3 pos = col.bounds.center;
				float dx = pos.x - center.x;
				float dz = pos.z - center.z;
				float planarDist = Mathf.Sqrt(dx * dx + dz * dz);

				if (planarDist >= radius - halfThickness && planarDist <= radius + halfThickness)
				{
					var damageable = col.GetComponentInParent<IDamageable>();
					if (damageable != null)
					{
						damageable.TakeDamage(damagePerRing);
						ShowDamageText(col, damagePerRing);
						alreadyHitThisRing.Add(col);
					}
				}
			}

			yield return null;
		}

		if (indicator != null) Destroy(indicator.gameObject);
	}

	private AoEIndicator CreateIndicator()
	{
		if (ringIndicatorPrefab != null)
		{
			return Instantiate(ringIndicatorPrefab);
		}
		// Minimal runtime-created indicator if no prefab was assigned
		var go = new GameObject("Earthbreaker Ring");
		var ind = go.AddComponent<AoEIndicator>();
		return ind;
	}

	private void ShowDamageText(Collider target, int amount)
	{
		if (damageTextPrefab == null) return;
		Vector3 top = target.bounds.center + Vector3.up * (target.bounds.extents.y + 0.5f);
		var dt = Instantiate(damageTextPrefab, top, Quaternion.identity, target.transform);
		dt.Init(amount);
	}
}


