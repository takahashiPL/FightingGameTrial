namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// P2の立ちガード検証用Debug設定です（本番のP2操作・AIではない）。
    ///
    /// 最小実装: Normal / StandGuard のみ。
    /// しゃがみガード・後ろ入力ガード・Just Guard は後工程。
    /// StandGuard 時は P2 が接地・非攻撃・非CombatReaction・相手向きなら、
    /// 立ちガード可能な技の片側候補を Guard として解決します。
    /// </summary>
    public enum DebugP2StanceGuardMode
    {
        /// <summary>通常。Guard分岐を使わず、従来どおり Normal Hit 等。</summary>
        Normal = 0,

        /// <summary>
        /// 検証用に P2 を立ちガード固定（接触時判定）。
        /// 正式な後ろ入力ガードではない。
        /// </summary>
        StandGuard = 1
    }
}
