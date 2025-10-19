using UnityEngine;

[DisallowMultipleComponent]
public class LunarMoon : MonoBehaviour
{
	[Header("Movement")]
	[SerializeField] private float fallGravity = 30f;
	[SerializeField] private float startHeightOffset = 12f; // spawn this far above impact point

	[Header("Damage Area")]
	[SerializeField] private float radius = 3.5f;
	[SerializeField] private int damage = 150;
	[SerializeField] private LayerMask hitMask = ~0; // default: everything

	[Header("VFX/SFX")]
	[SerializeField] private GameObject impactVfx;
	[SerializeField] private DamageText damageTextPrefab;

	private Vector3 targetPoint;
	private float verticalVelocity;
	private bool hasImpacted;
	private GameObject indicatorToDestroy;
	private Transform ownerRoot; // who spawned this moon; ignored for damage

	public float Radius => radius;

	public void SetIndicatorToDestroy(GameObject indicator)
	{
		indicatorToDestroy = indicator;
	}

	public void SetOwner(Transform owner)
	{
		ownerRoot = owner != null ? owner.root : null;
	}

	public void InitAtTarget(Vector3 groundPoint, int overrideDamage)
	{
		targetPoint = groundPoint;
		if (overrideDamage >= 0) damage = overrideDamage;
		transform.position = groundPoint + Vector3.up * startHeightOffset;
	}

	void Update()
	{
		if (hasImpacted) return;

		verticalVelocity += -fallGravity * Time.deltaTime;
		Vector3 delta = new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime;
		transform.position += delta;

		// Impact when reached or passed target Y
		if (transform.position.y <= targetPoint.y)
		{
			Impact();
		}
	}

	private void Impact()
	{
		hasImpacted = true;
		transform.position = new Vector3(transform.position.x, targetPoint.y, transform.position.z);

		if (impactVfx != null)
		{
			Instantiate(impactVfx, transform.position, Quaternion.identity);
		}

		ApplyAreaDamage();
		if (indicatorToDestroy != null)
		{
			Destroy(indicatorToDestroy);
		}
		Destroy(gameObject);
	}

	private void ApplyAreaDamage()
	{
		// Collect colliders in radius and damage any IDamageable found in parents
		Collider[] hits = Physics.OverlapSphere(transform.position, radius, hitMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < hits.Length; i++)
		{
			var col = hits[i];
			// Prevent self-damage: skip any collider under the owner's root
			if (ownerRoot != null && col.transform.root == ownerRoot) continue;
			var damageable = col.GetComponentInParent<IDamageable>();
			if (damageable != null)
			{
				damageable.TakeDamage(damage);
				ShowDamageText(col);
			}
		}
	}

	private void ShowDamageText(Collider target)
	{
		if (damageTextPrefab == null) return;
		Vector3 top = target.bounds.center + Vector3.up * (target.bounds.extents.y + 0.5f);
		var dt = Instantiate(damageTextPrefab, top, Quaternion.identity, target.transform);
		dt.Init(damage);
	}

	// Allow gizmo preview of radius in editor
	void OnDrawGizmosSelected()
	{
		Gizmos.color = new Color(0.7f, 0.8f, 1f, 0.35f);
		Gizmos.DrawSphere(Application.isPlaying ? targetPoint : transform.position, radius);
	}
}


