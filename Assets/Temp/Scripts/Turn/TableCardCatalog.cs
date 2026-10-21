using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カードの画像と、テーブルに置くカードのオブジェクトの対応表。
/// 対応がない画像には fallback を使う。
/// </summary>
[CreateAssetMenu(menuName = "CJ/Table Card Catalog", fileName = "TableCardCatalog")]
public class TableCardCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public Sprite sprite;
        public TableCard prefab;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    [Tooltip("画像がない・対応がないときに使うカード")]
    [SerializeField] private TableCard fallback;

    public TableCard Find(Sprite sprite)
    {
        if (sprite != null)
        {
            foreach (Entry entry in entries)
            {
                if (entry.sprite == sprite || (entry.sprite != null && entry.sprite.texture == sprite.texture))
                    return entry.prefab;
            }
        }
        return fallback;
    }
}
