using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// タイトル選択をスプライン上のオブジェクト移動で切り替える。
/// </summary>
public class TitleSelectManager : MonoBehaviour
{
    /// <summary>
    /// Title選択
    /// </summary>
    public enum TitleSelect
    {
        /// <summary>
        /// シングルプレイ
        /// </summary>
        SinglePlay = 0,

        /// <summary>
        /// マルチプレイ(2~4人プレイ)
        /// </summary>
        MultiPlay = 1,

        /// <summary>
        /// オプション
        /// </summary>
        Option = 2,

        /// <summary>
        /// クレジット
        /// </summary>
        Credit = 3,

        /// <summary>
        /// トロフィー
        /// </summary>
        Trophy = 4,

        /// <summary>
        /// 終了
        /// </summary>
        Quit = 5
    }

    /// <summary>
    /// Title選択項目
    /// </summary>
    [System.Serializable]
    struct SelectOption
    {
        public GameObject targetObject;
        [FormerlySerializedAs("monitorSprite2D")] public Sprite monitorSprite;
    }

    [SerializeField] private SplineContainer splineContainer;

    [SerializeField] private Renderer monitorScreenRenderer;

    [SerializeField] private float slideSpeed = 3f;

    [SerializeField] private SelectOption[] selectOptions;

    [SerializeField] private string humanMonitorSceneName = "HumanMonitorScene";

    [SerializeField] private Sprite monitorDefaultSprite;

    [SerializeField] private MonitorZoomCamera monitorZoomCamera;

    [SerializeField] private CanvasGroupFader titleUiFader;

    TitleSelect currentSelect = TitleSelect.SinglePlay;

    float[] optionHeights;
    float currentOffset;
    float moveStartOffset;
    float moveDistance;
    float movedAmount;
    int currentIndex;
    int targetIndex;
    int moveDirection;
    bool isMoving;
    bool isLoadingHumanMonitor;
    bool isSceneTransitioning;

    const float StickThreshold = 0.5f;
    float previousStickX;

    MonitorScreenFader monitorFader;

    /// <summary>
    /// 現在の選択状態
    /// </summary>
    public TitleSelect CurrentSelect => currentSelect;

    /// <summary>
    /// スプライン上のカルーセルオフセット（0〜1）
    /// </summary>
    public float CurrentSplineOffset => currentOffset;

    /// <summary>
    /// 移動中かどうか
    /// </summary>
    public bool IsMoving => isMoving;

    /// <summary>
    /// モニターとの遷移中・HumanMonitorScene 表示中は Title 選択を停止する
    /// </summary>
    public bool IsTitleSelectStopped => isSceneTransitioning || isLoadingHumanMonitor || IsHumanMonitorSceneLoaded();

    void OnEnable()
    {
        SceneTransitionEvents.ReturnToTitleRequested += BeginReturnFromMonitor;
    }

    void OnDisable()
    {
        SceneTransitionEvents.ReturnToTitleRequested -= BeginReturnFromMonitor;
    }

    void Start()
    {
        if (splineContainer == null)
        {
            Debug.LogError($"{nameof(TitleSelectManager)}: SplineContainer が未設定です。", this);
            enabled = false;
            return;
        }

        if (selectOptions == null || selectOptions.Length == 0)
        {
            Debug.LogError($"{nameof(TitleSelectManager)}: selectOptions が空です。", this);
            enabled = false;
            return;
        }

        CacheHeights();

        currentIndex = 0;
        currentSelect = TitleSelect.SinglePlay;
        currentOffset = 0f;
        targetIndex = currentIndex;

        ApplyPositions(currentOffset);

        if (monitorScreenRenderer != null)
        {
            monitorFader = new MonitorScreenFader(monitorScreenRenderer, monitorDefaultSprite);
            monitorFader.ShowImmediate(ResolveMonitorSprite(currentIndex));
        }
    }

    void Update()
    {

        if (IsTitleSelectStopped)
        {
            // 表示中はスティック閾値だけ同期し、入力・移動は止める
            var gamepad = Gamepad.current;
            previousStickX = gamepad != null ? gamepad.leftStick.ReadValue().x : 0f;
            if (isMoving)
            {
                isMoving = false;
                ApplyPositions(currentOffset);
                monitorFader?.ShowImmediate(ResolveMonitorSprite(currentIndex));
            }
            return;
        }

        HandleInput();
        if (isMoving)
            TickMove(Time.deltaTime);
    }

    bool IsHumanMonitorSceneLoaded()
    {
        if (string.IsNullOrEmpty(humanMonitorSceneName))
            return false;

        var scene = SceneManager.GetSceneByName(humanMonitorSceneName);
        return scene.IsValid() && scene.isLoaded;
    }

    void CacheHeights()
    {
        optionHeights = new float[selectOptions.Length];
        for (int i = 0; i < selectOptions.Length; i++)
        {
            var go = selectOptions[i].targetObject;
            optionHeights[i] = go != null ? go.transform.position.y : 0f;
        }
    }

    void HandleInput()
    {
        float stickX = 0f;
        var gamepad = Gamepad.current;
        if (gamepad != null)
            stickX = gamepad.leftStick.ReadValue().x;

        if (isMoving || isLoadingHumanMonitor)
        {
            previousStickX = stickX;
            return;
        }

        if (ReadConfirmInput())
        {
            previousStickX = stickX;
            BeginMonitorTransition();
            return;
        }

        // 右入力（D / →）: Quit 方向  左入力（A / ←）: Book 方向
        int input = ReadNavigateInput(stickX);
        previousStickX = stickX;

        if (input > 0)
            BeginMove(-1, +1);
        else if (input < 0)
            BeginMove(+1, -1);
    }

    /// <summary>
    /// キーボード・Xbox パッドから左右入力を取得する。右=+1 / 左=-1 / なし=0
    /// </summary>
    int ReadNavigateInput(float stickX)
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                return +1;
            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                return -1;
        }

        var gamepad = Gamepad.current;
        if (gamepad == null)
            return 0;

        // Xbox: 十字キー左右
        if (gamepad.dpad.right.wasPressedThisFrame)
            return +1;
        if (gamepad.dpad.left.wasPressedThisFrame)
            return -1;

        // Xbox: 左スティック左右（閾値をまたいだ瞬間のみ）
        if (previousStickX < StickThreshold && stickX >= StickThreshold)
            return +1;
        if (previousStickX > -StickThreshold && stickX <= -StickThreshold)
            return -1;

        return 0;
    }

    /// <summary>
    /// 確定入力（Space / Enter / 左クリック / Xbox A）
    /// </summary>
    bool ReadConfirmInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                return true;
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
            return true;

        return false;
    }

    /// <summary>
    /// タイトル UI をフェードアウト → カメラをモニター正面へズーム → HumanMonitorScene を読み込む
    /// </summary>
    void BeginMonitorTransition()
    {
        if (IsHumanMonitorSceneLoaded())
            return;

        isSceneTransitioning = true;
        FadeOutTitleUi(() => ZoomToMonitor(() =>
        {
            isSceneTransitioning = false;
            TryLoadHumanMonitorScene();
        }));
    }

    /// <summary>
    /// HumanMonitorScene をアンロード → カメラを元の位置へ戻す → タイトル UI をフェードイン（行きの逆順）。
    /// 選択中の項目はそのまま維持する。
    /// </summary>
    void BeginReturnFromMonitor()
    {
        if (isSceneTransitioning)
            return;

        isSceneTransitioning = true;
        UnloadHumanMonitorScene(() => ReturnCameraFromMonitor(() => FadeInTitleUi(() =>
        {
            isSceneTransitioning = false;
        })));
    }

    void UnloadHumanMonitorScene(Action onCompleted)
    {
        if (!IsHumanMonitorSceneLoaded())
        {
            onCompleted();
            return;
        }

        var operation = SceneManager.UnloadSceneAsync(humanMonitorSceneName);
        if (operation == null)
        {
            onCompleted();
            return;
        }

        operation.completed += _ => onCompleted();
    }

    void ReturnCameraFromMonitor(Action onCompleted)
    {
        if (monitorZoomCamera == null)
        {
            onCompleted();
            return;
        }

        monitorZoomCamera.ReturnToOrigin(onCompleted);
    }

    void FadeInTitleUi(Action onCompleted)
    {
        if (titleUiFader == null)
        {
            onCompleted();
            return;
        }

        titleUiFader.FadeIn(onCompleted);
    }

    void FadeOutTitleUi(Action onCompleted)
    {
        if (titleUiFader == null)
        {
            onCompleted();
            return;
        }

        titleUiFader.FadeOut(onCompleted);
    }

    void ZoomToMonitor(Action onCompleted)
    {
        if (monitorZoomCamera == null || monitorScreenRenderer == null)
        {
            onCompleted();
            return;
        }

        monitorZoomCamera.Play(monitorScreenRenderer, onCompleted);
    }

    void TryLoadHumanMonitorScene()
    {
        if (string.IsNullOrEmpty(humanMonitorSceneName))
        {
            Debug.LogError($"{nameof(TitleSelectManager)}: HumanMonitor シーン名が空です。", this);
            return;
        }

        var loaded = SceneManager.GetSceneByName(humanMonitorSceneName);
        if (loaded.isLoaded)
            return;

        isLoadingHumanMonitor = true;
        TitleSelectionContext.Set(currentSelect);
        var operation = SceneManager.LoadSceneAsync(humanMonitorSceneName, LoadSceneMode.Additive);
        if (operation == null)
        {
            isLoadingHumanMonitor = false;
            Debug.LogError($"{nameof(TitleSelectManager)}: シーン '{humanMonitorSceneName}' のロードに失敗しました。Build Settings を確認してください。", this);
            return;
        }

        operation.completed += _ =>
        {
            isLoadingHumanMonitor = false;
            AlignLoadedSceneCamera(SceneManager.GetSceneByName(humanMonitorSceneName));
        };
    }

    /// <summary>
    /// 読み込んだシーンのカメラを、ズーム後のタイトルカメラと同じ位置・回転・画角にする
    /// </summary>
    void AlignLoadedSceneCamera(Scene scene)
    {
        if (monitorZoomCamera == null || monitorZoomCamera.TargetCamera == null || !scene.IsValid())
            return;

        Camera source = monitorZoomCamera.TargetCamera;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                camera.fieldOfView = source.fieldOfView;
            }
        }
    }

    /// <summary>
    /// selectionStep: 選択 index の変化 / offsetStep: オブジェクト移動方向
    /// </summary>
    void BeginMove(int selectionStep, int offsetStep)
    {
        int count = selectOptions.Length;
        targetIndex = Mod(currentIndex + selectionStep, count);
        moveDirection = offsetStep;
        moveStartOffset = currentOffset;
        moveDistance = 1f / count;
        movedAmount = 0f;
        isMoving = true;
        monitorFader?.BeginTransition(ResolveMonitorSprite(targetIndex));
    }

    void TickMove(float deltaTime)
    {
        float length = Mathf.Max(splineContainer.CalculateLength(), 0.0001f);
        float deltaT = (slideSpeed / length) * deltaTime;
        movedAmount += deltaT;

        if (movedAmount >= moveDistance)
        {
            currentOffset = Normalize01(moveStartOffset + moveDirection * moveDistance);
            currentIndex = targetIndex;
            currentSelect = (TitleSelect)currentIndex;
            isMoving = false;
            ApplyPositions(currentOffset);
            monitorFader?.CompleteTransition();
            return;
        }

        currentOffset = Normalize01(moveStartOffset + moveDirection * movedAmount);
        ApplyPositions(currentOffset);
        monitorFader?.SetProgress(movedAmount / moveDistance);
    }

    void ApplyPositions(float offset)
    {
        int count = selectOptions.Length;
        for (int i = 0; i < count; i++)
        {
            var go = selectOptions[i].targetObject;
            if (go == null)
                continue;

            float t = Normalize01((float)i / count + offset);
            float3 sample = splineContainer.EvaluatePosition(t);
            Vector3 pos = go.transform.position;
            pos.x = sample.x;
            pos.z = sample.z;
            pos.y = optionHeights[i];
            go.transform.position = pos;
        }
    }

    /// <summary>
    /// 項目の画像を返す。未割り当てなら既定画像（それも無ければ null = 黒）
    /// </summary>
    Sprite ResolveMonitorSprite(int index)
    {
        Sprite sprite = selectOptions[index].monitorSprite;
        return sprite != null ? sprite : monitorDefaultSprite;
    }

    static float Normalize01(float value)
    {
        value %= 1f;
        if (value < 0f)
            value += 1f;
        return value;
    }

    static int Mod(int value, int modulo)
    {
        int result = value % modulo;
        return result < 0 ? result + modulo : result;
    }
}
