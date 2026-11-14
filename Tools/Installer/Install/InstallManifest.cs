using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CJInstaller.Install
{
    /// <summary>
    /// インストールしたファイルの一覧（インストール先からの相対パス）をインストール先に保存する。
    /// アップデートとアンインストールでは、この一覧に載っているファイルだけを消す（ほかのファイルには触れない）。
    /// </summary>
    static class InstallManifest
    {
        public static IReadOnlyList<string> Read(string directory)
        {
            string path = Path.Combine(directory, Product.ManifestFile);
            return File.Exists(path)
                ? File.ReadAllLines(path, Encoding.UTF8).Where(line => !string.IsNullOrWhiteSpace(line)).ToList()
                : new List<string>();
        }

        public static void Write(string directory, IEnumerable<string> relativePaths)
        {
            File.WriteAllLines(Path.Combine(directory, Product.ManifestFile), relativePaths.Distinct(StringComparer.OrdinalIgnoreCase), new UTF8Encoding(false));
        }

        /// <summary>一覧のファイルと、空になったフォルダを消す。keepPath（実行中の自分など）は残す。消せなかったファイルを返す</summary>
        public static IReadOnlyList<string> RemoveListed(string directory, IEnumerable<string> relativePaths, string keepPath)
        {
            string root = InstallPaths.Normalize(directory);
            var failed = new List<string>();
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string relative in relativePaths)
            {
                string path = InstallPaths.Resolve(root, relative);
                if (path == null || string.Equals(path, keepPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                for (string folder = Path.GetDirectoryName(path); folder != null && folder.Length > root.Length; folder = Path.GetDirectoryName(folder))
                    folders.Add(folder);

                try
                {
                    if (File.Exists(path))
                    {
                        File.SetAttributes(path, FileAttributes.Normal);
                        File.Delete(path);
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    failed.Add(path);
                }
            }

            foreach (string folder in folders.OrderByDescending(folder => folder.Length))
            {
                try
                {
                    if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                        Directory.Delete(folder);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                }
            }

            return failed;
        }
    }
}
