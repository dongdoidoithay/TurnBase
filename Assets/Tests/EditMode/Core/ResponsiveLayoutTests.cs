using Game.Core.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Core
{
    /// <summary>
    /// task-phase-5-gaps.md Phần E — hàm thuần của <see cref="LayoutProfileSwitcher"/> và
    /// <see cref="SafeAreaFitter"/>, không cần Unity runtime/scene thật (roadmap.md DoD Phase 6:
    /// "Test 5 tỉ lệ: 9:16, 3:4, 16:9, 20:9, 21:9" — cộng thêm 1:1 theo task-phase-5-gaps.md §E1).
    /// </summary>
    public class ResponsiveLayoutTests
    {
        private static readonly LayoutProfile Portrait = new()
        {
            Name = "Portrait", AnchorMin = new Vector2(0.5f, 0.5f), AnchorMax = new Vector2(0.5f, 0.5f),
            Pivot = new Vector2(0.5f, 0.5f), AnchoredPosition = new Vector2(0, 10), SizeDelta = new Vector2(360, 340),
            Scale = Vector3.one,
        };

        private static readonly LayoutProfile Landscape = new()
        {
            Name = "Landscape", AnchorMin = new Vector2(0.5f, 0.5f), AnchorMax = new Vector2(0.5f, 0.5f),
            Pivot = new Vector2(0.5f, 0.5f), AnchoredPosition = new Vector2(0, -5), SizeDelta = new Vector2(420, 300),
            Scale = Vector3.one,
        };

        // ---------- LayoutProfileSwitcher.IsLandscape / PickProfile — 5 tỉ lệ roadmap + 1:1 ----------

        [TestCase(1080, 1920, false, TestName = "9:16 portrait")]
        [TestCase(1536, 2048, false, TestName = "3:4 portrait (tablet)")]
        [TestCase(1920, 1080, true, TestName = "16:9 landscape")]
        [TestCase(2400, 1080, true, TestName = "20:9 landscape")]
        [TestCase(2520, 1080, true, TestName = "21:9 landscape (ultra-wide)")]
        [TestCase(1080, 1080, false, TestName = "1:1 square (=, không phải landscape)")]
        public void IsLandscape_MatchesWidthGreaterThanHeight(int width, int height, bool expectedLandscape)
        {
            Assert.AreEqual(expectedLandscape, LayoutProfileSwitcher.IsLandscape(width, height));
        }

        [TestCase(1080, 1920)]
        [TestCase(1536, 2048)]
        public void PickProfile_Portrait_ReturnsPortraitProfile(int width, int height)
        {
            var picked = LayoutProfileSwitcher.PickProfile(width, height, Portrait, Landscape);
            Assert.AreEqual(Portrait.Name, picked.Name);
            Assert.AreEqual(Portrait.SizeDelta, picked.SizeDelta);
        }

        [TestCase(1920, 1080)]
        [TestCase(2400, 1080)]
        [TestCase(2520, 1080)]
        public void PickProfile_Landscape_ReturnsLandscapeProfile(int width, int height)
        {
            var picked = LayoutProfileSwitcher.PickProfile(width, height, Portrait, Landscape);
            Assert.AreEqual(Landscape.Name, picked.Name);
            Assert.AreEqual(Landscape.SizeDelta, picked.SizeDelta);
        }

        [Test]
        public void PickProfile_Square_FallsBackToPortrait()
        {
            // width == height không lớn hơn → không phải landscape (LayoutProfileSwitcher.IsLandscape
            // dùng "width > height" nghiêm ngặt, khớp task-phase-5-gaps.md §E2 "portable hơn
            // Screen.orientation").
            var picked = LayoutProfileSwitcher.PickProfile(1080, 1080, Portrait, Landscape);
            Assert.AreEqual(Portrait.Name, picked.Name);
        }

        [Test]
        public void LayoutProfile_ApplyTo_SetsEveryFieldOnRectTransform()
        {
            var go = new GameObject("Test", typeof(RectTransform));
            try
            {
                var rt = (RectTransform)go.transform;
                Landscape.ApplyTo(rt);

                Assert.AreEqual(Landscape.AnchorMin, rt.anchorMin);
                Assert.AreEqual(Landscape.AnchorMax, rt.anchorMax);
                Assert.AreEqual(Landscape.Pivot, rt.pivot);
                Assert.AreEqual(Landscape.AnchoredPosition, rt.anchoredPosition);
                Assert.AreEqual(Landscape.SizeDelta, rt.sizeDelta);
                Assert.AreEqual(Landscape.Scale, rt.localScale);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LayoutProfile_CaptureFrom_RoundTripsThroughApplyTo()
        {
            var go = new GameObject("Test", typeof(RectTransform));
            try
            {
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.1f, 0.2f);
                rt.anchorMax = new Vector2(0.9f, 0.8f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(12, -34);
                rt.sizeDelta = new Vector2(200, 60);
                rt.localScale = new Vector3(1.1f, 1.1f, 1f);

                var captured = LayoutProfile.CaptureFrom(rt, "Snapshot");

                var other = new GameObject("Other", typeof(RectTransform));
                try
                {
                    var otherRt = (RectTransform)other.transform;
                    captured.ApplyTo(otherRt);
                    Assert.AreEqual(rt.anchorMin, otherRt.anchorMin);
                    Assert.AreEqual(rt.sizeDelta, otherRt.sizeDelta);
                    Assert.AreEqual(rt.localScale, otherRt.localScale);
                }
                finally
                {
                    Object.DestroyImmediate(other);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------- SafeAreaFitter.GetSafeAreaRect ----------

        [Test]
        public void GetSafeAreaRect_NoNotch_ReturnsFullNormalizedRect()
        {
            var screen = new Rect(0, 0, 1920, 1080);
            var safeArea = new Rect(0, 0, 1920, 1080);

            var r = SafeAreaFitter.GetSafeAreaRect(screen, safeArea);

            Assert.AreEqual(new Vector2(0, 0), r.position);
            Assert.AreEqual(new Vector2(1, 1), (Vector2)(r.position + r.size));
        }

        [Test]
        public void GetSafeAreaRect_TopNotch_ShrinksTopEdgeOnly()
        {
            // Notch trên cùng cắt 120px khỏi 2400px chiều cao — safeArea bắt đầu từ y=0 (gốc dưới-
            // trái theo Screen.safeArea) và cao 2280px.
            var screen = new Rect(0, 0, 1080, 2400);
            var safeArea = new Rect(0, 0, 1080, 2280);

            var r = SafeAreaFitter.GetSafeAreaRect(screen, safeArea);

            Assert.AreEqual(0f, r.xMin, 1e-4f);
            Assert.AreEqual(1f, r.xMax, 1e-4f);
            Assert.AreEqual(0f, r.yMin, 1e-4f);
            Assert.AreEqual(2280f / 2400f, r.yMax, 1e-4f);
        }

        [Test]
        public void GetSafeAreaRect_PillarboxBothSides_ShrinksSymmetrically()
        {
            // Camera cutout 2 bên (landscape gập) — safeArea thụt vào 60px mỗi bên theo chiều rộng.
            var screen = new Rect(0, 0, 2520, 1080);
            var safeArea = new Rect(60, 0, 2400, 1080);

            var r = SafeAreaFitter.GetSafeAreaRect(screen, safeArea);

            Assert.AreEqual(60f / 2520f, r.xMin, 1e-4f);
            Assert.AreEqual((60f + 2400f) / 2520f, r.xMax, 1e-4f);
            Assert.AreEqual(0f, r.yMin, 1e-4f);
            Assert.AreEqual(1f, r.yMax, 1e-4f);
        }

        [TestCase(1080, 1920)]
        [TestCase(1536, 2048)]
        [TestCase(1920, 1080)]
        [TestCase(2400, 1080)]
        [TestCase(2520, 1080)]
        public void GetSafeAreaRect_FullSafeArea_IsIdentityAcrossAllRatios(int width, int height)
        {
            var screen = new Rect(0, 0, width, height);
            var r = SafeAreaFitter.GetSafeAreaRect(screen, screen);

            Assert.AreEqual(new Vector2(0, 0), r.position);
            Assert.AreEqual(1f, r.size.x, 1e-4f);
            Assert.AreEqual(1f, r.size.y, 1e-4f);
        }

        [Test]
        public void GetSafeAreaRect_ZeroScreen_ReturnsFullRectWithoutDivideByZero()
        {
            var r = SafeAreaFitter.GetSafeAreaRect(new Rect(0, 0, 0, 0), new Rect(0, 0, 0, 0));
            Assert.AreEqual(new Vector2(0, 0), r.position);
            Assert.AreEqual(new Vector2(1, 1), r.size);
        }
    }
}
