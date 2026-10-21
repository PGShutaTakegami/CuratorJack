using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 効果カード・スペシャルカードの効果を処理する。状態の書き換えは GameManager を通して行い、
/// 画面で見せること（公開・追加のドロー・逆転・メッセージ）は EffectReport にまとめて返す。
/// ・出品時：真贋証明が付いていれば、付いている効果カードを全員に公開する。
/// ・受け取り時：出品に付いていた効果カードと商品のスペシャルカードを公開してから、受け取った人（落札者、いなければ出品者）に発動する。
///   受け取った人に免罪符が残っていれば、そのとき受けた効果はすべて無効になり、免罪符を 1 回使う。
/// ・全鑑定・全横やり・全圧力は、受け取った人以外の生きている全員（出品者も含む）にかかる（免罪符が残っている人には無効）。
/// ・ランダムは、ランダムのカードからディゾルブで選ばれたスペシャルカードへ変えて公開してから発動する。
/// </summary>
public sealed class CardEffectResolver
{
    const int GoldenBoonCoins = 20;
    const int GraceTokenPassRights = 1;
    static readonly CardEffect[] RandomChoices = { CardEffect.AppraisalAll, CardEffect.InterferenceAll, CardEffect.PressureAll };

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

    /// <summary>出品が決まったときの効果（真贋証明）</summary>
    public EffectReport ResolveListing(Listing listing)
    {
        var report = new EffectReport();
        if (listing.Buffs.Any(buff => EffectOf(buff) == CardEffect.CertificateOfAuthenticity))
        {
            CardData[] attached = listing.Buffs.Where(buff => buff.Data != null).Select(buff => buff.Data).ToArray();
            report.AddReveal(listing.Seller, $"{nameOf(listing.Seller)} の出品に付いている効果カード（真贋証明）", attached);
        }
        return report;
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
                ? $"落札者なし：{nameOf(recipient)} に発動する効果カード"
                : $"{nameOf(recipient)} が落札した商品に付いていた効果カード";
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
            report.AddMessage($"免罪符：{nameOf(recipient)} が受けた効果はすべて無効になりました");
            return report;
        }

        foreach (HandCard buff in buffs)
        {
            if (buff.Data != null)
                Apply(buff.Data, recipient, seller, report);
        }
        if (randomChoice != CardEffect.None)
        {
            report.AddMessage($"{special.Parameter.CardName}：{LabelOf(randomChoice)} が発動します");
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
            ? $"落札者なし：{nameOf(recipient)} に発動するスペシャルカード"
            : $"{nameOf(recipient)} が落札した商品のスペシャルカード";
        CardData chosen = randomChoice != CardEffect.None ? specialOf(randomChoice) : null;
        if (chosen == null)
        {
            report.AddReveal(recipient, title, new[] { special });
            return;
        }

        report.AddTransform(recipient, $"{title}（{special.Parameter.CardName} → {chosen.Parameter.CardName}）", new[] { special }, new[] { chosen });
    }

    static CardEffect EffectOf(HandCard card) => card.Data != null ? card.Data.Effect : CardEffect.None;

    void Apply(CardData card, int recipient, int seller, EffectReport report)
    {
        string who = nameOf(recipient);
        string label = card.Parameter.CardName;
        switch (card.Effect)
        {
            case CardEffect.WordOfMouth:
                report.AddDraw(recipient, CardManager.CardKinds.EffectCard);
                report.AddMessage($"{label}：{who} が効果カードを 1 枚引きます");
                break;
            case CardEffect.Connections:
                report.AddDraw(recipient, CardManager.CardKinds.GoodCard);
                report.AddMessage($"{label}：{who} が出品カードを 1 枚引きます");
                break;
            case CardEffect.Indulgence:
                game.AddStatus(recipient, PlayerStatus.Indulgence, 1);
                report.AddMessage($"{label}：{who} が次に受ける効果は 1 回無効になります");
                break;
            case CardEffect.Reversal:
                report.AddReversal();
                report.AddMessage($"{label}：出品ターンの回る向きが逆になります");
                break;
            case CardEffect.FireSale:
                int discount = random.Next(1, 6);
                game.AddStatus(recipient, PlayerStatus.FireSaleDiscount, discount);
                report.AddMessage($"{label}：{who} の次の出品は最低出品額が {discount} 下がります");
                break;
            case CardEffect.Pressure:
                game.AddStatus(recipient, PlayerStatus.Pressure, 1);
                report.AddMessage($"{label}：{who} は出品カードのドローが 1 回できません");
                break;
            case CardEffect.Markdown:
                ApplyMarkdown(label, recipient, seller, report);
                break;
            case CardEffect.SecondTurn:
                game.AddStatus(recipient, PlayerStatus.ExtraTurn, 1);
                report.AddMessage($"{label}：{who} はこのターンの後にもう一度出品します");
                break;
            case CardEffect.Interference:
                game.AddStatus(recipient, PlayerStatus.Interference, 1);
                report.AddMessage($"{label}：{who} は次の出品で効果カードを付けられません");
                break;
            case CardEffect.Appraisal:
                ApplyAppraisal(label, recipient, report);
                break;
            case CardEffect.GoldenBoon:
                int before = game.TryGet(recipient, out PlayerState state) ? state.Coins : 0;
                game.AddCoins(recipient, GoldenBoonCoins);
                report.AddCoinChange(recipient, before, before + GoldenBoonCoins);
                report.AddMessage($"{label}：{who} のコインが {GoldenBoonCoins} 枚増えました");
                break;
            case CardEffect.GraceToken:
                game.AddPassRights(recipient, GraceTokenPassRights);
                report.AddMessage($"{label}：{who} の見送り権が {GraceTokenPassRights} 枚増えました");
                break;
            case CardEffect.AppraisalAll:
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
            report.AddMessage($"{label}：出品者が受け取ったため効果はありません");
            return;
        }

        bool sellerEarns = game.SellerEarnsProceeds;
        int paid = game.PayProceeds(recipient, seller, random.Next(1, 4));
        report.AddMessage(sellerEarns
            ? $"{label}：{nameOf(recipient)} が {nameOf(seller)} に {paid} コインを追加で払いました"
            : $"{label}：{nameOf(recipient)} が {paid} コインを追加で払いました（最後の 1v1 のため出品者には入りません）");
    }

    /// <summary>鑑定：受け取った人の手札の効果カードを 1 枚ランダムで公開する</summary>
    void ApplyAppraisal(string label, int recipient, EffectReport report)
    {
        CardData[] cards = EffectCardsInHand(recipient);
        if (cards.Length == 0)
        {
            report.AddMessage($"{label}：{nameOf(recipient)} は公開する効果カードを持っていません");
            return;
        }

        CardData picked = cards[random.Next(cards.Length)];
        report.AddReveal(recipient, $"{nameOf(recipient)} の効果カード（{label}）", new[] { picked });
    }

    /// <summary>全鑑定・全横やり・全圧力を、受け取った人以外の生きている全員（出品者も含む）にかける</summary>
    void ApplyToOthers(CardEffect effect, int recipient, EffectReport report)
    {
        string label = LabelOf(effect);
        int[] targets = game.AlivePlayers
            .Select(player => player.PlayerNumber)
            .Where(player => player != recipient)
            .ToArray();
        if (targets.Length == 0)
        {
            report.AddMessage($"{label}：対象のプレイヤーがいません");
            return;
        }

        var affected = new List<string>();
        foreach (int target in targets)
        {
            if (game.TryUseStatus(target, PlayerStatus.Indulgence))
            {
                report.AddMessage($"{label}：{nameOf(target)} は免罪符で無効にしました");
                continue;
            }

            switch (effect)
            {
                case CardEffect.AppraisalAll:
                    CardData[] cards = EffectCardsInHand(target);
                    if (cards.Length == 0)
                        report.AddMessage($"{label}：{nameOf(target)} は効果カードを持っていません");
                    else
                        report.AddReveal(target, $"{nameOf(target)} の効果カード（{label}）", cards);
                    break;
                case CardEffect.InterferenceAll:
                    game.AddStatus(target, PlayerStatus.Interference, 1);
                    affected.Add(nameOf(target));
                    break;
                case CardEffect.PressureAll:
                    game.AddStatus(target, PlayerStatus.Pressure, 1);
                    affected.Add(nameOf(target));
                    break;
            }
        }

        if (affected.Count == 0)
            return;
        string names = string.Join("・", affected);
        report.AddMessage(effect == CardEffect.InterferenceAll
            ? $"{label}：{names} は次の出品で効果カードを付けられません"
            : $"{label}：{names} は出品カードのドローが 1 回できません");
    }

    CardData[] EffectCardsInHand(int player)
    {
        if (!game.TryGet(player, out PlayerState state))
            return Array.Empty<CardData>();

        return state.Hand.Where(card => !card.IsGoods && card.Data != null).Select(card => card.Data).ToArray();
    }

    static string LabelOf(CardEffect effect)
    {
        switch (effect)
        {
            case CardEffect.AppraisalAll: return "全鑑定";
            case CardEffect.InterferenceAll: return "全横やり";
            case CardEffect.PressureAll: return "全圧力";
            default: return effect.ToString();
        }
    }
}
