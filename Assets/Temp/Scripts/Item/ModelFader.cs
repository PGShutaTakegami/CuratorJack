using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 3D モデル（URP Lit）の見た目の濃さを変える。
/// 薄くしている間だけ半透明にしたマテリアルの複製に差し替え、濃さが 1 に戻ったら元のマテリアルへ戻す（不透明の描画順のまま置いておくため）。
/// </summary>
public sealed class ModelFader
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    readonly Renderer[] renderers;
    readonly Material[][] originals;
    Material[][] faded;

    public ModelFader(GameObject root)
    {
        renderers = root.GetComponentsInChildren<Renderer>(true);
        originals = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
            originals[i] = renderers[i].sharedMaterials;
    }

    /// <summary>濃さ（0 で見えない、1 で元の見た目）</summary>
    public void SetAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        if (alpha >= 1f)
        {
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterials = originals[i];
            return;
        }

        faded ??= CreateFaded();
        for (int i = 0; i < renderers.Length; i++)
        {
            for (int m = 0; m < faded[i].Length; m++)
            {
                Material original = originals[i][m];
                if (original == null || !original.HasProperty(BaseColor))
                    continue;
                Color color = original.GetColor(BaseColor);
                color.a *= alpha;
                faded[i][m].SetColor(BaseColor, color);
            }
            renderers[i].sharedMaterials = faded[i];
        }
    }

    /// <summary>作った複製を消す（モデルを消すときに呼ぶ）</summary>
    public void Dispose()
    {
        if (faded == null)
            return;

        foreach (Material[] materials in faded)
        {
            foreach (Material material in materials)
            {
                if (material != null)
                    Object.Destroy(material);
            }
        }
        faded = null;
    }

    Material[][] CreateFaded()
    {
        var created = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            created[i] = new Material[originals[i].Length];
            for (int m = 0; m < originals[i].Length; m++)
            {
                if (originals[i][m] == null)
                    continue;
                created[i][m] = new Material(originals[i][m]);
                MakeTransparent(created[i][m]);
            }
        }
        return created;
    }

    static void MakeTransparent(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
