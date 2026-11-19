using UnityEngine;

[DisallowMultipleComponent]
public class Turret : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("What the turret aims at. Usually the XR camera / head.")]
    public Transform target;

    [Tooltip("Offset from the target position to aim at (e.g. chest instead of eyes).")]
    [SerializeField] public Vector3 targetOffset = Vector3.zero;

    [Header("Prefab")]
    [SerializeField] public GameObject projectilePrefab;

    [Tooltip("Muzzle point. If null, uses this transform.")]
    [SerializeField] public Transform muzzle;

    [Header("Fire Timing")]
    [Tooltip("Minimum seconds between shots.")]
    [SerializeField] public float minInterval = 1.0f;

    [Tooltip("Maximum seconds between shots.")]
    [SerializeField] public float maxInterval = 3.0f;

    [Tooltip("Delay before the first shot after enabling.")]
    [SerializeField] public float startDelay = 0f;

    [Header("Aim")]
    [Tooltip("If true, the turret rotates to face the target before firing.")]
    public bool aimAtTarget = true;

    [Tooltip("Degrees per second when rotating toward the target.")]
    [SerializeField] public float turnSpeed = 180f;

    [Tooltip("Max distance to fire. 0 = unlimited.")]
    [SerializeField] public float maxRange = 0f;

    [Tooltip("Optional: only fire if the target is within this angle of forward.")]
    [SerializeField] public float fireAngleTolerance = 15f;

    [Header("Spread")]
    [Tooltip("Random cone spread applied to the shot direction, in degrees.")]
    public float spreadDegrees = 0f;

    [Header("Debug")]
    [SerializeField] public bool drawGizmos = true;
    [SerializeField] public bool debugLogs;

    private float nextShotTime;
    private bool isFiring;

    void OnEnable()
    {
        isFiring = true;
        nextShotTime = Time.time + startDelay + Random.Range(minInterval, maxInterval);
    }

    void OnDisable()
    {
        isFiring = false;
    }
    void Start()
    {
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
            Debug.Log("Set target succesfully");
        }
            
    }

    void Update()
    {
        if (debugLogs)
        {
            if (target == null) { Debug.LogWarning($"[Turret] {name}: target is null"); return; }
            if (projectilePrefab == null) { Debug.LogWarning($"[Turret] {name}: projectilePrefab is null"); return; }
        }

        if (!isFiring || projectilePrefab == null || target == null) return;

        Vector3 aimPoint = target.position + targetOffset;

        if (aimAtTarget)
            RotateToward(aimPoint);

        if (Time.time >= nextShotTime)
        {
            bool can = CanFire(aimPoint);
            if (debugLogs)
                Debug.Log($"[Turret] {name}: trying to fire, CanFire={can}, angle={Vector3.Angle(transform.forward, aimPoint - GetMuzzlePosition())}, dist={Vector3.Distance(GetMuzzlePosition(), aimPoint)}");

            if (can)
                Fire(aimPoint);

            nextShotTime = Time.time + Random.Range(minInterval, maxInterval);
        }
    }

    void RotateToward(Vector3 aimPoint)
    {
        Vector3 dir = aimPoint - GetMuzzlePosition();
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion desired = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, desired, turnSpeed * Time.deltaTime);
    }

    bool CanFire(Vector3 aimPoint)
    {
        Vector3 muzzlePos = GetMuzzlePosition();
        Vector3 dir = aimPoint - muzzlePos;

        if (maxRange > 0f && dir.magnitude > maxRange)
            return false;

        if (aimAtTarget && fireAngleTolerance > 0f)
        {
            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > fireAngleTolerance) return false;
        }

        return true;
    }

    
    void Fire(Vector3 aimPoint)
    {
        Vector3 muzzlePos = GetMuzzlePosition();
        Vector3 dir = (aimPoint - muzzlePos).normalized;

        if (spreadDegrees > 0f)
            dir = ApplySpread(dir, spreadDegrees);

        GameObject projectileObject = Instantiate(
            projectilePrefab,
            muzzlePos,
            Quaternion.LookRotation(dir)
        );

        Projectile p = projectileObject.GetComponent<Projectile>();

        if (p != null)
        {
            p.Initialize(dir, gameObject);
        }
        else
        {
            Debug.LogError(
                "Projectile prefab is missing the Projectile component!",
                projectileObject
            );
        }
    }


    Vector3 GetMuzzlePosition()
    {
        return muzzle != null ? muzzle.position : transform.position;
    }

    static Vector3 ApplySpread(Vector3 dir, float degrees)
    {
        // Random point inside a cone around dir.
        float rad = degrees * Mathf.Deg2Rad;
        Vector3 random = Random.insideUnitSphere * Mathf.Tan(rad);
        Quaternion rot = Quaternion.LookRotation(dir);
        return (dir + rot * new Vector3(random.x, random.y, 0f)).normalized;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.red;
        Vector3 pos = GetMuzzlePosition();
        Gizmos.DrawWireSphere(pos, 0.1f);

        if (target != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawLine(pos, target.position + targetOffset);
        }
    }
}