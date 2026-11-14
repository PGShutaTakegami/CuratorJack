using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CJInstaller.Install;
using CJInstaller.Releases;

namespace CJInstaller.UI
{
    /// <summary>
    /// インストールとアップデートの画面。起動すると GitHub の最新リリースを読み、
    /// 未インストールならインストール、古ければアップデート、最新なら再インストールを出す。
    /// </summary>
    sealed class InstallerForm : Form
    {
        enum NextAction { Retry, Install, Update, Reinstall, Launch }

        readonly GitHubReleaseClient releases;
        readonly GameInstaller installer;

        readonly Label versionLabel = new Label();
        readonly TextBox folderBox = new TextBox();
        readonly Button browseButton = new Button();
        readonly CheckBox desktopCheck = new CheckBox();
        readonly CheckBox startMenuCheck = new CheckBox();
        readonly ProgressBar progressBar = new ProgressBar();
        readonly Label statusLabel = new Label();
        readonly Button actionButton = new Button();
        readonly Button closeButton = new Button();

        InstalledApp installed;
        ReleaseInfo latest;
        NextAction next;
        CancellationTokenSource running;

        public InstallerForm(GitHubReleaseClient releases, GameInstaller installer)
        {
            this.releases = releases;
            this.installer = installer;
            BuildLayout();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await RefreshAsync();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (running != null)
            {
                running.Cancel();
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        void BuildLayout()
        {
            Text = $"{Product.Name} セットアップ";
            Font = new Font("Yu Gothic UI", 9f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 310);

            var title = new Label { Text = Product.Name, Font = new Font("Yu Gothic UI", 16f, FontStyle.Bold), Location = new Point(16, 12), AutoSize = true };
            versionLabel.SetBounds(16, 54, 488, 40);
            var folderLabel = new Label { Text = "インストール先", Location = new Point(16, 102), AutoSize = true };
            folderBox.SetBounds(16, 122, 400, 24);
            browseButton.Text = "参照…";
            browseButton.SetBounds(424, 121, 80, 26);
            browseButton.Click += (_, __) => BrowseFolder();
            desktopCheck.Text = "デスクトップにショートカットを作成する";
            desktopCheck.SetBounds(16, 158, 488, 22);
            startMenuCheck.Text = "スタートメニューにショートカット（起動・アップデート）を作成する";
            startMenuCheck.SetBounds(16, 182, 488, 22);
            progressBar.SetBounds(16, 218, 488, 18);
            statusLabel.SetBounds(16, 242, 488, 20);
            statusLabel.AutoEllipsis = true;
            actionButton.SetBounds(296, 270, 100, 30);
            actionButton.Click += async (_, __) => await RunNextAsync();
            closeButton.Text = "閉じる";
            closeButton.SetBounds(404, 270, 100, 30);
            closeButton.Click += (_, __) =>
            {
                if (running != null)
                    running.Cancel();
                else
                    Close();
            };

            Controls.AddRange(new Control[] { title, versionLabel, folderLabel, folderBox, browseButton, desktopCheck, startMenuCheck, progressBar, statusLabel, actionButton, closeButton });
            AcceptButton = actionButton;
        }

        async Task RefreshAsync()
        {
            installed = InstallRegistry.Load();
            bool fresh = installed == null;
            folderBox.Text = fresh ? Product.DefaultInstallDirectory : installed.Location;
            desktopCheck.Checked = fresh || installed.DesktopShortcut;
            startMenuCheck.Checked = fresh || installed.StartMenuShortcut;
            TrySetIcon();

            SetBusy(true);
            ShowStatus("最新版を確認しています…", null);
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                try
                {
                    latest = await releases.FetchLatestAsync(timeout.Token);
                    ShowStatus(string.Empty, 0);
                }
                catch (Exception exception)
                {
                    latest = null;
                    ShowStatus("最新版を確認できませんでした: " + Describe(exception), 0);
                }
            }
            SetBusy(false);

            next = latest == null ? NextAction.Retry
                : fresh ? NextAction.Install
                : installed.Tag == latest.Tag || installed.PublishedAt > latest.PublishedAt ? NextAction.Reinstall
                : NextAction.Update;
            ShowVersions();
            ShowNextAction();
        }

        async Task RunNextAsync()
        {
            switch (next)
            {
                case NextAction.Retry:
                    await RefreshAsync();
                    break;
                case NextAction.Launch:
                    LaunchGame();
                    break;
                default:
                    await InstallAsync();
                    break;
            }
        }

        async Task InstallAsync()
        {
            string directory = folderBox.Text.Trim();
            if (!IsValidDirectory(directory) || !ConfirmDirectory(directory))
                return;

            bool finished = false;
            var progress = new Progress<InstallStatus>(status =>
            {
                if (!finished)
                    ShowStatus(status.Message, status.Fraction);
            });
            running = new CancellationTokenSource();
            SetBusy(true);
            closeButton.Text = "キャンセル";
            try
            {
                installed = await installer.InstallAsync(latest, new InstallOptions(directory, desktopCheck.Checked, startMenuCheck.Checked), progress, running.Token);
                ShowStatus(next == NextAction.Update ? "アップデートが完了しました" : "インストールが完了しました", 1);
                next = NextAction.Launch;
            }
            catch (OperationCanceledException)
            {
                ShowStatus("キャンセルしました", 0);
            }
            catch (Exception exception)
            {
                ShowStatus("失敗しました: " + Describe(exception), 0);
                MessageBox.Show(this, Describe(exception), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                finished = true;
                running.Dispose();
                running = null;
                closeButton.Text = "閉じる";
                SetBusy(false);
            }
            ShowVersions();
            ShowNextAction();
        }

        bool IsValidDirectory(string directory)
        {
            if (Path.IsPathRooted(directory) && directory.IndexOfAny(Path.GetInvalidPathChars()) < 0)
                return true;

            MessageBox.Show(this, "インストール先のフォルダを正しく指定してください。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        /// <summary>ほかのファイルが入っているフォルダへ新しく入れるときだけ確認する</summary>
        bool ConfirmDirectory(string directory)
        {
            bool hasOtherFiles = installed == null
                && Directory.Exists(directory)
                && Directory.EnumerateFileSystemEntries(directory).Any()
                && !File.Exists(Path.Combine(directory, Product.ManifestFile));
            return !hasOtherFiles
                || MessageBox.Show(this, $"「{directory}」にはすでにファイルがあります。\nこのフォルダにインストールしますか？", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        void BrowseFolder()
        {
            using (var dialog = new FolderBrowserDialog { Description = "インストール先のフォルダを選んでください（中に CuratorJack フォルダを作ります）" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                string selected = dialog.SelectedPath;
                folderBox.Text = string.Equals(Path.GetFileName(selected.TrimEnd('\\')), Product.Name, StringComparison.OrdinalIgnoreCase)
                    ? selected
                    : Path.Combine(selected, Product.Name);
            }
        }

        void LaunchGame()
        {
            string exe = Path.Combine(installed.Location, Product.GameExe);
            Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = installed.Location, UseShellExecute = true })?.Dispose();
            Close();
        }

        void ShowVersions()
        {
            string current = installed == null ? "なし" : installed.DisplayVersion;
            string newest = latest == null ? "-" : $"{latest.DisplayVersion}　{latest.PublishedAt.ToLocalTime():yyyy/MM/dd}";
            versionLabel.Text = $"インストール済み: {current}\n最新版: {newest}";
        }

        void ShowNextAction()
        {
            switch (next)
            {
                case NextAction.Retry: actionButton.Text = "再試行"; break;
                case NextAction.Install: actionButton.Text = "インストール"; break;
                case NextAction.Update: actionButton.Text = "アップデート"; break;
                case NextAction.Reinstall: actionButton.Text = "再インストール"; break;
                case NextAction.Launch: actionButton.Text = "ゲームを起動"; break;
            }

            if (next == NextAction.Reinstall && string.IsNullOrEmpty(statusLabel.Text))
                ShowStatus("最新版がインストールされています", 0);
            if (next == NextAction.Update && string.IsNullOrEmpty(statusLabel.Text))
                ShowStatus("新しいバージョンがあります", 0);

            bool canChangeFolder = installed == null && next != NextAction.Launch;
            folderBox.Enabled = canChangeFolder;
            browseButton.Enabled = canChangeFolder;
            bool canChangeShortcuts = next != NextAction.Launch;
            desktopCheck.Enabled = canChangeShortcuts;
            startMenuCheck.Enabled = canChangeShortcuts;
        }

        void SetBusy(bool busy)
        {
            actionButton.Enabled = !busy;
            folderBox.Enabled = !busy && installed == null;
            browseButton.Enabled = !busy && installed == null;
            desktopCheck.Enabled = !busy;
            startMenuCheck.Enabled = !busy;
            UseWaitCursor = busy;
        }

        void ShowStatus(string message, double? fraction)
        {
            statusLabel.Text = message;
            progressBar.Style = fraction.HasValue ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
            if (fraction.HasValue)
                progressBar.Value = (int)Math.Round(Math.Max(0, Math.Min(1, fraction.Value)) * progressBar.Maximum);
        }

        void TrySetIcon()
        {
            string exe = installed == null ? null : Path.Combine(installed.Location, Product.GameExe);
            if (exe != null && File.Exists(exe))
                Icon = Icon.ExtractAssociatedIcon(exe);
        }

        static string Describe(Exception exception)
        {
            switch (exception)
            {
                case InstallException install: return install.Message;
                case UnauthorizedAccessException _: return "このフォルダには書き込めません。別のフォルダを選んでください。";
                case TaskCanceledException _: return "時間内に応答がありませんでした。通信状況を確認してください。";
                case System.Net.Http.HttpRequestException _: return "インターネットに接続できませんでした。通信状況を確認してください。";
                default: return exception.Message;
            }
        }
    }
}
