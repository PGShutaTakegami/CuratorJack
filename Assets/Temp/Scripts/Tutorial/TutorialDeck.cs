using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// チュートリアルで配るカードを決めておく。自分と CPU で別の並びを使い、カードの種類ごとに先頭から順に配る。
/// 並びを使い切った種類は、山札から引かせる。
/// </summary>
public class TutorialDeck : MonoBehaviour, IScriptedDraw
{
    [Serializable]
    struct ScriptedCard
    {
        public CardManager.CardKinds kind;

        public CardData card;

        [Tooltip("出品カードに付けるスペシャルカード（付けなければ空）")]
        public CardData special;
    }

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("自分に配るカード（種類ごとに上から順に配る）")]
    [SerializeField] private ScriptedCard[] localCards;

    [Tooltip("CPU に配るカード（CPU ごとに、種類ごとに上から順に配る）")]
    [SerializeField] private ScriptedCard[] cpuCards;

    readonly Dictionary<(int player, CardManager.CardKinds kind), int> dealt = new Dictionary<(int, CardManager.CardKinds), int>();

    public bool TryCreate(int player, CardManager.CardKinds kind, out HandCard card)
    {
        ScriptedCard[] script = player == registry.LocalPlayerNumber ? localCards : cpuCards;
        dealt.TryGetValue((player, kind), out int index);

        int found = 0;
        foreach (ScriptedCard entry in script)
        {
            if (entry.kind != kind)
                continue;

            if (found == index)
            {
                dealt[(player, kind)] = index + 1;
                card = new HandCard(kind, entry.card, entry.special);
                return true;
            }
            found++;
        }

        card = null;
        return false;
    }
}
