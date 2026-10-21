using TMPro;
using UnityEngine;

/// <summary>
/// リールの枠の中の表記を、巻いて切り替える。direction が +1 なら今の表記を下へ巻き込み新しい表記を上から、
/// -1 ならその逆（上へ巻き込み下から）、0 なら巻かずに切り替える。
/// 円筒に沿って回るように、縦に縮めながら上下へ動かす。
/// </summary>
public class ReelRoller : MonoBehaviour
{
    [Tooltip("表記を 2 枚交互に使う。枠の外ははみ出さないようにマスクする")]
    [SerializeField] private TMP_Text labelA;

    [SerializeField] private TMP_Text labelB;

    [SerializeField] private float rollSeconds = 0.25f;

    TMP_Text current;
    TMP_Text next;
    int rollDirection;
    float elapsed;
    bool isRolling;

    public string Text => Current.text;

    TMP_Text Current
    {
        get
        {
            if (current == null)
            {
                current = labelA;
                next = labelB;
                current.gameObject.SetActive(true);
                next.gameObject.SetActive(false);
            }
            return current;
        }
    }

    void OnDisable()
    {
        if (isRolling)
            FinishRoll();
    }

    public void Set(string text, int direction)
    {
        if (isRolling)
            FinishRoll();
        TMP_Text shown = Current;

        if (direction == 0 || !isActiveAndEnabled)
        {
            shown.text = text;
            Place(shown, 0f);
            next.gameObject.SetActive(false);
            return;
        }

        if (shown.text == text)
            return;

        next.text = text;
        next.gameObject.SetActive(true);
        rollDirection = direction > 0 ? 1 : -1;
        elapsed = 0f;
        isRolling = true;
        Roll(0f);
    }

    void Update()
    {
        if (!isRolling)
            return;

        elapsed += Time.deltaTime;
        float t = rollSeconds > 0f ? Mathf.Clamp01(elapsed / rollSeconds) : 1f;
        Roll(t);
        if (t >= 1f)
            FinishRoll();
    }

    void Roll(float t)
    {
        float eased = t * t * (3f - 2f * t);
        Place(current, eased * rollDirection);
        Place(next, (eased - 1f) * rollDirection);
    }

    void FinishRoll()
    {
        isRolling = false;
        (current, next) = (next, current);
        Place(current, 0f);
        next.gameObject.SetActive(false);
    }

    /// <summary>リールの円筒上の位置に置く。0 が正面、+1 が下へ巻き込まれた位置、-1 が上へ巻き込まれた位置</summary>
    void Place(TMP_Text label, float turn)
    {
        float angle = turn * Mathf.PI * 0.5f;
        float radius = ((RectTransform)transform).rect.height * 0.5f;
        float facing = Mathf.Cos(angle);

        label.rectTransform.anchoredPosition = new Vector2(0f, -Mathf.Sin(angle) * radius);
        label.rectTransform.localScale = new Vector3(1f, Mathf.Max(0f, facing), 1f);
        label.alpha = Mathf.Clamp01(facing);
    }
}
