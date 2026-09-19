using UnityEditor;
using UnityEngine;
using EnvironmentSwitcher;

namespace EnvironmentSwitcher.Editor
{
    /// <summary>EnvironmentSettings の Inspector 表示。</summary>
    [CustomEditor(typeof(EnvironmentSettings))]
    public sealed class EnvironmentSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _activeEnvironment;
        private SerializedProperty _environments;
        private SerializedProperty _devDebug;
        private SerializedProperty _enableNetwork;

        private void OnEnable()
        {
            _activeEnvironment = serializedObject.FindProperty("activeEnvironment");
            _environments = serializedObject.FindProperty("environments");
            _devDebug = serializedObject.FindProperty("devDebug");
            _enableNetwork = serializedObject.FindProperty("enableNetwork");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_activeEnvironment);
            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(_environments, true);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("共通機能", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "ネット通信の ON/OFF は Environment Switcher で変更し、Apply Environment でのみ確定します。",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle(
                    "ネット通信（Apply 済み）",
                    _enableNetwork != null && _enableNetwork.boolValue);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Dev 設定", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Development 時の左下 DEBUG オーバーレイ。Environment Switcher ウィンドウからも編集できます。",
                MessageType.Info);
            EditorGUILayout.PropertyField(_devDebug, true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
