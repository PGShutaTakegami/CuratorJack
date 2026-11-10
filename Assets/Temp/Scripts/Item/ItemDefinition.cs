using UnityEngine;

/// <summary>
/// アイテム 1 種類分のデータ（種類・名前・説明・アイコン）。使ったときの効果は種類で決まる（ItemEffectResolver）。
/// </summary>
[CreateAssetMenu(fileName = "Item", menuName = "CJ/ItemDefinition")]
public class ItemDefinition : ScriptableObject
{
    [SerializeField] private string id;

    [SerializeField] private ItemKind kind;

    [SerializeField] private string displayName;

    [TextArea]
    [SerializeField] private string description;

    [Tooltip("マスに出すアイコン（無ければ名前を文字で出す）")]
    [SerializeField] private Sprite icon;

    public string Id => id;

    public ItemKind Kind => kind;

    /// <summary>使うときに相手のプレイヤーを選ぶか</summary>
    public bool NeedsTarget => kind == ItemKind.RemoteControl;

    public string DisplayName => displayName;

    public string Description => description;

    public Sprite Icon => icon;
}
