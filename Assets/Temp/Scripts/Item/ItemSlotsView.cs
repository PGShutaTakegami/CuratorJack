using System;
using UnityEngine;

/// <summary>
/// 自分が持っているアイテムを、左下に横並びのマスで表示する。
/// 表示とクリックの通知だけを受け持ち、使えるかどうかの判断は操作役（LocalPlayerAgent）が SetUsable で渡す。
/// </summary>
public class ItemSlotsView : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("ゲームが始まるまではマスを隠す")]
    [SerializeField] private GameStartGate startGate;

    [Tooltip("左から順に並べたマス（ItemInventory.Capacity 個）")]
    [SerializeField] private ItemSlotView[] slots;

    bool isUsable;
    bool isOwnListing;

    /// <summary>マスが押された（左から 0 始まりの番号）</summary>
    public event Action<int> SlotClicked;

    void Awake()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int slot = i;
            slots[i].Clicked += () => SlotClicked?.Invoke(slot);
        }
        game.ItemsChanged += HandleItemsChanged;
        startGate.Started += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        game.ItemsChanged -= HandleItemsChanged;
        startGate.Started -= Refresh;
    }

    /// <summary>マスのアイテムを使える間だけ usable を true にする。ownListing は自分の出品のオークション中か（そのとき使えないアイテムは暗くする）</summary>
    public void SetUsable(bool usable, bool ownListing)
    {
        if (isUsable == usable && isOwnListing == ownListing)
            return;

        isUsable = usable;
        isOwnListing = ownListing;
        Refresh();
    }

    /// <summary>今 slot のアイテムを使えるか（空きなら false）</summary>
    public bool CanUse(int slot)
    {
        ItemDefinition item = ItemAt(slot);
        return isUsable && IsAllowed(item);
    }

    bool IsAllowed(ItemDefinition item) => item != null && (!isOwnListing || item.CanUseOnOwnListing);

    /// <summary>自分の slot のアイテム（空きなら null）</summary>
    public ItemDefinition ItemAt(int slot)
    {
        return game.TryGet(registry.LocalPlayerNumber, out PlayerState me) && slot >= 0 && slot < me.Items.Slots.Count
            ? me.Items.Slots[slot]
            : null;
    }

    void HandleItemsChanged(PlayerState player)
    {
        if (player.PlayerNumber == registry.LocalPlayerNumber)
            Refresh();
    }

    void Refresh()
    {
        bool hasPlayer = game.TryGet(registry.LocalPlayerNumber, out PlayerState me);
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].gameObject.SetActive(startGate.HasStarted);
            ItemDefinition item = hasPlayer && i < me.Items.Slots.Count ? me.Items.Slots[i] : null;
            slots[i].Bind(item);
            slots[i].SetUsable(isUsable && (item == null || IsAllowed(item)));
        }
    }
}
