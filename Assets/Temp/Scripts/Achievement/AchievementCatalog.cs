using System.Collections.Generic;
using UnityEngine;

/// <summary>実績の一覧と、表示に使う文言とアイコン。Resources/AchievementCatalog に置いて AchievementService が読む</summary>
[CreateAssetMenu(fileName = "AchievementCatalog", menuName = "CJ/AchievementCatalog")]
public class AchievementCatalog : ScriptableObject
{
    [Tooltip("一覧に並べる順")]
    [SerializeField] private List<AchievementDefinition> achievements = new List<AchievementDefinition>();

    [Tooltip("実績の名前・説明とトロフィー画面・通知の文言")]
    [SerializeField] private LanguageTable texts;

    [SerializeField] private Sprite unlockedIcon;

    [Tooltip("未解除の普通の実績のアイコン（灰色のトロフィー）")]
    [SerializeField] private Sprite lockedIcon;

    [Tooltip("未解除の隠し実績のアイコン")]
    [SerializeField] private Sprite secretIcon;

    public IReadOnlyList<AchievementDefinition> Achievements => achievements;

    public LanguageTable Texts => texts;

    public string NameOf(AchievementDefinition definition) => Text(definition.NameKey);

    public string DescriptionOf(AchievementDefinition definition) => Text(definition.DescriptionKey);

    public string Text(string key) => texts != null ? texts.Get(key) : key;

    public string Format(string key, params object[] args) => texts != null ? texts.Format(key, args) : key;

    public Sprite IconOf(AchievementDefinition definition, bool unlocked)
    {
        if (unlocked)
            return unlockedIcon;
        return definition.IsHidden ? secretIcon : lockedIcon;
    }

    public Sprite UnlockedIcon => unlockedIcon;
}
