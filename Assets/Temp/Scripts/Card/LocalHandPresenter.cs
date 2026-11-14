using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 自分の手札（GameManager の状態）を画面の手札（CardHandView）に映す。
/// 手札に加わったカードは引く演出つきで表示し、消費されたカードは画面からも消す。
/// 広げた手札のカードがクリックされたら、そのカードを通知する。
/// </summary>
public class LocalHandPresenter : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private CardHandView view;

    readonly Dictionary<HandCard, Image> shown = new Dictionary<HandCard, Image>();

    public event Action<HandCard> CardClicked;

    public bool IsAnimating => view.IsMoving;

    public bool IsSpread => view.IsSpread;

    /// <summary>広げている手札を重ねた状態に戻す</summary>
    public void Collapse()
    {
        if (view.IsSpread)
            view.SetSpread(false);
    }

    /// <summary>手札のカードを画面に映しているカードの画像（映していなければ false）</summary>
    public bool TryGetImage(HandCard card, out Image image)
    {
        return shown.TryGetValue(card, out image);
    }

    /// <summary>手札のカードを震わせる（使えないカードを押したときの合図）</summary>
    public void Shake(HandCard card)
    {
        if (shown.TryGetValue(card, out Image image))
            view.Shake(image);
    }

    void Awake()
    {
        game.HandCardAdded += HandleAdded;
        game.HandCardRemoved += HandleRemoved;
        view.CardClicked += HandleViewClicked;
    }

    void OnDestroy()
    {
        game.HandCardAdded -= HandleAdded;
        game.HandCardRemoved -= HandleRemoved;
        view.CardClicked -= HandleViewClicked;
    }

    bool IsLocal(PlayerState player) => player.PlayerNumber == registry.LocalPlayerNumber;

    void HandleAdded(PlayerState player, HandCard card)
    {
        if (!IsLocal(player))
            return;

        SoundEffectPlayer.Play(SoundId.CardDraw);
        Image image = view.InsertCard(ShownBefore(player, card));
        if (image.TryGetComponent(out CardManager manager))
        {
            manager.SetKind(card.Kind);
            if (card.Data != null)
                manager.SetData(card.Data);
            manager.SetSpecial(card.Special);
        }
        shown[card] = image;
    }

    /// <summary>手札でそのカードより左にあり、画面に映しているカードの枚数（画面の並びでの位置）</summary>
    int ShownBefore(PlayerState player, HandCard card)
    {
        int count = 0;
        foreach (HandCard held in player.Hand)
        {
            if (held == card)
                break;
            if (shown.ContainsKey(held))
                count++;
        }
        return count;
    }

    void HandleRemoved(PlayerState player, HandCard card)
    {
        if (!IsLocal(player) || !shown.TryGetValue(card, out Image image))
            return;

        shown.Remove(card);
        view.RemoveCard(image);
    }

    void HandleViewClicked(Image image)
    {
        foreach (KeyValuePair<HandCard, Image> pair in shown)
        {
            if (pair.Value == image)
            {
                CardClicked?.Invoke(pair.Key);
                return;
            }
        }
    }
}
