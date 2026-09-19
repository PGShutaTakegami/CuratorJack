using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EnvironmentSwitcher
{
    /// <summary>
    /// Development 環境専用の左下 Debug ボタン。
    /// 標準機能: ゲーム終了 / シーン変更。
    /// </summary>
    public sealed class DevDebugOverlay : MonoBehaviour
    {
        private const string BootstrapObjectName = "EnvironmentSwitcher_DevDebugOverlay";

        private string buttonLabel = "DEBUG";
        private Vector2 anchoredPosition = new Vector2(16f, 16f);
        private Vector2 buttonSize = new Vector2(120f, 48f);
        private Vector2 panelSize = new Vector2(360f, 420f);
        private bool _enableSceneChange = true;
        private bool _enableQuitGame = true;
        private bool _enableSaveClear = true;
        private bool _enableLogFile = true;
        private float _scrollSensitivity = 40f;

        private GameObject _uiRoot;
        private GameObject _panelRoot;
        private Transform _panelContent;
        private Dropdown _sceneDropdown;
        private readonly List<string> _sceneNames = new List<string>();
        private bool _panelVisible;
        private Font _uiFont;
        private bool _rebuildingContent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!EnvironmentRuntime.Is(GameEnvironment.Development))
            {
                return;
            }

            EnvironmentSettings settings = EnvironmentRuntime.Settings;
            if (settings != null && !settings.DevDebug.enableOverlay)
            {
                return;
            }

            if (Object.FindFirstObjectByType<DevDebugOverlay>() != null)
            {
                return;
            }

            GameObject host = new GameObject(BootstrapObjectName);
            Object.DontDestroyOnLoad(host);
            host.AddComponent<DevDebugOverlay>();
        }

        private void OnEnable()
        {
            DevDebugRegistry.SectionsChanged += HandleSectionsChanged;
        }

        private void OnDisable()
        {
            DevDebugRegistry.SectionsChanged -= HandleSectionsChanged;
        }

        private void Start()
        {
            if (!EnvironmentRuntime.Is(GameEnvironment.Development))
            {
                Destroy(gameObject);
                return;
            }

            ApplySettingsFromAsset();
            if (EnvironmentRuntime.Settings != null && !EnvironmentRuntime.Settings.DevDebug.enableOverlay)
            {
                Destroy(gameObject);
                return;
            }

            EnsureEventSystem();
            BuildUi();
            SetPanelVisible(false);
        }

        private void HandleSectionsChanged()
        {
            if (_rebuildingContent || _panelContent == null)
            {
                return;
            }

            _rebuildingContent = true;
            try
            {
                bool wasVisible = _panelVisible;
                RebuildPanelContent();
                SetPanelVisible(wasVisible);
            }
            finally
            {
                _rebuildingContent = false;
            }
        }

        private void ApplySettingsFromAsset()
        {
            EnvironmentSettings settings = EnvironmentRuntime.Settings;
            if (settings == null)
            {
                return;
            }

            DevDebugSettings dev = settings.DevDebug;
            buttonLabel = string.IsNullOrEmpty(dev.buttonLabel) ? "DEBUG" : dev.buttonLabel;
            anchoredPosition = dev.buttonPosition;
            buttonSize = dev.buttonSize;
            panelSize = dev.panelSize;
            _enableSceneChange = dev.enableSceneChange;
            _enableQuitGame = dev.enableQuitGame;
            _enableSaveClear = dev.enableSaveClear;
            _enableLogFile = dev.enableLogFile;
            _scrollSensitivity = dev.scrollSensitivity;
        }

        private void OnDestroy()
        {
            if (_uiRoot != null)
            {
                Destroy(_uiRoot);
                _uiRoot = null;
            }
        }

        private void BuildUi()
        {
            _uiFont = ResolveUiFont();

            _uiRoot = new GameObject(
                "DevDebugCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            _uiRoot.transform.SetParent(transform, false);

            Canvas canvas = _uiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            CanvasScaler scaler = _uiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CreateLauncherButton(_uiRoot.transform);
            CreateSettingsPanel(_uiRoot.transform);
        }

        private void CreateLauncherButton(Transform parent)
        {
            Button button = CreateButton(
                parent,
                "DevDebugButton",
                buttonLabel,
                buttonSize,
                new Color(0.15f, 0.55f, 0.25f, 0.92f));

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = anchoredPosition;

            button.onClick.AddListener(TogglePanel);
        }

        private void CreateSettingsPanel(Transform parent)
        {
            _panelRoot = new GameObject(
                "DevDebugPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            _panelRoot.transform.SetParent(parent, false);

            RectTransform panelRect = _panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = anchoredPosition + new Vector2(0f, buttonSize.y + 12f);
            panelRect.sizeDelta = panelSize;

            Image panelImage = _panelRoot.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.1f, 0.12f, 0.94f);

            // ScrollView（ホイール + 右端バー）
            GameObject scrollGo = new GameObject(
                "ScrollView",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(ScrollRect));
            scrollGo.transform.SetParent(_panelRoot.transform, false);

            RectTransform scrollRectTransform = scrollGo.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(8f, 8f);
            scrollRectTransform.offsetMax = new Vector2(-8f, -8f);

            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.01f);
            scrollBg.raycastTarget = true;

            ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = _scrollSensitivity;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;

            const float scrollbarWidth = 14f;

            // Viewport
            GameObject viewport = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);

            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = new Vector2(-scrollbarWidth - 4f, 0f);

            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImage.raycastTarget = true;

            // Content
            GameObject content = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);

            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(8, 8, 8, 8);
            contentLayout.spacing = 10f;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 右端スクロールバー（Web の位置バー相当）
            Scrollbar scrollbar = CreateVerticalScrollbar(scrollGo.transform, scrollbarWidth);

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scrollRect.verticalScrollbarSpacing = 4f;

            _panelContent = content.transform;
            FillPanelContent();
        }

        private void RebuildPanelContent()
        {
            if (_panelContent == null)
            {
                return;
            }

            for (int i = _panelContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_panelContent.GetChild(i).gameObject);
            }

            _sceneDropdown = null;
            FillPanelContent();
        }

        private void FillPanelContent()
        {
            Transform contentParent = _panelContent;

            CreateLabel(contentParent, "Debug Menu", 24, FontStyle.Bold);
            CreateLabel(contentParent, "標準機能", 18, FontStyle.Bold);

            if (_enableSceneChange)
            {
                CreateSectionHeader(contentParent, "シーン変更");
                CreateSceneChangeControls(contentParent);
            }

            if (_enableQuitGame || _enableSaveClear || _enableLogFile)
            {
                CreateSectionHeader(contentParent, "ゲーム / データ");
                CreateGameControlButtons(contentParent);
            }

            DevDebugRegistry.BuildAll(new DevDebugMenuContext(contentParent, _uiFont));

            Button closeButton = CreateButton(
                contentParent,
                "CloseButton",
                "閉じる",
                new Vector2(0f, 40f),
                new Color(0.35f, 0.35f, 0.38f, 1f));
            closeButton.onClick.AddListener(() => SetPanelVisible(false));
            LayoutElement closeLayout = closeButton.gameObject.AddComponent<LayoutElement>();
            closeLayout.minHeight = 40f;
            closeLayout.preferredHeight = 40f;
        }

        private Scrollbar CreateVerticalScrollbar(Transform parent, float width)
        {
            GameObject scrollbarGo = new GameObject(
                "Scrollbar Vertical",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Scrollbar));
            scrollbarGo.transform.SetParent(parent, false);

            RectTransform barRect = scrollbarGo.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(width, 0f);

            Image trackImage = scrollbarGo.GetComponent<Image>();
            trackImage.color = new Color(0.16f, 0.18f, 0.22f, 0.95f);

            GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
            slidingArea.transform.SetParent(scrollbarGo.transform, false);
            RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.offsetMin = new Vector2(2f, 2f);
            slidingRect.offsetMax = new Vector2(-2f, -2f);

            GameObject handle = new GameObject(
                "Handle",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            handle.transform.SetParent(slidingArea.transform, false);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;

            Image handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color(0.55f, 0.58f, 0.64f, 1f);

            Scrollbar scrollbar = scrollbarGo.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handleRect;
            scrollbar.size = 0.3f;
            scrollbar.numberOfSteps = 0;
            return scrollbar;
        }

        private void CreateSceneChangeControls(Transform parent)
        {
            RebuildSceneNameList();

            GameObject dropdownGo = new GameObject(
                "SceneDropdown",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Dropdown));
            dropdownGo.transform.SetParent(parent, false);

            Image dropdownImage = dropdownGo.GetComponent<Image>();
            dropdownImage.color = new Color(0.18f, 0.2f, 0.24f, 1f);

            LayoutElement dropdownLayout = dropdownGo.AddComponent<LayoutElement>();
            dropdownLayout.minHeight = 40f;
            dropdownLayout.preferredHeight = 40f;

            _sceneDropdown = dropdownGo.GetComponent<Dropdown>();
            _sceneDropdown.targetGraphic = dropdownImage;
            _sceneDropdown.captionText = CreateDropdownLabel(dropdownGo.transform, "Caption");
            _sceneDropdown.itemText = null;

            // Template（最低限）
            GameObject template = CreateDropdownTemplate(dropdownGo.transform);
            _sceneDropdown.template = template.GetComponent<RectTransform>();
            _sceneDropdown.itemText = template.GetComponentInChildren<Text>();
            template.SetActive(false);

            _sceneDropdown.ClearOptions();
            _sceneDropdown.AddOptions(_sceneNames);

            int activeIndex = Mathf.Max(0, _sceneNames.IndexOf(SceneManager.GetActiveScene().name));
            if (_sceneNames.Count > 0)
            {
                _sceneDropdown.value = Mathf.Clamp(activeIndex, 0, _sceneNames.Count - 1);
                _sceneDropdown.RefreshShownValue();
            }

            Button loadButton = CreateButton(
                parent,
                "LoadSceneButton",
                "シーンを読み込む",
                new Vector2(0f, 44f),
                new Color(0.2f, 0.45f, 0.75f, 1f));
            LayoutElement loadLayout = loadButton.gameObject.AddComponent<LayoutElement>();
            loadLayout.minHeight = 44f;
            loadLayout.preferredHeight = 44f;
            loadButton.onClick.AddListener(LoadSelectedScene);
        }

        private void CreateGameControlButtons(Transform parent)
        {
            if (_enableSaveClear)
            {
                Button clearEnv = CreateButton(
                    parent,
                    "ClearEnvSave",
                    "この環境のセーブを初期化",
                    new Vector2(0f, 44f),
                    new Color(0.55f, 0.35f, 0.15f, 1f));
                LayoutElement clearEnvLayout = clearEnv.gameObject.AddComponent<LayoutElement>();
                clearEnvLayout.minHeight = 44f;
                clearEnvLayout.preferredHeight = 44f;
                clearEnv.onClick.AddListener(() =>
                {
                    EnvironmentSave.ClearCurrentEnvironmentPrefs();
                });

                Button clearAll = CreateButton(
                    parent,
                    "ClearAllPrefs",
                    "PlayerPrefs 全削除",
                    new Vector2(0f, 44f),
                    new Color(0.7f, 0.25f, 0.1f, 1f));
                LayoutElement clearAllLayout = clearAll.gameObject.AddComponent<LayoutElement>();
                clearAllLayout.minHeight = 44f;
                clearAllLayout.preferredHeight = 44f;
                clearAll.onClick.AddListener(EnvironmentSave.ClearAllPlayerPrefs);
            }

            if (_enableLogFile)
            {
                Button openLog = CreateButton(
                    parent,
                    "OpenLogFolder",
                    "ログフォルダを開く",
                    new Vector2(0f, 44f),
                    new Color(0.25f, 0.4f, 0.65f, 1f));
                LayoutElement openLogLayout = openLog.gameObject.AddComponent<LayoutElement>();
                openLogLayout.minHeight = 44f;
                openLogLayout.preferredHeight = 44f;
                openLog.onClick.AddListener(EnvironmentLogFile.OpenLogFolder);
            }

            if (_enableQuitGame)
            {
                Button quit = CreateButton(
                    parent,
                    "QuitGame",
                    "ゲーム終了",
                    new Vector2(0f, 44f),
                    new Color(0.65f, 0.2f, 0.2f, 1f));
                LayoutElement quitLayout = quit.gameObject.AddComponent<LayoutElement>();
                quitLayout.minHeight = 44f;
                quitLayout.preferredHeight = 44f;
                quit.onClick.AddListener(QuitGame);
            }
        }

        private void RebuildSceneNameList()
        {
            _sceneNames.Clear();
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(name))
                {
                    _sceneNames.Add(name);
                }
            }

            if (_sceneNames.Count == 0)
            {
                _sceneNames.Add(SceneManager.GetActiveScene().name);
            }
        }

        private void LoadSelectedScene()
        {
            if (_sceneDropdown == null || _sceneNames.Count == 0)
            {
                Debug.LogWarning("[DEBUG] 読込可能なシーンがありません。", this);
                return;
            }

            int index = _sceneDropdown.value;
            if (index < 0 || index >= _sceneNames.Count)
            {
                return;
            }

            string sceneName = _sceneNames[index];
            SetPanelVisible(false);
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        private void TogglePanel()
        {
            if (!_panelVisible)
            {
                RebuildSceneNameList();
                if (_sceneDropdown != null)
                {
                    int previous = _sceneDropdown.value;
                    _sceneDropdown.ClearOptions();
                    _sceneDropdown.AddOptions(_sceneNames);
                    _sceneDropdown.value = Mathf.Clamp(previous, 0, Mathf.Max(0, _sceneNames.Count - 1));
                    _sceneDropdown.RefreshShownValue();
                }
            }

            SetPanelVisible(!_panelVisible);
        }

        private void SetPanelVisible(bool visible)
        {
            _panelVisible = visible;
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(visible);
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void CreateSectionHeader(Transform parent, string text)
        {
            CreateLabel(parent, text, 18, FontStyle.Bold);
        }

        private void CreateLabel(Transform parent, string text, int fontSize, FontStyle style)
        {
            GameObject labelGo = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(parent, false);

            Text label = labelGo.GetComponent<Text>();
            label.text = text;
            label.font = _uiFont;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;

            LayoutElement layout = labelGo.AddComponent<LayoutElement>();
            layout.minHeight = fontSize + 10;
            layout.preferredHeight = fontSize + 10;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 size, Color color)
        {
            GameObject buttonGo = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonGo.transform.SetParent(parent, false);

            RectTransform rect = buttonGo.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            Image image = buttonGo.GetComponent<Image>();
            image.color = color;

            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(buttonGo.transform, false);

            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text text = labelGo.GetComponent<Text>();
            text.text = label;
            text.font = _uiFont;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            return button;
        }

        private Text CreateDropdownLabel(Transform parent, string name)
        {
            GameObject labelGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(parent, false);

            RectTransform rect = labelGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 4f);
            rect.offsetMax = new Vector2(-10f, -4f);

            Text text = labelGo.GetComponent<Text>();
            text.font = _uiFont;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private GameObject CreateDropdownTemplate(Transform parent)
        {
            GameObject template = new GameObject(
                "Template",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(ScrollRect));
            template.transform.SetParent(parent, false);

            RectTransform templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, 160f);

            Image templateImage = template.GetComponent<Image>();
            templateImage.color = new Color(0.12f, 0.14f, 0.18f, 1f);

            GameObject viewport = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 28f);

            ScrollRect scroll = template.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;

            GameObject item = new GameObject(
                "Item",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 28f);

            GameObject itemBg = new GameObject(
                "Item Background",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            itemBg.transform.SetParent(item.transform, false);
            RectTransform itemBgRect = itemBg.GetComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.offsetMin = Vector2.zero;
            itemBgRect.offsetMax = Vector2.zero;
            itemBg.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.26f, 1f);

            GameObject itemLabel = new GameObject(
                "Item Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            itemLabel.transform.SetParent(item.transform, false);
            RectTransform itemLabelRect = itemLabel.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(10f, 1f);
            itemLabelRect.offsetMax = new Vector2(-10f, -1f);

            Text itemText = itemLabel.GetComponent<Text>();
            itemText.font = _uiFont;
            itemText.fontSize = 16;
            itemText.color = Color.white;
            itemText.alignment = TextAnchor.MiddleLeft;

            Toggle toggle = item.GetComponent<Toggle>();
            toggle.targetGraphic = itemBg.GetComponent<Image>();

            return template;
        }

        private static Font ResolveUiFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));

            // Input System があればそれを使い、なければ Standalone
            System.Type inputSystemModule = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                eventSystem.AddComponent(inputSystemModule);
            }
            else
            {
                eventSystem.AddComponent<StandaloneInputModule>();
            }
        }
    }
}
