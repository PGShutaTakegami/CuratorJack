using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 実績を解除したら、画面右上の少し下に右からスライドして出し、しばらくしたら右へ戻して消す。
/// 続けて解除したときは順に 1 件ずつ出す。ポーズ中も進む（timeScale に左右されない）。
/// 起動時に Resources から生成してシーンをまたいで常駐させる。
/// </summary>
public class AchievementToast : MonoBehaviour
{
    const string ResourcePath = "AchievementToast";
    const string HeaderKey = "achievement.toast.header";

    [SerializeField] private RectTransform panel;

    [SerializeField] private Image icon;

    [SerializeField] private TMP_Text header;

    [SerializeField] private TMP_Text title;

    [Tooltip("画面の上端から通知の上端までの距離")]
    [SerializeField] private float topOffset = 130f;

    [Tooltip("画面の右端から通知の右端までの距離")]
    [SerializeField] private float rightMargin = 32f;

    [SerializeField, Min(0.01f)] private float slideSeconds = 0.35f;

    [SerializeField, Min(0f)] private float holdSeconds = 3f;

    [Tooltip("続けて出すときの間")]
    [SerializeField, Min(0f)] private float gapSeconds = 0.2f;

    static AchievementToast instance;

    readonly Queue<AchievementDefinition> pending = new Queue<AchievementDefinition>();
    Coroutine showing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (instance != null)
            return;

        AchievementToast prefab = Resources.Load<AchievementToast>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"{nameof(AchievementToast)}: Resources/{ResourcePath} が見つかりません。");
            return;
        }

        instance = Instantiate(prefab);
        instance.name = nameof(AchievementToast);
        DontDestroyOnLoad(instance.gameObject);
    }

    void Awake()
    {
        panel.anchoredPosition = HiddenPosition;
        panel.gameObject.SetActive(false);
        AchievementService.Unlocked += Enqueue;
    }

    void OnDestroy()
    {
        AchievementService.Unlocked -= Enqueue;
    }

    Vector2 HiddenPosition => new Vector2(panel.rect.width + rightMargin * 2f, -topOffset);

    Vector2 ShownPosition => new Vector2(-rightMargin, -topOffset);

    void Enqueue(AchievementDefinition definition)
    {
        pending.Enqueue(definition);
        if (showing == null)
            showing = StartCoroutine(ShowPending());
    }

    IEnumerator ShowPending()
    {
        while (pending.Count > 0)
        {
            Present(pending.Dequeue());
            yield return Slide(HiddenPosition, ShownPosition, EaseOutCubic);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Slide(ShownPosition, HiddenPosition, EaseInCubic);
            panel.gameObject.SetActive(false);
            if (pending.Count > 0)
                yield return new WaitForSecondsRealtime(gapSeconds);
        }
        showing = null;
    }

    void Present(AchievementDefinition definition)
    {
        AchievementCatalog catalog = AchievementService.Catalog;
        icon.sprite = catalog.UnlockedIcon;
        header.text = catalog.Text(HeaderKey);
        title.text = catalog.NameOf(definition);
        panel.anchoredPosition = HiddenPosition;
        panel.gameObject.SetActive(true);
    }

    IEnumerator Slide(Vector2 from, Vector2 to, System.Func<float, float> ease)
    {
        for (float t = 0f; t < slideSeconds; t += Time.unscaledDeltaTime)
        {
            panel.anchoredPosition = Vector2.LerpUnclamped(from, to, ease(t / slideSeconds));
            yield return null;
        }
        panel.anchoredPosition = to;
    }

    static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    static float EaseInCubic(float t) => t * t * t;
}
