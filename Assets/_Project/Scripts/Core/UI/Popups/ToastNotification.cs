using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.Core.UI.Popups
{
    /// <summary>Thông báo nhỏ tự ẩn — góc màn hình, không chặn tương tác (task UI mới, chrome
    /// vàng/đỏ mận theo Art_Sample/Screen_combat.jpg).</summary>
    public sealed class ToastNotification : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TextMeshProUGUI _label;

        private const float FADE_IN = 0.15f;
        private const float FADE_OUT = 0.35f;

        public void Show(string message, float duration)
        {
            _label.text = message;
            StopAllCoroutines();
            StartCoroutine(PlayRoutine(duration));
        }

        private IEnumerator PlayRoutine(float duration)
        {
            _group.alpha = 0f;
            float t = 0f;
            while (t < FADE_IN)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Clamp01(t / FADE_IN);
                yield return null;
            }
            _group.alpha = 1f;

            yield return new WaitForSecondsRealtime(duration);

            t = 0f;
            while (t < FADE_OUT)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = 1f - Mathf.Clamp01(t / FADE_OUT);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
