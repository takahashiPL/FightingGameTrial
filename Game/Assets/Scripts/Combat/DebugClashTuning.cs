namespace FightingGameTrial.Combat
{
    /// <summary>
    /// Ground Clash の仮数値です（1か所で確認・変更する）。
    /// 技相性による分岐はまだせず、双方成立なら常にこの値を使います。
    /// </summary>
    public static class DebugClashTuning
    {
        /// <summary>双方 Damage。</summary>
        public const int Damage = 0;

        /// <summary>互いに離れる方向の横 Knockback 初速（絶対値）。</summary>
        public const float HorizontalKnockback = 0.16f;

        /// <summary>Clash 後の短い行動不能（CombatFrame）。専用 ClashRecoil で適用（通常 HitStun ではない）。</summary>
        public const int ClashStunFrames = 3;

        /// <summary>Clash 時の共有 HitStop。</summary>
        public const int HitStopFrames = 4;

        /// <summary>Clash Knockback の減速（Punch/Kick と同様の CombatFrame 単位）。</summary>
        public const float KnockbackDeceleration = 0.015f;
    }
}
