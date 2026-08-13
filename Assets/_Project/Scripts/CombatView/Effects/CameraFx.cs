using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.CombatView.Effects
{
    /// <summary>
    /// Rung máy quay + zoom crit + chớp toàn màn — plan.md §10.5 bảng Juice.
    /// Gắn tự động lên camera trận trong <see cref="CombatPresenter.Setup"/>.
    /// </summary>
    public sealed class CameraFx : MonoBehaviour
    {
        private Camera _cam;
        private Vector3 _basePos;
        private float _baseOrthoSize;
        private Image _flashImage;

        private Coroutine _shakeRoutine;
        private Coroutine _zoomRoutine;

        public bool ShakeEnabled { get; set; } = true;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _basePos = transform.localPosition;
            _baseOrthoSize = _cam.orthographic ? _cam.orthographicSize : 5f;
            BuildFlashOverlay();
        }

        /// <summary>ScreenFlashCanvas/Flash dựng sẵn trên Hierarchy (con của BattleCamera trong
        /// Battle.unity) — chỉ tạo mới nếu thiếu (an toàn khi test camera lẻ ngoài scene Battle).</summary>
        private void BuildFlashOverlay()
        {
            var canvasT = transform.Find("ScreenFlashCanvas");
            if (canvasT != null)
            {
                _flashImage = canvasT.Find("Flash").GetComponent<Image>();
                return;
            }

            var canvasGo = new GameObject("ScreenFlashCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            canvasGo.AddComponent<CanvasScaler>();

            var imgGo = new GameObject("Flash", typeof(RectTransform));
            imgGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)imgGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            _flashImage = imgGo.AddComponent<Image>();
            _flashImage.color = new Color(1, 1, 1, 0);
            _flashImage.raycastTarget = false;
        }

        // =====================================================================

        /// <summary>Rung camera — 3px damage thường, 6px Perfect, mạnh hơn khi Break.</summary>
        public void Shake(float amplitudeWorldUnits, float duration)
        {
            if (!ShakeEnabled || duration <= 0f) return;
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeRoutine(amplitudeWorldUnits, duration));
        }

        private IEnumerator ShakeRoutine(float amplitude, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float falloff = 1f - t / duration;
                transform.localPosition = _basePos + (Vector3)(Random.insideUnitCircle * amplitude * falloff);
                yield return null;
            }
            transform.localPosition = _basePos;
            _shakeRoutine = null;
        }

        /// <summary>Zoom nhẹ 1.05× rồi trả về — dùng khi Crit.</summary>
        public void ZoomPulse(float targetMultiplier, float duration)
        {
            if (!_cam.orthographic) return;
            if (_zoomRoutine != null) StopCoroutine(_zoomRoutine);
            _zoomRoutine = StartCoroutine(ZoomRoutine(targetMultiplier, duration));
        }

        private IEnumerator ZoomRoutine(float mult, float duration)
        {
            float target = _baseOrthoSize / mult; // ortho NHỎ hơn = zoom IN
            float half = duration * 0.5f;

            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                _cam.orthographicSize = Mathf.Lerp(_baseOrthoSize, target, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                _cam.orthographicSize = Mathf.Lerp(target, _baseOrthoSize, t / half);
                yield return null;
            }
            _cam.orthographicSize = _baseOrthoSize;
            _zoomRoutine = null;
        }

        /// <summary>Chớp toàn màn — vàng cho Perfect, trắng cho Break.</summary>
        public void Flash(Color color, float duration)
        {
            StartCoroutine(FlashRoutine(color, duration));
        }

        private IEnumerator FlashRoutine(Color color, float duration)
        {
            color.a = 0.55f;
            _flashImage.color = color;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                var c = color;
                c.a = Mathf.Lerp(color.a, 0f, t / duration);
                _flashImage.color = c;
                yield return null;
            }
            _flashImage.color = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
