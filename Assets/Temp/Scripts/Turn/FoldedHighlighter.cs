using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// オークションで降りたプレイヤーのモデルの縁を赤く光らせ（ゆっくり明滅させ）、オークションが終わったら消す。
/// 自分が降りたときは、カメラが自分の頭の中にあり縁が視界をふさぐため光らせない。
/// </summary>
public class FoldedHighlighter : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [Tooltip("オークションの始まり・終わりを知らせる進行")]
    [SerializeField] private TurnFlowController turnFlow;

    [Tooltip("縁の描画に使うマテリアル（CJ/Outline）")]
    [SerializeField] private Material outlineMaterial;

    [ColorUsage(false, true)]
    [SerializeField] private Color glowColor = new Color(1f, 0.15f, 0.1f);

    [Tooltip("明滅の明るさの範囲（色の倍率。1 を超えると白く飛ぶ）")]
    [SerializeField] private Vector2 intensityRange = new Vector2(0.9f, 1.6f);

    [Tooltip("明滅 1 往復の秒数")]
    [SerializeField] private float pulseSeconds = 1.6f;

    [Tooltip("角ばったメッシュの名前。法線で広げると縁が途切れるため、中心から外へ広げる")]
    [SerializeField] private string[] boxyRendererNames = { "Monitor_Case", "Monitor_Screen" };

    readonly Dictionary<int, OutlineGlow> glows = new Dictionary<int, OutlineGlow>();
    AuctionModel auction;
    float elapsed;

    void OnEnable()
    {
        turnFlow.AuctionStarted += HandleAuctionStarted;
        turnFlow.AuctionFinished += HandleAuctionFinished;
    }

    void OnDisable()
    {
        turnFlow.AuctionStarted -= HandleAuctionStarted;
        turnFlow.AuctionFinished -= HandleAuctionFinished;
        HandleAuctionFinished();
    }

    void HandleAuctionStarted(AuctionModel started)
    {
        HandleAuctionFinished();
        auction = started;
        auction.Folded += Show;
        elapsed = 0f;
    }

    void HandleAuctionFinished()
    {
        if (auction != null)
            auction.Folded -= Show;
        auction = null;

        foreach (OutlineGlow glow in glows.Values)
            glow.Detach();
        glows.Clear();
    }

    void Show(int playerNumber)
    {
        if (playerNumber == registry.LocalPlayerNumber || glows.ContainsKey(playerNumber) || !registry.TryGet(playerNumber, out Transform body))
            return;

        glows[playerNumber] = new OutlineGlow(body, outlineMaterial, boxyRendererNames);
        UpdateGlow();
    }

    void Update()
    {
        if (glows.Count == 0)
            return;

        elapsed += Time.deltaTime;
        UpdateGlow();
    }

    void UpdateGlow()
    {
        Color color = glowColor * Mathf.Lerp(intensityRange.x, intensityRange.y, OutlineGlow.Pulse(elapsed, pulseSeconds));
        foreach (OutlineGlow glow in glows.Values)
            glow.SetColor(color);
    }
}
