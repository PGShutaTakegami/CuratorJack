using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using EnvironmentSwitcher;

namespace EnvironmentSwitcher.Editor
{
    /// <summary>Define Symbols の適用（Unity 6 NamedBuildTarget）。</summary>
    public static class EnvironmentDefineApplier
    {
        public const string NetworkDefineSymbol = "ENV_NETWORK";

        private static readonly NamedBuildTarget[] TargetBuildTargets =
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS,
            NamedBuildTarget.WebGL
        };

        public static bool TryApply(
            EnvironmentSettings settings,
            GameEnvironment environment,
            out string message)
        {
            if (settings == null)
            {
                message = "EnvironmentSettings がありません。";
                return false;
            }

            EnvironmentEntry entry = settings.FindEntry(environment);
            if (entry == null)
            {
                message = $"{environment} の設定エントリがありません。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.defineSymbol))
            {
                message = $"{environment} の defineSymbol が空です。";
                return false;
            }

            HashSet<string> managed = new HashSet<string>(
                settings.GetManagedDefineSymbols(),
                StringComparer.Ordinal);
            managed.Add(NetworkDefineSymbol);

            string activeSymbol = entry.defineSymbol.Trim();
            int updatedTargets = 0;

            for (int i = 0; i < TargetBuildTargets.Length; i++)
            {
                NamedBuildTarget target = TargetBuildTargets[i];
                if (!TryGetDefines(target, out string raw))
                {
                    continue;
                }

                List<string> defines = SplitDefines(raw)
                    .Where(d => !managed.Contains(d))
                    .ToList();

                if (!defines.Contains(activeSymbol))
                {
                    defines.Add(activeSymbol);
                }

                if (settings.EnableNetwork && !defines.Contains(NetworkDefineSymbol))
                {
                    defines.Add(NetworkDefineSymbol);
                }

                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
                updatedTargets++;
            }

            settings.SetActiveEnvironment(environment);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            EnvironmentRuntime.BindSettings(settings);

            string networkLabel = settings.EnableNetwork
                ? $"{NetworkDefineSymbol} ON"
                : $"{NetworkDefineSymbol} OFF";
            message =
                $"{entry.displayName} に切替（シンボル: {activeSymbol}, {networkLabel} / {updatedTargets} ターゲット）。再コンパイルされます。";
            return true;
        }

        public static GameEnvironment? DetectEnvironmentFromDefines(EnvironmentSettings settings)
        {
            if (settings == null)
            {
                return null;
            }

            if (!TryGetDefines(NamedBuildTarget.Standalone, out string raw) &&
                !TryGetDefines(
                    NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup),
                    out raw))
            {
                return null;
            }

            HashSet<string> defines = new HashSet<string>(SplitDefines(raw), StringComparer.Ordinal);
            IReadOnlyList<EnvironmentEntry> entries = settings.Environments;
            for (int i = 0; i < entries.Count; i++)
            {
                EnvironmentEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.defineSymbol))
                {
                    continue;
                }

                if (defines.Contains(entry.defineSymbol.Trim()))
                {
                    return entry.environment;
                }
            }

            return null;
        }

        private static bool TryGetDefines(NamedBuildTarget target, out string defines)
        {
            try
            {
                defines = PlayerSettings.GetScriptingDefineSymbols(target);
                return true;
            }
            catch (ArgumentException)
            {
                defines = string.Empty;
                return false;
            }
        }

        private static IEnumerable<string> SplitDefines(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                yield break;
            }

            string[] parts = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string trimmed = parts[i].Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    yield return trimmed;
                }
            }
        }
    }
}
