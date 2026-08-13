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

        // ---- Animation frame-sequence (pilot: chỉ hero có bộ frame mới dùng; các unit khác
        // giữ nguyên hành vi sprite tĩnh cũ — xem task-animation-pilot.md §1/§4) ----
        private enum AnimState { StaticSprite, Idle, Attack, Move, Damage, Die }
        private const float IDLE_FPS = 8f;    // khớp bảng plan.md §2.2
        private const float ATTACK_FPS = 14f;
        private const float MOVE_FPS = 12f;
        private const float DAMAGE_FPS = 16f;
        private const float DIE_FPS = 10f;
        private Sprite[] _idleFrames;
        private Sprite[] _attackFrames;
        private Sprite[] _moveFrames;   // nạp sẵn — CHƯA có điểm trigger gameplay thật (không có
                                         // "đi bộ" trong trận lượt), dành cho lần dùng sau
        private Sprite[] _damageFrames;
        private Sprite[] _dieFrames;
        private AnimState _animState = AnimState.StaticSprite;
        private int _frameIndex;
        private float _frameTimer;

        // =====================================================================

        public void Bind(CombatUnit unit, Sprite sprite)
        {
            UnitId = unit.Id;
            Side = unit.Side;
            name = $"Unit_{unit.Id}_{unit.DefId}";

            EnsureRefs();

            _idleFrames = LoadFrames(unit.DefId, "idle");
            _attackFrames = LoadFrames(unit.DefId, "attack");
            _moveFrames = LoadFrames(unit.DefId, "move");
            _damageFrames = LoadFrames(unit.DefId, "damage");
            _dieFrames = LoadFrames(unit.DefId, "die");
            _frameIndex = 0;
            _frameTimer = 0f;

            if (_idleFrames != null)
            {
                _animState = AnimState.Idle;
                _sprite.sprite = _idleFrames[0];
            }
            else
            {
                _animState = AnimState.StaticSprite;
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

        /// <summary>Nạp bộ frame Animations/{defId}_{state}_00.. nếu có — thử cả Heroes lẫn Enemies.
        /// Không có thì trả null, unit dùng lại sprite tĩnh như cũ (fallback bắt buộc — chỉ
        /// hero_ember_knight có bộ frame trong pilot này).</summary>
        private static Sprite[] LoadFrames(string defId, string state)
        {
            foreach (var kind in new[] { "Heroes", "Enemies" })
            {
                var list = new System.Collections.Generic.List<Sprite>(4);
                for (int i = 0; i < 8; i++)
                {
                    var s = Resources.Load<Sprite>(
                        $"Art/Characters/{kind}/{defId}/Animations/{defId}_{state}_{i:00}");
                    if (s == null) break;
                    list.Add(s);
                }
                if (list.Count > 0) return list.ToArray();
            }
            return null;
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
        }

        public void SetHome(Vector3 position)
        {
            _homePosition = position;
            transform.position = position;
        }

        // =====================================================================
        // Phản hồi trực quan — bảng juice plan.md §10.5
        // =====================================================================

        /// <summary>Nhận sát thương: chớp trắng 2 frame + rung nhẹ (+ frame "damage" nếu có).</summary>
        public void PlayHit()
        {
            _flashUntil = Time.time + _hitFlashDuration;
            _shakeUntil = Time.time + _hitFlashDuration * 2f;

            if (_damageFrames != null && _animState != AnimState.StaticSprite)
            {
                _animState = AnimState.Damage;
                _frameIndex = 0;
                _frameTimer = 0f;
            }
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

            if (_attackFrames != null)
            {
                _animState = AnimState.Attack;
                _frameIndex = 0;
                _frameTimer = 0f;
            }
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

            if (_dieFrames != null && _animState != AnimState.StaticSprite)
            {
                _animState = AnimState.Die;
                _frameIndex = 0;
                _frameTimer = 0f;
            }
        }

        public void PlayRevive()
        {
            _dying = false;
            _deathT = 0f;
            var c = _baseColor;
            c.a = 1f;
            _sprite.color = c;
            _sprite.enabled = true;
        }

        // =====================================================================

        private void Update()
        {
            if (_sprite == null) return;

            if (_dying)
            {
                AdvanceAnimFrame(); // chạy frame "die" (nếu có) song song với fade — giữ nguyên fade
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

            AdvanceAnimFrame();

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

        private Sprite[] FramesFor(AnimState s) => s switch
        {
            AnimState.Idle => _idleFrames,
            AnimState.Attack => _attackFrames,
            AnimState.Move => _moveFrames,
            AnimState.Damage => _damageFrames,
            AnimState.Die => _dieFrames,
            _ => null,
        };

        private float FpsFor(AnimState s) => s switch
        {
            AnimState.Attack => ATTACK_FPS,
            AnimState.Move => MOVE_FPS,
            AnimState.Damage => DAMAGE_FPS,
            AnimState.Die => DIE_FPS,
            _ => IDLE_FPS,
        };

        private static bool Loops(AnimState s) => s == AnimState.Idle || s == AnimState.Move;

        /// <summary>Đổi sprite theo mảng frame của state hiện tại. Idle/Move lặp; Attack/Damage chạy
        /// 1 lần rồi tự về Idle; Die chạy 1 lần rồi GIỮ NGUYÊN frame cuối (plan.md §2.2 "down" —
        /// không quay về Idle vì unit đã chết).</summary>
        private void AdvanceAnimFrame()
        {
            if (_animState == AnimState.StaticSprite) return;

            Sprite[] frames = FramesFor(_animState);
            if (frames == null || frames.Length == 0) return;

            float frameDuration = 1f / FpsFor(_animState);
            _frameTimer += Time.deltaTime;
            while (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _frameIndex++;
                if (_frameIndex >= frames.Length)
                {
                    if (_animState == AnimState.Die)
                    {
                        _frameIndex = frames.Length - 1; // giữ frame cuối, không loop/không về Idle
                        break;
                    }
                    if (Loops(_animState))
                    {
                        _frameIndex = 0;
                    }
                    else
                    {
                        _animState = AnimState.Idle;
                        frames = _idleFrames;
                        if (frames == null || frames.Length == 0) return;
                        _frameIndex = 0;
                    }
                }
            }
            _sprite.sprite = frames[_frameIndex];
        }
    }
}
