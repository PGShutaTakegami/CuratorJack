using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CJInstaller.Install
{
    /// <summary>デスクトップとスタートメニューのショートカットを作る・消す（STA スレッドから呼ぶ）</summary>
    static class Shortcuts
    {
        static string DesktopGame =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Product.Name + ".lnk");

        static string StartMenuFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Product.Name);

        static string StartMenuGame => Path.Combine(StartMenuFolder, Product.Name + ".lnk");

        static string StartMenuUpdate => Path.Combine(StartMenuFolder, Product.Name + " アップデート.lnk");

        /// <summary>選ばれたショートカットを作り、選ばれなかったものは消す</summary>
        public static void Apply(string installDirectory, bool desktop, bool startMenu)
        {
            string game = Path.Combine(installDirectory, Product.GameExe);
            string updater = Path.Combine(installDirectory, Product.UpdaterExe);

            if (desktop)
                Create(DesktopGame, game, game, Product.Name);
            else
                Delete(DesktopGame);

            if (startMenu)
            {
                Directory.CreateDirectory(StartMenuFolder);
                Create(StartMenuGame, game, game, Product.Name);
                Create(StartMenuUpdate, updater, game, Product.Name + " を最新版に更新");
            }
            else
            {
                RemoveStartMenu();
            }
        }

        public static void RemoveAll()
        {
            Delete(DesktopGame);
            RemoveStartMenu();
        }

        static void RemoveStartMenu()
        {
            Delete(StartMenuGame);
            Delete(StartMenuUpdate);
            try
            {
                if (Directory.Exists(StartMenuFolder) && Directory.GetFileSystemEntries(StartMenuFolder).Length == 0)
                    Directory.Delete(StartMenuFolder);
            }
            catch (IOException)
            {
            }
        }

        static void Create(string link, string target, string icon, string description)
        {
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                dynamic shortcut = ((dynamic)shell).CreateShortcut(link);
                try
                {
                    shortcut.TargetPath = target;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(target);
                    shortcut.Description = description;
                    shortcut.IconLocation = icon + ",0";
                    shortcut.Save();
                }
                finally
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        static void Delete(string link)
        {
            if (File.Exists(link))
                File.Delete(link);
        }
    }
}
