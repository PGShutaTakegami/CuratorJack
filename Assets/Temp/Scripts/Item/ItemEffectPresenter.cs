using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// 自分がアイテムを使ったときの見せ方。
/// ・虫眼鏡：今の出品に付いている効果カードを、自分の画面だけに公開する（付いていなければその旨を出す）。
/// ・電気プラグ：自分の画面全体を一瞬緑に光らせる。
/// 続いて残る効果（シールドの緑・マヒの電気）は StatusAuraPresenter が見せる。
/// </summary>
public class ItemEffectPresenter : MonoBehaviour
{
    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private PlayerNameDirectory names;

    [Tooltip("虫眼鏡で効果カードを見せる演出")]
    [SerializeField] private EffectCardReveal reveal;

    [Tooltip("電気プラグで画面を光らせる")]
    [SerializeField] private ScreenFlash flash;

    [SerializeField] private Color plugFlashColor = new Color(0.25f, 1f, 0.4f, 1f);

    void OnEnable()
    {
        turnFlow.ItemEffects.Used += HandleUsed;
    }

    void OnDisable()
    {
        turnFlow.ItemEffects.Used -= HandleUsed;
    }

    void HandleUsed(ItemUse use)
    {
        if (use.Player != registry.LocalPlayerNumber)
            return;

        switch (use.Item.Kind)
        {
            case ItemKind.Magnifier:
                StartCoroutine(ShowAttached(use.Listing));
                break;
            case ItemKind.PowerPlug:
                flash.Flash(plugFlashColor);
                break;
        }
    }

    IEnumerator ShowAttached(Listing listing)
    {
        yield return new WaitWhile(() => reveal.IsPlaying);

        CardData[] attached = listing.Buffs.Where(buff => buff.Data != null).Select(buff => buff.Data).ToArray();
        string seller = names.NameOf(listing.Seller);
        if (attached.Length == 0)
            yield return reveal.PresentNone($"虫眼鏡：{seller} の出品に効果カードは付いていません");
        else
            yield return reveal.Present($"虫眼鏡：{seller} の出品に付いている効果カード", attached);
    }
}
