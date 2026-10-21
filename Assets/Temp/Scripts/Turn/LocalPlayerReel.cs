using UnityEngine;

/// <summary>
/// 自分の状態の数値（所持コイン・落札数など）を上部のバーのリールに表示する。最低 minimumDigits 桁で、足りなければ枠を足す。
/// 値が変わったら、変わった桁を増減の向きに巻いて切り替える（最初に表示するときは巻かない）。
/// </summary>
public abstract class LocalPlayerReel : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private ReelNumberView reels;

    [Tooltip("値が小さくても表示しておく桁数（足りない上の桁は 0）")]
    [SerializeField] private int minimumDigits = 2;

    bool hasShownValue;

    protected abstract int ValueOf(PlayerState player);

    void Awake()
    {
        reels.SetMinimumDigits(minimumDigits);
        reels.SetValue(0, false);
    }

    void Update()
    {
        if (!game.TryGet(registry.LocalPlayerNumber, out PlayerState me))
            return;

        reels.SetValue(ValueOf(me), hasShownValue);
        hasShownValue = true;
    }
}
