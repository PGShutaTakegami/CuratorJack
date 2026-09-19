using UnityEditor;
using UnityEngine;
using EnvironmentSwitcher;

namespace EnvironmentSwitcher.Editor
{
    /// <summary>EnvironmentSettings 資産の検索・生成（ガワ）。</summary>
    public static class EnvironmentSettingsAssetUtility
    {
        public const string AssetPath = "Assets/Resources/EnvironmentSettings.asset";

        public static EnvironmentSettings LoadOrCreate()
        {
            EnvironmentSettings settings = AssetDatabase.LoadAssetAtPath<EnvironmentSettings>(AssetPath);
            if (settings != null)
            {
                return settings;
            }

            string directory = System.IO.Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            settings = ScriptableObject.CreateInstance<EnvironmentSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static EnvironmentSettings Load()
        {
            return AssetDatabase.LoadAssetAtPath<EnvironmentSettings>(AssetPath);
        }
    }
}
