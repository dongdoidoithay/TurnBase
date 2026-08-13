using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.CombatView.Effects
{
    /// <summary>
    /// Phát VFX 4-frame tại vị trí unit — object-map.md §3.3 GO-BTL-VFX.
    /// Sprite nạp qua Resources (chưa có Addressables, xem plan.md ghi chú P2/P3).
    /// Pool object để 0 GC/frame trong trận (plan.md §11.9).
    /// </summary>
    public sealed class VfxPlayer : MonoBehaviour
    {
        private const float FRAME_TIME = 0.07f; // 4 frame ≈ 280ms ở tốc độ ×1
        private const int POOL_SIZE = 8;

        // Lớp "juice" mượt phủ NGOÀI frame pixel-art rời rạc — không đụng/blur pixel gốc, chỉ
        // nội suy scale/alpha mỗi Update() giữa các lần đổi sprite (task-animation-pilot.md §3
        // đã kết luận: bloom/juice hậu kỳ phải tách lớp riêng, không bake vào sprite).
        private const float POP_IN_FRACTION = 0.35f; // % thời lượng dành cho scale "nảy" vào
        private const float POP_START_SCALE = 0.55f; // scale ban đầu trước khi nảy tới 1x
        private const float FADE_OUT_START = 0.65f;  // % thời lượng bắt đầu fade alpha ra

        /// <summary>key hiển thị → (thư mục Resources, tiền tố file). Một vài element dùng
        /// chung VFX vì bộ 10 VFX gốc không phủ hết 7 nguyên tố (xem art_catalog.json).</summary>
        private static readonly Dictionary<string, (string folder, string prefix)> KEYS = new()
        {
            ["fire"] = ("vfx_fire_burst", "vfx_fire_burst"),
            ["ice"] = ("vfx_ice_shatter", "vfx_ice_shatter"),
            ["earth"] = ("vfx_earth_spike", "vfx_earth_spike"),
            ["lightning"] = ("vfx_lightning", "vfx_lightning"),
            ["dark"] = ("vfx_dark", "vfx_dark_void"),
            ["slash"] = ("vfx_slash_arc", "vfx_slash_arc"),
            ["break"] = ("vfx_break_shatter", "vfx_break_shatter"),
            ["heal"] = ("vfx_heal_sparkle", "vfx_heal_sparkle"),
            ["shield"] = ("vfx_shield_barrier", "vfx_shield_barrier"),
            ["poison"] = ("vfx_poison_cloud", "vfx_poison_cloud"),
            ["inferno_bulwark"] = ("vfx_inferno_bulwark", "vfx_inferno_bulwark"),
            // Phase 5 — 3 VFX mới (task-animation-pilot.md Giai đoạn 5)
            ["wind"] = ("vfx_wind_gust", "vfx_wind_gust"),
            ["light"] = ("vfx_light_radiant", "vfx_light_radiant"),
            ["magic"] = ("vfx_magic_bolt", "vfx_magic_bolt"),
        };

        /// <summary>key → đường dẫn Resources của material HDR riêng cho key đó (Bloom — xem
        /// task-animation-pilot.md §3.1/§4 Giai đoạn 2). Key không có trong bảng này dùng
        /// `_defaultMaterial` (material mặc định của SpriteRenderer, không đổi hành vi 10 VFX cũ).</summary>
        private static readonly Dictionary<string, string> MATERIAL_OVERRIDES = new()
        {
            ["inferno_bulwark"] = "Art/VFX/Mat_HDREmissiveSprite",
        };

        private static readonly Dictionary<string, Sprite[]> _cache = new();
        private static readonly Dictionary<string, Material> _materialCache = new();

        private readonly Queue<SpriteRenderer> _pool = new();
        private Material _defaultMaterial;

        private void Awake()
        {
            for (int i = 0; i < POOL_SIZE; i++)
            {
                var go = new GameObject("VfxSlot");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 20;
                if (_defaultMaterial == null) _defaultMaterial = sr.sharedMaterial;
                go.SetActive(false);
                _pool.Enqueue(sr);
            }
        }

        /// <summary>Ánh xạ Element → key VFX — dùng khi trúng đòn theo nguyên tố.</summary>
        public static string KeyForElement(Data.Element element) => element switch
        {
            Data.Element.Fire => "fire",
            Data.Element.Water => "ice",
            Data.Element.Earth => "earth",
            Data.Element.Wind => "wind",
            Data.Element.Light => "light",
            Data.Element.Dark => "dark",
            _ => "slash"
        };

        public void Play(string key, Vector3 worldPos, float scale = 1f)
        {
            var frames = LoadFrames(key);
            if (frames == null || frames.Length == 0) return;

            var sr = _pool.Count > 0 ? _pool.Dequeue() : null;
            if (sr == null) return; // hết pool — bỏ qua thay vì cấp phát thêm giữa trận

            sr.sharedMaterial = ResolveMaterial(key);
            sr.transform.position = worldPos;
            sr.transform.rotation = Quaternion.identity;
            sr.transform.localScale = Vector3.one * scale * POP_START_SCALE;
            sr.color = Color.white;
            sr.gameObject.SetActive(true);
            StartCoroutine(PlayRoutine(sr, frames, scale));
        }

        /// <summary>Material HDR riêng cho key nếu có đăng ký trong `MATERIAL_OVERRIDES`, không thì
        /// trả `_defaultMaterial` — 10 VFX cũ không đổi hành vi.</summary>
        private Material ResolveMaterial(string key)
        {
            if (!MATERIAL_OVERRIDES.TryGetValue(key, out var path)) return _defaultMaterial;
            if (_materialCache.TryGetValue(key, out var cached))
                return cached != null ? cached : _defaultMaterial;
            var mat = Resources.Load<Material>(path);
            _materialCache[key] = mat;
            return mat != null ? mat : _defaultMaterial;
        }

        /// <summary>Đổi frame theo cadence pixel-art rời rạc (giữ nguyên FRAME_TIME) nhưng nội
        /// suy scale (nảy vào) + alpha (mờ ra) mỗi Update() — mượt hơn hẳn bản cũ (chỉ SetActive
        /// bật/tắt cứng) mà không làm nhoè sprite gốc.</summary>
        private IEnumerator PlayRoutine(SpriteRenderer sr, Sprite[] frames, float targetScale)
        {
            float total = frames.Length * FRAME_TIME;
            float elapsed = 0f;
            int shownFrame = -1;

            while (elapsed < total)
            {
                float t = elapsed / total;
                int idx = Mathf.Min(frames.Length - 1, (int)(elapsed / FRAME_TIME));
                if (idx != shownFrame)
                {
                    shownFrame = idx;
                    sr.sprite = frames[idx];
                }

                sr.transform.localScale = Vector3.one * targetScale * PopScale(t);
                var c = sr.color;
                c.a = FadeAlpha(t);
                sr.color = c;

                elapsed += Time.deltaTime;
                yield return null;
            }

            sr.gameObject.SetActive(false);
            sr.sprite = null;
            sr.color = Color.white;
            _pool.Enqueue(sr);
        }

        /// <summary>Ease-out-back: nảy vượt nhẹ qua 1x rồi ổn định — chỉ trong POP_IN_FRACTION đầu.</summary>
        private static float PopScale(float t)
        {
            if (t >= POP_IN_FRACTION) return 1f;
            float u = t / POP_IN_FRACTION;
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float eased = 1f + c3 * Mathf.Pow(u - 1f, 3) + c1 * Mathf.Pow(u - 1f, 2);
            return Mathf.LerpUnclamped(POP_START_SCALE, 1f, eased);
        }

        /// <summary>Giữ alpha=1 phần lớn thời lượng, mờ dần (ease-in) ở đoạn cuối thay vì cắt cứng.</summary>
        private static float FadeAlpha(float t)
        {
            if (t < FADE_OUT_START) return 1f;
            float u = (t - FADE_OUT_START) / (1f - FADE_OUT_START);
            return 1f - u * u;
        }

        private static Sprite[] LoadFrames(string key)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;
            if (!KEYS.TryGetValue(key, out var info)) return null;

            var list = new List<Sprite>(4);
            for (int i = 0; i < 4; i++)
            {
                var s = Resources.Load<Sprite>($"Art/VFX/{info.folder}/{info.prefix}_{i:00}");
                if (s != null) list.Add(s);
            }

            var arr = list.ToArray();
            _cache[key] = arr;
            return arr;
        }
    }
}
