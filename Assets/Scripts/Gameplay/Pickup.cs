using UnityEngine;

public enum PickupType { Health, Ammo, Score }

[RequireComponent(typeof(Collider2D))]
public class Pickup : MonoBehaviour
{
    [SerializeField] private PickupType type = PickupType.Health;
    [SerializeField] private int amount = 1;
    [SerializeField] private int weaponIndex = 1;
    [SerializeField] private AudioClip pickupClip;
    [SerializeField] private float bobAmplitude = 0.1f;
    [SerializeField] private float bobFrequency = 1.5f;

    private Vector3 startPos;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        transform.position = startPos + Vector3.up * Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !other.attachedRigidbody) return;
        var root = other.attachedRigidbody.gameObject;

        switch (type)
        {
            case PickupType.Health:
                if (!root.TryGetComponent<Health>(out var h) || h.Current >= h.Max) return;
                h.Heal(amount);
                break;
            case PickupType.Ammo:
                if (!root.TryGetComponent<PlayerShooter>(out var s)) return;
                s.AddAmmo(weaponIndex, amount);
                break;
            case PickupType.Score:
                if (GameManager.Instance) GameManager.Instance.AddScore(amount);
                break;
        }

        if (pickupClip) AudioSource.PlayClipAtPoint(pickupClip, transform.position);
        Destroy(gameObject);
    }
}
