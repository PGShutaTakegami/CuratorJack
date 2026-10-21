using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Unity Gaming Services の初期化と匿名サインインを 1 回だけ行う。
/// </summary>
public static class OnlineServices
{
    static Task signInTask;

    public static Task EnsureSignedInAsync()
    {
        if (signInTask == null || signInTask.IsFaulted || signInTask.IsCanceled)
            signInTask = SignInAsync();

        return signInTask;
    }

    static async Task SignInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(ProfileName()));

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Debug.Log($"[Online] サインインしました（PlayerId={AuthenticationService.Instance.PlayerId}）");
    }

    /// <summary>同じ PC で複数起動したテスト用ウィンドウも別プレイヤーとして扱われるよう、起動元ごとにプロフィールを分ける。</summary>
    static string ProfileName()
    {
        if (Application.isEditor)
            return "editor";

        return LaunchArguments.TryGetValue(LaunchArguments.WindowIndexKey, out string index) && int.TryParse(index, out int number)
            ? $"window{number}"
            : "default";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        signInTask = null;
    }
}
