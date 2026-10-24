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
    /// Local / LocalNet は Development の追加版、OnlineNet は Staging の追加版として扱う。
    /// 元環境の機能（Dev なら DEBUG パネル・ログ等、Stg なら右下の表示のみ）をすべて引き継ぎ、通信方式だけが加わる。
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
                   || environment == GameEnvironment.LocalNet;
        }

        public static bool IsStagingFamily(this GameEnvironment environment)
        {
            return environment == GameEnvironment.Staging
                   || environment.IsStagingExtension();
        }

        public static bool IsStagingExtension(this GameEnvironment environment)
        {
            return environment == GameEnvironment.OnlineNet;
        }

        /// <summary>Define を併用する元環境。追加版でなければ null。</summary>
        public static GameEnvironment? GetBaseEnvironment(this GameEnvironment environment)
        {
            if (environment.IsDevelopmentExtension())
            {
                return GameEnvironment.Development;
            }

            if (environment.IsStagingExtension())
            {
                return GameEnvironment.Staging;
            }

            return null;
        }

        /// <summary>Staging / Release は配布して別々の PC で遊ぶため、オンライン（OnlineNet）で通信する。</summary>
        public static EnvironmentNetworkMode ToNetworkMode(this GameEnvironment environment)
        {
            switch (environment)
            {
                case GameEnvironment.Local:
                    return EnvironmentNetworkMode.Local;
                case GameEnvironment.LocalNet:
                    return EnvironmentNetworkMode.LocalNet;
                case GameEnvironment.OnlineNet:
                case GameEnvironment.Staging:
                case GameEnvironment.Release:
                    return EnvironmentNetworkMode.OnlineNet;
                default:
                    return EnvironmentNetworkMode.None;
            }
        }

        /// <summary>HUD 表示用の短いラベル（例: Dev / Dev:Local / Stg）。Stg の追加版も Stg と表示する。</summary>
        public static string ToShortLabel(this GameEnvironment environment)
        {
            switch (environment)
            {
                case GameEnvironment.Staging:
                case GameEnvironment.OnlineNet:
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
