using System.Collections.Generic;
using UnityEngine;

/// <summary>落札したときにもらえるアイテムの候補一覧</summary>
[CreateAssetMenu(fileName = "ItemCatalog", menuName = "CJ/ItemCatalog")]
public class ItemCatalog : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();

    public IReadOnlyList<ItemDefinition> Items => items;
}
