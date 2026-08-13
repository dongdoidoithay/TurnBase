using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.CombatView.Tutorial
{
    /// <summary>
    /// Banner chỉ dẫn 5 bước tutorial + nút "Bỏ qua" — task-phase-5-gaps.md Phần B. GameObject
    /// tĩnh trong Battle.unity (sibling <c>BattleHud</c>/<c>ActionCommandUI</c> dưới
    /// <c>BattleSceneInstaller/__UI__</c>, cùng mẫu Canvas+CanvasScaler+GraphicRaycaster dựng sẵn
    /// trên Hierarchy — KHÔNG tự tạo Canvas bằng code). Đặt ở giữa màn (không đè HeroPanel/
    /// EnemyPanel/TurnOrderBar/SkillGrid — đo toạ độ thật của <c>BattleHudScreen</c> trước khi
    /// chọn vị trí, xem task file §B4). Dim nền KHÔNG raycast (không chặn thao tác skill/Action
    /// Command bên dưới) — chỉ banner + nút Skip nhận input.
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour
    {
        private static readonly Color PANEL_BG = new(0.114f, 0.078f, 0.129f, 0.94f);
        private static readonly Color BORDER = new(0.957f, 0.635f, 0.349f);
        private static readonly Color TEXT = new(0.949f, 0.910f, 0.810f);

        public event Action OnSkipPressed;

        private RectTransform _banner;
        private TextMeshProUGUI _label;
        private bool _built;

        private static readonly System.Collections.Generic.Dictionary<TutorialStep, string> STEP_TEXT = new()
        {
            { TutorialStep.ChooseSkill, "Chọn 1 kỹ năng trong Skill Grid để bắt đầu." },
            { TutorialStep.ActionCommand, "Bấm đúng nhịp trong cửa sổ Action Command để tăng sát thương." },
            { TutorialStep.Counter, "Đánh trúng hệ khắc chế (element) gây thêm sát thương." },
            { TutorialStep.Break, "Hạ Poise của địch về 0 để gây Break — mất lượt phòng thủ." },
            { TutorialStep.Ultimate, "Dùng Ultimate khi thanh năng lượng đầy để tung đòn mạnh nhất." },
        };

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 300; // trên BattleHud(100)/ActionCommandUI(200)
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(960, 540);
                scaler.matchWidthOrHeight = 0.5f;
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // Dim nền toàn màn — chỉ trang trí, KHÔNG raycast (game vẫn chơi được bình thường
            // trong lúc overlay hiện, đúng tinh thần "dạy trong trận thật" của §B).
            var dimGo = new GameObject("Dim", typeof(RectTransform));
            dimGo.transform.SetParent(transform, false);
            var dimRt = (RectTransform)dimGo.transform;
            dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.12f);
            dimImg.raycastTarget = false;

            // Banner giữa màn, giữa khoảng trống HeroPanel(x:12-260)/EnemyPanel(x:722-948) và dưới
            // TurnOrderBar(y-top:12-42), trên SkillGrid(y-bottom:58-240) — xem task file §B4 cho
            // phép tính toạ độ đầy đủ.
            _banner = new GameObject("Banner", typeof(RectTransform)).GetComponent<RectTransform>();
            _banner.SetParent(transform, false);
            _banner.anchorMin = _banner.anchorMax = new Vector2(0.5f, 0.5f);
            _banner.pivot = new Vector2(0.5f, 0.5f);
            _banner.anchoredPosition = new Vector2(0, 35);
            // 400×90 tại (0,+35) canvas-center: x:[280,680] (HeroPanel phải=260 / EnemyPanel trái=722
            // — dư ≥20px 2 bên), y-từ-đáy:[260,350] (SkillGrid đỉnh=240 / HeroPanel đáy=370 — dư
            // 10-20px). Xem phép tính đầy đủ ở task-phase-5-gaps.md §B4.
            _banner.sizeDelta = new Vector2(400, 90);

            var bannerImg = _banner.gameObject.AddComponent<Image>();
            bannerImg.color = PANEL_BG;

            var borderGo = new GameObject("Border", typeof(RectTransform));
            borderGo.transform.SetParent(_banner, false);
            var borderRt = (RectTransform)borderGo.transform;
            borderRt.anchorMin = Vector2.zero; borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-2, -2); borderRt.offsetMax = new Vector2(2, 2);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = BORDER;
            borderGo.transform.SetAsFirstSibling();

            _label = NewLabel(_banner, "", 14, TextAlignmentOptions.TopLeft,
                              new Vector2(14, -10), new Vector2(340, 70));

            var skipGo = new GameObject("SkipButton", typeof(RectTransform));
            skipGo.transform.SetParent(_banner, false);
            var skipRt = (RectTransform)skipGo.transform;
            skipRt.anchorMin = skipRt.anchorMax = new Vector2(1, 0.5f);
            skipRt.pivot = new Vector2(1, 0.5f);
            skipRt.anchoredPosition = new Vector2(-10, 0);
            skipRt.sizeDelta = new Vector2(64, 28);
            var skipImg = skipGo.AddComponent<Image>();
            skipImg.color = new Color(0.42f, 0.32f, 0.06f, 0.95f);
            var skipBtn = skipGo.AddComponent<Button>();
            skipBtn.targetGraphic = skipImg;
            skipBtn.onClick.AddListener(() => OnSkipPressed?.Invoke());
            NewLabel(skipRt, "SKIP", 12, TextAlignmentOptions.Center, Vector2.zero, new Vector2(64, 28));

            gameObject.SetActive(false);
        }

        public void Show(TutorialStep step)
        {
            EnsureBuilt();
            if (!STEP_TEXT.TryGetValue(step, out var text)) { Hide(); return; }
            gameObject.SetActive(true);
            _label.text = text;
        }

        public void Hide()
        {
            if (!_built) return;
            gameObject.SetActive(false);
        }

        private static TextMeshProUGUI NewLabel(Transform parent, string text, int size,
                                                  TextAlignmentOptions align, Vector2 pos, Vector2 dim)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = dim;

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = TEXT;
            t.raycastTarget = false;
            return t;
        }
    }
}
