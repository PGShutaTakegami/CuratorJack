namespace CJInstaller.Install
{
    /// <summary>インストール画面で選んだ内容</summary>
    sealed class InstallOptions
    {
        public InstallOptions(string directory, bool desktopShortcut, bool startMenuShortcut)
        {
            Directory = directory;
            DesktopShortcut = desktopShortcut;
            StartMenuShortcut = startMenuShortcut;
        }

        public string Directory { get; }

        public bool DesktopShortcut { get; }

        public bool StartMenuShortcut { get; }
    }
}
