using System.Collections.Generic;
using UnityEngine;

/// <summary>落札したときにもらえるアイテムの候補一覧。Active でないアイテムは候補にも一覧にも出さない</summary>
[CreateAssetMenu(fileName = "ItemCatalog", menuName = "CJ/ItemCatalog")]
public class ItemCatalog : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();

    public IReadOnlyList<ItemDefinition> Items => Activatable.ActiveOnly(items);
}
