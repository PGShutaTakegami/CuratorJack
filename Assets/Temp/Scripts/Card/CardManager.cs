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

    [Serializable]
    public struct cardparameter
    {
        [Tooltip("カード名")]
        [SerializeField] private string cardName;

        [Tooltip("英語のカード名（空なら日本語のカード名を使う）")]
        [SerializeField] private string englishName;

        [Tooltip("カードの画像")]
        [SerializeField] private Sprite cardSprite;

        [Tooltip("カードの説明")]
        [TextArea]
        [SerializeField] private string discription;

        [Tooltip("初期価格")]
        [SerializeField] private int initialPrice;

        [Header("翻訳（空の言語は LanguageCodes.Pick の順で代わりの言語を使う）")]
        [TextArea]
        [SerializeField] private string englishDescription;

        [SerializeField] private string chineseSimplifiedName;

        [TextArea]
        [SerializeField] private string chineseSimplifiedDescription;

        [SerializeField] private string chineseTraditionalName;

        [TextArea]
        [SerializeField] private string chineseTraditionalDescription;

        /// <summary>現在の表示言語のカード名</summary>
        public string CardName => LanguageCodes.Pick(GameLanguage.Current, cardName, englishName, chineseSimplifiedName, chineseTraditionalName);

        public Sprite CardSprite => cardSprite;

        /// <summary>現在の表示言語のカードの説明</summary>
        public string Discription => LanguageCodes.Pick(GameLanguage.Current, discription, englishDescription, chineseSimplifiedDescription, chineseTraditionalDescription);

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

    /// <summary>このカードに付いているスペシャルカード（付いていなければ null）</summary>
    public CardData Special { get; private set; }

    public void SetSpecial(CardData special)
    {
        Special = special;
    }

    /// <summary>データを反映する。データに画像があれば、カードの画像をその画像にする。</summary>
    public void SetData(CardData cardData)
    {
        data = cardData;
        if (cardData != null && cardData.Parameter.CardSprite != null && TryGetComponent(out UnityEngine.UI.Image image))
            image.sprite = cardData.Parameter.CardSprite;
    }
}
