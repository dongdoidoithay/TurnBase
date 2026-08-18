using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Combat.Model;
using Game.Combat.Systems;
using Game.Core;
using Game.Core.UI;
using Game.Data;
using Game.Services.Audio;
using Game.Services.Localization;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Battle
{
    /// <summary>
    /// HUD trận đấu — bố cục theo image_UI.jpg (plan.md §10.2): panel bo góc + viền màu
    /// riêng từng khối, portrait tròn, HP bar đổi màu theo %, mỗi địch một dòng có thanh máu
    /// riêng (không còn dump text). Dựng bằng code để chạy được ngay ở P1; P6 thay bằng
    /// prefab + LayoutProfileSwitcher.
    ///
    /// QUY TẮC: HUD chỉ ĐỌC BattleState và phát sự kiện lên trên.
    /// Không bao giờ gọi thẳng vào CombatSimulation (object-map.md §3.3).
    /// </summary>
    public sealed class BattleHudScreen : MonoBehaviour
    {
        private const int GRID_COLS = 5;
        // task-consumable-items.md — hàng 2 (ITEM) nay đã có hệ thống đứng sau.
        // task-tactic-row.md — hàng 3 (TACTIC): Guard / ESC / SWAP / FOCUS.
        private const int GRID_ROWS = 3;
        private const int MAX_ENEMY_ROWS = 5;
        private const int MAX_METER_ROWS = 5;

        private static readonly Color PANEL_BG = new(0.114f, 0.078f, 0.129f, 0.94f);
        private static readonly Color PANEL_BORDER = new(0.957f, 0.635f, 0.349f);
        private static readonly Color HERO_ACCENT = new(0.482f, 0.788f, 0.314f);
        private static readonly Color ENEMY_ACCENT = new(0.788f, 0.365f, 0.788f);
        private static readonly Color GRID_ACCENT = new(1f, 0.718f, 0.012f);
        private static readonly Color TEXT = new(0.949f, 0.910f, 0.810f);
        private static readonly Color TEXT_DIM = new(0.72f, 0.66f, 0.70f);
        private static readonly Color SP_COLOR = new(0.271f, 0.482f, 0.616f);
        private static readonly Color POISE_COLOR = new(1f, 0.820f, 0.400f);
        private static readonly Color ULT_COLOR = new(1f, 0.718f, 0.012f);
        private static readonly Color DEAD_COLOR = new(0.36f, 0.33f, 0.38f);

        /// <summary>(skillSlot) — người chơi chọn skill; targeting xử lý ở tầng trên.</summary>
        public event Action<int> OnSkillChosen;
        /// <summary>task-consumable-items.md — người chơi chọn dùng vật phẩm; auto-target xử lý
        /// ở Combat layer (ItemResolver), không có targeting UI riêng.</summary>
        public event Action<ItemType> OnItemChosen;
        public event Action OnEndTurnPressed;
        public event Action<bool> OnAutoToggled;
        public event Action<int> OnSpeedChanged;
        // task-tactic-row.md — hàng 3 TACTIC
        public event Action OnGuardPressed;
        public event Action OnEscapePressed;
        public event Action OnSwapRowPressed;
        public event Action OnFocusPressed;
        // task-analyze-tactic.md
        public event Action OnAnalyzePressed;

        private Canvas _canvas;
        private readonly List<SkillSlotView> _slots = new(GRID_COLS);
        private readonly List<ItemSlotView> _itemSlots = new(GRID_COLS);
        // task-tactic-row.md — nút hàng 3
        private Button _guardBtn, _escBtn, _swapBtn, _focusBtn;
        // task-analyze-tactic.md — nút ANALYZE (col 4) + panel hiển thị stat địch
        private Button _analyzeBtn;
        private RectTransform _analyzePanel;
        private TextMeshProUGUI _analyzeName, _analyzeHpSp, _analyzeStats, _analyzeElem;

        private TextMeshProUGUI _heroName, _heroStats, _zoneLabel, _roundLabel, _heroInitial;
        private Image _hpFill, _spFill, _ultFill, _heroAvatarRing, _heroPortrait;
        private static readonly Dictionary<string, Sprite> _portraitCache = new();
        private string _lastPortraitDefId;
        private TextMeshProUGUI _turnOrderLabel;
        private Button _endTurnButton;
        private TextMeshProUGUI _autoLabel, _speedLabel;

        private readonly struct EnemyRow
        {
            public readonly TextMeshProUGUI Name;
            public readonly Image HpFill;
            public readonly TextMeshProUGUI HpText;
            public readonly TextMeshProUGUI StatusText;
            public readonly GameObject Root;

            public EnemyRow(GameObject root, TextMeshProUGUI name, Image hpFill,
                             TextMeshProUGUI hpText, TextMeshProUGUI statusText)
            {
                Root = root; Name = name; HpFill = hpFill; HpText = hpText; StatusText = statusText;
            }
        }

        private readonly List<EnemyRow> _enemyRows = new(MAX_ENEMY_ROWS);
        private RectTransform _enemyRowsRoot;

        /// <summary>task-damage-meter.md — 1 dòng "tên · tổng damage" trong panel Damage Meter.</summary>
        private readonly struct MeterRow
        {
            public readonly GameObject Root;
            public readonly TextMeshProUGUI Name;
            public readonly TextMeshProUGUI Value;
            public MeterRow(GameObject root, TextMeshProUGUI name, TextMeshProUGUI value)
            { Root = root; Name = name; Value = value; }
        }

        private readonly List<MeterRow> _meterRows = new(MAX_METER_ROWS);
        private readonly List<KeyValuePair<int, long>> _meterBuffer = new(MAX_METER_ROWS + 4);

        private CombatSimulation _sim;
        private IAudioService _audio;
        private ILocalizationService _loc;
        private Game.Services.Settings.ISettingsService _settings;
        private int _selectedSlot = -1;
        private int _selectedItemSlot = -1;
        private bool _auto;
        private int _speed = 1;

        // =====================================================================

        public void Bind(CombatSimulation sim)
        {
            _sim = sim;
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _loc);
            ServiceLocator.TryGet(out _settings);
            BuildLayout();
            BuildEnemyRows();

            // task-accessibility.md — scene Battle dựng lại MỖI trận (khác Meta chỉ dựng 1 lần lúc
            // đầu) nên áp TextScale ở đây thay vì chỉ dựa vào WireSettingsToTextScale phản ứng
            // (ServiceInstaller) — đảm bảo HUD LUÔN đúng scale ngay từ trận đầu tiên, không cần đợi
            // người chơi vào Settings đổi lại 1 lần sau khi đã vào trận.
            if (_settings != null)
                Game.Meta.Accessibility.TextScaleApplier.Apply(transform, _settings.Current.TextScale);
        }

        private void BuildLayout()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 100;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                // Landscape ref theo plan.md §10.4; P6 sẽ đổi theo hướng màn hình
                scaler.referenceResolution = new Vector2(960, 540);
                scaler.matchWidthOrHeight = 0.5f;
                gameObject.AddComponent<GraphicRaycaster>();
            }

            BuildHeroPanel();
            BuildEnemyPanel();
            BuildDamageMeterPanel();
            BuildAnalyzePanel();
            BuildTurnOrderBar();
            BuildSkillGrid();
            BuildEndTurn();
            BuildAutoSpeed();
        }

        // ---------- Hero panel (trái) — portrait tròn + tên + 3 thanh chỉ số ----------

        private void BuildHeroPanel()
        {
            var panel = Panel("HeroPanel", new Vector2(0, 1), new Vector2(0, 1),
                              new Vector2(12, -12), new Vector2(248, 158), HERO_ACCENT);

            // task-phase-5-gaps.md Phần E — pilot LayoutProfileSwitcher trên HUD trận: HeroPanel
            // neo góc trên-trái độc lập với mọi panel khác (không cascade), an toàn để thử co giãn.
            // Landscape = chụp lại đúng số liệu vừa gán ở trên (canvas 960×540 vốn thiết kế cho
            // landscape — không đổi hành vi hiện có); Portrait = thu hẹp width (màn dọc thật ít bề
            // ngang hơn hẳn khung tham chiếu 960 khi CanvasScaler match=0.5) — số liệu tự thiết kế
            // cho pilot, không phải bản responsive Battle HUD cuối cùng.
            var heroLandscape = LayoutProfile.CaptureFrom(panel, "HeroPanel_Landscape");
            var heroPortrait = heroLandscape;
            heroPortrait.Name = "HeroPanel_Portrait";
            heroPortrait.SizeDelta = new Vector2(200, 158);
            panel.gameObject.AddComponent<LayoutProfileSwitcher>()
                 .SetProfiles(panel, heroPortrait, heroLandscape);

            const float avatarSize = 52f;
            BuildAvatar(panel, new Vector2(10, -10), avatarSize, HERO_ACCENT,
                       out _heroAvatarRing, out _heroInitial, out _heroPortrait);

            float textLeft = 10 + avatarSize + 8;
            float textWidth = 248 - textLeft - 10;

            _heroName = Label(panel, "NAME", 16, TextAlignmentOptions.TopLeft,
                              new Vector2(textLeft, -10), new Vector2(textWidth, 22));
            // Tên hero + "Lv10" ghép 1 dòng dễ dài hơn bề rộng cột (đã bị avatar tròn ăn bớt) —
            // không co chữ thì nó tràn xuống đè lên thanh HP bên dưới. Co chữ tự động thay vì wrap.
            _heroName.enableAutoSizing = true;
            _heroName.fontSizeMin = 9;
            _heroName.fontSizeMax = 16;
            _heroName.enableWordWrapping = false;
            _heroName.overflowMode = TextOverflowModes.Ellipsis;

            BarLabel(panel, "HP", new Vector2(textLeft, -34), textWidth);
            _hpFill = Bar(panel, new Vector2(textLeft, -34), new Vector2(textWidth, 13), HERO_ACCENT);

            BarLabel(panel, "SP", new Vector2(textLeft, -52), textWidth);
            _spFill = Bar(panel, new Vector2(textLeft, -52), new Vector2(textWidth, 13), SP_COLOR);

            _heroStats = Label(panel, "", 12, TextAlignmentOptions.TopLeft,
                               new Vector2(10, -68), new Vector2(228, 46));

            BarLabel(panel, "ULT", new Vector2(10, -118));
            _ultFill = Bar(panel, new Vector2(40, -118), new Vector2(198, 9), ULT_COLOR);

            _zoneLabel = Label(panel, "MEADOW [1/3]", 12, TextAlignmentOptions.BottomLeft,
                               new Vector2(10, -138), new Vector2(228, 18));
            _zoneLabel.color = GRID_ACCENT;
        }

        // ---------- Enemy panel (phải) — mỗi địch 1 dòng có thanh máu riêng ----------

        private void BuildEnemyPanel()
        {
            var panel = Panel("EnemyPanel", new Vector2(1, 1), new Vector2(1, 1),
                              new Vector2(-12, -12), new Vector2(226, 158), ENEMY_ACCENT);

            // task-ui-vfx-polish.md §7 — cùng kỹ thuật pilot đã có ở HeroPanel: Landscape = chụp
            // đúng số liệu hiện có (canvas 960×540 vốn thiết kế cho landscape), Portrait = thu hẹp
            // width ~19% (khớp tỉ lệ HeroPanel 248→200 đã chọn trước) — neo góc phải nên chỉ kéo
            // cạnh trái vào gần tâm hơn, không bao giờ tạo chồng lấn mới (chỉ giảm diện tích chiếm).
            var enemyLandscape = LayoutProfile.CaptureFrom(panel, "EnemyPanel_Landscape");
            var enemyPortrait = enemyLandscape;
            enemyPortrait.Name = "EnemyPanel_Portrait";
            enemyPortrait.SizeDelta = new Vector2(182, 158);
            panel.gameObject.AddComponent<LayoutProfileSwitcher>()
                 .SetProfiles(panel, enemyPortrait, enemyLandscape);

            var title = Label(panel, "ENEMIES", 12, TextAlignmentOptions.TopLeft,
                              new Vector2(10, -8), new Vector2(140, 16));
            title.color = ENEMY_ACCENT;
            title.fontStyle = FontStyles.Bold;

            _enemyRowsRoot = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
            _enemyRowsRoot.SetParent(panel, false);
            _enemyRowsRoot.anchorMin = new Vector2(0, 1);
            _enemyRowsRoot.anchorMax = new Vector2(1, 1);
            _enemyRowsRoot.pivot = new Vector2(0.5f, 1);
            _enemyRowsRoot.anchoredPosition = new Vector2(0, -26);
            _enemyRowsRoot.sizeDelta = new Vector2(-16, 128);
        }

        private void BuildEnemyRows()
        {
            if (_sim == null || _enemyRowsRoot == null) return;

            int count = 0;
            for (int i = 0; i < _sim.State.Units.Count && count < MAX_ENEMY_ROWS; i++)
                if (_sim.State.Units[i].Side == TeamSide.Enemy) count++;

            const float rowH = 25f;
            for (int i = 0; i < count; i++)
            {
                var row = new GameObject($"EnemyRow_{i}", typeof(RectTransform));
                row.transform.SetParent(_enemyRowsRoot, false);
                var rt = (RectTransform)row.transform;
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.anchoredPosition = new Vector2(0, -i * rowH);
                rt.sizeDelta = new Vector2(0, rowH - 3);

                var name = Label(rt, "", 11, TextAlignmentOptions.TopLeft,
                                 new Vector2(0, 0), new Vector2(200, 13));

                var hpFill = Bar(rt, new Vector2(0, -13), new Vector2(150, 7), ENEMY_ACCENT);
                var hpText = Label(rt, "", 9, TextAlignmentOptions.MidlineLeft,
                                   new Vector2(154, -13), new Vector2(46, 9));
                hpText.color = TEXT_DIM;

                var status = Label(rt, "", 9, TextAlignmentOptions.TopLeft,
                                   new Vector2(0, -21), new Vector2(204, 10));
                status.color = GRID_ACCENT;

                _enemyRows.Add(new EnemyRow(row, name, hpFill, hpText, status));
            }
        }

        // ---------- Damage Meter (trái-dưới) — task-damage-meter.md ----------
        //
        // Đọc thẳng BattleState.DamageByUnit (đã có sẵn, đúng đủ mọi nguồn sát thương thật — đòn
        // trực tiếp/DoT/Counter/Reflect, xem RecordDamage ở ActionResolver/StatusProcessor) — KHÔNG
        // tự quét CombatEventQueue (sẽ tranh event với CombatPresenter đang tiêu thụ hàng đợi đó).
        // Góc trái-dưới trống hoàn toàn trước đây (HeroPanel/EnemyPanel chiếm trên, SkillGrid/
        // EndTurn giữa-dưới, AutoSpeed phải-dưới).

        private void BuildDamageMeterPanel()
        {
            var panel = Panel("DamageMeterPanel", new Vector2(0, 0), new Vector2(0, 0),
                              new Vector2(12, 14), new Vector2(150, 118), GRID_ACCENT);

            // task-ui-vfx-polish.md §7 — cùng kỹ thuật HeroPanel/EnemyPanel.
            var meterLandscape = LayoutProfile.CaptureFrom(panel, "DamageMeterPanel_Landscape");
            var meterPortrait = meterLandscape;
            meterPortrait.Name = "DamageMeterPanel_Portrait";
            meterPortrait.SizeDelta = new Vector2(120, 118);
            panel.gameObject.AddComponent<LayoutProfileSwitcher>()
                 .SetProfiles(panel, meterPortrait, meterLandscape);

            var title = Label(panel, "DAMAGE", 12, TextAlignmentOptions.TopLeft,
                              new Vector2(10, -8), new Vector2(120, 16));
            title.color = GRID_ACCENT;
            title.fontStyle = FontStyles.Bold;

            const float rowH = 20f;
            for (int i = 0; i < MAX_METER_ROWS; i++)
            {
                var row = new GameObject($"MeterRow_{i}", typeof(RectTransform));
                row.transform.SetParent(panel, false);
                var rt = (RectTransform)row.transform;
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.anchoredPosition = new Vector2(0, -26 - i * rowH);
                rt.sizeDelta = new Vector2(-20, rowH - 2);

                var name = Label(rt, "", 11, TextAlignmentOptions.TopLeft,
                                 new Vector2(0, 0), new Vector2(78, 16));

                var value = Label(rt, "", 11, TextAlignmentOptions.TopRight,
                                  new Vector2(78, 0), new Vector2(50, 16));
                value.color = TEXT_DIM;

                row.SetActive(false);
                _meterRows.Add(new MeterRow(row, name, value));
            }
        }

        // ---------- Analyze Info panel (phải-dưới) — task-analyze-tactic.md ----------

        private void BuildAnalyzePanel()
        {
            // Anchor phải-dưới, ngay trên AutoSpeed (AutoSpeed=14–52), gap 4px → bắt đầu y=56.
            _analyzePanel = Panel("AnalyzePanel", new Vector2(1, 0), new Vector2(1, 0),
                                  new Vector2(-12, 56), new Vector2(210, 120), ENEMY_ACCENT);
            _analyzePanel.gameObject.SetActive(false);

            // task-ui-vfx-polish.md §7 — cùng kỹ thuật HeroPanel/EnemyPanel/DamageMeterPanel.
            var analyzeLandscape = LayoutProfile.CaptureFrom(_analyzePanel, "AnalyzePanel_Landscape");
            var analyzePortrait = analyzeLandscape;
            analyzePortrait.Name = "AnalyzePanel_Portrait";
            analyzePortrait.SizeDelta = new Vector2(168, 120);
            _analyzePanel.gameObject.AddComponent<LayoutProfileSwitcher>()
                 .SetProfiles(_analyzePanel, analyzePortrait, analyzeLandscape);

            var title = Label(_analyzePanel, "ANALYZED:", 11, TextAlignmentOptions.TopLeft,
                              new Vector2(10, -8), new Vector2(190, 15));
            title.color = ENEMY_ACCENT;
            title.fontStyle = FontStyles.Bold;

            _analyzeName  = Label(_analyzePanel, "", 11, TextAlignmentOptions.TopLeft,
                                  new Vector2(10, -24), new Vector2(190, 16));
            _analyzeHpSp  = Label(_analyzePanel, "", 10, TextAlignmentOptions.TopLeft,
                                  new Vector2(10, -42), new Vector2(190, 14));
            _analyzeStats = Label(_analyzePanel, "", 10, TextAlignmentOptions.TopLeft,
                                  new Vector2(10, -58), new Vector2(190, 14));
            _analyzeElem  = Label(_analyzePanel, "", 10, TextAlignmentOptions.TopLeft,
                                  new Vector2(10, -74), new Vector2(190, 36));
            _analyzeHpSp.color  = TEXT_DIM;
            _analyzeStats.color = TEXT_DIM;
            _analyzeElem.color  = TEXT_DIM;
        }

        // ---------- Turn order bar (giữa trên) ----------

        private void BuildTurnOrderBar()
        {
            var panel = Panel("TurnOrderBar", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                              new Vector2(0, -12), new Vector2(360, 30), GRID_ACCENT);

            // "R{n}" có vùng riêng ở rìa phải — trước đây trùng vị trí với chuỗi turn-order
            // (cả 2 đều đặt tại (6,-4) rộng gần hết panel) nên chữ đè lên nhau ("...enemy_R1").
            _roundLabel = Label(panel, "R1", 12, TextAlignmentOptions.MidlineRight,
                                new Vector2(-6, -4), new Vector2(34, 22));
            _roundLabel.rectTransform.anchorMin = _roundLabel.rectTransform.anchorMax = new Vector2(1, 1);
            _roundLabel.rectTransform.pivot = new Vector2(1, 1);
            _roundLabel.color = GRID_ACCENT;

            _turnOrderLabel = Label(panel, "", 13, TextAlignmentOptions.Center,
                                    new Vector2(6, -4), new Vector2(300, 22));
            // Co chữ thay vì wrap 2 dòng — 8 mục nối bằng "›" thường dài hơn 1 dòng vừa khít,
            // wrap khiến dòng 2 tràn ra khỏi khung panel bo góc 30px cao.
            _turnOrderLabel.enableAutoSizing = true;
            _turnOrderLabel.fontSizeMin = 8;
            _turnOrderLabel.fontSizeMax = 13;
            _turnOrderLabel.enableWordWrapping = false;
            _turnOrderLabel.overflowMode = TextOverflowModes.Ellipsis;
        }

        // ---------- Skill Grid 5×3 (giữa dưới) ----------

        private void BuildSkillGrid()
        {
            const float cell = 52f, gap = 5f;
            float w = GRID_COLS * cell + (GRID_COLS - 1) * gap + 16;
            float h = GRID_ROWS * cell + (GRID_ROWS - 1) * gap + 16;

            var panel = Panel("SkillGrid", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                              new Vector2(0, 58), new Vector2(w, h), GRID_ACCENT);

            // task-ui-vfx-polish.md §9 — Portrait THẬT (thay identity trước đó). Không đổi `cell`/
            // `gap` (tránh viết lại logic tạo nút, rủi ro cao) — dùng field `Scale` sẵn có của
            // `LayoutProfile` (chưa ai dùng tới) để co ĐỀU toàn bộ lưới qua `localScale`: mọi nút +
            // khoảng cách + chữ thu nhỏ cùng tỉ lệ, raycast/click vẫn đúng vị trí thật (Unity tự xử
            // lý input theo RectTransform đã scale). Panel neo pivot=(0.5,0) (đáy-giữa, xem `Panel()`)
            // nên co theo Scale luôn giữ nguyên điểm neo đáy-giữa, chỉ thu nhỏ hướng lên/vào trong —
            // không cần chỉnh lại vị trí.
            //
            // Số tính tay: constraint thật là `AutoSpeed` (phải-dưới, rộng 128, KHÔNG có Portrait
            // riêng — cố định) và `DamageMeterPanel` Portrait (trái-dưới, rộng 120) — cả 2 đều neo
            // góc với biên 12px cố định, không phụ thuộc canvas rộng bao nhiêu. Với canvas portrait
            // thật hẹp nhất hợp lý (~480 đơn vị — tính từ CanvasScaler match=0.5, màn ngang 20:9 tới
            // 21:9 xoay dọc, xem §5/§7 công thức) và biên an toàn 5px mỗi bên quanh SkillGrid:
            // width tối đa an toàn ≈ 480/2 − (12+120+5) ≈ 190 → chọn hệ số co 0.6 (296×0.6≈178, dư
            // ~12px so với ngưỡng 190 — có biên dự phòng thay vì bám sát giới hạn tính toán).
            const float skillGridPortraitScale = 0.6f;
            var gridLandscape = LayoutProfile.CaptureFrom(panel, "SkillGrid_Landscape");
            var gridPortrait = gridLandscape;
            gridPortrait.Name = "SkillGrid_Portrait";
            gridPortrait.Scale = new Vector3(skillGridPortraitScale, skillGridPortraitScale, 1f);
            panel.gameObject.AddComponent<LayoutProfileSwitcher>()
                 .SetProfiles(panel, gridPortrait, gridLandscape);

            // Hàng 0: skill của hero đang tới lượt.
            for (int c = 0; c < GRID_COLS; c++)
            {
                var slot = SkillSlotView.Create(panel, c, cell);
                var rt = (RectTransform)slot.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(8 + c * (cell + gap), -8);
                slot.OnClicked += HandleSlotClicked;
                _slots.Add(slot);
            }

            // Hàng 1: vật phẩm tiêu hao (task-consumable-items.md) — tối đa 5 loại, khớp
            // ĐÚNG GRID_COLS, không cần tính toán kích thước riêng.
            for (int c = 0; c < GRID_COLS; c++)
            {
                var slot = ItemSlotView.Create(panel, c, cell);
                var rt = (RectTransform)slot.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(8 + c * (cell + gap), -8 - (cell + gap));
                slot.OnClicked += HandleItemSlotClicked;
                _itemSlots.Add(slot);
            }

            // Hàng 2: tactic (task-tactic-row.md) — Guard / ESC / SWAP / FOCUS
            BuildTacticRow(panel, cell, gap);
        }

        /// <summary>task-tactic-row.md — 4 nút tactic + task-analyze-tactic.md — nút ANALYZE (col 4).</summary>
        private void BuildTacticRow(RectTransform panel, float cell, float gap)
        {
            float row2Y = -8 - 2 * (cell + gap);
            var labels = new[] { "GUARD", "ESC", "SWAP", "FOCUS", "ANALYZE" };
            var colors = new[] {
                new Color(0.2f, 0.8f, 0.7f),   // Guard — cyan
                new Color(0.9f, 0.3f, 0.3f),   // ESC — red
                new Color(1f,   0.75f, 0.1f),  // SWAP — yellow
                new Color(0.7f, 0.3f, 1f),     // FOCUS — purple
                new Color(1f,   0.55f, 0.1f),  // ANALYZE — orange
            };

            var btns = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                int col = i;  // closure capture
                var go = new GameObject($"TacticBtn_{labels[i]}", typeof(RectTransform));
                go.transform.SetParent(panel, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(8 + col * (cell + gap), row2Y);
                rt.sizeDelta = new Vector2(cell, cell);

                var bg = go.AddComponent<Image>();
                bg.sprite = MetalPanelSprite();
                bg.type = Image.Type.Sliced;
                bg.color = new Color(colors[col].r * 0.25f, colors[col].g * 0.25f,
                                     colors[col].b * 0.25f, 0.92f);

                var btn = go.AddComponent<Button>();
                btn.targetGraphic = bg;
                var cs = btn.colors;
                cs.normalColor = Color.white;
                cs.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
                cs.pressedColor = new Color(0.7f, 0.7f, 0.7f);
                cs.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                btn.colors = cs;
                btns[col] = btn;

                var lbl = Label(go.transform as RectTransform, labels[col], 11,
                                TextAlignmentOptions.Center, Vector2.zero,
                                new Vector2(cell, cell));
                lbl.color = colors[col];

                int idx = col;
                btn.onClick.AddListener(() =>
                {
                    _audio?.PlaySfx("ui/sfx_ui_tick");
                    switch (idx)
                    {
                        case 0: OnGuardPressed?.Invoke();   break;
                        case 1: OnEscapePressed?.Invoke();  break;
                        case 2: OnSwapRowPressed?.Invoke(); break;
                        case 3: OnFocusPressed?.Invoke();   break;
                        case 4: OnAnalyzePressed?.Invoke(); break;
                    }
                });
            }

            _guardBtn   = btns[0];
            _escBtn     = btns[1];
            _swapBtn    = btns[2];
            _focusBtn   = btns[3];
            _analyzeBtn = btns[4];
        }

        private void BuildEndTurn()
        {
            var panel = Panel("EndTurn", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                              new Vector2(0, 14), new Vector2(160, 38), POISE_COLOR);
            var fillImg = panel.Find("Fill").GetComponent<Image>();
            fillImg.color = new Color(0.42f, 0.32f, 0.06f, 0.95f);

            _endTurnButton = panel.gameObject.AddComponent<Button>();
            _endTurnButton.targetGraphic = fillImg;
            _endTurnButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_confirm");
                OnEndTurnPressed?.Invoke();
            });

            var t = Label(panel, "END TURN", 16, TextAlignmentOptions.Center,
                          new Vector2(0, 0), new Vector2(160, 38));
            t.color = POISE_COLOR;
        }

        private void BuildAutoSpeed()
        {
            var panel = Panel("AutoSpeed", new Vector2(1, 0), new Vector2(1, 0),
                              new Vector2(-12, 14), new Vector2(128, 38), GRID_ACCENT);

            _autoLabel = Label(panel, "AUTO OFF", 12, TextAlignmentOptions.Center,
                               new Vector2(4, -2), new Vector2(76, 34));
            var autoBtn = _autoLabel.gameObject.AddComponent<Button>();
            autoBtn.onClick.AddListener(() =>
            {
                _auto = !_auto;
                _autoLabel.text = _auto ? "AUTO ON" : "AUTO OFF";
                _autoLabel.color = _auto ? POISE_COLOR : TEXT;
                _audio?.PlaySfx("ui/sfx_ui_tick");
                OnAutoToggled?.Invoke(_auto);
            });
            _autoLabel.raycastTarget = true;

            _speedLabel = Label(panel, "x1", 14, TextAlignmentOptions.Center,
                                new Vector2(82, -2), new Vector2(40, 34));
            var speedBtn = _speedLabel.gameObject.AddComponent<Button>();
            speedBtn.onClick.AddListener(() =>
            {
                _speed = _speed % 3 + 1;
                _speedLabel.text = $"x{_speed}";
                _audio?.PlaySfx("ui/sfx_ui_tick");
                OnSpeedChanged?.Invoke(_speed);
            });
            _speedLabel.raycastTarget = true;
        }

        // =====================================================================
        // Cập nhật mỗi frame từ BattleState — rẻ vì chỉ đọc, không cấp phát
        // =====================================================================

        private void Update()
        {
            if (_sim == null) return;

            var actor = _sim.CurrentActor;
            bool playerTurn = _sim.NeedsPlayerInput;

            RefreshHeroPanel(actor);
            RefreshEnemyPanel();
            RefreshDamageMeter();
            RefreshAnalyzePanel();
            RefreshTurnOrder();
            RefreshSlots(actor, playerTurn);
            RefreshTacticRow(actor, playerTurn);

            _endTurnButton.interactable = playerTurn;

            if (playerTurn) HandleHotkeys();
        }

        /// <summary>task-accessibility.md, plan.md §10.7 — phím tắt PC: 1-5 chọn ô skill/item,
        /// Enter kết thúc lượt. Dùng Unity.InputSystem (Keyboard.current) — dự án chỉ bật Input
        /// System mới (activeInputHandler=1, xem ActionCommandUI.cs cùng quy ước), KHÔNG dùng
        /// UnityEngine.Input cũ. Tái dùng ĐÚNG đường xử lý click thật (`SkillSlotView.OnClicked`/
        /// `Button.onClick`) thay vì viết logic chọn skill riêng — hotkey chỉ là lối tắt, không
        /// phải luồng thứ 2.</summary>
        private void HandleHotkeys()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) TrySelectSlot(0);
                else if (kb.digit2Key.wasPressedThisFrame) TrySelectSlot(1);
                else if (kb.digit3Key.wasPressedThisFrame) TrySelectSlot(2);
                else if (kb.digit4Key.wasPressedThisFrame) TrySelectSlot(3);
                else if (kb.digit5Key.wasPressedThisFrame) TrySelectSlot(4);
                else if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && _endTurnButton.interactable)
                    _endTurnButton.onClick.Invoke();
            }

            // task-accessibility-part2.md, plan.md §10.7 — gamepad hotkey: 4 nút mặt trước cho ô
            // skill 0-3 (không đủ 5 nút mặt để khớp Ultimate ô 4 — dùng vai phải thay), Start = kết
            // lượt. Tái dùng ĐÚNG TrySelectSlot/_endTurnButton.onClick, cùng nguyên tắc "hotkey chỉ
            // là lối tắt" như nhánh bàn phím ở trên.
            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.buttonWest.wasPressedThisFrame) TrySelectSlot(0);
                else if (gp.buttonSouth.wasPressedThisFrame) TrySelectSlot(1);
                else if (gp.buttonEast.wasPressedThisFrame) TrySelectSlot(2);
                else if (gp.buttonNorth.wasPressedThisFrame) TrySelectSlot(3);
                else if (gp.rightShoulder.wasPressedThisFrame) TrySelectSlot(4);
                else if (gp.startButton.wasPressedThisFrame && _endTurnButton.interactable)
                    _endTurnButton.onClick.Invoke();
            }
        }

        private void TrySelectSlot(int index)
        {
            if (index < 0 || index >= _slots.Count) return;
            var slot = _slots[index];
            if (slot.Interactable) slot.OnClicked?.Invoke(slot);
        }

        private void RefreshHeroPanel(CombatUnit actor)
        {
            if (actor == null || actor.Side != TeamSide.Player)
            {
                // Khi tới lượt địch, vẫn hiện hero đầu tiên còn sống
                actor = FirstAlive(TeamSide.Player);
                if (actor == null) return;
            }

            _heroName.text = $"{DisplayName(actor).ToUpperInvariant()}  Lv{actor.Level}";
            var accent = ElementColor(actor.Element);
            _heroAvatarRing.color = accent;

            if (actor.DefId != _lastPortraitDefId)
            {
                _lastPortraitDefId = actor.DefId;
                var sprite = LoadPortrait(actor.DefId);
                _heroPortrait.sprite = sprite;
                _heroPortrait.enabled = sprite != null;
                _heroInitial.gameObject.SetActive(sprite == null);
                if (sprite == null) _heroInitial.text = Initial(actor.DefId);
            }

            float hpPct = actor.MaxHp > 0 ? (float)actor.Hp / actor.MaxHp : 0f;
            SetFill(_hpFill, actor.Hp, actor.MaxHp);
            _hpFill.color = HpColor(hpPct);
            SetFill(_spFill, actor.Sp, actor.MaxSp);
            SetFill(_ultFill, _sim.State.UltimateGauge, BattleState.ULTIMATE_MAX);

            var s = actor.Stats;
            _heroStats.text =
                $"HP {actor.Hp}/{actor.MaxHp}   SP {actor.Sp}/{actor.MaxSp}\n" +
                $"ATK {s.AtkPhys:0}  DEF {s.Def:0}  SPD {s.Spd:0}\n" +
                $"CRIT {s.Crit * 100:0}%  {actor.Element}  {StatusSummary(actor)}";
        }

        private void RefreshEnemyPanel()
        {
            int row = 0;
            for (int i = 0; i < _sim.State.Units.Count && row < _enemyRows.Count; i++)
            {
                var u = _sim.State.Units[i];
                if (u.Side != TeamSide.Enemy) continue;

                var r = _enemyRows[row++];
                bool dead = u.IsDead;

                r.Root.SetActive(true);
                r.Name.text = dead ? $"{ShortDisplayName(u)} — DOWN" : ShortDisplayName(u);
                r.Name.color = dead ? DEAD_COLOR : TEXT;

                if (dead)
                {
                    r.HpFill.gameObject.transform.parent.gameObject.SetActive(false);
                    r.HpText.text = "";
                    r.StatusText.text = "";
                    continue;
                }

                r.HpFill.gameObject.transform.parent.gameObject.SetActive(true);
                float pct = u.MaxHp > 0 ? (float)u.Hp / u.MaxHp : 0f;
                AnimateFill(r.HpFill, pct);
                r.HpFill.color = HpColor(pct);
                r.HpText.text = $"{u.Hp}/{u.MaxHp}";

                string brk = u.IsBroken ? "[BREAK] " : "";
                string st = StatusSummary(u);
                r.StatusText.text = (brk + st).Trim();
            }

            for (; row < _enemyRows.Count; row++) _enemyRows[row].Root.SetActive(false);
        }

        /// <summary>task-damage-meter.md — top <see cref="MAX_METER_ROWS"/> unit theo tổng sát
        /// thương giảm dần. Chỉ hiện unit ĐÃ TỪNG gây sát thương (RecordDamage bỏ qua amount &lt;= 0
        /// nên dictionary không có entry rác) — tránh liệt kê "0 dmg" ngay đầu trận.</summary>
        private void RefreshDamageMeter()
        {
            _meterBuffer.Clear();
            foreach (var kv in _sim.State.DamageByUnit) _meterBuffer.Add(kv);
            _meterBuffer.Sort((a, b) => b.Value.CompareTo(a.Value));

            int shown = Mathf.Min(_meterBuffer.Count, _meterRows.Count);
            for (int i = 0; i < shown; i++)
            {
                var u = _sim.State.GetUnit(_meterBuffer[i].Key);
                var r = _meterRows[i];
                r.Root.SetActive(true);
                r.Name.text = u != null ? ShortDisplayName(u) : $"#{_meterBuffer[i].Key}";
                r.Name.color = u != null && u.Side == TeamSide.Player ? HERO_ACCENT : ENEMY_ACCENT;
                r.Value.text = _meterBuffer[i].Value.ToString();
            }
            for (int i = shown; i < _meterRows.Count; i++) _meterRows[i].Root.SetActive(false);
        }

        /// <summary>task-analyze-tactic.md — hiện stat địch được analyze gần nhất (ID cao nhất =
        /// được cấp phát muộn nhất = được analyze sau cùng).</summary>
        private void RefreshAnalyzePanel()
        {
            if (_analyzePanel == null) return;
            var ids = _sim.State.AnalyzedEnemyIds;
            if (ids.Count == 0) { _analyzePanel.gameObject.SetActive(false); return; }

            _analyzePanel.gameObject.SetActive(true);
            int lastId = -1;
            foreach (var id in ids) if (id > lastId) lastId = id;

            var u = _sim.State.GetUnit(lastId);
            if (u == null) return;

            _analyzeName.text  = $"{DisplayName(u).ToUpperInvariant()}  [{u.Element}]";
            _analyzeHpSp.text  = $"HP {u.Hp}/{u.MaxHp}  SP {u.Sp}/{u.MaxSp}";
            var s = u.Stats;
            _analyzeStats.text = $"ATK {s.AtkPhys:0}  DEF {s.Def:0}  SPD {s.Spd:0}";

            float mFire  = ElementTable.Multiplier(Element.Fire,  u.Element);
            float mWater = ElementTable.Multiplier(Element.Water, u.Element);
            float mEarth = ElementTable.Multiplier(Element.Earth, u.Element);
            float mWind  = ElementTable.Multiplier(Element.Wind,  u.Element);
            _analyzeElem.text = $"F×{mFire:0.0} W×{mWater:0.0} E×{mEarth:0.0} Wi×{mWind:0.0}";
        }

        private void RefreshTurnOrder()
        {
            _roundLabel.text = $"R{_sim.State.RoundNumber}";

            var order = new TurnOrderPreview(_sim).Take(8);
            _turnOrderLabel.text = string.Join(" › ", order);
        }

        private void RefreshSlots(CombatUnit actor, bool playerTurn)
        {
            bool ultReady = _sim.State.IsUltimateReady;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];

                if (actor != null && actor.Side == TeamSide.Player)
                    slot.Bind(actor.GetSkill(i));
                else
                    slot.Bind(null);

                slot.Refresh(playerTurn ? actor : null, _selectedItemSlot < 0 && _selectedSlot == i, ultReady);
            }

            RefreshItemSlots(playerTurn);
        }

        /// <summary>task-consumable-items.md — chỉ phe Player mới có ItemLoadout (AI địch không
        /// dùng item), lặp <see cref="ItemType"/> theo đúng thứ tự cố định enum (khớp
        /// <c>ItemCatalog.ALL</c>), tối đa 5 ô = GRID_COLS.</summary>
        private void RefreshItemSlots(bool playerTurn)
        {
            int i = 0;
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                if (i >= _itemSlots.Count) break;
                bool owns = _sim.State.ItemLoadout.ContainsKey(type);
                int remaining = owns ? _sim.State.ItemLoadout[type] : 0;
                _itemSlots[i].Bind(owns ? type : (ItemType?)null,
                                    remaining, playerTurn && _selectedItemSlot == i);
                i++;
            }
        }

        /// <summary>task-tactic-row.md — enable/disable các nút tactic theo context.</summary>
        private void RefreshTacticRow(CombatUnit actor, bool playerTurn)
        {
            if (_guardBtn == null) return;

            bool canEscape  = playerTurn && _sim.State.AllowEscape;
            bool canSwap    = playerTurn && actor != null && !actor.HasSwappedRowThisTurn;
            bool canAnalyze = playerTurn && actor != null && actor.Sp >= 5;

            _guardBtn.interactable   = playerTurn;
            _escBtn.interactable     = canEscape;
            _swapBtn.interactable    = canSwap;
            _focusBtn.interactable   = playerTurn;
            _analyzeBtn.interactable = canAnalyze;
        }

        private void HandleSlotClicked(SkillSlotView slot)
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _selectedSlot = slot.SlotIndex;
            _selectedItemSlot = -1;
            OnSkillChosen?.Invoke(slot.SlotIndex);
        }

        private void HandleItemSlotClicked(ItemSlotView slot)
        {
            if (slot.Type == null) return;
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _selectedItemSlot = slot.SlotIndex;
            _selectedSlot = -1;
            OnItemChosen?.Invoke(slot.Type.Value);
        }

        public void ClearSelection() { _selectedSlot = -1; _selectedItemSlot = -1; }

        /// <summary>Đồng bộ nhãn/nút AUTO với trạng thái THẬT của simulation (bug báo trực tiếp:
        /// "không chọn auto mà vẫn chơi auto") — <see cref="_auto"/> luôn khởi tạo false/"AUTO OFF"
        /// bất kể <c>BattleSceneInstaller.BuildBattle</c> đã khôi phục <c>_autoPlay</c> thật từ
        /// <c>SettingsDto.AutoBattle</c> (trận trước bật Auto → trận này chạy auto ngầm dù HUD hiện
        /// "AUTO OFF"). Gọi 1 lần ngay sau <see cref="Bind"/>, KHÔNG phát <see cref="OnAutoToggled"/>
        /// (chỉ đồng bộ hiển thị, không phải người chơi bấm).</summary>
        public void SetAutoState(bool auto)
        {
            _auto = auto;
            if (_autoLabel == null) return;
            _autoLabel.text = _auto ? "AUTO ON" : "AUTO OFF";
            _autoLabel.color = _auto ? POISE_COLOR : TEXT;
        }

        // =====================================================================
        // Helper dựng UI — panel bo góc + viền màu (thay hình chữ nhật phẳng cũ)
        // =====================================================================

        private static Sprite _metalPanelSprite;

        /// <summary>task-ui-vfx-polish.md §6 — texture pixel-art dùng chung với 11 màn Meta (Point
        /// filter, cứng nét) thay cho <see cref="RoundedSprite"/> (procedural bilinear, mượt) —
        /// trước đây HUD trận là màn DUY NHẤT không theo ngôn ngữ hình ảnh chung của game. File gốc
        /// ở <c>Art/UI/Frames/</c> (dùng qua tham chiếu serialize trong 11 prefab, không cần
        /// Resources) — copy 1 bản sang <c>Resources/Art/UI/Frames/</c> (GUID mới, không đụng bản
        /// gốc) vì màn này dựng code thuần, cần <see cref="Resources.Load"/>.</summary>
        private static Sprite MetalPanelSprite()
        {
            if (_metalPanelSprite == null)
                _metalPanelSprite = Resources.Load<Sprite>("Art/UI/Frames/pixel_metal_panel");
            return _metalPanelSprite;
        }

        private static Sprite _bronzeFrameSprite;

        private static Sprite BronzeFrameSprite()
        {
            if (_bronzeFrameSprite == null)
                _bronzeFrameSprite = Resources.Load<Sprite>("Art/UI/Frames/pixel_bronze_frame");
            return _bronzeFrameSprite;
        }

        private static Sprite _circleSprite;

        private static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;

            const int size = 64;
            const float aa = 1.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var center = new Vector2(size / 2f, size / 2f);
            float r = size / 2f - 1f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01((r - dist) / aa + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _circleSprite;
        }

        /// <summary>Panel bo góc 2 lớp: viền màu accent (lớp dưới) + nền tối (lớp trên, inset 3px) —
        /// nhìn giống khung neon của image_UI.jpg thay vì hình chữ nhật viền phẳng.</summary>
        private RectTransform Panel(string name, Vector2 anchorMin, Vector2 anchorMax,
                                    Vector2 pos, Vector2 size, Color accent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var border = go.AddComponent<Image>();
            border.sprite = BronzeFrameSprite();
            border.type = Image.Type.Sliced;
            border.color = accent;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(rt, false);
            var frt = (RectTransform)fillGo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(3, 3); frt.offsetMax = new Vector2(-3, -3);
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = MetalPanelSprite();
            fill.type = Image.Type.Sliced;
            fill.color = PANEL_BG;
            fill.raycastTarget = false;

            return rt;
        }

        /// <summary>Avatar tròn: viền màu nguyên tố + ảnh nhân vật thật (crop tròn qua Mask),
        /// chữ cái chỉ còn là fallback khi chưa nạp được sprite — trước đây LUÔN hiện chữ cái
        /// dù đã có sẵn sprite battle của hero, không có icon nhân vật nào thật sự.</summary>
        private static void BuildAvatar(RectTransform parent, Vector2 pos, float size, Color accent,
                                        out Image ring, out TextMeshProUGUI initial, out Image portrait)
        {
            var ringGo = new GameObject("AvatarRing", typeof(RectTransform));
            ringGo.transform.SetParent(parent, false);
            var rrt = (RectTransform)ringGo.transform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0, 1);
            rrt.pivot = new Vector2(0, 1);
            rrt.anchoredPosition = pos;
            rrt.sizeDelta = new Vector2(size, size);
            ring = ringGo.AddComponent<Image>();
            ring.sprite = CircleSprite();
            ring.color = accent;
            ring.raycastTarget = false;

            var innerGo = new GameObject("AvatarFill", typeof(RectTransform));
            innerGo.transform.SetParent(rrt, false);
            var irt = (RectTransform)innerGo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(3, 3); irt.offsetMax = new Vector2(-3, -3);
            var inner = innerGo.AddComponent<Image>();
            inner.sprite = CircleSprite();
            inner.color = new Color(0.169f, 0.106f, 0.180f, 1f);
            inner.raycastTarget = false;
            var mask = innerGo.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var portraitGo = new GameObject("Portrait", typeof(RectTransform));
            portraitGo.transform.SetParent(irt, false);
            var prt = (RectTransform)portraitGo.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(-4, -6); prt.offsetMax = new Vector2(4, 6);
            portrait = portraitGo.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.enabled = false;

            initial = Label(irt, "?", size * 0.4f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(size, size));
            initial.raycastTarget = false;
        }

        /// <summary>Nạp sprite battle sẵn có của hero làm avatar — SpriteFolder trùng DefId
        /// cho cả 6 hero hiện có (heroes.csv), không cần vẽ portrait riêng.</summary>
        private static Sprite LoadPortrait(string defId)
        {
            if (_portraitCache.TryGetValue(defId, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>($"Art/Characters/Heroes/{defId}/{defId}_v1_00");
            _portraitCache[defId] = sprite;
            return sprite;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string text, float size,
                                             TextAlignmentOptions align, Vector2 pos, Vector2 dim)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = dim;

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = TEXT;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        private static void BarLabel(RectTransform parent, string text, Vector2 pos, float barWidth = 178f)
        {
            var t = Label(parent, text, 11, TextAlignmentOptions.MidlineLeft, pos, new Vector2(24, 13));
            t.color = TEXT_DIM;
        }

        private static Image Bar(RectTransform parent, Vector2 pos, Vector2 size, Color color)
        {
            var bg = new GameObject("BarBg", typeof(RectTransform));
            bg.transform.SetParent(parent, false);
            var brt = (RectTransform)bg.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 1);
            brt.anchoredPosition = pos;
            brt.sizeDelta = size;
            var bimg = bg.AddComponent<Image>();
            bimg.sprite = MetalPanelSprite();
            bimg.type = Image.Type.Sliced;
            bimg.color = new Color(0.08f, 0.06f, 0.09f, 0.95f);
            bimg.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(brt, false);
            var frt = (RectTransform)fill.transform;
            frt.anchorMin = new Vector2(0, 0);
            frt.anchorMax = new Vector2(1, 1);
            frt.pivot = new Vector2(0, 0.5f);
            frt.offsetMin = new Vector2(1, 1);
            frt.offsetMax = new Vector2(-1, -1);
            var fimg = fill.AddComponent<Image>();
            fimg.color = color;
            fimg.type = Image.Type.Filled;
            fimg.fillMethod = Image.FillMethod.Horizontal;
            fimg.raycastTarget = false;
            return fimg;
        }

        // Tốc độ hội tụ theo % KHÔNG GIAN CÒN LẠI mỗi giây (kiểu lerp mượt), KHÔNG PHẢI
        // tốc độ tuyệt đối cố định — MoveTowards trước đó chạy với tốc độ cố định nên một
        // đòn mất 5% máu chỉ mất ~0.03s để bar đuổi kịp, nhìn như snap tức thì, chỉ đòn gần
        // chết (mất gần hết thanh) mới thấy "chạy". Lerp mũ cho thời gian hội tụ GẦN NHƯ
        // NHƯ NHAU dù đòn to hay nhỏ — luôn thấy bar rút, đúng cảm giác "chạy theo giá trị".
        private const float FILL_LERP_RATE = 7f; // hội tụ ~95% sau ~0.43s bất kể mất bao nhiêu %

        private static void SetFill(Image img, float current, float max)
            => AnimateFill(img, max > 0f ? Mathf.Clamp01(current / max) : 0f);

        private static void AnimateFill(Image img, float target)
        {
            float t = 1f - Mathf.Exp(-FILL_LERP_RATE * Time.deltaTime);
            img.fillAmount = Mathf.Abs(img.fillAmount - target) < 0.0015f
                ? target
                : Mathf.Lerp(img.fillAmount, target, t);
        }

        /// <summary>Xanh khi khoẻ → vàng cảnh báo → đỏ nguy hiểm, khớp cách image_UI.jpg
        /// và các JRPG khác mã hoá % máu bằng màu thay vì luôn 1 màu cố định.
        /// task-accessibility.md — `SettingsDto.ColorblindMode` (tồn tại sẵn, chưa từng dùng) đổi
        /// sang bộ màu xanh dương/cam/đỏ SẪM: xanh lá/đỏ tươi là cặp khó phân biệt nhất với
        /// protanopia/deuteranopia (dạng mù màu phổ biến nhất) vì chỉ khác NHAU về hue; bộ thay thế
        /// khác nhau CẢ hue lẫn ĐỘ SÁNG (đỏ sẫm gần đen ở mức nguy hiểm) để vẫn phân biệt được dù
        /// mù màu loại nào.</summary>
        private Color HpColor(float pct)
        {
            bool colorblind = _settings?.Current.ColorblindMode ?? false;
            if (colorblind)
            {
                return pct switch
                {
                    > 0.6f => new Color(0.169f, 0.447f, 0.698f),
                    > 0.3f => new Color(0.902f, 0.624f, 0.000f),
                    _ => new Color(0.502f, 0.000f, 0.125f)
                };
            }
            return pct switch
            {
                > 0.6f => new Color(0.482f, 0.788f, 0.314f),
                > 0.3f => new Color(1f, 0.820f, 0.400f),
                _ => new Color(0.902f, 0.224f, 0.275f)
            };
        }

        private static Color ElementColor(Element e) => e switch
        {
            Element.Fire  => new Color(0.902f, 0.400f, 0.275f),
            Element.Water => new Color(0.271f, 0.482f, 0.616f),
            Element.Earth => new Color(0.651f, 0.443f, 0.259f),
            Element.Wind  => new Color(0.482f, 0.788f, 0.314f),
            Element.Light => new Color(1.000f, 0.820f, 0.400f),
            Element.Dark  => new Color(0.608f, 0.365f, 0.898f),
            _ => TEXT
        };

        private static string Initial(string defId)
        {
            var parts = defId.Replace("hero_", "").Replace("enemy_", "").Split('_');
            return parts.Length > 0 && parts[0].Length > 0 ? parts[0][..1].ToUpperInvariant() : "?";
        }

        private static string Short(string defId)
        {
            string s = defId.Replace("enemy_", "").Replace("boss_", "");
            return s.Length > 14 ? s[..14] : s;
        }

        /// <summary>task-phase-5-gaps.md Phần D đã xây <see cref="ILocalizationService.GetName"/>
        /// nhưng chỉ migrate 5 màn Meta — HUD trận vẫn hiện raw <c>DefId</c> tới giờ. <c>_loc==null</c>
        /// (hiếm, chỉ khi service chưa đăng ký) → fallback nguyên hành vi cũ.</summary>
        private string DisplayName(CombatUnit u)
        {
            if (_loc == null) return u.DefId.ToUpperInvariant();
            var kind = u.Side == TeamSide.Player ? LocalizedNameKind.Hero
                : u.DefId.StartsWith("boss_") ? LocalizedNameKind.Boss
                : LocalizedNameKind.Enemy;
            return _loc.GetName(u.DefId, kind);
        }

        private string ShortDisplayName(CombatUnit u)
        {
            if (_loc == null) return Short(u.DefId);
            string name = DisplayName(u);
            return name.Length > 14 ? name[..14] : name;
        }

        private CombatUnit FirstAlive(TeamSide side)
        {
            for (int i = 0; i < _sim.State.Units.Count; i++)
            {
                var u = _sim.State.Units[i];
                if (u.Side == side && u.IsAlive) return u;
            }
            return null;
        }

        private static string StatusSummary(CombatUnit u)
        {
            if (u.Statuses.Count == 0) return "";
            var sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < u.Statuses.Count && i < 6; i++)
            {
                var s = u.Statuses[i];
                sb.Append(s.Id);
                if (s.Stacks > 1) sb.Append('×').Append(s.Stacks);
                sb.Append(' ');
            }
            return sb.ToString();
        }

        /// <summary>Bọc PreviewOrder để HUD không phải biết TurnScheduler.</summary>
        private readonly struct TurnOrderPreview
        {
            private readonly CombatSimulation _sim;
            public TurnOrderPreview(CombatSimulation sim) => _sim = sim;

            public List<string> Take(int count)
            {
                var names = new List<string>(count);
                var scheduler = new Game.Combat.Systems.TurnScheduler(_sim.State);
                var ids = scheduler.PreviewOrder(count);
                for (int i = 0; i < ids.Count; i++)
                {
                    var u = _sim.State.GetUnit(ids[i]);
                    if (u == null) continue;
                    string n = u.DefId.Length > 6 ? u.DefId[..6] : u.DefId;
                    names.Add(u.Side == TeamSide.Player ? $"<color=#7BC950>{n}</color>" : n);
                }
                return names;
            }
        }
    }
}
