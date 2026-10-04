using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトルで決定された項目に対応する画面を表示し、戻る入力でタイトルへ戻る。
/// </summary>
public class MonitorManager : MonoBehaviour
{
    [Tooltip("TitleSelect の並び順（SinglePlay, MultiPlay, Option, Credit, Trophy, Quit）で登録する")]
    [SerializeField] private GameObject[] selectedCanvas;

    [SerializeField] private CanvasGroupFader sceneFader;

    [Tooltip("タイトルを経由せず単体再生した場合に表示する画面")]
    [SerializeField] private TitleSelectManager.TitleSelect fallbackSelect = TitleSelectManager.TitleSelect.SinglePlay;

    [Tooltip("タイトル側が存在しない場合（単体再生時）に読み込むシーン")]
    [SerializeField] private string titleSceneName = "Temp_Title";

    /// <summary>
    /// タイトルへ戻る処理中かどうか
    /// </summary>
    public bool IsReturning { get; private set; }

    GameObject activeCanvas;

    void Awake()
    {
        ShowCanvas(TitleSelectionContext.HasSelection ? TitleSelectionContext.Selected : fallbackSelect);
    }

    void Update()
    {
        if (!IsReturning && ReadBackInput() && !HandleBackInActiveCanvas())
            ReturnToTitle();
    }

    bool HandleBackInActiveCanvas()
    {
        if (activeCanvas == null)
            return false;

        foreach (IBackInputHandler handler in activeCanvas.GetComponentsInChildren<IBackInputHandler>())
        {
            if (handler.HandleBack())
                return true;
        }

        return false;
    }

    /// <summary>
    /// 画面をフェードアウトしてタイトルへ戻る
    /// </summary>
    public void ReturnToTitle()
    {
        if (IsReturning)
            return;

        IsReturning = true;
        if (sceneFader == null)
        {
            RequestReturnToTitle();
            return;
        }

        sceneFader.FadeOut(RequestReturnToTitle);
    }

    void ShowCanvas(TitleSelectManager.TitleSelect select)
    {
        int index = (int)select;
        if (selectedCanvas == null || index < 0 || index >= selectedCanvas.Length || selectedCanvas[index] == null)
            Debug.LogWarning($"{nameof(MonitorManager)}: {select} に対応する画面が未登録です。", this);

        if (selectedCanvas == null)
            return;

        activeCanvas = index >= 0 && index < selectedCanvas.Length ? selectedCanvas[index] : null;
        for (int i = 0; i < selectedCanvas.Length; i++)
        {
            if (selectedCanvas[i] != null)
                selectedCanvas[i].SetActive(i == index);
        }
    }

    void RequestReturnToTitle()
    {
        if (SceneTransitionEvents.RequestReturnToTitle())
            return;

        if (!string.IsNullOrEmpty(titleSceneName))
            SceneManager.LoadScene(titleSceneName);
    }

    /// <summary>
    /// 戻る入力（Esc / コントローラーの B / メニューボタン）
    /// </summary>
    static bool ReadBackInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && (gamepad.buttonEast.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
    }
}
