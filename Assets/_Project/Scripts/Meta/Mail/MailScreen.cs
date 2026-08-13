using System.Linq;
using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Services.Audio;
using Game.Services.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta.Mail
{
    /// <summary>
    /// Modal Mail — task-mail.md. Mở từ nút MAIL trên TopBar. Dựng từ UI_Mail.prefab (clone
    /// UI_Quest.prefab), cùng khuôn <see cref="Quest.QuestScreen"/>. Khác Quest (luôn đúng 6 mục
    /// cố định), số mail thật là ĐỘNG — ẩn/hiện row theo <c>profile.Mail.Count</c>, mail chưa
    /// claim hiện trước.
    /// </summary>
    public sealed class MailScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_Mail";
        private const int ROW_COUNT = 6;

        private GameObject _root;
        private Text _walletLabel;
        private GameObject[] _rows;
        private Text[] _nameLabels;
        private Text[] _progressLabels;
        private Button[] _claimButtons;
        private Button _closeButton;
        private Button _claimAllButton;

        private IAudioService _audio;
        private IEconomyService _economy;
        private PlayerProfileDto _profile;
        private System.Action _onClosed;

        /// <summary>Bắn sau Claim/Claim-All/PurgeExpired (nếu có xoá gì) — task-mail-extras.md,
        /// mirror <c>TeamSelectScreen.OnProfileChanged</c>. MetaSceneInstaller nghe để refresh
        /// badge trên TopBar (MailScreen không tự vẽ TopBar, 2 script độc lập).</summary>
        public event System.Action OnMailChanged;

        public void Open(PlayerProfileDto profile, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _economy);
            if (_root == null) BuildShell();

            _profile = profile;
            _onClosed = onClosed;

            // Dọn mail hết hạn TRƯỚC khi vẽ — cùng mẫu DungeonSystem.EnsureDailyReset (gọi ở điểm
            // vào màn hình liên quan). Mail Welcome không có hạn (ExpiresAtUtc rỗng) nên không bị
            // đụng — chỉ ảnh hưởng mail có hạn thật (test tự tạo để verify pipeline).
            if (MailSystem.PurgeExpired(_profile, System.DateTime.UtcNow) > 0)
                OnMailChanged?.Invoke();

            _root.SetActive(true);
            Refresh();
        }

        private void BuildShell()
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            _walletLabel = panel.Find("WalletLabel").GetComponent<Text>();

            var list = panel.Find("RowListContainer");
            _rows = new GameObject[ROW_COUNT];
            _nameLabels = new Text[ROW_COUNT];
            _progressLabels = new Text[ROW_COUNT];
            _claimButtons = new Button[ROW_COUNT];
            for (int i = 0; i < ROW_COUNT; i++)
            {
                var row = list.Find($"Row_{i}");
                _rows[i] = row.gameObject;
                _nameLabels[i] = row.Find("NameLabel").GetComponent<Text>();
                _progressLabels[i] = row.Find("ProgressLabel").GetComponent<Text>();
                var btn = row.Find("ClaimButton");
                _claimButtons[i] = btn.GetComponent<Button>();

                int index = i; // capture đúng giá trị cho closure
                _claimButtons[i].onClick.AddListener(() => Claim(index));
            }

            _closeButton = panel.Find("CloseButton").GetComponent<Button>();
            _closeButton.onClick.AddListener(Close);

            _claimAllButton = panel.Find("ClaimAllButton").GetComponent<Button>();
            _claimAllButton.onClick.AddListener(ClaimAll);
        }

        private void Close()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _root.SetActive(false);
            _onClosed?.Invoke();
        }

        /// <summary>Mail chưa claim lên trước — sort ổn định theo <c>CreatedAtUtc</c> trong từng
        /// nhóm để thứ tự không nhảy lung tung giữa các lần Refresh.</summary>
        private System.Collections.Generic.List<MailDto> SortedMail()
            => _profile.Mail.OrderBy(m => m.Claimed).ThenBy(m => m.CreatedAtUtc).ToList();

        private void Claim(int index)
        {
            var mail = SortedMail();
            if (index >= mail.Count) return;

            if (!MailSystem.TryClaim(_profile, mail[index].Id, _economy)) return;
            _audio?.PlaySfx("ui/sfx_ui_confirm");
            OnMailChanged?.Invoke();
            Refresh();
        }

        private void ClaimAll()
        {
            int count = MailSystem.ClaimAll(_profile, _economy);
            if (count == 0) return;
            _audio?.PlaySfx("ui/sfx_ui_confirm");
            OnMailChanged?.Invoke();
            Refresh();
        }

        private void Refresh()
        {
            long gold = _economy?.Get(_profile.Wallet, CurrencyType.Gold) ?? 0;
            long gem = _economy?.Get(_profile.Wallet, CurrencyType.Gem) ?? 0;
            _walletLabel.text = $"Gold {gold}   Gem {gem}";

            var mail = SortedMail();
            for (int i = 0; i < ROW_COUNT; i++)
            {
                bool active = i < mail.Count;
                _rows[i].SetActive(active);
                if (!active) continue;

                var m = mail[i];
                _nameLabels[i].text = m.Title;
                _progressLabels[i].text = m.Claimed ? "CLAIMED" : FormatRewardLine(m);
                _claimButtons[i].interactable = !m.Claimed;
            }

            _claimAllButton.interactable = mail.Any(m => !m.Claimed);
        }

        /// <summary>Rút gọn khỏi "+2000 Gold · +100 Gem" (bản gốc TRÀN ProgressLabel 90px — phát
        /// hiện lúc đo thật bằng TextGenerator, task-mail-extras.md §0, không phải lỗi mới do task
        /// này gây ra) — bỏ dấu "+" (luôn là thưởng, không cần nhấn mạnh), thêm số ngày còn lại nếu
        /// mail có hạn. Đã đo thật cả 2 nhánh vừa khít ProgressLabel sau khi nới 90→128px +
        /// fontSize 12→10.</summary>
        private string FormatRewardLine(MailDto m)
        {
            string rewards = string.Join(" · ", m.Rewards.Select(r => $"{r.Amount} {(CurrencyType)r.CurrencyType}"));
            if (string.IsNullOrEmpty(m.ExpiresAtUtc)) return rewards;

            if (!System.DateTime.TryParse(m.ExpiresAtUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var expiry))
                return rewards;

            int daysLeft = (int)System.Math.Ceiling((expiry - System.DateTime.UtcNow).TotalDays);
            return daysLeft > 0 ? $"{rewards} · {daysLeft}d" : $"{rewards} · <1d";
        }
    }
}
