using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 終了確認メニュー。終了ボタンでゲームを終了し、キャンセルボタンでタイトルへ戻る。
/// </summary>
public class QuitMenuController : MonoBehaviour
{
    [SerializeField] private Button quitButton;

    [SerializeField] private Button cancelButton;

    [SerializeField] private MonitorManager monitorManager;

    bool isQuitting;

    void OnEnable()
    {
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(ReturnToTitle);
    }

    void OnDisable()
    {
        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(ReturnToTitle);
    }

    void QuitGame()
    {
        if (isQuitting || (monitorManager != null && monitorManager.IsReturning))
            return;

        isQuitting = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ReturnToTitle()
    {
        if (isQuitting)
            return;

        if (monitorManager == null)
        {
            Debug.LogError($"{nameof(QuitMenuController)}: MonitorManager が未設定です。", this);
            return;
        }

        monitorManager.ReturnToTitle();
    }
}
