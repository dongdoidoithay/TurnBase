using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.CombatView.Effects
{
    /// <summary>Một số damage / chữ bay lên. Được pool, không cấp phát khi phát.</summary>
    public sealed class FloatingText : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _label;

        private float _life;
        private float _duration;
        private Vector3 _velocity;
        private Color _color;

        public bool IsActive => gameObject.activeSelf;

        public void Init()
        {
            if (_label == null)
            {
                _label = gameObject.GetComponent<TextMeshPro>()
                         ?? gameObject.AddComponent<TextMeshPro>();
                _label.alignment = TextAlignmentOptions.Center;
                _label.fontSize = 4f;
                _label.enableWordWrapping = false;
                _label.sortingOrder = 100;
            }
            gameObject.SetActive(false);
        }

        public void Show(Vector3 worldPos, string text, Color color, float scale = 1f, float duration = 0.9f)
        {
            transform.position = worldPos;
            transform.localScale = Vector3.one * scale;
            _label.text = text;
            _color = color;
            _label.color = color;
            _duration = duration;
            _life = 0f;
            // Lệch ngang nhẹ để nhiều số cùng lúc không chồng lên nhau
            _velocity = new Vector3(Random.Range(-0.5f, 0.5f), 2.4f, 0f);
            gameObject.SetActive(true);
        }

        private void Update()
        {
            _life += Time.deltaTime;
            float k = _life / _duration;

            if (k >= 1f) { gameObject.SetActive(false); return; }

            transform.position += _velocity * Time.deltaTime;
            _velocity.y -= 4.5f * Time.deltaTime;   // trọng lực nhẹ

            // Nảy to lúc đầu rồi mờ dần — đọc được ngay cả khi nhiều số cùng lúc
            float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1f, k / 0.15f) : 1f;
            transform.localScale = Vector3.one * pop * transform.localScale.x / Mathf.Max(pop, 0.001f);

            var c = _color;
            c.a = k < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.7f) / 0.3f);
            _label.color = c;
        }
    }

    /// <summary>
    /// Pool damage number — plan.md §11.8 (30 cái, grow được).
    /// Màu theo bảng vai trò ở references/palette.md.
    /// </summary>
    public sealed class FloatingTextLayer : MonoBehaviour
    {
        public static readonly Color COLOR_DAMAGE = new(0.949f, 0.910f, 0.810f);  // #F2E8CF
        public static readonly Color COLOR_CRIT   = new(0.957f, 0.635f, 0.349f);  // #F4A259
        public static readonly Color COLOR_HEAL   = new(0.482f, 0.788f, 0.314f);  // #7BC950
        public static readonly Color COLOR_MISS   = new(0.604f, 0.545f, 0.620f);  // #9A8B9E
        public static readonly Color COLOR_PERFECT= new(1.000f, 0.820f, 0.400f);  // #FFD166
        public static readonly Color COLOR_BREAK  = new(1.000f, 1.000f, 1.000f);

        private const int PREWARM = 30;
        private const float LARGE_NUMBERS_MULTIPLIER = 1.5f;

        /// <summary>task-accessibility-part2.md, plan.md §10.7 "hiển thị số damage lớn" — set bởi
        /// caller (BattleSceneInstaller) từ SettingsDto.ShowLargeDamageNumbers. Chỉ nhân thêm vào
        /// scale của <see cref="ShowDamage"/>/<see cref="ShowHeal"/> (số damage/heal thật) — KHÔNG
        /// áp cho Miss/Perfect/Break (đã là banner trạng thái to sẵn, không phải "số damage").</summary>
        public bool LargeNumbers;

        private readonly List<FloatingText> _pool = new(PREWARM);

        private void Awake()
        {
            for (int i = 0; i < PREWARM; i++) Create();
        }

        private FloatingText Create()
        {
            var go = new GameObject("FloatingText");
            go.transform.SetParent(transform, false);
            var ft = go.AddComponent<FloatingText>();
            ft.Init();
            _pool.Add(ft);
            return ft;
        }

        private FloatingText Take()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].IsActive) return _pool[i];
            return Create();   // vượt prewarm thì mở rộng, không bao giờ trả null
        }

        public void ShowDamage(Vector3 pos, int amount, bool isCrit)
            => Take().Show(pos, amount.ToString(),
                           isCrit ? COLOR_CRIT : COLOR_DAMAGE,
                           (isCrit ? 1.6f : 1f) * (LargeNumbers ? LARGE_NUMBERS_MULTIPLIER : 1f));

        public void ShowHeal(Vector3 pos, int amount)
            => Take().Show(pos, $"+{amount}", COLOR_HEAL, LargeNumbers ? LARGE_NUMBERS_MULTIPLIER : 1f);

        public void ShowMiss(Vector3 pos) => Take().Show(pos, "MISS", COLOR_MISS, 0.9f);

        public void ShowResist(Vector3 pos) => Take().Show(pos, "RESIST!", COLOR_MISS, 0.9f);

        public void ShowPerfect(Vector3 pos)
            => Take().Show(pos, "PERFECT!", COLOR_PERFECT, 1.4f, 1.1f);

        public void ShowBreak(Vector3 pos)
            => Take().Show(pos, "BREAK!", COLOR_BREAK, 2.0f, 1.3f);

        public void ShowText(Vector3 pos, string text, Color color, float scale = 1f)
            => Take().Show(pos, text, color, scale);
    }
}
