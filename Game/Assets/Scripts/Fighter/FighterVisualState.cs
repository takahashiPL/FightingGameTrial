namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 戦闘状態から見た目側へ渡す Visual State です。
    ///
    /// 内部状態は細かく分けます（素材節約のために統合しない）。
    /// 表示素材は複数状態で共有してよい（例: WalkForward/Backward で同じ歩行列）。
    /// 状態の決定は Session、Sprite 割り当ては Visual の責務です。
    ///
    /// HitStun / KO は専用 State を持ちつつ、専用 Sprite が無い間は Idle 画像へ fallback します。
    /// 色による被弾／KO 表現（Participant.ApplyDisplayColor）は維持します。
    /// </summary>
    public enum FighterVisualState
    {
        Idle = 0,
        WalkForward = 1,
        WalkBackward = 2,
        Attack = 3,
        JumpRise = 4,
        JumpFall = 5,
        Landing = 6,
        JumpStart = 7,
        JumpApex = 8,
        HitStun = 9,
        KO = 10,
        /// <summary>地上 Kick 攻撃ポーズ。Attack（Punch）とは別 Sequence を使う。</summary>
        Kick = 11
    }
}
