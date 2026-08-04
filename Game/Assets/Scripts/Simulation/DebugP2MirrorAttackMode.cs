namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// P2鏡写しDebug時に、P1の攻撃ボタンをどうP2入力へ変換するか。
    ///
    /// 異技Ground Clash／P1 Guard検証のための検証専用設定です（本番AIではない）。
    /// StartJPunch / StartGroundKick を直接呼ばず、SimulationInputState の Attack / Kick だけを変え、
    /// P2の通常開始条件（接地・エッジ・同時押し優先など）をそのまま通します。
    /// </summary>
    public enum DebugP2MirrorAttackMode
    {
        /// <summary>
        /// 同技確認用。P1 J→P2 J、P1 K→P2 K（双方接地時のみ）。
        /// </summary>
        SameAsP1 = 0,

        /// <summary>
        /// 異技Clash確認用。P1 J→P2 K、P1 K→P2 J（双方接地時のみ）。
        /// </summary>
        SwapPunchAndKick = 1,

        /// <summary>
        /// 片側Hit確認用。攻撃ボタンは渡さず、左右・Jump鏡写しだけ残す。
        /// </summary>
        NoAttack = 2,

        /// <summary>
        /// P1 Guard検証用。P1攻撃を鏡写しせず、P2だけが Delay 間隔で JPunch を繰り返す。
        /// 方向は Neutral 固定。Inspector 往復なしで Back／Neutral などを比較できる。
        /// </summary>
        JPunchAfterDelay = 3,

        /// <summary>
        /// 異種地上技Clash検証用（単発）。P2 GroundKick を先に、5CF後に P1 JPunch を
        /// SimulationInputState 経由で発火し、最初の Active／候補 CF を揃える。
        /// Mirror Replayではない。方向は P2 Neutral 固定。
        /// </summary>
        P1JPunchP2GroundKickClash = 4
    }
}
