using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// マルチプレイの VC（Vivox）。部屋の VC が ON の間だけ、部屋ごとのチャンネルへ入る。シングルプレイでは入らない。
/// マイク・出力デバイス・ノイズ抑制・音量・話し方（プッシュトゥトーク / 常時 / OFF）はオプションの設定を使い、ゲーム中に変えてもすぐ反映する。
/// プッシュトゥトークは操作の割り当てアセットの操作で読み（キーボードとコントローラー）、オプションで変えたキーボードのキーは上書きとして重ねる。
/// ミュートは自分の画面だけで、選んだプレイヤーの声を聞こえなくする（相手やほかの人には影響しない）。
/// 自分が声を送っているかは PlayerVoiceIdentity で全員へ知らせ、プレイヤーごとの「送っているか」「声が入っているか」を表示用に読める。
/// </summary>
public class VoiceChatSession : MonoBehaviour
{
    [SerializeField] private NetworkRoomControl room;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("操作の割り当てアセットのプッシュトゥトーク（キーボードとコントローラー）")]
    [SerializeField] private InputActionReference pushToTalkAction;

    [Tooltip("参加に失敗したとき、次に試すまでの秒数")]
    [SerializeField] private float retrySeconds = 15f;

    [Tooltip("ほかの人のミュートを合わせ直す間隔（秒）。相手の VC 上の ID が後から届いても反映するため")]
    [SerializeField] private float muteCheckSeconds = 0.25f;

    readonly HashSet<int> mutedPlayers = new HashSet<int>();
    readonly List<VivoxParticipant> participants = new List<VivoxParticipant>();
    readonly Dictionary<VivoxParticipant, bool> appliedMutes = new Dictionary<VivoxParticipant, bool>();
    VivoxParticipant selfParticipant;
    GameSettings settings;
    string joiningChannel;
    string joinedChannel;
    bool isBusy;
    bool isDestroyed;
    bool isListening;
    bool areDevicesDirty;
    bool? isTransmitting;
    float nextAttemptTime;
    float nextMuteCheckTime;
    PlayerVoiceIdentity reportedIdentity;

    /// <summary>ミュートが変わった</summary>
    public event Action Changed;

    static IVivoxService Vivox => VivoxService.Instance;

    bool IsJoined => !string.IsNullOrEmpty(joinedChannel);

    InputAction PushToTalk => pushToTalkAction != null ? pushToTalkAction.action : null;

    void Awake()
    {
        ApplySettings(GameSettingsStore.Current);
        GameSettingsStore.Saved += HandleSettingsSaved;
    }

    void OnEnable() => PushToTalk?.Enable();

    void OnDisable() => PushToTalk?.Disable();

    void OnDestroy()
    {
        isDestroyed = true;
        GameSettingsStore.Saved -= HandleSettingsSaved;
        if (isListening)
        {
            Vivox.ParticipantAddedToChannel -= HandleParticipantAdded;
            Vivox.ParticipantRemovedFromChannel -= HandleParticipantRemoved;
        }
        if (IsJoined)
            _ = ShutdownAsync(joinedChannel);
    }

    /// <summary>部屋の VC に入っているか</summary>
    public bool IsInVoiceChat => IsJoined;

    public bool IsMuted(int playerNumber) => mutedPlayers.Contains(playerNumber);

    /// <summary>そのプレイヤーが VC に入っている人か（VC 上の ID がある）。CPU や VC に入っていない人は false</summary>
    public bool HasVoice(int playerNumber)
    {
        PlayerVoiceIdentity identity = IdentityOf(playerNumber);
        return identity != null && !string.IsNullOrEmpty(identity.VoiceId);
    }

    /// <summary>そのプレイヤーが今声を送っているか（プッシュトゥトークのキーを押している／常時）</summary>
    public bool IsTransmitting(int playerNumber)
    {
        PlayerVoiceIdentity identity = IdentityOf(playerNumber);
        return identity != null && identity.IsTransmitting;
    }

    /// <summary>そのプレイヤーの声が今入っているか</summary>
    public bool IsSpeaking(int playerNumber)
    {
        if (playerNumber == registry.LocalPlayerNumber)
            return selfParticipant != null && selfParticipant.SpeechDetected;

        PlayerVoiceIdentity identity = IdentityOf(playerNumber);
        if (identity == null || string.IsNullOrEmpty(identity.VoiceId))
            return false;

        foreach (VivoxParticipant participant in participants)
        {
            if (participant.PlayerId == identity.VoiceId)
                return participant.SpeechDetected;
        }
        return false;
    }

    /// <summary>自分の画面だけで、そのプレイヤーの声のミュートを切り替える。</summary>
    public void ToggleMute(int playerNumber)
    {
        if (playerNumber == PlayerRegistry.NoPlayer)
            return;

        if (!mutedPlayers.Remove(playerNumber))
            mutedPlayers.Add(playerNumber);
        nextMuteCheckTime = 0f;
        Changed?.Invoke();
    }

    void Update()
    {
        string wanted = WantedChannel();
        if (!isBusy)
        {
            if (IsJoined && joinedChannel != wanted)
                _ = LeaveAsync();
            else if (!IsJoined && !string.IsNullOrEmpty(wanted) && Time.unscaledTime >= nextAttemptTime)
                _ = JoinAsync(wanted);
            else if (IsJoined && areDevicesDirty)
                _ = ApplyDevicesAsync();
        }

        if (!IsJoined)
        {
            LocalIdentity()?.SetTransmitting(false);
            return;
        }

        ReportIdentity();
        ApplyTransmission();
        if (Time.unscaledTime < nextMuteCheckTime)
            return;

        nextMuteCheckTime = Time.unscaledTime + muteCheckSeconds;
        ApplyMutes();
    }

    /// <summary>入るべきチャンネル。VC に入らない状況なら空</summary>
    string WantedChannel()
    {
        if (room == null || !room.IsVoiceChatOn || LocalIdentity() == null)
            return string.Empty;

        return room.VoiceChannel;
    }

    PlayerVoiceIdentity LocalIdentity() => IdentityOf(registry.LocalPlayerNumber);

    PlayerVoiceIdentity IdentityOf(int playerNumber)
    {
        return playerNumber != PlayerRegistry.NoPlayer
            && registry.TryGet(playerNumber, out Transform player)
            && player.TryGetComponent(out PlayerVoiceIdentity identity)
            ? identity
            : null;
    }

    async Task JoinAsync(string channel)
    {
        isBusy = true;
        try
        {
            await OnlineServices.EnsureSignedInAsync();
            if (Vivox.InitializationState != VivoxInitializationState.Initialized)
                await Vivox.InitializeAsync();
            ListenParticipants();
            ApplyNoiseSuppression();
            if (!Vivox.IsLoggedIn)
                await Vivox.LoginAsync(new LoginOptions());
            joiningChannel = channel;
            await Vivox.JoinGroupChannelAsync(channel, ChatCapability.AudioOnly);

            joinedChannel = channel;
            if (isDestroyed)
            {
                await ShutdownAsync(channel);
                return;
            }

            isTransmitting = null;
            nextMuteCheckTime = 0f;
            reportedIdentity = null;
            ApplyVolume();
            await ApplyDevicesAsync(false);
            Debug.Log($"[VC] 部屋の VC に参加しました（{channel}）");
        }
        catch (Exception e)
        {
            joiningChannel = null;
            ForgetParticipants();
            nextAttemptTime = Time.unscaledTime + retrySeconds;
            Debug.LogWarning($"[VC] VC に参加できませんでした。{retrySeconds} 秒後にもう一度試します: {e.Message}");
        }
        finally
        {
            isBusy = false;
        }
    }

    async Task LeaveAsync()
    {
        isBusy = true;
        string channel = joinedChannel;
        try
        {
            await Vivox.LeaveChannelAsync(channel);
            Debug.Log("[VC] 部屋の VC から抜けました");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VC] VC から抜けられませんでした: {e.Message}");
        }
        finally
        {
            joiningChannel = null;
            joinedChannel = null;
            ForgetParticipants();
            isBusy = false;
        }
    }

    void ListenParticipants()
    {
        if (isListening)
            return;

        isListening = true;
        Vivox.ParticipantAddedToChannel += HandleParticipantAdded;
        Vivox.ParticipantRemovedFromChannel += HandleParticipantRemoved;
    }

    void HandleParticipantAdded(VivoxParticipant participant)
    {
        if (participant.ChannelName != joiningChannel)
            return;

        if (participant.IsSelf)
        {
            selfParticipant = participant;
            return;
        }

        if (participants.Contains(participant))
            return;

        participants.Add(participant);
        nextMuteCheckTime = 0f;
    }

    void HandleParticipantRemoved(VivoxParticipant participant)
    {
        if (participant == selfParticipant)
            selfParticipant = null;
        participants.Remove(participant);
        appliedMutes.Remove(participant);
    }

    void ForgetParticipants()
    {
        selfParticipant = null;
        participants.Clear();
        appliedMutes.Clear();
    }

    /// <summary>シーンを離れるとき。Vivox はシーンをまたいで残るため、チャンネルから抜けてログアウトする</summary>
    static async Task ShutdownAsync(string channel)
    {
        try
        {
            await Vivox.LeaveChannelAsync(channel);
            await Vivox.LogoutAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VC] VC の終了に失敗しました: {e.Message}");
        }
    }

    void HandleSettingsSaved(GameSettings saved) => ApplySettings(saved);

    void ApplySettings(GameSettings next)
    {
        bool devicesChanged = settings == null
            || settings.MicrophoneDevice != next.MicrophoneDevice
            || settings.OutputDevice != next.OutputDevice;
        settings = next;
        InputBindings.OverrideKeyboardKey(PushToTalk, PushToTalkKeyParser.Parse(settings.PushToTalkKey));
        areDevicesDirty |= devicesChanged;
        if (!IsJoined)
            return;

        ApplyNoiseSuppression();
        ApplyVolume();
    }

    void ApplyNoiseSuppression()
    {
        if (Vivox.InitializationState == VivoxInitializationState.Initialized)
            Vivox.VivoxGlobalAudioSettings.NoiseSuppressionEnabled = settings.NoiseSuppression;
    }

    /// <summary>VC の音量にマスター音量を掛けたもの（Vivox の声は Unity の AudioListener を通らないため）</summary>
    void ApplyVolume()
    {
        float volume = settings.MasterVolume * settings.VoiceChatVolume;
        Vivox.SetOutputDeviceVolume(VoiceVolume.ToVivox(volume));
        if (VoiceVolume.IsSilent(volume))
            Vivox.MuteOutputDevice();
        else
            Vivox.UnmuteOutputDevice();
    }

    Task ApplyDevicesAsync() => ApplyDevicesAsync(true);

    async Task ApplyDevicesAsync(bool lockWhileRunning)
    {
        areDevicesDirty = false;
        if (lockWhileRunning)
            isBusy = true;
        try
        {
            VivoxInputDevice input = VoiceDeviceMatcher.Find(Vivox.AvailableInputDevices, settings.MicrophoneDevice, d => d.DeviceName, d => d.DeviceID);
            if (input != null && input.DeviceID != Vivox.ActiveInputDevice?.DeviceID)
                await Vivox.SetActiveInputDeviceAsync(input);

            VivoxOutputDevice output = VoiceDeviceMatcher.Find(Vivox.AvailableOutputDevices, settings.OutputDevice, d => d.DeviceName, d => d.DeviceID);
            if (output != null && output.DeviceID != Vivox.ActiveOutputDevice?.DeviceID)
                await Vivox.SetActiveOutputDeviceAsync(output);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VC] デバイスを切り替えられませんでした: {e.Message}");
        }
        finally
        {
            if (lockWhileRunning)
                isBusy = false;
        }
    }

    /// <summary>ほかの人が、自分の声を自分のプレイヤーと結び付けられるように、VC 上の ID を送る</summary>
    void ReportIdentity()
    {
        PlayerVoiceIdentity identity = LocalIdentity();
        if (identity == null || identity == reportedIdentity || !AuthenticationService.Instance.IsSignedIn)
            return;

        identity.Report(AuthenticationService.Instance.PlayerId);
        reportedIdentity = identity;
    }

    void ApplyTransmission()
    {
        bool transmit = ShouldTransmit();
        LocalIdentity()?.SetTransmitting(transmit);
        if (isTransmitting == transmit)
            return;

        isTransmitting = transmit;
        if (transmit)
            Vivox.UnmuteInputDevice();
        else
            Vivox.MuteInputDevice();
    }

    bool ShouldTransmit()
    {
        switch (settings.TalkMode)
        {
            case VoiceTalkMode.Open:
                return true;
            case VoiceTalkMode.PushToTalk:
                return PushToTalk != null && PushToTalk.IsPressed();
            default:
                return false;
        }
    }

    void ApplyMutes()
    {
        foreach (VivoxParticipant participant in participants)
        {
            bool mute = mutedPlayers.Contains(PlayerNumberOf(participant.PlayerId));
            if (appliedMutes.TryGetValue(participant, out bool applied) && applied == mute)
                continue;

            appliedMutes[participant] = mute;
            if (mute)
                participant.MutePlayerLocally();
            else
                participant.UnmutePlayerLocally();
        }
    }

    int PlayerNumberOf(string voiceId)
    {
        foreach (int number in registry.PlayerNumbers)
        {
            PlayerVoiceIdentity identity = IdentityOf(number);
            if (identity != null && identity.VoiceId == voiceId)
                return number;
        }

        return PlayerRegistry.NoPlayer;
    }
}
