using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// コントローラーで動かす疑似カーソル。左スティックを倒すと現れ、仮想のマウスとして UI・手札・テーブルを指し、A でクリックする。
/// 十字キー・キーボード・本物のマウスを使うと消え、A は各画面の決定（ControllerInput.SubmitPressed）に戻る。
/// 起動時に Resources から生成してシーンをまたいで常駐させる。
/// </summary>
public class VirtualCursor : MonoBehaviour
{
    const string ResourcePath = "VirtualCursor";
    const float ReferenceHeight = 1080f;
    const float RealMouseMoveThreshold = 1f;

    [SerializeField] private RectTransform cursor;

    [Tooltip("倒しきったときの速さ（画面の高さ 1080 換算の画素/秒）")]
    [SerializeField] private float speed = 1400f;

    [Tooltip("この強さより倒すとカーソルを出す")]
    [SerializeField, Range(0f, 1f)] private float showThreshold = 0.3f;

    [Tooltip("倒し具合に対する速さの効き方（大きいほど小さく倒したときにゆっくり動く）")]
    [SerializeField, Min(1f)] private float responseExponent = 2f;

    static VirtualCursor instance;

    Mouse virtualMouse;
    Vector2 position;
    bool visible;

    public static bool IsVisible => instance != null && instance.visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null)
            return;

        VirtualCursor prefab = Resources.Load<VirtualCursor>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"{nameof(VirtualCursor)}: Resources/{ResourcePath} が見つかりません。");
            return;
        }

        instance = Instantiate(prefab);
        instance.name = nameof(VirtualCursor);
        DontDestroyOnLoad(instance.gameObject);
    }

    void Awake()
    {
        cursor.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        virtualMouse = InputSystem.AddDevice<Mouse>(nameof(VirtualCursor));
    }

    void OnDisable()
    {
        if (visible)
            Hide(false);
        if (virtualMouse != null && virtualMouse.added)
            InputSystem.RemoveDevice(virtualMouse);
        virtualMouse = null;
    }

    void Update()
    {
        Gamepad gamepad = Gamepad.current;
        Vector2 stick = gamepad != null ? gamepad.leftStick.ReadValue() : Vector2.zero;

        if (!visible)
        {
            if (stick.magnitude < showThreshold)
                return;
            Show();
        }
        else if (gamepad == null)
        {
            Hide(true);
            return;
        }
        else if (OtherInputUsed(gamepad, out bool byRealMouse))
        {
            Hide(!byRealMouse);
            return;
        }

        Vector2 delta = Move(stick);
        Send(delta, gamepad.buttonSouth.isPressed);
    }

    Vector2 Move(Vector2 stick)
    {
        float magnitude = Mathf.Clamp01(stick.magnitude);
        if (magnitude <= 0f)
            return Vector2.zero;

        float pixelsPerSecond = speed * Mathf.Pow(magnitude, responseExponent) * Screen.height / ReferenceHeight;
        Vector2 previous = position;
        position += stick / stick.magnitude * pixelsPerSecond * Time.unscaledDeltaTime;
        position.x = Mathf.Clamp(position.x, 0f, Screen.width - 1);
        position.y = Mathf.Clamp(position.y, 0f, Screen.height - 1);
        return position - previous;
    }

    void Send(Vector2 delta, bool leftPressed)
    {
        MouseState state = new MouseState { position = position, delta = delta }.WithButton(MouseButton.Left, leftPressed);
        InputSystem.QueueStateEvent(virtualMouse, state);
        virtualMouse.MakeCurrent();
        cursor.position = position;
    }

    void Show()
    {
        Mouse realMouse = FindRealMouse();
        position = realMouse != null ? realMouse.position.ReadValue() : new Vector2(Screen.width, Screen.height) * 0.5f;
        visible = true;
        Cursor.visible = false;
        cursor.gameObject.SetActive(true);
    }

    /// <param name="warpRealMouse">本物のマウスを疑似カーソルの位置へ移す（本物のマウスで消したときは移さない）</param>
    void Hide(bool warpRealMouse)
    {
        Send(Vector2.zero, false);
        visible = false;
        Cursor.visible = true;
        cursor.gameObject.SetActive(false);

        Mouse realMouse = FindRealMouse();
        if (realMouse == null)
            return;
        if (warpRealMouse)
            realMouse.WarpCursorPosition(position);
        realMouse.MakeCurrent();
    }

    bool OtherInputUsed(Gamepad gamepad, out bool byRealMouse)
    {
        byRealMouse = false;
        Mouse realMouse = FindRealMouse();
        if (realMouse != null
            && (realMouse.delta.ReadValue().sqrMagnitude > RealMouseMoveThreshold * RealMouseMoveThreshold
                || realMouse.leftButton.wasPressedThisFrame
                || realMouse.rightButton.wasPressedThisFrame))
        {
            byRealMouse = true;
            return true;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            return true;

        var dpad = gamepad.dpad;
        return dpad.up.wasPressedThisFrame || dpad.down.wasPressedThisFrame || dpad.left.wasPressedThisFrame || dpad.right.wasPressedThisFrame;
    }

    Mouse FindRealMouse()
    {
        foreach (InputDevice device in InputSystem.devices)
        {
            if (device is Mouse mouse && mouse != virtualMouse)
                return mouse;
        }
        return null;
    }
}
