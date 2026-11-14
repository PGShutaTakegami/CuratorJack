using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>データの Active を外したカード・アイテムが、山札から引かれず一覧にも出ないことを確かめる</summary>
public class ActiveDataTests
{
    const int Draws = 200;

    readonly List<Object> created = new List<Object>();

    [TearDown]
    public void DestroyCreated()
    {
        foreach (Object entry in created)
            Object.DestroyImmediate(entry);
        created.Clear();
    }

    T NewData<T>(bool active) where T : ScriptableObject
    {
        var data = ScriptableObject.CreateInstance<T>();
        var serialized = new SerializedObject(data);
        serialized.FindProperty("active").boolValue = active;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        created.Add(data);
        return data;
    }

    T NewComponent<T>() where T : Component
    {
        var holder = new GameObject(typeof(T).Name);
        created.Add(holder);
        return holder.AddComponent<T>();
    }

    static void SetList(Object target, string property, params Object[] entries)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty list = serialized.FindProperty(property);
        list.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [Test]
    public void 新しく作ったデータはActive()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        created.Add(card);
        created.Add(item);

        Assert.IsTrue(card.Active);
        Assert.IsTrue(item.Active);
    }

    [Test]
    public void 効果カードの山札はActiveでないカードを引かず一覧にも出さない()
    {
        CardData on = NewData<CardData>(true);
        CardData off = NewData<CardData>(false);
        EffectCardDeck deck = NewComponent<EffectCardDeck>();
        SetList(deck, "effects", off, on, null);
        deck.Reseed(1);

        CollectionAssert.AreEqual(new[] { on }, deck.Effects);
        for (int i = 0; i < Draws; i++)
            Assert.AreSame(on, deck.Draw());
    }

    [Test]
    public void 引けるデバフがなければ通常の候補から引く()
    {
        CardData on = NewData<CardData>(true);
        CardData offDebuff = NewData<CardData>(false);
        EffectCardDeck deck = NewComponent<EffectCardDeck>();
        SetList(deck, "effects", on);
        SetList(deck, "debuffs", offDebuff);
        deck.Reseed(1);

        for (int i = 0; i < Draws; i++)
            Assert.AreSame(on, deck.DrawDebuff());
    }

    [Test]
    public void スペシャルカードの山札はActiveでないカードを引かない()
    {
        CardData on = NewData<CardData>(true);
        CardData off = NewData<CardData>(false);
        SpecialCardDeck deck = NewComponent<SpecialCardDeck>();
        SetList(deck, "specials", on, off);
        deck.Reseed(1);

        CollectionAssert.AreEqual(new[] { on }, deck.Specials);
        for (int i = 0; i < Draws; i++)
            Assert.AreSame(on, deck.Draw());
    }

    [Test]
    public void すべてActiveでなければ何も引かない()
    {
        SpecialCardDeck deck = NewComponent<SpecialCardDeck>();
        SetList(deck, "specials", NewData<CardData>(false));

        Assert.IsNull(deck.Draw());
        Assert.IsEmpty(deck.Specials);
    }

    [Test]
    public void グッズカードの山札はActiveでないカードを配らない()
    {
        CardData on = NewData<CardData>(true);
        CardData off = NewData<CardData>(false);
        GoodsCardDeck deck = NewComponent<GoodsCardDeck>();
        SetList(deck, "goods", off, on);
        deck.Reseed(1);

        CollectionAssert.AreEqual(new[] { on }, deck.Goods);
        for (int i = 0; i < Draws; i++)
            Assert.AreSame(on, deck.Draw(deck.CreateRule()));
    }

    [Test]
    public void アイテムはActiveでないものを候補にも一覧にも出さない()
    {
        ItemDefinition on = NewData<ItemDefinition>(true);
        ItemDefinition off = NewData<ItemDefinition>(false);
        var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
        created.Add(catalog);
        SetList(catalog, "items", off, on);

        CollectionAssert.AreEqual(new[] { on }, catalog.Items);

        var deck = new ItemDeck(new[] { off, on });
        deck.Reseed(1);
        for (int i = 0; i < Draws; i++)
            Assert.AreSame(on, deck.Draw());
    }
}
