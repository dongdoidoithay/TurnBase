using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI.Popups
{
    /// <summary>Popup xác nhận modal (OK/Cancel) — chrome vàng/đỏ mận theo
    /// Art_Sample/Screen_combat.jpg. Dim overlay chặn click xuyên qua màn phía sau.</summary>
    public sealed class ConfirmDialogView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _message;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TextMeshProUGUI _confirmLabel;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TextMeshProUGUI _cancelLabel;

        public void Show(string title, string message, string confirmLabel, string cancelLabel,
                          Action onConfirm, Action onCancel)
        {
            _title.text = title;
            _message.text = message;
            _confirmLabel.text = confirmLabel;
            _cancelLabel.text = cancelLabel;

            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.AddListener(() =>
            {
                onConfirm?.Invoke();
                Destroy(gameObject);
            });

            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(() =>
            {
                onCancel?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}
