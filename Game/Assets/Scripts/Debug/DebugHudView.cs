using System.Text;
using FightingGameTrial.Combat;
using FightingGameTrial.Fighter;
using FightingGameTrial.Input;
using FightingGameTrial.Simulation;
using TMPro;
using UnityEngine;

namespace FightingGameTrial.DebugTools
{
    /// <summary>
    /// デバッグ用の画面表示だけを担当します。
    ///
    /// 攻撃状態表示の正本:
    /// P1 DebugFighterParticipant.AttackState（Action / AF / Punch Phase / AttackResult / PunchHitDone）。
    ///
    /// SimulationTimeState が担う共有時間状態:
    /// SimulationTick / CombatFrame / Pause / HitStop / Step / Status / 入力サンプル。
    ///
    /// レイアウト（段階12A 後）:
    /// - 状態行: 左上（既存 hudText）
    /// - 操作説明: 左下（ランタイム生成の別 TMP）
    /// 縦に1本で積み続けず、状態行が増えても操作説明へ重ならないようにする。
    ///
    /// Inspector 明示参照のみ使い、検索はしません。
    /// Scene の手動再配線は不要です（操作ブロックは Awake で構築）。
    /// </summary>
    public class DebugHudView : MonoBehaviour
    {
        private const float HudMarginPixels = 28f;

        /// <summary>
        /// 操作説明ブロック高さ。1行分（約28px）を状態側へ譲り、
        /// JPunch Data が Truncate で切れにくくする（Scene は触らない）。
        /// </summary>
        private const float HelpBlockHeightPixels = 108f;

        private const float StatusHelpGapPixels = 16f;

        /// <summary>
        /// 状態 HUD 本文用の再利用バッファ（GC-2）。
        /// 毎 Frame new せず、Clear して容量を再利用する。
        /// 初期容量は現状の HUD 行数を見ての余裕込み。
        /// </summary>
        private readonly StringBuilder statusTextBuilder = new StringBuilder(2048);

        [Header("参照（Inspectorで接続。自動検索はしません）")]
        [Tooltip("時間状態と P1/P2 Participant 参照へ到達するための SimulationSession です。")]
        [SerializeField]
        private SimulationSession simulationSession;

        [Tooltip("左上の状態表示用 TextMeshProUGUI です。")]
        [SerializeField]
        private TextMeshProUGUI hudText;

        /// <summary>
        /// 左下の操作説明用。Awake で既存 hudText と同 Canvas 配下へ生成します。
        /// </summary>
        private TextMeshProUGUI helpText;

        private void Awake()
        {
            if (simulationSession == null)
            {
                Debug.LogError("DebugHudView: SimulationSession が未設定です。");
            }

            if (hudText == null)
            {
                Debug.LogError("DebugHudView: TextMeshProUGUI が未設定です。");
                return;
            }

            EnsureSplitHudLayout();
        }

        private void Update()
        {
            if (simulationSession == null || hudText == null)
            {
                return;
            }

            SimulationTimeState timeState = simulationSession.TimeState;
            if (timeState == null)
            {
                return;
            }

            // 状態 HUD は動的値のため毎描画 Frame で再構築する（表示内容は変えない）。
            // StringBuilder を Clear 再利用し、中間 string の大量生成を抑える（GC-2）。
            BuildStatusHudText(timeState);
            // TMP_Text.SetText(StringBuilder) は導入パッケージに実在する API。
            // 最終の string をこちらで ToString しない（Editor 内の TMP 処理は別問題）。
            hudText.SetText(statusTextBuilder);

            // Help 本文は固定文言のため、EnsureSplitHudLayout（Awake）で1回だけ設定する。
            // Update で毎 Frame 再構築すると、不要な string 生成と TMP 再代入の候補になる。
        }

        /// <summary>
        /// 状態=左上、操作=左下の2ブロック構成にします。
        /// Font Asset は既存 hudText の参照をコピーするだけで、アセット自体は変更しません。
        /// </summary>
        private void EnsureSplitHudLayout()
        {
            RectTransform statusRect = hudText.rectTransform;
            Transform canvasTransform = statusRect.parent;
            if (canvasTransform == null)
            {
                Debug.LogError("DebugHudView: hudText の親 Canvas がありません。");
                return;
            }

            RectTransform canvasRect = canvasTransform as RectTransform;
            float canvasHeight = 720f;
            if (canvasRect != null)
            {
                canvasHeight = canvasRect.rect.height;
                if (canvasHeight < 1f)
                {
                    canvasHeight = 720f;
                }
            }

            // 左上状態領域: 下端に操作ブロック＋余白分を残し、はみ出しを防ぐ。
            // HelpBlockHeightPixels を抑えた分だけ statusHeight が増える（重ならない）。
            float statusHeight =
                canvasHeight
                - HudMarginPixels
                - HelpBlockHeightPixels
                - StatusHelpGapPixels
                - HudMarginPixels;
            if (statusHeight < 200f)
            {
                statusHeight = 200f;
            }

            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(0f, 1f);
            statusRect.pivot = new Vector2(0f, 1f);
            statusRect.anchoredPosition = new Vector2(HudMarginPixels, -HudMarginPixels);
            statusRect.sizeDelta = new Vector2(statusRect.sizeDelta.x, statusHeight);

            hudText.verticalAlignment = VerticalAlignmentOptions.Top;
            hudText.overflowMode = TextOverflowModes.Truncate;

            // 既に生成済みなら使い回す（Domain Reload なしの再入対策）。
            Transform existingHelp = canvasTransform.Find("DebugHudHelpText");
            GameObject helpObject;
            if (existingHelp != null)
            {
                helpObject = existingHelp.gameObject;
                helpText = helpObject.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                helpObject = new GameObject("DebugHudHelpText", typeof(RectTransform));
                helpObject.transform.SetParent(canvasTransform, false);
                helpObject.layer = hudText.gameObject.layer;
                helpText = helpObject.AddComponent<TextMeshProUGUI>();
            }

            if (helpText == null)
            {
                Debug.LogError("DebugHudView: 操作説明用 TextMeshProUGUI を作れませんでした。");
                return;
            }

            RectTransform helpRect = helpText.rectTransform;
            helpRect.anchorMin = new Vector2(0f, 0f);
            helpRect.anchorMax = new Vector2(0f, 0f);
            helpRect.pivot = new Vector2(0f, 0f);
            helpRect.anchoredPosition = new Vector2(HudMarginPixels, HudMarginPixels);
            helpRect.sizeDelta = new Vector2(
                statusRect.sizeDelta.x > 1f ? statusRect.sizeDelta.x : 536f,
                HelpBlockHeightPixels
            );

            // 見た目は状態表示に合わせる（Font Asset ファイルは触らない）。
            helpText.font = hudText.font;
            helpText.fontSharedMaterial = hudText.fontSharedMaterial;
            helpText.fontSize = hudText.fontSize;
            helpText.color = hudText.color;
            helpText.alignment = TextAlignmentOptions.BottomLeft;
            helpText.verticalAlignment = VerticalAlignmentOptions.Bottom;
            helpText.overflowMode = TextOverflowModes.Overflow;
            helpText.enableWordWrapping = true;
            helpText.raycastTarget = false;
            // 固定 Help 本文の正本設定箇所（Awake → EnsureSplitHudLayout で1回）。
            helpText.text = BuildHelpHudText();
        }

        /// <summary>
        /// 左下固定の操作説明（内容は削除せず、配置だけ分ける）。
        /// 固定文言のため Update では呼ばず、EnsureSplitHudLayout で1回だけ使う。
        /// </summary>
        private string BuildHelpHudText()
        {
            string text = "";
            text = text + "操作:\n";
            text = text + "Space=Pause切替      .=1Tick送り\n";
            text = text + "H=HitStop            A=Action開始\n";
            text = text + "R=Combatリセット     矢印=方向\n";
            text = text + "Up=Jump              J=Attack";
            return text;
        }

        /// <summary>
        /// 左上の状態行のみを statusTextBuilder へ構築する（GC-2）。
        /// Clear は内容を消すが確保済み容量は再利用する。表示内容・改行は従来どおり。
        /// 改行は既存どおり '\n'（AppendLine の Environment.NewLine は使わない）。
        /// </summary>
        private void BuildStatusHudText(SimulationTimeState timeState)
        {
            statusTextBuilder.Clear();

            // P1 AttackState を1回だけ取得（攻撃表示の正本。重複取得しない）
            DebugFighterParticipant p1 = simulationSession.ParticipantP1;
            DebugFighterAttackState attackState = null;
            if (p1 != null)
            {
                attackState = p1.AttackState;
            }

            string pauseLabel = timeState.IsPaused ? "Yes" : "No";

            // Action 表示: P1 AttackState が正本
            string actionPlayingLabel = "No";
            if (attackState != null && attackState.IsActionPlaying)
            {
                actionPlayingLabel = "Yes";
            }

            // AF 表示: P1 AttackState が正本
            int actionFrameValue = 0;
            if (attackState != null)
            {
                actionFrameValue = attackState.ActionFrame;
            }

            string stepLabel = timeState.LastStepResult;
            if (string.IsNullOrEmpty(stepLabel))
            {
                stepLabel = "-";
            }

            string statusLabel = timeState.LastStatusMessage;
            if (string.IsNullOrEmpty(statusLabel))
            {
                statusLabel = "-";
            }

            SimulationInputState input = timeState.CurrentInput;
            if (input == null)
            {
                input = new SimulationInputState();
            }

            string leftLabel = input.Left ? "1" : "0";
            string rightLabel = input.Right ? "1" : "0";
            string upLabel = input.Up ? "1" : "0";
            string downLabel = input.Down ? "1" : "0";
            string attackHeldLabel = input.Attack ? "1" : "0";
            string attackPressedLabel = simulationSession.AttackPressedThisTick ? "1" : "0";

            // ASCII labels only (avoid adding new JP glyphs to NotoSansJP SDF).
            string p1XLabel = "-";
            string p1FacingLabel = "-";
            if (p1 != null && p1.Motor != null)
            {
                // 小数桁は既存書式を維持（GC-2 では数値 ToString の完全除去はしない）
                p1XLabel = p1.Motor.LogicalX.ToString("0.00");
                p1FacingLabel = p1.Motor.FacingRight ? "R" : "L";
            }

            string visualLabel = "Idle";
            if (p1 != null && p1.Visual != null)
            {
                visualLabel = p1.Visual.CurrentVisualLabel;
            }

            string p1YLabel = "-";
            string p1JumpTypeLabel = "None";
            if (p1 != null && p1.Motor != null)
            {
                p1YLabel = p1.Motor.LogicalY.ToString("0.00");
                p1JumpTypeLabel = p1.Motor.CurrentJumpType.ToString();
            }

            string p2XLabel = "-";
            string p2FacingLabel = "-";
            DebugFighterParticipant p2 = simulationSession.ParticipantP2;
            if (p2 != null && p2.Motor != null)
            {
                p2XLabel = p2.Motor.LogicalX.ToString("0.00");
                p2FacingLabel = p2.Motor.FacingRight ? "R" : "L";
            }

            string p2HitLabel = "0";
            string p2HitStunLabel = "0";
            string p2StateLabel = "Idle";
            string p1StateLabel = "Idle";
            if (p2 != null)
            {
                p2HitLabel = p2.HitCount.ToString();
                if (p2.HitState != null)
                {
                    p2HitStunLabel = p2.HitState.HitStunRemainingFrames.ToString();
                }

                p2StateLabel = p2.BuildDebugStateLabel(timeState.HitStopRemaining);
            }

            if (p1 != null)
            {
                p1StateLabel = p1.BuildDebugStateLabel(timeState.HitStopRemaining);
            }

            string punchPhase = simulationSession.GetPunchPhaseLabel();
            if (string.IsNullOrEmpty(punchPhase))
            {
                punchPhase = "Idle";
            }

            // AttackResult / PunchHitDone も同じ attackState を再利用
            string attackResult = "None";
            string punchHitDone = "0";
            if (attackState != null)
            {
                attackResult = attackState.LastAttackResult;
                if (attackState.HasCurrentJPunchHit)
                {
                    punchHitDone = "1";
                }
            }

            if (string.IsNullOrEmpty(attackResult))
            {
                attackResult = "None";
            }

            // --- Tick / Combat / AF ---
            statusTextBuilder.Append("Tick/Combat/AF : ");
            statusTextBuilder.Append(timeState.SimulationTick);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(timeState.CombatFrame);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(actionFrameValue);
            statusTextBuilder.Append('\n');

            // --- Action / Pause / HitStop ---
            statusTextBuilder.Append("Action/Pause/HS: ");
            statusTextBuilder.Append(actionPlayingLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(pauseLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(timeState.HitStopRemaining);
            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("Step           : ");
            statusTextBuilder.Append(stepLabel);
            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("LRUD / Atk H/P : ");
            statusTextBuilder.Append(leftLabel);
            statusTextBuilder.Append(rightLabel);
            statusTextBuilder.Append(upLabel);
            statusTextBuilder.Append(downLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(attackHeldLabel);
            statusTextBuilder.Append('/');
            statusTextBuilder.Append(attackPressedLabel);
            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("P1 X/Face/St   : ");
            statusTextBuilder.Append(p1XLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(p1FacingLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(p1StateLabel);
            statusTextBuilder.Append('\n');

            // --- HP / Life（段階14A）---
            statusTextBuilder.Append("P1 HP          : ");
            if (p1 != null)
            {
                statusTextBuilder.Append(p1.CurrentHitPoints);
                statusTextBuilder.Append(" / ");
                statusTextBuilder.Append(p1.MaxHitPoints);
            }
            else
            {
                statusTextBuilder.Append('-');
            }

            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("P1 Life        : ");
            if (p1 != null)
            {
                statusTextBuilder.Append(p1.BuildLifeLabel());
            }
            else
            {
                statusTextBuilder.Append('-');
            }

            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("Sprite         : ");
            statusTextBuilder.Append(visualLabel);
            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("P1 Y/Jump      : ");
            statusTextBuilder.Append(p1YLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(p1JumpTypeLabel);
            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("P2 X/Face/Hit  : ");
            statusTextBuilder.Append(p2XLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(p2FacingLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(p2HitLabel);
            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("P2 HP          : ");
            if (p2 != null)
            {
                statusTextBuilder.Append(p2.CurrentHitPoints);
                statusTextBuilder.Append(" / ");
                statusTextBuilder.Append(p2.MaxHitPoints);
            }
            else
            {
                statusTextBuilder.Append('-');
            }

            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("P2 Life        : ");
            if (p2 != null)
            {
                statusTextBuilder.Append(p2.BuildLifeLabel());
            }
            else
            {
                statusTextBuilder.Append('-');
            }

            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("P2 Stun/State  : ");
            statusTextBuilder.Append(p2HitStunLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(p2StateLabel);
            statusTextBuilder.Append('\n');

            // --- Knockback（段階13A）---
            string p2KnockbackVxLabel = "0.000";
            string p2KnockbackActiveLabel = "0";
            if (p2 != null)
            {
                p2KnockbackVxLabel = p2.KnockbackVelocityX.ToString("0.000");
                if (p2.IsBeingKnockedBack)
                {
                    p2KnockbackActiveLabel = "1";
                }
            }

            statusTextBuilder.Append("P2 KB Vx/Act   : ");
            statusTextBuilder.Append(p2KnockbackVxLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(p2KnockbackActiveLabel);
            statusTextBuilder.Append('\n');

            // --- J Punch 攻撃データ（段階15）---
            DebugAttackData jPunchData = DebugAttackData.JPunch;
            statusTextBuilder.Append("JPunch Data    : S/A/R ");
            statusTextBuilder.Append(jPunchData.StartupFrames);
            statusTextBuilder.Append('/');
            statusTextBuilder.Append(jPunchData.ActiveFrames);
            statusTextBuilder.Append('/');
            statusTextBuilder.Append(jPunchData.RecoveryFrames);
            statusTextBuilder.Append(" Dmg ");
            statusTextBuilder.Append(jPunchData.Damage);
            statusTextBuilder.Append(" HStop ");
            statusTextBuilder.Append(jPunchData.HitStopFrames);
            statusTextBuilder.Append(" HStun ");
            statusTextBuilder.Append(jPunchData.HitStunFrames);
            statusTextBuilder.Append(" KB ");
            statusTextBuilder.Append(jPunchData.KnockbackInitialVelocityX.ToString("0.000"));
            statusTextBuilder.Append('\n');

            // --- Push（段階10B-3）---
            string pushDistLabel = simulationSession.LastPushCenterDistance.ToString("0.00");
            string pushOverlapLabel = simulationSession.LastPushWasOverlapping ? "1" : "0";
            statusTextBuilder.Append("Push Dist/Over : ");
            statusTextBuilder.Append(pushDistLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(pushOverlapLabel);
            statusTextBuilder.Append('\n');

            // --- Box 可視化（段階11A）---
            string pushBoxOnLabel = "0";
            string hurtBoxOnLabel = "0";
            string hitBoxOnLabel = "0";
            string hitBoxCenterXLabel = "-";
            if (p1 != null)
            {
                DebugBox2D pushWorld = p1.EvaluateWorldPushBox();
                DebugBox2D hurtWorld = p1.EvaluateWorldHurtBox();
                DebugBox2D hitWorld = p1.EvaluateWorldHitBox();
                if (pushWorld != null && pushWorld.IsActive)
                {
                    pushBoxOnLabel = "1";
                }

                if (hurtWorld != null && hurtWorld.IsActive)
                {
                    hurtBoxOnLabel = "1";
                }

                if (hitWorld != null && hitWorld.IsActive)
                {
                    hitBoxOnLabel = "1";
                    hitBoxCenterXLabel = hitWorld.CenterX.ToString("0.00");
                }
            }

            statusTextBuilder.Append("Box P/H/Hit    : ");
            statusTextBuilder.Append(pushBoxOnLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(hurtBoxOnLabel);
            statusTextBuilder.Append(" / ");
            statusTextBuilder.Append(hitBoxOnLabel);
            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("HitBox CenterX : ");
            statusTextBuilder.Append(hitBoxCenterXLabel);
            statusTextBuilder.Append('\n');

            // --- Hit 判定ラベル（段階11B）---
            string boxOverlapLabel = simulationSession.LastBoxOverlap ? "1" : "0";
            string hitCheckLabel = simulationSession.LastHitCheckLabel;
            if (string.IsNullOrEmpty(hitCheckLabel))
            {
                hitCheckLabel = "Inactive";
            }

            statusTextBuilder.Append("BoxOverlap     : ");
            statusTextBuilder.Append(boxOverlapLabel);
            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("HitCheck       : ");
            statusTextBuilder.Append(hitCheckLabel);
            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("Punch Phase    : ");
            statusTextBuilder.Append(punchPhase);
            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("AttackResult   : ");
            statusTextBuilder.Append(attackResult);
            statusTextBuilder.Append('\n');

            statusTextBuilder.Append("PunchHitDone   : ");
            statusTextBuilder.Append(punchHitDone);
            statusTextBuilder.Append('\n');

            // 最終行は従来どおり末尾改行なし
            statusTextBuilder.Append("Status         : ");
            statusTextBuilder.Append(statusLabel);
        }
    }
}
