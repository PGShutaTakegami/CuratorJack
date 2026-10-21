using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// リールを背景に 2 つの表記（オフ/オン）を切り替える表示。クリックで通知し、値が変わると
/// 今の表記を下へ巻き込み、新しい表記を上から巻き出す（円筒に沿って回るように縦に縮めて動かす）。
/// 値そのものは持ち主が決め、SetValue で反映する。
/// </summary>
public class ReelToggleView : MonoBehaviour
{
    [SerializeField] private Button button;

    [Tooltip("表記を 2 枚交互に使う。リールの外ははみ出さないようにマスクする")]
    [SerializeField] private TMP_Text labelA;

    [SerializeField] private TMP_Text labelB;

    [SerializeField] private string offText = "0";

    [SerializeField] private string onText = "+5";

    [SerializeField] private float rollSeconds = 0.35f;

    public event Action Clicked;

    TMP_Text current;
    TMP_Text next;
    bool value;
    float elapsed;
    bool isRolling;

    void Awake()
    {
        button.onClick.AddListener(() => Clicked?.Invoke());
        current = labelA;
        next = labelB;
        SetValue(false, false);
    }

    void OnDisable()
    {
        if (isRolling)
            FinishRoll();
    }

    /// <summary>値を反映する。animate なら変わったときだけ巻いて切り替える</summary>
    public void SetValue(bool on, bool animate)
    {
        if (current == null)
            return;

        if (isRolling)
            FinishRoll();

        bool changed = on != value;
        value = on;
        if (!animate || !changed || !isActiveAndEnabled)
        {
            current.text = Text(on);
            Place(current, 0f);
            next.gameObject.SetActive(false);
            return;
        }

        next.text = Text(on);
        next.gameObject.SetActive(true);
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

    string Text(bool on) => on ? onText : offText;

    void Roll(float t)
    {
        float eased = t * t * (3f - 2f * t);
        Place(current, eased);
        Place(next, eased - 1f);
    }

    void FinishRoll()
    {
        isRolling = false;
        (current, next) = (next, current);
        Place(current, 0f);
        next.gameObject.SetActive(false);
    }

    /// <summary>リールの円筒上の位置に置く。0 が正面、+1 が下へ巻き込まれた位置、-1 が上から出てくる前の位置</summary>
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
