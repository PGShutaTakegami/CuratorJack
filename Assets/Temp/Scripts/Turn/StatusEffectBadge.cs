using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 自分にかかっている持続効果 1 つ分の表示。効果カードの画像・カード名・残り（回数か額）を並べる。
/// </summary>
public class StatusEffectBadge : MonoBehaviour
{
    [SerializeField] private Image cardImage;

    [SerializeField] private TMP_Text nameLabel;

    [SerializeField] private TMP_Text valueLabel;

    public void Bind(Sprite card, string cardName, string value)
    {
        cardImage.sprite = card;
        cardImage.enabled = card != null;
        nameLabel.text = cardName;
        valueLabel.text = value;
    }
}
