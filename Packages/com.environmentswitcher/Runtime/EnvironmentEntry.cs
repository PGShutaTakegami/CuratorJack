using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentSwitcher
{
    [Serializable]
    public class EnvironmentKeyValue
    {
        public string key = string.Empty;
        public string value = string.Empty;
    }

    /// <summary>1環境分の設定。</summary>
    [Serializable]
    public class EnvironmentEntry
    {
        public GameEnvironment environment = GameEnvironment.Development;
        public string displayName = "Development";
        public string defineSymbol = "ENV_DEV";
        public string apiBaseUrl = string.Empty;

        [Tooltip("詳細ログの意図フラグ（実フィルタは Runtime が環境ごとに適用）")]
        public bool enableDebugLog = true;

        [Tooltip("クラッシュレポート送信を想定する環境か（SDK 接続はゲーム側）")]
        public bool enableCrashReporting = false;

        [Tooltip("解析をテスト用 ID / サンドボックス向けにするか")]
        public bool useAnalyticsSandbox = true;

        [Tooltip("解析アプリ ID（Stg=テスト用 / Prod=本番）")]
        public string analyticsAppId = string.Empty;

        [Tooltip("課金をサンドボックスとして扱うか")]
        public bool useIapSandbox = true;

        [Tooltip("PlayerPrefs / セーブキーを環境ごとに分離する")]
        public bool isolateSaveData = true;

        public List<EnvironmentKeyValue> customValues = new List<EnvironmentKeyValue>();
    }
}
