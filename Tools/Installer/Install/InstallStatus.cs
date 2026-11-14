namespace CJInstaller.Install
{
    /// <summary>インストールの進み具合（Fraction は 0〜1。割合が分からないときは null）</summary>
    readonly struct InstallStatus
    {
        public InstallStatus(string message, double? fraction)
        {
            Message = message;
            Fraction = fraction;
        }

        public string Message { get; }

        public double? Fraction { get; }
    }
}
