using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CJInstaller.Releases;

namespace CJInstaller.Install
{
    /// <summary>
    /// リリースの zip をダウンロードしてインストール先に展開する。アップデートも同じ流れで、前回の一覧にあるファイルを消してから入れ直す。
    /// ダウンロードと確認が済むまでは今のファイルに触れないので、途中で失敗・キャンセルしても今のゲームはそのまま遊べる。
    /// </summary>
    sealed class GameInstaller
    {
        readonly HttpClient http;

        readonly string selfPath;

        /// <param name="selfPath">実行中のこの exe。インストール先へアンインストーラー・アップデーターとしてコピーする</param>
        public GameInstaller(HttpClient http, string selfPath)
        {
            this.http = http;
            this.selfPath = Path.GetFullPath(selfPath);
        }

        /// <summary>UI（STA）スレッドから呼ぶ。重い処理は別スレッドで行い、ショートカットと登録は呼び出し元のスレッドで行う</summary>
        public async Task<InstalledApp> InstallAsync(ReleaseInfo release, InstallOptions options, IProgress<InstallStatus> progress, CancellationToken cancellation)
        {
            string directory = InstallPaths.Normalize(options.Directory);
            if (GameProcess.IsRunning(directory))
                throw new InstallException("ゲームが起動中です。ゲームを閉じてからもう一度お試しください。");

            string zip = Path.Combine(Path.GetTempPath(), $"CJSetup_{Guid.NewGuid():N}.zip");
            try
            {
                await Task.Run(() => DownloadAsync(release, zip, progress, cancellation), cancellation);
                cancellation.ThrowIfCancellationRequested();

                List<string> files = await Task.Run(() => ReplaceFiles(zip, directory, progress));

                progress.Report(new InstallStatus("ショートカットと登録を更新しています…", 1));
                Shortcuts.Apply(directory, options.DesktopShortcut, options.StartMenuShortcut);
                var installed = new InstalledApp(directory, release.Tag, release.Version, release.PublishedAt, options.DesktopShortcut, options.StartMenuShortcut);
                InstallRegistry.Save(installed, files.Sum(file => new FileInfo(Path.Combine(directory, file)).Length));
                return installed;
            }
            finally
            {
                if (File.Exists(zip))
                    File.Delete(zip);
            }
        }

        async Task DownloadAsync(ReleaseInfo release, string zip, IProgress<InstallStatus> progress, CancellationToken cancellation)
        {
            using (HttpResponseMessage response = await http.GetAsync(release.ZipUrl, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                    throw new InstallException($"ダウンロードできませんでした（{(int)response.StatusCode} {response.ReasonPhrase}）");

                long total = response.Content.Headers.ContentLength ?? release.ZipSize;
                using (Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var target = new FileStream(zip, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                using (SHA256 sha = SHA256.Create())
                {
                    var buffer = new byte[81920];
                    long done = 0;
                    int lastPercent = -1;
                    int read;
                    while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false)) > 0)
                    {
                        await target.WriteAsync(buffer, 0, read, cancellation).ConfigureAwait(false);
                        sha.TransformBlock(buffer, 0, read, null, 0);
                        done += read;

                        int percent = total > 0 ? (int)(done * 100 / total) : 0;
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            progress.Report(new InstallStatus($"ダウンロード中… {Megabytes(done)} / {Megabytes(total)} MB", total > 0 ? (double)done / total : (double?)null));
                        }
                    }
                    sha.TransformFinalBlock(new byte[0], 0, 0);

                    string actual = string.Concat(sha.Hash.Select(b => b.ToString("x2")));
                    if (!string.IsNullOrEmpty(release.Sha256) && actual != release.Sha256)
                        throw new InstallException("ダウンロードしたファイルが壊れています。もう一度お試しください。");
                }
            }
        }

        List<string> ReplaceFiles(string zip, string directory, IProgress<InstallStatus> progress)
        {
            progress.Report(new InstallStatus("前のバージョンのファイルを削除しています…", null));
            IReadOnlyList<string> locked = InstallManifest.RemoveListed(directory, InstallManifest.Read(directory), selfPath);
            if (locked.Count > 0)
                throw new InstallException($"使用中のファイルがあるため更新できません。\n{locked[0]}");

            Directory.CreateDirectory(directory);
            var files = new List<string>();
            using (ZipArchive archive = ZipFile.OpenRead(zip))
            {
                List<ZipArchiveEntry> entries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToList();
                for (int i = 0; i < entries.Count; i++)
                {
                    string path = InstallPaths.Resolve(directory, entries[i].FullName)
                        ?? throw new InstallException($"zip の中に不正なパスがあります: {entries[i].FullName}");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    entries[i].ExtractToFile(path, true);
                    files.Add(path.Substring(directory.Length + 1));

                    if (i % 20 == 0 || i == entries.Count - 1)
                        progress.Report(new InstallStatus($"展開しています… {i + 1} / {entries.Count}", (double)(i + 1) / entries.Count));
                }
            }

            foreach (string tool in new[] { Product.UninstallerExe, Product.UpdaterExe })
            {
                string path = Path.Combine(directory, tool);
                if (!string.Equals(path, selfPath, StringComparison.OrdinalIgnoreCase))
                    File.Copy(selfPath, path, true);
                files.Add(tool);
            }

            files.Add(Product.ManifestFile);
            InstallManifest.Write(directory, files);
            return files;
        }

        static string Megabytes(long bytes) => (bytes / 1024d / 1024d).ToString("0.0");
    }
}
