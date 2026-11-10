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

    [Tooltip("左から順に並べたマス（ItemInventory.Capacity 個）")]
    [SerializeField] private ItemSlotView[] slots;

    bool isUsable;

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
        Refresh();
    }

    void OnDestroy()
    {
        game.ItemsChanged -= HandleItemsChanged;
    }

    /// <summary>マスのアイテムを使える間だけ true にする</summary>
    public void SetUsable(bool usable)
    {
        if (isUsable == usable)
            return;

        isUsable = usable;
        Refresh();
    }

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
            slots[i].Bind(hasPlayer && i < me.Items.Slots.Count ? me.Items.Slots[i] : null);
            slots[i].SetUsable(isUsable);
        }
    }
}
