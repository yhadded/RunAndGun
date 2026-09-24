using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerShooter playerShooter;

    [Header("Health")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Text healthText;
    [SerializeField] private bool useColorGradient = false;
    [SerializeField] private Gradient healthColor;

    [Header("Score / weapon")]
    [SerializeField] private Text scoreText;
    [SerializeField] private Text weaponText;

    private void Start()
    {
        if (!playerHealth)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player)
            {
                playerHealth = player.GetComponent<Health>();
                playerShooter = player.GetComponent<PlayerShooter>();
            }
        }

        if (playerHealth)
        {
            playerHealth.HealthChanged += OnHealthChanged;
            OnHealthChanged(playerHealth.Current, playerHealth.Max);
        }
        if (playerShooter)
        {
            playerShooter.WeaponChanged += OnWeaponChanged;
            OnWeaponChanged();
        }
        if (GameManager.Instance)
        {
            GameManager.Instance.ScoreChanged += OnScoreChanged;
            OnScoreChanged(GameManager.Score);
        }
    }

    private void OnDestroy()
    {
        if (playerHealth) playerHealth.HealthChanged -= OnHealthChanged;
        if (playerShooter) playerShooter.WeaponChanged -= OnWeaponChanged;
        if (GameManager.Instance) GameManager.Instance.ScoreChanged -= OnScoreChanged;
    }

    private void OnHealthChanged(int current, int max)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        if (healthFill)
        {
            healthFill.fillAmount = ratio;
            if (useColorGradient && healthColor != null) healthFill.color = healthColor.Evaluate(ratio);
        }
        if (healthText) healthText.text = $"{current} / {max}";
    }

    private void OnScoreChanged(int score)
    {
        if (scoreText) scoreText.text = $"SCORE {score:000000}";
    }

    private void OnWeaponChanged()
    {
        if (!weaponText || !playerShooter) return;
        var w = playerShooter.CurrentWeapon;
        if (w == null) return;
        weaponText.text = w.InfiniteAmmo ? $"{w.name}  --" : $"{w.name}  {w.ammo}";
    }
}
