using UnityEngine;

/// <summary>
/// 出品者のターンの間、出品者のモデルの縁を光らせる（ゆっくり明滅させる）。
/// 自分が出品者のときは、カメラが自分の頭の中にあり縁が視界をふさぐため光らせない。
/// </summary>
public class SellerHighlighter : MonoBehaviour
{
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

    OutlineGlow glow;
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

        glow = new OutlineGlow(body, outlineMaterial, boxyRendererNames);
        elapsed = 0f;
        UpdateGlow();
    }

    public void Hide()
    {
        glow?.Detach();
        glow = null;
    }

    void Update()
    {
        if (glow == null)
            return;

        elapsed += Time.deltaTime;
        UpdateGlow();
    }

    void UpdateGlow()
    {
        glow.SetColor(glowColor * Mathf.Lerp(intensityRange.x, intensityRange.y, OutlineGlow.Pulse(elapsed, pulseSeconds)));
    }
}
