using System;

/// <summary>
/// シーンをまたいだ遷移要求を仲介する。要求側は受け取り側のシーンを直接参照しない。
/// </summary>
public static class SceneTransitionEvents
{
    /// <summary>
    /// タイトルへ戻る要求
    /// </summary>
    public static event Action ReturnToTitleRequested;

    /// <summary>
    /// タイトルへ戻る要求を通知する。受け取り側がいなければ false を返す
    /// </summary>
    public static bool RequestReturnToTitle()
    {
        if (ReturnToTitleRequested == null)
            return false;

        ReturnToTitleRequested.Invoke();
        return true;
    }
}
