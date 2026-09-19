using UnityEngine;

namespace EnvironmentSwitcher
{
    /// <summary>ビルド識別情報。</summary>
    public static class EnvironmentBuildInfo
    {
        public static string Version => Application.version;

        public static string ShortGuid
        {
            get
            {
                string guid = Application.buildGUID;
                if (string.IsNullOrEmpty(guid) || guid.Length < 7)
                {
                    return "editor";
                }

                return guid.Substring(0, 7);
            }
        }

        public static string UnityVersion => Application.unityVersion;

        public static string SummaryLine => $"v{Version} ({ShortGuid})";
    }
}
