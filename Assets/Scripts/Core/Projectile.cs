using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 16f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private LayerMask destroyOnLayers;
    [SerializeField] private GameObject hitEffectPrefab;

    private Rigidbody2D rb;
    private Team ownerTeam;
    private bool hasHit;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        GetComponent<Collider2D>().isTrigger = true;
    }

    public void Launch(Vector2 direction, Team owner)
    {
        ownerTeam = owner;
        direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;

        rb.linearVelocity = direction * speed;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || other.isTrigger) return;

        var target = other.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            if (target.IsDead || target.Team == ownerTeam) return;
            target.TakeDamage(damage, rb.linearVelocity.normalized);
            Hit();
            return;
        }

        if (destroyOnLayers.value == 0 || (destroyOnLayers.value & (1 << other.gameObject.layer)) != 0) Hit();
    }

    private void Hit()
    {
        hasHit = true;
        if (hitEffectPrefab) Instantiate(hitEffectPrefab, transform.position, transform.rotation);
        Destroy(gameObject);
    }
}
