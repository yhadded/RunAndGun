using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(PlayerInputReader), typeof(Health))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float acceleration = 70f;
    [SerializeField] private float deceleration = 90f;

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 14f;
    [SerializeField] private float jumpCutMultiplier = 0.5f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float maxFallSpeed = 20f;

    [Header("Ground check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.6f, 0.1f);
    [SerializeField] private LayerMask groundLayer;

    [Header("Hurt")]
    [SerializeField] private Vector2 knockback = new Vector2(5f, 6f);
    [SerializeField] private float hurtControlLock = 0.25f;

    [Header("Visual")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool spriteFacesRight = true;
    [SerializeField] private AudioClip jumpClip;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int VelocityYHash = Animator.StringToHash("VelocityY");
    private static readonly int HurtHash = Animator.StringToHash("Hurt");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    private Rigidbody2D rb;
    private PlayerInputReader input;
    private Health health;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float controlLockTimer;
    private bool controlEnabled = true;
    private bool isJumping;

    public int Facing { get; private set; } = 1;
    public bool IsGrounded { get; private set; }
    public PlayerInputReader Input => input;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        input = GetComponent<PlayerInputReader>();
        health = GetComponent<Health>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    private void Update()
    {
        if (health.IsDead) return;

        CheckGround();

        coyoteTimer = IsGrounded ? coyoteTime : coyoteTimer - Time.deltaTime;
        jumpBufferTimer = CanControl && input.JumpPressed ? jumpBufferTime : jumpBufferTimer - Time.deltaTime;
        controlLockTimer -= Time.deltaTime;

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            IsGrounded = false;
            isJumping = true;
            if (jumpClip) AudioSource.PlayClipAtPoint(jumpClip, transform.position);
        }

        if (isJumping && rb.linearVelocity.y <= 0f) isJumping = false;
        if (isJumping && !input.JumpHeld)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
            isJumping = false;
        }

        float h = CanControl ? input.Move.x : 0f;
        if (Mathf.Abs(h) > 0.1f) SetFacing(h > 0f ? 1 : -1);

        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (health.IsDead) return;

        float h = CanControl ? input.Move.x : 0f;
        if (Mathf.Abs(h) < 0.1f) h = 0f;

        float target = h * moveSpeed;
        float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
        float vx = controlLockTimer > 0f ? rb.linearVelocity.x : Mathf.MoveTowards(rb.linearVelocity.x, target, rate * Time.fixedDeltaTime);
        float vy = Mathf.Max(rb.linearVelocity.y, -maxFallSpeed);

        rb.linearVelocity = new Vector2(vx, vy);
    }

    private bool CanControl => controlEnabled && controlLockTimer <= 0f;

    public void SetControlEnabled(bool value)
    {
        controlEnabled = value;
        if (!value) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void CheckGround()
    {
        if (!groundCheck)
        {
            IsGrounded = false;
            return;
        }
        bool touching = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
        IsGrounded = touching && rb.linearVelocity.y <= 0.05f;
    }

    private void SetFacing(int dir)
    {
        if (dir == Facing) return;
        Facing = dir;
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (spriteFacesRight ? dir : -dir);
        transform.localScale = s;
    }

    private void UpdateAnimator()
    {
        animator.TrySetFloat(SpeedHash, Mathf.Abs(rb.linearVelocity.x));
        animator.TrySetBool(GroundedHash, IsGrounded);
        animator.TrySetFloat(VelocityYHash, rb.linearVelocity.y);
    }

    private void OnDamaged(Vector2 hitDirection)
    {
        float side = Mathf.Abs(hitDirection.x) > 0.01f ? Mathf.Sign(hitDirection.x) : -Facing;
        rb.linearVelocity = new Vector2(side * knockback.x, knockback.y);
        isJumping = false;
        controlLockTimer = hurtControlLock;
        animator.TrySetTrigger(HurtHash);
    }

    private void OnDied()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        animator.TrySetBool(IsDeadHash, true);
        input.enabled = false;
        if (GameManager.Instance) GameManager.Instance.PlayerDied();
    }

    private void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }
}
