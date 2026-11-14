using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// カード一覧の「アイテム」ページの 1 つ。カードと同じ形の下地にアイテムのアイコンを置き、上の帯に名前、下半分に説明を出す（見るだけで操作はできない）。
/// </summary>
public class ItemCatalogEntry : MonoBehaviour
{
    [SerializeField] private Image icon;

    [Tooltip("下地の上に重ねる、アイテム名と説明")]
    [SerializeField] private CardFaceLabels labels;

    public void Bind(ItemDefinition item)
    {
        icon.sprite = item.Icon;
        icon.enabled = item.Icon != null;
        labels.Show(item.DisplayName, item.Description);
    }
}
