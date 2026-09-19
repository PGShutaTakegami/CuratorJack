using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace EnvironmentSwitcher
{
    /// <summary>
    /// ログを persistentDataPath 配下のファイルへ追記する。
    /// </summary>
    public static class EnvironmentLogFile
    {
        private static bool _enabled;
        private static string _filePath;
        private static readonly object Gate = new object();

        public static string CurrentFilePath => _filePath;

        public static string LogDirectory
        {
            get
            {
                return Path.Combine(Application.persistentDataPath, "EnvironmentSwitcherLogs");
            }
        }

        public static void SetEnabled(bool enabled)
        {
            if (_enabled == enabled)
            {
                return;
            }

            if (enabled)
            {
                EnsurePath();
                Application.logMessageReceivedThreaded += HandleLog;
                _enabled = true;
                WriteLine($"---- Log capture start ({DateTime.Now:O}) env={EnvironmentRuntime.Current} ----");
            }
            else
            {
                Application.logMessageReceivedThreaded -= HandleLog;
                _enabled = false;
            }
        }

        public static void OpenLogFolder()
        {
            EnsurePath();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(LogDirectory);
#else
            Application.OpenURL(new Uri(LogDirectory).AbsoluteUri);
#endif
        }

        private static void EnsurePath()
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }

            if (string.IsNullOrEmpty(_filePath))
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                _filePath = Path.Combine(
                    LogDirectory,
                    $"{EnvironmentRuntime.Current}-{stamp}.log");
            }
        }

        private static void HandleLog(string condition, string stackTrace, LogType type)
        {
            WriteLine($"[{DateTime.Now:HH:mm:ss}] [{type}] {condition}");
            if (type == LogType.Exception || type == LogType.Error)
            {
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    WriteLine(stackTrace);
                }
            }
        }

        private static void WriteLine(string line)
        {
            lock (Gate)
            {
                try
                {
                    EnsurePath();
                    File.AppendAllText(_filePath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception e)
                {
                    // 再帰ログを避ける
                    System.Console.WriteLine("EnvironmentLogFile write failed: " + e.Message);
                }
            }
        }
    }
}
