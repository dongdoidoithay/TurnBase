# Task: Red Dot Notification System

plan.md §10.6. Từ audit "hoàn thiện chức năng" — người dùng chọn qua AskUserQuestion cùng đợt Boss
phase/enrage, Gacha rate disclosure, Accessibility phần còn lại.

## §1. Diễn giải quy tắc "miễn phí khả thi"

Spec gốc: cây phân cấp `Root → BottomNav.{Hero,Summon,Dungeon,Shop} → SubTab → Item`,
`RedDotService.SetDirty(path)` lan lên cha tự động, chỉ hiện khi có hành động MIỄN PHÍ khả thi.

**Đơn giản hoá có chủ đích**: TopBar dự án này chỉ có 1 tầng nút phẳng (không sub-tab/item con
thật) — xây 1 hệ push/dirty-propagation cho quy mô này là thừa. `RedDotService` tính lại toàn bộ
(pull) mỗi lần `Refresh`, đơn giản hơn hẳn, đúng lối "đơn giản hoá có chủ đích" lặp lại nhiều lần
trong dự án.

"Miễn phí" diễn giải là "dùng được NGAY bằng tài nguyên đã có sẵn" (không cần mua thêm) — quy ước
phổ biến thể loại, khớp tinh thần câu loại trừ "không hiện chỉ vì có đồ bán" (phân biệt "cần MUA"
khỏi "đã đủ, chỉ cần bấm").

## §2. 4 node phủ THẬT — khác danh sách ví dụ Hero/Summon/Dungeon/Shop

- **Hero** → `_heroListButton`: có hero nâng skill được (`SkillUpgradeSystem.CanUpgrade`) VÀ đủ
  Gold trả ngay (`UpgradeCost`).
- **Dungeon** → `_dungeonButton`: có dungeon hằng ngày (Gold/Exp/Material/Stone) còn lượt hôm nay
  (`DungeonSystem.CanEnter`) — phát hiện quan trọng: Energy KHÔNG bị enforce ở bất kỳ đâu trong code
  thật (đã audit), nên "vào được" ĐÃ là miễn phí thật, không cần thêm check ví.
- **TrialBoss** → `_trialBossButton`, **Tower** → `_towerButton`: có thưởng mốc tuần đủ điều kiện
  nhưng CHƯA nhận (`TrialBossSystem.HasClaimable`/`TowerSystem.HasClaimable`, 2 hàm PEEK mới, không
  mutate — tách khỏi `TryClaimRewards` để không đổi hành vi claim thật).
- **Summon** BỊ BỎ: Summon Ticket (currency "miễn phí" hợp lý duy nhất) chưa từng được wiring bất kỳ
  đâu — gacha hiện chỉ tiêu Gem trả phí. Ép 1 tín hiệu giả sẽ là bịa hành vi không có thật.
- **Shop** BỊ BỎ: không có nút Shop nào trên TopBar (chỉ vào qua node bản đồ) — quy tắc chính đã
  loại trừ Shop vì mọi thứ trong đó đều phải mua.

## §3. Implementation

`Assets/_Project/Scripts/Meta/Notifications/RedDotService.cs` (mới, pure static, `Game.Meta`):
4 hàm `Is*Dirty` độc lập, không có state — đọc thẳng `PlayerProfileDto` + gọi hệ thống pure có sẵn.

`TrialBossSystem.HasClaimable`/`TowerSystem.HasClaimable` — 2 hàm PEEK mới thêm vào file hiện có,
copy logic `highestEligible > ClaimedTier` từ `TryClaimRewards` nhưng KHÔNG mutate.

4 GameObject `RedDot` dựng TĨNH trong `Boot.unity` (đúng [[feedback_hierarchy_means_static]]) —
child của `HeroListButton`/`DungeonButton`/`TrialBossButton`/`TowerButton`, cùng neo góc trên-phải
như `MailBadge` (anchor/pivot (1,1), pos (-2,-2)) nhưng nhỏ hơn (14×14, không có `BadgeLabel` — chấm
thuần, không số) và mặc định `SetActive(false)`.

`MetaSceneInstaller.RefreshRedDots()` mới, gọi trong `RefreshMap()` cạnh `RefreshMailBadge()` —
cùng điểm hook, chạy lại mỗi khi bản đồ refresh (sau trận, sau claim, mở lại Meta).

## §4. Verify

- `validate_script` 4 file sửa (`RedDotService.cs` mới, `TrialBossSystem.cs`, `TowerSystem.cs`,
  `MetaSceneInstaller.cs`) 0 lỗi. Compile toàn project 0 lỗi.
- 10 test mới `RedDotServiceTests.cs` (Hero đủ/thiếu Gold, Hero rỗng, Hero skill maxed hết, Dungeon
  tươi mới/đã sạch hôm nay, TrialBoss/Tower có/không thưởng chờ).

## §5. Ngoài phạm vi

- Summon/Shop (xem §2, không có tín hiệu thật để phủ trung thực).
- Animation/juice cho chấm đỏ (chỉ SetActive tĩnh, không pulse/scale).
- Sub-tab/Item level của cây phân cấp gốc — TopBar hiện tại không có cấp con thật để phủ.
