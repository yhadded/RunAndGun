using System;
using UnityEngine;

[Serializable]
public class Weapon
{
    public string name = "Pistol";
    public Projectile projectilePrefab;
    public float fireRate = 4f;
    public bool automatic = false;
    public float spreadAngle = 0f;
    public int maxAmmo = -1;
    public int startAmmo = 0;
    public AudioClip shotClip;
    [NonSerialized] public int ammo;

    public bool InfiniteAmmo => maxAmmo < 0;
    public bool HasAmmo => InfiniteAmmo || ammo > 0;
}

[RequireComponent(typeof(PlayerController), typeof(Health))]
public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private Weapon[] weapons = { new Weapon() };
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private float muzzleDistance = 0.7f;
    [SerializeField] private bool allowDownShotOnGround = false;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private Animator animator;

    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int AimXHash = Animator.StringToHash("AimX");
    private static readonly int AimYHash = Animator.StringToHash("AimY");

    private PlayerController controller;
    private Health health;
    private int currentIndex;
    private float nextFireTime;

    public Weapon CurrentWeapon => weapons.Length > 0 ? weapons[currentIndex] : null;
    public event Action WeaponChanged;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        health = GetComponent<Health>();
        if (!aimOrigin) aimOrigin = transform;
        foreach (var w in weapons) w.ammo = w.InfiniteAmmo ? 0 : Mathf.Clamp(w.startAmmo, 0, w.maxAmmo);
    }

    private void Update()
    {
        if (health.IsDead || CurrentWeapon == null) return;

        var input = controller.Input;
        if (!input.enabled) return;

        Vector2 aim = GetAimDirection();
        animator.TrySetFloat(AimXHash, Mathf.Abs(aim.x));
        animator.TrySetFloat(AimYHash, aim.y);

        if (input.SwitchWeaponPressed) SelectNextWeapon();

        var w = CurrentWeapon;
        bool wantsToFire = w.automatic ? input.FireHeld : input.FirePressed;
        if (wantsToFire && Time.time >= nextFireTime) Fire(w, aim);
    }

    public Vector2 GetAimDirection()
    {
        Vector2 m = controller.Input.Move;
        float h = Mathf.Abs(m.x) > 0.3f ? Mathf.Sign(m.x) : 0f;
        float v = Mathf.Abs(m.y) > 0.3f ? Mathf.Sign(m.y) : 0f;

        if (v < 0f && controller.IsGrounded && !allowDownShotOnGround) v = 0f;

        if (h == 0f && v == 0f) return new Vector2(controller.Facing, 0f);
        if (h == 0f) return new Vector2(0f, v);
        return new Vector2(h, v).normalized;
    }

    private void Fire(Weapon w, Vector2 aim)
    {
        if (!w.projectilePrefab) return;

        nextFireTime = Time.time + 1f / Mathf.Max(0.01f, w.fireRate);

        float spread = UnityEngine.Random.Range(-w.spreadAngle, w.spreadAngle);
        Vector2 dir = Quaternion.Euler(0f, 0f, spread) * aim;
        Vector3 spawnPos = aimOrigin.position + (Vector3)(aim * muzzleDistance);

        Projectile p = Instantiate(w.projectilePrefab, spawnPos, Quaternion.identity);
        p.Launch(dir, health.Team);

        if (muzzleFlash)
        {
            muzzleFlash.transform.position = spawnPos;
            muzzleFlash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            muzzleFlash.Play();
        }
        if (w.shotClip) AudioSource.PlayClipAtPoint(w.shotClip, spawnPos, 0.6f);
        animator.TrySetTrigger(ShootHash);

        if (!w.InfiniteAmmo)
        {
            w.ammo--;
            if (w.ammo <= 0) SelectWeapon(0);
            else WeaponChanged?.Invoke();
        }
    }

    public void SelectNextWeapon()
    {
        for (int i = 1; i <= weapons.Length; i++)
        {
            int idx = (currentIndex + i) % weapons.Length;
            if (weapons[idx].HasAmmo)
            {
                SelectWeapon(idx);
                return;
            }
        }
    }

    public void SelectWeapon(int index)
    {
        if (index < 0 || index >= weapons.Length || !weapons[index].HasAmmo) return;
        currentIndex = index;
        nextFireTime = 0f;
        WeaponChanged?.Invoke();
    }

    public void AddAmmo(int weaponIndex, int amount)
    {
        if (weaponIndex < 0 || weaponIndex >= weapons.Length) return;
        var w = weapons[weaponIndex];
        if (w.InfiniteAmmo) return;
        w.ammo = Mathf.Min(w.maxAmmo, w.ammo + amount);
        SelectWeapon(weaponIndex);
    }

    private void OnDrawGizmosSelected()
    {
        var origin = aimOrigin ? aimOrigin.position : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, muzzleDistance);
    }
}
