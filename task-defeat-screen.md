# Task: Màn hình còn thiếu theo roadmap — bắt đầu từ Defeat (3 lựa chọn, plan.md §4.15)

Yêu cầu người dùng: "tiếp tục làm màn hình còn lại theo roadmap" — sau khi hoàn tất toàn bộ
task-ui-vfx-polish.md (11 màn Meta + Battle HUD + Landscape). Audit lại `plan.md §10.1` (danh sách
23 màn hình bắt buộc) để biết còn thiếu gì thật, trước khi chọn làm cái nào.

## §1. Audit 23 màn hình (qua Explore agent, đọc code thật — không đoán)

| Màn | Trạng thái | Bằng chứng |
|---|---|---|
| Splash | ❌ Thiếu | `GameBootstrap.cs` không có logo/timer nào, boot thẳng Title/Meta |
| Loading | ❌ Thiếu | Không overlay nào giữa `SceneManager.LoadScene` |
| Home | ✅ Gộp vào NodeMap | `MetaSceneInstaller.cs:27-29` — comment ghi rõ "gộp vào đây, sẽ tách ở P4/P6" |
| HeroList | ❌ Thiếu (dạng roster lọc/sort riêng) | Chỉ có pick-4 panel trong `TeamSelectScreen` |
| Summon | ✅ Xong | |
| Dungeon | ✅ Xong (gộp cả Tower/TrialBoss) | `Game.Meta.Endgame` |
| Arena | ❌ Thiếu, KHÔNG CÓ CẢ placeholder "Sắp có" | 0 kết quả grep "Arena"/"Coming Soon" |
| Shop | ✅ Xong | |
| HeroDetail | ✅ Xong | |
| Equipment | ✅ Gộp vào TeamSelect gear panel | `EquipButton`/`UI_GearSlotRow` |
| Enhance | ✅ Gộp vào TeamSelect gear row | `EnhanceButton` |
| Inventory | ✅ Xong | |
| Formation | ✅ Gộp vào TeamSelect cycle button | `BuildFormationButton` |
| Quest | ✅ Xong (gộp Achievement) | |
| Achievement | ✅ Gộp vào Quest | |
| Collection | ✅ Xong (=CodexScreen) | |
| Mail | ✅ Xong | |
| Settings | ✅ Xong | |
| ChapterSelect | ❌ Thiếu, tự động chọn | `MetaSceneInstaller.cs:367-380` — cùng comment "để dành P4/P6" |
| NodeMap | ✅ Xong | |
| PreBattle | ✅ Xong (=TeamSelectScreen) | |
| BattleHud | ✅ Xong | |
| Result | ✅ Xong (chung 1 overlay) | `BattleSceneInstaller.cs` `ShowResultOverlay`/`BuildResultOverlay` |
| **Defeat** | 🟡 **CÓ NHƯNG THIẾU ĐÚNG SPEC** | Chỉ 1 nút CONTINUE — plan.md §4.15 yêu cầu **3 lựa chọn**: Thử lại / Về map / Hồi sinh bằng Gem |

**Quyết định phạm vi lượt này:** Equipment/Enhance/Formation/Home/ChapterSelect/Achievement là quyết
định GỘP CÓ CHỦ Ý (ghi rõ trong code, không phải thiếu sót) — không đụng vào. Splash/Loading/Arena/
HeroList là thiếu thật nhưng KHÔNG có spec hành vi cụ thể nào bị vi phạm (chỉ là chưa xây). **Defeat**
là hạng mục DUY NHẤT vừa tồn tại vừa VI PHẠM RÕ RÀNG 1 dòng spec cụ thể (plan.md §4.15: "Thua: màn
Defeat: Thử lại / Về map (mất run) / Hồi sinh bằng Gem") — ưu tiên cao nhất, chọn làm trước.

## §2. Phát hiện quan trọng — "mất run" đã bị CỐ Ý bỏ qua từ trước, không phải thiếu sót lượt này

Đọc `MetaSceneInstaller.ApplyPendingBattleResult()` phát hiện comment có sẵn (dòng 254-255):
> "Thua: KHÔNG đánh dấu đã qua — người chơi thử lại node đó (đơn giản hoá P2; roguelike 'mất run
> khi thua' để dành cho P3+ khi có đủ nội dung bù đắp)."

Nghĩa là: game HIỆN TẠI không có khái niệm "mất run" khi thua — thua chỉ đơn giản là không đánh dấu
node đã qua, người chơi bấm lại node đó từ map sau. Đây là quyết định P2 CÓ CHỦ Ý, không phải bug.
**Không implement "mất run" theo nghĩa đen của plan.md** (sẽ mâu thuẫn với quyết định đã có) — nút
"Về map" tái dùng ĐÚNG hành vi CONTINUE hiện tại (không đổi gì, đã đúng ý "về map" sẵn). Chỉ 2/3 lựa
chọn là hành vi MỚI thật sự: **Thử lại** (restart cùng trận) và **Hồi sinh bằng Gem** (cứu trận đang
chơi dở, tiếp tục).

## §3. Implementation

### §3.1. `CombatSimulation.TryReviveWithGem()` (Game.Combat — logic thuần)

Hồi TOÀN BỘ hero về 40% MaxHP — **tái dùng ĐÚNG `RevivePercent=0.4f`** đã có sẵn cho item Revive
Feather (`ItemResolver.UseReviveFeather`, task-consumable-items.md), không bịa tỉ lệ mới. Đặt lại
`State.Result = InProgress` — **KHÔNG cần code "resume" riêng**: đọc `BattleSceneInstaller.Update()`
xác nhận vòng lặp đã check `Simulation.IsFinished` (= `State.Result != InProgress`) MỖI FRAME, tự
chạy tiếp ngay khung hình kế — phát hiện này giúp tránh viết thêm code resume không cần thiết.
`Phase` vẫn giữ nguyên `Finished` sau khi hồi sinh (không reset tay) — đọc `Advance()`'s switch xác
nhận nhánh `default:` tự đưa `Phase` bất kỳ (kể cả `Finished`) về `TurnStart` an toàn, verify bằng
test thật (không chỉ đọc code): thua THẬT qua `Finish()` thật (không set tay `Result`), hồi sinh,
gọi `Advance()` — chạy tiếp không exception, không kẹt.

Trừ Gem là việc tầng Meta (`Game.Combat` không được tham chiếu `Game.Services`, AssemblyRuleTests) —
gọi hàm này SAU KHI đã trừ Gem thành công ở lớp gọi (`BattleSceneInstaller`).

### §3.2. `BattleSceneInstaller.cs` — 3 nút cho Defeat trận THƯỜNG, giữ nguyên 1 nút CONTINUE cho mọi
trường hợp khác

`isRegularDefeat = !victory && _pending != null && !_pending.SpecialMode.HasValue` — Tower/TrialBoss/
Dungeon có ngữ nghĩa "thua" khác hẳn đã hoạt động đúng từ trước (Tower luôn bank tầng, TrialBoss
Timeout=bình thường, Dungeon thất bại chỉ cần thử lại từ Dungeon screen) — KHÔNG đụng vào, chỉ thêm
nhánh mới cho trận node-map thường.

- **RETRY** — `RunContext.QueueBattle` lại với ĐÚNG `_pending` cũ (node/hero/địch/item/formation)
  nhưng seed MỚI (`System.DateTime.UtcNow.Ticks`, đúng cách `LaunchBattle` gốc sinh seed) — replay
  cùng seed sẽ ra cùng kết quả hệt (combat deterministic), seed mới mới là "thử lại" thật. Nạp lại
  scene Battle — tái dùng TOÀN BỘ pipeline khởi tạo có sẵn, không viết lại gì.
- **RETURN TO MAP** — gọi thẳng `HandleContinue(false, 0, 0, 0)` — ĐÚNG hành vi cũ, không đổi.
- **REVIVE — 100 Gem** — số tự thiết kế (plan.md không cho số), chưa bằng 1 lượt gacha đơn
  (`GachaSystem.SINGLE_PULL_COST=300`) — đủ để là lựa chọn cân nhắc thật. Nút tự khoá
  (`interactable=false`) khi không đủ Gem (đọc `IEconomyService.Get` thật, đúng mẫu mọi nút mua
  hàng khác trong game). Click: trừ Gem thật → `TryReviveWithGem()` → nếu fail (an toàn cuối, không
  nên xảy ra vì nút chỉ hiện khi `Result==Defeat`) hoàn lại Gem đã trừ → nếu thành công, lưu profile
  + đóng overlay + `_resultShown=false` (để lần thua sau vẫn hiện lại overlay đúng 1 lần).

Helper `NewOverlayButton` mới (tách từ code CONTINUE cũ) — dùng chung cho cả 4 nút (CONTINUE +
3 nút mới), tránh lặp code tạo Button/Image/Label 4 lần.

## §4. Verify (đo thật, không chỉ tin compile)

- `CombatSimulation.TryReviveWithGem()`: test trực tiếp qua `execute_code` — set `hero.Hp=0` +
  `Result=Defeat` thủ công, gọi hàm, xác nhận `hero.Hp=68` (đúng 40% của MaxHp=170), `Result=
  InProgress`, gọi lần 2 đúng trả `False` (chặn double-revive).
- Mô phỏng đường THẬT nhất: dựng 1v1 hero yếu (str=1,con=1) vs địch mạnh, chạy `Advance()`/
  `SubmitIntent()` tới khi thua THẬT qua `Finish()` thật (`Phase=Finished` thật, không set tay) —
  hồi sinh, gọi `Advance()` tiếp — không exception (trận thua lại ngay vì matchup cố tình cực đoan,
  đúng kỳ vọng — hồi sinh không đảm bảo thắng, chỉ đảm bảo trận TIẾP TỤC được).
- `BuildResultOverlay` qua reflection: `_pending` giả lập trận thường + `victory=false` → đúng
  3 nút, REVIVE tự khoá khi không có `IEconomyService` (gem=0 < 100). `victory=true` → đúng giữ
  nguyên 1 nút CONTINUE, panel height vẫn 200 (không đổi hành vi cũ).
- **5 test mới** (`Assets/Tests/EditMode/Combat/CombatSimulationReviveTests.cs`) — phủ đúng các kịch
  bản đã verify tay ở trên thành test cố định: chặn khi chưa thua, hồi đúng %, không hồi địch, chặn
  gọi lặp, resume không exception qua đường Finish() thật.
- `validate_script` 0 lỗi cả 3 file, `refresh_unity` force+compile 0 lỗi console, **637/637 test
  xanh** (632 cũ + 5 mới).

## §5. Còn thiếu (chưa làm lượt này, ghi lại để dành)

- **Splash, Loading** — cosmetic, độ ưu tiên thấp, không vi phạm spec hành vi cụ thể nào.
- **Arena** — plan.md tự ghi "v1.1, v1.0 hiện 'Sắp có'" — hiện KHÔNG có cả placeholder, nên làm khi
  có TopBar/Home thật (P6), 1 nút disabled + label "Coming Soon" là đủ cho v1.0.
- **HeroList** (roster lọc/sort riêng, tách khỏi pick-4 panel) — cần thiết kế màn mới, độ ưu tiên
  trung bình (đã có Codex làm nơi xem toàn bộ hero, dù không lọc/sort được).
- **ChapterSelect** — tự động chọn chương hiện tại là hợp lý cho lối chơi tuyến tính hiện có (chưa
  cho chơi lại chương cũ) — chỉ cần khi thêm tính năng "chơi lại chương đã qua".
