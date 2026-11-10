using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 自分（この画面を操作している人）の操作役。画面の UI と手札のクリックを、進行中の手続きへつなぐ。
/// 手札は出品者として出品する商品を決めているときだけ使える（それ以外は見る・広げるだけ）。
/// 出品の確定ボタンは出品できる間、手札を広げているか確定を押せるときだけ画面の右端に表示し、その下にやり直しボタンを出す。
/// オークションでは金額を 0 から W/S・↑/↓・十字キー上下で指定し（長押しで連続して変わる）、入札できる額のときだけ入札ボタンで入札する。
/// コントローラーは A で確定・入札、B で降りる、Y で増額の切り替え（画面のボタンが押せるときだけ）。
/// アイテムはオークションの間だけ（参加していなくても、生きていれば）、マスのクリックかキーボードの 1/2/3 で使う。
/// 相手が要るアイテムは、選ぶ画面で自分以外の生きている人を選んでから使う（右クリック・同じマスをもう一度・使えなくなったらやめる）。
/// 確定・入札・降りる・アイテムは、連打しても PressThrottle の間隔より短い間隔では送らない。
/// ポーズ画面を開いている間はキー・コントローラーの操作を受け付けない（メニューの選択と重ならないように）。脱落した後は出品を受け持たない。
/// </summary>
public class LocalPlayerAgent : MonoBehaviour, IPlayerAgent
{
    [SerializeField] private GameManager game;

    [SerializeField] private TurnUI ui;

    [SerializeField] private LocalHandPresenter hand;

    [Tooltip("状態の表示に出す自分の名前")]
    [SerializeField] private PlayerNameDirectory names;

    [Tooltip("開いている間は金額のキー操作を受け付けない")]
    [SerializeField] private PauseMenu pause;

    [Tooltip("自分のアイテムのマス")]
    [SerializeField] private ItemSlotsView items;

    [Tooltip("相手が要るアイテム（リモコン）の相手を選ぶ画面")]
    [SerializeField] private ItemTargetPicker targetPicker;

    ListingSession listing;
    AuctionModel auction;
    bool isItemPhase;
    bool isOwnListing;
    int pickingSlot = -1;
    IPlayerCommands commands;
    int amount;
    readonly KeyRepeat raiseKey = new KeyRepeat();
    readonly KeyRepeat lowerKey = new KeyRepeat();
    readonly PressThrottle confirmThrottle = new PressThrottle();
    readonly PressThrottle resetThrottle = new PressThrottle();
    readonly PressThrottle bidThrottle = new PressThrottle();
    readonly PressThrottle foldThrottle = new PressThrottle();
    readonly PressThrottle itemThrottle = new PressThrottle();

    public int PlayerNumber { get; private set; } = PlayerRegistry.NoPlayer;

    /// <param name="playerCommands">操作を送る窓口（反映は窓口が行うため、ここでは手続きを直接書き換えない）</param>
    public void Initialize(int playerNumber, IPlayerCommands playerCommands)
    {
        PlayerNumber = playerNumber;
        commands = playerCommands;
    }

    void Awake()
    {
        ui.ConfirmClicked += HandleConfirm;
        ui.ResetClicked += HandleReset;
        ui.BidClicked += HandleBid;
        ui.FoldClicked += HandleFold;
        ui.RaiseClicked += HandleRaise;
        hand.CardClicked += HandleCardClicked;
        items.SlotClicked += HandleItemClicked;
        targetPicker.Picked += HandleTargetPicked;
        game.PlayerDied += HandlePlayerDied;
    }

    void OnDestroy()
    {
        hand.CardClicked -= HandleCardClicked;
        items.SlotClicked -= HandleItemClicked;
        targetPicker.Picked -= HandleTargetPicked;
        game.PlayerDied -= HandlePlayerDied;
    }

    /// <summary>自分が死んだら操作中の案内とボタンを片付ける（進行状況の表示は TurnFlowController が続ける）</summary>
    void HandlePlayerDied(PlayerState player)
    {
        if (player.PlayerNumber == PlayerNumber)
            EndPhase();
    }

    public void BeginListing(ListingSession session)
    {
        EndPhase();
        if (!game.IsAlive(PlayerNumber))
            return;

        listing = session;
        listing.Changed += RefreshListing;
        RefreshListing();
    }

    public void BeginAuction(AuctionModel model)
    {
        EndPhase();
        isItemPhase = game.IsAlive(PlayerNumber);
        isOwnListing = model.Listing.Seller == PlayerNumber;
        if (!model.Participants.Contains(PlayerNumber))
        {
            ui.SetPrompt("オークションを観戦中");
            return;
        }

        auction = model;
        amount = 0;
        hand.Collapse();
        ui.ShowAuction(true);
        RefreshAuction();
    }

    public void EndPhase()
    {
        if (listing != null)
            listing.Changed -= RefreshListing;
        listing = null;
        auction = null;
        isItemPhase = false;
        isOwnListing = false;
        items.SetUsable(false, false);
        CancelTargetPick();
        ui.ShowConfirm(false, false, false);
        ui.SetBidInteractable(false);
        ui.ShowRaise(false, false);
        ui.ShowAuction(false);
        ui.SetPrompt(string.Empty);
    }

    void Update()
    {
        if (game.TryGet(PlayerNumber, out PlayerState me))
            ui.SetStatus($"{names.NameOf(PlayerNumber)}  コイン {me.Coins}  落札 {me.Collection.Count}{(me.IsAlive ? string.Empty : "  （観戦中）")}");

        if (listing != null)
        {
            ui.RevealConfirm(ConfirmRevealed);
            if (!pause.BlocksGameInput)
                HandleListingButtons();
        }

        bool itemsUsable = isItemPhase && game.IsAlive(PlayerNumber) && !pause.BlocksGameInput;
        items.SetUsable(itemsUsable, isOwnListing);
        if (!itemsUsable)
            CancelTargetPick();
        else if (targetPicker.IsPicking && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            CancelTargetPick();
        else
            HandleItemKeys();

        if (auction == null)
            return;

        if (!auction.IsActive(PlayerNumber))
        {
            ui.ShowAuction(false);
            ui.SetBidInteractable(false);
            ui.SetPrompt("オークションを観戦中");
            auction = null;
            return;
        }

        bool acceptsInput = !ui.IsAuctionHidden && !pause.BlocksGameInput;
        if (acceptsInput)
            amount += ReadAmountSteps() * auction.Listing.BidStep;
        RefreshAuction();
        if (acceptsInput)
            HandleAuctionButtons();
    }

    /// <summary>出品中のコントローラー操作。A で確定、Y で増額の切り替え</summary>
    void HandleListingButtons()
    {
        if (ControllerInput.SubmitPressed)
            ui.PressConfirm();
        else if (ControllerInput.AltPressed)
            ui.PressRaise();
    }

    /// <summary>オークション中のコントローラー操作。A で入札、B で降りる</summary>
    void HandleAuctionButtons()
    {
        if (ControllerInput.SubmitPressed)
            ui.PressBid();
        else if (ControllerInput.BackPressed)
            ui.PressFold();
    }

    /// <summary>キーボードの 1/2/3 で、左から 1〜3 番目のマスのアイテムを使う</summary>
    void HandleItemKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame)
            HandleItemClicked(0);
        else if (keyboard.digit2Key.wasPressedThisFrame)
            HandleItemClicked(1);
        else if (keyboard.digit3Key.wasPressedThisFrame)
            HandleItemClicked(2);
    }

    void HandleItemClicked(int slot)
    {
        if (!isItemPhase || !game.IsAlive(PlayerNumber) || pause.BlocksGameInput)
            return;

        if (targetPicker.IsPicking)
        {
            bool sameSlot = slot == pickingSlot;
            CancelTargetPick();
            if (sameSlot)
                return;
        }

        ItemDefinition item = items.ItemAt(slot);
        if (item == null || !items.CanUse(slot))
        {
            SoundEffectPlayer.Play(SoundId.Denied);
            return;
        }

        if (item.NeedsTarget)
            BeginTargetPick(slot, item);
        else
            UseItem(slot, PlayerRegistry.NoPlayer);
    }

    /// <summary>相手が要るアイテムは、使う前に生きている人（自分を選べないアイテムなら自分以外）を選ばせる</summary>
    void BeginTargetPick(int slot, ItemDefinition item)
    {
        int[] candidates = game.Players
            .Where(player => player.IsAlive && (item.CanTargetSelf || player.PlayerNumber != PlayerNumber))
            .Select(player => player.PlayerNumber)
            .ToArray();
        if (candidates.Length == 0)
        {
            SoundEffectPlayer.Play(SoundId.Denied);
            return;
        }

        SoundEffectPlayer.Play(SoundId.CardTap);
        pickingSlot = slot;
        targetPicker.Begin(candidates, TargetPrompt(item));
    }

    static string TargetPrompt(ItemDefinition item) => item.Kind == ItemKind.PowerPlug
        ? "シールドを付ける人を選んでください（右クリックでやめる）"
        : "マヒさせる相手を選んでください（右クリックでやめる）";

    void HandleTargetPicked(int target)
    {
        int slot = pickingSlot;
        CancelTargetPick();
        if (isItemPhase && game.IsAlive(PlayerNumber) && !pause.BlocksGameInput)
            UseItem(slot, target);
    }

    void CancelTargetPick()
    {
        pickingSlot = -1;
        if (targetPicker.IsPicking)
            targetPicker.End();
    }

    void UseItem(int slot, int target)
    {
        if (itemThrottle.TryAccept())
        {
            SoundEffectPlayer.Play(SoundId.CardTap);
            commands.UseItem(PlayerNumber, slot, target);
        }
    }

    /// <summary>W/S・↑/↓・十字キー上下で金額を何段変えるか（長押しで連続して変わる）</summary>
    int ReadAmountSteps()
    {
        Keyboard keyboard = Keyboard.current;
        bool upPressed = ControllerInput.UpPressed;
        bool upHeld = ControllerInput.UpHeld;
        bool downPressed = ControllerInput.DownPressed;
        bool downHeld = ControllerInput.DownHeld;
        if (keyboard != null)
        {
            upPressed |= keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
            upHeld |= keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
            downPressed |= keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame;
            downHeld |= keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
        }

        int ups = raiseKey.Tick(upPressed, upHeld, Time.deltaTime);
        int downs = lowerKey.Tick(downPressed, downHeld, Time.deltaTime);
        return ups - downs;
    }

    void HandleCardClicked(HandCard card)
    {
        bool denied = listing != null && listing.Goods != null && !listing.CanAttachBuffs && !card.IsGoods;
        SoundEffectPlayer.Play(denied ? SoundId.Denied : SoundId.CardTap);
        if (listing == null)
            return;

        if (listing.Goods == null)
            commands.SelectGoods(PlayerNumber, card);
        else if (denied)
            hand.Shake(card);
        else
            commands.AttachBuff(PlayerNumber, card);
    }

    void HandleConfirm()
    {
        if (listing != null && confirmThrottle.TryAccept())
            commands.Confirm(PlayerNumber);
    }

    void HandleReset()
    {
        if (listing != null && listing.CanReset && resetThrottle.TryAccept())
            commands.ResetListing(PlayerNumber);
    }

    /// <summary>確定ボタンを出すか。押せる（橙色の）ときは手札を閉じていても出す</summary>
    bool ConfirmRevealed => hand.IsSpread || listing.CanConfirm;

    void HandleRaise(bool raise)
    {
        if (listing != null)
            commands.SetRaise(PlayerNumber, raise);
    }

    void HandleBid()
    {
        if (auction != null && bidThrottle.TryAccept())
            commands.Bid(PlayerNumber, amount);
    }

    void HandleFold()
    {
        if (auction != null && foldThrottle.TryAccept())
            commands.Fold(PlayerNumber);
    }

    void RefreshListing()
    {
        if (listing == null)
            return;

        ui.ShowConfirm(true, listing.CanConfirm, listing.CanReset);
        ui.RevealConfirm(ConfirmRevealed);
        ui.ShowRaise(listing.CanRaise, listing.IsRaised);
        if (listing.Goods == null)
            ui.SetPrompt("手札を広げて、出品するグッズカードを選んでください");
        else if (!listing.CanAttachBuffs)
            ui.SetPrompt($"{listing.Goods.DisplayName}（最低 {listing.MinPrice}）を出品します。横やりでバフデバフカードは付けられません。確定してください");
        else if (!listing.CanRaise)
            ui.SetPrompt($"{listing.Goods.DisplayName}（最低 {listing.MinPrice}）を出品します。バフデバフカードを使うならクリック、決まったら確定");
        else
            ui.SetPrompt($"{listing.Goods.DisplayName}（最低 {listing.MinPrice}）を出品します。増額するか選んで確定");
    }

    /// <summary>金額は 0〜所持コインの範囲で自由に指定させ、入札できる額かどうかはボタンの可否で示す。最高額の間は降りるを押せない</summary>
    void RefreshAuction()
    {
        int highest = auction.HighestSelectable(PlayerNumber);
        amount = Mathf.Clamp(amount, 0, highest);
        ui.SetBidInteractable(auction.CanBid(PlayerNumber, amount));
        ui.SetFoldInteractable(auction.CanFold(PlayerNumber));
        string drain = auction.DrainsSeller ? $" / 相手から削る {auction.DrainOf(amount)}" : string.Empty;
        ui.SetAuction(amount, $"最低 {auction.Listing.MinPrice} / 最高額 {auction.HighestBid} / 自分 {auction.BidOf(PlayerNumber)} / 所持 {auction.CoinsOf(PlayerNumber)}{drain}");
        if (highest < auction.Listing.MinPrice)
            ui.SetPrompt("コインが足りないため降りるしかありません（見送り権を使います）");
        else if (auction.IsLeading(PlayerNumber))
            ui.SetPrompt("最高額です。さらに金額を上げて入札することもできます（最高額のあいだは降りられません）");
        else if (auction.DrainsSeller)
            ui.SetPrompt("FINAL BATTLE：最低額より多く入札して落札した分だけ、相手のコインを削れます。金額を指定して入札するか、降りるかを選んでください");
        else
            ui.SetPrompt("W/S か ↑/↓（コントローラーは十字キー）で金額を指定して入札するか、降りるかを選んでください（降りると見送り権を使います）");
    }
}
