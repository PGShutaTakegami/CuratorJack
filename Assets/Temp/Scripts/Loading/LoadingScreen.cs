using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// LoadingScene の画面を起動時に読み込んで常駐させ、表示要求があるか、画面へ描画するカメラが無い間は表示する。
/// シーン切り替えの読み込み中や、マルチの接続待ちでエディター背景が見えないようにするため。
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    const string SceneName = "LoadingScene";

    static LoadingScreen instance;
    static int requestCount;

    LoadingScreenView view;
    Camera[] cameraBuffer = new Camera[8];

    /// <summary>画面を表示できる状態か（LoadingScene の読み込みが済んでいるか）。</summary>
    public static bool IsReady => instance != null && instance.view != null;

    /// <summary>Dispose されるまで表示し続ける。</summary>
    public static IDisposable Request()
    {
        requestCount++;
        if (instance != null)
            instance.Refresh();
        return new RequestHandle();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        requestCount = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null)
            return;

        var root = new GameObject(nameof(LoadingScreen));
        DontDestroyOnLoad(root);
        instance = root.AddComponent<LoadingScreen>();
        instance.LoadView();
    }

    void LoadView()
    {
        Scene opened = SceneManager.GetSceneByName(SceneName);
        if (opened.isLoaded)
        {
            UseOpenedView(opened);
            return;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
        if (operation == null)
        {
            Debug.LogWarning($"[LoadingScreen] {SceneName} を読み込めませんでした。ビルド設定に追加されているか確認してください");
            return;
        }

        operation.completed += _ => AdoptView();
    }

    /// <summary>シーンの切り替えで消えないよう、画面を DontDestroyOnLoad へ移してから空になったシーンを閉じる。</summary>
    void AdoptView()
    {
        Scene scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid())
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (view == null)
                view = root.GetComponentInChildren<LoadingScreenView>(true);
            DontDestroyOnLoad(root);
        }

        SceneManager.UnloadSceneAsync(scene);

        if (view == null)
            Debug.LogWarning($"[LoadingScreen] {SceneName} に LoadingScreenView がありません");
        else
            Refresh();
    }

    /// <summary>LoadingScene を直接再生したときは、開いている画面をそのまま使う（最後のシーンは閉じられないため移さない）。</summary>
    void UseOpenedView(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            view = root.GetComponentInChildren<LoadingScreenView>(true);
            if (view != null)
                break;
        }
    }

    void LateUpdate()
    {
        Refresh();
    }

    void Refresh()
    {
        if (view != null)
            view.SetVisible(requestCount > 0 || !HasScreenCamera());
    }

    /// <summary>RenderTexture へ描くカメラだけでは画面は埋まらないため、画面へ描くカメラがあるかを見る。</summary>
    bool HasScreenCamera()
    {
        int count = Camera.allCamerasCount;
        if (cameraBuffer.Length < count)
            cameraBuffer = new Camera[count];

        int found = Camera.GetAllCameras(cameraBuffer);
        for (int i = 0; i < found; i++)
        {
            if (cameraBuffer[i].targetTexture == null)
                return true;
        }
        return false;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    class RequestHandle : IDisposable
    {
        bool released;

        public void Dispose()
        {
            if (released)
                return;

            released = true;
            requestCount--;
        }
    }
}
