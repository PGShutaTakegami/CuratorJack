using TMPro;
using UnityEngine;

/// <summary>
/// 3D 空間に置く文字（と画像）。通常は画面を映しているカメラの方を向き、向きを指定して置くこともできる。
/// </summary>
public class WorldLabel : MonoBehaviour
{
    TextMeshPro text;
    SpriteRenderer image;
    bool facesCamera = true;

    public static WorldLabel Create(string name, Transform parent, TMP_FontAsset font, float fontSize, Color color)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        WorldLabel label = root.AddComponent<WorldLabel>();

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(root.transform, false);
        label.text = textObject.AddComponent<TextMeshPro>();
        label.text.font = font;
        label.text.fontSize = fontSize;
        label.text.color = color;
        label.text.alignment = TextAlignmentOptions.Center;
        label.text.textWrappingMode = TextWrappingModes.NoWrap;
        label.text.rectTransform.sizeDelta = new Vector2(4f, 1f);

        root.SetActive(false);
        return label;
    }

    /// <summary>文字の上に画像を表示する。画像の幅は width にそろえる。</summary>
    public void SetImage(Sprite sprite, float width, float gap)
    {
        if (image == null)
        {
            var imageObject = new GameObject("Image");
            imageObject.transform.SetParent(transform, false);
            image = imageObject.AddComponent<SpriteRenderer>();
        }

        image.sprite = sprite;
        image.gameObject.SetActive(sprite != null);
        if (sprite == null)
            return;

        float scale = width / sprite.bounds.size.x;
        image.transform.localScale = new Vector3(scale, scale, 1f);
        image.transform.localPosition = new Vector3(0f, gap + sprite.bounds.size.y * scale * 0.5f, 0f);
    }

    /// <summary>カメラの方を向けて表示する</summary>
    public void Show(Vector3 position, string message)
    {
        facesCamera = true;
        transform.position = position;
        text.text = message;
        gameObject.SetActive(true);
        FaceCamera();
    }

    /// <summary>決まった向きで表示する（カメラの方へは向けない）</summary>
    public void Show(Vector3 position, Quaternion rotation, string message)
    {
        facesCamera = false;
        transform.SetPositionAndRotation(position, rotation);
        text.text = message;
        gameObject.SetActive(true);
    }

    /// <summary>文字の下端を位置の基準にする（下端を軸に起こしたり寝かせたりするときに使う）</summary>
    public void SetPivotBottom()
    {
        text.rectTransform.pivot = new Vector2(0.5f, 0f);
        text.alignment = TextAlignmentOptions.Bottom;
    }

    /// <summary>描画順を指定する。半透明のものより後に描いて隠れないようにするときに使う</summary>
    public void SetRenderQueue(int queue)
    {
        text.fontMaterial.renderQueue = queue;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>文字と画像の透明度（フェード用）</summary>
    public void SetAlpha(float alpha)
    {
        text.alpha = alpha;
        if (image != null)
            image.color = new Color(1f, 1f, 1f, alpha);
    }

    void LateUpdate()
    {
        if (facesCamera)
            FaceCamera();
    }

    void FaceCamera()
    {
        Camera viewer = Camera.main != null && Camera.main.isActiveAndEnabled ? Camera.main : (Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null);
        if (viewer != null)
            transform.rotation = Quaternion.LookRotation(transform.position - viewer.transform.position, viewer.transform.up);
    }
}
