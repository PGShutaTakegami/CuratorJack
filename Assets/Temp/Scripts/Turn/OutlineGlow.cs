using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 1 人のモデルの縁を光らせる。縁のマテリアルを各レンダラーの最後に足し、Detach で足したマテリアルだけを外す。
/// 同じモデルに別の縁（降りたときの赤とシールドの緑など）が付いていても、互いに消し合わない。
/// 角ばったメッシュは法線で広げると縁が途切れるため、中心から外へ広げる。
/// </summary>
public sealed class OutlineGlow
{
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int RadialId = Shader.PropertyToID("_Radial");
    static readonly int CenterId = Shader.PropertyToID("_Center");

    readonly Dictionary<Renderer, Material> glowMaterials = new Dictionary<Renderer, Material>();

    public OutlineGlow(Transform body, Material outlineMaterial, string[] boxyRendererNames)
    {
        foreach (Renderer target in body.GetComponentsInChildren<Renderer>())
        {
            if (!IsModel(target))
                continue;

            var glow = new Material(outlineMaterial);
            glow.SetFloat(RadialId, IsBoxy(target, boxyRendererNames) ? 1f : 0f);
            glowMaterials[target] = glow;

            Material[] materials = target.sharedMaterials;
            var withOutline = new Material[materials.Length + 1];
            materials.CopyTo(withOutline, 0);
            withOutline[materials.Length] = glow;
            target.sharedMaterials = withOutline;
        }
    }

    /// <summary>明滅の明るさ（0〜1）。elapsed 秒のときの、pulseSeconds で 1 往復する波</summary>
    public static float Pulse(float elapsed, float pulseSeconds)
    {
        return 0.5f - 0.5f * Mathf.Cos(elapsed / pulseSeconds * 2f * Mathf.PI);
    }

    /// <summary>縁の色を変え、動いても中心から広げられるよう中心を追いかける</summary>
    public void SetColor(Color color)
    {
        foreach (KeyValuePair<Renderer, Material> pair in glowMaterials)
        {
            if (pair.Key == null)
                continue;
            pair.Value.SetColor(OutlineColorId, color);
            pair.Value.SetVector(CenterId, pair.Key.bounds.center);
        }
    }

    public void Detach()
    {
        foreach (KeyValuePair<Renderer, Material> pair in glowMaterials)
        {
            if (pair.Key != null)
                pair.Key.sharedMaterials = System.Array.FindAll(pair.Key.sharedMaterials, material => material != pair.Value);
            Object.Destroy(pair.Value);
        }
        glowMaterials.Clear();
    }

    static bool IsBoxy(Renderer target, string[] boxyRendererNames)
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
