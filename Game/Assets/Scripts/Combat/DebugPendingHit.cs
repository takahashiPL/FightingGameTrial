using FightingGameTrial.Fighter;

namespace FightingGameTrial.Combat
{
    /// <summary>
    /// 片方向の Hit「候補」です。まだ Damage / HitStun / Knockback は適用しません。
    ///
    /// なぜ候補を溜めるか:
    /// 同じ CombatFrame で P1→P2 と P2→P1 の両方を見てから結果を決めるためです。
    /// 先に評価した側だけを即適用すると、処理順で有利不利が生まれます。
    ///
    /// 将来の技相性（開始順・距離・Punch対Kick 等）で使う材料もここに残します。
    /// 接触点や先端率は今回持たず、後からフィールド追加しやすい小さな型にします。
    /// </summary>
    public sealed class DebugPendingHit
    {
        public bool IsValid;
        public DebugFighterParticipant Attacker;
        public DebugFighterParticipant Defender;
        public DebugAttackId AttackId;
        public DebugAttackData AttackData;
        public int AttackStartedCombatFrame;
        public int HitCombatFrame;
        public float DistanceX;
        public bool DefenderWasAttacking;
        public DebugAttackId DefenderAttackId;
        public DebugAttackPhase DefenderAttackPhase;
        public DebugBox2D HitBox;
        public DebugBox2D HurtBox;

        public void Clear()
        {
            IsValid = false;
            Attacker = null;
            Defender = null;
            AttackId = DebugAttackId.None;
            AttackData = null;
            AttackStartedCombatFrame = 0;
            HitCombatFrame = 0;
            DistanceX = 0f;
            DefenderWasAttacking = false;
            DefenderAttackId = DebugAttackId.None;
            DefenderAttackPhase = DebugAttackPhase.None;
            HitBox = null;
            HurtBox = null;
        }
    }
}
