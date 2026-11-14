using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CJInstaller.Install;

namespace CJInstaller.UI
{
    /// <summary>
    /// アンインストールの確認と実行。インストール先の Uninstall.exe は実行中だと自分を消せないため、
    /// 一時フォルダへ自分をコピーしてそちらで実行し直し、終わったら一時コピーも消す。
    /// </summary>
    static class UninstallFlow
    {
        const string UninstallArg = "/uninstall";
        const string LocationArg = "/location";
        const string TempCopyArg = "/tempcopy";

        static string Caption => $"{Product.Name} のアンインストール";

        /// <summary>引数か exe 名（Uninstall.exe）でアンインストールが指示されたか</summary>
        public static bool IsRequested(string[] args, string selfPath) =>
            args.Any(arg => string.Equals(arg, UninstallArg, StringComparison.OrdinalIgnoreCase))
            || string.Equals(Path.GetFileName(selfPath), Product.UninstallerExe, StringComparison.OrdinalIgnoreCase);

        public static int Run(string[] args, string selfPath)
        {
            string directory = FindInstallDirectory(args, selfPath);
            if (directory == null)
            {
                MessageBox.Show($"{Product.Name} はインストールされていません。", Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }

            if (InstallPaths.IsInside(selfPath, directory))
            {
                RelaunchFromTemp(selfPath, directory);
                return 0;
            }

            int result = Uninstall(directory, selfPath);
            if (args.Any(arg => string.Equals(arg, TempCopyArg, StringComparison.OrdinalIgnoreCase)))
                ScheduleDelete(selfPath);
            return result;
        }

        static int Uninstall(string directory, string selfPath)
        {
            bool deleteSaveData;
            using (var dialog = new UninstallDialog(Caption, directory))
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                    return 1;
                deleteSaveData = dialog.DeleteSaveData;
            }

            if (GameProcess.IsRunning(directory))
            {
                MessageBox.Show("ゲームが起動中です。ゲームを閉じてからもう一度お試しください。", Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }

            IReadOnlyList<string> failed = Uninstaller.Run(directory, selfPath);
            string saveDataFailed = deleteSaveData ? SaveData.Delete() : null;

            var message = new StringBuilder($"{Product.Name} をアンインストールしました。");
            message.Append(!deleteSaveData ? "\nセーブデータは残しています。"
                : saveDataFailed == null ? "\nセーブデータも削除しました。"
                : $"\nセーブデータの一部を削除できませんでした。\n{saveDataFailed}");
            if (failed.Count > 0)
                message.Append($"\n使用中のため消せなかったファイルがあります。\n{failed[0]}");

            bool clean = failed.Count == 0 && saveDataFailed == null;
            MessageBox.Show(message.ToString(), Caption, MessageBoxButtons.OK, clean ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            return 0;
        }

        static string FindInstallDirectory(string[] args, string selfPath)
        {
            int index = Array.FindIndex(args, arg => string.Equals(arg, LocationArg, StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && index + 1 < args.Length)
                return args[index + 1];

            InstalledApp installed = InstallRegistry.Load();
            if (installed != null)
                return installed.Location;

            string selfDirectory = Path.GetDirectoryName(selfPath);
            return File.Exists(Path.Combine(selfDirectory, Product.ManifestFile)) ? selfDirectory : null;
        }

        static void RelaunchFromTemp(string selfPath, string directory)
        {
            string copy = Path.Combine(Path.GetTempPath(), $"CJUninstall_{Guid.NewGuid():N}.exe");
            File.Copy(selfPath, copy);
            string arguments = $"{UninstallArg} {TempCopyArg} {LocationArg} \"{InstallPaths.Normalize(directory)}\"";
            Process.Start(new ProcessStartInfo(copy, arguments) { UseShellExecute = false })?.Dispose();
        }

        static void ScheduleDelete(string path)
        {
            var command = new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 > nul & del /f /q \"{path}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            Process.Start(command)?.Dispose();
        }
    }
}
