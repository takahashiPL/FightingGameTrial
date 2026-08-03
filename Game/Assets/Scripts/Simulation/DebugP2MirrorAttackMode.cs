namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// P2鏡写しDebug時に、P1の攻撃ボタンをどうP2入力へ変換するか。
    ///
    /// 異技Ground Clashを再現するための検証専用設定です（本番AIではない）。
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
        NoAttack = 2
    }
}
