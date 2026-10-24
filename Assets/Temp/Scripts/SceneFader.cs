using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 画面を黒くフェードアウトしてからシーンを読み込み、読み込み後にフェードインする。
/// 黒くなったあとの片付けと読み込みの間は LoadingScreen を表示し、読み込み後は LoadingScreen のフェードアウトで明ける。
/// 黒幕はシーンをまたいで残すため、実行時に生成して DontDestroyOnLoad にする。
/// </summary>
public class SceneFader : MonoBehaviour
{
    const float FadeOutDuration = 0.5f;
    const float FadeInDuration = 0.5f;
    const int SortingOrder = 32767;
    const float MaxFadeStep = 1f / 30f;
    const float SettledFrameTime = 1f / 20f;
    const float MaxSettleWait = 1f;

    static SceneFader current;

    CanvasGroup canvasGroup;
    IDisposable loadingRequest;

    public static bool IsFading => current != null;

    /// <summary>
    /// フェードアウト → シーン読み込み → フェードインの順に遷移する。フェード中の呼び出しは無視する。
    /// </summary>
    public static void LoadScene(string sceneName)
    {
        LoadScene(sceneName, null);
    }

    /// <summary>
    /// フェードアウトで画面が隠れてから beforeLoad（接続の片付けなど）の完了を待ち、シーンを読み込む。
    /// </summary>
    public static void LoadScene(string sceneName, Func<Task> beforeLoad)
    {
        if (IsFading)
            return;

        current = Create();
        current.StartCoroutine(current.Run(sceneName, beforeLoad));
    }

    static SceneFader Create()
    {
        var root = new GameObject(nameof(SceneFader));
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        root.AddComponent<GraphicRaycaster>();

        var fader = root.AddComponent<SceneFader>();
        fader.canvasGroup = root.AddComponent<CanvasGroup>();
        fader.canvasGroup.alpha = 0f;
        fader.canvasGroup.blocksRaycasts = true;

        var curtain = new GameObject("Curtain", typeof(RectTransform), typeof(Image));
        curtain.transform.SetParent(root.transform, false);
        var rect = curtain.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        curtain.GetComponent<Image>().color = Color.black;

        return fader;
    }

    IEnumerator Run(string sceneName, Func<Task> beforeLoad)
    {
        yield return Fade(0f, 1f, FadeOutDuration);

        bool coveredByLoading = LoadingScreen.IsReady;
        loadingRequest = LoadingScreen.Request();
        if (coveredByLoading)
            canvasGroup.alpha = 0f;

        if (beforeLoad != null)
        {
            Task task = beforeLoad();
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
                Debug.LogException(task.Exception);
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (operation != null && !operation.isDone)
            yield return null;

        yield return WaitUntilFramesSettle();
        ReleaseLoading();

        if (coveredByLoading)
            yield return new WaitForSecondsRealtime(FadeInDuration);
        else
            yield return Fade(1f, 0f, FadeInDuration);

        current = null;
        Destroy(gameObject);
    }

    void ReleaseLoading()
    {
        loadingRequest?.Dispose();
        loadingRequest = null;
    }

    /// <summary>読み込み直後は初期化でフレームが重く、そのまま明けるとフェードインが一瞬で終わるため、落ち着くまで待つ。</summary>
    IEnumerator WaitUntilFramesSettle()
    {
        float deadline = Time.realtimeSinceStartup + MaxSettleWait;
        do
        {
            yield return null;
        }
        while (Time.unscaledDeltaTime > SettledFrameTime && Time.realtimeSinceStartup < deadline);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxFadeStep);
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    void OnDestroy()
    {
        ReleaseLoading();
        if (current == this)
            current = null;
    }
}
