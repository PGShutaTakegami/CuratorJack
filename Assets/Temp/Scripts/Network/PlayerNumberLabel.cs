using TMPro;
using UnityEngine;

/// <summary>
/// プレイヤーの頭上にプレイヤー番号を表示し、常にカメラへ向ける。
/// モデルのルートは拡大されているため、ラベルは親を持たない独立オブジェクトとして生成する。
/// </summary>
[RequireComponent(typeof(NetworkSessionPlayer))]
public class PlayerNumberLabel : MonoBehaviour
{
    [SerializeField] private Transform headAnchor;

    [SerializeField] private Vector3 offset = new Vector3(0f, 0.9f, 0f);

    [SerializeField] private float fontSize = 6f;

    [SerializeField] private Color color = new Color(1f, 0.85f, 0.35f, 1f);

    NetworkSessionPlayer player;
    TextMeshPro label;

    void Awake()
    {
        player = GetComponent<NetworkSessionPlayer>();
        label = CreateLabel();
        player.PlayerNumberChanged += ApplyNumber;
        ApplyNumber(player.PlayerNumber);
    }

    void OnDestroy()
    {
        player.PlayerNumberChanged -= ApplyNumber;
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
            label.text = playerNumber.ToString();
    }

    TextMeshPro CreateLabel()
    {
        var go = new GameObject($"{name}_NumberLabel");
        var text = go.AddComponent<TextMeshPro>();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.rectTransform.sizeDelta = new Vector2(4f, 2f);
        return text;
    }
}
