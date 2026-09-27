using UnityEngine;

/// <summary>
/// モニター画面の画像を進行度(0〜1)に合わせてクロスフェードで切り替える。
/// 切り替え元・先のどちらも既定画像でない場合は、前半で既定画像へ、後半で切り替え先へフェードする。
/// </summary>
public class MonitorScreenFader
{
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    static readonly int SubMapId = Shader.PropertyToID("_SubMap");
    static readonly int SubMapStId = Shader.PropertyToID("_SubMap_ST");
    static readonly int BlendId = Shader.PropertyToID("_Blend");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    readonly Renderer screenRenderer;
    readonly Sprite defaultSprite;
    readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

    Sprite fromSprite;
    Sprite toSprite;
    bool viaDefault;

    public MonitorScreenFader(Renderer screenRenderer, Sprite defaultSprite)
    {
        this.screenRenderer = screenRenderer;
        this.defaultSprite = defaultSprite;
    }

    /// <summary>
    /// フェードせずに即座に表示する
    /// </summary>
    public void ShowImmediate(Sprite sprite)
    {
        fromSprite = sprite;
        toSprite = sprite;
        viaDefault = false;
        SetMaps(sprite, sprite, 0f);
    }

    /// <summary>
    /// 現在の表示から target への切り替えを開始する（進行度 0）
    /// </summary>
    public void BeginTransition(Sprite target)
    {
        toSprite = target;
        viaDefault = fromSprite != defaultSprite && target != defaultSprite;
        SetProgress(0f);
    }

    /// <summary>
    /// 切り替えの進行度(0〜1)を反映する
    /// </summary>
    public void SetProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (!viaDefault)
        {
            SetMaps(fromSprite, toSprite, progress);
            return;
        }

        if (progress < 0.5f)
            SetMaps(fromSprite, defaultSprite, progress * 2f);
        else
            SetMaps(defaultSprite, toSprite, (progress - 0.5f) * 2f);
    }

    /// <summary>
    /// 切り替えを完了し、切り替え先を表示する
    /// </summary>
    public void CompleteTransition()
    {
        ShowImmediate(toSprite);
    }

    void SetMaps(Sprite baseSprite, Sprite subSprite, float blend)
    {
        SetMap(BaseMapId, BaseMapStId, baseSprite);
        SetMap(SubMapId, SubMapStId, subSprite);
        propertyBlock.SetFloat(BlendId, blend);
        propertyBlock.SetColor(BaseColorId, Color.white);

        if (screenRenderer != null)
            screenRenderer.SetPropertyBlock(propertyBlock);
    }

    void SetMap(int textureId, int stId, Sprite sprite)
    {
        if (sprite == null)
        {
            propertyBlock.SetTexture(textureId, Texture2D.blackTexture);
            propertyBlock.SetVector(stId, new Vector4(1f, 1f, 0f, 0f));
            return;
        }

        Texture2D texture = sprite.texture;
        Rect rect = sprite.textureRect;
        propertyBlock.SetTexture(textureId, texture);
        propertyBlock.SetVector(stId, new Vector4(
            rect.width / texture.width,
            rect.height / texture.height,
            rect.x / texture.width,
            rect.y / texture.height));
    }
}
