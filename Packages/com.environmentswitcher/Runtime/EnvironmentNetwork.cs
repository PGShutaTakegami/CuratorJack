using UnityEngine;

namespace EnvironmentSwitcher
{
    /// <summary>
    /// ネット通信の共通フラグ・統計・エラーログ補助。
    /// </summary>
    public static class EnvironmentNetwork
    {
        private static int _successCount;
        private static int _failCount;

        /// <summary>
        /// Apply 済みのネット通信 ON/OFF。
        /// Settings があればそれを優先（Apply 直後の再コンパイル前でも正しい）。
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                EnvironmentSettings settings = EnvironmentRuntime.Settings;
                if (settings != null)
                {
                    return settings.EnableNetwork;
                }

#if ENV_NETWORK
                return true;
#else
                return false;
#endif
            }
        }

        public static int SuccessCount => _successCount;

        public static int FailCount => _failCount;

        public static int SampleCount => _successCount + _failCount;

        /// <summary>0〜100。サンプルが無いときは -1。</summary>
        public static float PacketLossPercent
        {
            get
            {
                int total = SampleCount;
                if (total <= 0)
                {
                    return -1f;
                }

                return (_failCount * 100f) / total;
            }
        }

        /// <summary>
        /// 通信してよいか。OFF のときは false を返し、呼び出し側はモック／スキップする。
        /// </summary>
        public static bool TryBeginRequest(string operationName = null)
        {
            if (IsEnabled)
            {
                return true;
            }

            string op = string.IsNullOrEmpty(operationName) ? "network" : operationName;
            Debug.LogWarning($"[EnvironmentNetwork] 通信オフのためスキップ: {op}");
            return false;
        }

        /// <summary>通信成功を記録（パケロス表示用）。</summary>
        public static void ReportSuccess()
        {
            _successCount++;
        }

        /// <summary>通信失敗を記録（パケロス表示用）。</summary>
        public static void ReportFailure()
        {
            _failCount++;
        }

        public static void ResetStats()
        {
            _successCount = 0;
            _failCount = 0;
        }

        /// <summary>ネット関連エラー。全環境でエラーログが出る。失敗統計にも加算。</summary>
        public static void LogError(string message)
        {
            ReportFailure();
            Debug.LogError($"[EnvironmentNetwork] {message}");
        }

        /// <summary>ネット関連例外。全環境でエラーログが出る。失敗統計にも加算。</summary>
        public static void LogException(System.Exception exception, string context = null)
        {
            ReportFailure();
            if (!string.IsNullOrEmpty(context))
            {
                Debug.LogError($"[EnvironmentNetwork] {context}");
            }

            Debug.LogException(exception);
        }
    }
}
