using UnityEngine;

namespace EnvironmentSwitcher
{
    /// <summary>
    /// Production で Dev 機能が残らないようにする実行時ガード。
    /// </summary>
    public static class EnvironmentProductionGuard
    {
        public static void EnforceRuntime()
        {
            if (EnvironmentRuntime.Current != GameEnvironment.Release)
            {
                return;
            }

#if ENV_DEV
            Debug.LogError(
                "[EnvironmentSwitcher] Production なのに ENV_DEV が定義されています。Define 適用を確認してください。");
#endif

            DevDebugOverlay overlay = Object.FindFirstObjectByType<DevDebugOverlay>();
            if (overlay != null)
            {
                Object.Destroy(overlay.gameObject);
                Debug.LogWarning("[EnvironmentSwitcher] Production のため DevDebugOverlay を破棄しました。");
            }
        }
    }
}
