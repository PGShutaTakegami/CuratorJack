using TMPro;
using UnityEngine;

/// <summary>
/// カードの絵の上に重ねる文字。上の帯にカード名、下半分にカードの能力（説明）を出す。
/// カードと同じ大きさに広げて使い、文字はカードの大きさに合わせて縮む。能力が空なら下半分は出さない。
/// </summary>
public class CardFaceLabels : MonoBehaviour
{
    [Tooltip("全体の濃さを変える（ディゾルブに合わせて出すときに使う）")]
    [SerializeField] private CanvasGroup group;

    [SerializeField] private TMP_Text nameLabel;

    [Tooltip("能力の文字と背景の親。能力が空なら隠す")]
    [SerializeField] private GameObject abilityPanel;

    [SerializeField] private TMP_Text abilityLabel;

    public float Alpha
    {
        get => group.alpha;
        set => group.alpha = value;
    }

    public void Show(string cardName, string ability)
    {
        nameLabel.text = cardName ?? string.Empty;
        abilityLabel.text = ability ?? string.Empty;
        abilityPanel.SetActive(!string.IsNullOrEmpty(ability));
    }

    public void Show(CardData card) => Show(card != null ? card.Parameter.CardName : null, card != null ? card.Parameter.Discription : null);
}
