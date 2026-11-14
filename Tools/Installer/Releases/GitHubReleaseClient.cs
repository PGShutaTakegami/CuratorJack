using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CJInstaller.Releases
{
    /// <summary>GitHub のリリース一覧から、ゲームの zip が付いた一番新しいリリースを探す（プレリリースも含む）</summary>
    sealed class GitHubReleaseClient
    {
        static readonly Regex VersionPattern = new Regex(@"バージョン\s*([0-9]+(?:\.[0-9]+)+)");

        readonly HttpClient http;

        public GitHubReleaseClient(HttpClient http)
        {
            this.http = http;
        }

        public async Task<ReleaseInfo> FetchLatestAsync(CancellationToken cancellation)
        {
            string url = $"https://api.github.com/repos/{Product.RepositoryOwner}/{Product.RepositoryName}/releases?per_page=30";
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using (HttpResponseMessage response = await http.SendAsync(request, cancellation).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException($"GitHub からリリース一覧を取得できませんでした（{(int)response.StatusCode} {response.ReasonPhrase}）");

                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        var releases = (List<ReleaseDto>)new DataContractJsonSerializer(typeof(List<ReleaseDto>)).ReadObject(stream);
                        ReleaseInfo latest = releases
                            .Where(release => !release.Draft)
                            .Select(ToReleaseInfo)
                            .Where(release => release != null)
                            .OrderByDescending(release => release.PublishedAt)
                            .FirstOrDefault();
                        return latest ?? throw new InvalidOperationException("インストールできるリリースが見つかりませんでした");
                    }
                }
            }
        }

        static ReleaseInfo ToReleaseInfo(ReleaseDto release)
        {
            AssetDto zip = release.Assets?.FirstOrDefault(asset =>
                asset.Name != null
                && asset.Name.StartsWith(Product.AssetPrefix, StringComparison.OrdinalIgnoreCase)
                && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (zip == null || !DateTime.TryParse(release.PublishedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime publishedAt))
                return null;

            Match version = VersionPattern.Match(release.Body ?? string.Empty);
            return new ReleaseInfo(
                release.TagName,
                version.Success ? version.Groups[1].Value : string.Empty,
                publishedAt.ToUniversalTime(),
                zip.DownloadUrl,
                zip.Size,
                ParseSha256(zip.Digest));
        }

        static string ParseSha256(string digest)
        {
            const string prefix = "sha256:";
            return digest != null && digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? digest.Substring(prefix.Length).ToLowerInvariant()
                : string.Empty;
        }

        [DataContract]
        sealed class ReleaseDto
        {
            [DataMember(Name = "tag_name")] public string TagName { get; set; }
            [DataMember(Name = "body")] public string Body { get; set; }
            [DataMember(Name = "draft")] public bool Draft { get; set; }
            [DataMember(Name = "published_at")] public string PublishedAt { get; set; }
            [DataMember(Name = "assets")] public List<AssetDto> Assets { get; set; }
        }

        [DataContract]
        sealed class AssetDto
        {
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "size")] public long Size { get; set; }
            [DataMember(Name = "browser_download_url")] public string DownloadUrl { get; set; }
            [DataMember(Name = "digest")] public string Digest { get; set; }
        }
    }
}
