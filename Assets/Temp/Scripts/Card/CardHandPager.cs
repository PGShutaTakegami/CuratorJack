using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 広げた手札が複数ページになるときに、ページを切り替える操作と今のページを表示する。
/// 左右のボタン・マウスホイール・←/→（A/D）キーでページを切り替える。最初・最後のページでは、その先へのボタンを隠す。
/// ボタンを手札の子に置くと手札を閉じる操作になるため、手札の外に置く。
/// </summary>
public class CardHandPager : MonoBehaviour
{
    [SerializeField] private CardHandView view;

    [Tooltip("ページが複数あるときだけ表示する部分（ボタンとページ番号の親）")]
    [SerializeField] private GameObject root;

    [SerializeField] private Button previousButton;

    [SerializeField] private Button nextButton;

    [SerializeField] private TMP_Text pageLabel;

    void Awake()
    {
        previousButton.onClick.AddListener(ShowPrevious);
        nextButton.onClick.AddListener(ShowNext);
        root.SetActive(false);
    }

    void OnDestroy()
    {
        previousButton.onClick.RemoveListener(ShowPrevious);
        nextButton.onClick.RemoveListener(ShowNext);
    }

    void ShowPrevious() => view.ShowSpreadPage(view.SpreadPage - 1);

    void ShowNext() => view.ShowSpreadPage(view.SpreadPage + 1);

    void Update()
    {
        int pageCount = view.SpreadPageCount;
        bool visible = view.IsSpread && pageCount > 1;
        if (root.activeSelf != visible)
            root.SetActive(visible);
        if (!visible)
            return;

        int page = view.SpreadPage;
        previousButton.gameObject.SetActive(page > 0);
        nextButton.gameObject.SetActive(page < pageCount - 1);
        pageLabel.text = $"{page + 1} / {pageCount}";

        int direction = InputDirection();
        if (direction != 0)
            view.ShowSpreadPage(page + direction);
    }

    /// <summary>このフレームのページ送りの向き（前へ -1、次へ +1、なければ 0）</summary>
    static int InputDirection()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                return -1;
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                return 1;
        }

        Mouse mouse = Mouse.current;
        float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
        if (wheel > 0f)
            return -1;
        if (wheel < 0f)
            return 1;
        return 0;
    }
}
