using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>言語テーブルとカード・アイテムのデータが 4 言語すべてそろっていることを確かめる</summary>
public class LocalizationCoverageTests
{
    static readonly string[] EntryLanguages = { "japanese", "english", "chineseSimplified", "chineseTraditional" };
    static readonly Regex Placeholder = new Regex(@"\{(\d+)[^}]*\}");

    static IEnumerable<SerializedProperty> Entries(LanguageTable table)
    {
        SerializedProperty entries = new SerializedObject(table).FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
            yield return entries.GetArrayElementAtIndex(i);
    }

    static string Placeholders(string text) =>
        string.Join(",", Placeholder.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(v => v));

    [Test]
    public void EveryTableEntryHasAllLanguagesAndSamePlaceholders()
    {
        LanguageTable[] tables = Resources.LoadAll<LanguageTable>(GameText.ResourceFolder);
        Assert.That(tables.Length, Is.GreaterThan(0));

        var problems = new List<string>();
        var keys = new HashSet<string>();
        foreach (LanguageTable table in tables)
        {
            foreach (SerializedProperty entry in Entries(table))
            {
                string key = entry.FindPropertyRelative("key").stringValue;
                if (!keys.Add(key))
                    problems.Add($"{table.name}: {key} が他のテーブルと重複");
                string japanese = entry.FindPropertyRelative("japanese").stringValue;
                foreach (string language in EntryLanguages)
                {
                    string text = entry.FindPropertyRelative(language).stringValue;
                    if (string.IsNullOrEmpty(text))
                        problems.Add($"{table.name}: {key} の {language} が空");
                    else if (Placeholders(text) != Placeholders(japanese))
                        problems.Add($"{table.name}: {key} の {language} の {{n}} が日本語と違う");
                }
            }
        }
        Assert.That(problems, Is.Empty, string.Join("\n", problems));
    }

    [Test]
    public void EveryCardAndItemHasAllLanguages()
    {
        string[] translated = { "englishName", "englishDescription", "chineseSimplifiedName", "chineseSimplifiedDescription",
            "chineseTraditionalName", "chineseTraditionalDescription" };
        var problems = new List<string>();
        foreach (string path in AssetDatabase.FindAssets("t:CardData").Concat(AssetDatabase.FindAssets("t:ItemDefinition")).Select(AssetDatabase.GUIDToAssetPath))
        {
            var serialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(path));
            SerializedProperty parameter = serialized.FindProperty("parameter");
            foreach (string field in translated)
            {
                SerializedProperty property = parameter != null ? parameter.FindPropertyRelative(field) : serialized.FindProperty(field);
                if (property == null || string.IsNullOrEmpty(property.stringValue))
                    problems.Add($"{path}: {field} が空");
            }
        }
        Assert.That(problems, Is.Empty, string.Join("\n", problems));
    }

    [Test]
    public void GameTextFindsKeysInEachLanguage()
    {
        Assert.That(GameText.Get("game.ui.confirm", LanguageCode.Japanese), Is.EqualTo("確定"));
        Assert.That(GameText.Get("game.ui.confirm", LanguageCode.English), Is.EqualTo("Confirm"));
        Assert.That(GameText.Get("title.common.cancel", LanguageCode.ChineseSimplified), Is.EqualTo("取消"));
        Assert.That(GameText.Get("title.common.cancel", LanguageCode.ChineseTraditional), Is.EqualTo("取消"));
        Assert.That(GameText.Get("missing.key", LanguageCode.English), Is.EqualTo("missing.key"));
    }
}
