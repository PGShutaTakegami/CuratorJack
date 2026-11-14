using System;

namespace CJInstaller.Releases
{
    /// <summary>インストールできる GitHub リリース 1 件</summary>
    sealed class ReleaseInfo
    {
        public ReleaseInfo(string tag, string version, DateTime publishedAt, string zipUrl, long zipSize, string sha256)
        {
            Tag = tag;
            Version = version;
            PublishedAt = publishedAt;
            ZipUrl = zipUrl;
            ZipSize = zipSize;
            Sha256 = sha256;
        }

        public string Tag { get; }

        /// <summary>リリースの説明文に書かれたゲームのバージョン（書かれていなければ空）</summary>
        public string Version { get; }

        public DateTime PublishedAt { get; }

        public string ZipUrl { get; }

        public long ZipSize { get; }

        /// <summary>zip の SHA-256（16 進の小文字。GitHub が返さなければ空）</summary>
        public string Sha256 { get; }

        public string DisplayVersion => string.IsNullOrEmpty(Version) ? Tag : $"{Version}（{Tag}）";
    }
}
