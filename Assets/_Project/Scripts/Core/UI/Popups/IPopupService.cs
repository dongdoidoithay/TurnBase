using System;
using UnityEngine;

namespace Game.Core.UI.Popups
{
    /// <summary>Cổng duy nhất để hiện thông báo/popup toàn cục — dựng trên PopupLayer riêng
    /// (con của IUiRootHost.Root, sống xuyên suốt mọi scene), sortingOrder cao nhất nên luôn
    /// nổi trên UI màn hình hiện tại.</summary>
    public interface IPopupService
    {
        void ShowToast(string message, float duration = 2.4f);

        void ShowConfirm(string title, string message, Action onConfirm, Action onCancel = null,
                          string confirmLabel = "OK", string cancelLabel = "HỦY");

        void ShowReward(string title, string body, Sprite icon = null,
                         string continueLabel = "TIẾP TỤC", Action onContinue = null);
    }
}
