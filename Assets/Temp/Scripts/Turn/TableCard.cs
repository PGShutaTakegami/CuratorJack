using UnityEngine;

/// <summary>
/// テーブルに置くカードのオブジェクト。出したときに透明からフェードで表示する。
/// 重ねたカードは後から置いたものほど上に描く（半透明の描画順を置いた順にそろえる）。
/// </summary>
public class TableCard : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Tooltip("カードの表面（半透明にできるマテリアルを使う）")]
    [SerializeField] private Renderer face;

    Material material;
    Color color;

    /// <summary>カードの縦の長さ（forward 方向）</summary>
    public float Length => face.transform.lossyScale.y;
    float fadeSeconds;
    float elapsed;

    /// <summary>layer 番目に置いたカードとして、fadeSeconds かけて表示する</summary>
    public void FadeIn(float seconds, int layer)
    {
        material = face.material;
        material.renderQueue = face.sharedMaterial.renderQueue + layer;
        color = material.GetColor(BaseColorId);
        fadeSeconds = seconds;
        elapsed = 0f;
        SetAlpha(seconds > 0f ? 0f : 1f);
    }

    void Update()
    {
        if (material == null || elapsed >= fadeSeconds)
            return;

        elapsed += Time.deltaTime;
        SetAlpha(Mathf.Clamp01(elapsed / fadeSeconds));
    }

    void SetAlpha(float alpha)
    {
        material.SetColor(BaseColorId, new Color(color.r, color.g, color.b, color.a * alpha));
    }

    void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
