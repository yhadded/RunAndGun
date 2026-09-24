using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public abstract class Enemy : MonoBehaviour
{
    [Header("Enemy")]
    [SerializeField] protected DetectionZone detectionZone;
    [SerializeField] protected Animator animator;
    [SerializeField] protected bool spriteFacesRight = true;
    [SerializeField] private int scoreValue = 100;
    [SerializeField] private float destroyDelay = 1.5f;

    [Header("Drop (bonus)")]
    [SerializeField] private GameObject[] dropPrefabs;
    [SerializeField, Range(0f, 1f)] private float dropChance = 0.25f;

    protected static readonly int SpeedHash = Animator.StringToHash("Speed");
    protected static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    protected Rigidbody2D rb;
    protected Health health;
    protected int facing = 1;

    public bool IsDead => health.IsDead;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        rb.freezeRotation = true;
        facing = transform.localScale.x >= 0f == spriteFacesRight ? 1 : -1;
    }

    protected virtual void OnEnable()
    {
        health.Died += OnDied;
    }

    protected virtual void OnDisable()
    {
        health.Died -= OnDied;
    }

    private void Update()
    {
        if (health.IsDead) return;
        Tick();
        animator.TrySetFloat(SpeedHash, Mathf.Abs(rb.linearVelocity.x));
    }

    protected abstract void Tick();

    protected Transform Target
    {
        get
        {
            if (!detectionZone || !detectionZone.HasTarget) return null;
            var d = detectionZone.Target.GetComponent<IDamageable>();
            return d != null && d.IsDead ? null : detectionZone.Target;
        }
    }

    protected void Face(float dirX)
    {
        if (Mathf.Abs(dirX) < 0.01f) return;
        facing = dirX > 0f ? 1 : -1;
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (spriteFacesRight ? facing : -facing);
        transform.localScale = s;
    }

    protected void FaceTarget(Transform target)
    {
        Face(target.position.x - transform.position.x);
    }

    protected void SetHorizontalVelocity(float vx)
    {
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    protected virtual void OnDied()
    {
        StopAllCoroutines();
        animator.TrySetBool(IsDeadHash, true);
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        if (GameManager.Instance) GameManager.Instance.AddScore(scoreValue);

        if (dropPrefabs != null && dropPrefabs.Length > 0 && Random.value < dropChance)
        {
            var prefab = dropPrefabs[Random.Range(0, dropPrefabs.Length)];
            if (prefab) Instantiate(prefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

        Destroy(gameObject, destroyDelay);
    }
}
