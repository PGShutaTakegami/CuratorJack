using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 部屋一覧を上から順に並べて表示する。
/// </summary>
public class RoomListView : MonoBehaviour
{
    [SerializeField] private RoomListItemView itemPrefab;

    [SerializeField] private RectTransform container;

    [Tooltip("レイアウト確認用にシーンへ置いた見本の行（実行時は非表示）")]
    [SerializeField] private GameObject layoutSample;

    [SerializeField] private Vector2 firstItemPosition = new Vector2(-450f, 300f);

    [SerializeField] private float itemSpacing = 70f;

    readonly List<RoomListItemView> items = new List<RoomListItemView>();

    public event Action<RoomInfo> RoomDoubleClicked;

    void Awake()
    {
        if (layoutSample != null)
            layoutSample.SetActive(false);
    }

    public void Show(IReadOnlyList<RoomInfo> rooms)
    {
        while (items.Count < rooms.Count)
            items.Add(CreateItem(items.Count));

        for (int i = 0; i < items.Count; i++)
        {
            bool visible = i < rooms.Count;
            items[i].gameObject.SetActive(visible);
            if (visible)
                items[i].Bind(rooms[i]);
        }
    }

    RoomListItemView CreateItem(int index)
    {
        RoomListItemView item = Instantiate(itemPrefab, container);
        var rect = (RectTransform)item.transform;
        rect.anchoredPosition = firstItemPosition + Vector2.down * (itemSpacing * index);
        item.DoubleClicked += HandleItemDoubleClicked;
        return item;
    }

    void HandleItemDoubleClicked(RoomListItemView item)
    {
        if (item.Room != null)
            RoomDoubleClicked?.Invoke(item.Room);
    }
}
