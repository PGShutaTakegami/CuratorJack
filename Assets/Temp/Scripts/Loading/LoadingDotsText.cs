using TMPro;
using UnityEngine;

/// <summary>
/// 「Loading.」「Loading..」「Loading...」のように、末尾の点を一定間隔で増やして繰り返す。
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LoadingDotsText : MonoBehaviour
{
    [SerializeField] private string baseText = "Loading";

    [SerializeField] private int maxDots = 3;

    [Tooltip("点が 1 つ増えるまでの秒数")]
    [SerializeField] private float interval = 0.4f;

    TMP_Text target;
    float elapsed;
    int dots;

    void Awake()
    {
        target = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        elapsed = 0f;
        dots = 1;
        Apply();
    }

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < interval)
            return;

        elapsed = 0f;
        dots = dots % maxDots + 1;
        Apply();
    }

    void Apply()
    {
        target.text = baseText + new string('.', dots);
    }
}
