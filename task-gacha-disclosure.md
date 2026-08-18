# Task: Gacha Rate Disclosure + Pull History

plan.md §9.3: **"Bắt buộc: hiển thị tỉ lệ trong game, lưu lịch sử 100 lần gần nhất."** Từ audit
"hoàn thiện chức năng" — người dùng chọn qua AskUserQuestion cùng đợt Red Dot, Boss phase/enrage,
Accessibility phần còn lại.

## §1. Phát hiện quan trọng TRƯỚC khi viết code

**Lưu trữ lịch sử ĐÃ CÓ SẴN** — `GachaStateDto.History` (`List<string>` heroDefId, cap 100, FIFO
evict) đã được `GachaSystem.PullOne` ghi từ trước, chưa ai hiển thị. Đúng mẫu "hạ tầng có sẵn, chưa
dùng" lặp lại nhiều lần trong dự án. Việc thật CHỈ là hiển thị — không cần đổi schema
`PlayerProfileDto` (tránh rủi ro migration không cần thiết).

## §2. `GachaSystem` — tách hằng số tỉ lệ ra public

`RollRarity` trước đây dùng số ma thuật nội bộ (0.015f, 45, 60, 0.12f, 10, 0.365f). Tách thành
`LEGENDARY_BASE_RATE`/`LEGENDARY_SOFT_PITY_START`/`LEGENDARY_SOFT_PITY_STEP`/`LEGENDARY_HARD_PITY`/
`EPIC_BASE_RATE`/`EPIC_HARD_PITY`/`RARE_BASE_RATE` (public const) — `RollRarity` VÀ màn hiển thị mới
dùng CHUNG 1 nguồn, không thể lệch nhau kiểu "UI ghi 1 số, logic roll dùng số khác". Refactor thuần
cơ học (thay số ma thuật bằng hằng số cùng giá trị), verify an toàn ngay bằng `GachaPityTests` sẵn
có (chứng minh tỉ lệ ±0.05%/1M roll — không đổi hành vi).

## §3. `GachaInfoScreen` — màn RATES/HISTORY mới

Dựng từ `UI_GachaInfo.prefab` — **clone `UI_Codex.prefab`** (không xây mới từ đầu) vì Codex đã có
ĐÚNG khuôn cần: 2-tab (`SwitchTabButton`) + phân trang (`PrevButton`/`NextButton`) + 6 dòng
`NameLabel`/`ProgressLabel`/`Icon`. Mở từ nút `InfoButton` mới (góc trên-phải `UI_Summon.prefab`).

- **Tab RATES**: 4 dòng tĩnh (Legendary/Epic/Rare/Common), không phân trang (`Prev`/`Next`
  disabled). `Icon` không cần sprite — Image không gán sprite tự render như khối màu đặc theo
  `Icon.color` (tô theo `TeamSelectScreen.RarityColor`, tái dùng bảng màu đã có).
- **Tab HISTORY**: đọc `profile.Gacha.History`, đảo ngược (mới nhất trước), 6 dòng/trang → tối đa
  ~17 trang cho 100 mục. Tra rarity qua `Addressables.LoadAssetAsync<HeroDefinitionSO>` (đúng mẫu
  đã dùng ở `TeamSelectScreen`/`HeroDetailScreen`) vì `History` chỉ lưu defId, không lưu rarity —
  tránh đổi schema. Mục `"none"` (hồ chứa rỗng, hiếm) hiện `—` an toàn.

`SummonScreen` gắn `GachaInfoScreen` như sub-screen con lười khởi tạo — đúng mẫu
`TeamSelectScreen.EnsureDetailScreen` (đã có sẵn tiền lệ trong dự án), không cần
`MetaSceneInstaller` biết tới màn này.

## §4. Verify

- `validate_script` 3 file sửa (`GachaSystem.cs`, `SummonScreen.cs`, `GachaInfoScreen.cs` mới) 0
  lỗi. Compile toàn project 0 lỗi.
- **668/668 test xanh** (không đổi — `GachaPityTests` xác nhận refactor hằng số không đổi tỉ lệ
  thật; không thêm test EditMode mới cho `GachaInfoScreen` vì đây là UI đọc thuần, cùng mẫu
  Arena/NodeChoice/Accessibility đã verify qua reflection thay vì test tự động).
- Functional thật qua `execute_code`: dựng `GachaInfoScreen` thật (không mock) với `LocalizationService`
  thật + profile có 3 mục lịch sử giả (2 hero thật + 1 `"none"`) — tab RATES hiện đúng
  "1.5% — Tăng dần từ lần #45 · đảm bảo ở lần #60" v.v (VI, đúng args định dạng); bấm `SwitchTab`
  chuyển đúng sang HISTORY, hiện đúng thứ tự mới-nhất-trước ("#3 — Ember Knight | Rare", "#2 — — |
  ", "#1 — Ember Knight | Rare") — rarity tra đúng qua Addressables thật. Xác nhận `InfoButton` tồn
  tại thật trong `UI_Summon.prefab` đã lưu (đọc lại từ `AssetDatabase`, không tin thao tác vừa chạy).

## §5. Ngoài phạm vi

- `SummonScreen` các nhãn nút khác (PullOne/PullTen/Close/wallet) vẫn chưa localize — giới hạn có
  từ trước (task-content-i18n.md không đụng màn này), không mở rộng thêm ở lượt này.
- Đổi schema `GachaStateDto.History` để lưu rarity/timestamp trực tiếp — tra Addressables lúc hiển
  thị đã đủ, tránh rủi ro migration không cần thiết.
