using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI.Popups
{
    /// <summary>Popup kết quả/phần thưởng lớn (lên cấp, mở rương, gacha...) — chrome vàng/đỏ mận
    /// theo Art_Sample/Screen_combat.jpg. Dim overlay chặn click xuyên qua màn phía sau.</summary>
    public sealed class RewardPopupView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _body;
        [SerializeField] private Image _icon;
        [SerializeField] private Button _continueButton;
        [SerializeField] private TextMeshProUGUI _continueLabel;

        public void Show(string title, string body, Sprite icon, string continueLabel, Action onContinue)
        {
            _title.text = title;
            _body.text = body;
            _continueLabel.text = continueLabel;

            if (_icon != null)
            {
                _icon.enabled = icon != null;
                _icon.sprite = icon;
            }

            _continueButton.onClick.RemoveAllListeners();
            _continueButton.onClick.AddListener(() =>
            {
                onContinue?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}
