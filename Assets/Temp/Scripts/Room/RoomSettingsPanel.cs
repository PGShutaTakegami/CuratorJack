using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ルーム設定の画面。人数・待ち時間・VC と、プレイヤー枠（名前・ミュート・キック・CPU で埋める）を表示する。
/// ゲーム開始前はすべて変えられ、ゲーム中はミュートとキックだけ変えられる。キックは部屋主にだけ表示し、自分の枠にはミュートもキックも出さない。
/// 部屋の状態と操作（VC の ON/OFF を含む）は、今のプレイ形式の IRoomControl に任せる。VC の ON/OFF を変えられるのはマルチプレイの部屋主だけ。
/// ミュートは VoiceChatSession に任せ、自分の画面だけで相手の声を聞こえなくする。部屋の VC が ON のとき、CPU 以外のほかの人の枠にだけ出す。
/// </summary>
public class RoomSettingsPanel : MonoBehaviour
{
    [SerializeField] private GameObject view;

    [SerializeField] private Button closeButton;

    [SerializeField] private RoomOptionSelector capacitySelector;

    [SerializeField] private RoomOptionSelector waitTimeSelector;

    [Tooltip("値は 1 が ON、0 が OFF")]
    [SerializeField] private RoomOptionSelector voiceChatSelector;

    [SerializeField] private RoomSlotView[] slots;

    [Tooltip("IRoomControl を持つコンポーネント（SinglePlay / マルチプレイ）")]
    [SerializeField] private MonoBehaviour[] rooms;

    [SerializeField] private LocalRoomOwner roomOwner;

    [SerializeField] private GameStartGate startGate;

    [SerializeField] private GameManager game;

    [SerializeField] private PlayerNameDirectory names;

    [SerializeField] private VoiceChatSession voiceChat;

    readonly List<int> slotPlayers = new List<int>();
    IRoomControl room;

    public bool IsOpen => view.activeSelf;

    public event Action Closed;

    void Awake()
    {
        closeButton.onClick.AddListener(Close);
        capacitySelector.Selected += value => room?.SetCapacity(value);
        waitTimeSelector.Selected += value => room?.SetWaitSeconds(value);
        voiceChatSelector.Selected += value => room?.SetVoiceChat(value != 0);
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            slots[i].MuteClicked += () => ToggleMute(index);
            slots[i].KickClicked += () => Kick(index);
            slots[i].FillClicked += () => room?.FillWithCpu();
            slotPlayers.Add(PlayerRegistry.NoPlayer);
        }

        startGate.Changed += Refresh;
        game.PlayerDied += HandlePlayerDied;
        voiceChat.Changed += Refresh;
        view.SetActive(false);
    }

    void OnDestroy()
    {
        startGate.Changed -= Refresh;
        game.PlayerDied -= HandlePlayerDied;
        voiceChat.Changed -= Refresh;
        SetRoom(null);
    }

    public void Open()
    {
        SetRoom(FindActiveRoom());
        view.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        view.SetActive(false);
        Closed?.Invoke();
    }

    IRoomControl FindActiveRoom()
    {
        foreach (MonoBehaviour candidate in rooms)
        {
            if (candidate is IRoomControl control && control.IsActive)
                return control;
        }

        return null;
    }

    void SetRoom(IRoomControl next)
    {
        if (room != null)
            room.Changed -= Refresh;
        room = next;
        if (room != null)
            room.Changed += Refresh;
    }

    void HandlePlayerDied(PlayerState player) => Refresh();

    void ToggleMute(int slotIndex)
    {
        voiceChat.ToggleMute(slotPlayers[slotIndex]);
    }

    void Kick(int slotIndex)
    {
        room?.Kick(slotPlayers[slotIndex]);
    }

    void Refresh()
    {
        if (!IsOpen || room == null)
            return;

        bool canEditRoom = !startGate.HasStarted;
        IReadOnlyList<RoomMember> members = room.Members;

        capacitySelector.Show(room.Capacity, value => canEditRoom && value >= members.Count && value <= room.MaxCapacity);
        waitTimeSelector.Show(room.WaitSeconds, _ => canEditRoom && room.CanChangeWaitTime);
        voiceChatSelector.Show(room.IsVoiceChatOn ? 1 : 0, _ => canEditRoom && room.CanChangeVoiceChat);

        for (int i = 0; i < slots.Length; i++)
        {
            if (i < members.Count)
                ShowMember(i, members[i]);
            else if (i < room.Capacity)
                ShowEmpty(i, canEditRoom && room.CanFillWithCpu);
            else
                ShowClosed(i);
        }
    }

    void ShowMember(int slotIndex, RoomMember member)
    {
        int player = member.PlayerNumber;
        slotPlayers[slotIndex] = player;
        bool isOthers = !member.IsLocal;
        bool canMute = isOthers && !member.IsCpu && room.IsVoiceChatOn;
        bool canKick = !startGate.HasStarted || game.IsAlive(player);
        slots[slotIndex].ShowMember(names.NameOf(player), canMute, voiceChat.IsMuted(player), isOthers && roomOwner.IsOwner, canKick);
    }

    void ShowEmpty(int slotIndex, bool canFill)
    {
        slotPlayers[slotIndex] = PlayerRegistry.NoPlayer;
        slots[slotIndex].ShowEmpty(canFill);
    }

    void ShowClosed(int slotIndex)
    {
        slotPlayers[slotIndex] = PlayerRegistry.NoPlayer;
        slots[slotIndex].ShowClosed();
    }
}
