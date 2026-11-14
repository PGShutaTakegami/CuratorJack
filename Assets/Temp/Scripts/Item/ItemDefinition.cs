using UnityEngine;

/// <summary>
/// アイテム 1 種類分のデータ（種類・名前・説明・アイコン・机に置くモデル）。使ったときの効果は種類で決まる（ItemEffectResolver）。
/// </summary>
[CreateAssetMenu(fileName = "Item", menuName = "CJ/ItemDefinition")]
public class ItemDefinition : ScriptableObject, IActivatable
{
    [Tooltip("オフにするとゲームに出てこない（落札でもらえる候補・インフォメーション・チュートリアルから外れる）")]
    [SerializeField] private bool active = true;

    [SerializeField] private string id;

    [SerializeField] private ItemKind kind;

    [SerializeField] private string displayName;

    [TextArea]
    [SerializeField] private string description;

    [Tooltip("マスに出すアイコン（無ければ名前を文字で出す）")]
    [SerializeField] private Sprite icon;

    [Tooltip("使ったときに使った人の手元の机に置くモデル（寝かせた形で、長い方向が forward。無ければ置かない）")]
    [SerializeField] private GameObject tableModel;

    [Header("翻訳（空の言語は LanguageCodes.Pick の順で代わりの言語を使う）")]
    [SerializeField] private string englishName;

    [TextArea]
    [SerializeField] private string englishDescription;

    [SerializeField] private string chineseSimplifiedName;

    [TextArea]
    [SerializeField] private string chineseSimplifiedDescription;

    [SerializeField] private string chineseTraditionalName;

    [TextArea]
    [SerializeField] private string chineseTraditionalDescription;

    public bool Active => active;

    public string Id => id;

    public ItemKind Kind => kind;

    /// <summary>使うときに相手のプレイヤーを選ぶか</summary>
    public bool NeedsTarget => kind == ItemKind.RemoteControl || kind == ItemKind.PowerPlug;

    /// <summary>相手に自分を選べるか</summary>
    public bool CanTargetSelf => kind == ItemKind.PowerPlug;

    /// <summary>自分の出品のオークション中にも使えるか（虫眼鏡は自分で付けた効果カードを見るだけになるため使えない）</summary>
    public bool CanUseOnOwnListing => kind != ItemKind.Magnifier;

    /// <summary>現在の表示言語の名前</summary>
    public string DisplayName => LanguageCodes.Pick(GameLanguage.Current, displayName, englishName, chineseSimplifiedName, chineseTraditionalName);

    /// <summary>現在の表示言語の説明</summary>
    public string Description => LanguageCodes.Pick(GameLanguage.Current, description, englishDescription, chineseSimplifiedDescription, chineseTraditionalDescription);

    public Sprite Icon => icon;

    public GameObject TableModel => tableModel;
}
