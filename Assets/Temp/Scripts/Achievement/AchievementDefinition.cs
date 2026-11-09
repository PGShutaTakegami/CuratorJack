using System;
using UnityEngine;

/// <summary>
/// 実績 1 つの定義。名前と説明は言語テーブルの「achievement.{id}.name」「achievement.{id}.description」に置く。
/// 隠し実績は、解除するまで一覧で名前と説明を伏せる。
/// Steam など外部の実績とは SteamApiName で対応づける。
/// </summary>
[Serializable]
public sealed class AchievementDefinition
{
    const string SteamApiNamePrefix = "ACH_";

    [Tooltip("実績を区別する ID（AchievementIds の値）")]
    [SerializeField] private string id;

    [Tooltip("解除するまで名前と説明を伏せる")]
    [SerializeField] private bool hidden;

    [Tooltip("Steamworks に登録する実績の API Name。空なら「ACH_ + ID の大文字」を使う")]
    [SerializeField] private string steamApiName;

    public AchievementDefinition(string id, bool hidden, string steamApiName = null)
    {
        this.id = id;
        this.hidden = hidden;
        this.steamApiName = steamApiName;
    }

    public string Id => id;

    public bool IsHidden => hidden;

    public string SteamApiName => string.IsNullOrEmpty(steamApiName) ? SteamApiNamePrefix + (id ?? string.Empty).ToUpperInvariant() : steamApiName;

    public string NameKey => $"achievement.{id}.name";

    public string DescriptionKey => $"achievement.{id}.description";
}
