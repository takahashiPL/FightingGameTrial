using System.Collections.Generic;
using FightingGameTrial.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FightingGameTrial.DebugTools
{
    /// <summary>
    /// Build 用の最小 P2 TEST MODE UI（画面右上）です。
    ///
    /// 責務:
    /// - Dropdown で実モードを SimulationSession.ApplyP2TestMode へ渡す
    /// - Dropdown 末尾の RESET は一時コマンド（enum ではない）。Training Reset 後に表示を元モードへ戻す
    /// - 必要なモードだけ Delay Slider を表示し、Session の公開 Delay API 経由で値を読み書きする
    ///
    /// UI 構築方針:
    /// Unity 6 では旧 Built-in `UI/Skin/*.psd` が無いため、GetBuiltinResource は使わない。
    /// Image は sprite=null・Simple・明示 color だけで矩形を描く。
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class DebugP2TestModeView : MonoBehaviour
    {
        private const float PanelWidth = 280f;
        private const float MarginPixels = 28f;
        private const float TitleHeight = 28f;
        private const float RowHeight = 32f;
        private const float DelayLabelHeight = 22f;
        private const float SliderHeight = 24f;
        private const float ItemHeight = 28f;
        private const int ModeOptionCount = 6;
        private const int ResetDropdownIndex = 6;
        private const int DropdownOptionCount = 7;

        private static readonly Color PanelColor = new Color(0.1f, 0.11f, 0.13f, 0.92f);
        private static readonly Color SurfaceColor = new Color(0.18f, 0.2f, 0.24f, 1f);
        private static readonly Color SurfaceHoverColor = new Color(0.3f, 0.36f, 0.46f, 1f);
        private static readonly Color SurfacePressedColor = new Color(0.24f, 0.3f, 0.4f, 1f);
        private static readonly Color TemplateColor = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        private static readonly Color ItemColor = new Color(0.18f, 0.2f, 0.24f, 1f);
        private static readonly Color ItemHoverColor = new Color(0.34f, 0.4f, 0.52f, 1f);
        private static readonly Color SliderFillColor = new Color(0.45f, 0.55f, 0.75f, 1f);
        private static readonly Color TextColor = Color.white;

        [Header("参照（Inspectorで接続。自動検索はしません）")]
        [Tooltip("P2 検証モードと Training Reset の窓口になる SimulationSession です。")]
        [SerializeField]
        private SimulationSession simulationSession;

        [Tooltip("任意。未設定なら同 Canvas 上の DebugHudText から Font を借ります。")]
        [SerializeField]
        private TextMeshProUGUI fontSourceText;

        private TMP_Dropdown modeDropdown;
        private TextMeshProUGUI arrowText;
        private GameObject delayBlockObject;
        private TextMeshProUGUI delayLabel;
        private Slider delaySlider;
        private bool suppressDropdownCallback;
        private bool suppressSliderCallback;

        private void Awake()
        {
            if (simulationSession == null)
            {
                Debug.LogError("DebugP2TestModeView: SimulationSession が未設定です。");
                return;
            }

            EnsurePanelUi();
            WireUiEvents();
            SyncUiFromSession();
        }

        private void EnsurePanelUi()
        {
            Transform existing = transform.Find("P2TestControlPanel");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            CreatePanelHierarchy();

            if (modeDropdown == null || delaySlider == null || delayLabel == null)
            {
                Debug.LogError(
                    "DebugP2TestModeView: Dropdown または Delay UI を構築できませんでした。"
                );
            }
        }

        private void CreatePanelHierarchy()
        {
            TMP_FontAsset font = ResolveFontAsset();

            GameObject panel = new GameObject("P2TestControlPanel", typeof(RectTransform));
            panel.layer = gameObject.layer;
            panel.transform.SetParent(transform, false);

            RectTransform panelRectTransform = panel.GetComponent<RectTransform>();
            panelRectTransform.anchorMin = new Vector2(1f, 1f);
            panelRectTransform.anchorMax = new Vector2(1f, 1f);
            panelRectTransform.pivot = new Vector2(1f, 1f);
            panelRectTransform.anchoredPosition = new Vector2(-MarginPixels, -MarginPixels);
            panelRectTransform.sizeDelta = new Vector2(PanelWidth, 120f);

            Image panelImage = panel.AddComponent<Image>();
            ConfigurePlainImage(panelImage, PanelColor);
            panelImage.raycastTarget = true;

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateTitle(panel.transform, font);
            CreateDropdown(panel.transform, font);
            CreateDelayBlock(panel.transform, font);
        }

        private void CreateTitle(Transform parent, TMP_FontAsset font)
        {
            GameObject titleObject = new GameObject("TitleText", typeof(RectTransform));
            titleObject.layer = gameObject.layer;
            titleObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = titleObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = TitleHeight;
            layoutElement.preferredHeight = TitleHeight;
            layoutElement.flexibleHeight = 0f;

            TextMeshProUGUI title = titleObject.AddComponent<TextMeshProUGUI>();
            ApplyLabelStyle(title, font, 18f);
            title.alignment = TextAlignmentOptions.Center;
            title.verticalAlignment = VerticalAlignmentOptions.Middle;
            title.text = "P2 TEST MODE";
            title.raycastTarget = false;
        }

        private void CreateDropdown(Transform parent, TMP_FontAsset font)
        {
            GameObject dropdownObject = new GameObject(
                "P2TestModeDropdown",
                typeof(RectTransform)
            );
            dropdownObject.layer = gameObject.layer;
            dropdownObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = dropdownObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = RowHeight;
            layoutElement.preferredHeight = RowHeight;

            Image dropdownImage = dropdownObject.AddComponent<Image>();
            ConfigurePlainImage(dropdownImage, SurfaceColor);

            modeDropdown = dropdownObject.AddComponent<TMP_Dropdown>();
            modeDropdown.targetGraphic = dropdownImage;
            ApplySelectableColors(modeDropdown);

            const float captionRightPadding = 36f;
            TextMeshProUGUI caption = CreateTmpLabel(
                dropdownObject.transform,
                "CaptionText",
                font,
                14f,
                new Vector2(8f, 0f),
                new Vector2(-captionRightPadding, 0f)
            );
            caption.alignment = TextAlignmentOptions.Left;
            caption.verticalAlignment = VerticalAlignmentOptions.Middle;
            caption.overflowMode = TextOverflowModes.Ellipsis;

            arrowText = CreateArrowText(dropdownObject.transform, font);
            arrowText.transform.SetAsLastSibling();

            float templateHeight = ItemHeight * DropdownOptionCount + 8f;
            GameObject templateObject = new GameObject("Template", typeof(RectTransform));
            templateObject.layer = gameObject.layer;
            templateObject.transform.SetParent(dropdownObject.transform, false);

            RectTransform templateRect = templateObject.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, templateHeight);

            Image templateImage = templateObject.AddComponent<Image>();
            ConfigurePlainImage(templateImage, TemplateColor);

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform));
            viewportObject.layer = gameObject.layer;
            viewportObject.transform.SetParent(templateObject.transform, false);

            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            StretchFull(viewportRect);
            viewportRect.offsetMin = new Vector2(4f, 4f);
            viewportRect.offsetMax = new Vector2(-4f, -4f);
            viewportObject.AddComponent<RectMask2D>();

            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.layer = gameObject.layer;
            contentObject.transform.SetParent(viewportObject.transform, false);

            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, ItemHeight);

            GameObject itemObject = new GameObject("Item", typeof(RectTransform));
            itemObject.layer = gameObject.layer;
            itemObject.transform.SetParent(contentObject.transform, false);

            RectTransform itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, ItemHeight);

            Image itemBackground = itemObject.AddComponent<Image>();
            ConfigurePlainImage(itemBackground, ItemColor);

            Toggle itemToggle = itemObject.AddComponent<Toggle>();
            itemToggle.targetGraphic = itemBackground;
            itemToggle.isOn = false;
            ColorBlock toggleColors = itemToggle.colors;
            toggleColors.normalColor = ItemColor;
            toggleColors.highlightedColor = ItemHoverColor;
            toggleColors.pressedColor = SurfacePressedColor;
            toggleColors.selectedColor = ItemHoverColor;
            toggleColors.disabledColor = ItemColor;
            toggleColors.colorMultiplier = 1f;
            itemToggle.colors = toggleColors;
            itemToggle.graphic = null;

            TextMeshProUGUI itemLabel = CreateTmpLabel(
                itemObject.transform,
                "Item Label",
                font,
                13f,
                new Vector2(8f, 0f),
                new Vector2(-8f, 0f)
            );
            itemLabel.alignment = TextAlignmentOptions.Left;
            itemLabel.verticalAlignment = VerticalAlignmentOptions.Middle;

            modeDropdown.captionText = caption;
            modeDropdown.itemText = itemLabel;
            modeDropdown.template = templateRect;
            templateObject.SetActive(false);

            modeDropdown.ClearOptions();
            List<string> options = new List<string>(DropdownOptionCount);
            options.Add("NO ACTION");
            options.Add("MIRROR J PUNCH");
            options.Add("MIRROR GROUND KICK");
            options.Add("J PUNCH vs GROUND KICK");
            options.Add("P2 AIR KICK");
            options.Add("STAND GUARD");
            options.Add("RESET");
            modeDropdown.AddOptions(options);
            modeDropdown.value = 0;
            modeDropdown.RefreshShownValue();
            EnsureArrowVisible();
        }

        private void CreateDelayBlock(Transform parent, TMP_FontAsset font)
        {
            delayBlockObject = new GameObject("DelayBlock", typeof(RectTransform));
            delayBlockObject.layer = gameObject.layer;
            delayBlockObject.transform.SetParent(parent, false);

            LayoutElement blockLayout = delayBlockObject.AddComponent<LayoutElement>();
            blockLayout.minHeight = DelayLabelHeight + SliderHeight + 4f;
            blockLayout.preferredHeight = DelayLabelHeight + SliderHeight + 4f;

            VerticalLayoutGroup blockVertical =
                delayBlockObject.AddComponent<VerticalLayoutGroup>();
            blockVertical.padding = new RectOffset(0, 0, 0, 0);
            blockVertical.spacing = 4f;
            blockVertical.childControlHeight = true;
            blockVertical.childControlWidth = true;
            blockVertical.childForceExpandHeight = false;
            blockVertical.childForceExpandWidth = true;

            GameObject labelObject = new GameObject("DelayLabel", typeof(RectTransform));
            labelObject.layer = gameObject.layer;
            labelObject.transform.SetParent(delayBlockObject.transform, false);

            LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
            labelLayout.minHeight = DelayLabelHeight;
            labelLayout.preferredHeight = DelayLabelHeight;

            delayLabel = labelObject.AddComponent<TextMeshProUGUI>();
            ApplyLabelStyle(delayLabel, font, 14f);
            delayLabel.alignment = TextAlignmentOptions.Left;
            delayLabel.verticalAlignment = VerticalAlignmentOptions.Middle;
            delayLabel.text = "DELAY : 0 CF";

            GameObject sliderObject = new GameObject("DelaySlider", typeof(RectTransform));
            sliderObject.layer = gameObject.layer;
            sliderObject.transform.SetParent(delayBlockObject.transform, false);

            LayoutElement sliderLayout = sliderObject.AddComponent<LayoutElement>();
            sliderLayout.minHeight = SliderHeight;
            sliderLayout.preferredHeight = SliderHeight;

            Image sliderBg = sliderObject.AddComponent<Image>();
            ConfigurePlainImage(sliderBg, SurfaceColor);

            delaySlider = sliderObject.AddComponent<Slider>();
            delaySlider.wholeNumbers = true;
            delaySlider.minValue = 0f;
            delaySlider.maxValue = 120f;
            delaySlider.direction = Slider.Direction.LeftToRight;

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.layer = gameObject.layer;
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            StretchFull(fillAreaRect);
            fillAreaRect.offsetMin = new Vector2(6f, 6f);
            fillAreaRect.offsetMax = new Vector2(-6f, -6f);

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform));
            fillObject.layer = gameObject.layer;
            fillObject.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            StretchFull(fillRect);
            Image fillImage = fillObject.AddComponent<Image>();
            ConfigurePlainImage(fillImage, SliderFillColor);
            fillImage.raycastTarget = false;

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.layer = gameObject.layer;
            handleArea.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            StretchFull(handleAreaRect);
            handleAreaRect.offsetMin = new Vector2(8f, 2f);
            handleAreaRect.offsetMax = new Vector2(-8f, -2f);

            GameObject handleObject = new GameObject("Handle", typeof(RectTransform));
            handleObject.layer = gameObject.layer;
            handleObject.transform.SetParent(handleArea.transform, false);
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(14f, 0f);
            Image handleImage = handleObject.AddComponent<Image>();
            ConfigurePlainImage(handleImage, TextColor);

            delaySlider.fillRect = fillRect;
            delaySlider.handleRect = handleRect;
            delaySlider.targetGraphic = handleImage;
        }

        private static TextMeshProUGUI CreateArrowText(Transform parent, TMP_FontAsset font)
        {
            GameObject arrowObject = new GameObject("ArrowText", typeof(RectTransform));
            arrowObject.layer = parent.gameObject.layer;
            arrowObject.transform.SetParent(parent, false);

            RectTransform rect = arrowObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(32f, 0f);
            rect.anchoredPosition = new Vector2(-4f, 0f);

            TextMeshProUGUI arrow = arrowObject.AddComponent<TextMeshProUGUI>();
            ApplyLabelStyle(arrow, font, 14f);
            arrow.alignment = TextAlignmentOptions.Center;
            arrow.verticalAlignment = VerticalAlignmentOptions.Middle;
            arrow.text = "▼";
            arrow.raycastTarget = false;
            arrow.overflowMode = TextOverflowModes.Overflow;
            return arrow;
        }

        private void EnsureArrowVisible()
        {
            if (arrowText == null)
            {
                return;
            }

            arrowText.transform.SetAsLastSibling();
            arrowText.text = "▼";
            arrowText.raycastTarget = false;
            arrowText.enabled = true;
            arrowText.gameObject.SetActive(true);
        }

        private void WireUiEvents()
        {
            if (modeDropdown != null)
            {
                modeDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
                modeDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            }

            if (delaySlider != null)
            {
                delaySlider.onValueChanged.RemoveListener(OnDelaySliderChanged);
                delaySlider.onValueChanged.AddListener(OnDelaySliderChanged);
            }
        }

        private void SyncUiFromSession()
        {
            if (simulationSession == null || modeDropdown == null)
            {
                return;
            }

            int index = (int)simulationSession.ActiveP2TestMode;
            if (index < 0 || index >= ModeOptionCount)
            {
                index = 0;
            }

            suppressDropdownCallback = true;
            modeDropdown.value = index;
            modeDropdown.RefreshShownValue();
            suppressDropdownCallback = false;
            EnsureArrowVisible();
            RefreshDelayUiForMode(simulationSession.ActiveP2TestMode);
        }

        private void OnDropdownValueChanged(int index)
        {
            if (suppressDropdownCallback || simulationSession == null)
            {
                return;
            }

            // RESET は enum ではない一時コマンド。モード／Delay は変えず Training Reset のみ行う。
            if (index == ResetDropdownIndex)
            {
                DebugP2TestMode previousMode = simulationSession.ActiveP2TestMode;
                simulationSession.ResetTrainingFromUI();

                int restoreIndex = (int)previousMode;
                if (restoreIndex < 0 || restoreIndex >= ModeOptionCount)
                {
                    restoreIndex = 0;
                }

                suppressDropdownCallback = true;
                modeDropdown.value = restoreIndex;
                modeDropdown.RefreshShownValue();
                suppressDropdownCallback = false;
                EnsureArrowVisible();
                RefreshDelayUiForMode(previousMode);
                return;
            }

            if (index < 0 || index >= ModeOptionCount)
            {
                index = 0;
            }

            simulationSession.ApplyP2TestMode((DebugP2TestMode)index);
            EnsureArrowVisible();
            RefreshDelayUiForMode((DebugP2TestMode)index);
        }

        private void OnDelaySliderChanged(float value)
        {
            if (suppressSliderCallback || simulationSession == null)
            {
                return;
            }

            int frames = Mathf.RoundToInt(value);
            DebugP2TestMode mode = simulationSession.ActiveP2TestMode;
            if (mode == DebugP2TestMode.MirrorJPunch
                || mode == DebugP2TestMode.MirrorGroundKick)
            {
                simulationSession.SetP2MirrorAttackDelayFrames(frames);
                UpdateDelayLabel(simulationSession.GetP2MirrorAttackDelayFrames());
            }
            else if (mode == DebugP2TestMode.P2AirKick)
            {
                simulationSession.SetP2AirKickDelayFrames(frames);
                UpdateDelayLabel(simulationSession.GetP2AirKickDelayFrames());
            }
        }

        private void RefreshDelayUiForMode(DebugP2TestMode mode)
        {
            if (delayBlockObject == null || delaySlider == null || delayLabel == null)
            {
                return;
            }

            bool showMirrorDelay =
                mode == DebugP2TestMode.MirrorJPunch
                || mode == DebugP2TestMode.MirrorGroundKick;
            bool showAirDelay = mode == DebugP2TestMode.P2AirKick;
            bool show = showMirrorDelay || showAirDelay;

            delayBlockObject.SetActive(show);
            if (show == false)
            {
                return;
            }

            suppressSliderCallback = true;
            if (showMirrorDelay)
            {
                delaySlider.minValue = SimulationSession.P2MirrorAttackDelayMinFrames;
                delaySlider.maxValue = SimulationSession.P2MirrorAttackDelayMaxFrames;
                int frames = simulationSession.GetP2MirrorAttackDelayFrames();
                delaySlider.SetValueWithoutNotify(frames);
                UpdateDelayLabel(frames);
            }
            else
            {
                delaySlider.minValue = SimulationSession.P2AirKickDelayMinFrames;
                delaySlider.maxValue = SimulationSession.P2AirKickDelayMaxFrames;
                int frames = simulationSession.GetP2AirKickDelayFrames();
                delaySlider.SetValueWithoutNotify(frames);
                UpdateDelayLabel(frames);
            }

            suppressSliderCallback = false;
        }

        private void UpdateDelayLabel(int frames)
        {
            if (delayLabel == null)
            {
                return;
            }

            delayLabel.text = "DELAY : " + frames + " CF";
        }

        private TMP_FontAsset ResolveFontAsset()
        {
            if (fontSourceText != null && fontSourceText.font != null)
            {
                return fontSourceText.font;
            }

            Transform hudTextTransform = transform.Find("DebugHudText");
            if (hudTextTransform != null)
            {
                TextMeshProUGUI hud = hudTextTransform.GetComponent<TextMeshProUGUI>();
                if (hud != null && hud.font != null)
                {
                    return hud.font;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static TextMeshProUGUI CreateTmpLabel(
            Transform parent,
            string objectName,
            TMP_FontAsset font,
            float fontSize,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
            labelObject.layer = parent.gameObject.layer;
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            StretchFull(rect);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            ApplyLabelStyle(label, font, fontSize);
            return label;
        }

        private static void ApplyLabelStyle(
            TextMeshProUGUI label,
            TMP_FontAsset font,
            float fontSize)
        {
            if (label == null)
            {
                return;
            }

            if (font != null)
            {
                label.font = font;
            }

            label.fontSize = fontSize;
            label.color = TextColor;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
        }

        private static void ConfigurePlainImage(Image image, Color color)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = true;
        }

        private static void ApplySelectableColors(Selectable selectable)
        {
            ColorBlock colors = selectable.colors;
            colors.normalColor = SurfaceColor;
            colors.highlightedColor = SurfaceHoverColor;
            colors.pressedColor = SurfacePressedColor;
            colors.selectedColor = SurfaceHoverColor;
            colors.disabledColor = new Color(0.15f, 0.15f, 0.17f, 0.7f);
            colors.colorMultiplier = 1f;
            selectable.colors = colors;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
