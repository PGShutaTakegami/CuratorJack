using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// キーごとの日本語・英語・中国語（簡体字・繁体字）の文言を持つ言語テーブル。画面ごとにアセットを分ける。
/// 訳が空の言語は LanguageCodes.Pick の順で代わりの言語を使う。
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

        [Tooltip("中国語（簡体字）")]
        [TextArea]
        public string chineseSimplified;

        [Tooltip("中国語（繁体字）")]
        [TextArea]
        public string chineseTraditional;

        public string TextOf(LanguageCode language) => LanguageCodes.Pick(language, japanese, english, chineseSimplified, chineseTraditional);
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    Dictionary<string, Entry> lookup;

    public IReadOnlyList<Entry> Entries => entries;

    /// <summary>キーがあるか</summary>
    public bool Contains(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        EnsureLookup();
        return lookup.ContainsKey(key);
    }

    /// <summary>現在の表示言語の文言を返す。キーがなければ空文字。</summary>
    public string Get(string key) => Get(key, GameLanguage.Current);

    /// <summary>指定言語の文言を返す。キーがなければ空文字。</summary>
    public string Get(string key, LanguageCode language)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        EnsureLookup();
        return lookup.TryGetValue(key, out Entry entry) ? entry.TextOf(language) : string.Empty;
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
