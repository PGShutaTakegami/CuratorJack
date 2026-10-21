using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// メニューのボタンの押せる / 押せないを切り替え、押せないときは画像と文字を灰色にする。
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButton : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Tooltip("押せないときに画像と文字へ掛ける色")]
    [SerializeField] private Color disabledTint = new Color(0.45f, 0.45f, 0.45f, 1f);

    Button button;
    Image image;
    Color imageColor;
    Color labelColor;

    public Button Button
    {
        get
        {
            Cache();
            return button;
        }
    }

    public void SetAvailable(bool available)
    {
        Cache();
        button.interactable = available;
        image.color = available ? imageColor : imageColor * disabledTint;
        if (label != null)
            label.color = available ? labelColor : labelColor * disabledTint;
    }

    void Cache()
    {
        if (button != null)
            return;

        button = GetComponent<Button>();
        image = GetComponent<Image>();
        imageColor = image.color;
        if (label != null)
            labelColor = label.color;
    }
}
