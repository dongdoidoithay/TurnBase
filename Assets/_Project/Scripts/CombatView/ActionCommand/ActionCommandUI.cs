using System;
using System.Collections;
using System.Collections.Generic;
using Game.Combat.Systems;
using Game.Core;
using Game.Data;
using Game.Services.Audio;
using Game.Services.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.CombatView.ActionCommand
{
    /// <summary>
    /// Cửa sổ bấm nhịp — plan.md §4.8. Vẽ bằng code (không cần prefab) để chạy được ngay ở P1.
    ///
    /// Chia việc rõ ràng: lớp này CHỈ đo thời gian và vẽ; việc chấm điểm nằm ở
    /// ActionCommandEvaluator (thuần C#, đã có 23 test).
    /// </summary>
    public sealed class ActionCommandUI : MonoBehaviour
    {
        private const float RING_START_SCALE = 3.2f;
        private const float RING_TARGET_SCALE = 1.0f;

        [Header("Màu (references/palette.md)")]
        [SerializeField] private Color _ringColor = new(0.957f, 0.635f, 0.349f);   // #F4A259
        [SerializeField] private Color _targetColor = new(1f, 0.820f, 0.400f);     // #FFD166
        [SerializeField] private Color _perfectColor = new(1f, 0.820f, 0.400f);
        [SerializeField] private Color _chargeGoodColor = new(0.482f, 0.788f, 0.314f);

        private RectTransform _root;
        private Image _outerRing;
        private Image _targetRing;
        private Image _chargeFill;
        private RectTransform _chargeBar;
        private readonly List<Image> _beatDots = new(5);

        private IAudioService _audio;
        private ISettingsService _settings;

        private bool _active;
        private CommandWindow _window;
        private float _openedAt;
        private Action<CommandGrade> _onResult;

        // Combo
        private int _currentBeat;
        private CommandGrade[] _beatGrades;

        // Charge
        private bool _charging;

        public bool IsActive => _active;

        /// <summary>Độ trễ nền tảng ước lượng — đo thật ở màn Settings, đây là mặc định an toàn.</summary>
        public static int PlatformLatencyMs =>
            Application.platform switch
            {
                RuntimePlatform.Android => 60,
                RuntimePlatform.IPhonePlayer => 25,
                _ => 8
            };

        private static bool IsMobile =>
            Application.platform is RuntimePlatform.Android or RuntimePlatform.IPhonePlayer;

        // =====================================================================

        private void Awake()
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _settings);
            BuildVisuals();
            Hide();
        }

        private void BuildVisuals()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("ActionCommandCanvas");
                go.transform.SetParent(transform, false);
                canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                go.AddComponent<CanvasScaler>().uiScaleMode =
                    CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }

            _root = NewRect("ActionCommandRoot", canvas.transform);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.35f);
            _root.sizeDelta = new Vector2(200, 200);

            _targetRing = NewCircle("TargetRing", _root, _targetColor, 110f);
            _outerRing = NewCircle("OuterRing", _root, _ringColor, 110f);

            // Thanh charge
            _chargeBar = NewRect("ChargeBar", _root);
            _chargeBar.sizeDelta = new Vector2(220, 22);
            _chargeBar.anchoredPosition = new Vector2(0, -90);
            var bg = _chargeBar.gameObject.AddComponent<Image>();
            bg.color = new Color(0.17f, 0.11f, 0.18f, 0.9f);

            var fill = NewRect("Fill", _chargeBar);
            fill.anchorMin = new Vector2(0, 0);
            fill.anchorMax = new Vector2(0, 1);
            fill.pivot = new Vector2(0, 0.5f);
            fill.offsetMin = new Vector2(2, 2);
            fill.offsetMax = new Vector2(2, -2);
            fill.sizeDelta = new Vector2(0, -4);
            _chargeFill = fill.gameObject.AddComponent<Image>();
            _chargeFill.color = _chargeGoodColor;

            // 5 chấm nhịp cho combo
            for (int i = 0; i < 5; i++)
            {
                var dot = NewCircle($"Beat{i}", _root, new Color(0.36f, 0.29f, 0.37f), 22f);
                dot.rectTransform.anchoredPosition = new Vector2(-88 + i * 44, 70);
                _beatDots.Add(dot);
            }
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Sprite _roundSprite;

        private static Image NewCircle(string name, Transform parent, Color color, float size)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            // Không có sprite thì Image vẽ hình vuông đặc — với RING_START_SCALE=3.2
            // sẽ thành khối đặc che gần hết màn hình thay vì một vòng tròn.
            img.sprite = RoundSprite();
            img.type = Image.Type.Simple;
            return img;
        }

        /// <summary>Sinh sprite tròn ở runtime (không phụ thuộc built-in resource path,
        /// vốn khác nhau/có thể thiếu giữa các phiên bản Unity) — dựng 1 lần, dùng chung.</summary>
        private static Sprite RoundSprite()
        {
            if (_roundSprite != null) return _roundSprite;

            const int diameter = 64;
            var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var center = new Vector2(diameter / 2f, diameter / 2f);
            float radius = diameter / 2f - 1f;
            var pixels = new Color32[diameter * diameter];
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    byte alpha = dist <= radius ? (byte)255 : (byte)0;
                    pixels[y * diameter + x] = new Color32(255, 255, 255, alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            _roundSprite = Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f));
            return _roundSprite;
        }

        // =====================================================================

        /// <summary>
        /// Mở cửa sổ nhịp. <paramref name="onResult"/> được gọi đúng một lần.
        /// Nếu người chơi tắt Action Command (Accessibility) thì trả Good ngay,
        /// KHÔNG hiện UI và KHÔNG bị phạt (plan.md §4.8.4).
        /// </summary>
        public void Open(ActionCommandType type, int beats, Action<CommandGrade> onResult)
        {
            bool disabledByPlayer = _settings != null && !_settings.Current.ActionCommandEnabled;
            if (type == ActionCommandType.None || disabledByPlayer)
            {
                onResult?.Invoke(ActionCommandEvaluator.AutoGrade);
                return;
            }

            _window = ActionCommandEvaluator.BuildWindow(type, beats, IsMobile);
            _onResult = onResult;
            _openedAt = Time.unscaledTime;
            _active = true;
            _charging = false;
            _currentBeat = 0;
            _beatGrades = type == ActionCommandType.Combo ? new CommandGrade[beats] : null;

            ShowFor(type, beats);
            StartCoroutine(WindowRoutine());
        }

        private void ShowFor(ActionCommandType type, int beats)
        {
            _root.gameObject.SetActive(true);

            bool ring = type is ActionCommandType.SingleTap or ActionCommandType.Guard;
            _outerRing.gameObject.SetActive(ring);
            _targetRing.gameObject.SetActive(ring);
            _chargeBar.gameObject.SetActive(type == ActionCommandType.Charge);

            for (int i = 0; i < _beatDots.Count; i++)
                _beatDots[i].gameObject.SetActive(type == ActionCommandType.Combo && i < beats);
        }

        public void Hide()
        {
            _active = false;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        // =====================================================================

        private IEnumerator WindowRoutine()
        {
            while (_active)
            {
                int elapsed = ElapsedMs();

                if (elapsed > _window.DurationMs)
                {
                    // Hết giờ mà chưa bấm
                    Finish(_window.Type == ActionCommandType.Combo
                        ? ActionCommandEvaluator.EvaluateCombo(_beatGrades).grade
                        : CommandGrade.Miss);
                    yield break;
                }

                UpdateVisuals(elapsed);
                HandleInput(elapsed);
                yield return null;
            }
        }

        private int ElapsedMs() => Mathf.RoundToInt((Time.unscaledTime - _openedAt) * 1000f);

        private void UpdateVisuals(int elapsed)
        {
            switch (_window.Type)
            {
                case ActionCommandType.SingleTap:
                case ActionCommandType.Guard:
                {
                    // Vòng ngoài thu về vòng đích — bấm khi trùng nhau
                    float t = Mathf.Clamp01((float)elapsed / Mathf.Max(1, _window.TargetMs));
                    float scale = Mathf.Lerp(RING_START_SCALE, RING_TARGET_SCALE, t);
                    _outerRing.rectTransform.localScale = Vector3.one * scale;

                    // Sáng lên khi vào vùng Perfect để mắt bắt kịp
                    bool inPerfect = Mathf.Abs(elapsed - _window.TargetMs) <= _window.PerfectToleranceMs;
                    _targetRing.color = inPerfect ? _perfectColor : _targetColor;
                    break;
                }

                case ActionCommandType.Charge:
                {
                    float ratio = (float)elapsed / ActionCommandEvaluator.CHARGE_DURATION_MS;
                    float w = Mathf.Clamp01(ratio) * (_chargeBar.sizeDelta.x - 4);
                    _chargeFill.rectTransform.sizeDelta = new Vector2(w, -4);
                    _chargeFill.color = ratio >= ActionCommandEvaluator.CHARGE_PERFECT_MIN
                        ? _perfectColor
                        : (ratio >= ActionCommandEvaluator.CHARGE_GOOD_MIN
                            ? _chargeGoodColor
                            : new Color(0.9f, 0.22f, 0.27f));
                    break;
                }

                case ActionCommandType.Combo:
                {
                    int expected = elapsed / ActionCommandEvaluator.COMBO_BEAT_INTERVAL_MS;
                    for (int i = 0; i < _beatDots.Count && i < _window.Beats; i++)
                    {
                        bool isNext = i == _currentBeat;
                        _beatDots[i].rectTransform.localScale =
                            Vector3.one * (isNext && i == expected ? 1.5f : 1f);
                    }
                    break;
                }
            }
        }

        private void HandleInput(int elapsed)
        {
            bool down = Pressed();
            bool up = Released();

            switch (_window.Type)
            {
                case ActionCommandType.SingleTap:
                case ActionCommandType.Guard:
                    if (down) Finish(Grade(elapsed));
                    break;

                case ActionCommandType.Charge:
                    if (down) _charging = true;
                    if (_charging && up)
                    {
                        float ratio = (float)elapsed / ActionCommandEvaluator.CHARGE_DURATION_MS;
                        Finish(ActionCommandEvaluator.EvaluateCharge(ratio));
                    }
                    break;

                case ActionCommandType.Combo:
                    if (!down) break;
                    {
                        int beatTarget = (_currentBeat + 1) * ActionCommandEvaluator.COMBO_BEAT_INTERVAL_MS;
                        int delta = Mathf.Abs(elapsed - beatTarget);
                        var g = delta <= _window.PerfectToleranceMs ? CommandGrade.Perfect
                              : delta <= _window.GoodToleranceMs ? CommandGrade.Good
                              : CommandGrade.Miss;

                        _beatGrades[_currentBeat] = g;
                        FlashBeat(_currentBeat, g);
                        _currentBeat++;

                        if (_currentBeat >= _window.Beats)
                            Finish(ActionCommandEvaluator.EvaluateCombo(_beatGrades).grade);
                    }
                    break;
            }
        }

        private CommandGrade Grade(int rawElapsed)
        {
            int userOffset = _settings?.Current.ActionCommandOffsetMs ?? 0;
            int effective = ActionCommandEvaluator.ApplyLatencyCompensation(
                rawElapsed, PlatformLatencyMs, userOffset);
            return ActionCommandEvaluator.Evaluate(_window, effective);
        }

        private void FlashBeat(int index, CommandGrade grade)
        {
            if (index < 0 || index >= _beatDots.Count) return;
            _beatDots[index].color = grade switch
            {
                CommandGrade.Perfect => _perfectColor,
                CommandGrade.Good => _chargeGoodColor,
                _ => new Color(0.9f, 0.22f, 0.27f)
            };
        }

        private void Finish(CommandGrade grade)
        {
            _active = false;
            Hide();

            _audio?.PlaySfx(grade switch
            {
                CommandGrade.Perfect => "battle/sfx_battle_perfect",
                CommandGrade.Good => "battle/sfx_battle_good",
                _ => "battle/sfx_battle_miss"
            });

            var cb = _onResult;
            _onResult = null;
            cb?.Invoke(grade);
        }

        // Input tối giản để chạy được ở P1 — P2 nối qua IInputService.
        // Dùng Input System package (Keyboard/Mouse/Touchscreen.current), KHÔNG dùng
        // UnityEngine.Input — Player Settings đã chốt Active Input Handling = Input System
        // package, UnityEngine.Input ném InvalidOperationException mỗi frame, làm coroutine
        // WindowRoutine chết ngay lập tức và treo cửa sổ Action Command vĩnh viễn (soft-lock trận).
        private static bool Pressed()
            => (Keyboard.current?.spaceKey.wasPressedThisFrame ?? false)
               || (Mouse.current?.leftButton.wasPressedThisFrame ?? false)
               || (Touchscreen.current?.primaryTouch.press.wasPressedThisFrame ?? false);

        private static bool Released()
            => (Keyboard.current?.spaceKey.wasReleasedThisFrame ?? false)
               || (Mouse.current?.leftButton.wasReleasedThisFrame ?? false)
               || (Touchscreen.current?.primaryTouch.press.wasReleasedThisFrame ?? false);
    }
}
