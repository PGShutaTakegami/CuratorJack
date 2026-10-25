using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ビルドで Alt+Enter が押されたら、フルスクリーンとウィンドウを切り替える。
/// オプションの「表示方法」と食い違わないよう、設定を書き換えて保存し、大きさはオプションと同じ ScreenModeApplier で決める。
/// Unity 標準の Alt+Enter の切り替え（Allow Fullscreen Switch）はウィンドウが画面と同じ大きさになるため使わない。
/// どのシーンでも使えるよう、起動時に自動で作ってシーンをまたいで残す（エディターでは作らない）。
/// </summary>
public class DisplayModeToggle : MonoBehaviour
{
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

    /// <summary>設定の値ではなく今の画面の状態から反対側を選ぶ（設定と画面がずれていても、押せば必ず切り替わるように）</summary>
    static void Toggle()
    {
        GameSettings settings = GameSettingsStore.Current;
        settings.WindowMode = Screen.fullScreenMode == FullScreenMode.Windowed ? WindowMode.FullScreen : WindowMode.Windowed;
        GameSettingsStore.Save(settings);
        GameSettingsStore.Flush();
        ScreenModeApplier.Apply(settings);
    }
}
