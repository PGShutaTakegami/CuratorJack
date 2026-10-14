using System;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    public enum CardKinds
    {
        GoodCard,
        EffectCard,
        SpecialCard
    }
    enum GoodsCardKinds
    {
        //骨董品カード
        Jar1,Jar2,Jar3,
        //宝石カード
        Emerald,Rubby,Diamond,
        //絵画カード
        Picture1,Picture2,Picture3
    }

    enum effectCardKinds
    {
        /// <summary>
        /// 口コミ,効果カードを追加ドロー
        /// </summary>
        WordOfMouth,
        /// <summary>
        /// 真贋証明,付与されているカード効果を公開
        /// </summary>
        CertificateOfAuthenticity,
        /// <summary>
        /// 免罪符,自身のバフデバフ全てを一回無効にする。発動は強制
        /// </summary>
        Indulgence,
        /// <summary>
        /// 逆転,出品ターンの周りを逆にする
        /// </summary>
        Reversal,
        /// <summary>
        /// 投げ売り,付与されたものは次の出品最低開始価格がランダムで-1~-5で下がる
        /// </summary>
        FireSale,
        /// <summary>
        /// 圧力,出品カードのドローが1回出来なくなる
        /// </summary>
        Pressure,
        /// <summary>
        /// 伝手,出品カードのドロー
        /// </summary>
        Connections,
        /// <summary>
        /// 値踏み,付与された商品を購入した後に1~3増額される
        /// </summary>
        Markdown,
        /// <summary>
        /// 2度目,もう一度出品ターンにできる
        /// </summary>
        SecondTurn,
        /// <summary>
        /// 横やり,カードを出品カードに付与できない
        /// </summary>
        Interference,
        /// <summary>
        /// 鑑定,自分の持っている効果カードを1枚ランダムで公開
        /// </summary>
        Appraisal

    }
    enum specialBuff
    {
        /// <summary>
        /// 黄金律,コインを20枚増加
        /// </summary>
        GoldenBoon,
        /// <summary>
        /// 猶予の証,見送り権を1枚+
        /// </summary>
        GraceToken,
        /// <summary>
        /// ランダム,下記3つの効果のどれかをランダムで発動
        /// </summary>
        Random,
        /// <summary>
        /// 全鑑定,自分と出品者以外の全員のバフデバフカードを公開
        /// </summary>
        AppraisalAll,
        /// <summary>
        /// 全横やり,自分と出品者以外の全員のカード付与を封じる
        /// </summary>
        InterferenceAll,
        /// <summary>
        /// 全圧力,
        /// </summary>
        PressureAll,
    }

    [Serializable]
    public struct cardparameter
    {
        [Tooltip("カード名")]
        [SerializeField] private string cardName;

        [Tooltip("カードの画像")]
        [SerializeField] private Sprite cardSprite;

        [Tooltip("カードの説明")]
        [TextArea]
        [SerializeField] private string discription;

        [Tooltip("初期価格")]
        [SerializeField] private int initialPrice;

        public string CardName => cardName;

        public Sprite CardSprite => cardSprite;

        public string Discription => discription;

        public int InitialPrice => initialPrice;
    }

    [Tooltip("カードの種類")]
    [SerializeField] private CardKinds cardKind;

    public CardKinds CardKind => cardKind;

    public void SetKind(CardKinds kind)
    {
        cardKind = kind;
    }

    [Tooltip("このカードに反映しているデータ")]
    [SerializeField] private CardData data;

    public CardData Data => data;

    /// <summary>データを反映する。データに画像があれば、カードの画像をその画像にする。</summary>
    public void SetData(CardData cardData)
    {
        data = cardData;
        if (cardData != null && cardData.Parameter.CardSprite != null && TryGetComponent(out UnityEngine.UI.Image image))
            image.sprite = cardData.Parameter.CardSprite;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
