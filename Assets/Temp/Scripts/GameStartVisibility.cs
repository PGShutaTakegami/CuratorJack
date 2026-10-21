using UnityEngine;

/// <summary>
/// 指定したオブジェクトをゲームが開始されるまで隠し、開始されたら表示する。
/// 隠す対象が自分自身だと開始の通知を受け取れないため、常に有効なオブジェクトに付ける。
/// </summary>
public class GameStartVisibility : MonoBehaviour
{
    [SerializeField] private GameStartGate startGate;

    [Tooltip("ゲーム開始まで隠すオブジェクト")]
    [SerializeField] private GameObject[] hiddenUntilStart;

    void Awake()
    {
        SetVisible(startGate.HasStarted);
        startGate.Started += HandleStarted;
    }

    void OnDestroy()
    {
        startGate.Started -= HandleStarted;
    }

    void HandleStarted()
    {
        SetVisible(true);
    }

    void SetVisible(bool visible)
    {
        foreach (GameObject target in hiddenUntilStart)
        {
            if (target != null)
                target.SetActive(visible);
        }
    }
}
