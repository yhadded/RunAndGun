using System;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CombatZone : MonoBehaviour
{
    [SerializeField] private GameObject[] enemies;
    [SerializeField] private GameObject[] barriers;
    [SerializeField] private bool lockCamera = true;
    [SerializeField] private AudioClip alarmClip;

    public bool IsTriggered { get; private set; }
    public bool IsCleared { get; private set; }
    public event Action Cleared;

    private CameraFollow2D cameraFollow;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        foreach (var e in enemies) if (e) e.SetActive(false);
        foreach (var b in barriers) if (b) b.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsTriggered || !other.CompareTag("Player")) return;
        IsTriggered = true;

        foreach (var e in enemies) if (e) e.SetActive(true);
        foreach (var b in barriers) if (b) b.SetActive(true);

        if (alarmClip) AudioSource.PlayClipAtPoint(alarmClip, transform.position);

        if (lockCamera)
        {
            cameraFollow = FindFirstObjectByType<CameraFollow2D>();
            if (cameraFollow) cameraFollow.LockTo(GetComponent<Collider2D>().bounds);
        }
    }

    private void Update()
    {
        if (!IsTriggered || IsCleared) return;
        if (AllEnemiesDead()) Clear();
    }

    private bool AllEnemiesDead()
    {
        foreach (var e in enemies)
        {
            if (e == null) continue;
            if (e.TryGetComponent<Health>(out var h) && !h.IsDead) return false;
        }
        return true;
    }

    private void Clear()
    {
        IsCleared = true;
        foreach (var b in barriers) if (b) b.SetActive(false);
        if (cameraFollow) cameraFollow.Unlock();
        Cleared?.Invoke();
    }
}
