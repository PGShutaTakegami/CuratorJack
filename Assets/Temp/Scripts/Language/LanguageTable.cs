using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// キーごとの日本語／英語文言を持つ言語テーブル。タイトル用とゲーム内用でアセットを分ける。
/// </summary>
[CreateAssetMenu(fileName = "LanguageTable", menuName = "CJ/LanguageTable")]
public class LanguageTable : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Tooltip("コードから参照するキー")]
        public string key;

        [Tooltip("日本語")]
        [TextArea]
        public string japanese;

        [Tooltip("英語")]
        [TextArea]
        public string english;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    Dictionary<string, Entry> lookup;

    public IReadOnlyList<Entry> Entries => entries;

    /// <summary>現在の表示言語の文言を返す。キーがなければ空文字。</summary>
    public string Get(string key) => Get(key, GameLanguage.Current);

    /// <summary>指定言語の文言を返す。キーがなければ空文字。</summary>
    public string Get(string key, LanguageCode language)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        EnsureLookup();
        if (!lookup.TryGetValue(key, out Entry entry))
            return string.Empty;

        return language == LanguageCode.English ? entry.english : entry.japanese;
    }

    /// <summary>書式付き文言（string.Format）。キーがなければ空文字。</summary>
    public string Format(string key, params object[] args)
    {
        string template = Get(key);
        if (string.IsNullOrEmpty(template))
            return string.Empty;
        return string.Format(template, args);
    }

    void EnsureLookup()
    {
        if (lookup != null)
            return;

        lookup = new Dictionary<string, Entry>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (string.IsNullOrEmpty(entry.key) || lookup.ContainsKey(entry.key))
                continue;
            lookup.Add(entry.key, entry);
        }
    }

    void OnEnable() => lookup = null;

    void OnValidate() => lookup = null;
}
