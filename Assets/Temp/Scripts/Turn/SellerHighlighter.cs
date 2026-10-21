using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 出品者のターンの間、出品者のモデルの縁を光らせる（ゆっくり明滅させる）。
/// 縁のマテリアルを各レンダラーの最後に足し、終わったら元のマテリアルへ戻す。
/// 自分が出品者のときは、カメラが自分の頭の中にあり縁が視界をふさぐため光らせない。
/// </summary>
public class SellerHighlighter : MonoBehaviour
{
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int RadialId = Shader.PropertyToID("_Radial");
    static readonly int CenterId = Shader.PropertyToID("_Center");

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("縁の描画に使うマテリアル（CJ/Outline）")]
    [SerializeField] private Material outlineMaterial;

    [ColorUsage(false, true)]
    [SerializeField] private Color glowColor = new Color(1f, 0.75f, 0.3f);

    [Tooltip("明滅の明るさの範囲（色の倍率。1 を超えると白く飛ぶ）")]
    [SerializeField] private Vector2 intensityRange = new Vector2(0.5f, 1f);

    [Tooltip("明滅 1 往復の秒数")]
    [SerializeField] private float pulseSeconds = 1.6f;

    [Tooltip("角ばったメッシュの名前。法線で広げると縁が途切れるため、中心から外へ広げる")]
    [SerializeField] private string[] boxyRendererNames = { "Monitor_Case", "Monitor_Screen" };

    readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    readonly Dictionary<Renderer, Material> glowMaterials = new Dictionary<Renderer, Material>();
    float elapsed;

    void OnDestroy()
    {
        Hide();
    }

    public void Show(int playerNumber)
    {
        Hide();
        if (playerNumber == registry.LocalPlayerNumber || !registry.TryGet(playerNumber, out Transform body))
            return;

        foreach (Renderer target in body.GetComponentsInChildren<Renderer>())
        {
            if (!IsModel(target))
                continue;

            var glow = new Material(outlineMaterial);
            glow.SetFloat(RadialId, IsBoxy(target) ? 1f : 0f);
            glowMaterials[target] = glow;

            Material[] materials = target.sharedMaterials;
            originalMaterials[target] = materials;
            var withOutline = new Material[materials.Length + 1];
            materials.CopyTo(withOutline, 0);
            withOutline[materials.Length] = glow;
            target.sharedMaterials = withOutline;
        }
        elapsed = 0f;
        UpdateGlow();
    }

    public void Hide()
    {
        foreach (KeyValuePair<Renderer, Material[]> pair in originalMaterials)
        {
            if (pair.Key != null)
                pair.Key.sharedMaterials = pair.Value;
        }
        originalMaterials.Clear();

        foreach (Material glow in glowMaterials.Values)
            Destroy(glow);
        glowMaterials.Clear();
    }

    void Update()
    {
        if (glowMaterials.Count == 0)
            return;

        elapsed += Time.deltaTime;
        UpdateGlow();
    }

    /// <summary>明るさを明滅させ、動いても中心から広げられるよう中心を追いかける</summary>
    void UpdateGlow()
    {
        float wave = 0.5f - 0.5f * Mathf.Cos(elapsed / pulseSeconds * 2f * Mathf.PI);
        Color color = glowColor * Mathf.Lerp(intensityRange.x, intensityRange.y, wave);
        foreach (KeyValuePair<Renderer, Material> pair in glowMaterials)
        {
            if (pair.Key == null)
                continue;
            pair.Value.SetColor(OutlineColorId, color);
            pair.Value.SetVector(CenterId, pair.Key.bounds.center);
        }
    }

    bool IsBoxy(Renderer target)
    {
        foreach (string boxyName in boxyRendererNames)
        {
            if (target.name == boxyName)
                return true;
        }
        return false;
    }

    /// <summary>体とモニターのメッシュだけを対象にする（文字や影だけのレンダラーは除く）</summary>
    static bool IsModel(Renderer target)
    {
        return target.enabled
               && target.shadowCastingMode != ShadowCastingMode.ShadowsOnly
               && (target is SkinnedMeshRenderer || target is MeshRenderer)
               && target.GetComponent<TMPro.TMP_Text>() == null;
    }
}
