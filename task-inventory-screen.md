# Task: Inventory screen

Yêu cầu: chọn qua `AskUserQuestion` ("Inventory screen (Recommended)") trong số 2 lựa chọn còn lại
(Inventory / Damage Meter). Việc mới, có quyết định kiến trúc thật (đặt data nào, dựng UI ra sao,
đặt nút ở đâu) — viết xong task file này rồi mới chạm CODE (đã dò cấu trúc scene/prefab trước để
quyết định phạm vi, chưa sửa file .cs nào).

## §0. Findings

Đọc `PlayerProfileDto.cs` (`InventoryDto`), `ItemCatalog.cs`, `EconomyService.cs`, `CodexScreen.cs`
(mẫu gần nhất phù hợp — màn đọc thuần, có tab switch), `MetaEnums.cs` (`CurrencyType`), đo thật
TopBar qua `execute_code`.

- **Dữ liệu cần hiện đã tồn tại đủ, không cần thêm field/hệ thống mới nào**:
  - `InventoryDto.Items` (`List<CurrencyEntryDto>`, key = `ItemType.ToString()`) — 6 vật phẩm tiêu
    hao, đọc qua `economy.GetItemCount(profile.Inventory, type)` (đã có từ task-consumable-items.md).
  - Vật liệu Ascend: `economy.Get(profile.Wallet, CurrencyType.EssenceI/II/III/Core/EnhanceStone)` —
    5 loại, đều có consumer thật (`AscendSystem`, `EquipmentService.TryEnhance`).
  - `ItemCatalog.ALL` đã có sẵn Name/Description cho 6 item — tái dùng thẳng, không cần bảng mới.
- **`CurrencyType.Energy`/`Ticket`/`Honor` là 3 currency CHẾT** — grep toàn bộ `Assets/_Project/
  Scripts` cho cả 3 tên ra 0 kết quả ngoài chỗ khai báo enum + set 1 lần trong
  `LocalPlayerRepository.CreateNew()` (`Wallet.Energy=120, Ticket=1`). Không consumer, không
  producer nào khác. **KHÔNG hiện 3 currency này trong Inventory** — hiện chúng sẽ ngụ ý chúng có
  tác dụng thật, gây hiểu lầm. Ghi nhận, không sửa (ngoài phạm vi, không phải việc của task này).
- **Gold/Gem đã hiện sẵn trên TopBar** — không lặp lại trong Inventory, chỉ hiện phần CHƯA có chỗ
  xem: 6 item tiêu hao + 5 vật liệu Ascend. Tổng 11 mục, vẫn vượt 6-hàng-cố-định như mọi màn khác
  (Quest/Mail) — nhưng KHÔNG cần phân trang kiểu Codex (24/66 mục) vì mỗi tab (Items/Materials) đều
  ≤ 6 hàng (6 và 5) — chỉ cần tab switch, không cần Prev/Next.
- **Mẫu dựng gần nhất: `CodexScreen`/`UI_Codex.prefab`** (đọc thuần, có `SwitchTabButton`) — khớp
  hơn `UI_Quest`/`UI_Mail` (có `ClaimButton` hành động thật). `UI_Codex.prefab` đã có sẵn
  `ClaimButton` per-row nhưng ẩn đi (Codex cũng đọc thuần) — Inventory làm y hệt. `PrevButton`/
  `NextButton` trong `UI_Codex.prefab` KHÔNG cần cho Inventory (không phân trang) — XOÁ khỏi bản
  clone thay vì để lại UI chết không hoạt động (đúng kỷ luật "no unused UI" đã áp dụng ở
  NodeChoiceScreen — xoá hẳn dòng thứ 4 thay vì ẩn).
- **Chỗ đặt nút TopBar mới** — đo thật qua `GetWorldCorners()` (không tin `anchoredPosition` hand
  math — phát hiện quan trọng bên dưới), tìm khoảng trống thật giữa `MailButton` (world x
  [431,497]) và `QuestButton` (world x [591,687]) — khoảng trống 94px. Đặt `InventoryButton` (clone
  `MailButton`, label "ITEMS") tại world x [513,579] — còn dư ~16px mỗi bên, không đè.
  **Phát hiện kỹ thuật quan trọng cho lần sau**: `anchoredPosition` (canvas-local unit) KHÔNG scale
  tuyến tính đơn giản sang world-pixel qua `lossyScale` của chính nút đó khi tính DELTA vị trí —
  `lossyScale` (0.73) đúng cho quy đổi SIZE (sizeDelta→world width, xác nhận khớp 66.1px/90unit ≈
  0.734), nhưng quy đổi VỊ TRÍ (anchoredPosition→world) dùng tỉ lệ KHÁC (~1.18, đo thực nghiệm từ 2
  điểm dữ liệu thật) vì anchoredPosition là dịch chuyển trong không gian PARENT (TopBar), trong khi
  sizeDelta là kích thước riêng của chính RectTransform đó — 2 phép biến đổi khác hệ quy chiếu dù
  cùng "trông giống" 1 con số scale. Bài học: KHÔNG suy luận vị trí mới bằng cách cộng trừ
  `anchoredPosition` theo tỉ lệ ước lượng từ `sizeDelta`/`lossyScale` — phải đo ít nhất 2 điểm THẬT
  qua `GetWorldCorners()` rồi nội suy tuyến tính (đã làm đúng cách này, đặt đúng ngay lần chỉnh thứ 2).
  Cũng phát hiện phụ: mỗi nút TopBar hiện có `localScale` khác nhau (Mail 0.62, Codex 0.52...) — dấu
  vết từ thao tác chỉnh sửa/nhân bản trước đó, không phải bug ảnh hưởng gameplay (chỉ ảnh hưởng cách
  tính toán vị trí nút mới), KHÔNG sửa (ngoài phạm vi, cosmetic-only, không ai nhận ra bằng mắt).

## §1. Scope decision

**Trong phạm vi:**
1. `InventoryScreen.cs` mới (`Meta/`, mirror `CodexScreen.cs`) — 2 tab: ITEMS (6 dòng,
   `ItemCatalog.ALL` + `economy.GetItemCount`) / MATERIALS (5 dòng, `EssenceI/II/III/Core/
   EnhanceStone` + `economy.Get`). Đọc thuần, không có hành động nào theo dòng (giống Codex).
2. `UI_Inventory.prefab` — clone `UI_Codex.prefab`, XOÁ `PrevButton`/`NextButton` (không cần,
   tránh UI chết), giữ `SwitchTabButton`/`CloseButton`/`RowListContainer` (6 row)/`WalletLabel`
   (repurpose thành status "ITEMS · 6 loại" kiểu Codex)/`Title` ("INVENTORY").
3. `InventoryButton` mới trên TopBar (Boot.unity, static) — clone `MailButton`, label "ITEMS", đặt
   tại vị trí đã đo/verify ở §0 (world x ~[513,579], khoảng trống thật giữa Mail/Quest).
4. `MetaSceneInstaller.cs`: field `_inventoryScreen`/`_inventoryButton`, bind trong
   `BindCanvasRefs`, tạo trong `BuildUi`, wire `onClick` mở `InventoryScreen.Open(_profile, ...)`.
5. Test: KHÔNG cần EditMode test riêng — `InventoryScreen` chỉ đọc dữ liệu đã có qua API đã test
   sẵn (`EconomyService`/`ItemCatalog`), không có logic mới nào (giống `CodexScreen` cũng không có
   test riêng, chỉ có `CodexSystem` — nhưng Inventory không cần cả 1 `System` class vì không có
   logic "unlock/tính toán" nào, chỉ là danh sách cố định + lookup trực tiếp).

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG hiện Gold/Gem (đã có TopBar), KHÔNG hiện Energy/Ticket/Honor (currency chết, xem §0).
- KHÔNG hiện trang bị chưa mặc (`profile.Equipment` unequipped) — đã xem được qua `TeamSelectScreen`
  gear panel dù gián tiếp; thêm 1 tab thứ 3 "Equipment" vào đây là mở rộng phạm vi quá xa cho 1 task
  "tối thiểu nhưng thật", để dành nếu có yêu cầu riêng sau.
- KHÔNG hiện Mảnh hero theo từng hero (24 hero × Mảnh riêng — quá nhiều mục, cần phân trang kiểu
  Codex nếu làm, ngoài phạm vi "tối thiểu" của task này).
- KHÔNG sửa `localScale` bất thường của các nút TopBar hiện có — cosmetic-only, không phải việc
  của task này (xem §0).

## §2. Implementation checklist

- [x] `InventoryButton` trên TopBar (Boot.unity) — clone `MailButton`, label "ITEMS", đặt tại world
      x [513,579] (khoảng trống thật giữa Mail [431,497] và Quest [591,687], đo/verify qua
      `GetWorldCorners()` sau khi nội suy tuyến tính từ 2 điểm dữ liệu thật — xem bài học §0 về
      `anchoredPosition` không scale tuyến tính đơn giản theo `lossyScale`). Đã lưu scene.
- [x] `UI_Inventory.prefab` — clone `UI_Codex.prefab`, xoá `PrevButton`/`NextButton` (không cần,
      2 tab đều ≤ 6 mục), đổi `Title` "QUEST"→"INVENTORY". **Phát hiện phụ**: `UI_Codex.prefab`
      chính nó vẫn còn `Title` ghi "QUEST" (sót lại từ lúc clone `UI_Quest.prefab`,
      `CodexScreen.cs` chưa từng set Title runtime) — lỗi có thật, vô hại (WalletLabel/status vẫn
      hiện đúng context "HEROES · Page x/y"), báo riêng qua `spawn_task` (task_9f4f6131), KHÔNG tự
      sửa trong task này (không phải file của Inventory).
- [x] `InventoryScreen.cs` mới — mirror `CodexScreen.cs`, 2 tab ITEMS/MATERIALS. **Phát hiện lúc
      làm**: định dạng ban đầu dự tính "×N — {description}" tràn `ProgressLabel` 90px nặng (đo thật
      144-193px cần) — bỏ hẳn description khỏi hàng danh sách (chỉ NameLabel + `×N`), đúng cách
      `ShopScreen`/`CodexScreen` cũng không hiện mô tả đầy đủ trong hàng, ghi rõ trong doc-comment.
- [x] `MetaSceneInstaller.cs` — thêm field `_inventoryButton`/`_inventoryScreen`, bind trong
      `BindCanvasRefs`, tạo trong `BuildUi`, wire `onClick` (đọc thuần, không cần
      `SaveProfile`/refresh gì sau khi đóng — giống Codex).
- [x] `refresh_unity` compile sạch.
- [x] Chạy full EditMode suite — **413/413 xanh, không đổi** (đúng dự kiến, không thêm/sửa logic
      nào có test — `InventoryScreen` chỉ gọi API đã test sẵn).
- [x] Verify Play-mode THẬT — session này KHÔNG gặp frame-stall (`MetaSceneInstaller.Start()` chạy
      bình thường, khác 2/4 task Play-mode gần nhất). Vào Play mode thật, bấm `StartButton` (Title
      screen) → Meta, bấm `InventoryButton` thật → modal mở, đọc dữ liệu THẬT từ save đang chạy:
      tab ITEMS hiện đúng "Potion ×3 / Ether ×1 / Antidote ×1 / Smoke Bomb ×0 / Revive Feather ×0 /
      Elemental Bomb ×0" (khớp lịch sử mua/dùng item từ task-consumable-items.md); bấm
      `SwitchTabButton` thật → tab MATERIALS hiện đúng "EssenceI ×999 / EssenceII ×1020 /
      EssenceIII ×10 / Core ×10 / EnhanceStone ×0", `row5` đúng inactive (chỉ 5/6 dòng dùng); bấm
      `CloseButton` thật → modal tắt đúng. Toàn bộ chuỗi thao tác THẬT, không có bước nào phải bỏ
      qua vì môi trường — lần verify đầy đủ nhất trong 5 task UI gần đây của session này.
- [x] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`.
