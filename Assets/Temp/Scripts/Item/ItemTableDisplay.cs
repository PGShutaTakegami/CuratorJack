using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 各プレイヤーが持っているアイテムのモデルを、その人の手元（落札カードの山の、その人の席から見て左で、
/// 残りの見送り権のチケットの列より 1 段テーブル中央側。使った半券は山の右に並ぶ）に置く。
/// アイテムのマスごとに置く場所を決め、その人の席から見て左へマスの順に並べる。
/// 手に入れたモデルは見えない状態で置いておき、PresentAcquired で全員のカメラをその場所へ向けてからフェードで出す。
/// 使ったモデルは、カメラをその場所へ向けてからフェードで消す（カメラの動きは入札のアップと同じ順番待ちで再生する）。
/// アイテムの増減と使った知らせは全員の画面で同じ順に届くため、マルチでもそれぞれの画面で同じプレイヤーの手元に置かれる。
/// </summary>
public class ItemTableDisplay : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private TurnFlowController turnFlow;

    [Tooltip("カメラを手元のアイテムへ向ける演出（入札のアップと同じ順番待ちで再生する）")]
    [SerializeField] private BidderCloseUpShot closeUp;

    [Tooltip("置き場の基準（落札カードの山の中心と、その人の席から読める向き）")]
    [SerializeField] private PlayerCardPiles piles;

    [Tooltip("落札カードの山の中心から、その人の席から見て左へずらす距離（右には使った半券が並ぶ）。" +
             "山は横に約 0.48 まで広がり、モデルは幅の半分が約 0.15 あるため、それより外にする")]
    [SerializeField] private float sideOffset = 0.75f;

    [Tooltip("落札カードの山の中心から、テーブル中央の側へずらす距離。" +
             "山の左に並ぶ残りの見送り権のチケット（奥行きの半分が約 0.1）と、モデルの長さの半分（約 0.25）より奥にする")]
    [SerializeField] private float rowOffset = 0.4f;

    [Tooltip("マスどうしの、その人の席から見て横の間隔")]
    [SerializeField] private float slotSpacing = 0.3f;

    [Tooltip("モデルの大きさの倍率")]
    [SerializeField] private float modelScale = 2f;

    [Tooltip("向きをずらす最大の角度")]
    [SerializeField] private float angleJitter = 12f;

    [Tooltip("面からの高さ")]
    [SerializeField] private float surfaceOffset = 0.002f;

    [Tooltip("フェードで出す / 消す秒数")]
    [SerializeField] private float fadeSeconds = 0.5f;

    [Tooltip("出し終えてから / 消し終えてから、カメラを向けたまま止めておく秒数")]
    [SerializeField] private float holdSeconds = 0.5f;

    sealed class Placed
    {
        public ItemDefinition item;
        public GameObject model;
        public ModelFader fader;
        public Vector3 spot;
        public bool isShown;
    }

    readonly Dictionary<int, Placed[]> shelves = new Dictionary<int, Placed[]>();
    readonly Dictionary<int, List<Placed>> spent = new Dictionary<int, List<Placed>>();

    void OnEnable()
    {
        game.ItemsChanged += HandleItemsChanged;
        turnFlow.ItemEffects.Used += HandleUsed;
    }

    void OnDisable()
    {
        game.ItemsChanged -= HandleItemsChanged;
        turnFlow.ItemEffects.Used -= HandleUsed;
    }

    /// <summary>その人が手に入れてまだ見せていないモデルへ全員のカメラを向け、フェードで出す。見せ終えるまで待つ</summary>
    public IEnumerator PresentAcquired(int playerNumber)
    {
        if (!shelves.TryGetValue(playerNumber, out Placed[] shelf))
            yield break;

        var hidden = new List<Placed>();
        foreach (Placed placed in shelf)
        {
            if (placed != null && !placed.isShown)
                hidden.Add(placed);
        }
        if (hidden.Count == 0)
            yield break;

        Vector3 focus = Vector3.zero;
        foreach (Placed placed in hidden)
        {
            placed.isShown = true;
            focus += placed.spot;
        }
        focus /= hidden.Count;

        closeUp.ShowTableSpot(playerNumber, focus, () => FadeIn(hidden));
        yield return new WaitWhile(() => closeUp.IsPlaying);
    }

    void HandleItemsChanged(PlayerState player)
    {
        if (!shelves.TryGetValue(player.PlayerNumber, out Placed[] shelf))
            shelves[player.PlayerNumber] = shelf = new Placed[ItemInventory.Capacity];

        IReadOnlyList<ItemDefinition> slots = player.Items.Slots;
        for (int slot = 0; slot < shelf.Length; slot++)
        {
            ItemDefinition item = slot < slots.Count ? slots[slot] : null;
            Placed placed = shelf[slot];
            if (placed != null && placed.item != item)
            {
                SpentOf(player.PlayerNumber).Add(placed);
                shelf[slot] = placed = null;
            }

            if (placed == null && item != null && item.TableModel != null)
                shelf[slot] = Create(player.PlayerNumber, slot, item);
        }
    }

    void HandleUsed(ItemUse use)
    {
        List<Placed> list = SpentOf(use.Player);
        int index = list.FindIndex(placed => placed.item == use.Item);
        if (index < 0)
            return;

        Placed used = list[index];
        list.RemoveAt(index);
        if (!used.isShown)
        {
            Remove(used);
            return;
        }
        closeUp.ShowTableSpot(use.Player, used.spot, () => FadeOut(used));
    }

    List<Placed> SpentOf(int playerNumber)
    {
        if (!spent.TryGetValue(playerNumber, out List<Placed> list))
            spent[playerNumber] = list = new List<Placed>();
        return list;
    }

    Placed Create(int playerNumber, int slot, ItemDefinition item)
    {
        if (!piles.TryGetSpot(playerNumber, out Pose pile))
            return null;

        Vector3 spot = pile.position
            + pile.rotation * (Vector3.left * (sideOffset + slot * slotSpacing) + Vector3.forward * rowOffset)
            + Vector3.up * surfaceOffset;
        Quaternion rotation = pile.rotation * Quaternion.Euler(0f, Random.Range(-angleJitter, angleJitter), 0f);

        GameObject model = Instantiate(item.TableModel, spot, rotation, transform);
        model.transform.localScale = item.TableModel.transform.localScale * modelScale;
        var placed = new Placed { item = item, model = model, fader = new ModelFader(model), spot = spot };
        placed.fader.SetAlpha(0f);
        model.SetActive(false);
        return placed;
    }

    IEnumerator FadeIn(List<Placed> targets)
    {
        foreach (Placed placed in targets)
        {
            if (placed.model != null)
                placed.model.SetActive(true);
        }
        yield return Fade(targets, 0f, 1f);
        yield return new WaitForSeconds(holdSeconds);
    }

    IEnumerator FadeOut(Placed used)
    {
        yield return Fade(new[] { used }, 1f, 0f);
        Remove(used);
        yield return new WaitForSeconds(holdSeconds);
    }

    IEnumerator Fade(IReadOnlyList<Placed> targets, float from, float to)
    {
        for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.deltaTime)
        {
            float alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / fadeSeconds));
            foreach (Placed placed in targets)
            {
                if (placed.model != null)
                    placed.fader.SetAlpha(alpha);
            }
            yield return null;
        }
        foreach (Placed placed in targets)
        {
            if (placed.model != null)
                placed.fader.SetAlpha(to);
        }
    }

    static void Remove(Placed placed)
    {
        placed.fader.Dispose();
        if (placed.model != null)
            Destroy(placed.model);
    }
}
