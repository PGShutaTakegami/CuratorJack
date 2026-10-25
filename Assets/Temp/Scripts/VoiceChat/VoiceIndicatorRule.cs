/// <summary>VC のマイクマークの出し方</summary>
public enum VoiceIndicatorState
{
    /// <summary>出さない</summary>
    Hidden,

    /// <summary>声を送っているが、声は入っていない（薄暗いマイク）</summary>
    Idle,

    /// <summary>声が入っている（白いマイク）</summary>
    Speaking,

    /// <summary>自分の画面でミュートしている（赤い斜線付きのマイク）</summary>
    Muted
}

/// <summary>
/// VC のマイクマークの出し方を決める。
/// VC に入っていない人には出さない。ミュートしている人にはずっと出す。それ以外は、声を送っている間（プッシュトゥトークのキーを押している／常時）だけ出す。
/// </summary>
public static class VoiceIndicatorRule
{
    public static VoiceIndicatorState Decide(bool hasVoice, bool isMuted, bool isTransmitting, bool isSpeaking)
    {
        if (!hasVoice)
            return VoiceIndicatorState.Hidden;
        if (isMuted)
            return VoiceIndicatorState.Muted;
        if (!isTransmitting)
            return VoiceIndicatorState.Hidden;
        return isSpeaking ? VoiceIndicatorState.Speaking : VoiceIndicatorState.Idle;
    }
}
