using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// エディター上での確認用。押したボタンの人数になるよう、テスト用ビルドを追加ウィンドウとして起動する
/// （2 → 1 個、3 → 2 個、4 → 3 個）。追加ウィンドウは MultiPlay 画面から始まる。
/// 起動したウィンドウは再生終了時にまとめて閉じる。ビルド上ではボタンを隠す。
/// </summary>
public class LocalTestWindowLauncher : MonoBehaviour
{
    [Serializable]
    struct Option
    {
        [Tooltip("自分を含めた合計人数")]
        public int totalPlayers;
        public Button button;
    }

    [SerializeField] private Option[] options;

    [SerializeField] private Vector2Int windowSize = new Vector2Int(960, 540);

    static readonly List<Process> launchedProcesses = new List<Process>();

    UnityAction[] listeners;

    void Awake()
    {
#if !UNITY_EDITOR
        foreach (Option option in options)
            option.button.gameObject.SetActive(false);
        enabled = false;
#endif
    }

    void OnEnable()
    {
        listeners = new UnityAction[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int total = options[i].totalPlayers;
            listeners[i] = () => Launch(total - 1);
            options[i].button.onClick.AddListener(listeners[i]);
        }
    }

    void OnDisable()
    {
        if (listeners == null)
            return;

        for (int i = 0; i < options.Length; i++)
            options[i].button.onClick.RemoveListener(listeners[i]);
    }

    void Launch(int windowCount)
    {
        string path = LocalTestBuildPaths.ExecutablePath;
        if (!File.Exists(path))
        {
            Debug.LogError("[LocalTest] テスト用ビルドがありません。メニュー Tools/CJ/Local テストビルドを作成 を実行してください。");
            return;
        }

        CloseLaunchedWindows();
        for (int i = 0; i < windowCount; i++)
        {
            string arguments =
                $"-screen-fullscreen 0 -screen-width {windowSize.x} -screen-height {windowSize.y} " +
                $"{LaunchArguments.StartSelectKey} {TitleSelectManager.TitleSelect.MultiPlay} " +
                $"{LaunchArguments.WindowIndexKey} {i}";
            launchedProcesses.Add(Process.Start(path, arguments));
        }

        Debug.Log($"[LocalTest] 追加ウィンドウを {windowCount} 個起動しました");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterQuitHandler()
    {
        Application.quitting -= CloseLaunchedWindows;
        Application.quitting += CloseLaunchedWindows;
    }

    static void CloseLaunchedWindows()
    {
        foreach (Process process in launchedProcesses)
        {
            try
            {
                if (process != null && !process.HasExited)
                    process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
        }

        launchedProcesses.Clear();
    }
}
