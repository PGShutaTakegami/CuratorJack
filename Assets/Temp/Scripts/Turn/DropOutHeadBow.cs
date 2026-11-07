using System.Collections;
using UnityEngine;

/// <summary>
/// 自分以外のプレイヤーが脱落したら、頭（モニター）をテーブル側へ少しうつむかせる。
/// 頭のボーンを傾けるので、モニターとモニターに出しているものも一緒に傾く。
/// </summary>
public class DropOutHeadBow : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("うつむく向きを決める基準（テーブル中央）")]
    [SerializeField] private Transform tableCenter;

    [Tooltip("うつむく角度（度）")]
    [SerializeField] private float bowAngle = 20f;

    [SerializeField] private float bowSeconds = 0.6f;

    void Awake()
    {
        game.PlayerDied += HandlePlayerDied;
    }

    void OnDestroy()
    {
        game.PlayerDied -= HandlePlayerDied;
    }

    void HandlePlayerDied(PlayerState player)
    {
        if (player.PlayerNumber == registry.LocalPlayerNumber || !registry.TryGet(player.PlayerNumber, out Transform body))
            return;

        Transform head = PlayerBody.FindHead(body);
        if (head != null)
            StartCoroutine(Bow(head));
    }

    IEnumerator Bow(Transform head)
    {
        Vector3 forward = tableCenter.position - head.position;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f)
            yield break;

        Vector3 axis = Vector3.Cross(Vector3.up, forward.normalized);
        Quaternion from = head.rotation;
        Quaternion to = Quaternion.AngleAxis(bowAngle, axis) * from;

        for (float elapsed = 0f; elapsed < bowSeconds; elapsed += Time.deltaTime)
        {
            if (head == null)
                yield break;
            head.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / bowSeconds));
            yield return null;
        }
        if (head != null)
            head.rotation = to;
    }
}
