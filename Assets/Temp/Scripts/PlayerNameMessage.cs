using TMPro;
using UnityEngine;

/// <summary>
/// 告知の文字のうち、名前の目印（USERNAME など）をプレイヤーの名前に置き換えて表示する。
/// 置き換える前の文字を覚えておくため、何度でも別の名前に置き換えられる。
/// </summary>
public class PlayerNameMessage : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Tooltip("文字の中で、名前に置き換える目印")]
    [SerializeField] private string placeholder = "USERNAME";

    string template;

    public GameObject Message => gameObject;

    public void SetName(string playerName)
    {
        if (template == null)
            template = label.text;
        label.text = template.Replace(placeholder, playerName);
    }
}
