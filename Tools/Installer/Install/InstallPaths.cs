using System;
using System.IO;

namespace CJInstaller.Install
{
    /// <summary>インストール先の中のパスを扱う（一覧や zip の相対パスがインストール先の外を指さないようにする）</summary>
    static class InstallPaths
    {
        public static string Normalize(string directory) =>
            Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        /// <summary>root の中の相対パスを絶対パスにする。root の外を指すときは null</summary>
        public static string Resolve(string root, string relative)
        {
            string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? path : null;
        }

        public static bool IsInside(string path, string directory) =>
            Resolve(Normalize(directory), Path.GetFullPath(path)) != null;
    }
}
