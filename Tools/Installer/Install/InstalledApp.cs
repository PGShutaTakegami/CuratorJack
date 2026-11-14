using System;

namespace CJInstaller.Install
{
    /// <summary>この PC にインストール済みのゲームの情報</summary>
    sealed class InstalledApp
    {
        public InstalledApp(string location, string tag, string version, DateTime publishedAt, bool desktopShortcut, bool startMenuShortcut)
        {
            Location = location;
            Tag = tag;
            Version = version;
            PublishedAt = publishedAt;
            DesktopShortcut = desktopShortcut;
            StartMenuShortcut = startMenuShortcut;
        }

        public string Location { get; }

        /// <summary>インストールしたリリースのタグ（例: online-20261111）</summary>
        public string Tag { get; }

        public string Version { get; }

        public DateTime PublishedAt { get; }

        public bool DesktopShortcut { get; }

        public bool StartMenuShortcut { get; }

        public string DisplayVersion => string.IsNullOrEmpty(Version) ? Tag : $"{Version}（{Tag}）";
    }
}
