using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CJInstaller.Install
{
    /// <summary>
    /// インストールしたファイル・ショートカット・「アプリと機能」の登録を消す。
    /// セーブデータ（レジストリの設定・AppData）はインストール先の外にあるので残る。
    /// </summary>
    static class Uninstaller
    {
        /// <summary>消せなかったファイルを返す。keepPath（実行中の自分）は消さない</summary>
        public static IReadOnlyList<string> Run(string directory, string keepPath)
        {
            Shortcuts.RemoveAll();
            IReadOnlyList<string> failed = InstallManifest.RemoveListed(directory, InstallManifest.Read(directory), keepPath);
            InstallRegistry.Delete();

            try
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
            }

            return failed;
        }
    }
}
