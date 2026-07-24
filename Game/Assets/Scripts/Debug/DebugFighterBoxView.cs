using FightingGameTrial.Fighter;
using UnityEngine;

namespace FightingGameTrial.DebugTools
{
    /// <summary>
    /// Push / Hurt / Hit Box の Game ビュー可視化です（段階11A）。
    ///
    /// 何を担当するか:
    /// - Participant が計算した World Box を LineRenderer の矩形枠として描く
    /// - Push / Hurt / Hit を色で区別する
    /// - Hit Box は IsActive のときだけ表示する
    ///
    /// なぜ専用 View に分離するか:
    /// 本番の Sprite 描画（Visual）とデバッグ枠を混ぜると責務が追いにくいため。
    /// 判定ロジックや Push Resolver はここから呼ばない（描画のみ）。
    ///
    /// なぜ Gizmos だけにしないか:
    /// Play 中の Game ビューでも常時確認できるようにするため。
    ///
    /// 自動構築:
    /// Participant.Awake から AddComponent される想定。Scene への手動配線は不要。
    /// </summary>
    public class DebugFighterBoxView : MonoBehaviour
    {
        /// <summary>
        /// 全 Participant 共通の一括表示スイッチです。
        /// false なら各 View は枠を消し、描画更新を止めます。
        /// </summary>
        public static bool GlobalDrawEnabled = true;

        [Header("参照")]
        [Tooltip("描画元の Participant です。未設定なら同じ GameObject から取得します。")]
        [SerializeField]
        private DebugFighterParticipant participant;

        [Header("表示スイッチ")]
        [Tooltip("この参加者の Box 枠を描くか。GlobalDrawEnabled も true のときだけ描画します。")]
        [SerializeField]
        private bool drawEnabled = true;

        [Header("色（Push / Hurt / Hit を見分ける）")]
        [SerializeField]
        private Color pushBoxColor = new Color(0.2f, 0.75f, 1f, 1f);

        [SerializeField]
        private Color hurtBoxColor = new Color(0.25f, 0.95f, 0.35f, 1f);

        [SerializeField]
        private Color hitBoxColor = new Color(1f, 0.25f, 0.25f, 1f);

        [Header("線")]
        [Tooltip("枠線の太さ（ワールド単位）。")]
        [SerializeField]
        private float lineWidth = 0.03f;

        [Tooltip("Sprite より手前に出すための sortingOrder です。")]
        [SerializeField]
        private int sortingOrder = 50;

        private LineRenderer pushLine;
        private LineRenderer hurtLine;
        private LineRenderer hitLine;
        private Material lineMaterial;

        /// <summary>
        /// HUD 用: この View が今描画対象か。
        /// </summary>
        public bool IsDrawEnabled
        {
            get { return drawEnabled && GlobalDrawEnabled; }
        }

        /// <summary>
        /// Participant から呼ばれ、参照を確定します。
        /// </summary>
        public void Bind(DebugFighterParticipant owner)
        {
            participant = owner;
            EnsureLineRenderers();
            RefreshNow();
        }

        private void Awake()
        {
            if (participant == null)
            {
                participant = GetComponent<DebugFighterParticipant>();
            }

            EnsureLineRenderers();
        }

        private void LateUpdate()
        {
            // SimulationTick と独立して「いまの状態」を読む。
            // Pause / HitStop 中は状態が変わらないので枠も固定されて見える。
            RefreshNow();
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
                lineMaterial = null;
            }
        }

        /// <summary>
        /// Participant の World Box を読み、3本の LineRenderer を更新します。
        /// </summary>
        public void RefreshNow()
        {
            if (participant == null)
            {
                SetLineVisible(pushLine, false);
                SetLineVisible(hurtLine, false);
                SetLineVisible(hitLine, false);
                return;
            }

            EnsureLineRenderers();

            if (IsDrawEnabled == false)
            {
                SetLineVisible(pushLine, false);
                SetLineVisible(hurtLine, false);
                SetLineVisible(hitLine, false);
                return;
            }

            DebugBox2D pushBox = participant.EvaluateWorldPushBox();
            DebugBox2D hurtBox = participant.EvaluateWorldHurtBox();
            DebugBox2D hitBox = participant.EvaluateWorldHitBox();

            ApplyBoxToLine(pushLine, pushBox, pushBoxColor);
            ApplyBoxToLine(hurtLine, hurtBox, hurtBoxColor);
            ApplyBoxToLine(hitLine, hitBox, hitBoxColor);
        }

        private void EnsureLineRenderers()
        {
            if (lineMaterial == null)
            {
                lineMaterial = CreateLineMaterial();
            }

            if (pushLine == null)
            {
                pushLine = CreateLineChild("DebugPushBoxLine", pushBoxColor);
            }

            if (hurtLine == null)
            {
                hurtLine = CreateLineChild("DebugHurtBoxLine", hurtBoxColor);
            }

            if (hitLine == null)
            {
                hitLine = CreateLineChild("DebugHitBoxLine", hitBoxColor);
            }
        }

        private LineRenderer CreateLineChild(string childName, Color color)
        {
            Transform existing = transform.Find(childName);
            GameObject childObject;
            if (existing != null)
            {
                childObject = existing.gameObject;
            }
            else
            {
                childObject = new GameObject(childName);
                childObject.transform.SetParent(transform, false);
                childObject.transform.localPosition = Vector3.zero;
                childObject.transform.localRotation = Quaternion.identity;
                childObject.transform.localScale = Vector3.one;
            }

            LineRenderer line = childObject.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = childObject.AddComponent<LineRenderer>();
            }

            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 5;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.allowOcclusionWhenDynamic = false;
            line.sortingOrder = sortingOrder;
            line.startColor = color;
            line.endColor = color;
            line.enabled = false;
            return line;
        }

        private Material CreateLineMaterial()
        {
            // URP 2D でも使えるシンプルなスプライト用シェーダを優先する。
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                Debug.LogError("DebugFighterBoxView: 線用 Shader が見つかりません。");
                return new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            Material material = new Material(shader);
            material.name = "DebugFighterBoxLineMaterial";
            return material;
        }

        private void ApplyBoxToLine(LineRenderer line, DebugBox2D box, Color color)
        {
            if (line == null)
            {
                return;
            }

            if (box == null || box.IsActive == false)
            {
                SetLineVisible(line, false);
                return;
            }

            Vector3 bottomLeft;
            Vector3 bottomRight;
            Vector3 topRight;
            Vector3 topLeft;
            box.GetCorners(out bottomLeft, out bottomRight, out topRight, out topLeft);

            // Z は Participant と同じ面に揃える（カメラが XY を見る前提）。
            float z = transform.position.z;
            bottomLeft.z = z;
            bottomRight.z = z;
            topRight.z = z;
            topLeft.z = z;

            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.startColor = color;
            line.endColor = color;
            line.SetPosition(0, bottomLeft);
            line.SetPosition(1, bottomRight);
            line.SetPosition(2, topRight);
            line.SetPosition(3, topLeft);
            line.SetPosition(4, bottomLeft);
            SetLineVisible(line, true);
        }

        private void SetLineVisible(LineRenderer line, bool visible)
        {
            if (line == null)
            {
                return;
            }

            line.enabled = visible;
        }
    }
}
