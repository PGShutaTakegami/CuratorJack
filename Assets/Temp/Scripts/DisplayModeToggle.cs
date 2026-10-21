using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ビルドで Alt+Enter が押されたら、フルスクリーン（モニターの解像度）とウィンドウ（モニターの 3/4 の大きさの 16:9）を切り替える。
/// Unity 標準の Alt+Enter の切り替え（Allow Fullscreen Switch）はウィンドウが画面と同じ大きさになるため使わない。
/// どのシーンでも使えるよう、起動時に自動で作ってシーンをまたいで残す（エディターでは作らない）。
/// </summary>
public class DisplayModeToggle : MonoBehaviour
{
    const float WindowedScale = 0.75f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (Application.isEditor)
            return;

        var owner = new GameObject(nameof(DisplayModeToggle));
        DontDestroyOnLoad(owner);
        owner.AddComponent<DisplayModeToggle>();
    }

    /// <summary>Alt を押しているか（押しながらの Enter は画面の切り替えに使うため、決定には使わない）</summary>
    public static bool IsAltHeld(Keyboard keyboard) => keyboard != null && keyboard.altKey.isPressed;

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (IsAltHeld(keyboard) && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            Toggle();
    }

    static void Toggle()
    {
        Resolution display = Screen.currentResolution;
        if (Screen.fullScreenMode == FullScreenMode.Windowed)
        {
            Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
            Debug.Log($"[Display] フルスクリーンに切り替えました（{display.width}x{display.height}）");
            return;
        }

        int width = Mathf.RoundToInt(display.width * WindowedScale);
        int height = Mathf.RoundToInt(width * 9f / 16f);
        Screen.SetResolution(width, height, FullScreenMode.Windowed);
        Debug.Log($"[Display] ウィンドウに切り替えました（{width}x{height}）");
    }
}
