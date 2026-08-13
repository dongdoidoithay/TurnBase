using TMPro;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.UI
{
    /// <summary>
    /// roadmap.md rủi ro R6 ("kiểm tra font pixel có đủ dấu tiếng Việt — làm ngay, không hoãn") —
    /// lần đầu kiểm THẬT phát hiện atlas TMP mặc định (<c>LiberationSans SDF</c>, dùng ở
    /// <c>Game.UI</c>/<c>Game.CombatView</c> — <c>BattleHudScreen</c>/<c>ActionCommandUI</c>/
    /// <c>TutorialOverlay</c>) thiếu 54/73 ký tự dấu tiếng Việt trong atlas STATIC gốc (mọi màn
    /// <c>Game.Meta</c> dùng <c>UnityEngine.UI.Text</c>/<c>LegacyRuntime.ttf</c> — font dynamic hệ
    /// điều hành, KHÔNG có vấn đề này). Sửa bằng cách gán <c>sourceFontFile</c> + chuyển
    /// <c>atlasPopulationMode</c> sang <c>Dynamic</c> (tự bake glyph khi cần, không giới hạn bộ ký
    /// tự cố định lúc tạo) — test này là lưới an toàn chống hồi quy nếu ai đó sau này tạo lại font
    /// asset qua Font Asset Creator (mặc định Static) mà không để ý.
    ///
    /// LƯU Ý QUAN TRỌNG (phát hiện lúc viết test này — đừng lặp lại nhầm lẫn):
    /// <c>TMP_FontAsset.HasCharacter(c, tryAddCharacter: true)</c> KHÔNG đáng tin để kiểm tra
    /// Dynamic mode hoạt động — gọi ngoài 1 lượt render TMP_Text thật (không qua
    /// <c>ForceMeshUpdate()</c>) thường trả `false` dù font THẬT SỰ render đúng, và kết quả phụ
    /// thuộc THỨ TỰ chạy test trước đó trong cùng phiên Editor (baked glyph từ test khác chạy
    /// trước "rò" sang test sau, che giấu lỗi thật khi chạy 1 mình nhưng lộ ra khi chạy cả bộ theo
    /// thứ tự khác). Cách kiểm ĐÚNG duy nhất: dựng <c>TMP_Text</c> thật, gọi
    /// <c>ForceMeshUpdate()</c>, đọc <c>characterInfo[i].textElement</c> (null = tofu) — xem
    /// <see cref="RenderingVietnameseText_ProducesNoMissingGlyphs"/>.
    /// </summary>
    public class VietnameseFontTests
    {
        private const string FontResourcePath = "Fonts & Materials/LiberationSans SDF";

        // Đủ đại diện 5 dấu thanh (sắc/huyền/hỏi/ngã/nặng) trên nguyên âm đơn/đôi + đ/Đ — không
        // lặp lại toàn bộ 73 ký tự đã kiểm tay, chỉ đủ để bắt hồi quy nếu atlas bị tạo lại từ đầu.
        private static readonly char[] SAMPLE_CHARS =
        {
            'ă', 'ắ', 'ằ', 'ẳ', 'ẵ', 'ặ',
            'â', 'ấ', 'ầ', 'ẩ', 'ẫ', 'ậ',
            'ê', 'ế', 'ề', 'ể', 'ễ', 'ệ',
            'ô', 'ố', 'ồ', 'ổ', 'ỗ', 'ộ',
            'ơ', 'ớ', 'ờ', 'ở', 'ỡ', 'ợ',
            'ư', 'ứ', 'ừ', 'ử', 'ữ', 'ự',
            'đ', 'Đ',
            'ỉ', 'ị', 'ỏ', 'ọ', 'ủ', 'ụ', 'ỹ', 'ỵ',
        };

        private static TMP_FontAsset LoadFont()
        {
            var font = Resources.Load<TMP_FontAsset>(FontResourcePath);
            Assert.IsNotNull(font, $"Không tìm thấy TMP font asset tại Resources/{FontResourcePath}");
            return font;
        }

        [Test]
        public void DefaultFont_AtlasPopulationMode_IsDynamic()
        {
            // Static + bộ ký tự cố định lúc tạo là nguồn gốc lỗi thiếu dấu — Dynamic tự bake theo
            // nhu cầu runtime, không giới hạn bộ ký tự đoán trước.
            Assert.AreEqual(AtlasPopulationMode.Dynamic, LoadFont().atlasPopulationMode);
        }

        [Test]
        public void DefaultFont_HasSourceFontFile_ForDynamicGlyphGeneration()
        {
            // Dynamic mode vô nghĩa nếu sourceFontFile null — không có gì để bake glyph mới từ đó.
            Assert.IsNotNull(LoadFont().sourceFontFile);
        }

        [TestCaseSource(nameof(SAMPLE_CHARS))]
        public void DefaultFont_RendersVietnameseDiacriticCharacter_NoTofu(char c)
        {
            // Mỗi test case tự dựng+render+dọn RIÊNG (không dùng chung state với test khác) —
            // tránh đúng cái bẫy order-dependency đã ghi ở doc-comment class.
            var canvasGo = new GameObject("VietnameseFontCharTestCanvas");
            var textGo = new GameObject("VietnameseFontCharTestText");
            try
            {
                canvasGo.AddComponent<Canvas>();
                textGo.transform.SetParent(canvasGo.transform, false);
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.font = LoadFont();
                tmp.text = c.ToString();
                tmp.ForceMeshUpdate();

                Assert.IsTrue(tmp.textInfo.characterCount > 0, "Không có ký tự nào được layout");
                Assert.IsNotNull(tmp.textInfo.characterInfo[0].textElement,
                    $"Ký tự '{c}' (U+{(int)c:X4}) không có glyph khi render thật (tofu)");
            }
            finally
            {
                Object.DestroyImmediate(textGo);
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void RenderingVietnameseText_ProducesNoMissingGlyphs()
        {
            // Kiểm gần thật nhất có thể trong EditMode: dựng TMP_Text thật, ép layout, đọc lại
            // characterInfo — characterInfo.textElement null nghĩa là glyph không tồn tại (tofu).
            var canvasGo = new GameObject("VietnameseFontTestCanvas");
            var textGo = new GameObject("VietnameseFontTestText");
            try
            {
                canvasGo.AddComponent<Canvas>();
                textGo.transform.SetParent(canvasGo.transform, false);
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.font = LoadFont();
                tmp.text = new string(SAMPLE_CHARS);
                tmp.ForceMeshUpdate();

                var info = tmp.textInfo;
                for (int i = 0; i < info.characterCount; i++)
                {
                    var ci = info.characterInfo[i];
                    Assert.IsNotNull(ci.textElement,
                        $"Ký tự '{ci.character}' (vị trí {i}) không có glyph khi render thật (tofu)");
                }
            }
            finally
            {
                Object.DestroyImmediate(textGo);
                Object.DestroyImmediate(canvasGo);
            }
        }
    }
}
