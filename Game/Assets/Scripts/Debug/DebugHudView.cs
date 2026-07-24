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
        private const float HelpBlockHeightPixels = 120f;
        private const float StatusHelpGapPixels = 16f;

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

            hudText.text = BuildStatusHudText(timeState);

            if (helpText != null)
            {
                helpText.text = BuildHelpHudText();
            }
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
            helpText.text = BuildHelpHudText();
        }

        /// <summary>
        /// 左下固定の操作説明（内容は削除せず、配置だけ分ける）。
        /// </summary>
        private string BuildHelpHudText()
        {
            string text = "";
            text = text + "操作:\n";
            text = text + "Space=Pause切替      .=1Tick送り\n";
            text = text + "H=HitStop            A=Action開始\n";
            text = text + "R=Combatリセット     矢印=方向\n";
            text = text + "J=Attack";
            return text;
        }

        /// <summary>
        /// 左上の状態行のみ。追加ラベルは ASCII 優先（TMP欠落回避）。
        /// </summary>
        private string BuildStatusHudText(SimulationTimeState timeState)
        {
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
                p1XLabel = p1.Motor.LogicalX.ToString("0.00");
                p1FacingLabel = p1.Motor.FacingRight ? "R" : "L";
            }

            string visualLabel = "Idle";
            if (p1 != null && p1.Visual != null)
            {
                visualLabel = p1.Visual.CurrentVisualLabel;
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

            string text = "";
            // Tick / Combat は timeState。AF だけ AttackState。
            text = text + "Tick/Combat/AF : " + timeState.SimulationTick
                + " / " + timeState.CombatFrame
                + " / " + actionFrameValue + "\n";
            // Action は AttackState。Pause / HitStop は timeState。
            text = text + "Action/Pause/HS: " + actionPlayingLabel
                + " / " + pauseLabel
                + " / " + timeState.HitStopRemaining + "\n";
            text = text + "Step           : " + stepLabel + "\n";
            text = text + "LRUD / Atk H/P : " + leftLabel + rightLabel + upLabel + downLabel
                + " / " + attackHeldLabel + "/" + attackPressedLabel + "\n";
            text = text + "P1 X/Face/St   : " + p1XLabel
                + " / " + p1FacingLabel
                + " / " + p1StateLabel + "\n";
            text = text + "Sprite         : " + visualLabel + "\n";
            text = text + "P2 X/Face/Hit  : " + p2XLabel
                + " / " + p2FacingLabel
                + " / " + p2HitLabel + "\n";
            text = text + "P2 Stun/State  : " + p2HitStunLabel
                + " / " + p2StateLabel + "\n";

            // Push Box（段階10B-3）: ASCII のみ。常時大量ログは出さず HUD で確認する。
            string pushDistLabel = simulationSession.LastPushCenterDistance.ToString("0.00");
            string pushOverlapLabel = simulationSession.LastPushWasOverlapping ? "1" : "0";
            text = text + "Push Dist/Over : " + pushDistLabel
                + " / " + pushOverlapLabel + "\n";

            // Box 可視化（段階11A）: P1 の World Box 状態を短く表示。
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

            text = text + "Box P/H/Hit    : " + pushBoxOnLabel
                + " / " + hurtBoxOnLabel
                + " / " + hitBoxOnLabel + "\n";
            text = text + "HitBox CenterX : " + hitBoxCenterXLabel + "\n";

            // Hit Box × Hurt Box（段階11B）: ASCII のみ。Miss の毎フレームログは出さない。
            string boxOverlapLabel = simulationSession.LastBoxOverlap ? "1" : "0";
            string hitCheckLabel = simulationSession.LastHitCheckLabel;
            if (string.IsNullOrEmpty(hitCheckLabel))
            {
                hitCheckLabel = "Inactive";
            }

            text = text + "BoxOverlap     : " + boxOverlapLabel + "\n";
            text = text + "HitCheck       : " + hitCheckLabel + "\n";

            text = text + "Punch Phase    : " + punchPhase + "\n";
            text = text + "AttackResult   : " + attackResult + "\n";
            text = text + "PunchHitDone   : " + punchHitDone + "\n";
            text = text + "Status         : " + statusLabel;
            return text;
        }
    }
}
