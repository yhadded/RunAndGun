using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset = new Vector2(0f, 1.5f);
    [SerializeField] private float lookAhead = 2.5f;
    [SerializeField] private float smoothTime = 0.2f;
    [SerializeField] private bool noBacktracking = false;

    [Header("Level bounds")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private Vector2 minBounds = new Vector2(-10f, -5f);
    [SerializeField] private Vector2 maxBounds = new Vector2(100f, 15f);

    private Camera cam;
    private PlayerController player;
    private Vector3 velocity;
    private float furthestX = float.NegativeInfinity;
    private bool locked;
    private Bounds lockBounds;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (!target)
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player) target = player.transform;
        }
        else
        {
            player = target.GetComponent<PlayerController>();
        }

        if (target) transform.position = Clamp(Desired());
    }

    private void LateUpdate()
    {
        if (!target) return;
        Vector3 goal = Clamp(Desired());
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
    }

    private Vector3 Desired()
    {
        float facing = player ? player.Facing : 1f;
        return new Vector3(
            target.position.x + offset.x + facing * lookAhead,
            target.position.y + offset.y,
            transform.position.z);
    }

    private Vector3 Clamp(Vector3 p)
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        if (noBacktracking && !locked)
        {
            furthestX = Mathf.Max(furthestX, p.x);
            p.x = furthestX;
        }

        if (useBounds)
        {
            p.x = ClampAxis(p.x, minBounds.x + halfW, maxBounds.x - halfW);
            p.y = ClampAxis(p.y, minBounds.y + halfH, maxBounds.y - halfH);
        }

        if (locked)
            p.x = ClampAxis(p.x, lockBounds.min.x + halfW, lockBounds.max.x - halfW);

        return p;
    }

    private static float ClampAxis(float v, float min, float max)
    {
        return min > max ? (min + max) * 0.5f : Mathf.Clamp(v, min, max);
    }

    public void LockTo(Bounds area)
    {
        locked = true;
        lockBounds = area;
    }

    public void Unlock()
    {
        locked = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!useBounds) return;
        Gizmos.color = Color.magenta;
        Vector3 center = (minBounds + maxBounds) * 0.5f;
        Vector3 size = maxBounds - minBounds;
        Gizmos.DrawWireCube(center, size);
    }
}
