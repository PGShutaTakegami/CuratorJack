using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 持続効果（シールド・マヒなど）が残っている生きている人を光らせ、効果が無くなったら消す。
/// ほかの人はモデルの縁を光らせ（稲妻があれば上半身を囲む円に沿っても走らせる）、
/// 自分はカメラが頭の中にあり体が見えないため、効果を受けた瞬間だけ画面の端を一瞬光らせる（稲妻があれば画面の端にも走らせる）。
/// flashOnDecrease のときは、効果が減った瞬間（シールドがダメージを肩代わりしたときなど）にも一瞬光らせる。
/// 同じ人に降りたときの赤い縁が付いても、互いの縁は消し合わない（OutlineGlow）。
/// </summary>
public class StatusAuraPresenter : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("光らせる持続効果")]
    [SerializeField] private PlayerStatus status;

    [Tooltip("縁の描画に使うマテリアル（CJ/Outline）")]
    [SerializeField] private Material outlineMaterial;

    [ColorUsage(false, true)]
    [SerializeField] private Color glowColor = new Color(0.2f, 1f, 0.35f);

    [Tooltip("明るさの範囲（色の倍率。1 を超えると白く飛ぶ）")]
    [SerializeField] private Vector2 intensityRange = new Vector2(0.9f, 1.6f);

    [Tooltip("明滅 1 往復の秒数")]
    [SerializeField] private float pulseSeconds = 1.6f;

    [Tooltip("ゆっくり明滅する代わりに、電気のように不規則にちらつかせる")]
    [SerializeField] private bool flicker;

    [Tooltip("ちらつきの明るさを変える間隔（秒）")]
    [SerializeField] private float flickerInterval = 0.05f;

    [Tooltip("角ばったメッシュの名前。法線で広げると縁が途切れるため、中心から外へ広げる")]
    [SerializeField] private string[] boxyRendererNames = { "Monitor_Case", "Monitor_Screen" };

    [Tooltip("自分が効果を受けた瞬間に光らせる画面の端")]
    [SerializeField] private ScreenEdgeGlow localEdge;

    [Tooltip("自分が効果を受けた瞬間に画面の端に走らせる稲妻（無ければ走らせない）")]
    [SerializeField] private ElectricBolts screenBolts;

    [Tooltip("自分が効果を受けたときに画面の端を光らせておく秒数")]
    [SerializeField] private float localFlashSeconds = 0.8f;

    [Tooltip("自分の効果が減った瞬間（シールドがダメージを肩代わりしたときなど）にも光らせる")]
    [SerializeField] private bool flashOnDecrease;

    [Tooltip("ほかの人の体の周りに走らせる稲妻（無ければ走らせない）")]
    [SerializeField] private ElectricBolts bodyBolts;

    [Tooltip("ほかの人の体を画面に映すときの、自分のカメラ")]
    [SerializeField] private TableInspectCamera viewer;

    [Tooltip("体の周りの稲妻を走らせる円の半径（m）")]
    [SerializeField] private float bodyRadius = 1.1f;

    [Tooltip("円の中心を頭から下げる量（m）。上半身の中心に合わせる")]
    [SerializeField] private float bodyCenterDrop = 0.55f;

    readonly Dictionary<int, OutlineGlow> glows = new Dictionary<int, OutlineGlow>();
    readonly Dictionary<int, Transform> heads = new Dictionary<int, Transform>();
    readonly List<int> removing = new List<int>();
    readonly List<Rect> screenAreas = new List<Rect>();
    readonly List<Rect> bodyAreas = new List<Rect>();
    float elapsed;
    float flickerTimer;
    float flickerIntensity = 1f;
    int watchedLocal = PlayerRegistry.NoPlayer;
    int lastLocalValue;
    float localFlashTimer;

    void OnDisable()
    {
        foreach (OutlineGlow glow in glows.Values)
            glow.Detach();
        glows.Clear();
        heads.Clear();
        watchedLocal = PlayerRegistry.NoPlayer;
        localFlashTimer = 0f;
        localEdge.Visible = false;
        if (screenBolts != null)
            screenBolts.SetAreas(System.Array.Empty<Rect>());
        if (bodyBolts != null)
            bodyBolts.SetAreas(System.Array.Empty<Rect>());
    }

    void Update()
    {
        int local = registry.LocalPlayerNumber;
        foreach (PlayerState player in game.Players)
        {
            bool has = player.IsAlive && player.StatusOf(status) > 0;
            if (player.PlayerNumber != local && has && !glows.ContainsKey(player.PlayerNumber) && registry.TryGet(player.PlayerNumber, out Transform body))
                glows[player.PlayerNumber] = new OutlineGlow(body, outlineMaterial, boxyRendererNames);
        }
        RemoveEnded();

        bool localFlashing = UpdateLocalFlash(local);
        localEdge.Visible = localFlashing;
        elapsed += Time.deltaTime;
        Color color = glowColor * Intensity();
        foreach (OutlineGlow glow in glows.Values)
            glow.SetColor(color);

        if (screenBolts != null)
        {
            screenAreas.Clear();
            if (localFlashing)
                screenAreas.Add(new Rect(0f, 0f, Screen.width, Screen.height));
            screenBolts.SetAreas(screenAreas);
        }
        if (bodyBolts != null)
            bodyBolts.SetAreas(BodyAreas());
    }

    /// <summary>自分の効果の残りが増えた（減ったときも光らせるなら減った）瞬間から localFlashSeconds の間だけ true。見始めた時点で残っている分では光らせない</summary>
    bool UpdateLocalFlash(int local)
    {
        bool alive = game.TryGet(local, out PlayerState me) && me.IsAlive;
        int value = alive ? me.StatusOf(status) : 0;
        if (local != watchedLocal || !alive)
        {
            watchedLocal = local;
            lastLocalValue = value;
            localFlashTimer = 0f;
        }

        if (value > lastLocalValue || (flashOnDecrease && value < lastLocalValue))
            localFlashTimer = localFlashSeconds;
        lastLocalValue = value;

        localFlashTimer -= Time.deltaTime;
        return localFlashTimer > 0f;
    }

    void RemoveEnded()
    {
        removing.Clear();
        foreach (int player in glows.Keys)
        {
            if (!game.TryGet(player, out PlayerState state) || !state.IsAlive || state.StatusOf(status) <= 0 || player == registry.LocalPlayerNumber)
                removing.Add(player);
        }
        foreach (int player in removing)
        {
            glows[player].Detach();
            glows.Remove(player);
            heads.Remove(player);
        }
    }

    float Intensity()
    {
        if (!flicker)
            return Mathf.Lerp(intensityRange.x, intensityRange.y, OutlineGlow.Pulse(elapsed, pulseSeconds));

        flickerTimer -= Time.deltaTime;
        if (flickerTimer <= 0f)
        {
            flickerTimer = flickerInterval;
            flickerIntensity = Random.value < 0.2f ? 0.2f : Random.Range(intensityRange.x, intensityRange.y);
        }
        return flickerIntensity;
    }

    /// <summary>ほかの人の上半身を囲む円を、画面に映した範囲（中心がカメラの前にある人だけ）</summary>
    List<Rect> BodyAreas()
    {
        bodyAreas.Clear();
        Camera camera = viewer != null ? viewer.LocalCamera : null;
        if (camera == null)
            return bodyAreas;

        foreach (int player in glows.Keys)
        {
            if (TryBodyCenter(player, out Vector3 center) && TryProjectCircle(camera, center, out Rect area))
                bodyAreas.Add(area);
        }
        return bodyAreas;
    }

    bool TryBodyCenter(int player, out Vector3 center)
    {
        center = default;
        if (!registry.TryGet(player, out Transform body))
            return false;

        if (!heads.TryGetValue(player, out Transform head) || head == null)
            heads[player] = head = PlayerBody.FindHead(body);
        Vector3 top = head != null ? head.position : PlayerBody.HeadPosition(body);
        center = top + Vector3.down * bodyCenterDrop;
        return true;
    }

    bool TryProjectCircle(Camera camera, Vector3 center, out Rect area)
    {
        area = default;
        Vector3 middle = camera.WorldToScreenPoint(center);
        if (middle.z <= 0f)
            return false;

        Vector3 edge = camera.WorldToScreenPoint(center + camera.transform.right * bodyRadius);
        float radius = Vector2.Distance(middle, edge);
        area = new Rect(middle.x - radius, middle.y - radius, radius * 2f, radius * 2f);
        return area.Overlaps(camera.pixelRect);
    }
}
