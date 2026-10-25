using TMPro;
using UnityEngine;

/// <summary>
/// 言語テーブルのキーの文言を TextMeshPro に表示し、表示言語が変わったら書き換える。
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private LanguageTable table;

    [Tooltip("table のキー")]
    [SerializeField] private string key;

    TMP_Text target;

    void Awake()
    {
        target = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        GameLanguage.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        GameLanguage.Changed -= Refresh;
    }

    void Refresh()
    {
        string text = table != null ? table.Get(key) : string.Empty;
        if (!string.IsNullOrEmpty(text))
            target.text = text;
    }
}
