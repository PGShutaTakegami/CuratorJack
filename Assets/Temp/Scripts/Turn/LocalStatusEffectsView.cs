using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 自分にかかっている持続効果（圧力・横やり・免罪符・2度目・投げ売り）を縦に並べて表示する。
/// 残りが 0 になった（発動し終わった）効果は表示から消す。表示はその効果を持つ効果カードの画像と名前を使う。
/// </summary>
public class LocalStatusEffectsView : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("効果カードの画像と名前を探す山札")]
    [SerializeField] private EffectCardDeck effectDeck;

    [Tooltip("並べる表示の見本（実行時は非表示）")]
    [SerializeField] private StatusEffectBadge badgeTemplate;

    [Tooltip("表示を並べる親（上から順に並べる）")]
    [SerializeField] private Transform container;

    static readonly PlayerStatus[] Order =
    {
        PlayerStatus.Indulgence,
        PlayerStatus.ExtraTurn,
        PlayerStatus.FireSaleDiscount,
        PlayerStatus.Interference,
        PlayerStatus.Pressure
    };

    readonly Dictionary<PlayerStatus, StatusEffectBadge> badges = new Dictionary<PlayerStatus, StatusEffectBadge>();

    void Awake()
    {
        badgeTemplate.gameObject.SetActive(false);
        game.StatusChanged += HandleStatusChanged;
    }

    void OnDestroy()
    {
        game.StatusChanged -= HandleStatusChanged;
    }

    void HandleStatusChanged(PlayerState player)
    {
        if (player.PlayerNumber == registry.LocalPlayerNumber)
            Refresh(player);
    }

    void Refresh(PlayerState me)
    {
        int index = 0;
        foreach (PlayerStatus status in Order)
        {
            int value = me.StatusOf(status);
            if (value <= 0)
            {
                if (badges.TryGetValue(status, out StatusEffectBadge shown))
                    shown.gameObject.SetActive(false);
                continue;
            }

            StatusEffectBadge badge = BadgeOf(status);
            CardData card = CardOf(status);
            badge.Bind(card != null ? card.Parameter.CardSprite : null, card != null ? card.Parameter.CardName : status.ToString(), ValueText(status, value));
            badge.transform.SetSiblingIndex(index++);
            badge.gameObject.SetActive(true);
        }
    }

    StatusEffectBadge BadgeOf(PlayerStatus status)
    {
        if (!badges.TryGetValue(status, out StatusEffectBadge badge))
        {
            badge = Instantiate(badgeTemplate, container);
            badge.name = status.ToString();
            badges[status] = badge;
        }
        return badge;
    }

    CardData CardOf(PlayerStatus status)
    {
        CardEffect effect = EffectOf(status);
        return effectDeck.Effects.FirstOrDefault(card => card != null && card.Effect == effect);
    }

    static CardEffect EffectOf(PlayerStatus status)
    {
        switch (status)
        {
            case PlayerStatus.Pressure: return CardEffect.Pressure;
            case PlayerStatus.Interference: return CardEffect.Interference;
            case PlayerStatus.Indulgence: return CardEffect.Indulgence;
            case PlayerStatus.ExtraTurn: return CardEffect.SecondTurn;
            case PlayerStatus.FireSaleDiscount: return CardEffect.FireSale;
            default: throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }

    /// <summary>残りの表示。投げ売りは下げる額、ほかは回数</summary>
    static string ValueText(PlayerStatus status, int value) => status == PlayerStatus.FireSaleDiscount ? $"-{value}" : $"×{value}";
}
