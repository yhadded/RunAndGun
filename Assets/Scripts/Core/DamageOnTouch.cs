using System.Collections.Generic;
using UnityEngine;

public class DamageOnTouch : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private Team ownerTeam = Team.Enemy;
    [SerializeField] private bool hurtsEveryone = false;
    [SerializeField] private bool instantKill = false;
    [SerializeField] private float repeatDelay = 0.5f;

    private readonly Dictionary<IDamageable, float> nextHitTime = new();

    private void OnDisable()
    {
        nextHitTime.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other) => TryDamage(other);
    private void OnTriggerStay2D(Collider2D other) => TryDamage(other);
    private void OnCollisionEnter2D(Collision2D collision) => TryDamage(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => TryDamage(collision.collider);

    private void TryDamage(Collider2D other)
    {
        if (other.isTrigger) return;

        var target = other.GetComponentInParent<IDamageable>();
        if (target == null || target.IsDead) return;
        if (!hurtsEveryone && target.Team == ownerTeam) return;

        if (nextHitTime.TryGetValue(target, out float t) && Time.time < t) return;
        nextHitTime[target] = Time.time + repeatDelay;

        if (instantKill)
        {
            target.Kill();
            return;
        }

        Vector2 dir = (other.transform.position - transform.position);
        dir = new Vector2(Mathf.Sign(dir.x), 0.5f).normalized;
        target.TakeDamage(damage, dir);
    }
}
