using System;
using System.IO;

namespace CJInstaller
{
    /// <summary>インストールするゲームと、配布元（GitHub リリース）の情報</summary>
    static class Product
    {
        public const string Name = "CuratorJack";

        public const string Publisher = "PGShutaTakegami";

        public const string RepositoryOwner = "PGShutaTakegami";

        public const string RepositoryName = "CuratorJack";

        /// <summary>リリースに付いているゲーム本体の zip の名前の先頭（例: CJ_Online_20261111.zip）</summary>
        public const string AssetPrefix = "CJ_Online_";

        public const string GameExe = "CJ.exe";

        public const string GameProcessName = "CJ";

        public const string UninstallerExe = "Uninstall.exe";

        public const string UpdaterExe = "CJUpdate.exe";

        public const string ManifestFile = "install-manifest.txt";

        /// <summary>ゲームの会社名（Unity の Company Name）。セーブデータの場所に使われる</summary>
        public const string SaveDataCompany = "DefaultCompany";

        /// <summary>ゲームの製品名（Unity の Product Name）。セーブデータの場所に使われる</summary>
        public const string SaveDataProduct = "CJ";

        public static string DefaultInstallDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Name);
    }
}
