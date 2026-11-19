using System.Collections.Generic;
using UnityEngine;

public class AreaSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Prefab to spawn.")]
    [SerializeField] public GameObject prefab;

    [Tooltip("Seconds between spawns.")]
    [SerializeField] public float spawnInterval = 2f;

    [Tooltip("Seconds before each spawned object is destroyed.")]
    [SerializeField] public float lifetime = 5f;

    [Tooltip("Max objects alive at once. 0 = unlimited.")]
    [SerializeField] public int maxAlive = 0;

    [Header("Spawn Area (local to this transform)")]
    [Tooltip("Half-extents of the box on X/Z, and Y for height range.")]
    [SerializeField] public Vector3 areaSize = new Vector3(5f, 0f, 5f);

    [Tooltip("If true, spawns only on the XZ plane at this transform's Y.")]
    public bool flatArea = true;

    [Tooltip("Random rotation on spawn.")]
    public bool randomRotation = true;

    [Header("Debug")]
    public bool drawGizmos = true;

    private float timer;
    private readonly List<GameObject> alive = new List<GameObject>();

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer -= spawnInterval;
            TrySpawn();
        }
    }

    void TrySpawn()
    {
        if (prefab == null) return;

        // Clean up destroyed entries so maxAlive stays accurate.
        alive.RemoveAll(go => go == null);

        if (maxAlive > 0 && alive.Count >= maxAlive) return;

        Vector3 pos = GetRandomPointInArea();
        Quaternion rot = randomRotation ? Random.rotation : transform.rotation;

        GameObject spawned = Instantiate(prefab, pos, rot);
        alive.Add(spawned);

        if (lifetime > 0f)
            Destroy(spawned, lifetime);
    }

    Vector3 GetRandomPointInArea()
    {
        float x = Random.Range(-areaSize.x, areaSize.x);
        float z = Random.Range(-areaSize.z, areaSize.z);

        float y = flatArea ? 0f : Random.Range(-areaSize.y, areaSize.y);

        // Convert local offset to world space, respecting this object's rotation/scale.
        return transform.TransformPoint(new Vector3(x, y, z));
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 size = flatArea
            ? new Vector3(areaSize.x * 2f, 0.05f, areaSize.z * 2f)
            : areaSize * 2f;

        Gizmos.DrawCube(Vector3.zero, size);

        Gizmos.color = new Color(0f, 1f, 0.5f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, size);

        Gizmos.matrix = old;
    }
}