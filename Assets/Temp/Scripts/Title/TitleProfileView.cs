using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイトル左上のプロフィール。白い四角（自分のアイコン。押すとお絵かき画面を開く）と、その右の名前の入力欄。
/// 編集中（お絵かき中・名前の入力中）はタイトルの選択操作を止めるために IsEditing を返す。
/// </summary>
public class TitleProfileView : MonoBehaviour
{
    [SerializeField] Button iconButton;
    [SerializeField] RawImage iconImage;
    [SerializeField] TMP_InputField nameField;
    [SerializeField] IconPainterWindow painter;

    Texture2D shownIcon;
    int lastEditingFrame = -10;

    /// <summary>編集中か。編集を終えたフレームの次のフレームまでは編集中として扱う（決定のキーやクリックがタイトルに伝わらないように）</summary>
    public bool IsEditing => IsEditingNow || Time.frameCount - lastEditingFrame <= 1;

    bool IsEditingNow => painter.IsOpen || nameField.isFocused;

    void Awake()
    {
        iconButton.onClick.AddListener(painter.Open);
    }

    void OnEnable()
    {
        PlayerIconStore.Changed += ShowIcon;
        ShowIcon();
    }

    void OnDisable()
    {
        PlayerIconStore.Changed -= ShowIcon;
    }

    void OnDestroy()
    {
        if (shownIcon != null)
            Destroy(shownIcon);
    }

    void Update()
    {
        if (IsEditingNow)
            lastEditingFrame = Time.frameCount;
    }

    void ShowIcon()
    {
        if (shownIcon != null)
            Destroy(shownIcon);

        shownIcon = PlayerIconStore.CreateTexture(PlayerIconStore.Current);
        iconImage.texture = shownIcon;
        iconImage.enabled = shownIcon != null;
    }
}
