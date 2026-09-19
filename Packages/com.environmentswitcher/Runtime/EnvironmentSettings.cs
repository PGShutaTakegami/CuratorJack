using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentSwitcher
{
    /// <summary>
    /// 環境設定 ScriptableObject。Inspector / Switcher ウィンドウから編集する。
    /// </summary>
    [CreateAssetMenu(
        fileName = "EnvironmentSettings",
        menuName = "Environment Switcher/Environment Settings")]
    public class EnvironmentSettings : ScriptableObject
    {
        [SerializeField] private GameEnvironment activeEnvironment = GameEnvironment.Development;
        [SerializeField] private List<EnvironmentEntry> environments = new List<EnvironmentEntry>
        {
            new EnvironmentEntry
            {
                environment = GameEnvironment.Development,
                displayName = "Development",
                defineSymbol = "ENV_DEV",
                apiBaseUrl = "https://dev.example.local",
                enableDebugLog = true,
                enableCrashReporting = false,
                useAnalyticsSandbox = true,
                analyticsAppId = "analytics-dev",
                useIapSandbox = true,
                isolateSaveData = true
            },
            new EnvironmentEntry
            {
                environment = GameEnvironment.Staging,
                displayName = "Staging",
                defineSymbol = "ENV_STG",
                apiBaseUrl = "https://stg.example.local",
                enableDebugLog = true,
                enableCrashReporting = true,
                useAnalyticsSandbox = true,
                analyticsAppId = "analytics-stg",
                useIapSandbox = true,
                isolateSaveData = true
            },
            new EnvironmentEntry
            {
                environment = GameEnvironment.Release,
                displayName = "Release",
                defineSymbol = "ENV_RELEASE",
                apiBaseUrl = "https://api.example.com",
                enableDebugLog = false,
                enableCrashReporting = true,
                useAnalyticsSandbox = false,
                analyticsAppId = "analytics-prod",
                useIapSandbox = false,
                isolateSaveData = true
            }
        };

        [SerializeField] private DevDebugSettings devDebug = new DevDebugSettings();

        [Header("Global Features")]
        [Tooltip("ネット通信機能。Environment Switcher で変更し Apply で反映する（全環境共通）")]
        [SerializeField] private bool enableNetwork = true;

        public GameEnvironment ActiveEnvironment => activeEnvironment;

        public IReadOnlyList<EnvironmentEntry> Environments => environments;

        public DevDebugSettings DevDebug => devDebug ?? (devDebug = new DevDebugSettings());

        /// <summary>ネット通信が有効か（Apply 後の確定値）。</summary>
        public bool EnableNetwork => enableNetwork;

        public void SetActiveEnvironment(GameEnvironment environment)
        {
            activeEnvironment = environment;
        }

        public void SetEnableNetwork(bool enabled)
        {
            enableNetwork = enabled;
        }

        public EnvironmentEntry FindEntry(GameEnvironment environment)
        {
            for (int i = 0; i < environments.Count; i++)
            {
                EnvironmentEntry entry = environments[i];
                if (entry != null && entry.environment == environment)
                {
                    return entry;
                }
            }

            return null;
        }

        public EnvironmentEntry GetActiveEntry()
        {
            return FindEntry(activeEnvironment);
        }

        public IEnumerable<string> GetManagedDefineSymbols()
        {
            for (int i = 0; i < environments.Count; i++)
            {
                EnvironmentEntry entry = environments[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.defineSymbol))
                {
                    continue;
                }

                yield return entry.defineSymbol.Trim();
            }
        }
    }
}
