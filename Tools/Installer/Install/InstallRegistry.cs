using System;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace CJInstaller.Install
{
    /// <summary>
    /// インストール情報を「アプリと機能」の登録（現在のユーザーの Uninstall キー）に読み書きする。
    /// アンインストールの起動先も同じキーに書くので、Windows の設定からもアンインストールできる。
    /// </summary>
    static class InstallRegistry
    {
        const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + Product.Name;

        const string TagValue = "CJReleaseTag";
        const string VersionValue = "CJVersion";
        const string PublishedAtValue = "CJPublishedAt";
        const string DesktopValue = "CJDesktopShortcut";
        const string StartMenuValue = "CJStartMenuShortcut";

        /// <summary>インストール済みならその情報、なければ null</summary>
        public static InstalledApp Load()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
            {
                string location = key?.GetValue("InstallLocation") as string;
                if (string.IsNullOrEmpty(location))
                    return null;

                DateTime.TryParse(key.GetValue(PublishedAtValue) as string, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime publishedAt);
                return new InstalledApp(
                    location,
                    key.GetValue(TagValue) as string ?? string.Empty,
                    key.GetValue(VersionValue) as string ?? string.Empty,
                    publishedAt,
                    ReadFlag(key, DesktopValue),
                    ReadFlag(key, StartMenuValue));
            }
        }

        public static void Save(InstalledApp app, long installedBytes)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                key.SetValue("DisplayName", Product.Name);
                key.SetValue("DisplayVersion", string.IsNullOrEmpty(app.Version) ? app.Tag : app.Version);
                key.SetValue("Publisher", Product.Publisher);
                key.SetValue("InstallLocation", app.Location);
                key.SetValue("DisplayIcon", Path.Combine(app.Location, Product.GameExe));
                key.SetValue("UninstallString", $"\"{Path.Combine(app.Location, Product.UninstallerExe)}\" /uninstall");
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, installedBytes / 1024), RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue(TagValue, app.Tag);
                key.SetValue(VersionValue, app.Version ?? string.Empty);
                key.SetValue(PublishedAtValue, app.PublishedAt.ToString("o", CultureInfo.InvariantCulture));
                key.SetValue(DesktopValue, app.DesktopShortcut ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(StartMenuValue, app.StartMenuShortcut ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        public static void Delete()
        {
            Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false);
        }

        static bool ReadFlag(RegistryKey key, string name) => key.GetValue(name) is int value && value != 0;
    }
}
