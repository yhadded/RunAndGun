using System;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DetectionZone : MonoBehaviour
{
    [SerializeField] private string targetTag = "Player";

    public Transform Target { get; private set; }
    public bool HasTarget => Target != null;

    public event Action<Transform> TargetEntered;
    public event Action<Transform> TargetExited;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(targetTag)) return;
        Target = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;
        TargetEntered?.Invoke(Target);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(targetTag)) return;
        var t = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;
        if (t != Target) return;
        Target = null;
        TargetExited?.Invoke(t);
    }
}
