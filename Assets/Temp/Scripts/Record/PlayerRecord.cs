using System;
using UnityEngine;

/// <summary>
/// 自分（このパソコンのプレイヤー）の戦績。JsonUtility で保存できる形にしている。
/// </summary>
[Serializable]
public sealed class PlayerRecord
{
    [SerializeField] private int gamesPlayed;
    [SerializeField] private int wins;
    [SerializeField] private int purchases;
    [SerializeField] private int sales;
    [SerializeField] private int itemsUsed;

    /// <summary>遊んだゲームの数（始めた時点で数える）</summary>
    public int GamesPlayed => gamesPlayed;

    /// <summary>勝ったゲームの数</summary>
    public int Wins => wins;

    /// <summary>落札した商品の数</summary>
    public int Purchases => purchases;

    /// <summary>自分の出品が落札された数</summary>
    public int Sales => sales;

    /// <summary>使ったアイテムの数</summary>
    public int ItemsUsed => itemsUsed;

    public void AddGamePlayed() => gamesPlayed++;

    public void AddWin() => wins++;

    public void AddPurchase() => purchases++;

    public void AddSale() => sales++;

    public void AddItemUsed() => itemsUsed++;

    public string ToJson() => JsonUtility.ToJson(this);

    /// <summary>保存した文字列から戻す。空や壊れた文字列なら、何も記録していない状態を返す</summary>
    public static PlayerRecord FromJson(string json)
    {
        var record = new PlayerRecord();
        if (string.IsNullOrEmpty(json))
            return record;

        try
        {
            JsonUtility.FromJsonOverwrite(json, record);
        }
        catch (ArgumentException)
        {
            return new PlayerRecord();
        }
        return record;
    }
}
