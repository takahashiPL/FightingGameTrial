using FightingGameTrial.Combat;
using FightingGameTrial.Fighter;
using FightingGameTrial.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 1回分の論理 SimulationTick を進める入口です。
    ///
    /// 段階10B-3:
    /// - 両体移動のあと、Facing 更新の前に Participant 共通 Push Box で重なりを解消する
    /// - Facing は Push 補正後の最終位置を基準に更新する
    /// - P1 専用停止ではなく、DebugFighterPushResolver で双方へ等分補正する
    ///
    /// 段階11B:
    /// - Jパンチ Hit は距離ではなく Hit Box × Hurt Box の重なりで判定する
    /// - 可視化と同じ EvaluateWorldHitBox / EvaluateWorldHurtBox を実判定でも使う
    /// - 旧 attackRange 距離判定は使用しない
    ///
    /// 段階15:
    /// - J Punch の固定設定（Frame / Damage / HitStop / HitStun / KB / Hit Box local）は
    ///   DebugAttackData.JPunch が正本。Session は進行と適用のみ行い、数値の正本にしない
    /// - ScriptableObject / Inspector 編集はまだしない（参照経路の整理が目的）
    ///
    /// 段階14B:
    /// - HP 0 到達時に defender.TryEnterKnockout を一度だけ呼ぶ
    /// - KO 中は入力移動・新規攻撃・被 Hit（追加 Damage/HitStop 等）を禁止
    /// - 最後の一撃の HitStop / HitStun / Knockback は通常どおり成立させる
    /// - Round 終了・勝敗表示はしない
    ///
    /// 段階14A:
    /// - 有効 Hit 成立時に defender.ApplyDamage を1回だけ呼ぶ（Damage は攻撃データ）
    /// - HP 正本は各 Participant。HitState / Motor / Push は HP に触れない
    ///
    /// 段階13B-1:
    /// - Push 補正でステージ端（Motor minX/maxX）により片方の実移動が足りないとき、
    ///   未解消量をもう片方へ再配分する（DebugFighterPushResolver + TryMoveLogicalXBy）
    /// - KnockbackVelocityX は Push で変更しない。Facing は既存どおり Push 後
    ///
    /// 段階13A:
    /// - Hit 成立時に被弾側へ横ノックバック初速を予約する（LogicalX 比較で符号決定）
    /// - HitStop 中は移動・減速しない。HitStop 終了後の Combat から固定フレームで移動
    /// - 入力移動 → ノックバック移動 → Push → Facing → Action → Hit → Visual → HitStun
    /// - ノックバックは Motor.SetLogicalX / TryMoveLogicalXBy 経由
    /// - ステージ端の壁バウンド・壁やられはしない（Push 再配分のみ 13B-1）
    ///
    /// 段階12A:
    /// - 被弾側 Participant が HitState（HitStun / TotalHitCount / ノックバック速度）を所有する
    /// - HitStop 中は HitStun を減らさない。Combat 末尾で1回だけ消費する
    /// - HitStun 中は移動・攻撃開始を止め、Push / Facing は維持する
    ///
    /// 段階10B-2（維持）:
    /// - 攻撃状態（入力・開始・ActionFrame・終了・Visual）は各 Participant.AttackState
    /// - Hit 判定は attacker / defender 共通関数で行い、defender.ReceiveHit で被弾を記録する
    /// - 同一 CombatFrame 内で P1→P2 と P2→P1 の両方を判定する（相打ち将来対応）
    /// - P2 は Neutral 入力のため、現状は通常攻撃しない（棒立ち）
    /// - SimulationTimeState は共有時間状態のみ（攻撃状態は持たない）
    ///
    /// 段階10B-1（維持）:
    /// - P1/P2 を同じ Participant + Motor + Visual 構成で扱う
    /// - P1 は Gameplay 入力、P2 は Neutral 入力（棒立ち）
    ///
    /// 段階10A（維持）:
    /// - 移動入力と Facing を分離する
    /// - Facing 確定のあとで Hit 判定を行う
    /// - ほぼ同位置なら直前 Facing を維持する
    ///
    /// 1 CombatFrame 内の処理順:
    /// 1. P1/P2 入力解決
    /// 2. P1/P2 攻撃入力サンプリング（HitStop判定より前）
    /// 3. （HitStop中なら Combat をスキップして return）
    /// 4. CombatFrame +1
    /// 5. BeginCombatFrame（P1/P2 被弾旗リセット）
    /// 6. P1/P2 攻撃開始判定
    /// 7. P1/P2 入力移動（HitStun 中はスキップ）
    /// 8. P1/P2 ノックバック移動＋減速（HitStun 中かつ速度あり。1CFに1回）
    /// 9. Push Box 重なり解消（Participant 共通。速度は触らない）
    /// 10. P1 Facing / P2 Facing（Push 後の最終位置基準）
    /// 11. P1/P2 ActionFrame 進行
    /// 12. Hit 候補収集→分類→適用（CollectAndResolveHitsForCombatFrame）
    ///    片側 NormalHit / 地上同士 Ground Clash / Air 含む双方は未対応で未適用
    /// 13. Visual 更新（AttackState 反映）
    /// 14. P1/P2 攻撃終了判定
    /// 15. HitStun 消費（0 ならノックバック残速度もクリア）
    ///
    /// HitStop中:
    /// Combat 処理全体をスキップするため、移動も Push も Facing も Action もノックバックも進めない。
    /// ただし攻撃入力の SampleAttackInput は HitStop 前に行い、
    /// previousAttackHeld 相当の更新だけは維持する（既存仕様）。
    /// Hit 成立 tick では HitStopRemaining を設定するだけで、同じ tick 内では減らさない
    /// （次 tick 先頭から Combat 停止が始まる）。
    /// Pause中は自動進行せず、Step 時だけ本メソッドが1回呼ばれます。
    /// </summary>
    public class SimulationSession : MonoBehaviour
    {
        /// <summary>
        /// J Punch 設定の参照（正本は DebugAttackData.JPunch。毎 Frame new しない）。
        /// </summary>
        private static readonly DebugAttackData JPunchData = DebugAttackData.JPunch;
        private static readonly DebugAttackData KickData = DebugAttackData.Kick;
        private static readonly DebugAttackData AirKickData = DebugAttackData.AirKick;

        /// <summary>
        /// 専用の戻しモーションがない間だけ使う、Recovery前半の攻撃Visual終了AFです。
        /// 判定のActive区間ではなく見た目だけの境界で、Hit Boxの有効性には影響しません。
        /// </summary>
        private const int JPunchRecoveryVisualEndActionFrame = 10;
        private const int GroundKickRecoveryVisualEndActionFrame = 19;

        /// <summary>
        /// selfX と opponentX がほぼ同じときの Facing 維持用しきい値です。
        /// 完全一致や微小な誤差で毎フレーム向きが反転しないようにします。
        /// </summary>
        private const float FacingSameXEpsilon = 0.001f;

        [Header("参照（Inspectorで接続。自動検索はしません）")]
        [Tooltip("物理キーの最新状態を持つ DebugGameplayInput です（P1用）。")]
        [SerializeField]
        private DebugGameplayInput debugGameplayInput;

        [Tooltip("P1 参加枠です。Motor / Visual / Opponent / AttackState を内包します。")]
        [SerializeField]
        private DebugFighterParticipant participantP1;

        [Tooltip("P2 参加枠です。Motor / Visual / Opponent / AttackState を内包します。")]
        [SerializeField]
        private DebugFighterParticipant participantP2;

        [Header("時間状態")]
        [Tooltip(
            "SimulationTick / CombatFrame / Pause / HitStop など共有時間状態を保持します。"
            + " 攻撃状態は各 Participant.AttackState が正本です。"
        )]
        [SerializeField]
        private SimulationTimeState timeState = new SimulationTimeState();

        [Header("Consoleログ（毎tickは出さない）")]
        [Tooltip("何 SimulationTick ごとに1回ログを出すか。60なら約1秒に1回です。")]
        [SerializeField]
        private int consoleLogIntervalTicks = 60;

        [Tooltip(
            "true なら Jump started / apex / landed のイベントログを出します。"
            + " false でもジャンプ処理は動きます。文字列生成はこのフラグが true のときだけ行います。"
        )]
        [SerializeField]
        private bool enableJumpDebugLog = true;

        [Header("Debug（Clash / 左右対称検証専用・本番機能ではない）")]
        [Tooltip(
            "Clash や左右対称動作の検証専用です（本番機能ではない）。\n"
            + "ON: P1 の確定済み入力を鏡写し変換し、P2 用 SimulationInputState へ渡します。"
            + " その後は P2 の通常経路（移動・Jump・攻撃開始）だけを使います。\n"
            + "左右は反転、Jump / J Punch / Ground Kick は通常開始処理で模倣します。"
            + " 地上鏡写しは Air Kick を入力へ載せません"
            + "（空中双方候補の検証は debugEnableP2AirKickAssist を使う）。\n"
            + "OFF: P2 の既存入力（Neutral）と既存挙動を変更しません。"
            + " 通常プレイでは false のままにしてください。"
        )]
        [FormerlySerializedAs("debugForceP2AttackWithP1ForClashTest")]
        [SerializeField]
        private bool debugMirrorP1InputToP2 = false;

        [Tooltip(
            "異技Ground Clash／P1 Guard再現用の検証専用設定です（本番AIではない）。\n"
            + "debugMirrorP1InputToP2 が ON のときだけ効きます。OFF 時は無視し、P2 は Neutral のままです。\n"
            + "SameAsP1: 同技Clash確認（P1 J→P2 J、P1 K→P2 K）。\n"
            + "SwapPunchAndKick: 異技Clash確認（P1 J→P2 K、P1 K→P2 J）。\n"
            + "NoAttack: 攻撃は渡さず左右・Jump鏡写しだけ残し、片側Normal Hitを確認。\n"
            + "JPunchAfterDelay: P1攻撃なしでP2がDelay間隔でJPunchを繰り返す（P1 Back Guard検証用）。\n"
            + "P1JPunchP2GroundKickClash: 単発でP2 GroundKick→5CF後P1 JPunch（異種Clash検証用）。\n"
            + "技を直接開始せず Attack/Kick 入力だけ変換し、P2 の通常開始条件を維持します。"
            + " 双方接地時のみ攻撃変換し、Air Kick を起こしません。"
            + " Air双方候補の検証では NoAttack + debugEnableP2AirKickAssist を併用します。"
        )]
        [SerializeField]
        private DebugP2MirrorAttackMode debugP2MirrorAttackMode = DebugP2MirrorAttackMode.SameAsP1;

        [Tooltip(
            "P2へ渡す Attack／Kick の押下開始を遅らせる CombatFrame 数です（検証専用）。\n"
            + "debugMirrorP1InputToP2 が ON のときだけ有効。左右・Up・Down には影響しません。\n"
            + "異技Clash確認例: SwapPunchAndKick + 遅延5 で P1 GroundKick 開始の5CF後に P2 JPunch エッジ。\n"
            + "JPunchAfterDelay: P2単独JPunchの繰り返し間隔。"
            + " 攻撃終了（または再攻撃可能）後から数え、60Hzで 60CF≈1秒・120CF≈2秒。\n"
            + "攻撃データや Clash 条件は変えず、Active 重ね／Guard検証間隔だけを調整します。既定 0。"
            + " Air Kick アシストの遅延は debugP2AirKickDelayFrames を使います。"
        )]
        [Range(0, 120)]
        [SerializeField]
        private int debugP2MirrorAttackDelayFrames = 0;

        [Header("Debug（Air双方候補検証専用・本番機能ではない）")]
        [Tooltip(
            "Airを含む双方命中候補の未適用分岐をPlay確認するための検証専用です"
            + "（本番のP2操作・AIではない）。\n"
            + "ON: P1がこのCombatFrameでAirKickを開始するKickエッジをトリガに、"
            + "P2用SimulationInputStateへKickエッジを1回だけ載せます。"
            + " StartAirKickやPendingHitの直接注入はしません。\n"
            + "debugMirrorP1InputToP2 が ON のときだけ有効（P2入力ソースが鏡写しバッファのため）。\n"
            + "推奨: NoAttack + 本フラグON。既定 false。"
        )]
        [SerializeField]
        private bool debugEnableP2AirKickAssist = false;

        [Tooltip(
            "P2 Air Kick検証アシストのKickエッジを遅らせるCombatFrame数です（検証専用）。\n"
            + "debugEnableP2AirKickAssist が ON のときだけ有効。\n"
            + "0: P1と同じCombatFrameでP2もAirKick開始しやすい。"
            + " Active重ねの微調整に使います。既定 0。"
        )]
        [Range(0, 15)]
        [SerializeField]
        private int debugP2AirKickDelayFrames = 0;

        [Header("Debug（立ちガード検証専用・本番機能ではない）")]
        [Tooltip(
            "P2の立ちガード検証用です（本番のP2操作・AIではない）。\n"
            + "Normal: Guard分岐なし（従来どおり）。\n"
            + "StandGuard: P2が接地・非攻撃・非CombatReaction・相手向きのとき、"
            + "立ちガード可能な技（JPunch / GroundKick）の片側候補を Guard として解決。\n"
            + "AirKick・しゃがみ・後ろ入力・Just Guard は対象外。既定 Normal。\n"
            + "推奨: Mirror ON + JPunchAfterDelay + Delay（P1 Back Guard検証）。"
            + " P2自身のDebug StandGuard検証時は Mirror ON + NoAttack + StandGuard。"
        )]
        [SerializeField]
        private DebugP2StanceGuardMode debugP2StanceGuardMode = DebugP2StanceGuardMode.Normal;

        /// <summary>
        /// P2 専用の Neutral 入力です。
        /// P1 の CurrentInput とは別インスタンスで、毎tick new しません。
        /// 静的共有値にもしません（誤って書き換えられるのを防ぐため）。
        /// </summary>
        private SimulationInputState p2NeutralInput;

        /// <summary>
        /// P2 用の論理入力バッファです（Debug 鏡写し ON 時にだけ埋める）。
        ///
        /// 経路の考え方（将来 AI 差し替え用）:
        /// P1 確定入力 →（ここを鏡写し／将来は AI）→ P2 用 SimulationInputState
        /// → ResolveInput → 通常の移動・Jump・攻撃開始。
        /// 攻撃開始を Session から直接叩く裏口は使いません。
        /// </summary>
        private SimulationInputState p2MirrorInput;

        /// <summary>
        /// 変換後の Attack／Kick 意図の前tick保持（遅延キュー用の立ち上がり検出）。
        /// </summary>
        private bool previousMirrorAttackIntent;

        private bool previousMirrorKickIntent;

        /// <summary>
        /// P2 Attack／Kick を発火する CombatFrame（未予約は -1）。
        /// HitStop中は CombatFrame が進まないため、遅延も Combat 進行に同期します。
        /// </summary>
        private int pendingP2MirrorAttackFireCombatFrame = -1;

        private int pendingP2MirrorKickFireCombatFrame = -1;

        /// <summary>
        /// JPunchAfterDelay 用: P2 JPunch を発火する CombatFrame（未予約は -1）。
        /// 攻撃終了後から Delay を数え直す。P1攻撃鏡写しの Delay 予約とは別状態。
        /// </summary>
        private int pendingP2SoloJPunchFireCombatFrame = -1;

        /// <summary>
        /// JPunchAfterDelay 用: Attack 1tick 発火後、P2 の攻撃 Action 終了を待っている。
        /// 終了（または再攻撃可能）になるまで次の予約をしない。
        /// </summary>
        private bool p2SoloJPunchWaitingForAttackEnd;

        /// <summary>
        /// JPunchAfterDelay 用: 発火後に一度でも IsActionPlaying を見たか。
        /// 発火同一tickではまだ開始前なので、終了判定を誤らないために使う。
        /// </summary>
        private bool p2SoloJPunchSawAttackPlaying;

        /// <summary>
        /// JPunchAfterDelay 用: 繰り返し回数（ログ用。1始まりで発火時に加算）。
        /// </summary>
        private int p2SoloJPunchCycleIndex;

        /// <summary>
        /// JPunchAfterDelay 用: waiting ログをサイクルあたり1回に抑える。
        /// </summary>
        private bool p2SoloJPunchLoggedWaitingForEnd;

        /// <summary>
        /// 前tickの MirrorAttackMode（JPunchAfterDelay／CrossMoveClash への切替検出用）。
        /// </summary>
        private DebugP2MirrorAttackMode previousDebugP2MirrorAttackMode =
            DebugP2MirrorAttackMode.SameAsP1;

        /// <summary>
        /// P1JPunchP2GroundKickClash 用: P2 Kick 発火予定 CombatFrame（未予約は -1）。
        /// UpdateDebugMirror 時点の CombatFrame（++前）を基準にする。
        /// </summary>
        private int pendingCrossMoveClashP2KickFireCombatFrame = -1;

        /// <summary>
        /// P1JPunchP2GroundKickClash 用: P1 Attack 発火予定 CombatFrame（未予約は -1）。
        /// P2 Kick 発火基準から Startup 差（既定5）だけ遅らせ、Attack started 差を5にする。
        /// </summary>
        private int pendingCrossMoveClashP1PunchFireCombatFrame = -1;

        /// <summary>
        /// P1JPunchP2GroundKickClash 用: 期待する最初の双方候補 CombatFrame（ログ用）。
        /// </summary>
        private int expectedCrossMoveClashCandidateCombatFrame = -1;

        /// <summary>
        /// P1JPunchP2GroundKickClash 用: この Mode 入場中に試行を完了したか（単発）。
        /// P2発火後の P1 成功／失敗で true。P2発火不能の再予約では false のまま。
        /// </summary>
        private bool crossMoveClashAssistHasRun;

        /// <summary>
        /// P1JPunchP2GroundKickClash 用: 今の試行で P2 Kick を発火済みか。
        /// </summary>
        private bool crossMoveClashAssistP2KickFired;

        /// <summary>
        /// P2 Air Kick検証アシスト用: P1 Kick の前tick保持（立ち上がり検出）。
        /// </summary>
        private bool previousP1KickHeldForAirKickAssist;

        /// <summary>
        /// P2 Air Kick検証アシスト用: Kickエッジを発火するCombatFrame（未予約は -1）。
        /// </summary>
        private int pendingP2AirKickAssistFireCombatFrame = -1;

        /// <summary>
        /// 異技Clash検証用: 命中候補ログを攻撃開始単位で1回に抑えるための記録です。
        /// </summary>
        private int lastLoggedPendingHitStartedCombatFrameP1 = -1;

        private int lastLoggedPendingHitStartedCombatFrameP2 = -1;

        /// <summary>
        /// 両方向 Hit 候補の再利用バッファ（毎 Frame new しない）。
        /// </summary>
        private readonly DebugPendingHit pendingHitP1ToP2 = new DebugPendingHit();
        private readonly DebugPendingHit pendingHitP2ToP1 = new DebugPendingHit();

        /// <summary>
        /// HUD 用: 直近の Hit 解決結果（None / NormalHit / Clash）。
        /// </summary>
        private DebugHitResolutionType lastHitResolutionType = DebugHitResolutionType.None;

        /// <summary>
        /// Air を含む双方命中候補を未対応扱いにしたとき、警告をセッション中1回だけ出すための旗です。
        /// 毎フレームログで通常 Play を汚さないために使います。
        /// </summary>
        private bool hasLoggedUnsupportedAirMutualHitWarning;

        /// <summary>
        /// HUD 用: 直近 CombatFrame の Push 中心間距離（補正後）。
        /// </summary>
        private float lastPushCenterDistance;

        /// <summary>
        /// ジャンプ上入力の前 tick 保持（P1）。押下エッジ検出用。毎 tick new しない。
        /// </summary>
        private bool previousUpHeldP1;

        /// <summary>
        /// ジャンプ上入力の前 tick 保持（P2）。
        /// </summary>
        private bool previousUpHeldP2;

        /// <summary>
        /// Training Reset 後の共通 release gate。
        /// true の間は物理 Held を観測しつつ、Simulation へ渡す有効入力を全ニュートラルにする。
        /// Left/Right/Up/Down/Attack/Kick がすべて離れた tick で解除する（R は含めない）。
        /// </summary>
        private bool waitForAllGameplayInputReleaseAfterReset;

        /// <summary>
        /// HUD 用: 直近 CombatFrame で補正前に重なっていたか。
        /// </summary>
        private bool lastPushWasOverlapping;

        /// <summary>
        /// 直前 CombatFrame で Push 補正したか。
        /// 接触し続けている間の毎フレームログを避け、補正開始の立ち上がりだけ出すために使う。
        /// </summary>
        private bool previousPushDidCorrect;

        /// <summary>
        /// 直前 CombatFrame で壁際 Push 再配分を試みたか（段階13B-1）。
        /// 毎フレーム大量ログを避け、立ち上がりだけ出すために使う。
        /// </summary>
        private bool previousPushWallRedistributed;

        /// <summary>
        /// HUD 用: 直近の P1→P2 Hit 評価で Box が重なっていたか（段階11B）。
        /// </summary>
        private bool lastBoxOverlap;

        /// <summary>
        /// HUD 用: 直近の P1→P2 Hit 評価ラベル（Inactive / NoOverlap / Hit / AlreadyHit）。
        /// </summary>
        private string lastHitCheckLabel = DebugPunchHitCheckLabels.Inactive;

        public SimulationTimeState TimeState
        {
            get { return timeState; }
        }

        /// <summary>
        /// HUD 用: 直近の Push 中心間距離（補正後の絶対値）。
        /// </summary>
        public float LastPushCenterDistance
        {
            get { return lastPushCenterDistance; }
        }

        /// <summary>
        /// HUD 用: 直近 CombatFrame で Push 補正前に重なっていたか。
        /// </summary>
        public bool LastPushWasOverlapping
        {
            get { return lastPushWasOverlapping; }
        }

        /// <summary>
        /// HUD 用: 直近 P1→P2 の Hit Box × Hurt Box 重なり。
        /// </summary>
        public bool LastBoxOverlap
        {
            get { return lastBoxOverlap; }
        }

        /// <summary>
        /// HUD 用: 直近 P1→P2 の Hit 評価ラベル。
        /// </summary>
        public string LastHitCheckLabel
        {
            get
            {
                if (string.IsNullOrEmpty(lastHitCheckLabel))
                {
                    return DebugPunchHitCheckLabels.Inactive;
                }

                return lastHitCheckLabel;
            }
        }

        public DebugFighterParticipant ParticipantP1
        {
            get { return participantP1; }
        }

        public DebugFighterParticipant ParticipantP2
        {
            get { return participantP2; }
        }

        /// <summary>
        /// HUD / ClockDriver 互換用。P1 Motor を返します。
        /// </summary>
        public DebugFighterMotor DebugFighterMotor
        {
            get
            {
                if (participantP1 == null)
                {
                    return null;
                }

                return participantP1.Motor;
            }
        }

        /// <summary>
        /// HUD / ClockDriver 互換用。P1 Visual を返します。
        /// </summary>
        public DebugFighterVisual DebugFighterVisual
        {
            get
            {
                if (participantP1 == null)
                {
                    return null;
                }

                return participantP1.Visual;
            }
        }

        /// <summary>
        /// HUD 互換用。P1 の AttackState から Attack 立ち上がりを返します。
        /// </summary>
        public bool AttackPressedThisTick
        {
            get
            {
                if (participantP1 == null || participantP1.AttackState == null)
                {
                    return false;
                }

                return participantP1.AttackState.AttackPressedThisTick;
            }
        }

        private void Awake()
        {
            if (debugGameplayInput == null)
            {
                Debug.LogError("SimulationSession: DebugGameplayInput が未設定です。");
            }

            if (participantP1 == null)
            {
                Debug.LogError("SimulationSession: ParticipantP1 が未設定です。");
            }

            if (participantP2 == null)
            {
                Debug.LogError("SimulationSession: ParticipantP2 が未設定です。");
            }

            if (timeState == null)
            {
                timeState = new SimulationTimeState();
            }

            // P2 Neutral: 別インスタンスを1つだけ作り、OFF時は全 false のまま使う。
            p2NeutralInput = new SimulationInputState();
            p2NeutralInput.ResetToInitialValues();

            // P2 鏡写し用バッファ（ON時だけ毎tick更新。OFF時は参照されない）。
            p2MirrorInput = new SimulationInputState();
            p2MirrorInput.ResetToInitialValues();

            ClearDebugMirrorAttackDelayState();
            ClearDebugP2SoloJPunchAssistState(allowRestartOnNextResolve: true);
            ClearDebugP1JPunchP2GroundKickClashAssistState(allowRestartOnNextResolve: true);
            ClearDebugP2AirKickAssistState();

            timeState.ResetToInitialValues();
            timeState.LastStepResult = "未実行";
            timeState.LastStatusMessage = "Session ready";
        }

        private void Start()
        {
            // 開始時に初期配置が重なっていた場合だけ Push で離し、その後 Facing を合わせる。
            // Combat 進行ではないので Hit / Action は触らない。
            ResolvePushBoxBetweenParticipants(false);
            ApplyInitialFacingTowardOpponents();
            RefreshFighterVisual();
        }

        public void ProcessOneSimulationTick()
        {
            // 1. SimulationTick +1（HitStop中も進む）
            timeState.SimulationTick = timeState.SimulationTick + 1;

            // 2. 物理入力 → P1 用 CurrentInput（P2 Neutral / Mirror は別オブジェクト）
            SampleCurrentInputFromGameplay();
            // 2b. Debug 鏡写し ON のときだけ「入力ソース」を埋める（以後は通常経路のみ）。
            //     P1確定入力 → 鏡写し変換 → P2用 SimulationInputState
            UpdateDebugMirrorP2InputFromP1();

            // 3. P1/P2 入力を一度だけ解決（以後の移動・Jump・攻撃サンプリングで共用）
            SimulationInputState inputP1 = ResolveInputForParticipant(participantP1);
            SimulationInputState inputP2 = ResolveInputForParticipant(participantP2);

            // 4. 攻撃入力サンプリング（HitStop判定より前。ActionFrame進行とは分離）
            SampleAttackInputForParticipant(participantP1, inputP1);
            SampleAttackInputForParticipant(participantP2, inputP2);

            // 4b. ジャンプ用 Up 押下エッジ（HitStop 中も前状態を更新し、解除後の誤エッジを防ぐ）
            bool jumpPressedP1 = SampleJumpUpPressedThisTick(inputP1, ref previousUpHeldP1);
            bool jumpPressedP2 = SampleJumpUpPressedThisTick(inputP2, ref previousUpHeldP2);

            // 5. 既存 HitStop 判定
            //    Remaining>0 なら Combat 処理へ入らず、ここで1減らして return。
            //    （Hit成立tickで設定した6は、次tickから減り始める）
            if (timeState.HitStopRemaining > 0)
            {
                timeState.HitStopRemaining = timeState.HitStopRemaining - 1;
                if (timeState.HitStopRemaining < 0)
                {
                    timeState.HitStopRemaining = 0;
                }

                timeState.LastStatusMessage = "HitStop: Combat/Action/Fighter stop";

                if (timeState.HitStopRemaining == 0)
                {
                    Debug.Log("[FightDebug] HitStop ended");
                }

                WriteConsoleLogIfNeeded();
                return;
            }

            // 6. CombatFrame +1
            timeState.CombatFrame = timeState.CombatFrame + 1;

            // 7. 被弾旗リセット（Participant 単位）
            BeginCombatFrameForParticipant(participantP1);
            BeginCombatFrameForParticipant(participantP2);

            // 8. 攻撃開始（Punch を Kick より先に判定 → 同時押しは Punch 優先）
            //    同一 CombatFrame で Up と攻撃が同時でも、攻撃開始を Jump より先に試みる。
            TryStartJPunchForParticipant(participantP1);
            TryStartJPunchForParticipant(participantP2);
            // K はここ1か所で、CombatFrame 開始時点の接地状態から Ground / Air へ振り分ける。
            // 地上 Up+K は Jump より先に Ground Kick が始まり、空中への入力予約にはならない。
            TryStartKickForParticipant(participantP1);
            TryStartKickForParticipant(participantP2);

            // 8b. ジャンプ開始（地上・非 Attack・非 Landing。空中再ジャンプなし）
            //     鏡写し ON でも専用 Jump 開始はせず、inputP2.Up の通常経路だけを使う。
            TryStartJumpForParticipant(participantP1, inputP1, jumpPressedP1);
            TryStartJumpForParticipant(participantP2, inputP2, jumpPressedP2);
            FlushJumpDebugLogsForParticipant(participantP1);
            FlushJumpDebugLogsForParticipant(participantP2);

            // 9. 両体移動（地上: 入力移動 / 空中: ジャンプ軌道。HitStun・KO 中は入力移動スキップ）
            ProcessOneFighterMovement(participantP1, inputP1);
            ProcessOneFighterMovement(participantP2, inputP2);
            // Motor がこのフレームで着地したなら、Hit収集より前に Air Kick を即終了する。
            // 今回は地上 Recovery / 着地硬直を追加しない最小検証仕様です。
            TryEndAirKickOnLanding(participantP1);
            TryEndAirKickOnLanding(participantP2);
            // 頂点・着地イベントは空中軌道更新後に発生するため、ここで消費してログする
            FlushJumpDebugLogsForParticipant(participantP1);
            FlushJumpDebugLogsForParticipant(participantP2);

            // 10. ノックバック移動＋減速（段階13A）
            //     Hit 成立フレームでは速度セットが後段のため、ここではまだ動かない。
            //     HitStop 終了後の Combat から適用。Time.deltaTime は使わない。
            //     Push より前に動かし、めり込みは同フレームの Push で解消する。
            ProcessKnockbackForParticipant(participantP1);
            ProcessKnockbackForParticipant(participantP2);

            // 11. Push Box 重なり解消（空中で十分高いときはスキップして飛び越え可能）
            ResolvePushBoxBetweenParticipants(true);

            // 12. 両体 Facing 確定（Push 後の最終位置基準。Hit 判定より前）
            UpdateFacingTowardOpponent(participantP1);
            UpdateFacingTowardOpponent(participantP2);

            // 13. ActionFrame 進行（Participant 単位）
            AdvanceActionForParticipant(participantP1);
            AdvanceActionForParticipant(participantP2);

            // 14. Hit 候補収集 → 結果決定 → 適用
            //     即適用しない理由: 同じ CombatFrame の両方向を揃えてから
            //     NormalHit / Ground Clash を決めるため（処理順で片側だけ有利にしない）。
            CollectAndResolveHitsForCombatFrame();

            // 15. Visual 更新（Attack / Kick / Jump / Walk / Idle。HitStun・KO は専用 State）
            //     同一 CombatFrame の本更新なので歩行 elapsed を進める。
            RefreshFighterVisual(true);

            // 16. 攻撃終了判定（AttackState 側。Punch / Kick 共通）
            TryEndAttackForParticipant(participantP1);
            TryEndAttackForParticipant(participantP2);
            // 攻撃終了で State が変わった場合の再適用。同一 CombatFrame のため elapsed は進めない。
            RefreshFighterVisual(false);

            // 17. HitStun 消費（Combat Frame 末尾・1回だけ。0 ならノックバック残速度もクリア）
            TickHitStunForParticipant(participantP1);
            TickHitStunForParticipant(participantP2);

            // 18. 状態文 / ログ
            UpdateStatusMessage();
            WriteConsoleLogIfNeeded();
        }

        /// <summary>
        /// HUD / ログ用: 現在参照している J Punch 攻撃データ。
        /// </summary>
        public DebugAttackData JPunchAttackData
        {
            get { return JPunchData; }
        }

        public DebugAttackData KickAttackData
        {
            get { return KickData; }
        }

        /// <summary>HUD 用: 直近 CombatFrame の Hit 解決結果。</summary>
        public DebugHitResolutionType LastHitResolutionType
        {
            get { return lastHitResolutionType; }
        }

        /// <summary>
        /// HUD用: 現在再生中攻撃の区間ラベル（Idle / Startup / Active / Recovery）。
        /// Punch / Kick 共通。未再生は Idle。
        /// </summary>
        public string GetPunchPhaseLabel()
        {
            if (participantP1 == null || participantP1.AttackState == null)
            {
                return "Idle";
            }

            DebugFighterAttackState attackState = participantP1.AttackState;
            if (attackState.IsActionPlaying == false || attackState.CurrentAttackData == null)
            {
                return "Idle";
            }

            DebugAttackPhase phase = attackState.CurrentPhase;
            if (phase == DebugAttackPhase.Startup)
            {
                return "Startup";
            }

            if (phase == DebugAttackPhase.Active)
            {
                return "Active";
            }

            if (phase == DebugAttackPhase.Recovery)
            {
                return "Recovery";
            }

            return "Idle";
        }

        /// <summary>
        /// 両 Participant の Visual を更新します。
        /// advanceVisualAnimationFrame が true のときだけ、同じ State の経過 CombatFrame を進めます。
        /// </summary>
        public void RefreshFighterVisual(bool advanceVisualAnimationFrame)
        {
            RefreshOneFighterVisual(participantP1, advanceVisualAnimationFrame);
            RefreshOneFighterVisual(participantP2, advanceVisualAnimationFrame);
        }

        /// <summary>
        /// 互換: 経過加算なしで両体 Visual を更新します（Reset / Start 時など）。
        /// </summary>
        public void RefreshFighterVisual()
        {
            RefreshFighterVisual(false);
        }

        /// <summary>
        /// Aキー用: P1 の AttackState でテスト用パンチを開始します（段階10B-2整理）。
        ///
        /// 何をするか: AttackState.StartJPunch → P1 Visual 即時更新。
        /// なぜ Session 経由か: ClockDriver が Participant を検索せず、所有は Session が持つため。
        ///
        /// SimulationTick は進めません。Pause 中でも呼べます。
        /// 進行・Hit・終了は次以降の通常 ProcessOneSimulationTick（Participant 共通経路）に任せます。
        /// PreviousAttackHeld / AttackPressedThisTick は変更しません（通常 J 入力経路を壊さない）。
        /// </summary>
        public void StartTestActionForP1()
        {
            if (participantP1 == null || participantP1.AttackState == null)
            {
                Debug.LogError("SimulationSession: StartTestActionForP1 に P1 AttackState がありません。");
                return;
            }

            DebugFighterAttackState attackState = participantP1.AttackState;

            // すでに再生中なら二重開始しない（通常 J 開始と同じ）。
            if (attackState.IsActionPlaying)
            {
                return;
            }

            // 空中攻撃は今回未実装（A キーも地上のみ）
            if (participantP1.Motor != null && participantP1.Motor.IsGrounded == false)
            {
                return;
            }

            attackState.StartJPunch(timeState != null ? timeState.CombatFrame : 0);
            RefreshOneFighterVisual(participantP1, false);

            if (timeState != null)
            {
                timeState.LastStatusMessage = "Test Action start P1";
            }

            Debug.Log("[FightDebug] Test Action started slot=P1");
        }

        /// <summary>
        /// Rキー用: 練習モードの Training Reset（段階12A＋位置・向き・ジャンプ復帰）。
        ///
        /// 何をするか:
        /// - P1/P2 の AttackState・HitState・HP・KO・表示色を Reset
        /// - P1/P2 の論理位置 X/Y とジャンプ状態を Scene 開始時へ戻す（Motor）
        /// - 初期配置に基づき互いに向き合う Facing を再適用
        /// - 共有 HitStopRemaining を 0、Sampled 入力をクリア
        /// - 共通 release gate を立て、全ゲーム操作を一度離すまで有効入力をニュートラル化
        /// - 未消費の Jump debug event を破棄（Motor Reset 経路）
        /// - Visual を Idle へ即時更新
        ///
        /// SimulationTick は進めません。Pause 中でも呼べます。
        /// ClockDriver は Reset 受理フレームで通常 tick へ進まないこと（同一フレーム再処理防止）。
        /// 抑制中に再度 R を押した場合も再実行してよい（gate は維持・解除しない）。
        /// </summary>
        public void ResetTestActionForP1()
        {
            if (participantP1 != null)
            {
                participantP1.ResetCombatDebugState();
            }
            else
            {
                Debug.LogError("SimulationSession: ResetTestActionForP1 に P1 がありません。");
            }

            if (participantP2 != null)
            {
                participantP2.ResetCombatDebugState();
            }

            // 位置を先に戻し、その配置から Facing を決め直す（Training Reset）。
            // Motor 側で Jump 計測・pending started/apex/landed も破棄する。
            ResetParticipantLogicalPositionAndJump(participantP1);
            ResetParticipantLogicalPositionAndJump(participantP2);
            ApplyInitialFacingTowardOpponents();

            // ------------------------------------------------------------
            // 共通 release gate
            //
            // なぜ全操作を一度離すまで無効化するか:
            // Reset 直後に押しっぱなしの Left/Up/J 等をそのまま通すと、
            // 意図しない移動・再ジャンプ・再攻撃が始まるため。
            //
            // 一部だけ離しても解除しない（全ゲーム操作が false になるまで待つ）。
            // R 自体は DebugPlaybackInput 側であり、解除条件に含めない。
            //
            // 有効入力は SampleCurrentInputFromGameplay 側でニュートラル化する。
            // 物理 Held（DebugGameplayInput）は消さない。
            // ------------------------------------------------------------
            waitForAllGameplayInputReleaseAfterReset = true;

            if (timeState != null && timeState.CurrentInput != null)
            {
                timeState.CurrentInput.ResetToInitialValues();
            }

            if (p2NeutralInput != null)
            {
                p2NeutralInput.ResetToInitialValues();
            }

            if (p2MirrorInput != null)
            {
                p2MirrorInput.ResetToInitialValues();
            }

            ClearDebugMirrorAttackDelayState();
            ClearDebugP2SoloJPunchAssistState(allowRestartOnNextResolve: true);
            ClearDebugP1JPunchP2GroundKickClashAssistState(allowRestartOnNextResolve: true);
            ClearDebugP2AirKickAssistState();
            lastLoggedPendingHitStartedCombatFrameP1 = -1;
            lastLoggedPendingHitStartedCombatFrameP2 = -1;

            // 有効入力はニュートラル前提なので、エッジ用 previous も false に揃える。
            // （個別の Up/Attack 押しっぱなし抑制は共通 gate に一本化した）
            ResyncGameplayInputEdgePreviousAfterReleaseGate();

            if (timeState != null)
            {
                timeState.HitStopRemaining = 0;
                timeState.LastStatusMessage = "Training reset";
            }

            // Visual を Idle に戻し、歩行 elapsed / JumpStart・Apex 残留を捨てる
            ResetFighterVisualToIdle(participantP1);
            ResetFighterVisualToIdle(participantP2);
            RefreshFighterVisual(false);
            Debug.Log(
                "[FightDebug] Training reset"
                + " (Attack/HitStun/Knockback/HitStop/HP/KO/LogicalX/LogicalY/Jump/Facing)"
            );
        }

        private static void ResetFighterVisualToIdle(DebugFighterParticipant participant)
        {
            if (participant == null || participant.Visual == null)
            {
                return;
            }

            participant.Visual.ResetToIdle();
        }

        /// <summary>
        /// 共通 release gate 解除時・Reset 時に、エッジ検出用 previous をニュートラルへ再同期します。
        /// 解除直後サンプルで偽エッジ（Up/Attack）を出さないためです。
        /// </summary>
        private void ResyncGameplayInputEdgePreviousAfterReleaseGate()
        {
            previousUpHeldP1 = false;
            previousUpHeldP2 = false;
            ClearAttackEdgePreviousForParticipant(participantP1);
            ClearAttackEdgePreviousForParticipant(participantP2);
        }

        private static void ClearAttackEdgePreviousForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null || participant.AttackState == null)
            {
                return;
            }

            participant.AttackState.ClearAttackEdgePrevious();
        }

        /// <summary>
        /// Participant の Motor 論理位置とジャンプ状態を Scene 開始時へ戻します。
        /// </summary>
        private void ResetParticipantLogicalPositionAndJump(DebugFighterParticipant participant)
        {
            if (participant == null || participant.Motor == null)
            {
                return;
            }

            participant.Motor.ResetLogicalPositionAndJumpToInitial();
        }

        /// <summary>
        /// 互換ヘルパー（旧名）。位置＋ジャンプ Reset へ委譲します。
        /// </summary>
        private void ResetParticipantLogicalXToInitial(DebugFighterParticipant participant)
        {
            ResetParticipantLogicalPositionAndJump(participant);
        }

        /// <summary>
        /// 1体分の Visual を AttackState / HitState / KO / Jump / 移動入力から反映します。
        ///
        /// Sprite 優先:
        /// ClashRecoil → KO → HitStun → Attack(Punch) → Kick → JumpStart → JumpRise → JumpApex → JumpFall → Landing
        /// → WalkForward / WalkBackward → Idle
        ///
        /// 色は Participant.ApplyDisplayColor（Clash 専用色 &gt; HitStun 赤 &gt; KO 暗色 &gt; 通常）。
        /// color は Visual では触らない。
        /// </summary>
        private void RefreshOneFighterVisual(
            DebugFighterParticipant participant,
            bool advanceVisualAnimationFrame)
        {
            if (participant == null || participant.Visual == null)
            {
                return;
            }

            FighterVisualState visualState = ResolveFighterVisualState(participant);
            participant.Visual.Apply(visualState, advanceVisualAnimationFrame);
            participant.ApplyDisplayColor();
        }

        /// <summary>
        /// 1体の Visual State を決定します（見た目の正本決定。Sprite 差し替えは Visual）。
        ///
        /// 優先: ClashRecoil → KO → HitStun → GuardStun(Idle) → Attack → Kick → JumpStart → JumpRise → JumpApex → JumpFall → Landing → Walk → Idle
        /// </summary>
        private FighterVisualState ResolveFighterVisualState(DebugFighterParticipant participant)
        {
            if (participant.IsInClashRecoil)
            {
                return FighterVisualState.ClashRecoil;
            }

            if (participant.IsKnockedOut)
            {
                return FighterVisualState.KO;
            }

            if (participant.IsInHitStun)
            {
                return FighterVisualState.HitStun;
            }

            // GuardStun 専用 Visual は未追加。Idle 姿勢 + Participant の Guard 色で区別する。
            if (participant.IsInGuardStun)
            {
                return FighterVisualState.Idle;
            }

            DebugFighterAttackState attackState = participant.AttackState;
            if (attackState != null && participant.Visual != null)
            {
                if (attackState.IsJPunchAttack)
                {
                    // 内部Phaseと見た目を分離します。Startup / ActiveはPunch Sequenceを表示します。
                    if (attackState.CurrentPhase == DebugAttackPhase.Startup
                        || attackState.CurrentPhase == DebugAttackPhase.Active)
                    {
                        return FighterVisualState.Attack;
                    }

                    if (attackState.CurrentPhase == DebugAttackPhase.Recovery)
                    {
                        // AF8〜10は「振り切り」の見た目としてAttackを残しますが、追加判定ではありません。
                        // Hit BoxはAttackData.IsActiveFrameだけが決めるため、Recovery中は常に無効です。
                        if (attackState.ActionFrame <= JPunchRecoveryVisualEndActionFrame)
                        {
                            return FighterVisualState.Attack;
                        }

                        // AF11〜15は拳を伸ばした姿勢を長く保持しすぎないようIdleへ戻します。
                        // 見た目がIdleでも内部はJPunch Recoveryのままで、次の攻撃・ジャンプは開始不可です。
                        // 専用の戻しモーションが完成したら、この暫定境界を再調整します。
                        return FighterVisualState.Idle;
                    }
                }

                if (attackState.IsGroundKickAttack)
                {
                    // Startup / ActiveはKick Sequenceを表示します。
                    if (attackState.CurrentPhase == DebugAttackPhase.Startup
                        || attackState.CurrentPhase == DebugAttackPhase.Active)
                    {
                        return FighterVisualState.Kick;
                    }

                    if (attackState.CurrentPhase == DebugAttackPhase.Recovery)
                    {
                        // AF14〜19は「振り切り」の見た目としてKickを残しますが、Hit Boxは復活しません。
                        if (attackState.ActionFrame <= GroundKickRecoveryVisualEndActionFrame)
                        {
                            return FighterVisualState.Kick;
                        }

                        // AF20〜26は脚を伸ばした最終コマを長く保持しないようIdleへ戻します。
                        // Visualと内部状態は別なので、GroundKick Recoveryと行動不能はAF26終了まで継続します。
                        // 専用の戻しモーションが完成したら、この暫定境界を再調整します。
                        return FighterVisualState.Idle;
                    }
                }

                if (attackState.IsAirKickAttack)
                {
                    // 攻撃の内部状態と表示は分けます。
                    // Startup / Active は脚を伸ばす AirKick 表示、Recovery は Hit Box のない行動不能時間です。
                    if (attackState.CurrentPhase == DebugAttackPhase.Startup
                        || attackState.CurrentPhase == DebugAttackPhase.Active)
                    {
                        return FighterVisualState.AirKick;
                    }

                    if (attackState.CurrentPhase == DebugAttackPhase.Recovery)
                    {
                        // Recoveryまで蹴り画像を出すと「脚が当たっているのにHitしない」ように見えます。
                        // そこで見た目だけJumpFallへ戻しますが、AttackStateはAirKick Recoveryのままです。
                        // IsActionPlayingも維持されるため、新しい攻撃やジャンプは開始できません。
                        return FighterVisualState.JumpFall;
                    }
                }
            }

            DebugFighterMotor motor = participant.Motor;
            if (motor != null)
            {
                if (motor.IsGrounded == false)
                {
                    return ResolveAirborneJumpVisualState(participant.Visual, motor);
                }

                if (motor.IsLanding)
                {
                    return FighterVisualState.Landing;
                }
            }

            return ResolveLocomotionVisualState(participant);
        }

        /// <summary>
        /// 空中の Jump Visual State を決めます。
        /// JumpStart / Apex の長さは Visual 側の見た目用パラメータ（軌道は変更しない）。
        /// Apex は Motor の頂点通過フラグ＋経過を使い、毎 Frame の速度 Abs だけでは決めません。
        /// </summary>
        private static FighterVisualState ResolveAirborneJumpVisualState(
            DebugFighterVisual visual,
            DebugFighterMotor motor)
        {
            int jumpStartFrames = 2;
            int jumpApexFrames = 2;
            if (visual != null)
            {
                jumpStartFrames = visual.JumpStartVisualFrames;
                jumpApexFrames = visual.JumpApexVisualFrames;
            }

            // Jump 開始直後は JumpStart（例: Elapsed 1〜2）
            if (motor.JumpElapsedFrames <= jumpStartFrames)
            {
                return FighterVisualState.JumpStart;
            }

            // 頂点通過後の短い見た目窓
            if (motor.HasPassedJumpApex
                && motor.FramesSinceJumpApex >= 0
                && motor.FramesSinceJumpApex < jumpApexFrames)
            {
                return FighterVisualState.JumpApex;
            }

            if (motor.HasPassedJumpApex == false && motor.IsRising)
            {
                return FighterVisualState.JumpRise;
            }

            if (motor.IsRising)
            {
                return FighterVisualState.JumpRise;
            }

            return FighterVisualState.JumpFall;
        }

        /// <summary>
        /// 左右入力と Facing から前進／後退／停止の Visual State を決めます。
        ///
        /// 右向き+右 / 左向き+左 → WalkForward
        /// 右向き+左 / 左向き+右 → WalkBackward
        /// 入力なし・左右同時 → Idle
        ///
        /// 実座標の変化は見ない（入力意図ベース。壁際でも入力があれば歩行 State）。
        /// Push / Knockback による移動は歩行にしない（入力が無いため）。
        /// </summary>
        private FighterVisualState ResolveLocomotionVisualState(DebugFighterParticipant participant)
        {
            if (participant.Motor == null)
            {
                return FighterVisualState.Idle;
            }

            SimulationInputState input = ResolveInputForParticipant(participant);
            if (input == null)
            {
                return FighterVisualState.Idle;
            }

            bool left = input.Left;
            bool right = input.Right;

            // Motor と同じ: Left+Right 同時は移動なし → Idle
            if (left && right)
            {
                return FighterVisualState.Idle;
            }

            if (left == false && right == false)
            {
                return FighterVisualState.Idle;
            }

            bool facingRight = participant.Motor.FacingRight;

            if (facingRight)
            {
                if (right)
                {
                    return FighterVisualState.WalkForward;
                }

                return FighterVisualState.WalkBackward;
            }

            // 左向き
            if (left)
            {
                return FighterVisualState.WalkForward;
            }

            return FighterVisualState.WalkBackward;
        }

        /// <summary>
        /// Participant 単位で Attack / Kick ボタンの立ち上がりをサンプリングします。
        /// HitStop中でも呼ばれ、ActionFrame進行とは分離しています。
        /// Held（押しっぱなし）ではエッジは立ちません。
        /// </summary>
        private void SampleAttackInputForParticipant(
            DebugFighterParticipant participant,
            SimulationInputState input)
        {
            if (participant == null || participant.AttackState == null)
            {
                return;
            }

            bool attackHeldNow = false;
            bool kickHeldNow = false;

            if (input != null)
            {
                attackHeldNow = input.Attack;
                kickHeldNow = input.Kick;
            }

            participant.AttackState.SampleAttackButtons(attackHeldNow, kickHeldNow);
        }

        /// <summary>
        /// 地上 J Punch 開始。Kick より先に呼ばれ、同時押しは Punch 優先。
        /// </summary>
        private void TryStartJPunchForParticipant(DebugFighterParticipant participant)
        {
            if (CanStartGroundAttack(participant) == false)
            {
                return;
            }

            DebugFighterAttackState attackState = participant.AttackState;
            if (attackState.AttackPressedThisTick == false)
            {
                return;
            }

            attackState.StartJPunch(timeState.CombatFrame);
            LogAttackStarted(participant, JPunchData);
        }

        /// <summary>
        /// 地上 Ground Kick 開始。Punch 開始の後に呼ぶ（同時押しで Punch が先に取った場合は IsActionPlaying で弾く）。
        /// </summary>
        private void TryStartKickForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null
                || participant.AttackState == null
                || participant.Motor == null)
            {
                return;
            }

            DebugFighterAttackState attackState = participant.AttackState;
            if (attackState.KickPressedThisTick == false)
            {
                return;
            }

            // K の押下エッジを一度だけ読み、CombatFrame 開始時点の接地状態で攻撃を選びます。
            // Held は新しいエッジにならないため、地上Kを空中へ予約することもありません。
            if (participant.Motor.IsGrounded)
            {
                if (CanStartGroundAttack(participant) == false)
                {
                    return;
                }

                attackState.StartGroundKick(timeState.CombatFrame);
                LogAttackStarted(participant, KickData);
                return;
            }

            if (CanStartAirKick(participant) == false)
            {
                return;
            }

            attackState.StartAirKick(timeState.CombatFrame);
            LogAttackStarted(participant, AirKickData);
        }

        /// <summary>
        /// Air Kick 専用ゲート。上昇／頂点／下降は区別せず、すでに空中なら開始できます。
        /// AirKickUsedThisJump により同じジャンプ中の再発動を禁止します。
        /// </summary>
        private static bool CanStartAirKick(DebugFighterParticipant participant)
        {
            if (participant == null
                || participant.AttackState == null
                || participant.Motor == null
                || participant.Motor.IsGrounded)
            {
                return false;
            }

            if (participant.IsKnockedOut
                || participant.IsInCombatReaction
                || participant.AttackState.IsActionPlaying
                || participant.AttackState.AirKickUsedThisJump)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 地上攻撃共通ゲート: KO / HitStun / 空中 / 他 Action 中は不可。
        /// </summary>
        private static bool CanStartGroundAttack(DebugFighterParticipant participant)
        {
            if (participant == null || participant.AttackState == null)
            {
                return false;
            }

            if (participant.IsKnockedOut)
            {
                return false;
            }

            if (participant.IsInCombatReaction)
            {
                return false;
            }

            if (participant.Motor != null && participant.Motor.IsGrounded == false)
            {
                return false;
            }

            if (participant.AttackState.IsActionPlaying)
            {
                return false;
            }

            return true;
        }

        private void LogAttackStarted(
            DebugFighterParticipant participant,
            DebugAttackData attackData)
        {
            int simulationTick = 0;
            int combatFrame = 0;
            if (timeState != null)
            {
                simulationTick = timeState.SimulationTick;
                combatFrame = timeState.CombatFrame;
            }

            Debug.Log(
                "[FightDebug] Attack started"
                + " SimulationTick=" + simulationTick
                + " CombatFrame=" + combatFrame
                + " slot=" + participant.SlotId
                + " attack=" + attackData.AttackId
                + " S/A/R=" + attackData.StartupFrames
                + "/" + attackData.ActiveFrames
                + "/" + attackData.RecoveryFrames
                + " mirrorDelay=" + debugP2MirrorAttackDelayFrames
                + " mirrorAttackMode=" + debugP2MirrorAttackMode
                + " Damage=" + attackData.Damage
                + " HitStop=" + attackData.HitStopFrames
                + " HitStun=" + attackData.HitStunFrames
                + " KB=" + attackData.KnockbackInitialVelocityX.ToString("0.000")
            );
        }

        /// <summary>
        /// Participant 単位で ActionFrame を1進めます。
        /// Startup→Active / Active→Recovery の瞬間だけ異技Clash検証用ログを出します。
        /// </summary>
        private void AdvanceActionForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null || participant.AttackState == null)
            {
                return;
            }

            DebugFighterAttackState attackState = participant.AttackState;
            DebugAttackPhase phaseBefore = attackState.CurrentPhase;
            attackState.AdvanceActionFrame();
            DebugAttackPhase phaseAfter = attackState.CurrentPhase;

            if (phaseBefore != DebugAttackPhase.Active
                && phaseAfter == DebugAttackPhase.Active)
            {
                Debug.Log(
                    "[FightDebug] Attack active started"
                    + " CombatFrame=" + timeState.CombatFrame
                    + " slot=" + participant.SlotId
                    + " attack=" + attackState.CurrentAttackId
                    + " ActionFrame=" + attackState.ActionFrame
                );
            }
            else if (phaseBefore != DebugAttackPhase.Recovery
                && phaseAfter == DebugAttackPhase.Recovery)
            {
                Debug.Log(
                    "[FightDebug] Attack recovery started"
                    + " CombatFrame=" + timeState.CombatFrame
                    + " slot=" + participant.SlotId
                    + " attack=" + attackState.CurrentAttackId
                    + " ActionFrame=" + attackState.ActionFrame
                );
            }
        }

        /// <summary>
        /// Punch / Kick 共通の攻撃終了。終了条件の正本は CurrentAttackData.TotalFrames。
        /// </summary>
        private void TryEndAttackForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null || participant.AttackState == null)
            {
                return;
            }

            DebugFighterAttackState attackState = participant.AttackState;
            if (attackState.IsActionPlaying == false || attackState.CurrentAttackData == null)
            {
                return;
            }

            DebugAttackData attackData = attackState.CurrentAttackData;
            if (attackData.IsFinished(attackState.ActionFrame))
            {
                string attackLabel = attackData.AttackId;
                attackState.EndAttack();

                Debug.Log(
                    "[FightDebug] Attack ended slot=" + participant.SlotId
                    + " attack=" + attackLabel
                );
            }
        }

        /// <summary>
        /// Air Kick中にMotorが着地したら、その場で攻撃を終了します。
        /// 地上Recoveryへ持ち越さず、将来のAir Hit／Air Knockbackとも混ぜない検証用ルールです。
        /// </summary>
        private static void TryEndAirKickOnLanding(DebugFighterParticipant participant)
        {
            if (participant == null
                || participant.Motor == null
                || participant.AttackState == null
                || participant.Motor.IsGrounded == false
                || participant.AttackState.IsAirKickAttack == false)
            {
                return;
            }

            participant.AttackState.EndAttack();
            Debug.Log(
                "[FightDebug] Attack ended on landing"
                + " slot=" + participant.SlotId
                + " attack=AirKick"
            );
        }

        /// <summary>互換: 旧名。</summary>
        private void TryEndJPunchForParticipant(DebugFighterParticipant participant)
        {
            TryEndAttackForParticipant(participant);
        }

        /// <summary>
        /// CombatFrame 開始時に、この参加者の被弾フラグを下ろします。
        /// </summary>
        private void BeginCombatFrameForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null)
            {
                return;
            }

            participant.BeginCombatFrame();
        }

        /// <summary>
        /// 参加枠へ 1 CombatFrame 分の移動／ジャンプ軌道を依頼します。
        /// Facing は変えません。HitStun / KO 中は入力移動・空中制御を適用しません。
        /// </summary>
        private void ProcessOneFighterMovement(
            DebugFighterParticipant participant,
            SimulationInputState input)
        {
            if (participant == null)
            {
                return;
            }

            if (participant.Motor == null)
            {
                return;
            }

            DebugFighterMotor motor = participant.Motor;

            // 空中軌道は HitStun / KO 中も重力落下を進める（入力による空中制御は Motor 側で入力 null 相当にできる）
            if (motor.IsGrounded == false)
            {
                if (participant.IsKnockedOut || participant.IsInCombatReaction)
                {
                    // プレイヤー空中制御なし（Up/左右を無視した軌道継続）
                    motor.ProcessOneAirborneCombatFrame(null);
                }
                else
                {
                    motor.ProcessOneAirborneCombatFrame(input);
                }

                return;
            }

            // KO 中は本人の入力移動だけ無効（段階14B）。最後の一撃のノックバックは後段で効く。
            if (participant.IsKnockedOut)
            {
                return;
            }

            // HitStun 中は本人の移動だけ無効。Push / ノックバックは後段で効く。
            if (participant.IsInCombatReaction)
            {
                return;
            }

            participant.Motor.ProcessOneCombatFrame(input);
        }

        /// <summary>
        /// Up の押下エッジを検出し、前 tick 保持を更新します（HitStop 前でも呼ぶ）。
        /// </summary>
        private static bool SampleJumpUpPressedThisTick(
            SimulationInputState input,
            ref bool previousUpHeld)
        {
            bool upHeldNow = false;
            if (input != null)
            {
                upHeldNow = input.Up;
            }

            bool pressed = upHeldNow && previousUpHeld == false;
            previousUpHeld = upHeldNow;
            return pressed;
        }

        /// <summary>
        /// 上入力エッジでジャンプ開始を試みます。
        /// Attack 再生中・空中・Landing・HitStun・KO では開始しません。
        /// </summary>
        private void TryStartJumpForParticipant(
            DebugFighterParticipant participant,
            SimulationInputState input,
            bool upPressedThisTick)
        {
            if (participant == null || participant.Motor == null)
            {
                return;
            }

            if (upPressedThisTick == false)
            {
                return;
            }

            if (participant.IsKnockedOut || participant.IsInCombatReaction)
            {
                return;
            }

            DebugFighterMotor motor = participant.Motor;
            if (motor.IsGrounded == false)
            {
                return;
            }

            if (motor.IsLanding)
            {
                return;
            }

            if (participant.AttackState != null && participant.AttackState.IsActionPlaying)
            {
                return;
            }

            FighterJumpType jumpType = ResolveJumpType(motor.FacingRight, input);
            if (jumpType == FighterJumpType.None)
            {
                return;
            }

            bool started = motor.TryStartJump(jumpType);
            if (started)
            {
                // 1ジャンプ1回制限は「新しいジャンプが実際に始まった時点」で解除します。
                // 着地時に解除しないため、状態の境界がMotorのジャンプ開始と一致します。
                if (participant.AttackState != null)
                {
                    participant.AttackState.BeginNewJumpForAirKickUsage();
                }

                if (timeState != null)
                {
                    timeState.LastStatusMessage = "Jump " + jumpType;
                }
            }
        }

        /// <summary>
        /// Motor が立てたジャンプ計測イベントを消費し、slot 付きでログします。
        ///
        /// なぜ Session がログするか: slot（P1/P2）を知るのは参加枠側だからです。
        /// なぜ毎フレーム呼ばないか: イベントがあるときだけ文字列を作り、GC を抑えるためです。
        /// enableJumpDebugLog=false でも Consume して旗を下ろし、古いログの遅延出力を防ぎます。
        /// </summary>
        private void FlushJumpDebugLogsForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null || participant.Motor == null)
            {
                return;
            }

            DebugFighterMotor motor = participant.Motor;
            string slotLabel = participant.SlotId.ToString();

            FighterJumpType startedType;
            float startedX;
            float startedY;
            bool startedFacing;
            if (motor.TryConsumeJumpStartedDebug(
                out startedType,
                out startedX,
                out startedY,
                out startedFacing))
            {
                if (enableJumpDebugLog)
                {
                    Debug.Log(
                        "[FightDebug] Jump started"
                        + " slot=" + slotLabel
                        + " type=" + startedType
                        + " startX=" + startedX.ToString("0.00")
                        + " startY=" + startedY.ToString("0.00")
                        + " facingRight=" + (startedFacing ? "true" : "false")
                    );
                }
            }

            FighterJumpType apexType;
            int apexElapsed;
            int apexHeld;
            int apexDirHeld;
            float apexHeight;
            float apexXDistance;
            if (motor.TryConsumeJumpApexDebug(
                out apexType,
                out apexElapsed,
                out apexHeld,
                out apexDirHeld,
                out apexHeight,
                out apexXDistance))
            {
                if (enableJumpDebugLog)
                {
                    Debug.Log(
                        "[FightDebug] Jump apex"
                        + " slot=" + slotLabel
                        + " type=" + apexType
                        + " elapsed=" + apexElapsed
                        + " jumpHeld=" + apexHeld
                        + " directionHeld=" + apexDirHeld
                        + " height=" + apexHeight.ToString("0.00")
                        + " xDistance=" + apexXDistance.ToString("0.00")
                    );
                }
            }

            FighterJumpType landedType;
            int landedTotal;
            int landedHeld;
            int landedDirHeld;
            float landedMaxHeight;
            float landedHoriz;
            float landedStartX;
            float landedEndX;
            bool landedFacing;
            if (motor.TryConsumeJumpLandedDebug(
                out landedType,
                out landedTotal,
                out landedHeld,
                out landedDirHeld,
                out landedMaxHeight,
                out landedHoriz,
                out landedStartX,
                out landedEndX,
                out landedFacing))
            {
                if (enableJumpDebugLog)
                {
                    Debug.Log(
                        "[FightDebug] Jump landed"
                        + " slot=" + slotLabel
                        + " type=" + landedType
                        + " totalFrames=" + landedTotal
                        + " jumpHeld=" + landedHeld
                        + " directionHeld=" + landedDirHeld
                        + " maxHeight=" + landedMaxHeight.ToString("0.00")
                        + " horizontalDistance=" + landedHoriz.ToString("0.00")
                        + " startX=" + landedStartX.ToString("0.00")
                        + " endX=" + landedEndX.ToString("0.00")
                        + " facingRight=" + (landedFacing ? "true" : "false")
                    );
                }
            }
        }

        /// <summary>
        /// Facing と左右入力から Neutral / Forward / Backward を決めます。
        /// 左右同時は Neutral。JumpType 自体は開始後に変更しません。
        /// </summary>
        private static FighterJumpType ResolveJumpType(bool facingRight, SimulationInputState input)
        {
            if (input == null)
            {
                return FighterJumpType.Neutral;
            }

            bool left = input.Left;
            bool right = input.Right;

            if (left && right)
            {
                return FighterJumpType.Neutral;
            }

            if (left == false && right == false)
            {
                return FighterJumpType.Neutral;
            }

            if (facingRight)
            {
                if (right)
                {
                    return FighterJumpType.Forward;
                }

                return FighterJumpType.Backward;
            }

            if (left)
            {
                return FighterJumpType.Forward;
            }

            return FighterJumpType.Backward;
        }

        /// <summary>
        /// HitStun 中のノックバックを 1 Combat Frame 分だけ適用します（段階13A）。
        ///
        /// 流れ:
        /// LogicalX += knockbackVelocityX → 速度を deceleration だけ 0 へ近づける。
        /// Time.deltaTime は使わない（固定 Combat Frame 単位）。
        ///
        /// なぜ Hit 成立フレームでは動かないか:
        /// このメソッドは Hit 判定より前に走る。成立時にセットされた初速は
        /// HitStop 終了後の次 Combat から効く。
        ///
        /// Motor との責務:
        /// 速度の正本は HitState。位置書き込みは Motor.SetLogicalX。
        /// 既存 minX/maxX クランプはそのまま（壁バウンドはしない。Push 再配分は Resolver 側）。
        ///
        /// Push との関係:
        /// ノックバック後にめり込んだ場合、同フレームの Push で解消する。
        /// Push は knockbackVelocityX を変更しない。
        /// </summary>
        private void ProcessKnockbackForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null)
            {
                return;
            }

            if (participant.Motor == null)
            {
                return;
            }

            // ノックバックは HitStun 中だけ適用する。
            if (participant.IsInCombatReaction == false)
            {
                return;
            }

            float velocityX = participant.KnockbackVelocityX;
            if (velocityX == 0f)
            {
                return;
            }

            float newX = participant.Motor.LogicalX + velocityX;
            participant.Motor.SetLogicalX(newX);

            // 移動した Combat Frame でのみ減速する（HitStop 中はここへ来ない）。
            // 減速量は J Punch 攻撃データ（被弾共通設定ではなく、現状 J Punch 固有値）。
            participant.TickKnockbackVelocityForCombatFrame(
                JPunchData.KnockbackDecelerationPerCombatFrame
            );
        }

        /// <summary>
        /// Combat 末尾で HitStun を1減らします（段階12A）。
        ///
        /// HitStop 中（成立 tick で Remaining を立てた直後を含む）は減らさない。
        /// HitStop 外の Combat にだけ到達し、かつ Remaining==0 のときだけ消費する。
        /// HitStun が 0 になると Participant 側でノックバック残速度もクリアする（段階13A）。
        /// </summary>
        private void TickHitStunForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null)
            {
                return;
            }

            if (timeState != null && timeState.HitStopRemaining > 0)
            {
                return;
            }

            // GuardStun 終了は HitState に Slot が無いので、Session が Tick 前後で検出する。
            bool wasInGuardStun = participant.IsInGuardStun;
            participant.TickHitStunForCombatFrame();
            if (wasInGuardStun && participant.IsInGuardStun == false)
            {
                Debug.Log(
                    "[FightDebug] GuardStun ended slot=" + participant.SlotId
                );
            }
        }

        /// <summary>
        /// Participant へ渡す論理入力を選びます（入力ソースの切り替え点）。
        ///
        /// P1: CurrentInput（Gameplay）
        /// P2 OFF: Neutral（既存挙動。ここを変えない）
        /// P2 ON: p2MirrorInput（鏡写し変換済み。将来ここを AI 入力へ差し替え可能）
        ///
        /// この先の SampleAttack / Jump エッジ / TryStart* / 移動は共通です。
        /// </summary>
        private SimulationInputState ResolveInputForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null)
            {
                return p2NeutralInput;
            }

            if (participant.UsesGameplayInput)
            {
                return timeState.CurrentInput;
            }

            // Debug 鏡写しは P2（Gameplay 入力を使わない側）だけに適用する。
            if (debugMirrorP1InputToP2 && p2MirrorInput != null)
            {
                return p2MirrorInput;
            }

            return p2NeutralInput;
        }

        /// <summary>
        /// Debug 鏡写し ON のとき、P1 の確定済み入力を変換して P2 用 SimulationInputState を埋めます。
        /// OFF のときは何もしません（P2 は Neutral のまま＝既存挙動。攻撃変換・遅延も無視）。
        ///
        /// ここでやることは「入力ソースを埋める」だけです。
        /// StartJPunch / StartGroundKick / StartAirKick / TryStartJump を直接呼ばず、
        /// 以降は P2 の通常処理（SampleAttack → TryStart* → 移動）へ乗せます。
        ///
        /// 攻撃ボタンは debugP2MirrorAttackMode で変換し、
        /// debugP2MirrorAttackDelayFrames で押下開始だけ遅らせます（異技ClashのActive重ね用）。
        /// Air双方候補用に debugEnableP2AirKickAssist が ON なら、
        /// 別経路で Kick エッジを1回だけ OR します（地上攻撃変換とは独立）。
        /// 将来の本番AI入力とは別の Debug 補助です。
        /// </summary>
        private void UpdateDebugMirrorP2InputFromP1()
        {
            if (debugMirrorP1InputToP2 == false || p2MirrorInput == null)
            {
                // OFF 中に遅延予約が残ると、再ON時に古いエッジが飛ぶのを防ぐ。
                ClearDebugMirrorAttackDelayState();
                ClearDebugP2SoloJPunchAssistState(allowRestartOnNextResolve: true);
                ClearDebugP1JPunchP2GroundKickClashAssistState(allowRestartOnNextResolve: true);
                ClearDebugP2AirKickAssistState();
                return;
            }

            SimulationInputState p1 = null;
            if (timeState != null)
            {
                p1 = timeState.CurrentInput;
            }

            if (p1 == null)
            {
                p2MirrorInput.ClearGameplayHeldButtons();
                ClearDebugMirrorAttackDelayState();
                ClearDebugP2SoloJPunchAssistState(allowRestartOnNextResolve: true);
                ClearDebugP1JPunchP2GroundKickClashAssistState(allowRestartOnNextResolve: true);
                ClearDebugP2AirKickAssistState();
                return;
            }

            // 左右入力をそのままコピーすると、P1とP2が同じワールド方向へ動く。
            // 対面状態で鏡写しに接近・後退させるため、P2へ渡す左右入力を反転する。
            // P1 Left → P2 Right / P1 Right → P2 Left
            // 両方 OFF / 両方 ON はそのまま渡し、同時押しは既存の移動規則に従う。
            bool mirroredLeft = p1.Right;
            bool mirroredRight = p1.Left;

            // Jump は Up を通常経路へ渡す（専用 Jump 開始はしない）。
            bool mirroredUp = p1.Up;

            // Down は現状しゃがみ未実装だが、入力経路を欠けさせないためそのまま渡す。
            bool mirroredDown = p1.Down;

            // 攻撃: モード変換 →（必要なら）CombatFrame遅延 → 1tickのHeldでエッジを起こす。
            // 左右・Jumpには遅延を掛けない（SameAsP1 / Swap / NoAttack）。
            bool mirroredAttack;
            bool mirroredKick;
            ResolveDebugMirrorAttackButtonsWithDelay(
                p1.Attack,
                p1.Kick,
                out mirroredAttack,
                out mirroredKick
            );

            // P1 Back Guard／異種Clash検証用: 方向Mirrorだと距離が変わるため Neutral 固定。
            // JPunchAfterDelay: Attackの1tickだけ。P1JPunchP2GroundKickClash: Kickの1tickだけ。
            if (debugP2MirrorAttackMode == DebugP2MirrorAttackMode.JPunchAfterDelay)
            {
                mirroredLeft = false;
                mirroredRight = false;
                mirroredUp = false;
                mirroredDown = false;
                mirroredKick = false;
            }
            else if (debugP2MirrorAttackMode
                == DebugP2MirrorAttackMode.P1JPunchP2GroundKickClash)
            {
                mirroredLeft = false;
                mirroredRight = false;
                mirroredUp = false;
                mirroredDown = false;
                mirroredAttack = false;
            }
            else if (ResolveDebugP2AirKickAssistKickHeld(p1))
            {
                // Air Kick検証アシスト: 地上鏡写しのKickとは別にORする。
                // NoAttackでもAir双方候補を確認できるようにするため、地上モードのクリア後に合成する。
                mirroredKick = true;
            }

            p2MirrorInput.CopyFromPhysicalAndCommit(
                mirroredLeft,
                mirroredRight,
                mirroredUp,
                mirroredDown,
                mirroredAttack,
                mirroredKick,
                p1.SampledAtSimulationTick
            );
        }

        /// <summary>
        /// P1がこのCombatFrameでAirKickを開始するKickエッジをトリガに、
        /// P2へ載せるKick Heldを1tickだけ返します（検証専用）。
        ///
        /// なぜ StartAirKick を直接呼ばないか:
        /// P2の CanStartAirKick / SampleAttack / TryStartKick をバイパスすると、
        /// 本番経路とずれた「偽の双方候補」になりやすいためです。
        ///
        /// なぜ開始後ではなく入力段階でトリガするか:
        /// UpdateDebugMirror は TryStartKick より前のため、遅延0で同一CombatFrame開始するには
        /// 「P1が今フレームAirKickを開始できるKickエッジ」をトリガにする必要があります。
        /// </summary>
        private bool ResolveDebugP2AirKickAssistKickHeld(SimulationInputState p1)
        {
            if (debugEnableP2AirKickAssist == false || p1 == null)
            {
                ClearDebugP2AirKickAssistState();
                return false;
            }

            bool p1KickHeld = p1.Kick;
            bool p1KickEdge = p1KickHeld && previousP1KickHeldForAirKickAssist == false;
            previousP1KickHeldForAirKickAssist = p1KickHeld;

            int combatFrame = 0;
            if (timeState != null)
            {
                combatFrame = timeState.CombatFrame;
            }

            int delayFrames = debugP2AirKickDelayFrames;
            if (delayFrames < 0)
            {
                delayFrames = 0;
            }
            else if (delayFrames > 15)
            {
                delayFrames = 15;
            }

            // P1が空中でAirKick開始可能なKickエッジ → 予約。
            // 接地中のKはGroundKickになるため予約しない（誤ってP2へKickを載せない）。
            if (p1KickEdge && CanStartAirKick(participantP1))
            {
                pendingP2AirKickAssistFireCombatFrame = combatFrame + delayFrames;
                Debug.Log(
                    "[FightDebug] P2 AirKick assist reserved"
                    + " CombatFrame=" + combatFrame
                    + " fireCombatFrame=" + pendingP2AirKickAssistFireCombatFrame
                    + " delay=" + delayFrames
                );
            }

            if (pendingP2AirKickAssistFireCombatFrame < 0
                || combatFrame < pendingP2AirKickAssistFireCombatFrame)
            {
                return false;
            }

            pendingP2AirKickAssistFireCombatFrame = -1;

            // 発火時にP2がAirKick開始不能ならHeldを立てない。
            // 接地中にKickを載せるとGroundKickが始まり、検証目的から外れるため。
            if (CanStartAirKick(participantP2) == false)
            {
                Debug.Log(
                    "[FightDebug] P2 AirKick assist cancelled"
                    + " CombatFrame=" + combatFrame
                    + " reason=P2CannotStartAirKick"
                );
                return false;
            }

            Debug.Log(
                "[FightDebug] P2 AirKick assist fired"
                + " CombatFrame=" + combatFrame
                + " (Kick edge on P2 SimulationInputState; StartAirKick is not called directly)"
            );
            return true;
        }

        private void ClearDebugP2AirKickAssistState()
        {
            previousP1KickHeldForAirKickAssist = false;
            pendingP2AirKickAssistFireCombatFrame = -1;
        }

        /// <summary>
        /// 変換後の Attack／Kick 意図を取り、必要なら CombatFrame 遅延してから
        /// P2 入力用 Held を1回だけ立てます（SampleAttack がエッジ化する）。
        /// </summary>
        private void ResolveDebugMirrorAttackButtonsWithDelay(
            bool p1AttackHeld,
            bool p1KickHeld,
            out bool p2AttackHeld,
            out bool p2KickHeld)
        {
            p2AttackHeld = false;
            p2KickHeld = false;

            DebugP2MirrorAttackMode mode = debugP2MirrorAttackMode;

            // Mode 切替検出（JPunchAfterDelay／P1JPunchP2GroundKickClash）
            if (mode == DebugP2MirrorAttackMode.JPunchAfterDelay
                && previousDebugP2MirrorAttackMode != DebugP2MirrorAttackMode.JPunchAfterDelay)
            {
                ClearDebugP2SoloJPunchAssistState(allowRestartOnNextResolve: false);
                int enterDelay = debugP2MirrorAttackDelayFrames;
                if (enterDelay < 0)
                {
                    enterDelay = 0;
                }
                else if (enterDelay > 120)
                {
                    enterDelay = 120;
                }

                Debug.Log(
                    "[FightDebug] P2 solo JPunch loop started delay=" + enterDelay
                );
            }
            else if (mode != DebugP2MirrorAttackMode.JPunchAfterDelay
                && previousDebugP2MirrorAttackMode == DebugP2MirrorAttackMode.JPunchAfterDelay)
            {
                ClearDebugP2SoloJPunchAssistState(allowRestartOnNextResolve: false);
            }

            if (mode == DebugP2MirrorAttackMode.P1JPunchP2GroundKickClash
                && previousDebugP2MirrorAttackMode
                    != DebugP2MirrorAttackMode.P1JPunchP2GroundKickClash)
            {
                ClearDebugP1JPunchP2GroundKickClashAssistState(
                    allowRestartOnNextResolve: false
                );
                Debug.Log(
                    "[FightDebug] P1 JPunch / P2 GroundKick clash assist started"
                );
            }
            else if (mode != DebugP2MirrorAttackMode.P1JPunchP2GroundKickClash
                && previousDebugP2MirrorAttackMode
                    == DebugP2MirrorAttackMode.P1JPunchP2GroundKickClash)
            {
                ClearDebugP1JPunchP2GroundKickClashAssistState(
                    allowRestartOnNextResolve: false
                );
            }

            previousDebugP2MirrorAttackMode = mode;

            // 異種地上Clash単発: P1攻撃を鏡写しせず、P2 Kick→遅延P1 Attack だけを発火する。
            if (mode == DebugP2MirrorAttackMode.P1JPunchP2GroundKickClash)
            {
                ClearDebugMirrorAttackDelayState();
                p2KickHeld = ResolveDebugP1JPunchP2GroundKickClashAssistKickHeld();
                return;
            }

            // P1 Guard検証: P1攻撃を鏡写しせず、P2単独JPunchを Delay 間隔で繰り返す。
            if (mode == DebugP2MirrorAttackMode.JPunchAfterDelay)
            {
                ClearDebugMirrorAttackDelayState();
                p2AttackHeld = ResolveDebugP2SoloJPunchAssistAttackHeld();
                return;
            }

            // NoAttack: 攻撃は渡さない。遅延キューも作らない／残さない。
            if (mode == DebugP2MirrorAttackMode.NoAttack)
            {
                ClearDebugMirrorAttackDelayState();
                return;
            }

            bool intentAttack;
            bool intentKick;
            ResolveDebugMirrorAttackIntent(
                p1AttackHeld,
                p1KickHeld,
                out intentAttack,
                out intentKick
            );

            bool attackIntentEdge = intentAttack && previousMirrorAttackIntent == false;
            bool kickIntentEdge = intentKick && previousMirrorKickIntent == false;
            previousMirrorAttackIntent = intentAttack;
            previousMirrorKickIntent = intentKick;

            int combatFrame = 0;
            if (timeState != null)
            {
                combatFrame = timeState.CombatFrame;
            }

            int delayFrames = debugP2MirrorAttackDelayFrames;
            if (delayFrames < 0)
            {
                delayFrames = 0;
            }
            else if (delayFrames > 120)
            {
                delayFrames = 120;
            }

            // 立ち上がりを予約。発火 CombatFrame = 現在CF + 遅延。
            // 遅延0なら同一tickで発火し、従来どおり同時開始に近い。
            // UpdateDebugMirror は CombatFrame++ より前なので、
            // 遅延5なら「P1開始CFをNとして P2開始が N+5」になる。
            if (attackIntentEdge)
            {
                pendingP2MirrorAttackFireCombatFrame = combatFrame + delayFrames;
                Debug.Log(
                    "[FightDebug] Mirror attack delay reserved"
                    + " CombatFrame=" + combatFrame
                    + " fireCombatFrame=" + pendingP2MirrorAttackFireCombatFrame
                    + " button=Attack"
                    + " delay=" + delayFrames
                    + " mode=" + debugP2MirrorAttackMode
                );
            }

            if (kickIntentEdge)
            {
                pendingP2MirrorKickFireCombatFrame = combatFrame + delayFrames;
                Debug.Log(
                    "[FightDebug] Mirror attack delay reserved"
                    + " CombatFrame=" + combatFrame
                    + " fireCombatFrame=" + pendingP2MirrorKickFireCombatFrame
                    + " button=Kick"
                    + " delay=" + delayFrames
                    + " mode=" + debugP2MirrorAttackMode
                );
            }

            // 予約到達tickだけ Held=true（1回の正しい入力エッジ）。押しっぱなし複製ではない。
            if (pendingP2MirrorAttackFireCombatFrame >= 0
                && combatFrame >= pendingP2MirrorAttackFireCombatFrame)
            {
                p2AttackHeld = true;
                pendingP2MirrorAttackFireCombatFrame = -1;
                Debug.Log(
                    "[FightDebug] Mirror attack delay fired"
                    + " CombatFrame=" + combatFrame
                    + " button=Attack"
                );
            }

            if (pendingP2MirrorKickFireCombatFrame >= 0
                && combatFrame >= pendingP2MirrorKickFireCombatFrame)
            {
                p2KickHeld = true;
                pendingP2MirrorKickFireCombatFrame = -1;
                Debug.Log(
                    "[FightDebug] Mirror attack delay fired"
                    + " CombatFrame=" + combatFrame
                    + " button=Kick"
                );
            }
        }

        /// <summary>
        /// P1攻撃なしで、P2へ JPunch 用 Attack Held を1tickだけ返します（検証専用・繰り返し）。
        /// StartJPunch は直接呼ばず、通常の SampleAttack / TryStartJPunch へ載せます。
        ///
        /// なぜ繰り返すか:
        /// Inspector で Mode を外して戻すたびに Game ビューへフォーカスし直し、
        /// Back を保持し直す操作は Guard 検証の条件に含めたくないため。
        /// Guard 入力を押したまま複数回の命中結果を比較できるようにする。
        ///
        /// なぜ前回攻撃終了後から Delay を数えるか:
        /// 開始CF基準だと攻撃中に次予約が重なり、Attack を複数tick保持する事故や
        /// 攻撃中の再発火につながる。終了（再攻撃可能）後から数えると間隔が安定する。
        ///
        /// なぜ開始不能時に即攻撃せず Delay を数え直すか:
        /// HitStun / Action / 空中など CanStartGroundAttack=false の瞬間に発火しても
        /// 別技へ変換せず失敗するだけなので、可能になるまで待ち、そこから Delay する。
        ///
        /// なぜ Attack を1tickだけ通常経路へ通すか:
        /// StartJPunch 直呼びは接地・同時押し・1攻撃1開始などの通常条件をバイパスするため。
        ///
        /// なぜ P2 方向を Neutral 固定するか:
        /// P1 Back を鏡写しすると P2 が前進し距離が変わり、Guard 検証が汚れるため。
        /// </summary>
        private bool ResolveDebugP2SoloJPunchAssistAttackHeld()
        {
            int combatFrame = 0;
            if (timeState != null)
            {
                combatFrame = timeState.CombatFrame;
            }

            int delayFrames = debugP2MirrorAttackDelayFrames;
            if (delayFrames < 0)
            {
                delayFrames = 0;
            }
            else if (delayFrames > 120)
            {
                delayFrames = 120;
            }

            bool p2ActionPlaying =
                participantP2 != null
                && participantP2.AttackState != null
                && participantP2.AttackState.IsActionPlaying;

            // --- 攻撃終了待ち（発火後）---
            if (p2SoloJPunchWaitingForAttackEnd)
            {
                if (p2ActionPlaying)
                {
                    p2SoloJPunchSawAttackPlaying = true;
                    if (p2SoloJPunchLoggedWaitingForEnd == false)
                    {
                        p2SoloJPunchLoggedWaitingForEnd = true;
                        Debug.Log(
                            "[FightDebug] P2 solo JPunch waiting for attack end"
                        );
                    }

                    return false;
                }

                if (p2SoloJPunchSawAttackPlaying)
                {
                    // 前回 JPunch が終了した。次回 Delay はここから（可能なら即予約）。
                    p2SoloJPunchWaitingForAttackEnd = false;
                    p2SoloJPunchSawAttackPlaying = false;
                    p2SoloJPunchLoggedWaitingForEnd = false;
                }
                else if (CanStartGroundAttack(participantP2))
                {
                    // 発火したが Action が始まらなかった（HitStop中の開始スキップ等）。
                    // 攻撃可能になった時点から Delay を数え直す。
                    p2SoloJPunchWaitingForAttackEnd = false;
                    p2SoloJPunchLoggedWaitingForEnd = false;
                }
                else
                {
                    // まだ開始不能（HitStun 等）。可能になるまで待つ。
                    return false;
                }
            }

            // --- 予約到達: 1tick だけ Attack=true ---
            if (pendingP2SoloJPunchFireCombatFrame >= 0
                && combatFrame >= pendingP2SoloJPunchFireCombatFrame)
            {
                pendingP2SoloJPunchFireCombatFrame = -1;

                if (CanStartGroundAttack(participantP2) == false)
                {
                    // 別技へ変換せず発火しない。可能になってから Delay を数え直す。
                    Debug.Log(
                        "[FightDebug] P2 solo JPunch fire skipped"
                        + " CombatFrame=" + combatFrame
                        + " reason=P2CannotStartGroundAttack"
                        + " (will re-reserve after CanStart)"
                    );
                    return false;
                }

                p2SoloJPunchCycleIndex = p2SoloJPunchCycleIndex + 1;
                p2SoloJPunchWaitingForAttackEnd = true;
                p2SoloJPunchSawAttackPlaying = false;
                p2SoloJPunchLoggedWaitingForEnd = false;

                Debug.Log(
                    "[FightDebug] P2 solo JPunch fired"
                    + " CombatFrame=" + combatFrame
                    + " cycle=" + p2SoloJPunchCycleIndex
                );
                return true;
            }

            // 予約待ち（発火CF未達）
            if (pendingP2SoloJPunchFireCombatFrame >= 0)
            {
                return false;
            }

            // 攻撃終了待ちの途中でここに来ることはない（上で return 済み）。
            if (p2SoloJPunchWaitingForAttackEnd)
            {
                return false;
            }

            // --- 待機なし: 攻撃可能になった時点から Delay を予約 ---
            if (CanStartGroundAttack(participantP2) == false)
            {
                return false;
            }

            pendingP2SoloJPunchFireCombatFrame = combatFrame + delayFrames;
            if (p2SoloJPunchCycleIndex <= 0)
            {
                Debug.Log(
                    "[FightDebug] P2 solo JPunch reserved"
                    + " CombatFrame=" + combatFrame
                    + " fireCombatFrame=" + pendingP2SoloJPunchFireCombatFrame
                    + " delay=" + delayFrames
                );
            }
            else
            {
                Debug.Log(
                    "[FightDebug] P2 solo JPunch next cycle reserved"
                    + " CombatFrame=" + combatFrame
                    + " fireCombatFrame=" + pendingP2SoloJPunchFireCombatFrame
                    + " delay=" + delayFrames
                    + " cycle=" + p2SoloJPunchCycleIndex
                );
            }

            return false;
        }

        /// <summary>
        /// P2鏡写し用の Attack / Kick「意図」を決めます（まだ遅延・発火前）。
        ///
        /// なぜ入力だけ変換するか:
        /// StartJPunch / StartGroundKick を直接叩くと、P2の接地・同時押し優先・1攻撃1開始などの
        /// 通常開始条件をバイパスし、将来AI差し替え時の経路ともずれるためです。
        ///
        /// なぜ双方接地のときだけ攻撃変換するか:
        /// 空中の J/K を Swap すると P2 が Kick を受け取り Air Kick を開始し得るため。
        /// 今回の対象は地上 JPunch / GroundKick の異技Clash検証だけです。
        /// </summary>
        private void ResolveDebugMirrorAttackIntent(
            bool p1AttackHeld,
            bool p1KickHeld,
            out bool intentAttack,
            out bool intentKick)
        {
            intentAttack = false;
            intentKick = false;

            // Air Kick除外: 双方接地時だけ攻撃意図を立てる。
            bool bothGrounded =
                participantP1 != null
                && participantP1.Motor != null
                && participantP1.Motor.IsGrounded
                && participantP2 != null
                && participantP2.Motor != null
                && participantP2.Motor.IsGrounded;

            if (bothGrounded == false)
            {
                return;
            }

            if (debugP2MirrorAttackMode == DebugP2MirrorAttackMode.SwapPunchAndKick)
            {
                // 異技Clash確認: P1 J → P2 K、P1 K → P2 J
                intentAttack = p1KickHeld;
                intentKick = p1AttackHeld;
                return;
            }

            // SameAsP1（既定）: 同技Clash確認
            // JPunchAfterDelay / NoAttack は呼び出し側で分岐済み。
            intentAttack = p1AttackHeld;
            intentKick = p1KickHeld;
        }

        private void ClearDebugMirrorAttackDelayState()
        {
            previousMirrorAttackIntent = false;
            previousMirrorKickIntent = false;
            pendingP2MirrorAttackFireCombatFrame = -1;
            pendingP2MirrorKickFireCombatFrame = -1;
        }

        /// <summary>
        /// P2単独JPunch Assist の予約・発火待ち・攻撃終了待ちをすべてクリアします。
        ///
        /// OFF／Mode離脱／Training Reset／Awake で内部予約を消す理由:
        /// 古い fireCombatFrame や終了待ちが残ると、再ON直後に意図しない Attack エッジが飛ぶため。
        /// allowRestartOnNextResolve=true のときは Mode 再入場検出用に previousMode をずらす
        /// （Mirror OFF→ON で Mode が JPunchAfterDelay のままでも新しいサイクルを開始する）。
        /// </summary>
        private void ClearDebugP2SoloJPunchAssistState(bool allowRestartOnNextResolve)
        {
            pendingP2SoloJPunchFireCombatFrame = -1;
            p2SoloJPunchWaitingForAttackEnd = false;
            p2SoloJPunchSawAttackPlaying = false;
            p2SoloJPunchCycleIndex = 0;
            p2SoloJPunchLoggedWaitingForEnd = false;

            if (allowRestartOnNextResolve)
            {
                previousDebugP2MirrorAttackMode = DebugP2MirrorAttackMode.NoAttack;
            }
        }

        /// <summary>
        /// P1 JPunch / P2 GroundKick 異種Clash単発 Assist の内部状態をクリアします。
        /// allowRestartOnNextResolve=true のとき previousMode をずらし、Mirror 再ONで再入場検出する。
        /// </summary>
        private void ClearDebugP1JPunchP2GroundKickClashAssistState(
            bool allowRestartOnNextResolve)
        {
            pendingCrossMoveClashP2KickFireCombatFrame = -1;
            pendingCrossMoveClashP1PunchFireCombatFrame = -1;
            expectedCrossMoveClashCandidateCombatFrame = -1;
            crossMoveClashAssistHasRun = false;
            crossMoveClashAssistP2KickFired = false;

            if (allowRestartOnNextResolve)
            {
                previousDebugP2MirrorAttackMode = DebugP2MirrorAttackMode.NoAttack;
            }
        }

        /// <summary>
        /// GroundKick.Startup − JPunch.Startup。
        /// UpdateDebugMirror（CombatFrame++前）でこの差だけ P1 Attack を遅らせると、
        /// Attack started CF 差がちょうどこの値になる（既存 Mirror Delay と同じ関係）。
        /// </summary>
        private static int ResolveCrossMoveClashP1AttackLeadFrames()
        {
            int lead =
                DebugAttackData.Kick.StartupFrames
                - DebugAttackData.JPunch.StartupFrames;
            if (lead < 0)
            {
                lead = 0;
            }

            return lead;
        }

        /// <summary>
        /// P1JPunchP2GroundKickClash: P2へ Kick Held を1tick返す（検証専用・単発）。
        /// 必要なら同メソッド内で P1 CurrentInput.Attack を1tick OR する。
        /// StartGroundKick / StartJPunch は直接呼ばない。
        /// </summary>
        private bool ResolveDebugP1JPunchP2GroundKickClashAssistKickHeld()
        {
            int combatFrame = 0;
            if (timeState != null)
            {
                combatFrame = timeState.CombatFrame;
            }

            int p1LeadFrames = ResolveCrossMoveClashP1AttackLeadFrames();

            // 単発完了後は再発火しない（Mode再入場／Resetでクリア）。
            if (crossMoveClashAssistHasRun)
            {
                return false;
            }

            // --- P1 Attack 発火待ち（P2 Kick 発火後）---
            if (crossMoveClashAssistP2KickFired
                && pendingCrossMoveClashP1PunchFireCombatFrame >= 0)
            {
                if (combatFrame < pendingCrossMoveClashP1PunchFireCombatFrame)
                {
                    return false;
                }

                pendingCrossMoveClashP1PunchFireCombatFrame = -1;

                if (CanStartGroundAttack(participantP1) == false)
                {
                    crossMoveClashAssistHasRun = true;
                    Debug.Log(
                        "[FightDebug] P1 JPunch / P2 GroundKick clash assist"
                        + " P1 Attack fire skipped"
                        + " CombatFrame=" + combatFrame
                        + " reason=P1CannotStartGroundAttack"
                        + " (assist ended; no auto retry)"
                    );
                    return false;
                }

                OrP1AttackHeldForCrossMoveClashAssist();
                crossMoveClashAssistHasRun = true;
                Debug.Log(
                    "[FightDebug] P1 JPunch / P2 GroundKick clash assist"
                    + " P1 Attack fired"
                    + " CombatFrame=" + combatFrame
                    + " expectedCandidateCombatFrame="
                    + expectedCrossMoveClashCandidateCombatFrame
                    + " (Attack OR on P1 CurrentInput; StartJPunch is not called directly)"
                );
                return false;
            }

            // --- P2 Kick 予約待ち ---
            if (pendingCrossMoveClashP2KickFireCombatFrame >= 0)
            {
                if (combatFrame < pendingCrossMoveClashP2KickFireCombatFrame)
                {
                    return false;
                }

                return TryFireCrossMoveClashP2Kick(combatFrame);
            }

            // --- 未予約: 双方開始可能になるまで待つ ---
            if (CanStartGroundAttack(participantP1) == false
                || CanStartGroundAttack(participantP2) == false)
            {
                return false;
            }

            pendingCrossMoveClashP2KickFireCombatFrame = combatFrame;
            pendingCrossMoveClashP1PunchFireCombatFrame = combatFrame + p1LeadFrames;
            // 入力注入CFの次が Attack started CF。最初の Active = started + GroundKick.Startup。
            expectedCrossMoveClashCandidateCombatFrame =
                combatFrame + 1 + DebugAttackData.Kick.StartupFrames;

            Debug.Log(
                "[FightDebug] P1 JPunch / P2 GroundKick clash assist reserved"
                + " CombatFrame=" + combatFrame
                + " p2KickFireCombatFrame="
                + pendingCrossMoveClashP2KickFireCombatFrame
                + " p1PunchFireCombatFrame="
                + pendingCrossMoveClashP1PunchFireCombatFrame
                + " expectedCandidateCombatFrame="
                + expectedCrossMoveClashCandidateCombatFrame
                + " p1LeadFrames=" + p1LeadFrames
            );

            return TryFireCrossMoveClashP2Kick(combatFrame);
        }

        /// <summary>
        /// 予約済み P2 Kick 発火。開始不能なら予約を破棄して再待機可能にする。
        /// </summary>
        private bool TryFireCrossMoveClashP2Kick(int combatFrame)
        {
            pendingCrossMoveClashP2KickFireCombatFrame = -1;

            if (CanStartGroundAttack(participantP2) == false)
            {
                pendingCrossMoveClashP1PunchFireCombatFrame = -1;
                expectedCrossMoveClashCandidateCombatFrame = -1;
                crossMoveClashAssistP2KickFired = false;
                Debug.Log(
                    "[FightDebug] P1 JPunch / P2 GroundKick clash assist"
                    + " P2 Kick fire skipped"
                    + " CombatFrame=" + combatFrame
                    + " reason=P2CannotStartGroundAttack"
                    + " (will re-reserve when both can start)"
                );
                return false;
            }

            crossMoveClashAssistP2KickFired = true;
            Debug.Log(
                "[FightDebug] P1 JPunch / P2 GroundKick clash assist"
                + " P2 Kick fired"
                + " CombatFrame=" + combatFrame
                + " expectedCandidateCombatFrame="
                + expectedCrossMoveClashCandidateCombatFrame
                + " (Kick edge on P2 SimulationInputState; StartGroundKick is not called directly)"
            );
            return true;
        }

        /// <summary>
        /// P1 CurrentInput の Attack だけを true にする（方向・Kickは触らない）。
        /// SampleAttack 前の UpdateDebugMirror から呼ばれ、通常エッジ経路へ載せる。
        /// </summary>
        private void OrP1AttackHeldForCrossMoveClashAssist()
        {
            if (timeState == null || timeState.CurrentInput == null)
            {
                return;
            }

            timeState.CurrentInput.Attack = true;
        }

        /// <summary>
        /// 空中で十分な高さがあるとき横 Push をスキップするか。
        /// 開始直後の低高度ではすり抜けず、頂点付近で飛び越え可能にする。
        /// </summary>
        private static bool ShouldSkipPushForAerialSeparation(
            DebugFighterMotor motorA,
            DebugFighterMotor motorB)
        {
            if (motorA == null || motorB == null)
            {
                return false;
            }

            // 両方地上なら通常 Push
            if (motorA.IsGrounded && motorB.IsGrounded)
            {
                return false;
            }

            float thresholdA = motorA.JumpSettings.PushBoxVerticalSeparationThreshold;
            float thresholdB = motorB.JumpSettings.PushBoxVerticalSeparationThreshold;
            float threshold = thresholdA;
            if (thresholdB > threshold)
            {
                threshold = thresholdB;
            }

            float heightA = motorA.HeightAboveGround;
            float heightB = motorB.HeightAboveGround;
            float maxHeightAboveGround = heightA;
            if (heightB > maxHeightAboveGround)
            {
                maxHeightAboveGround = heightB;
            }

            float absDeltaY = Mathf.Abs(motorA.LogicalY - motorB.LogicalY);

            // どちらかが十分高く、かつ互いの Y 差も閾値以上
            if (maxHeightAboveGround >= threshold && absDeltaY >= threshold)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 両 Participant の横方向 Push Box 重なりを解消します（段階10B-3 / 13B-1）。
        ///
        /// 何をするか:
        /// - DebugFighterPushResolver に等分分離と壁際再配分を依頼する
        /// - HUD 用に中心距離・重なり有無を記録する
        /// - 補正開始／壁際再配分の立ち上がりだけ1行ログを出す（常時大量ログは出さない）
        ///
        /// なぜこの順か:
        /// Facing と Hit は最終位置を使うため、移動の直後・Facing の直前で行う。
        /// Push は KnockbackVelocityX を変更しない（位置のみ）。
        ///
        /// logOnCorrect: Combat tick 中のみ true。Start 時の初期離しでは false。
        /// </summary>
        private void ResolvePushBoxBetweenParticipants(bool logOnCorrect)
        {
            if (participantP1 == null || participantP2 == null)
            {
                lastPushCenterDistance = 0f;
                lastPushWasOverlapping = false;
                return;
            }

            if (participantP1.Motor == null || participantP2.Motor == null)
            {
                lastPushCenterDistance = 0f;
                lastPushWasOverlapping = false;
                return;
            }

            // 十分高い空中では横 Push をスキップし、飛び越えを許可する。
            if (ShouldSkipPushForAerialSeparation(participantP1.Motor, participantP2.Motor))
            {
                float airDist = Mathf.Abs(
                    participantP2.Motor.LogicalX - participantP1.Motor.LogicalX
                );
                lastPushCenterDistance = airDist;
                lastPushWasOverlapping = false;
                previousPushDidCorrect = false;
                return;
            }

            float beforeP1X = participantP1.Motor.LogicalX;
            float beforeP2X = participantP2.Motor.LogicalX;

            float centerDistanceBefore;
            float requiredMinDistance;
            bool wasOverlapping;
            bool didWallRedistribute;
            string wallLog;

            bool didCorrect = DebugFighterPushResolver.TryResolveHorizontalOverlap(
                participantP1,
                participantP2,
                out centerDistanceBefore,
                out requiredMinDistance,
                out wasOverlapping,
                out didWallRedistribute,
                out wallLog
            );

            lastPushWasOverlapping = wasOverlapping;

            // HUD には補正後の中心距離を出す（接触中はほぼ最小距離になる）。
            float afterP1X = participantP1.Motor.LogicalX;
            float afterP2X = participantP2.Motor.LogicalX;
            lastPushCenterDistance = Mathf.Abs(afterP2X - afterP1X);

            // 補正開始の立ち上がりだけ1行ログ（押し続け中の毎フレーム出力はしない）。
            if (didCorrect && logOnCorrect && previousPushDidCorrect == false)
            {
                Debug.Log(
                    "[FightDebug] Push correct"
                    + " P1 " + beforeP1X.ToString("0.00") + "->" + afterP1X.ToString("0.00")
                    + " P2 " + beforeP2X.ToString("0.00") + "->" + afterP2X.ToString("0.00")
                    + " dist " + centerDistanceBefore.ToString("0.00")
                    + "->" + lastPushCenterDistance.ToString("0.00")
                    + " min=" + requiredMinDistance.ToString("0.00")
                );
            }

            // 壁際再配分の立ち上がりだけ1行（段階13B-1）。押し続け中は出さない。
            if (didWallRedistribute
                && logOnCorrect
                && previousPushWallRedistributed == false
                && string.IsNullOrEmpty(wallLog) == false)
            {
                Debug.Log("[FightDebug] Push wall redistribute " + wallLog);
            }

            previousPushDidCorrect = didCorrect;
            previousPushWallRedistributed = didWallRedistribute;
        }

        /// <summary>
        /// Push 補正後の論理座標から、self が相手と向き合う Facing を決めます（段階10A/10B-3）。
        ///
        /// - selfX が opponentX より小さい → 右向き
        /// - selfX が opponentX より大きい → 左向き
        /// - ほぼ同位置 → 直前 Facing を維持
        ///
        /// 入力 Left/Right では呼ばない。P1 専用に増築せず、両体で同じ処理を使います。
        /// </summary>
        private void UpdateFacingTowardOpponent(DebugFighterParticipant self)
        {
            if (self == null || self.Motor == null)
            {
                return;
            }

            DebugFighterParticipant opponent = self.Opponent;
            if (opponent == null || opponent.Motor == null)
            {
                return;
            }

            float selfX = self.Motor.LogicalX;
            float opponentX = opponent.Motor.LogicalX;
            float deltaX = opponentX - selfX;

            // 同位置付近では向きを切り替えない。
            // 理由: 浮動小数の微小差やすれ違い直後に、毎 CombatFrame で flipX が点滅するのを防ぐため。
            if (deltaX > FacingSameXEpsilon)
            {
                self.Motor.SetFacingRight(true);
            }
            else if (deltaX < -FacingSameXEpsilon)
            {
                self.Motor.SetFacingRight(false);
            }
            else
            {
                // |deltaX| <= epsilon: 直前 Facing を維持（何もしない）
            }
        }

        private void ApplyInitialFacingTowardOpponents()
        {
            UpdateFacingTowardOpponent(participantP1);
            UpdateFacingTowardOpponent(participantP2);
        }

        /// <summary>
        /// 同じ CombatFrame の両方向 Hit 候補を先に集め、分類してから結果を適用します。
        ///
        /// 処理順:
        /// 1. P1→P2 の命中候補を収集
        /// 2. P2→P1 の命中候補を収集
        /// 3. この段階では Damage や HitStun を適用しない
        /// 4. 双方候補の組み合わせを分類
        /// 5. Ground Clash / 片側 Guard / 片側 Normal Hit として結果を適用
        ///
        /// 分類方針（現行）:
        /// - 双方候補かつ双方とも地上攻撃 → Ground Clash（技種は問わない・Guardより先）
        /// - 双方候補だが Air Kick 等を含む → Air Clash 未実装のため結果未適用
        /// - 片側だけ候補成立 → Defender が立ちガード成立なら Guard、それ以外は Normal Hit
        ///   （P1: Back保持 / P2: DebugStandGuard Mode。共通の身体・技条件あり）
        /// </summary>
        private void CollectAndResolveHitsForCombatFrame()
        {
            pendingHitP1ToP2.Clear();
            pendingHitP2ToP1.Clear();
            lastHitResolutionType = DebugHitResolutionType.None;

            // 1. P1→P2 の命中候補を収集
            CollectPendingHit(participantP1, participantP2, pendingHitP1ToP2);
            // 2. P2→P1 の命中候補を収集
            CollectPendingHit(participantP2, participantP1, pendingHitP2ToP1);

            // 3. ここまでは候補だけ。Damage / HitStun / ClashRecoil / HitStop はまだ適用しない。
            // 4. 双方候補の組み合わせを分類 → 5. 結果適用
            if (pendingHitP1ToP2.IsValid && pendingHitP2ToP1.IsValid)
            {
                if (IsGroundAttackForClash(pendingHitP1ToP2.AttackId)
                    && IsGroundAttackForClash(pendingHitP2ToP1.AttackId))
                {
                    ApplyGroundClash(pendingHitP1ToP2, pendingHitP2ToP1);
                    lastHitResolutionType = DebugHitResolutionType.Clash;
                    return;
                }

                // Air を含む双方候補: Ground Clash にも通常 Hit / Guard にもしない。
                LogUnsupportedAirMutualHitOnce(pendingHitP1ToP2, pendingHitP2ToP1);
                return;
            }

            if (pendingHitP1ToP2.IsValid)
            {
                if (TryApplyStandGuard(pendingHitP1ToP2))
                {
                    lastHitResolutionType = DebugHitResolutionType.Guard;
                    return;
                }

                ApplyNormalHit(pendingHitP1ToP2);
                lastHitResolutionType = DebugHitResolutionType.NormalHit;
                return;
            }

            if (pendingHitP2ToP1.IsValid)
            {
                if (TryApplyStandGuard(pendingHitP2ToP1))
                {
                    lastHitResolutionType = DebugHitResolutionType.Guard;
                    return;
                }

                ApplyNormalHit(pendingHitP2ToP1);
                lastHitResolutionType = DebugHitResolutionType.NormalHit;
            }
        }

        /// <summary>
        /// Ground Clash 対象の地上攻撃かどうか。
        /// J Punch / Ground Kick（将来のしゃがみ技もここに足す想定）。
        /// Air Kick は含めない。
        /// </summary>
        private static bool IsGroundAttackForClash(DebugAttackId attackId)
        {
            return attackId == DebugAttackId.JPunch
                || attackId == DebugAttackId.GroundKick;
        }

        /// <summary>
        /// Air を含む双方命中候補を未対応として記録します。
        /// 結果は適用せず、警告だけセッション中1回出します。
        /// </summary>
        private void LogUnsupportedAirMutualHitOnce(
            DebugPendingHit p1ToP2,
            DebugPendingHit p2ToP1)
        {
            if (hasLoggedUnsupportedAirMutualHitWarning)
            {
                return;
            }

            hasLoggedUnsupportedAirMutualHitWarning = true;

            Debug.LogWarning(
                "[FightDebug] Unsupported mutual hit (includes air attack)."
                + " No NormalHit / GroundClash applied until Air Clash is defined."
                + " CombatFrame=" + timeState.CombatFrame
                + " P1Attack=" + p1ToP2.AttackId
                + " P2Attack=" + p2ToP1.AttackId
            );
        }

        /// <summary>
        /// 片方向の命中候補だけを作ります（処理順の 1 / 2）。
        /// Damage / HitStun / HitStop / MarkHit はここでは適用しません（処理順の 3）。
        /// Active・未Hit・HitBox×HurtBox 重なりが揃ったときだけ destination を Valid にします。
        /// </summary>
        private void CollectPendingHit(
            DebugFighterParticipant attacker,
            DebugFighterParticipant defender,
            DebugPendingHit destination)
        {
            destination.Clear();

            if (attacker == null || defender == null || attacker == defender)
            {
                return;
            }

            DebugFighterAttackState attackState = attacker.AttackState;
            if (attackState == null
                || attackState.IsActionPlaying == false
                || attackState.CurrentAttackData == null
                || defender.IsKnockedOut)
            {
                return;
            }

            DebugBox2D hitBox = attacker.EvaluateWorldHitBox();
            DebugBox2D hurtBox = defender.EvaluateWorldHurtBox();

            string checkLabel;
            bool boxesOverlap;
            bool isCandidate = DebugPunchHitResolver.TryResolveHit(
                attackState.IsActionPlaying,
                attackState.HasCurrentAttackHit,
                hitBox,
                hurtBox,
                out checkLabel,
                out boxesOverlap
            );

            if (attacker == participantP1 && defender == participantP2)
            {
                lastHitCheckLabel = checkLabel;
                lastBoxOverlap = boxesOverlap;
            }

            if (isCandidate == false)
            {
                return;
            }

            destination.IsValid = true;
            destination.Attacker = attacker;
            destination.Defender = defender;
            destination.AttackId = attackState.CurrentAttackId;
            destination.AttackData = attackState.CurrentAttackData;
            destination.AttackStartedCombatFrame = attackState.AttackStartedCombatFrame;
            destination.HitCombatFrame = timeState.CombatFrame;
            destination.DistanceX = Mathf.Abs(
                defender.Motor.LogicalX - attacker.Motor.LogicalX
            );

            DebugFighterAttackState defenderAttack = defender.AttackState;
            destination.DefenderWasAttacking =
                defenderAttack != null && defenderAttack.IsActionPlaying;
            if (destination.DefenderWasAttacking)
            {
                destination.DefenderAttackId = defenderAttack.CurrentAttackId;
                destination.DefenderAttackPhase = defenderAttack.CurrentPhase;
            }

            destination.HitBox = hitBox;
            destination.HurtBox = hurtBox;

            // 異技Clash検証用: 同一攻撃開始につき候補成立ログは1回だけ。
            LogPendingHitCandidateOnce(attacker, attackState, destination.DistanceX);
        }

        /// <summary>
        /// 命中候補が初めて Valid になったときだけログします（毎フレーム出さない）。
        /// </summary>
        private void LogPendingHitCandidateOnce(
            DebugFighterParticipant attacker,
            DebugFighterAttackState attackState,
            float distanceX)
        {
            if (attacker == null || attackState == null || timeState == null)
            {
                return;
            }

            int startedCf = attackState.AttackStartedCombatFrame;
            if (attacker == participantP1)
            {
                if (lastLoggedPendingHitStartedCombatFrameP1 == startedCf)
                {
                    return;
                }

                lastLoggedPendingHitStartedCombatFrameP1 = startedCf;
            }
            else if (attacker == participantP2)
            {
                if (lastLoggedPendingHitStartedCombatFrameP2 == startedCf)
                {
                    return;
                }

                lastLoggedPendingHitStartedCombatFrameP2 = startedCf;
            }
            else
            {
                return;
            }

            Debug.Log(
                "[FightDebug] Pending hit candidate"
                + " CombatFrame=" + timeState.CombatFrame
                + " attacker=" + attacker.SlotId
                + " defender=" + (attacker == participantP1 ? "P2" : "P1")
                + " attack=" + attackState.CurrentAttackId
                + " ActionFrame=" + attackState.ActionFrame
                + " distanceX=" + distanceX.ToString("0.00")
            );
        }

        /// <summary>
        /// 片側だけ成立した命中候補を通常 Hit として適用します（処理順の 5）。
        /// Damage / HitStun / Knockback / HitStop / MarkHit / KO 判定をここで行います。
        /// </summary>
        private void ApplyNormalHit(DebugPendingHit pendingHit)
        {
            if (pendingHit == null
                || pendingHit.IsValid == false
                || pendingHit.Attacker == null
                || pendingHit.Defender == null
                || pendingHit.AttackData == null)
            {
                return;
            }

            DebugFighterParticipant attacker = pendingHit.Attacker;
            DebugFighterParticipant defender = pendingHit.Defender;
            DebugAttackData attackData = pendingHit.AttackData;

            float knockbackVelocityX = ResolveKnockbackVelocityX(
                attacker,
                defender,
                attackData.KnockbackInitialVelocityX
            );

            defender.ReceiveHit(
                timeState.CombatFrame,
                attackData.HitStunFrames,
                knockbackVelocityX
            );

            int actualDamage = defender.ApplyDamage(attackData.Damage);
            if (defender.CurrentHitPoints <= 0)
            {
                defender.TryEnterKnockout();
            }

            if (attacker.AttackState != null)
            {
                attacker.AttackState.MarkHit();
            }

            RefreshOneFighterVisual(defender, false);
            timeState.HitStopRemaining = attackData.HitStopFrames;
            timeState.LastStatusMessage =
                attacker.SlotId + " " + attackData.AttackId + " Hit";

            Debug.Log(
                "[FightDebug] Normal hit"
                + " attack=" + attackData.AttackId
                + " attacker=" + attacker.SlotId
                + " defender=" + defender.SlotId
                + " CombatFrame=" + timeState.CombatFrame
                + " Damage=" + attackData.Damage
                + " actual=" + actualDamage
                + " distanceX=" + pendingHit.DistanceX.ToString("0.00")
            );
        }

        /// <summary>
        /// 片側候補を立ちガードとして適用できるなら適用し true を返します。
        ///
        /// 入力元（Wants）と身体・技条件（Can）を分けます。
        /// - P1: Gameplay 入力の Back 保持（正式入力経路。ガードシステム全体の完成ではない）
        /// - P2: debugP2StanceGuardMode == StandGuard（既存 Debug）
        /// 共通: CanStandGuard 技・接地・非KO・非CombatReaction・非攻撃Action・相手向き
        ///
        /// ChipDamage なし / HitCount 非加算 / HitStun 非流用。
        /// </summary>
        private bool TryApplyStandGuard(DebugPendingHit pendingHit)
        {
            if (pendingHit == null
                || pendingHit.IsValid == false
                || pendingHit.Attacker == null
                || pendingHit.Defender == null
                || pendingHit.AttackData == null)
            {
                return false;
            }

            if (WantsStandGuard(pendingHit.Defender) == false)
            {
                return false;
            }

            if (CanStandGuardBody(pendingHit) == false)
            {
                return false;
            }

            ApplyStandGuard(pendingHit);
            return true;
        }

        /// <summary>
        /// この Defender が「立ちガードしたい」入力元か。
        /// Facing 更新後・Hit 解決時に呼ぶ（Back は接触解決時点の Facing を使う）。
        /// </summary>
        private bool WantsStandGuard(DebugFighterParticipant defender)
        {
            if (defender == null)
            {
                return false;
            }

            // P2 Debug StandGuard（既存。正式後ろ入力ではない）
            if (defender == participantP2
                && debugP2StanceGuardMode == DebugP2StanceGuardMode.StandGuard)
            {
                return true;
            }

            // P1 正式入力経路: 接触 CF で Back 保持のみ（猶予・履歴なし）
            if (defender == participantP1 && defender.UsesGameplayInput)
            {
                SimulationInputState input = ResolveInputForParticipant(defender);
                return IsBackHeld(defender, input);
            }

            return false;
        }

        /// <summary>
        /// 相手と反対方向の単一方向保持か。
        /// FacingRight + Left（Rightなし）／FacingLeft + Right（Leftなし）。
        /// 左右同時・Neutral は false。
        /// </summary>
        private static bool IsBackHeld(
            DebugFighterParticipant participant,
            SimulationInputState input)
        {
            if (participant == null
                || participant.Motor == null
                || input == null)
            {
                return false;
            }

            // Down + Back は将来のしゃがみガード候補。
            // 今回の最小立ちガードには流さない。
            if (input.Down)
            {
                return false;
            }

            bool facingRight = participant.Motor.FacingRight;
            if (facingRight && input.Left && input.Right == false)
            {
                return true;
            }

            if (facingRight == false && input.Right && input.Left == false)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 立ちガードの身体・技条件（入力元は見ない）。
        /// </summary>
        private bool CanStandGuardBody(DebugPendingHit pendingHit)
        {
            if (pendingHit == null
                || pendingHit.IsValid == false
                || pendingHit.Attacker == null
                || pendingHit.Defender == null
                || pendingHit.AttackData == null)
            {
                return false;
            }

            DebugAttackData attackData = pendingHit.AttackData;
            if (attackData.CanStandGuard == false)
            {
                return false;
            }

            DebugFighterParticipant defender = pendingHit.Defender;
            if (defender.IsKnockedOut
                || defender.IsInCombatReaction
                || defender.Motor == null
                || defender.Motor.IsGrounded == false)
            {
                return false;
            }

            if (defender.AttackState != null && defender.AttackState.IsActionPlaying)
            {
                return false;
            }

            if (IsFacingTowardOpponent(defender, pendingHit.Attacker) == false)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 相手の LogicalX 方向を向いているか（正面ガード用）。
        /// Facing 更新は Hit 収集前に済んでいる前提。
        /// </summary>
        private static bool IsFacingTowardOpponent(
            DebugFighterParticipant self,
            DebugFighterParticipant opponent)
        {
            if (self == null
                || opponent == null
                || self.Motor == null
                || opponent.Motor == null)
            {
                return false;
            }

            float deltaX = opponent.Motor.LogicalX - self.Motor.LogicalX;
            if (deltaX > FacingSameXEpsilon)
            {
                return self.Motor.FacingRight;
            }

            if (deltaX < -FacingSameXEpsilon)
            {
                return self.Motor.FacingRight == false;
            }

            // ほぼ同位置: 直前 Facing を維持している前提で成立扱い。
            return true;
        }

        /// <summary>
        /// 片側候補を立ちガードとして適用します。
        /// Damage 0 / HitCount 非加算 / GuardStun + 小Pushback / HitStop / MarkGuarded。
        /// </summary>
        private void ApplyStandGuard(DebugPendingHit pendingHit)
        {
            DebugFighterParticipant attacker = pendingHit.Attacker;
            DebugFighterParticipant defender = pendingHit.Defender;
            DebugAttackData attackData = pendingHit.AttackData;

            float pushbackVelocityX = ResolveKnockbackVelocityX(
                attacker,
                defender,
                attackData.GuardPushbackInitialVelocityX
            );

            defender.ReceiveGuardStun(
                attackData.GuardStunFrames,
                pushbackVelocityX
            );

            if (attacker.AttackState != null)
            {
                attacker.AttackState.MarkGuarded();
            }

            RefreshOneFighterVisual(defender, false);
            timeState.HitStopRemaining = attackData.HitStopFrames;
            timeState.LastStatusMessage =
                attacker.SlotId + " " + attackData.AttackId + " Guarded";

            // via: Play で P1 Back と P2 Debug Mode を区別する（本番恒常ログではない）
            string viaLabel = "Unknown";
            if (defender == participantP1)
            {
                viaLabel = "Back";
            }
            else if (defender == participantP2)
            {
                viaLabel = "DebugStandGuard";
            }

            Debug.Log(
                "[FightDebug] Stand guard"
                + " attack=" + attackData.AttackId
                + " attacker=" + attacker.SlotId
                + " defender=" + defender.SlotId
                + " via=" + viaLabel
                + " CombatFrame=" + timeState.CombatFrame
                + " GuardStun=" + attackData.GuardStunFrames
                + " ChipDamage=0"
                + " HitCount unchanged"
                + " distanceX=" + pendingHit.DistanceX.ToString("0.00")
            );
        }

        /// <summary>
        /// 地上攻撃同士の双方命中を Ground Clash として適用します。
        /// Damage 0 / 双方 HitStop / 双方 ClashRecoil。通常 HitStun・HitCount には入れません。
        /// 呼び出し側で双方とも IsGroundAttackForClash であることを保証してください。
        /// </summary>
        private void ApplyGroundClash(
            DebugPendingHit p1ToP2,
            DebugPendingHit p2ToP1)
        {
            DebugFighterParticipant p1 = p1ToP2.Attacker;
            DebugFighterParticipant p2 = p2ToP1.Attacker;
            if (p1 == null || p2 == null)
            {
                return;
            }

            float p1Knockback = ResolveKnockbackVelocityX(
                p2,
                p1,
                DebugClashTuning.HorizontalKnockback
            );
            float p2Knockback = ResolveKnockbackVelocityX(
                p1,
                p2,
                DebugClashTuning.HorizontalKnockback
            );

            if (p1.AttackState != null)
            {
                p1.AttackState.EndAttackAsClash();
            }

            if (p2.AttackState != null)
            {
                p2.AttackState.EndAttackAsClash();
            }

            p1.ReceiveGroundClashRecoil(
                DebugClashTuning.ClashStunFrames,
                p1Knockback
            );
            p2.ReceiveGroundClashRecoil(
                DebugClashTuning.ClashStunFrames,
                p2Knockback
            );

            timeState.HitStopRemaining = DebugClashTuning.HitStopFrames;
            timeState.LastStatusMessage = "Ground Clash";

            Debug.Log(
                "[FightDebug] Ground Clash"
                + " CombatFrame=" + timeState.CombatFrame
                + " P1Attack=" + p1ToP2.AttackId
                + " P2Attack=" + p2ToP1.AttackId
                + " Damage=0"
            );
        }

        /// <summary>
        /// ノックバック初速の符号付き値を決めます（段階13A）。
        ///
        /// 正本は Hit 成立時点の LogicalX 比較（Facing だけを正本にしない）。
        /// attacker.X &lt; defender.X → 右（+speed）
        /// attacker.X &gt; defender.X → 左（-speed）
        /// 同位置 → attacker の Facing を fallback
        /// 大きさは引数 speed（絶対値）。攻撃データまたは Clash 仮数値を渡します。
        /// </summary>
        private static float ResolveKnockbackVelocityX(
            DebugFighterParticipant attacker,
            DebugFighterParticipant defender,
            float speed)
        {
            speed = Mathf.Abs(speed);
            if (speed == 0f
                || attacker == null
                || defender == null
                || attacker.Motor == null
                || defender.Motor == null)
            {
                return 0f;
            }

            float attackerX = attacker.Motor.LogicalX;
            float defenderX = defender.Motor.LogicalX;

            if (attackerX < defenderX)
            {
                return speed;
            }

            if (attackerX > defenderX)
            {
                return -speed;
            }

            // 同位置: Facing を fallback（攻撃者が向いている側へ押し出す）
            return attacker.Motor.FacingRight ? speed : -speed;
        }

        private void UpdateStatusMessage()
        {
            DebugFighterAttackState p1Attack = null;
            if (participantP1 != null)
            {
                p1Attack = participantP1.AttackState;
            }

            if (p1Attack != null && p1Attack.IsActionPlaying)
            {
                if (p1Attack.IsJPunchAttack)
                {
                    timeState.LastStatusMessage = "J Punch " + GetPunchPhaseLabel();
                }
                else if (p1Attack.IsGroundKickAttack)
                {
                    timeState.LastStatusMessage = "GroundKick " + GetPunchPhaseLabel();
                }
                else if (p1Attack.IsAirKickAttack)
                {
                    // 同じ kickSequence を使っていても、HUD上の攻撃名は Ground Kick と区別します。
                    timeState.LastStatusMessage = "AirKick " + GetPunchPhaseLabel();
                }
                else
                {
                    timeState.LastStatusMessage = "Debug Action active";
                }
            }
            else
            {
                timeState.LastStatusMessage = "Combat ok / Action stop";
            }
        }

        private void SampleCurrentInputFromGameplay()
        {
            if (timeState.CurrentInput == null)
            {
                timeState.CurrentInput = new SimulationInputState();
            }

            // 物理 Held は DebugGameplayInput から読む（消さない）。
            // 共通 release gate 中は、この物理値で解除判定だけ行い、
            // Simulation へ渡す有効入力はニュートラルへ落とす。
            bool left = false;
            bool right = false;
            bool up = false;
            bool down = false;
            bool attack = false;
            bool kick = false;

            if (debugGameplayInput != null)
            {
                left = debugGameplayInput.IsLeftPressed;
                right = debugGameplayInput.IsRightPressed;
                up = debugGameplayInput.IsUpPressed;
                down = debugGameplayInput.IsDownPressed;
                attack = debugGameplayInput.IsAttackPressed;
                kick = debugGameplayInput.IsKickPressed;
            }

            if (waitForAllGameplayInputReleaseAfterReset)
            {
                // ------------------------------------------------------------
                // 全ゲーム操作が離れるまで有効入力を通さない。
                // R は DebugPlaybackInput のためここには含まれない。
                //
                // 一部だけ離しても解除しない。
                // 全解除したサンプルでも有効入力はニュートラルのままにし、
                // previous エッジを false へ再同期して偽エッジを防ぐ。
                // 次の新しい押下から通常受付を再開する。
                // ------------------------------------------------------------
                bool anyGameplayHeld = SimulationInputState.HasAnyGameplayInputHeld(
                    left,
                    right,
                    up,
                    down,
                    attack,
                    kick);

                timeState.CurrentInput.CopyFromPhysicalAndCommit(
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    timeState.SimulationTick);

                if (anyGameplayHeld == false)
                {
                    waitForAllGameplayInputReleaseAfterReset = false;
                    ResyncGameplayInputEdgePreviousAfterReleaseGate();
                    Debug.Log("[FightDebug] Gameplay input re-enabled after Training Reset");
                }

                return;
            }

            timeState.CurrentInput.CopyFromPhysicalAndCommit(
                left,
                right,
                up,
                down,
                attack,
                kick,
                timeState.SimulationTick
            );
        }

        private void WriteConsoleLogIfNeeded()
        {
            if (consoleLogIntervalTicks <= 0)
            {
                return;
            }

            bool shouldLog = (timeState.SimulationTick % consoleLogIntervalTicks) == 0;
            if (shouldLog == false)
            {
                return;
            }

            DebugFighterAttackState p1Attack = null;
            if (participantP1 != null)
            {
                p1Attack = participantP1.AttackState;
            }

            bool isActionPlaying = false;
            int actionFrame = 0;
            bool punchHitDoneFlag = false;
            string attackResult = "None";

            if (p1Attack != null)
            {
                isActionPlaying = p1Attack.IsActionPlaying;
                actionFrame = p1Attack.ActionFrame;
                punchHitDoneFlag = p1Attack.HasCurrentJPunchHit;
                attackResult = p1Attack.LastAttackResult;
            }

            if (string.IsNullOrEmpty(attackResult))
            {
                attackResult = "None";
            }

            string actionPlayingLabel = isActionPlaying ? "true" : "false";

            SimulationInputState input = timeState.CurrentInput;
            if (input == null)
            {
                input = new SimulationInputState();
            }

            int leftValue = input.Left ? 1 : 0;
            int rightValue = input.Right ? 1 : 0;
            int upValue = input.Up ? 1 : 0;
            int downValue = input.Down ? 1 : 0;
            int attackValue = input.Attack ? 1 : 0;
            int attackPressedValue = AttackPressedThisTick ? 1 : 0;
            int punchHitDone = punchHitDoneFlag ? 1 : 0;

            string p1Part = "";
            DebugFighterMotor p1Motor = DebugFighterMotor;
            if (p1Motor != null)
            {
                string facingLabel = p1Motor.FacingRight ? "true" : "false";
                p1Part =
                    " P1X=" + p1Motor.LogicalX.ToString("0.00")
                    + " P1FacingRight=" + facingLabel;
            }

            string visualLabel = "Idle";
            DebugFighterVisual p1Visual = DebugFighterVisual;
            if (p1Visual != null)
            {
                visualLabel = p1Visual.CurrentVisualLabel;
            }

            string p2Part = "";
            if (participantP2 != null && participantP2.Motor != null)
            {
                string p2FacingLabel = participantP2.Motor.FacingRight ? "true" : "false";
                p2Part =
                    " P2X=" + participantP2.Motor.LogicalX.ToString("0.00")
                    + " P2FacingRight=" + p2FacingLabel;
            }

            string p2HitPart = "";
            if (participantP2 != null)
            {
                p2HitPart = " P2HitCount=" + participantP2.HitCount;
            }

            Debug.Log(
                "[FightDebug] SimulationTick=" + timeState.SimulationTick
                + " / CombatFrame=" + timeState.CombatFrame
                + " / ActionFrame=" + actionFrame
                + " / IsActionPlaying=" + actionPlayingLabel
                + " / HitStopRemaining=" + timeState.HitStopRemaining
                + "\nInputSample=" + input.SampleSequence
                + " InputTick=" + input.SampledAtSimulationTick
                + " L=" + leftValue
                + " R=" + rightValue
                + " U=" + upValue
                + " D=" + downValue
                + " Attack=" + attackValue
                + " AttackPressed=" + attackPressedValue
                + " FighterVisual=" + visualLabel
                + p1Part
                + p2Part
                + p2HitPart
                + " PunchPhase=" + GetPunchPhaseLabel()
                + " AttackResult=" + attackResult
                + " PunchHitDone=" + punchHitDone
                + "\n（" + timeState.LastStatusMessage + "）"
            );
        }
    }
}
