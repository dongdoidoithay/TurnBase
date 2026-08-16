using Game.Core;
using Game.Core.UI;
using Game.Data.Dto;
using Game.Meta.Content;
using Game.Meta.Hero;
using Game.Services.Audio;
using Game.Services.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta
{
    /// <summary>
    /// Modal xem chi tiết 1 hero: portrait, cấp/EXP, 6 chỉ số gốc, danh sách skill (nâng cấp
    /// tại chỗ), nút Ascend. Dựng từ UI_HeroDetail.prefab (Instantiate + Find) — không dựng
    /// bằng code. Mở từ nút DETAIL (avatar) trên hero card ở TeamSelectScreen.
    /// </summary>
    public sealed class HeroDetailScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_HeroDetail";
        private const int SkillSlotCount = 5;
        private static readonly string[] STAT_ORDER = { "STR", "CON", "INT", "DEX", "AUR", "LUK" };
        private static readonly Color TEXT_DIM = new(0.65f, 0.60f, 0.64f);
        private static readonly Color DISABLED = new(0.32f, 0.29f, 0.33f);
        private static readonly Color GOLD_ACTIVE = new(0.647f, 0.365f, 0.898f, 0.95f);

        private GameObject _root;
        private Text _title, _levelLabel, _expLabel;
        private Image _expFill, _portrait;
        private readonly Text[] _statValues = new Text[6];
        private Text[] _skillNameLabels, _skillLevelLabels, _skillUpgradeCostLabels;
        private Button[] _skillUpgradeButtons;
        private Button _closeButton, _ascendButton;
        private Text _ascendLabel;
        private Image _ascendImage;

        private IAudioService _audio;
        private ILocalizationService _loc;
        private PlayerProfileDto _profile;
        private HeroInstanceDto _hero;

        // task-phase-5-gaps.md Phần D — HeroDisplayUtil vẫn là fallback cuối khi service (hiếm khi)
        // chưa đăng ký, không đổi hành vi cũ trong trường hợp đó.
        private string HeroName(string defId) =>
            _loc != null ? _loc.GetName(defId, LocalizedNameKind.Hero) : HeroDisplayUtil.FormatName(defId);
        private string SkillName(string skillId) =>
            _loc != null ? _loc.GetName(skillId, LocalizedNameKind.Skill) : HeroDisplayUtil.FormatSkillName(skillId);

        /// <summary>Bắn ra sau mỗi lần Ascend/nâng skill thành công (đổi Wallet) — TeamSelectScreen
        /// nghe để cập nhật TopBar, giống pattern OnProfileChanged sẵn có của chính nó.</summary>
        public event System.Action OnProfileChanged;

        public void Open(PlayerProfileDto profile, HeroInstanceDto hero)
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _loc);
            if (_root == null) BuildShell();

            _profile = profile;
            _hero = hero;
            _root.SetActive(true);
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            LayoutProfileSwitcher.ApplyStretchPanelLandscape(panel.gameObject, "HeroDetailPanel");
            _title = panel.Find("Title").GetComponent<Text>();
            _levelLabel = panel.Find("LevelLabel").GetComponent<Text>();
            _expLabel = panel.Find("ExpBar/ExpLabel").GetComponent<Text>();
            _expFill = panel.Find("ExpBar/Fill").GetComponent<Image>();
            _portrait = panel.Find("PortraitFrame/PortraitMask/PortraitSprite").GetComponent<Image>();

            var statsContainer = panel.Find("StatsContainer");
            for (int i = 0; i < STAT_ORDER.Length; i++)
                _statValues[i] = statsContainer.Find($"Row_{STAT_ORDER[i]}/Value").GetComponent<Text>();

            var skillList = panel.Find("SkillListContainer");
            _skillNameLabels = new Text[SkillSlotCount];
            _skillLevelLabels = new Text[SkillSlotCount];
            _skillUpgradeCostLabels = new Text[SkillSlotCount];
            _skillUpgradeButtons = new Button[SkillSlotCount];
            for (int i = 0; i < SkillSlotCount; i++)
            {
                var row = skillList.Find($"Row_{i}");
                _skillNameLabels[i] = row.Find("NameLabel").GetComponent<Text>();
                _skillLevelLabels[i] = row.Find("LevelLabel").GetComponent<Text>();
                var btn = row.Find("UpgradeButton");
                _skillUpgradeCostLabels[i] = btn.Find("Label").GetComponent<Text>();
                _skillUpgradeButtons[i] = btn.GetComponent<Button>();

                int slot = i; // capture đúng giá trị cho closure
                _skillUpgradeButtons[i].onClick.AddListener(() => UpgradeSkill(slot));
            }

            _ascendButton = panel.Find("AscendButton").GetComponent<Button>();
            _ascendImage = panel.Find("AscendButton").GetComponent<Image>();
            _ascendLabel = panel.Find("AscendButton/Label").GetComponent<Text>();
            _ascendButton.onClick.AddListener(Ascend);

            _closeButton = panel.Find("CloseButton").GetComponent<Button>();
            _closeButton.onClick.AddListener(Close);
        }

        private void Close()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _root.SetActive(false);
        }

        private void UpgradeSkill(int slot)
        {
            if (!SkillUpgradeSystem.TryUpgrade(_profile, _hero, slot)) return;
            _audio?.PlaySfx("ui/sfx_ui_confirm");
            Refresh();
            OnProfileChanged?.Invoke();
        }

        private void Ascend()
        {
            if (!AscendSystem.TryAscend(_profile, _hero)) return;
            _audio?.PlaySfx("ui/sfx_ui_confirm");
            Refresh();
            OnProfileChanged?.Invoke();
        }

        private void Refresh()
        {
            var hero = _hero;
            _title.text = HeroName(hero.DefId).ToUpperInvariant();
            _levelLabel.text = $"Lv {hero.Level}   ★{hero.Star}";

            int cap = HeroLevelSystem.LevelCap(hero.Star);
            if (hero.Level >= cap)
            {
                _expFill.fillAmount = 1f;
                _expLabel.text = "MAX LEVEL";
            }
            else
            {
                long haveAtLevel = HeroLevelSystem.ExpToReachLevel(hero.Level);
                long needForNext = HeroLevelSystem.ExpToReachLevel(hero.Level + 1);
                long span = System.Math.Max(1, needForNext - haveAtLevel);
                long progress = System.Math.Clamp(hero.Exp - haveAtLevel, 0, span);
                _expFill.fillAmount = progress / (float)span;
                _expLabel.text = $"{progress}/{span} EXP";
            }

            // task-addressables-pilot.md — pilot Addressables cho HeroDefinitionSO, xem doc-comment
            // đầy đủ ở TeamSelectScreen.FindHeroDef (cùng khuôn: WaitForCompletion đồng bộ).
            var def = UnityEngine.AddressableAssets.Addressables
                .LoadAssetAsync<HeroDefinitionSO>($"Data/Heroes/{hero.DefId}").WaitForCompletion();
            if (def != null)
            {
                var p = HeroLevelSystem.EffectivePrimary(def.BasePrimary, hero.Level);
                _statValues[0].text = $"{p.Str:0}";
                _statValues[1].text = $"{p.Con:0}";
                _statValues[2].text = $"{p.Int:0}";
                _statValues[3].text = $"{p.Dex:0}";
                _statValues[4].text = $"{p.Aur:0}";
                _statValues[5].text = $"{p.Luk:0}";

                for (int i = 0; i < SkillSlotCount; i++)
                    RefreshSkillRow(i, i < def.SkillIds.Length ? def.SkillIds[i] : null);
            }

            var sprite = Resources.Load<Sprite>($"Art/Characters/Heroes/{hero.DefId}/{hero.DefId}_v1_00");
            _portrait.sprite = sprite;
            _portrait.enabled = sprite != null;

            RefreshAscendButton();
        }

        private void RefreshSkillRow(int slot, string skillId)
        {
            bool hasSkill = !string.IsNullOrEmpty(skillId);
            _skillNameLabels[slot].text = hasSkill ? SkillName(skillId) : "—";

            if (!hasSkill)
            {
                _skillLevelLabels[slot].text = "";
                _skillUpgradeButtons[slot].gameObject.SetActive(false);
                return;
            }

            // Slot C(3)/Ultimate(4) khoá tới khi đủ sao — task-ascend.md §4.
            if (!AscendSystem.IsSkillSlotUnlocked(_hero.Star, slot))
            {
                int requiredStar = slot == 4 ? 5 : 4;
                _skillLevelLabels[slot].text = $"Locked ★{requiredStar}";
                _skillLevelLabels[slot].color = TEXT_DIM;
                _skillUpgradeButtons[slot].gameObject.SetActive(false);
                return;
            }

            int level = _hero.SkillLevels[slot];
            _skillLevelLabels[slot].text = $"Lv {level}/{SkillUpgradeSystem.MAX_SKILL_LEVEL}";
            _skillLevelLabels[slot].color = Color.white;

            bool canUpgrade = SkillUpgradeSystem.CanUpgrade(_hero, slot);
            _skillUpgradeButtons[slot].gameObject.SetActive(true);
            _skillUpgradeButtons[slot].interactable = canUpgrade;
            _skillUpgradeCostLabels[slot].text = canUpgrade
                ? $"+{SkillUpgradeSystem.UpgradeCost(level)}g"
                : "MAX";
        }

        private void RefreshAscendButton()
        {
            var cost = AscendSystem.CostForNextStar(_hero);
            bool canAscend = cost.HasValue;
            _ascendButton.interactable = canAscend;
            _ascendImage.color = canAscend ? GOLD_ACTIVE : DISABLED;

            // HeroInstanceDto.Awakened trước đây chỉ được GHI (AscendSystem.TryAscend) chưa từng
            // được ĐỌC ở production — người chơi không có cách nào biết hero đã Awakening thật
            // hay chưa ngoài cảm nhận qua combat. Đọc thẳng field này (không suy ra lại từ Star)
            // để phòng trường hợp hero được tạo thẳng ở ★6 (import/debug) mà chưa qua TryAscend.
            if (!canAscend) { _ascendLabel.text = _hero.Awakened ? "★6 · AWAKENED" : "MAX STAR"; return; }

            var c = cost.Value;
            string materials = string.Join(" · ", System.Array.ConvertAll(c.Materials,
                m => $"{m.Amount} {m.Type}"));
            _ascendLabel.text = $"ASCEND ★{_hero.Star + 1}\n{c.Shards} Shard · {materials} · {c.Gold}g";
        }
    }
}
