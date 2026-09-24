using System.Collections;
using UnityEngine;

public class RangedEnemy : Enemy
{
    [Header("Ranged")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private bool aimAtTarget = true;
    [SerializeField] private float targetHeightOffset = 0.6f;
    [SerializeField] private float fireCooldown = 1.6f;
    [SerializeField] private float shootWindup = 0.3f;
    [SerializeField] private int burstCount = 1;
    [SerializeField] private float burstInterval = 0.15f;
    [SerializeField] private AudioClip shotClip;

    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int AlertHash = Animator.StringToHash("Alert");

    private bool shooting;
    private float nextShotTime;

    protected override void Awake()
    {
        base.Awake();
        if (!muzzle) muzzle = transform;
    }

    protected override void Tick()
    {
        SetHorizontalVelocity(0f);

        var target = Target;
        animator.TrySetBool(AlertHash, target != null);
        if (target == null) return;

        if (!shooting) FaceTarget(target);

        if (!shooting && Time.time >= nextShotTime) StartCoroutine(Shoot());
    }

    private IEnumerator Shoot()
    {
        shooting = true;
        animator.TrySetTrigger(ShootHash);
        yield return new WaitForSeconds(shootWindup);

        for (int i = 0; i < burstCount; i++)
        {
            var target = Target;
            if (target == null) break;

            Vector2 dir;
            if (aimAtTarget)
            {
                Vector3 aimPoint = target.position + Vector3.up * targetHeightOffset;
                dir = (aimPoint - muzzle.position).normalized;
            }
            else
            {
                dir = new Vector2(facing, 0f);
            }

            Projectile p = Instantiate(projectilePrefab, muzzle.position, Quaternion.identity);
            p.Launch(dir, health.Team);
            if (shotClip) AudioSource.PlayClipAtPoint(shotClip, muzzle.position, 0.5f);

            if (i < burstCount - 1) yield return new WaitForSeconds(burstInterval);
        }

        nextShotTime = Time.time + fireCooldown;
        shooting = false;
    }
}
