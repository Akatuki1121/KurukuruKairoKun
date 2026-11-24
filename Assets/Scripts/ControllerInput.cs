using UnityEngine;
using UnityEngine.InputSystem;

public struct ControllerState
{
    public float RotationDeltaDegrees;
    public float RotationSpeedDegreesPerSecond;
    public int RotationDirection;
    public Vector2 Tilt;
    public bool Shaken;
    public float ShakeIntensity;
}

public interface IGameInput
{
    ControllerState Read(float deltaTime);
}

public sealed class DebugGameInput : IGameInput
{
    public ControllerState Read(float deltaTime)
    {
        Keyboard keyboard = Keyboard.current;
        float direction = 0f;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed) direction -= 1f;
            if (keyboard.dKey.isPressed) direction += 1f;
        }

        float wheelDelta = 0f;
        float mouseShakeIntensity = 0f;
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.isPressed) direction -= 1f;
            if (Mouse.current.rightButton.isPressed) direction += 1f;
            wheelDelta = Mouse.current.scroll.ReadValue().y * GameTuning.MouseWheelDegreesPerNotch;
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            float mouseSpeed = deltaTime > 0f ? mouseDelta.magnitude / deltaTime : 0f;
            mouseShakeIntensity = Mathf.Clamp01(mouseSpeed / GameTuning.DebugMouseShakePixelsPerSecond);
        }

        bool shaken = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        bool slowRotation = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        float debugSpeed = slowRotation ? GameTuning.DebugSlowRotationSpeed : GameTuning.DebugRotationSpeed;
        float keyboardSpeed = Mathf.Abs(direction) * debugSpeed;
        float delta = direction * keyboardSpeed * deltaTime + wheelDelta;
        float wheelSpeed = deltaTime > 0f ? Mathf.Abs(wheelDelta) / deltaTime : 0f;
        Vector2 tilt = Vector2.zero;
        if (keyboard != null)
        {
            tilt.x = (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);
            tilt.y = (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f);
        }
        return new ControllerState
        {
            RotationDeltaDegrees = delta,
            RotationSpeedDegreesPerSecond = Mathf.Max(keyboardSpeed, wheelSpeed),
            RotationDirection = delta > 0f ? 1 : delta < 0f ? -1 : 0,
            Tilt = tilt,
            Shaken = shaken,
            ShakeIntensity = shaken ? 1f : mouseShakeIntensity
        };
    }
}

public sealed class ControllerInput : MonoBehaviour
{
    [SerializeField] private bool useSerialInput;
    [SerializeField] private string portName = "COM3";
    private IGameInput activeInput;
    private DebugGameInput debugInput;
    private SerialGameInput serialInput;
    public ControllerState State { get; private set; }

    private void Awake()
    {
        debugInput = new DebugGameInput();
        serialInput = useSerialInput ? new SerialGameInput(portName) : null;
        bool serialReady = serialInput != null && serialInput.IsAvailable;
        if (useSerialInput && !serialReady)
            Debug.LogWarning("Serial input is not available. Falling back to debug input.");
        activeInput = serialReady ? (IGameInput)serialInput : debugInput;
    }

    private void Update() { State = activeInput.Read(Time.deltaTime); }
    private void OnDestroy() { serialInput?.Dispose(); }
}

