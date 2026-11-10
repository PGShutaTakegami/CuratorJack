using UnityEngine;

/// <summary>
/// アイテム 1 種類分のデータ（種類・名前・説明・アイコン・机に置くモデル）。使ったときの効果は種類で決まる（ItemEffectResolver）。
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

    [Tooltip("使ったときに使った人の手元の机に置くモデル（寝かせた形で、長い方向が forward。無ければ置かない）")]
    [SerializeField] private GameObject tableModel;

    public string Id => id;

    public ItemKind Kind => kind;

    /// <summary>使うときに相手のプレイヤーを選ぶか</summary>
    public bool NeedsTarget => kind == ItemKind.RemoteControl || kind == ItemKind.PowerPlug;

    /// <summary>相手に自分を選べるか</summary>
    public bool CanTargetSelf => kind == ItemKind.PowerPlug;

    /// <summary>自分の出品のオークション中にも使えるか（虫眼鏡は自分で付けた効果カードを見るだけになるため使えない）</summary>
    public bool CanUseOnOwnListing => kind != ItemKind.Magnifier;

    public string DisplayName => displayName;

    public string Description => description;

    public Sprite Icon => icon;

    public GameObject TableModel => tableModel;
}
