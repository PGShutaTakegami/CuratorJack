using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 見送り権を使ったプレイヤーの手元（落札カードの山の、その人の席から見て右）に、使った枚数だけ半券を置いていく。
/// 半券は少し上から回りながら落ちてきて置かれ、ずっと残す。重ねずに、その人の席から見て右へ 1 枚ずつ並べ、1 列が埋まったらテーブル中央側に次の列を作る。
/// 見送り権を使った知らせは全員の画面で同じ順に届くため、マルチでもそれぞれの画面で同じプレイヤーの手元に置かれる。
/// 足りずに脱落したとき（チケットを切らないとき）は置かない。
/// </summary>
public class PassTicketStubPiles : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("置く半券（寝かせた板が上を向き、半券の上が forward を向くもの）")]
    [SerializeField] private GameObject stubPrefab;

    [Tooltip("テーブル中央")]
    [SerializeField] private Transform tableCenter;

    [Tooltip("テーブル。縁の位置（半径）に使う")]
    [SerializeField] private Renderer table;

    [Tooltip("手元に置く面（中央の台より低いテーブルの外周の上面）の高さの目印")]
    [SerializeField] private Transform surface;

    [Tooltip("中央のマット。手元の半券はマットの外に置く")]
    [SerializeField] private Renderer mat;

    [Tooltip("マットの縁（0）からテーブルの縁（1）までのどこに置くか（落札カードの山と同じ値にすると横に並ぶ）")]
    [Range(0f, 1f)]
    [SerializeField] private float edgeRatio = 0.5f;

    [Tooltip("落札カードの山から、その人の席から見て右へずらす距離")]
    [SerializeField] private float sideOffset = 0.55f;

    [Tooltip("並べる半券どうしの、その人の席から見て横の間隔")]
    [SerializeField] private float columnSpacing = 0.25f;

    [Tooltip("1 列に並べる枚数（超えたらテーブル中央側に次の列を作る）")]
    [Min(1)]
    [SerializeField] private int columnsPerRow = 5;

    [Tooltip("列どうしの間隔（テーブル中央側へ）")]
    [SerializeField] private float rowSpacing = 0.4f;

    [Tooltip("置く位置をずらす最大の距離（間隔より十分小さくして隣と重ねない）")]
    [SerializeField] private float slotJitter = 0.015f;

    [Tooltip("向きをずらす最大の角度（隣と重ならない程度に小さく）")]
    [SerializeField] private float slotAngleJitter = 8f;

    [Tooltip("面からの高さ")]
    [SerializeField] private float surfaceOffset = 0.002f;

    [Tooltip("落ち始める高さ（置く位置から上へ）")]
    [SerializeField] private float dropHeight = 0.25f;

    [SerializeField] private float dropSeconds = 0.45f;

    [Tooltip("落ちる間に回る角度")]
    [SerializeField] private float dropSpin = 120f;

    [Tooltip("落ち始めの傾き")]
    [SerializeField] private float dropTilt = 35f;

    readonly Dictionary<int, int> counts = new Dictionary<int, int>();

    void Awake()
    {
        game.PassRightConsumed += HandlePassRightConsumed;
    }

    void OnDestroy()
    {
        game.PassRightConsumed -= HandlePassRightConsumed;
    }

    void HandlePassRightConsumed(PlayerState player, int count)
    {
        for (int i = 0; i < count; i++)
            Place(player.PlayerNumber);
    }

    void Place(int playerNumber)
    {
        if (!registry.TryGet(playerNumber, out Transform player))
            return;

        Vector3 center = tableCenter.position;
        Vector3 toPlayer = PlayerBody.HeadPosition(player) - center;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 1e-6f)
            return;
        toPlayer.Normalize();

        Quaternion facing = Quaternion.LookRotation(-toPlayer, Vector3.up);
        float distance = Mathf.Lerp(mat.bounds.extents.x, table.bounds.extents.x, edgeRatio);
        Vector2 jitter = Random.insideUnitCircle * slotJitter;

        counts.TryGetValue(playerNumber, out int index);
        counts[playerNumber] = index + 1;
        int column = index % columnsPerRow;
        int row = index / columnsPerRow;

        Vector3 spot = center + toPlayer * (distance - row * rowSpacing)
            + facing * Vector3.right * (sideOffset + column * columnSpacing)
            + new Vector3(jitter.x, 0f, jitter.y);
        spot.y = surface.position.y + surfaceOffset;
        Quaternion rotation = facing * Quaternion.Euler(0f, Random.Range(-slotAngleJitter, slotAngleJitter), 0f);

        GameObject stub = Instantiate(stubPrefab, spot + Vector3.up * dropHeight, rotation, transform);
        StartCoroutine(Drop(stub.transform, spot, rotation));
    }

    /// <summary>上から回りながら落ちてきて、傾きが戻りながら置かれる（落ちるほど速くなる）</summary>
    IEnumerator Drop(Transform stub, Vector3 spot, Quaternion rotation)
    {
        Vector3 start = spot + Vector3.up * dropHeight;
        Quaternion startRotation = rotation * Quaternion.Euler(dropTilt, -dropSpin, 0f);
        for (float elapsed = 0f; elapsed < dropSeconds; elapsed += Time.deltaTime)
        {
            if (stub == null)
                yield break;
            float t = elapsed / dropSeconds;
            stub.SetPositionAndRotation(Vector3.LerpUnclamped(start, spot, t * t), Quaternion.Slerp(startRotation, rotation, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        if (stub != null)
            stub.SetPositionAndRotation(spot, rotation);
    }
}
