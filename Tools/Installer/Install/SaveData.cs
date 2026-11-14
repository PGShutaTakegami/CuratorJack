using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CJInstaller.Install
{
    /// <summary>
    /// ゲームのセーブデータ。PlayerPrefs（実績・戦績・名前・設定）はレジストリ、アイコンとログは LocalLow のフォルダにある。
    /// </summary>
    static class SaveData
    {
        static readonly Guid LocalAppDataLow = new Guid("A520A1A4-1780-4FF6-BD18-167343C5AF16");

        static string CompanyKey => $@"Software\{Product.SaveDataCompany}";

        static string CompanyDirectory => Path.Combine(LocalLowPath(), Product.SaveDataCompany);

        /// <summary>セーブデータを消す。消せなかった場所を返す（なければ null）</summary>
        public static string Delete()
        {
            string failed = null;
            try
            {
                using (RegistryKey company = Registry.CurrentUser.OpenSubKey(CompanyKey, true))
                {
                    if (company != null)
                    {
                        company.DeleteSubKeyTree(Product.SaveDataProduct, false);
                        bool empty = company.SubKeyCount == 0 && company.ValueCount == 0;
                        company.Close();
                        if (empty)
                            Registry.CurrentUser.DeleteSubKey(CompanyKey, false);
                    }
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException || exception is System.Security.SecurityException)
            {
                failed = $@"HKEY_CURRENT_USER\{CompanyKey}\{Product.SaveDataProduct}";
            }

            string directory = Path.Combine(CompanyDirectory, Product.SaveDataProduct);
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
                if (Directory.Exists(CompanyDirectory) && !Directory.EnumerateFileSystemEntries(CompanyDirectory).Any())
                    Directory.Delete(CompanyDirectory);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                failed = failed ?? directory;
            }

            return failed;
        }

        static string LocalLowPath()
        {
            if (SHGetKnownFolderPath(LocalAppDataLow, 0, IntPtr.Zero, out IntPtr path) == 0)
            {
                try
                {
                    return Marshal.PtrToStringUni(path);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(path);
                }
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow");
        }

        [DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid folderId, uint flags, IntPtr token, out IntPtr path);
    }
}
