using TMPro;
using UnityEngine;

/// <summary>
/// プレイヤーの頭上に名前（未入力ならプレイヤー番号）を表示し、常にカメラへ向ける。
/// モデルのルートは拡大されているため、ラベルは親を持たない独立オブジェクトとして生成する。
/// </summary>
[RequireComponent(typeof(NetworkSessionPlayer))]
public class PlayerNumberLabel : MonoBehaviour
{
    [SerializeField] private Transform headAnchor;

    [SerializeField] private Vector3 offset = new Vector3(0f, 0.9f, 0f);

    [SerializeField] private float fontSize = 6f;

    [SerializeField] private Color color = new Color(1f, 0.85f, 0.35f, 1f);

    [Tooltip("名前に日本語を使えるフォント（未設定なら TMP の既定フォント）")]
    [SerializeField] private TMP_FontAsset font;

    NetworkSessionPlayer player;
    TextMeshPro label;

    void Awake()
    {
        player = GetComponent<NetworkSessionPlayer>();
        label = CreateLabel();
        player.PlayerNumberChanged += ApplyNumber;
        player.DisplayNameChanged += ApplyName;
        ApplyNumber(player.PlayerNumber);
    }

    void OnDestroy()
    {
        player.PlayerNumberChanged -= ApplyNumber;
        player.DisplayNameChanged -= ApplyName;
        if (label != null)
            Destroy(label.gameObject);
    }

    void LateUpdate()
    {
        if (label == null)
            return;

        Transform anchor = headAnchor != null ? headAnchor : transform;
        label.transform.position = anchor.position + offset;

        Camera cam = Camera.main != null ? Camera.main : (Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null);
        if (cam != null)
            label.transform.rotation = cam.transform.rotation;
    }

    void ApplyNumber(int playerNumber)
    {
        bool assigned = playerNumber != NetworkSessionPlayer.UnassignedNumber;
        label.gameObject.SetActive(assigned);
        if (assigned)
            label.text = LabelText(playerNumber, player.DisplayName);
    }

    void ApplyName(string displayName)
    {
        ApplyNumber(player.PlayerNumber);
    }

    /// <summary>入力された名前だけを出し、なければ「Player + 番号」にする。名前はタグとして解釈させない</summary>
    static string LabelText(int playerNumber, string displayName)
    {
        return string.IsNullOrEmpty(displayName)
            ? PlayerNameDirectory.FallbackOf(playerNumber)
            : $"<noparse>{displayName}</noparse>";
    }

    TextMeshPro CreateLabel()
    {
        var go = new GameObject($"{name}_NumberLabel");
        var text = go.AddComponent<TextMeshPro>();
        if (font != null)
            text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(4f, 2f);
        return text;
    }
}
