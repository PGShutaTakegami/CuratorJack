using UnityEngine;

namespace EnvironmentSwitcher
{
    /// <summary>実行時の環境参照・ポリシー適用。</summary>
    public static class EnvironmentRuntime
    {
        private const string DefaultSettingsResourcePath = "EnvironmentSettings";

        private static EnvironmentSettings _settings;

        public static EnvironmentSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = Resources.Load<EnvironmentSettings>(DefaultSettingsResourcePath);
                    SyncActiveEnvironmentFromDefines();
                }

                return _settings;
            }
        }

        public static void BindSettings(EnvironmentSettings settings)
        {
            _settings = settings;
            SyncActiveEnvironmentFromDefines();
            ApplyAllPolicies();
        }

        /// <summary>
        /// コンパイル済み Define を最優先。無いときだけ Settings.Active を使う。
        /// </summary>
        public static GameEnvironment Current
        {
            get
            {
#if ENV_RELEASE
                return GameEnvironment.Release;
#elif ENV_STG
                return GameEnvironment.Staging;
#elif ENV_LOCAL
                return GameEnvironment.Local;
#elif ENV_LOCALNET
                return GameEnvironment.LocalNet;
#elif ENV_ONLINENET
                return GameEnvironment.OnlineNet;
#elif ENV_DEV
                return GameEnvironment.Development;
#else
                if (Settings != null)
                {
                    return Settings.ActiveEnvironment;
                }

                return GameEnvironment.Development;
#endif
            }
        }

        public static EnvironmentEntry ActiveEntry
        {
            get
            {
                EnvironmentSettings settings = Settings;
                return settings != null ? settings.FindEntry(Current) : null;
            }
        }

        public static bool Is(GameEnvironment environment) => Current == environment;

        /// <summary>Development とその追加版（Local / LocalNet / OnlineNet）か。</summary>
        public static bool IsDevelopmentFamily => Current.IsDevelopmentFamily();

        /// <summary>現在環境のマルチプレイ通信方式。</summary>
        public static EnvironmentNetworkMode NetworkMode => Current.ToNetworkMode();

        public static string ApiBaseUrl
        {
            get
            {
                EnvironmentEntry entry = ActiveEntry;
                return entry != null ? entry.apiBaseUrl : string.Empty;
            }
        }

        public static bool EnableCrashReporting
        {
            get
            {
                EnvironmentEntry entry = ActiveEntry;
                return entry != null && entry.enableCrashReporting;
            }
        }

        public static bool UseAnalyticsSandbox
        {
            get
            {
                EnvironmentEntry entry = ActiveEntry;
                return entry == null || entry.useAnalyticsSandbox;
            }
        }

        public static string AnalyticsAppId
        {
            get
            {
                EnvironmentEntry entry = ActiveEntry;
                return entry != null ? entry.analyticsAppId : string.Empty;
            }
        }

        public static bool UseIapSandbox
        {
            get
            {
                EnvironmentEntry entry = ActiveEntry;
                return entry == null || entry.useIapSandbox;
            }
        }

        /// <summary>ネット通信が有効か（Settings の Apply 済み値）。</summary>
        public static bool NetworkEnabled => EnvironmentNetwork.IsEnabled;

        public static void ApplyAllPolicies()
        {
            ApplyLoggingPolicy();
            ApplyLogFilePolicy();
            EnvironmentProductionGuard.EnforceRuntime();
        }

        /// <summary>
        /// Dev: 全ログ / Stg: Warning 以上 / Release: Error のみ。
        /// いずれの環境でも Error / Exception は出力される。
        /// </summary>
        public static void ApplyLoggingPolicy()
        {
            Debug.unityLogger.logEnabled = true;

            if (IsDevelopmentFamily)
            {
                Debug.unityLogger.filterLogType = LogType.Log;
            }
            else if (Current == GameEnvironment.Staging)
            {
                Debug.unityLogger.filterLogType = LogType.Warning;
            }
            else
            {
                Debug.unityLogger.filterLogType = LogType.Error;
            }
        }

        /// <summary>Define と Settings.Active がズレていれば Define 側に合わせる。</summary>
        private static void SyncActiveEnvironmentFromDefines()
        {
            if (_settings == null)
            {
                return;
            }

            GameEnvironment fromDefines = Current;
#if !ENV_DEV && !ENV_STG && !ENV_RELEASE
            return;
#else
            if (_settings.ActiveEnvironment != fromDefines)
            {
                _settings.SetActiveEnvironment(fromDefines);
            }
#endif
        }

        private static void ApplyLogFilePolicy()
        {
            bool wantFile = IsDevelopmentFamily
                            && Settings != null
                            && Settings.DevDebug.enableLogFile;
            EnvironmentLogFile.SetEnabled(wantFile);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _settings = null;
            EnvironmentLogFile.SetEnabled(false);
            EnvironmentSaveKeyHints.Clear();
            EnvironmentNetwork.ResetStats();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyEarly()
        {
            ApplyAllPolicies();
        }
    }
}
