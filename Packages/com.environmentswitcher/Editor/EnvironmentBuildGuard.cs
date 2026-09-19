using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using EnvironmentSwitcher;

namespace EnvironmentSwitcher.Editor
{
    /// <summary>
    /// Release ビルド時に Dev シンボルや不整合が残っていないか検査する。
    /// </summary>
    public sealed class EnvironmentBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            EnvironmentSettings settings = EnvironmentSettingsAssetUtility.Load();
            if (settings == null)
            {
                return;
            }

            List<string> problems = new List<string>();

            GameEnvironment? defined = EnvironmentDefineApplier.DetectEnvironmentFromDefines(settings);
            string defines = PlayerSettings.GetScriptingDefineSymbols(
                NamedBuildTarget.FromBuildTargetGroup(report.summary.platformGroup));

            EnvironmentEntry dev = settings.FindEntry(GameEnvironment.Development);
            EnvironmentEntry release = settings.FindEntry(GameEnvironment.Release);

            bool isReleaseBuild = settings.ActiveEnvironment == GameEnvironment.Release
                                  || defined == GameEnvironment.Release;

            if (!isReleaseBuild)
            {
                return;
            }

            if (settings.ActiveEnvironment != GameEnvironment.Release && defined == GameEnvironment.Release)
            {
                problems.Add(
                    $"Define は Release ですが ActiveEnvironment が {settings.ActiveEnvironment} です。Apply で揃えてください。");
            }

            if (defined.HasValue &&
                settings.ActiveEnvironment == GameEnvironment.Release &&
                defined.Value != GameEnvironment.Release)
            {
                problems.Add(
                    $"ActiveEnvironment は Release ですが Define は {defined.Value} です。Apply で揃えてください。");
            }

            if (dev != null &&
                !string.IsNullOrWhiteSpace(dev.defineSymbol) &&
                defines.Contains(dev.defineSymbol.Trim()))
            {
                problems.Add($"Release ビルドなのに Dev シンボル '{dev.defineSymbol}' が残っています。");
            }

            if (release != null && release.useIapSandbox)
            {
                problems.Add("Release エントリの useIapSandbox が true です。");
            }

            if (release != null && release.useAnalyticsSandbox)
            {
                problems.Add("Release エントリの useAnalyticsSandbox が true です。");
            }

            if (problems.Count == 0)
            {
                return;
            }

            string message = "EnvironmentSwitcher Production ガード:\n- " + string.Join("\n- ", problems);
            throw new BuildFailedException(message);
        }
    }
}
