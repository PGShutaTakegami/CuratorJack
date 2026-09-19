using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace EnvironmentSwitcher
{
    /// <summary>
    /// 環境ごとの PlayerPrefs キー分離とクリア。
    /// Set* で書いたキーは追跡され、環境クリアで削除できる。
    /// </summary>
    public static class EnvironmentSave
    {
        private const string LegacyMarkerKey = "EnvironmentSwitcher.SaveNamespace";
        private const string TrackedKeysSuffix = "__tracked_keys";
        private const char TrackedSeparator = '\n';

        public static string Namespace
        {
            get
            {
                EnvironmentEntry entry = EnvironmentRuntime.ActiveEntry;
                if (entry == null || !entry.isolateSaveData)
                {
                    return "shared";
                }

                return entry.environment.ToString();
            }
        }

        public static string Key(string key)
        {
            return $"{Namespace}.{key}";
        }

        public static void SetString(string key, string value)
        {
            string full = Key(key);
            PlayerPrefs.SetString(full, value);
            TrackLogicalKey(key);
        }

        public static string GetString(string key, string defaultValue = "")
        {
            return PlayerPrefs.GetString(Key(key), defaultValue);
        }

        public static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(Key(key), value);
            TrackLogicalKey(key);
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            return PlayerPrefs.GetInt(Key(key), defaultValue);
        }

        public static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(Key(key), value);
            TrackLogicalKey(key);
        }

        public static float GetFloat(string key, float defaultValue = 0f)
        {
            return PlayerPrefs.GetFloat(Key(key), defaultValue);
        }

        public static bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(Key(key));
        }

        public static void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(Key(key));
            UntrackLogicalKey(key);
        }

        /// <summary>現在環境で EnvironmentSave 経由のキーを削除する。</summary>
        public static int ClearCurrentEnvironmentPrefs()
        {
            HashSet<string> logicalKeys = LoadTrackedLogicalKeys();
            foreach (string hint in EnvironmentSaveKeyHints.GetKeys())
            {
                logicalKeys.Add(hint);
            }

            int removed = 0;
            foreach (string logical in logicalKeys)
            {
                string full = Key(logical);
                if (PlayerPrefs.HasKey(full))
                {
                    PlayerPrefs.DeleteKey(full);
                    removed++;
                }
            }

            PlayerPrefs.DeleteKey(TrackedMetaKey());
            PlayerPrefs.SetString(LegacyMarkerKey, Namespace);
            PlayerPrefs.Save();

            Debug.LogWarning(
                $"[EnvironmentSave] 環境 '{Namespace}' のキーを {removed} 件削除しました。");
            return removed;
        }

        /// <summary>全 PlayerPrefs を消す（危険・Dev 用）。</summary>
        public static void ClearAllPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.LogWarning("[EnvironmentSave] PlayerPrefs.DeleteAll() を実行しました。");
        }

        private static void TrackLogicalKey(string logicalKey)
        {
            if (string.IsNullOrEmpty(logicalKey) || logicalKey == TrackedKeysSuffix)
            {
                return;
            }

            HashSet<string> keys = LoadTrackedLogicalKeys();
            if (!keys.Add(logicalKey))
            {
                return;
            }

            SaveTrackedLogicalKeys(keys);
            EnvironmentSaveKeyHints.Register(logicalKey);
        }

        private static void UntrackLogicalKey(string logicalKey)
        {
            HashSet<string> keys = LoadTrackedLogicalKeys();
            if (!keys.Remove(logicalKey))
            {
                return;
            }

            SaveTrackedLogicalKeys(keys);
        }

        private static string TrackedMetaKey()
        {
            return Key(TrackedKeysSuffix);
        }

        private static HashSet<string> LoadTrackedLogicalKeys()
        {
            var set = new HashSet<string>();
            string raw = PlayerPrefs.GetString(TrackedMetaKey(), string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return set;
            }

            string[] parts = raw.Split(TrackedSeparator);
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]))
                {
                    set.Add(parts[i]);
                }
            }

            return set;
        }

        private static void SaveTrackedLogicalKeys(HashSet<string> keys)
        {
            var sb = new StringBuilder();
            foreach (string key in keys)
            {
                if (sb.Length > 0)
                {
                    sb.Append(TrackedSeparator);
                }

                sb.Append(key);
            }

            PlayerPrefs.SetString(TrackedMetaKey(), sb.ToString());
            PlayerPrefs.Save();
        }
    }

    /// <summary>ClearCurrentEnvironmentPrefs 用の追加ヒントキー登録。</summary>
    public static class EnvironmentSaveKeyHints
    {
        private static readonly List<string> Keys = new List<string>();

        public static void Register(string key)
        {
            if (string.IsNullOrEmpty(key) || Keys.Contains(key))
            {
                return;
            }

            Keys.Add(key);
        }

        public static void Clear()
        {
            Keys.Clear();
        }

        internal static string[] GetKeys()
        {
            return Keys.ToArray();
        }
    }
}
