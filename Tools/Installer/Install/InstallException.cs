using System;

namespace CJInstaller.Install
{
    /// <summary>インストール・アンインストールを続けられない理由（メッセージはそのまま画面に出す）</summary>
    sealed class InstallException : Exception
    {
        public InstallException(string message) : base(message)
        {
        }
    }
}
