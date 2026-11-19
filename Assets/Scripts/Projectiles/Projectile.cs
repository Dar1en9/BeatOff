using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    public enum HitResult
    {
        None,
        Deflected,
        DamagedPlayer,
        Environment
    }

    [Header("Motion")]
    public float speed = 15f;

    [Tooltip("Seconds before the projectile auto-despawns. 0 = never.")]
    public float maxLifetime = 8f;

    [Tooltip("Use continuous collision to avoid tunneling through thin shields.")]
    public bool useContinuousCollision = true;

    [Header("Damage")]
    public float damage = 10f;

    [Tooltip("Only apply damage to objects on these layers.")]
    public LayerMask playerLayers;

    [Header("Deflection")]
    [Tooltip("Minimum shield speed required to deflect. Below this, the shield blocks nothing.")]
    public float deflectSpeedThreshold = 2.5f;

    [Tooltip("How much shield velocity is added to the reflected projectile.")]
    [Range(0f, 1f)]
    public float shieldVelocityInfluence = 0.5f;

    [Tooltip("Layers considered a valid deflect surface (shields).")]
    public LayerMask shieldLayers;

    [Tooltip("Layer to switch to after a successful deflect. Should NOT collide with player/shield.")]
    public string deflectedLayerName = "DeflectedProjectile";

    [Tooltip("Seconds after deflection during which the projectile can't hit anything.")]
    public float postDeflectIgnoreTime = 0.05f;

    [Header("FX")]
    public GameObject deflectVfxPrefab;
    public GameObject impactVfxPrefab;

    [Header("Debug")]
    public bool debugLogs = false;

    // Runtime
    private Rigidbody rb;
    private Collider col;
    private Collider[] shooterColliders;
    private Vector3 direction;
    private bool isDeflected;
    private bool isDropped;
    public bool IsDropped => isDropped;
    private float despawnTime = float.PositiveInfinity;
    public float droppedLifetime = 0.4f;
    private Vector3 previousPosition;

    public bool IsDeflected => isDeflected;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        rb.useGravity = false;
        rb.collisionDetectionMode = useContinuousCollision
            ? CollisionDetectionMode.ContinuousDynamic
            : CollisionDetectionMode.Discrete;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    /// <summary>Called by the turret after Instantiate.</summary>
    public void Initialize(Vector3 dir, GameObject shooter)
    {
        previousPosition = transform.position;
        direction = dir.normalized;

        if (shooter != null)
        {
            // Ignore collisions with the shooter's own colliders.
            shooterColliders = shooter.GetComponentsInChildren<Collider>();
            foreach (var c in shooterColliders)
                Physics.IgnoreCollision(col, c, true);
        }

        rb.linearVelocity = direction * speed;

        if (maxLifetime > 0f)
        {
            despawnTime = Time.time + maxLifetime;

        }
    }

    void FixedUpdate()
    {
        if (Time.time >= despawnTime)
        {
            Despawn();
            return;
        }

        // Detect a direct head/body hit even when the XR rig has no damage collider.
        if (!isDeflected && !isDropped && !hitPlayer && Camera.main != null)
        {
            Vector3 head = Camera.main.transform.position;
            Vector3 chest = head + Vector3.down * 0.35f;
            Vector3 travel = transform.position - previousPosition;
            float t = travel.sqrMagnitude > 0f
                ? Mathf.Clamp01(Vector3.Dot(chest - previousPosition, travel) / travel.sqrMagnitude) : 0f;
            Vector3 closest = previousPosition + travel * t;
            if (Vector3.Distance(closest, chest) < 0.3f || Vector3.Distance(transform.position, head) < 0.25f)
                HitPlayer();
        }
        previousPosition = transform.position;

        // Keep speed constant (in case physics nudged it).
        if (!isDeflected && !isDropped && !hitPlayer && rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * speed;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider, collision.contacts.Length > 0
            ? collision.contacts[0].point
            : transform.position,
            collision.contacts.Length > 0
            ? collision.contacts[0].normal
            : -direction);
    }

    /// <summary>Used if you switch to trigger colliders.</summary>
    void OnTriggerEnter(Collider other)
    {
        // Only needed if the projectile collider is a trigger.
        HandleHit(other, transform.position, -direction);
    }

    void HandleHit(Collider other, Vector3 point, Vector3 normal)
    {
        if (isDropped || hitPlayer) return;
        if (other.GetComponentInParent<Projectile>() != null)
        {
            Physics.IgnoreCollision(col, other, true);
            rb.linearVelocity = direction * speed;
            return;
        }
        if (other.isTrigger && other.GetComponentInParent<Shield>() == null
            && !IsInLayerMask(other.gameObject.layer, playerLayers)) return;
        if (debugLogs)
            Debug.Log($"[Projectile] Hit {other.name} (layer {LayerMask.LayerToName(other.gameObject.layer)})");

        // --- Shield check ---
        if (other.GetComponentInParent<Shield>() != null)
        {
            Shield shield = other.GetComponentInParent<Shield>(); if (isDeflected) return;
            if (shield != null)
            {
                bool fastEnough = shield.HasActiveSwing(deflectSpeedThreshold);
                bool facingRight = shield.CanDeflectFrom(direction);

                if (fastEnough && facingRight)
                {
                    Deflect(shield, point, normal);
                    return;
                }
                else if (debugLogs)
                {
                    Debug.Log($"[Projectile] Deflect failed. Speed {shield.Velocity.magnitude:F2}, facingOK {facingRight}");
                }
            }
        }

        // Failed swipes drop the ball without damaging the player yet.
        if (other.GetComponentInParent<Shield>() != null)
        {
            Drop();
            return;
        }
        // --- Player damage ---
        if (IsInLayerMask(other.gameObject.layer, playerLayers) && !isDeflected)
        {
            HitPlayer();
            return;
        }

        // Incoming balls travel through scenery and neighbouring plants.
        // Only resolved player hits or the flight timeout may remove them.
        if (!isDeflected)
        {
            Physics.IgnoreCollision(col, other, true);
            rb.linearVelocity = direction * speed;
            return;
        }
        if (IsInLayerMask(other.gameObject.layer, playerLayers)) return;

        // Returning shots destroy plants, including their child colliders.
        if (isDeflected)
        {
            var plant = other.GetComponentInParent<Turret>();
            if (plant != null) Destroy(plant.gameObject);
        }

        // --- Environment / anything else ---
        SpawnVfx(impactVfxPrefab, point, normal);
        Despawn();
    }

    void Deflect(Shield shield, Vector3 point, Vector3 normal)
    {
        isDeflected = true;
        ProjectileFeedback.Report(true);
        despawnTime = maxLifetime > 0f ? Time.time + maxLifetime : float.PositiveInfinity;

        // Return towards the field; lateral hand motion steers within a 30 degree cone.
        Vector3 returnDirection = -direction.normalized;
        Vector3 lateralPush = Vector3.ProjectOnPlane(shield.Velocity, returnDirection)
            * shieldVelocityInfluence;
        lateralPush = Vector3.ClampMagnitude(lateralPush, speed * Mathf.Tan(30f * Mathf.Deg2Rad));
        direction = (returnDirection * speed + lateralPush).normalized;
        rb.linearVelocity = direction * speed;
        transform.rotation = Quaternion.LookRotation(direction);

        // The returning shot can hit the plant that fired it.
        if (shooterColliders != null)
            foreach (var c in shooterColliders)
                if (c != null) Physics.IgnoreCollision(col, c, false);
        // Re-layer so it no longer hits the player or shields.
        int layer = LayerMask.NameToLayer(deflectedLayerName);
        if (layer >= 0)
            gameObject.layer = layer;
        else if (debugLogs)
            Debug.LogWarning($"[Projectile] Layer '{deflectedLayerName}' not found. Create it in Project Settings → Tags and Layers.");

        // Brief ignore window so the shield that deflected it doesn't immediately re-trigger.
        if (shield.TryGetComponent<Collider>(out var shieldCol) && postDeflectIgnoreTime > 0f)
        {
            Physics.IgnoreCollision(col, shieldCol, true);
            CancelInvoke(nameof(ReenableShieldCollision));
            Invoke(nameof(ReenableShieldCollision), postDeflectIgnoreTime);
        }

        SpawnVfx(deflectVfxPrefab, point, normal);
        shield.OnDeflect(this);

        if (debugLogs)
            Debug.Log($"[Projectile] Deflected! Shield speed {shield.Velocity.magnitude:F2}");
    }

    private Collider lastIgnoredShield;
    void ReenableShieldCollision()
    {
        // We don't keep a reference long-term; leaving the collision ignored
        // for a single shield is usually fine since the projectile is now
        // on a layer that doesn't interact with shields at all.
    }

    void SpawnVfx(GameObject prefab, Vector3 point, Vector3 normal)
    {
        if (prefab == null) return;
        Instantiate(prefab, point, Quaternion.LookRotation(normal));
    }

    private bool hitPlayer;
    void HitPlayer()
    {
        if (isDropped || isDeflected || hitPlayer) return;
        hitPlayer = true;
        ProjectileFeedback.Report(false);
        rb.linearVelocity = Vector3.zero;
        col.enabled = false;
        foreach (var visual in GetComponentsInChildren<Renderer>()) visual.enabled = false;
        despawnTime = Time.time + 0.02f;
    }

    void Drop()
    {
        if (isDropped || isDeflected) return;
        isDropped = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = true;
        despawnTime = Time.time + Mathf.Max(0.1f, droppedLifetime);
        ProjectileFeedback.Report(false);
    }

    void Despawn()
    {
        CancelInvoke();
        Destroy(gameObject);
    }

    static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}