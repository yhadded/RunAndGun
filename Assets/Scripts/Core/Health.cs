using System;
using System.Collections;
using UnityEngine;

public enum Team { Player, Enemy, Neutral }

public interface IDamageable
{
    Team Team { get; }
    bool IsDead { get; }
    void TakeDamage(int amount, Vector2 hitDirection);
    void Kill();
}

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private Team team = Team.Enemy;
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float invincibilityDuration = 0f;

    [Header("Feedback")]
    [SerializeField] private SpriteRenderer[] flashRenderers;
    [SerializeField] private Color flashColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private AudioClip hurtClip;
    [SerializeField] private AudioClip deathClip;

    public event Action<int, int> HealthChanged;
    public event Action<Vector2> Damaged;
    public event Action Died;

    public Team Team => team;
    public int Current { get; private set; }
    public int Max => maxHealth;
    public bool IsDead { get; private set; }
    public bool IsInvincible => Time.time < invincibleUntil;

    private float invincibleUntil;
    private Color[] originalColors;
    private Coroutine feedbackRoutine;

    private void Awake()
    {
        Current = maxHealth;
        if (flashRenderers == null || flashRenderers.Length == 0)
            flashRenderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[flashRenderers.Length];
        for (int i = 0; i < flashRenderers.Length; i++) originalColors[i] = flashRenderers[i].color;
    }

    public void TakeDamage(int amount, Vector2 hitDirection)
    {
        if (IsDead || amount <= 0 || IsInvincible) return;

        Current = Mathf.Max(0, Current - amount);
        HealthChanged?.Invoke(Current, maxHealth);

        if (Current == 0)
        {
            Die();
            return;
        }

        PlayClip(hurtClip);
        invincibleUntil = Time.time + invincibilityDuration;
        Damaged?.Invoke(hitDirection);

        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(invincibilityDuration > 0f ? Blink() : Flash());
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        HealthChanged?.Invoke(Current, maxHealth);
    }

    public void Kill()
    {
        if (IsDead) return;
        Current = 0;
        HealthChanged?.Invoke(Current, maxHealth);
        Die();
    }

    private void Die()
    {
        IsDead = true;
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        RestoreColors();
        PlayClip(deathClip);
        Died?.Invoke();
    }

    private IEnumerator Flash()
    {
        SetColor(flashColor);
        yield return new WaitForSeconds(flashDuration);
        RestoreColors();
    }

    private IEnumerator Blink()
    {
        while (IsInvincible)
        {
            SetColor(flashColor);
            yield return new WaitForSeconds(flashDuration);
            RestoreColors();
            yield return new WaitForSeconds(flashDuration);
        }
        RestoreColors();
    }

    private void SetColor(Color c)
    {
        foreach (var r in flashRenderers) if (r) r.color = c;
    }

    private void RestoreColors()
    {
        for (int i = 0; i < flashRenderers.Length; i++)
            if (flashRenderers[i]) flashRenderers[i].color = originalColors[i];
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip) AudioSource.PlayClipAtPoint(clip, transform.position);
    }
}
