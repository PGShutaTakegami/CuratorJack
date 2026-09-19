using UnityEditor;
using UnityEngine;
using EnvironmentSwitcher;

namespace EnvironmentSwitcher.Editor
{
    /// <summary>
    /// 環境切替と設定編集用ウィンドウ（Inspector 相当の表示）。
    /// Apply / Define の本実装は後続。ガワとしてウィンドウと Inspector 編集が動く。
    /// </summary>
    public sealed class EnvironmentSwitcherWindow : EditorWindow
    {
        private const string WindowTitle = "Environment Switcher";

        private EnvironmentSettings _settings;
        private Vector2 _scroll;
        private GameEnvironment _selectedEnvironment = GameEnvironment.Development;
        private bool _draftNetworkEnabled = true;
        private string _statusMessage = string.Empty;
        private MessageType _statusType = MessageType.Info;

        [MenuItem("Window/Environment Switcher")]
        [MenuItem("Tools/Environment Switcher")]
        public static void Open()
        {
            EnvironmentSwitcherWindow window = GetWindow<EnvironmentSwitcherWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(420f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureSettings();
            SyncSelectionFromSettings();
        }

        private void OnDisable()
        {
        }

        private void OnFocus()
        {
            EnsureSettings();
            SyncSelectionFromSettings();
            Repaint();
        }

        private void OnGUI()
        {
            EnsureSettings();
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("EnvironmentSettings を作成できませんでした。", MessageType.Error);
                if (GUILayout.Button("再試行"))
                {
                    EnsureSettings();
                }

                return;
            }

            DrawToolbar();
            EditorGUILayout.Space(6f);
            DrawSwitchSection();
            EditorGUILayout.Space(8f);

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
                EditorGUILayout.Space(4f);
            }

            DrawDevSettingsSection();
            EditorGUILayout.Space(8f);
            DrawInspectorLikeSettings();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Environment", EditorStyles.boldLabel, GUILayout.Width(90f));

                GameEnvironment detected = EnvironmentDefineApplier.DetectEnvironmentFromDefines(_settings)
                                           ?? _settings.ActiveEnvironment;
                GUILayout.Label($"Active: {detected}", EditorStyles.toolbarButton);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Ping Asset", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    EditorGUIUtility.PingObject(_settings);
                    Selection.activeObject = _settings;
                }

                if (GUILayout.Button("Select Asset", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    Selection.activeObject = _settings;
                }
            }
        }

        private void DrawSwitchSection()
        {
            EditorGUILayout.LabelField("環境切り替え", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                _selectedEnvironment = (GameEnvironment)EditorGUILayout.EnumPopup(
                    "Target Environment",
                    _selectedEnvironment);

                EnvironmentEntry preview = _settings.FindEntry(_selectedEnvironment);
                if (preview != null)
                {
                    EditorGUILayout.LabelField("Display Name", preview.displayName);
                    EditorGUILayout.LabelField("Define Symbol", preview.defineSymbol);
                    EditorGUILayout.LabelField("API Base URL", preview.apiBaseUrl);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "選択中の環境エントリが Settings にありません。下の設定で追加してください。",
                        MessageType.Warning);
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("共通機能", EditorStyles.boldLabel);
                _draftNetworkEnabled = EditorGUILayout.ToggleLeft(
                    new GUIContent(
                        "ネット通信",
                        "ON で通信可 / OFF で通信スキップ。Apply で確定（全環境共通）"),
                    _draftNetworkEnabled);

                EditorGUILayout.LabelField(
                    "現在の確定値",
                    _settings.EnableNetwork ? "ON" : "OFF");

                EditorGUILayout.Space(4f);

                EditorGUILayout.HelpBox(
                    "Apply すると Scripting Define Symbols（環境 + ENV_NETWORK）が切り替わり、再コンパイルされます。",
                    MessageType.Info);

                using (new EditorGUI.DisabledScope(preview == null))
                {
                    if (GUILayout.Button("Apply Environment", GUILayout.Height(28f)))
                    {
                        ApplySelectedEnvironment();
                    }
                }
            }
        }

        private void DrawDevSettingsSection()
        {
            EditorGUILayout.LabelField("Dev 設定", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Development 環境の DEBUG ボタン／標準機能の表示をここで変更できます。Play 中は次回起動から反映されます。",
                MessageType.Info);

            SerializedObject so = new SerializedObject(_settings);
            so.Update();
            SerializedProperty devDebug = so.FindProperty("devDebug");
            if (devDebug != null)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.PropertyField(devDebug, includeChildren: true);
                }
            }

            if (so.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_settings);
            }
        }

        private void DrawInspectorLikeSettings()
        {
            EditorGUILayout.LabelField("環境エントリ設定", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Environments 配列など、環境ごとの基本設定です。",
                MessageType.None);

            SerializedObject so = new SerializedObject(_settings);
            so.Update();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.PropertyField(so.FindProperty("activeEnvironment"));
            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(so.FindProperty("environments"), true);
            EditorGUILayout.EndScrollView();

            if (so.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_settings);
            }
        }

        private void ApplySelectedEnvironment()
        {
            _settings.SetEnableNetwork(_draftNetworkEnabled);
            EditorUtility.SetDirty(_settings);

            if (EnvironmentDefineApplier.TryApply(_settings, _selectedEnvironment, out string message))
            {
                string networkLabel = _settings.EnableNetwork ? "ネット通信 ON" : "ネット通信 OFF";
                _statusMessage = $"{message}\n{networkLabel}";
                _statusType = MessageType.Info;
                SyncSelectionFromSettings();
            }
            else
            {
                _statusMessage = message;
                _statusType = MessageType.Error;
            }
        }

        private void EnsureSettings()
        {
            if (_settings == null)
            {
                _settings = EnvironmentSettingsAssetUtility.LoadOrCreate();
            }

            EnvironmentRuntime.BindSettings(_settings);
        }

        private void SyncSelectionFromSettings()
        {
            if (_settings == null)
            {
                return;
            }

            GameEnvironment? fromDefines = EnvironmentDefineApplier.DetectEnvironmentFromDefines(_settings);
            _selectedEnvironment = fromDefines ?? _settings.ActiveEnvironment;
            _draftNetworkEnabled = _settings.EnableNetwork;
        }
    }
}
