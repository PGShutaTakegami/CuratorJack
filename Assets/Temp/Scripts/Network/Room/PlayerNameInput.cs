using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// MultiPlay 画面の名前入力欄。入力した名前は部屋の作成・入室のどちらでもゲーム内に表示される。
/// 名前はモニターの告知にも出すため、告知のフォントにある文字だけを入力できる（保存されていた名前からも、ない文字は取り除く）。
/// </summary>
public class PlayerNameInput : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;

    [Tooltip("名前に使える文字を決めるフォント（モニターの告知に使う CJ フォント）")]
    [SerializeField] private TMP_FontAsset allowedFont;

    void Awake()
    {
        inputField.characterLimit = PlayerNameContext.MaxLength;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.onValidateInput = ValidateCharacter;

        string saved = PlayerNameContext.Current;
        string allowed = new string(saved.Where(IsAllowed).ToArray());
        if (allowed != saved)
            PlayerNameContext.Set(allowed);
        inputField.text = PlayerNameContext.Current;
    }

    void OnEnable()
    {
        inputField.onValueChanged.AddListener(PlayerNameContext.Set);
    }

    void OnDisable()
    {
        inputField.onValueChanged.RemoveListener(PlayerNameContext.Set);
    }

    char ValidateCharacter(string text, int index, char addedChar) => IsAllowed(addedChar) ? addedChar : '\0';

    /// <summary>フォントの元のファイルにある文字か（ダイナミックフォントのため、まだアトラスにない文字は元のファイルから探す）</summary>
    bool IsAllowed(char character) => allowedFont.HasCharacter(character, false, true);
}
