namespace FightingGameTrial.Combat
{
    /// <summary>
    /// 1 CombatFrame の Hit 解決結果です。
    ///
    /// 現行: NormalHit / Clash（地上攻撃同士の Ground Clash）。
    /// Air を含む双方候補は未対応のため None のまま（結果未適用）。
    /// </summary>
    public enum DebugHitResolutionType
    {
        None = 0,
        NormalHit = 1,
        Clash = 2
    }
}
