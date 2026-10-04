namespace EnvironmentSwitcher
{
    public enum GameEnvironment
    {
        Development = 0,
        Staging = 1,
        Release = 2,
        Local = 3,
        LocalNet = 4,
        OnlineNet = 5
    }

    /// <summary>マルチプレイの通信方式。</summary>
    public enum EnvironmentNetworkMode
    {
        None = 0,
        Local = 1,
        LocalNet = 2,
        OnlineNet = 3
    }

    /// <summary>
    /// Local / LocalNet / OnlineNet は Development の追加版として扱う。
    /// Dev の機能（DEBUG パネル・ログ等）をすべて引き継ぎ、通信方式だけが加わる。
    /// </summary>
    public static class GameEnvironmentExtensions
    {
        public static bool IsDevelopmentFamily(this GameEnvironment environment)
        {
            return environment == GameEnvironment.Development
                   || environment.IsDevelopmentExtension();
        }

        public static bool IsDevelopmentExtension(this GameEnvironment environment)
        {
            return environment == GameEnvironment.Local
                   || environment == GameEnvironment.LocalNet
                   || environment == GameEnvironment.OnlineNet;
        }

        /// <summary>Define を併用する元環境。追加版でなければ null。</summary>
        public static GameEnvironment? GetBaseEnvironment(this GameEnvironment environment)
        {
            return environment.IsDevelopmentExtension()
                ? GameEnvironment.Development
                : (GameEnvironment?)null;
        }

        public static EnvironmentNetworkMode ToNetworkMode(this GameEnvironment environment)
        {
            switch (environment)
            {
                case GameEnvironment.Local:
                    return EnvironmentNetworkMode.Local;
                case GameEnvironment.LocalNet:
                    return EnvironmentNetworkMode.LocalNet;
                case GameEnvironment.OnlineNet:
                    return EnvironmentNetworkMode.OnlineNet;
                default:
                    return EnvironmentNetworkMode.None;
            }
        }

        /// <summary>HUD 表示用の短いラベル（例: Dev / Dev:Local / Stg）。</summary>
        public static string ToShortLabel(this GameEnvironment environment)
        {
            switch (environment)
            {
                case GameEnvironment.Staging:
                    return "Stg";
                case GameEnvironment.Release:
                    return "Prod";
                case GameEnvironment.Development:
                    return "Dev";
                default:
                    return $"Dev:{environment}";
            }
        }
    }
}
