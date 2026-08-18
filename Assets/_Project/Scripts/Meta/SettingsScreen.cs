using Game.Core;
using Game.Core.UI;
using Game.Services.Audio;
using Game.Services.Localization;
using Game.Services.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta
{
    /// <summary>
    /// Màn Settings tối thiểu — plan.md Tuần 11 "Màn hình Title + Settings tối thiểu".
    /// Dựng bằng UI.Text/Slider (không TMP) vì Game.Meta.asmdef không tham chiếu TMP —
    /// tránh phụ thuộc vòng ngược Game.UI (object-map.md §6).
    /// Đổi giá trị ghi thẳng qua ISettingsService — AudioService/CameraFx tự nghe OnChanged.
    /// task-localization-pilot.md — toàn bộ label màn này + nút Language (cycle VI↔EN) là 1 trong
    /// 2 màn pilot localization (cùng Title screen).
    /// </summary>
    public sealed class SettingsScreen : MonoBehaviour
    {
        private static readonly Color PANEL_BG = new(0.169f, 0.106f, 0.180f, 0.97f);
        private static readonly Color TEXT = new(0.949f, 0.910f, 0.810f);
        private static readonly Color ACCENT = new(0.957f, 0.635f, 0.349f);

        private ISettingsService _settings;
        private IAudioService _audio;
        private ILocalizationService _loc;
        private GameObject _root;

        private static readonly float[] TEXT_SCALE_STEPS = { 1f, 1.25f, 1.5f };

        private Text _bgmValueLabel, _sfxValueLabel;
        private Text _titleLabel, _musicLabel, _sfxLabel, _shakeLabel, _acLabel, _langLabel, _langValueLabel, _closeLabel;
        private Text _textScaleLabel, _textScaleValueLabel, _colorblindLabel, _largeDamageLabel;

        public void Open()
        {
            ServiceLocator.TryGet(out _settings);
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _loc);

            if (_root == null) Build();
            _root.SetActive(true);
            RefreshValues();
            RefreshLabels();
        }

        public void Close() => _root?.SetActive(false);

        public bool IsOpen => _root != null && _root.activeSelf;

        // =====================================================================

        private void Build()
        {
            var canvasGo = new GameObject("SettingsCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _root = canvasGo;

            var dim = NewImage(canvasGo.transform, new Color(0, 0, 0, 0.6f));
            var dimRt = (RectTransform)dim.transform;
            dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;

            var panel = NewImage(canvasGo.transform, PANEL_BG);
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(360, 560); // +60 — task-accessibility-part2.md, thêm hàng Large Damage Numbers

            // task-phase-5-gaps.md Phần E — pilot LayoutProfileSwitcher: Portrait = chụp lại đúng
            // số liệu vừa gán ở trên (không đổi hành vi màn hình dọc hiện có); Landscape = rộng hơn/
            // thấp hơn (màn hình ngang có nhiều bề rộng, ít chiều cao hơn) — số liệu tự thiết kế cho
            // pilot, KHÔNG phải bản thiết kế landscape cuối cùng (§E1 "ngoài phạm vi: áp toàn bộ
            // 23 màn").
            var portraitProfile = LayoutProfile.CaptureFrom(panelRt, "SettingsPanel_Portrait");
            var landscapeProfile = portraitProfile;
            landscapeProfile.Name = "SettingsPanel_Landscape";
            landscapeProfile.SizeDelta = new Vector2(420, 520); // +60, cùng lý do Portrait ở trên
            var layoutSwitcher = panel.gameObject.AddComponent<LayoutProfileSwitcher>();
            layoutSwitcher.SetProfiles(panelRt, portraitProfile, landscapeProfile);

            _titleLabel = Label(panelRt, "SETTINGS", 22, TextAnchor.MiddleCenter,
                 new Vector2(0, 155), new Vector2(300, 30));
            _titleLabel.color = ACCENT;

            _bgmValueLabel = Label(panelRt, "70%", 14, TextAnchor.MiddleRight,
                                   new Vector2(150, 100), new Vector2(60, 24));
            _musicLabel = Label(panelRt, "Music", 14, TextAnchor.MiddleLeft, new Vector2(-150, 100), new Vector2(100, 24));
            var bgmSlider = NewSlider(panelRt, new Vector2(0, 75));
            bgmSlider.onValueChanged.AddListener(v =>
            {
                _settings?.Modify(s => s.Bgm = v);
                if (_bgmValueLabel != null) _bgmValueLabel.text = $"{v * 100:0}%";
            });

            _sfxValueLabel = Label(panelRt, "90%", 14, TextAnchor.MiddleRight,
                                   new Vector2(150, 40), new Vector2(60, 24));
            _sfxLabel = Label(panelRt, "SFX", 14, TextAnchor.MiddleLeft, new Vector2(-150, 40), new Vector2(100, 24));
            var sfxSlider = NewSlider(panelRt, new Vector2(0, 15));
            sfxSlider.onValueChanged.AddListener(v =>
            {
                _settings?.Modify(s => s.Sfx = v);
                if (_sfxValueLabel != null) _sfxValueLabel.text = $"{v * 100:0}%";
            });

            Toggle shakeToggle; Toggle acToggle;
            (shakeToggle, _shakeLabel) = NewToggleWithLabel(panelRt, new Vector2(0, -25), "Screen Shake");
            shakeToggle.onValueChanged.AddListener(v => _settings?.Modify(s => s.ScreenShake = v));

            (acToggle, _acLabel) = NewToggleWithLabel(panelRt, new Vector2(0, -60), "Action Command");
            acToggle.onValueChanged.AddListener(v => _settings?.Modify(s => s.ActionCommandEnabled = v));

            // Nút Language — cycle vi↔en, cùng mẫu nút Speed trong BattleHudScreen (hiện giá trị
            // hiện tại, bấm đổi). Giá trị hiện raw code "VI"/"EN", KHÔNG dịch (giống AutoSpeed
            // hiện "x1" thô, không phải chuỗi cần dịch).
            _langLabel = Label(panelRt, "Language", 14, TextAnchor.MiddleLeft, new Vector2(-150, -100), new Vector2(140, 24));
            _langValueLabel = Label(panelRt, "VI", 14, TextAnchor.MiddleCenter, new Vector2(130, -100), new Vector2(60, 24));
            _langValueLabel.color = ACCENT;
            var langBtn = _langValueLabel.gameObject.AddComponent<Button>();
            langBtn.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                string next = _settings?.Current.Language == "vi" ? "en" : "vi";
                _settings?.Modify(s => s.Language = next);
                RefreshLabels();
            });
            _langValueLabel.raycastTarget = true;

            // task-accessibility.md — plan.md §10.7. TextScale/ColorblindMode đã tồn tại sẵn trong
            // SettingsDto từ đầu dự án (đúng mẫu "hạ tầng có sẵn, chưa dùng") — nay mới có UI thật.
            _textScaleLabel = Label(panelRt, "Text Size", 14, TextAnchor.MiddleLeft, new Vector2(-150, -135), new Vector2(140, 24));
            _textScaleValueLabel = Label(panelRt, "100%", 14, TextAnchor.MiddleCenter, new Vector2(130, -135), new Vector2(60, 24));
            _textScaleValueLabel.color = ACCENT;
            var textScaleBtn = _textScaleValueLabel.gameObject.AddComponent<Button>();
            textScaleBtn.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                float current = _settings?.Current.TextScale ?? 1f;
                int idx = System.Array.IndexOf(TEXT_SCALE_STEPS, current);
                float next = TEXT_SCALE_STEPS[(idx + 1 + TEXT_SCALE_STEPS.Length) % TEXT_SCALE_STEPS.Length];
                _settings?.Modify(s => s.TextScale = next);
                _textScaleValueLabel.text = $"{next * 100:0}%";
            });
            _textScaleValueLabel.raycastTarget = true;

            Toggle colorblindToggle;
            (colorblindToggle, _colorblindLabel) = NewToggleWithLabel(panelRt, new Vector2(0, -170), "Colorblind Mode");
            colorblindToggle.onValueChanged.AddListener(v => _settings?.Modify(s => s.ColorblindMode = v));

            // task-accessibility-part2.md — plan.md §10.7 "hiển thị số damage lớn", nốt cuối của
            // 6 mục §10.7 (3/6 đã xong ở task-accessibility.md, 2/6 khác là gamepad hotkey/icon
            // hình dạng — không có UI Settings riêng cho 2 mục đó, xem task file).
            Toggle largeDamageToggle;
            (largeDamageToggle, _largeDamageLabel) = NewToggleWithLabel(panelRt, new Vector2(0, -205), "Large Damage Numbers");
            largeDamageToggle.onValueChanged.AddListener(v => _settings?.Modify(s => s.ShowLargeDamageNumbers = v));

            var closeGo = new GameObject("Close", typeof(RectTransform));
            closeGo.transform.SetParent(panelRt, false);
            var closeRt = (RectTransform)closeGo.transform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0.5f);
            closeRt.anchoredPosition = new Vector2(0, -245);
            closeRt.sizeDelta = new Vector2(120, 32);
            var closeImg = closeGo.AddComponent<Image>();
            closeImg.color = new Color(0.42f, 0.32f, 0.06f, 0.95f);
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_cancel");
                Close();
            });
            _closeLabel = Label(closeRt, "CLOSE", 14, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(120, 32));

            // Gán slider/toggle giá trị hiện tại SAU khi build xong để không kích onValueChanged sớm
            if (_settings != null)
            {
                bgmSlider.SetValueWithoutNotify(_settings.Current.Bgm);
                sfxSlider.SetValueWithoutNotify(_settings.Current.Sfx);
                shakeToggle.SetIsOnWithoutNotify(_settings.Current.ScreenShake);
                acToggle.SetIsOnWithoutNotify(_settings.Current.ActionCommandEnabled);
                colorblindToggle.SetIsOnWithoutNotify(_settings.Current.ColorblindMode);
                largeDamageToggle.SetIsOnWithoutNotify(_settings.Current.ShowLargeDamageNumbers);
                _textScaleValueLabel.text = $"{_settings.Current.TextScale * 100:0}%";
            }

            // Màn đang mở khi đổi ngôn ngữ (chỉ có thể đổi từ chính màn này) — tự làm mới label
            // ngay, không cần đợi mở lại. Chỉ subscribe 1 lần lúc Build() (đúng mẫu mọi listener
            // khác trong file này), không unsubscribe — SettingsScreen sống suốt đời MetaSceneInstaller.
            if (_loc != null) _loc.OnLanguageChanged += RefreshLabels;
        }

        private void RefreshValues()
        {
            if (_settings == null) return;
            if (_bgmValueLabel != null) _bgmValueLabel.text = $"{_settings.Current.Bgm * 100:0}%";
            if (_sfxValueLabel != null) _sfxValueLabel.text = $"{_settings.Current.Sfx * 100:0}%";
        }

        /// <summary>task-localization-pilot.md — làm mới toàn bộ label tĩnh theo ngôn ngữ hiện tại.</summary>
        private void RefreshLabels()
        {
            if (_loc == null) return;
            _titleLabel.text = _loc.Get("settings.label.title");
            _musicLabel.text = _loc.Get("settings.label.music");
            _sfxLabel.text = _loc.Get("settings.label.sfx");
            _shakeLabel.text = _loc.Get("settings.label.screen_shake");
            _acLabel.text = _loc.Get("settings.label.action_command");
            _langLabel.text = _loc.Get("settings.label.language");
            _langValueLabel.text = _loc.CurrentLanguage.ToUpperInvariant();
            _textScaleLabel.text = _loc.Get("settings.label.text_scale");
            _colorblindLabel.text = _loc.Get("settings.label.colorblind");
            _largeDamageLabel.text = _loc.Get("settings.label.large_damage_numbers");
            _closeLabel.text = _loc.Get("settings.button.close");
        }

        // ---------- Helper dựng UI ----------

        private static Image NewImage(Transform parent, Color color)
        {
            var go = new GameObject("Image", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text Label(Transform parent, string text, int size, TextAnchor align,
                                  Vector2 pos, Vector2 dim)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = dim;

            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = TEXT;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.raycastTarget = false;
            return t;
        }

        private static Slider NewSlider(Transform parent, Vector2 pos)
        {
            var go = new GameObject("Slider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280, 16);

            var bg = NewImage(rt, new Color(0.1f, 0.07f, 0.11f, 0.95f));
            var bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(rt, false);
            var fillAreaRt = (RectTransform)fillArea.transform;
            fillAreaRt.anchorMin = Vector2.zero; fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = new Vector2(2, 2); fillAreaRt.offsetMax = new Vector2(-2, -2);

            var fill = NewImage(fillAreaRt, ACCENT);
            var fillRt = (RectTransform)fill.transform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = new Vector2(1, 1);
            fillRt.sizeDelta = Vector2.zero;

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(rt, false);
            var handleAreaRt = (RectTransform)handleArea.transform;
            handleAreaRt.anchorMin = Vector2.zero; handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = handleAreaRt.offsetMax = Vector2.zero;

            var handle = NewImage(handleAreaRt, Color.white);
            var handleRt = (RectTransform)handle.transform;
            handleRt.sizeDelta = new Vector2(12, 20);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f;
            return slider;
        }

        /// <summary>task-localization-pilot.md — trả kèm `Text` của label (khác `NewToggle` gốc
        /// chỉ build label nội bộ không giữ tham chiếu) để `RefreshLabels()` đổi được lúc runtime.</summary>
        private static (Toggle toggle, Text label) NewToggleWithLabel(Transform parent, Vector2 pos, string label)
        {
            var go = new GameObject("Toggle", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280, 24);

            var bgGo = new GameObject("Box", typeof(RectTransform));
            bgGo.transform.SetParent(rt, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0, 0.5f);
            bgRt.anchoredPosition = new Vector2(10, 0);
            bgRt.sizeDelta = new Vector2(20, 20);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.07f, 0.11f, 0.95f);

            var checkGo = new GameObject("Check", typeof(RectTransform));
            checkGo.transform.SetParent(bgRt, false);
            var checkRt = (RectTransform)checkGo.transform;
            checkRt.anchorMin = Vector2.zero; checkRt.anchorMax = Vector2.one;
            checkRt.offsetMin = new Vector2(3, 3); checkRt.offsetMax = new Vector2(-3, -3);
            var checkImg = checkGo.AddComponent<Image>();
            checkImg.color = ACCENT;

            var labelText = Label(rt, label, 14, TextAnchor.MiddleLeft, new Vector2(45, 0), new Vector2(220, 24));

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = true;
            return (toggle, labelText);
        }
    }
}
