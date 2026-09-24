using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    private InputAction move;
    private InputAction jump;
    private InputAction fire;
    private InputAction switchWeapon;

    public Vector2 Move { get; private set; }
    public bool JumpPressed => enabled && jump.WasPressedThisFrame();
    public bool JumpHeld => enabled && jump.IsPressed();
    public bool FirePressed => enabled && fire.WasPressedThisFrame();
    public bool FireHeld => enabled && fire.IsPressed();
    public bool SwitchWeaponPressed => enabled && switchWeapon.WasPressedThisFrame();

    private void Awake()
    {
        move = new InputAction("Move", InputActionType.Value);
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        move.AddBinding("<Gamepad>/leftStick");

        jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        jump.AddBinding("<Gamepad>/buttonSouth");

        fire = new InputAction("Fire", InputActionType.Button, "<Keyboard>/j");
        fire.AddBinding("<Mouse>/leftButton");
        fire.AddBinding("<Gamepad>/buttonWest");

        switchWeapon = new InputAction("SwitchWeapon", InputActionType.Button, "<Keyboard>/e");
        switchWeapon.AddBinding("<Gamepad>/buttonNorth");
    }

    private void OnEnable()
    {
        move.Enable();
        jump.Enable();
        fire.Enable();
        switchWeapon.Enable();
    }

    private void OnDisable()
    {
        move.Disable();
        jump.Disable();
        fire.Disable();
        switchWeapon.Disable();
        Move = Vector2.zero;
    }

    private void OnDestroy()
    {
        move.Dispose();
        jump.Dispose();
        fire.Dispose();
        switchWeapon.Dispose();
    }

    private void Update()
    {
        Move = move.ReadValue<Vector2>();
    }
}
