using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI.Popups
{
    /// <summary>Dựng 1 Canvas "PopupLayer" con của IUiRootHost.Root (DontDestroyOnLoad, sortingOrder
    /// cao nhất) rồi Instantiate prefab Toast/ConfirmDialog/RewardPopup (Resources/UI/Popups/*)
    /// lên trên đó theo yêu cầu — không màn hình nào tự dựng popup riêng nữa.</summary>
    public sealed class PopupService : MonoBehaviour, IPopupService
    {
        private const int SORTING_ORDER = 500;

        private GameObject _toastPrefab;
        private GameObject _confirmPrefab;
        private GameObject _rewardPrefab;

        public static PopupService Create(Transform parent)
        {
            var go = new GameObject("PopupLayer");
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 0.5f;
            canvas.sortingOrder = SORTING_ORDER;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            return go.AddComponent<PopupService>();
        }

        private void Awake()
        {
            _toastPrefab = Resources.Load<GameObject>("Prefabs/UI/Popups/Toast");
            _confirmPrefab = Resources.Load<GameObject>("Prefabs/UI/Popups/ConfirmDialog");
            _rewardPrefab = Resources.Load<GameObject>("Prefabs/UI/Popups/RewardPopup");
        }

        public void ShowToast(string message, float duration = 2.4f)
        {
            if (_toastPrefab == null) { Debug.LogWarning("[Popup] Thiếu prefab Toast."); return; }
            var go = Instantiate(_toastPrefab, transform);
            go.GetComponent<ToastNotification>().Show(message, duration);
        }

        public void ShowConfirm(string title, string message, Action onConfirm, Action onCancel = null,
                                 string confirmLabel = "OK", string cancelLabel = "HỦY")
        {
            if (_confirmPrefab == null) { Debug.LogWarning("[Popup] Thiếu prefab ConfirmDialog."); return; }
            var go = Instantiate(_confirmPrefab, transform);
            go.GetComponent<ConfirmDialogView>().Show(title, message, confirmLabel, cancelLabel, onConfirm, onCancel);
        }

        public void ShowReward(string title, string body, Sprite icon = null,
                                string continueLabel = "TIẾP TỤC", Action onContinue = null)
        {
            if (_rewardPrefab == null) { Debug.LogWarning("[Popup] Thiếu prefab RewardPopup."); return; }
            var go = Instantiate(_rewardPrefab, transform);
            go.GetComponent<RewardPopupView>().Show(title, body, icon, continueLabel, onContinue);
        }
    }
}
