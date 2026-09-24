using System.Collections;
using UnityEngine;

public class MeleeEnemy : Enemy
{
    [Header("Melee")]
    [SerializeField] private DetectionZone attackZone;
    [SerializeField] private GameObject attackHitbox;
    [SerializeField] private float chaseSpeed = 3f;
    [SerializeField] private float attackWindup = 0.35f;
    [SerializeField] private float attackActiveTime = 0.2f;
    [SerializeField] private float attackRecovery = 0.3f;
    [SerializeField] private float attackCooldown = 0.8f;

    [Header("Patrol")]
    [SerializeField] private float patrolSpeed = 1.2f;
    [SerializeField] private float patrolDistance = 2.5f;

    [Header("Edge check")]
    [SerializeField] private Transform groundAheadCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 1f;

    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private bool attacking;
    private float nextAttackTime;
    private float startX;
    private int patrolDir = 1;

    protected override void Awake()
    {
        base.Awake();
        startX = transform.position.x;
        patrolDir = facing;
        if (attackHitbox) attackHitbox.SetActive(false);
    }

    protected override void Tick()
    {
        if (attacking)
        {
            SetHorizontalVelocity(0f);
            return;
        }

        var target = Target;
        if (target == null)
        {
            Patrol();
            return;
        }

        FaceTarget(target);

        bool inRange = attackZone && attackZone.HasTarget;
        if (inRange)
        {
            SetHorizontalVelocity(0f);
            if (Time.time >= nextAttackTime) StartCoroutine(Attack());
        }
        else
        {
            SetHorizontalVelocity(GroundAhead() ? facing * chaseSpeed : 0f);
        }
    }

    private void Patrol()
    {
        if (patrolDistance <= 0f)
        {
            SetHorizontalVelocity(0f);
            return;
        }

        float offset = transform.position.x - startX;
        if ((offset > patrolDistance && patrolDir > 0) || (offset < -patrolDistance && patrolDir < 0) || !GroundAhead())
            patrolDir = -patrolDir;

        Face(patrolDir);
        SetHorizontalVelocity(patrolDir * patrolSpeed);
    }

    private IEnumerator Attack()
    {
        attacking = true;
        animator.TrySetTrigger(AttackHash);

        yield return new WaitForSeconds(attackWindup);
        if (attackHitbox) attackHitbox.SetActive(true);

        yield return new WaitForSeconds(attackActiveTime);
        if (attackHitbox) attackHitbox.SetActive(false);

        nextAttackTime = Time.time + attackCooldown;
        yield return new WaitForSeconds(attackRecovery);
        attacking = false;
    }

    private bool GroundAhead()
    {
        if (!groundAheadCheck) return true;
        return Physics2D.Raycast(groundAheadCheck.position, Vector2.down, groundCheckDistance, groundLayer);
    }

    protected override void OnDied()
    {
        if (attackHitbox) attackHitbox.SetActive(false);
        attacking = false;
        base.OnDied();
    }

    private void OnDrawGizmosSelected()
    {
        if (!groundAheadCheck) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(groundAheadCheck.position, groundAheadCheck.position + Vector3.down * groundCheckDistance);
    }
}
