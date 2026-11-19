using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

[DisallowMultipleComponent]
public class Shield : MonoBehaviour
{
    public enum SpeedMode
    {
        Linear,          // Only position delta counts.
        Angular,         // Only rotation delta counts.
        LinearAndAngular // Combined (recommended for wrist-flick feel).
    }

    [Header("Velocity Estimation")]
    public SpeedMode speedMode = SpeedMode.LinearAndAngular;

    [Tooltip("How much smoothing to apply to the estimated velocity. 0 = raw, higher = smoother.")]
    [Range(0f, 0.95f)]
    public float smoothing = 0.4f;

    [Tooltip("Radius (meters) used to convert angular velocity into linear-equivalent speed.")]
    public float angularRadius = 0.25f;

    [Tooltip("Weight of the angular contribution when combining modes.")]
    [Range(0f, 2f)]
    public float angularWeight = 1f;

    [Header("Deflect Rules")]
    [Tooltip("If true, the shield must be facing the projectile within 'maxDeflectAngle' to deflect.")]
    public bool requireFacing = true;

    [Tooltip("Max angle (degrees) between the shield's forward and the incoming projectile.")]
    [Range(0f, 180f)]
    public float maxDeflectAngle = 100f;

    [Tooltip("Axis of the shield that counts as 'forward'. Use +Z for most meshes.")]
    public Vector3 shieldForwardLocal = Vector3.forward;

    [Header("XRI Haptics")]
    [Tooltip("HapticImpulsePlayer on this controller. Auto-found in parents if left blank.")]
    public HapticImpulsePlayer hapticPlayer;

    [Range(0f, 1f)] public float deflectHapticAmplitude = 0.6f;
    [Range(0f, 1f)] public float deflectHapticDuration = 0.08f;

    [Header("Optional Feedback")]
    [Tooltip("Audio source to play on a successful deflect.")]
    public AudioSource deflectAudio;

    [Tooltip("Particle system to burst on a successful deflect.")]
    public ParticleSystem deflectParticles;

    [Header("Debug")]
    public bool debugLogs = false;

    // Public read-only state
    public Vector3 Velocity { get; private set; }
    public float Speed => Velocity.magnitude;
    public bool IsDeflecting { get; private set; }

    // Internal
    private Rigidbody rb;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 rawVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            if (debugLogs) Debug.Log("[Shield] Added a kinematic Rigidbody automatically.");
        }

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; foreach (var c in GetComponentsInChildren<Collider>()) c.isTrigger = true;

        if (hapticPlayer == null)
            hapticPlayer = GetComponentInParent<HapticImpulsePlayer>();

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    void OnEnable()
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        Velocity = Vector3.zero;
        rawVelocity = Vector3.zero;
    }

    public bool HasActiveSwing(float minimumSpeed)
    {
        return rawVelocity.sqrMagnitude >= minimumSpeed * minimumSpeed
            && Velocity.sqrMagnitude >= minimumSpeed * minimumSpeed;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // --- Linear velocity ---
        Vector3 linear = (transform.position - lastPosition) / dt;

        // --- Angular velocity converted to linear-equivalent ---
        Quaternion deltaRot = transform.rotation * Quaternion.Inverse(lastRotation);
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;

        if (Mathf.Abs(angleDeg) < 0.001f) axis = Vector3.zero;
        Vector3 angularAsLinear = Vector3.Cross(axis * (angleDeg * Mathf.Deg2Rad / dt), transform.TransformDirection(shieldForwardLocal.normalized) * angularRadius);

        // --- Combine based on mode ---
        Vector3 combined;
        switch (speedMode)
        {
            case SpeedMode.Linear:
                combined = linear;
                break;
            case SpeedMode.Angular:
                combined = angularAsLinear * angularWeight;
                break;
            default:
                combined = linear + angularAsLinear * angularWeight;
                break;
        }

        rawVelocity = combined;

        // Exponential smoothing so a single jitter frame doesn't spike or drop.
        float k = 1f - Mathf.Clamp01(smoothing);
        Velocity = Vector3.Lerp(Velocity, rawVelocity, k);

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    /// <summary>
    /// Called by the projectile when it successfully deflects.
    /// </summary>
    public void OnDeflect(Projectile projectile)
    {
        IsDeflecting = true;

        // Haptics
        if (hapticPlayer != null && !hapticPlayer.SendHapticImpulse(deflectHapticAmplitude, deflectHapticDuration) && debugLogs)
            Debug.Log("[Shield] Haptic impulse was not sent (no device bound?).");

        // Audio
        if (deflectAudio != null)
            deflectAudio.Play();

        // Particles
        if (deflectParticles != null)
            deflectParticles.Play();

        if (debugLogs)
            Debug.Log($"[Shield] Deflected {projectile.name} at speed {Speed:F2} m/s");
    }

    /// <summary>
    /// Returns true if this shield is allowed to deflect a projectile coming from 'incomingDir'.
    /// </summary>
    public bool CanDeflectFrom(Vector3 incomingDir)
    {
        if (!requireFacing) return true;

        Vector3 shieldForward = transform.TransformDirection(shieldForwardLocal.normalized);
        // Angle between shield forward and the direction the projectile is traveling.
        // A projectile flying "into" the shield has incomingDir roughly opposite to shield forward.
        float angle = Vector3.Angle(shieldForward, -incomingDir);
        return angle <= maxDeflectAngle;
    }

    /// <summary>
    /// Reset deflect state. Call this from a coroutine or the projectile if needed.
    /// </summary>
    public void ClearDeflectState()
    {
        IsDeflecting = false;
    }

    void OnDrawGizmosSelected()
    {
        // Draw the shield's forward direction
        Vector3 forward = transform.TransformDirection(shieldForwardLocal.normalized);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + forward * 0.5f);

        // Draw the current velocity
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Velocity * 0.1f);

        // Draw deflect angle cone
        if (requireFacing)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
            int steps = 24;
            float half = maxDeflectAngle * Mathf.Deg2Rad;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                float t = (i / (float)steps) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Sin(half) * Mathf.Cos(t), Mathf.Sin(half) * Mathf.Sin(t), Mathf.Cos(half));
                Vector3 world = transform.TransformDirection(dir);
                if (i > 0) Gizmos.DrawLine(transform.position + prev * 0.5f, transform.position + world * 0.5f);
                prev = world;
            }
        }
    }
}