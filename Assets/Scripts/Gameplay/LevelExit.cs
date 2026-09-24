using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LevelExit : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "";
    [SerializeField] private CombatZone requiredZone;
    [SerializeField] private GameObject lockedVisual;
    [SerializeField] private GameObject openVisual;

    private bool used;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        bool open = IsOpen;
        if (lockedVisual) lockedVisual.SetActive(!open);
        if (openVisual) openVisual.SetActive(open);
    }

    private bool IsOpen => requiredZone == null || requiredZone.IsCleared;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used || !IsOpen || !other.CompareTag("Player")) return;
        used = true;

        if (other.attachedRigidbody && other.attachedRigidbody.TryGetComponent<PlayerController>(out var player))
            player.SetControlEnabled(false);

        if (GameManager.Instance) GameManager.Instance.CompleteLevel(nextSceneName);
    }
}
