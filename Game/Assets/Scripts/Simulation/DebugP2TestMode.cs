namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// Build／画面右上 UI 用の P2 検証モードです（本番 AI ではない）。
    ///
    /// 個別の Debug フィールドを UI から直接いじらず、
    /// SimulationSession.ApplyP2TestMode が既存 Assist／Mirror／Guard 設定へ翻訳します。
    /// Dropdown 末尾の RESET は本 enum に含めず、UI 側の一時コマンドとして扱います。
    /// 既存の DebugP2MirrorAttackMode / DebugP2StanceGuardMode を置き換えるものではありません。
    /// </summary>
    public enum DebugP2TestMode
    {
        /// <summary>検証OFF。Mirror／Assist／StandGuard をすべて解除し P2 は Neutral。</summary>
        NoAction = 0,

        /// <summary>
        /// Mirror ON + SameAsP1。P1 の J で P2 も JPunch（既存鏡写し経路）。
        /// </summary>
        MirrorJPunch = 1,

        /// <summary>
        /// Mirror ON + SameAsP1。P1 の K で P2 も GroundKick（既存鏡写し経路）。
        /// MirrorJPunch と同じ内部設定。UI上は確認したい技を明示するための項目。
        /// </summary>
        MirrorGroundKick = 2,

        /// <summary>
        /// Mirror ON + P1JPunchP2GroundKickClash。既存の異種地上 Clash 単発 Assist。
        /// </summary>
        JPunchVsGroundKick = 3,

        /// <summary>
        /// Mirror ON + NoAttack + AirKick Assist。既存の P2 Air Kick 検証経路。
        /// </summary>
        P2AirKick = 4,

        /// <summary>
        /// Mirror ON + NoAttack + StandGuard。既存の P2 Debug 立ちガード経路。
        /// </summary>
        StandGuard = 5
    }
}
