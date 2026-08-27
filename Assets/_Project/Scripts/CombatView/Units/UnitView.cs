using Game.Combat.Model;
using Game.Data;
using UnityEngine;

namespace Game.CombatView.Units
{
    /// <summary>
    /// Đại diện một unit trên màn hình. CHỈ nghe event từ CombatPresenter —
    /// không bao giờ gọi thẳng vào CombatSimulation (object-map.md §3.3 quy tắc).
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private SpriteRenderer _sprite;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Animator _animator;

        [Header("Phản hồi")]
        [SerializeField] private float _hitFlashDuration = 0.06f;
        [SerializeField] private float _hitShakeAmount = 0.12f;
        [SerializeField] private float _deathFadeDuration = 0.5f;

        public int UnitId { get; private set; }
        public TeamSide Side { get; private set; }
        public Transform Anchor => _visualRoot != null ? _visualRoot : transform;

        private Vector3 _homePosition;
        private Color _baseColor = Color.white;
        private float _flashUntil;
        private float _shakeUntil;
        private bool _dying;
        private float _deathT;

        // =====================================================================

        public void Bind(CombatUnit unit, Sprite sprite)
        {
            UnitId = unit.Id;
            Side = unit.Side;
            name = $"Unit_{unit.Id}_{unit.DefId}";

            EnsureRefs();

            var controller = Resources.Load<RuntimeAnimatorController>(
                $"Animations/Controllers/{unit.DefId}");
            if (controller != null)
            {
                _animator.runtimeAnimatorController = controller;
                _animator.enabled = true;
                _animator.Rebind();
                _animator.Update(0f);
            }
            else
            {
                // Không có AnimatorController cho defId này — dùng lại sprite tĩnh như cũ.
                _animator.enabled = false;
                if (sprite != null) _sprite.sprite = sprite;
            }

            // Địch quay mặt sang trái để hai phe nhìn nhau
            _visualRoot.localScale = new Vector3(unit.Side == TeamSide.Enemy ? -1f : 1f, 1f, 1f);

            _baseColor = _sprite.color;
            _homePosition = transform.position;
            _dying = false;
            _deathT = 0f;
            _sprite.enabled = true;
        }

        private void EnsureRefs()
        {
            if (_visualRoot == null)
            {
                var found = transform.Find("Visual");
                if (found == null)
                {
                    var go = new GameObject("Visual");
                    go.transform.SetParent(transform, false);
                    found = go.transform;
                }
                _visualRoot = found;
            }

            if (_sprite == null)
            {
                // Lưu ý: KHÔNG dùng "??" — GetComponent<T>() trả về "fake null" của Unity
                // khi không tìm thấy component, và "??" chỉ kiểm tra null thật của CLR nên bỏ lỡ.
                var sr = _visualRoot.GetComponent<SpriteRenderer>();
                if (sr == null) sr = _visualRoot.gameObject.AddComponent<SpriteRenderer>();
                _sprite = sr;
                _sprite.sortingOrder = 10;
            }

            if (_animator == null)
            {
                var anim = _visualRoot.GetComponent<Animator>();
                if (anim == null) anim = _visualRoot.gameObject.AddComponent<Animator>();
                _animator = anim;
            }
        }

        public void SetHome(Vector3 position)
        {
            _homePosition = position;
            transform.position = position;
        }

        // =====================================================================
        // Phản hồi trực quan — bảng juice plan.md §10.5
        // =====================================================================

        /// <summary>Nhận sát thương: chớp trắng 2 frame + rung nhẹ + frame "damage" (nếu có).</summary>
        public void PlayHit()
        {
            _flashUntil = Time.time + _hitFlashDuration;
            _shakeUntil = Time.time + _hitFlashDuration * 2f;

            if (_animator != null && _animator.enabled)
                _animator.SetTrigger("Hit");
        }

        /// <summary>Né đòn: dịch ngang nhanh rồi về chỗ cũ — plan.md §10.5 "Miss / Né".</summary>
        public void PlayMiss(float distance = 0.28f, float duration = 0.12f)
        {
            StopCoroutine(nameof(MissRoutine));
            StartCoroutine(MissRoutine(distance, duration));
        }

        private System.Collections.IEnumerator MissRoutine(float distance, float duration)
        {
            float dir = Side == TeamSide.Player ? -1f : 1f; // lùi ra sau, ngược hướng lao tới
            Vector3 target = _homePosition + new Vector3(dir * distance, 0.1f, 0f);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(_homePosition, target, t / duration);
                yield return null;
            }
            t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(target, _homePosition, t / duration);
                yield return null;
            }
            transform.position = _homePosition;
        }

        /// <summary>Tiến lên đánh rồi lùi về — cho cảm giác "có va chạm".</summary>
        public void PlayAttackLunge(float distance = 0.6f, float duration = 0.18f)
        {
            StopAllCoroutines();
            StartCoroutine(LungeRoutine(distance, duration));

            if (_animator != null && _animator.enabled)
                _animator.SetTrigger("Attack");
        }

        private System.Collections.IEnumerator LungeRoutine(float distance, float duration)
        {
            float dir = Side == TeamSide.Player ? 1f : -1f;
            Vector3 target = _homePosition + new Vector3(dir * distance, 0f, 0f);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(_homePosition, target, t / duration);
                yield return null;
            }
            t = 0f;
            while (t < duration * 1.6f)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(target, _homePosition, t / (duration * 1.6f));
                yield return null;
            }
            transform.position = _homePosition;
        }

        public void PlayDeath()
        {
            _dying = true;
            _deathT = 0f;

            if (_animator != null && _animator.enabled)
                _animator.SetBool("isDead", true);
        }

        public void PlayRevive()
        {
            _dying = false;
            _deathT = 0f;
            var c = _baseColor;
            c.a = 1f;
            _sprite.color = c;
            _sprite.enabled = true;

            if (_animator != null && _animator.enabled)
                _animator.SetBool("isDead", false);
        }

        // =====================================================================

        private void Update()
        {
            if (_sprite == null) return;

            if (_dying)
            {
                _deathT += Time.deltaTime;
                float k = Mathf.Clamp01(_deathT / _deathFadeDuration);
                var c = _baseColor;
                c.a = 1f - k;
                _sprite.color = c;
                // Tan dần lên trên — thay cho dissolve shader ở bản đầy đủ
                transform.position = _homePosition + new Vector3(0f, k * 0.4f, 0f);
                if (k >= 1f) { _sprite.enabled = false; _dying = false; }
                return;
            }

            // Chớp trắng khi trúng đòn
            _sprite.color = Time.time < _flashUntil ? Color.white : _baseColor;

            // Rung
            if (Time.time < _shakeUntil)
            {
                float a = _hitShakeAmount;
                transform.position = _homePosition + new Vector3(
                    Random.Range(-a, a), Random.Range(-a, a), 0f);
            }
            else if (transform.position != _homePosition && !_dying)
            {
                transform.position = Vector3.MoveTowards(transform.position, _homePosition,
                                                         6f * Time.deltaTime);
            }
        }
    }
}
