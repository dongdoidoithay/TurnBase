using Game.Core;
using Game.Core.Random;
using Game.Data.Dto;
using Game.Meta.Dungeon;
using Game.Services.Audio;
using Game.Services.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Meta
{
    /// <summary>
    /// Modal chọn lựa cho node Rest/Event — task-eventrest.md. Dựng từ UI_NodeChoice.prefab (clone
    /// UI_Shop.prefab, 3 row option thay vì 4 row hàng hoá cố định), cùng khuôn với
    /// <see cref="ShopScreen"/>. Logic rủi ro/kết quả nằm hết ở <see cref="NodeChoiceSystem"/> —
    /// class này chỉ hiển thị + gọi.
    ///
    /// 2 trạng thái hiển thị trong 1 lần <see cref="Open"/>: đang chọn (3 hoặc 2 nút option hiện,
    /// nút Continue ẩn) → đã chọn (toàn bộ option ẩn, dòng mô tả đổi thành kết quả, nút Continue
    /// hiện). KHÔNG có nút thoát trước khi chọn — đã vào Event/Rest thì phải chọn 1 lựa chọn,
    /// đúng tinh thần "rủi ro" của plan.md §8.1.
    /// </summary>
    public sealed class NodeChoiceScreen : MonoBehaviour
    {
        private const string PrefabPath = "Prefabs/UI/Screens/UI_NodeChoice";
        private const int MaxOptions = 3;

        private GameObject _root;
        private Text _titleLabel, _descriptionLabel;
        private GameObject[] _rows;
        private Text[] _rowFlavorLabels;
        private Text[] _optionLabels;
        private Button[] _optionButtons;
        private Button _continueButton;

        private IAudioService _audio;
        private IEconomyService _economy;
        private IRandomSource _rng;
        private PlayerProfileDto _profile;
        private bool _isRest;
        private System.Action _onClosed;

        public void Open(PlayerProfileDto profile, bool isRest, IEconomyService economy,
            IRandomSource rng, System.Action onClosed)
        {
            ServiceLocator.TryGet(out _audio);
            if (_root == null) BuildShell();

            _profile = profile;
            _isRest = isRest;
            _economy = economy;
            _rng = rng;
            _onClosed = onClosed;

            _root.SetActive(true);
            ShowOptions();
        }

        private void BuildShell()
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            _root = Instantiate(prefab, transform);

            var panel = _root.transform.Find("Panel");
            _titleLabel = panel.Find("Title").GetComponent<Text>();
            _descriptionLabel = panel.Find("WalletLabel").GetComponent<Text>();

            _continueButton = panel.Find("CloseButton").GetComponent<Button>();
            panel.Find("CloseButton/Label").GetComponent<Text>().text = "CONTINUE";
            _continueButton.onClick.AddListener(Continue);

            var list = panel.Find("RowListContainer");
            _rows = new GameObject[MaxOptions];
            _rowFlavorLabels = new Text[MaxOptions];
            _optionLabels = new Text[MaxOptions];
            _optionButtons = new Button[MaxOptions];
            for (int i = 0; i < MaxOptions; i++)
            {
                var row = list.Find($"Row_{i}");
                _rows[i] = row.gameObject;
                _rowFlavorLabels[i] = row.Find("NameLabel").GetComponent<Text>();
                var btn = row.Find("BuyButton");
                _optionLabels[i] = btn.Find("Label").GetComponent<Text>();
                _optionButtons[i] = btn.GetComponent<Button>();

                int index = i; // capture đúng giá trị cho closure
                _optionButtons[i].onClick.AddListener(() => ChooseOption(index));
            }
        }

        private void ShowOptions()
        {
            var options = _isRest ? NodeChoiceSystem.RestOptions : NodeChoiceSystem.EventOptions;
            _titleLabel.text = _isRest ? "REST" : "EVENT";
            _descriptionLabel.text = _isRest
                ? "Choose how to spend the night."
                : "Something catches your eye. Choose your approach.";

            for (int i = 0; i < MaxOptions; i++)
            {
                bool active = i < options.Length;
                _rows[i].SetActive(active);
                if (!active) continue;

                _rowFlavorLabels[i].text = options[i].Flavor;
                _optionLabels[i].text = options[i].Label;
                // Mốc duy nhất hiện có thể chặn TRƯỚC khi chọn: Rest option 1 "Train" khi không
                // hero nào đủ điều kiện — mọi lựa chọn khác luôn khả dụng (Gold Grant tự kẹp 0,
                // không cần check số dư trước, xem task-eventrest.md §1).
                _optionButtons[i].interactable = !(_isRest && i == 1) || NodeChoiceSystem.IsRestTrainAvailable(_profile);
            }

            _continueButton.gameObject.SetActive(false);
        }

        private void ChooseOption(int index)
        {
            var result = _isRest
                ? NodeChoiceSystem.ResolveRest(_profile, index, _economy, _rng)
                : NodeChoiceSystem.ResolveEvent(_profile, index, _economy, _rng);
            if (!result.Applied) return; // an toàn cuối — UI đã chặn interactable từ trước

            _audio?.PlaySfx("ui/sfx_ui_confirm");
            for (int i = 0; i < MaxOptions; i++) _rows[i].SetActive(false);
            _descriptionLabel.text = result.ResultText;
            _continueButton.gameObject.SetActive(true);
        }

        private void Continue()
        {
            _audio?.PlaySfx("ui/sfx_ui_tick");
            _root.SetActive(false);
            _onClosed?.Invoke();
        }
    }
}
