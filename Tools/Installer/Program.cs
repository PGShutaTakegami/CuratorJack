using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;
using CJInstaller.Install;
using CJInstaller.Releases;
using CJInstaller.UI;

namespace CJInstaller
{
    /// <summary>
    /// CJSetup.exe の入口。同じ exe をインストール先に Uninstall.exe（アンインストール）と CJUpdate.exe（アップデート）としても置く。
    /// </summary>
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string selfPath = Application.ExecutablePath;
            if (UninstallFlow.IsRequested(args, selfPath))
                return UninstallFlow.Run(args, selfPath);

            using (var mutex = new Mutex(true, @"Local\CJSetup", out bool isFirst))
            {
                if (!isFirst)
                {
                    MessageBox.Show("セットアップはすでに起動しています。", Product.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 1;
                }

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan })
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("CJSetup/1.0");
                    Application.Run(new InstallerForm(new GitHubReleaseClient(http), new GameInstaller(http, selfPath)));
                }
                return 0;
            }
        }
    }
}
