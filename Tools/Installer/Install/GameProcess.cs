using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace CJInstaller.Install
{
    /// <summary>インストール先のゲームが起動中か調べる</summary>
    static class GameProcess
    {
        public static bool IsRunning(string installDirectory)
        {
            string exe = Path.Combine(InstallPaths.Normalize(installDirectory), Product.GameExe);
            foreach (Process process in Process.GetProcessesByName(Product.GameProcessName))
            {
                using (process)
                {
                    try
                    {
                        if (string.Equals(process.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch (Exception exception) when (exception is Win32Exception || exception is InvalidOperationException)
                    {
                    }
                }
            }
            return false;
        }
    }
}
