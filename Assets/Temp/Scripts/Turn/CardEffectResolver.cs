using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 効果カード・スペシャルカードの効果を処理する。状態の書き換えは GameManager を通して行い、
/// 画面で見せること（公開・追加のドロー・逆転・メッセージ）は EffectReport にまとめて返す。
/// ・受け取り時：出品に付いていた効果カードと商品のスペシャルカードを公開してから、受け取った人（落札者、いなければ出品者）に発動する。
///   受け取った人に免罪符が残っていれば、そのとき受けた効果はすべて無効になり、免罪符を 1 回使う。
/// ・全横やり・全圧力は、受け取った人以外の生きている全員（出品者も含む）にかかる（免罪符が残っている人には無効）。
/// ・ランダムは、ランダムのカードからディゾルブで選ばれたスペシャルカードへ変えて公開してから発動する。
/// </summary>
public sealed class CardEffectResolver
{
    const int GoldenBoonCoins = 20;
    const int GraceTokenPassRights = 1;
    static readonly CardEffect[] RandomChoices = { CardEffect.InterferenceAll, CardEffect.PressureAll };

    readonly GameManager game;
    readonly Func<int, string> nameOf;
    readonly Func<CardEffect, CardData> specialOf;
    Random random = new Random();

    /// <param name="nameOf">メッセージと公開の見出しに使うプレイヤーの呼び名</param>
    /// <param name="specialOf">効果からスペシャルカードのデータを探す（ランダムで選ばれたカードの公開に使う。なければ null）</param>
    public CardEffectResolver(GameManager game, Func<int, string> nameOf, Func<CardEffect, CardData> specialOf)
    {
        this.game = game;
        this.nameOf = nameOf;
        this.specialOf = specialOf;
    }

    /// <summary>効果量などの乱数をシードからやり直す（全員の画面で同じ結果にするため）</summary>
    public void Reseed(int seed)
    {
        random = new Random(seed);
    }

    /// <summary>出品に付いていた効果カードとスペシャルカードを、受け取った人に発動する</summary>
    public EffectReport ResolveReceived(int recipient, int seller, IReadOnlyList<HandCard> buffs, CardData special)
    {
        var report = new EffectReport();
        if ((buffs.Count == 0 && special == null) || !game.IsAlive(recipient))
            return report;

        CardData[] attached = buffs.Where(buff => buff.Data != null).Select(buff => buff.Data).ToArray();
        if (attached.Length > 0)
        {
            string title = recipient == seller
                ? GameText.Format("effect.title.unsold_effects", nameOf(recipient))
                : GameText.Format("effect.title.won_effects", nameOf(recipient));
            report.AddReveal(recipient, title, attached);
        }
        bool nullified = game.TryUseStatus(recipient, PlayerStatus.Indulgence);
        CardEffect randomChoice = special != null && special.Effect == CardEffect.Random && !nullified
            ? RandomChoices[random.Next(RandomChoices.Length)]
            : CardEffect.None;
        if (special != null)
            RevealSpecial(recipient, seller, special, randomChoice, report);

        if (nullified)
        {
            report.AddMessage(GameText.Format("effect.indulgence_nullified", nameOf(recipient)));
            return report;
        }

        foreach (HandCard buff in buffs)
        {
            if (buff.Data != null)
                Apply(buff.Data, recipient, seller, report);
        }
        if (randomChoice != CardEffect.None)
        {
            report.AddMessage(GameText.Format("effect.random_chosen", special.Parameter.CardName, LabelOf(randomChoice)));
            ApplyToOthers(randomChoice, recipient, report);
        }
        else if (special != null)
        {
            Apply(special, recipient, seller, report);
        }
        return report;
    }

    /// <summary>
    /// 受け取った人のスペシャルカードを公開する。ランダムで選ばれた効果があれば、ランダムのカードを見せてから
    /// ディゾルブで選ばれたスペシャルカードへ変える。
    /// </summary>
    void RevealSpecial(int recipient, int seller, CardData special, CardEffect randomChoice, EffectReport report)
    {
        string title = recipient == seller
            ? GameText.Format("effect.title.unsold_special", nameOf(recipient))
            : GameText.Format("effect.title.won_special", nameOf(recipient));
        CardData chosen = randomChoice != CardEffect.None ? specialOf(randomChoice) : null;
        if (chosen == null)
        {
            report.AddReveal(recipient, title, new[] { special });
            return;
        }

        report.AddTransform(recipient, GameText.Format("effect.title.transform", title, special.Parameter.CardName, chosen.Parameter.CardName), new[] { special }, new[] { chosen });
    }

    void Apply(CardData card, int recipient, int seller, EffectReport report)
    {
        string who = nameOf(recipient);
        string label = card.Parameter.CardName;
        switch (card.Effect)
        {
            case CardEffect.WordOfMouth:
                report.AddDraw(recipient, CardManager.CardKinds.EffectCard);
                report.AddMessage(GameText.Format("effect.word_of_mouth", label, who));
                break;
            case CardEffect.Connections:
                report.AddDraw(recipient, CardManager.CardKinds.GoodCard);
                report.AddMessage(GameText.Format("effect.connections", label, who));
                break;
            case CardEffect.Indulgence:
                game.AddStatus(recipient, PlayerStatus.Indulgence, 1);
                report.AddMessage(GameText.Format("effect.indulgence", label, who));
                break;
            case CardEffect.Reversal:
                report.AddReversal();
                report.AddMessage(GameText.Format("effect.reversal", label));
                break;
            case CardEffect.FireSale:
                int discount = random.Next(1, 6);
                game.AddStatus(recipient, PlayerStatus.FireSaleDiscount, discount);
                report.AddMessage(GameText.Format("effect.fire_sale", label, who, discount));
                break;
            case CardEffect.Pressure:
                game.AddStatus(recipient, PlayerStatus.Pressure, 1);
                report.AddMessage(GameText.Format("effect.pressure", label, who) + RemainingText(recipient, PlayerStatus.Pressure));
                break;
            case CardEffect.Markdown:
                ApplyMarkdown(label, recipient, seller, report);
                break;
            case CardEffect.SecondTurn:
                game.AddStatus(recipient, PlayerStatus.ExtraTurn, 1);
                report.AddMessage(GameText.Format("effect.second_turn", label, who));
                break;
            case CardEffect.Interference:
                game.AddStatus(recipient, PlayerStatus.Interference, 1);
                report.AddMessage(GameText.Format("effect.interference", label, who) + RemainingText(recipient, PlayerStatus.Interference));
                break;
            case CardEffect.GoldenBoon:
                int before = game.TryGet(recipient, out PlayerState state) ? state.Coins : 0;
                game.AddCoins(recipient, GoldenBoonCoins);
                report.AddCoinChange(recipient, before, before + GoldenBoonCoins);
                report.AddMessage(GameText.Format("effect.golden_boon", label, who, GoldenBoonCoins));
                break;
            case CardEffect.GraceToken:
                game.AddPassRights(recipient, GraceTokenPassRights);
                report.AddMessage(GameText.Format("effect.grace_token", label, who, GraceTokenPassRights));
                break;
            case CardEffect.InterferenceAll:
            case CardEffect.PressureAll:
                ApplyToOthers(card.Effect, recipient, report);
                break;
        }
    }

    /// <summary>
    /// 値踏み：購入した人が出品者へ 1〜3 コインを追加で払う（出品者自身が受け取ったときは何も起きない）。
    /// 最後の 1v1 では、購入した人は払うが出品者には入らない。
    /// </summary>
    void ApplyMarkdown(string label, int recipient, int seller, EffectReport report)
    {
        if (recipient == seller)
        {
            report.AddMessage(GameText.Format("effect.markdown_self", label));
            return;
        }

        bool sellerEarns = game.SellerEarnsProceeds;
        int paid = game.PayProceeds(recipient, seller, random.Next(1, 4));
        report.AddMessage(sellerEarns
            ? GameText.Format("effect.markdown_paid", label, nameOf(recipient), nameOf(seller), paid)
            : GameText.Format("effect.markdown_paid_final", label, nameOf(recipient), paid));
    }

    /// <summary>全横やり・全圧力を、受け取った人以外の生きている全員（出品者も含む）にかける</summary>
    void ApplyToOthers(CardEffect effect, int recipient, EffectReport report)
    {
        string label = LabelOf(effect);
        int[] targets = game.AlivePlayers
            .Select(player => player.PlayerNumber)
            .Where(player => player != recipient)
            .ToArray();
        if (targets.Length == 0)
        {
            report.AddMessage(GameText.Format("effect.no_targets", label));
            return;
        }

        var affected = new List<string>();
        foreach (int target in targets)
        {
            if (game.TryUseStatus(target, PlayerStatus.Indulgence))
            {
                report.AddMessage(GameText.Format("effect.blocked_by_indulgence", label, nameOf(target)));
                continue;
            }

            switch (effect)
            {
                case CardEffect.InterferenceAll:
                    game.AddStatus(target, PlayerStatus.Interference, 1);
                    affected.Add(nameOf(target) + RemainingText(target, PlayerStatus.Interference));
                    break;
                case CardEffect.PressureAll:
                    game.AddStatus(target, PlayerStatus.Pressure, 1);
                    affected.Add(nameOf(target) + RemainingText(target, PlayerStatus.Pressure));
                    break;
            }
        }

        if (affected.Count == 0)
            return;
        string names = string.Join(GameText.Get("effect.name_separator"), affected);
        report.AddMessage(effect == CardEffect.InterferenceAll
            ? GameText.Format("effect.interference", label, names)
            : GameText.Format("effect.pressure", label, names));
    }

    /// <summary>横やり・圧力は受けた枚数だけ回数が重なる。2 回以上残っていれば、その回数を添える</summary>
    string RemainingText(int player, PlayerStatus status)
    {
        int remaining = game.TryGet(player, out PlayerState state) ? state.StatusOf(status) : 0;
        return remaining > 1 ? GameText.Format("effect.remaining", remaining) : string.Empty;
    }

    static string LabelOf(CardEffect effect)
    {
        switch (effect)
        {
            case CardEffect.InterferenceAll: return GameText.Get("effect.label.interference_all");
            case CardEffect.PressureAll: return GameText.Get("effect.label.pressure_all");
            default: return effect.ToString();
        }
    }
}
