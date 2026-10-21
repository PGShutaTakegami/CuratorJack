using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 最高レアリティの商品が出品されたときに、付いているスペシャルカードを明かす画面の演出。
/// 下地のグレーのカードを大きく表示し、ディゾルブでスペシャルカードへ変えてから、フェードで消す。
/// </summary>
public class SpecialCardReveal : MonoBehaviour
{
    static readonly int ProgressId = Shader.PropertyToID("_Progress");

    [SerializeField] private CanvasGroup group;

    [Tooltip("ディゾルブで現すスペシャルカード。下地のグレーのカードの上に重ねる")]
    [SerializeField] private Image specialImage;

    [Tooltip("ディゾルブのシェーダー（CJ/UIDissolve）を使うマテリアル。実行時は複製して使う")]
    [SerializeField] private Material dissolveMaterial;

    [SerializeField] private float appearSeconds = 0.25f;

    [Tooltip("下地のカードを出してから、ディゾルブを始めるまでの秒数")]
    [SerializeField] private float baseHoldSeconds = 1f;

    [SerializeField] private float dissolveSeconds = 0.8f;

    [Tooltip("スペシャルカードが出てから、フェードを始めるまでの秒数")]
    [SerializeField] private float specialHoldSeconds = 1f;

    [SerializeField] private float fadeSeconds = 0.4f;

    Material material;

    void Awake()
    {
        material = new Material(dissolveMaterial);
        specialImage.material = material;
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }

    /// <summary>スペシャルカードを明かす演出をして、消えるまで待つ。</summary>
    public IEnumerator Present(CardData special)
    {
        specialImage.sprite = special != null ? special.Parameter.CardSprite : null;
        material.SetFloat(ProgressId, 0f);

        yield return Tween(appearSeconds, t => group.alpha = t);
        yield return new WaitForSeconds(baseHoldSeconds);
        yield return Tween(dissolveSeconds, t => material.SetFloat(ProgressId, t));
        yield return new WaitForSeconds(specialHoldSeconds);
        yield return Tween(fadeSeconds, t => group.alpha = 1f - t);
    }

    static IEnumerator Tween(float seconds, System.Action<float> apply)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
        {
            apply(Mathf.SmoothStep(0f, 1f, elapsed / seconds));
            yield return null;
        }
        apply(1f);
    }
}
