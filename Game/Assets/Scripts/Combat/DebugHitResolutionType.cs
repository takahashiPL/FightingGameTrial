namespace FightingGameTrial.Combat
{
    /// <summary>
    /// 1 CombatFrame の Hit 解決結果です。
    ///
    /// 今回実装: NormalHit / Clash。
    /// 将来候補（未実装）: PriorityWin / Guarded / Invulnerable など。
    /// </summary>
    public enum DebugHitResolutionType
    {
        None = 0,
        NormalHit = 1,
        Clash = 2
    }
}
