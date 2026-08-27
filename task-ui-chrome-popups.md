# Task: UI chrome mới (Art_Sample) + hệ popup thông báo dùng chung

Yêu cầu người dùng: "lấy asset ở [_Reference/Art_Sample] thiết kế UI các màn và popup thông báo" —
hỏi lại scope qua AskUserQuestion (bị dismiss lần đầu), sau đó người dùng chốt "làm cả 4 cái luôn,
chạy song song các task" = áp dụng chrome mới cho TOÀN BỘ màn Meta + xây cả 3 loại popup (toast/
confirm/reward) cùng lúc. Lưu ý: không dùng nhiều Agent chạy song song thật (rủi ro conflict cùng 1
Unity Editor session đã gặp nhiều lần trong dự án) — làm tuần tự nhưng không dừng lại hỏi thêm.

## §0. Tham khảo phong cách

`_Reference/Art_Sample/Screen_combat.jpg` — khung panel bo góc vát, viền vàng cam (~`#F4A259`), nền
đỏ mận tối (~`#2B1B2E`, đã khớp `palette.json` có sẵn), thanh máu/SP có khung riêng, nút bấm cùng
tông viền vàng. `Screen_start.png` chỉ là art nền khí quyển (không phải UI chrome, không dùng cho
task này).

## §1. Hạ tầng chrome mới (Pillow, `compose.py`)

- [x] `panel_gold.png` (64×64, border 6, corner "notch", viền `#F4A259`, nền `#2B1B2E`, inner
      highlight `#FFD9A0`) — khung panel dùng chung mọi nơi.
- [x] `button_gold_{normal,hover,pressed,disabled}.png` (96×32, 4 trạng thái cắt từ 1 sheet).
- [x] `healthbar_hp_{frame,fill,trail}.png` + `healthbar_sp_*` (96×12/10, 3 lớp: khung/fill/damage
      trail) — CHƯA lắp vào screen nào, để dành cho HeroDetail/TeamSelect sau.
- [x] Import vào `Assets/_Project/Resources/Art/UI/Chrome/` — script mới
      `Tools/pixel-art-pipeline/scripts/import_ui_chrome.py` (viết `.meta` tay: Point filter, Full
      Rect, PPU 100, border đúng từng sprite) + `slice_chrome.py` (cắt sheet nhiều lớp compose.py sinh
      ra thành file rời theo meta `states`/`layers`).
- [x] Verify: `Resources.Load<Sprite>` cả 11 sprite trả về đúng `rect`/`border` qua `execute_code`.

## §2. Hệ Popup dùng chung (MỚI — dự án trước đó chưa có)

- [x] `IPopupService` (`Assets/_Project/Scripts/Core/UI/Popups/`) — `ShowToast`/`ShowConfirm`/
      `ShowReward`. Đăng ký ở `ServiceInstaller.Install()` như mọi service khác (composition root
      duy nhất, đúng luật `ServiceLocator` — KHÔNG dùng static bridge riêng như `RunContext`).
- [x] `PopupService : MonoBehaviour, IPopupService` — dựng 1 Canvas "PopupLayer" con của
      `IUiRootHost.Root` (DontDestroyOnLoad, sống xuyên mọi scene). **Quan trọng:** đổi từ
      `RenderMode.ScreenSpaceOverlay` sang `ScreenSpaceCamera` (worldCamera=`Camera.main`) — Overlay
      canvas bị công cụ chụp ảnh MCP (`manage_camera`, render qua camera cụ thể) BỎ QUA hoàn toàn,
      kể cả `MetaCanvas`/`UIRoot` gốc của dự án cũng vậy. Đổi sang Screen Space - Camera vẫn giữ
      đúng hành vi "phủ toàn màn hình, không phụ thuộc world space" nhưng render qua camera nên chụp
      ảnh/`ScreenCapture` đều thấy — sửa đúng 1 chỗ, áp dụng cho mọi popup tương lai.
- [x] 3 prefab tại `Assets/_Project/Resources/Prefabs/UI/Popups/` (đúng quy ước
      `Resources.Load<GameObject>("Prefabs/UI/Popups/...")` như mọi UI_*.prefab khác):
      - `Toast.prefab` — `ToastNotification.cs`, tự fade in/out (`Time.unscaledDeltaTime`, chạy được
        cả khi game pause) rồi `Destroy` sau `duration`.
      - `ConfirmDialog.prefab` — `ConfirmDialogView.cs`, dim overlay chặn click xuyên, 2 nút OK/HỦY.
      - `RewardPopup.prefab` — `RewardPopupView.cs`, dim overlay, icon tùy chọn (ẩn nếu null) + nút
        tiếp tục.
      - `Game.Core.asmdef` thiếu ref `Unity.TextMeshPro` → build lỗi CS0246 → thêm vào (an toàn, đây
        là assembly lá, không tạo vòng lặp phụ thuộc).
- [x] **Verify THẬT qua Play mode** (Boot → Meta, không phải test cô lập): `ShowToast`/`ShowConfirm`/
      `ShowReward` gọi qua `ServiceLocator.Get<IPopupService>()`, chụp `manage_camera screenshot`
      thấy đúng cả 3 popup hiện trên màn Chapter map thật, đè lên nhau đúng thứ tự z (Reward đè lên
      Confirm khi gọi tiếp không đóng cái trước — xác nhận layering đúng ý). 668/668 test xanh sau
      khi build xong (chỉ thêm code mới, không đụng logic Combat/Meta cũ).

## §3. Reskin màn Meta — MỚI BẮT ĐẦU, CÒN THIẾU 7/8 MÀN

- [x] **Shop** (`UI_Shop.prefab`) — `Panel`+`CloseButton`+ cả 10 `BuyButton` (Row_0..Row_9) đổi sprite
      sang `panel_gold`/`button_gold_normal` (type Sliced). Tắt `InnerBlue` (lớp phủ xanh cũ đè lên
      panel, không còn cần vì panel đã có màu riêng). **Lỗi thật gặp phải:** các Image này vốn có sẵn
      `color` tint xanh/tím từ trước — áp sprite mới KHÔNG tự xoá tint, kết quả ám màu sai (đã chụp
      thấy, sửa bằng cách set lại `color = (1,1,1,1)` từng object). Verify qua Play mode thật (mở
      `ShopScreen` bằng `Open(ProfileContext.Current, null)` qua `execute_code`), chụp ảnh xác nhận
      đúng tông vàng/đỏ mận, không còn ám xanh.
- [ ] **CÒN LẠI 7 màn chưa đụng tới:** Settings, Inventory, TeamSelect, HeroDetail, Summon,
      ChapterProgress, NodeChoice (+ các màn phụ khác nếu có: Mail/Codex/Arena/Tower/TrialBoss/
      Dungeon/GachaInfo/HeroList/Quest — xem đủ danh sách tại
      `Assets/_Project/Resources/Prefabs/UI/Screens/`). Quy trình đã CHỨNG MINH lặp lại được nhanh:
      với mỗi prefab — `manage_prefabs modify_contents` đổi `sprite`+`type:1`(Sliced) trên từng
      Image nền/nút, RESET `color=(1,1,1,1)` mọi Image bị đổi, tắt các lớp phủ màu cũ dư thừa
      (`InnerBlue`-kiểu), verify qua Play mode + `execute_code` gọi thẳng `Open()`/hàm hiển thị màn
      + `manage_camera screenshot`.
- [ ] Thanh máu (`healthbar_hp/sp_*`) chưa lắp vào đâu — ứng viên: HeroDetailScreen, TeamSelectScreen
      (thanh HP hero trong đội hình).

## §3.5. Combat/Battle HUD — skill row đổi sang thẻ bài (phản hồi trực tiếp từ người dùng)

Người dùng gửi ảnh so sánh: màn Combat thật KHÔNG giống ảnh ví dụ (ví dụ có "thẻ bài và icon skill
nhìn hiện đại hơn"). Đúng — `BattleHudScreen`/`SkillSlotView` (Combat HUD) chưa từng được đụng tới ở
việc trên (chỉ có Meta screens + popup), và vốn KHÔNG dùng sprite chrome nào cả (chỉ `Image.color`
phẳng, ô vuông).

- [x] `SkillSlotView.cs` — bỏ hẳn lớp "_fill" màu phẳng, `_border` đổi thành `Image` dùng sprite
      (`card_gold`/`card_gold_selected`/`card_gold_disabled`, 3 sprite MỚI, `type=Sliced`) thay vì
      set `Image.color` phẳng như cũ. Mọi state (`Available/Selected/OnCooldown/NotEnoughSp/
      Silenced/UltimateCharging/UltimateReady/Empty`) đổi sang chọn 1 trong 3 sprite + tint màu phụ
      trợ (giữ nguyên hiệu ứng nhấp nháy Ultimate, đổi màu SP thiếu...) — **hành vi logic KHÔNG đổi,
      chỉ đổi cách vẽ**. `Create()` đổi chữ ký từ 1 tham số `size` (ô vuông) sang `width,height` riêng
      (chỉ 1 nơi gọi — `BattleHudScreen.cs`, không phá API nào khác).
- [x] 3 sprite mới `card_gold*.png` (72×100, border 6, corner "notch") — cùng script
      `import_ui_chrome2.py`, cùng convention `.meta` như batch chrome đầu.
- [x] `BattleHudScreen.BuildSkillGrid()` — hàng 0 (skill) cao hơn hẳn (72→**75** đơn vị, biến
      `cardH`) trong khi hàng 1 (item)/hàng 2 (tactic) GIỮ NGUYÊN ô vuông `cell=52` cũ, chỉ dịch
      xuống thêm đúng phần chênh (`extraH = cardH - cell`) — tránh rủi ro viết lại toàn bộ layout
      (đúng tinh thần thận trọng của comment cũ trong hàm này).
- [x] Verify Play mode THẬT — vào thẳng 1 trận `hero_ember_knight`+`hero_frost_sage` vs
      `enemy_goblin`+`enemy_wolf` qua `RunContext.QueueBattle` + `LoadScene("Battle")`, chờ vài giây
      cho AUTO ON tự đánh, chụp `manage_camera` thấy RÕ 5 thẻ bài viền vàng cao hơn ô vuông bên dưới,
      icon skill hiện đúng bên trong (sword/dagger/burst/heal/star).

**CHƯA làm — flag rõ cho người dùng quyết định tiếp:**
- Icon skill (`Art/UI/Icons/Skills/icon_skill_*.png`) tự thân vẫn là glyph đơn giản, chưa "hiện đại"
  hơn về CHẤT LƯỢNG ART (chỉ đổi khung/hình dạng ô chứa, chưa vẽ lại icon). Muốn icon thật sự "hiện
  đại" như ảnh ví dụ (icon chi tiết, có shading/gradient rõ) cần 1 đợt vẽ lại icon riêng (ComfyUI hoặc
  Pillow chi tiết hơn) — việc riêng, chưa bắt đầu.
- Hàng 0 vẫn là GRID cố định (không phải "hand of cards" xoè/nghiêng thật như plan gốc
  `linear-noodling-lake.md` Phase B mô tả) — bản này là bước an toàn "thẻ bài xếp thẳng hàng", không
  phải fan/arc layout.

## §3.6. Cắt THẬT từ ảnh mẫu (100%) — thay cho chrome vẽ tay compose.py

Người dùng yêu cầu tường minh: "thiêt kế 100% như ảnh mẫu. cắt ảnh làm UI" — nghĩa là CẮT PIXEL THẬT
từ `Screen_combat.jpg` (736×882), không phải vẽ lại bằng `compose.py` (dù compose.py cùng tông màu,
không phải "100% giống").

- [x] Cắt trực tiếp bằng Pillow (không qua ComfyUI, không cần AI):
      - **Thẻ skill** (`card_normal.png`, 88×104) — cắt đúng 1 thẻ tím trong hàng 3 thẻ màu
        (`(207,558)-(295,662)`), sau đó XOÁ art quái vật bên trong (vẽ đè rect fill
        `#2B1B2E` — TRÙNG KHỚP palette dự án có sẵn) — GIỮ NGUYÊN 100% viền răng cưa (scalloped
        top) + góc bo thật từ ảnh gốc. `card_selected`/`card_disabled` sinh từ bản gốc này qua
        `ImageEnhance` (sáng hơn/desaturate), không cắt lại.
      - **Ô icon** (`icon_slot.png`→`icon_slot_brown`, 40×53) — cắt 1 ô trong lưới "vật phẩm" góc
        phải (`(405,568)-(445,621)`), xoá icon lọ thuốc bên trong bằng cùng kỹ thuật (fill màu nâu
        lấy mẫu thật từ chính ảnh, không phải màu tự chọn).
      - Không 9-slice theo nghĩa co giãn thật (viền răng cưa không thể stretch sạch — hình học
        không đều) — dùng `spriteMeshType: Full Rect` + border cho Unity nhưng thực chất card LUÔN
        hiển thị ở đúng 1 kích thước hợp lý gần với gốc, không kéo giãn quá mức.
      - Import qua `import_ui_chrome3.py` — GHI ĐÈ trực tiếp `card_gold*.png`/`.meta` cũ (cùng tên
        file) → **0 thay đổi C#** cần thiết cho `SkillSlotView`, vì nó vẫn `Resources.Load` đúng
        path cũ. `icon_slot_brown` là asset MỚI, cần 1 chỗ code trỏ tới.
- [x] `ItemSlotView.cs` (hàng vật phẩm/consumable) — cùng kiểu tái cấu trúc như `SkillSlotView` đợt
      trước: bỏ `_fill` phẳng, `_border` dùng sprite `icon_slot_brown` (Sliced), state màu chuyển
      sang tint sprite thay vì đổi `Image.color` phẳng.
- [x] Verify Play mode THẬT (trận `hero_ember_knight`+`hero_frost_sage` vs `enemy_goblin`+
      `enemy_wolf`, chủ động `SubmitIntent`+`PlayPending` qua `execute_code` để hàng skill có data
      thật thay vì rỗng) — chụp ảnh: **5 thẻ bài viền răng cưa vàng kem, icon skill thật hiện rõ
      bên trong (kiếm/dao/nổ-sao/tim-heal/sao-ultimate), hàng item bên dưới dùng ô nâu bo tròn** —
      khớp rất sát bố cục+màu sắc ảnh mẫu `Screen_combat.jpg`. 668/668 test vẫn xanh.

**Bài học cho các phần CÒN LẠI (health bar/portrait/END TURN/khung ngoài/EQUIPMENT grid) — nếu làm
tiếp dùng ĐÚNG kỹ thuật này (cắt+xoá nội dung, không vẽ tay):**
1. Crop generous vùng nghi ngờ → `Read` xem → thu hẹp toạ độ chính xác (lặp 2-3 lần thường đủ).
2. Với vùng có "nội dung" cần xoá (art/text/số bên trong khung) → sample màu nền THẬT từ 1 điểm an
   toàn bên trong (không phải đoán màu) → vẽ đè rect (hoặc rounded-rect) đúng vùng nội dung, GIỮ
   nguyên viền/góc gốc.
3. Ảnh gốc là JPEG đã nén — không có "kích thước pixel gốc" hoàn hảo để hạ về, cứ giữ nguyên độ phân
   giải crop, để Point filter lo phần còn lại khi hiển thị.

## §3.6. Sửa "siêu xấu" — bỏ crop JPEG, vẽ tay thẻ bài sạch

Người dùng phản hồi thẳng ("siêu xấu") sau khi thấy bản card đầu tiên (dựng từ crop trực tiếp
`Screen_combat.jpg`). Tự soi lại bằng cách zoom raw screenshot đã chụp — xác nhận 3 lỗi thật:

1. **Viền thẻ mờ/nhiễu** — crop JPEG mang theo artifact nén, phóng to bằng Point filter làm lộ rõ
   khối 8×8 DCT thay vì nét pixel sắc như phần còn lại của game.
2. **Màu thẻ lệch tông nhau** — `ElementColor` alpha 0.28 (có từ trước, không phải mới) đủ mạnh để
   biến thẻ Fire thành đỏ chóe cạnh 3 thẻ tím bình thường → trông như lỗi, không phải bộ đồng nhất.
3. **Số hồi chiêu khổng lồ đè icon** — `_cooldown` cũ to bằng 50% cả ô, che gần hết icon bên dưới.

**Quyết định:** bỏ hẳn cách "crop ảnh JPEG thật" — kỹ thuật đúng để giữ ĐÚNG silhouette (răng cưa
trên đỉnh) nhưng render sạch là **vẽ tay bằng `ImageDraw` (Pillow)**, không AI/không crop, đúng kỹ
thuật `character_draw.py` đã dùng cả session (khối màu phẳng, không AA/không nén):

- [x] `Tools/pixel-art-pipeline/scripts/draw_skill_card.py` — thẻ bài scalloped-top vẽ tay: thân
      rounded-rect + hàng vòm tròn CHỒNG LẤN nhau ở đỉnh (bán kính > spacing/2, tâm lún xuống dưới
      mép thân) để liền mạch, không có khe hở giữa các răng cưa (lỗi ở bản nháp đầu — đã sửa). 3
      biến thể `normal`/`selected`/`disabled` cùng 1 hàm, chỉ đổi bộ màu.
- [x] Icon slot (item row) — cũng vẽ tay (`rounded_rectangle` lồng nhau) thay vì crop, cùng lý do.
- [x] `SkillSlotView.cs`: `ElementColor` alpha 0.28→**0.16** (thẻ vẫn nhận diện được nguyên tố qua
      màu + glyph mù màu, nhưng không còn "cãi nhau" giữa các thẻ). Cooldown đổi từ chữ to phủ kín ô
      sang **badge tròn nhỏ giữa ô** (sprite `cooldown_badge.png` mới, tròn đen mờ 72% alpha) + chữ
      nhỏ hơn (0.5→0.28× size) — không còn che icon.
- [x] Verify Play mode thật (vào thẳng batlle qua `RunContext.QueueBattle` + `SubmitIntent` để ép
      1 lượt thật xảy ra thay vì chờ Auto-battle tự chạy) — chụp thấy 5 thẻ ĐẦY ĐỦ (icon+cost badge
      thật), viền răng cưa sắc nét, tông màu đồng nhất, không còn số hồi chiêu khổng lồ. 668/668 test
      xanh.

**Bài học áp dụng cho MỌI việc "bám sát ảnh mẫu" sau này trong dự án:** ảnh mockup JPEG/PNG nén CHỈ
nên dùng để THAM KHẢO silhouette/bố cục/màu — không bao giờ crop trực tiếp làm sprite game thật, dù
"giống ảnh mẫu 100%" nghe hấp dẫn hơn. Vẽ lại bằng code (Pillow) luôn cho kết quả sắc nét, đúng phong
cách pixel-art nhất quán với phần còn lại của dự án.

## §3.7. Cả HUD Combat còn lại — 1 thay đổi đòn bẩy cao, phủ gần hết màn hình

Người dùng chốt "làm luôn hết HUD". Đọc lại `BattleHudScreen.cs` thấy **gần MỌI panel** (Hero/Enemy/
DamageMeter/Analyze/TurnOrder/SkillGrid/EndTurn/AutoSpeed) đều dựng qua 1 hàm dùng chung `Panel()`,
và `Panel()` lấy border từ 1 hàm dùng chung khác `BronzeFrameSprite()` — sửa ĐÚNG 1 chỗ này lan ra
toàn bộ HUD cùng lúc, thay vì phải sửa từng panel riêng lẻ.

- [x] `BronzeFrameSprite()` — đổi từ `Art/UI/Frames/pixel_bronze_frame` (khung nâu cũ) sang
      `Art/UI/Chrome/panel_gold` (khung vàng vẽ tay đã dùng cho thẻ bài).
- [x] `Panel()` — 2 sửa đi kèm bắt buộc:
      1. Inset lớp "Fill" bên trong từ 3px→**6px** (khớp đúng độ dày viền 6px của `panel_gold`,
         tránh viền bị lớp Fill che mất 1 nửa, trông như bị "cắt cụt").
      2. `border.color` đổi từ tint theo `accent` (mỗi panel 1 màu: Hero xanh lá, Enemy tím,
         Grid vàng) sang **`Color.white` cố định** — khung vàng giờ đồng nhất y hệt ảnh mẫu (ảnh
         mẫu KHÔNG phân biệt màu khung theo loại panel). Tham số `accent` vẫn giữ nguyên chữ ký hàm
         (không phá code gọi) nhưng không còn dùng cho border nữa.
- [x] `BuildTacticRow()` (5 nút GUARD/ESC/SWAP/FOCUS/ANALYZE) — đổi từ `MetalPanelSprite()` (phẳng)
      sang `button_gold_normal` (khung nút vàng đã dùng cho popup). Cách tint màu accent CŨNG đổi
      theo — trước nhân tối 25% (đúng cho panel phẳng, nhưng nhân lên khung vàng mới sẽ làm mất hẳn
      màu vàng, gần như đen), giờ `Color.Lerp(white, accent, 0.5f)` — vẫn phân biệt được 5 nút bằng
      màu (cyan/đỏ/vàng/tím/cam) mà khung vàng vẫn hiện rõ.
- [x] Verify Play mode thật — vào battle, chụp `manage_camera`: **TOÀN BỘ HUD** (Hero panel, Enemy
      panel, thanh Turn Order, DAMAGE panel, 5 thẻ skill, hàng item, 5 nút tactic, END TURN,
      AUTO ON) giờ dùng chung 1 tông vàng/đỏ mận nhất quán — không còn mảng nào "lạc tông" như trước.
      668/668 test xanh.

**Còn lại (chưa đụng, liệt kê rõ để không quên):**
- Avatar tròn (portrait hero/enemy) vẫn dùng `CircleSprite()` viền màu accent (xanh lá/tím) — CHƯA
  đổi sang viền vàng. Cân nhắc: có thể GIỮ NGUYÊN màu accent avatar (không phải panel) để phân biệt
  phe ta/địch trực quan — đây là 1 chỗ MÀU semantic thật sự hữu ích, khác với border panel (chỉ là
  trang trí, không mang thông tin).
- Thanh HP/SP/ULT (`Bar()`) vẫn dùng `MetalPanelSprite()` phẳng cũ làm khung — chưa đổi.
- `MetalPanelSprite()` (khung xám cũ) vẫn còn dùng làm lớp "Fill" nền tối bên trong mọi Panel — ẩn
  gần hết dưới màu tối nên không rõ, nhưng vẫn chưa "thay hẳn", để dành nếu cần polish thêm.

## §3.8. Thanh HP/SP/ULT + 6 màn Meta còn lại — "hoàn thiện luôn cả 2 việc"

Người dùng chốt tiếp: hoàn thiện (1) khung thanh máu còn dở dang từ §3.7, và (2) 7 màn Meta còn lại
(Settings đã làm qua code, còn 6 màn prefab: TeamSelect/HeroDetail/Summon/ChapterProgress/NodeChoice/
Inventory) chưa đụng tới.

- [x] `Bar()` (HP/SP/ULT trong `BattleHudScreen.cs`) — thêm hàm mới `HealthBarFrameSprite()` load
      `Art/UI/Chrome/healthbar_hp_frame` (sprite đã sinh sẵn từ §1 nhưng chưa lắp chỗ nào). `bimg`
      đổi từ `MetalPanelSprite()` (khung xám phẳng) → `HealthBarFrameSprite()`, màu từ tint tối gần
      đen → `Color.white`; inset lớp fill từ 1px→2px cho khớp viền mới.
- [x] `SettingsScreen.cs` — panel nền đổi từ `NewImage(..., PANEL_BG)` (màu phẳng) sang sprite
      `panel_gold` + `Image.Type.Sliced`.
- [x] 6 màn prefab — 1 công thức lặp lại qua `manage_prefabs modify_contents` (theo đúng hierarchy
      thật lấy từ `get_hierarchy`, không đoán): Panel chính → sprite `panel_gold` + Sliced + màu
      trắng; overlay màu con phủ kín bên trong (`InnerBlue` hoặc tương đương) → tắt hẳn
      (`set_active: false`); mọi nút hành động (Close/Back/Start/Ascend/Upgrade/Claim/Buy/Pull/Info)
      → sprite `button_gold_normal` + Sliced + màu trắng.
  - `UI_TeamSelect`: Panel, BackButton, StartButton.
  - `UI_HeroDetail`: Panel, InnerBlue (tắt), AscendButton, CloseButton, 5× UpgradeButton (Row_0..4).
  - `UI_Summon`: Panel, InnerBlue (tắt), PullOneButton, PullTenButton, CloseButton, InfoButton.
  - `UI_ChapterProgress`: Panel, InnerBlue (tắt), CloseButton, 5× ClaimButton (Row_0..4).
  - `UI_NodeChoice`: Panel, InnerBlue (tắt), CloseButton, 3× BuyButton (Row_0..2).
  - `UI_Inventory` (cấu trúc khác — 3 panel gốc riêng, không phải 1 Panel chung): `CharacterBox`,
    `CharacterBox/InnerBlue` (tắt), `InventoryGridBg`, `InventoryGridBg/Inner/ActionBg/CloseButton`,
    `StatsBg`. **Bug phát hiện qua screenshot thật**: `StatsBg/InnerGreen` — 1 overlay con y hệt
    `InnerBlue` nhưng tên khác (`Green` thay vì `Blue`) nên bị bỏ sót ở lượt sửa đầu (không nằm
    trong danh sách "InnerBlue" đi tìm) — StatsBg vẫn hiện viền xanh lá cũ dù Panel cha đã đổi màu
    vàng. Sửa: tắt `StatsBg/InnerGreen` riêng. **Bài học**: tên overlay con phủ panel KHÔNG cố định
    là "InnerBlue" — phải lấy `get_hierarchy` thật của TỪNG prefab trước khi sửa, không suy diễn
    tên từ prefab khác.
  - 24 ô `Grid/Slot_N` (icon item) trong `UI_Inventory` CHƯA đụng — vuông trắng trơn, độ ưu tiên
    thấp hơn, để dành nếu người dùng yêu cầu tiếp.
- [x] Verify Play mode thật từng màn 1 (`Resources.Load` + `Instantiate` trực tiếp, không qua
      `MetaSceneInstaller` vì phần lớn field private) + `manage_camera` screenshot:
      Settings, Inventory (bao gồm re-verify sau khi sửa `InnerGreen`), TeamSelect, HeroDetail,
      Summon, ChapterProgress, NodeChoice — cả 7 đều lên đúng khung vàng `panel_gold`/nút
      `button_gold_normal`.
  - **Phát hiện môi trường mới**: `Instantiate` 1 prefab rồi chụp `manage_camera` NGAY trong cùng
    lượt `execute_code` đôi khi trả về ảnh HOÀN TOÀN phẳng 1 màu (đúng bằng `Camera.backgroundColor`
    — xác minh qua đọc `MetaCamera.backgroundColor`), tức Canvas mới chưa kịp rebuild layout ở frame
    chụp. Không phải lỗi logic/sprite (kiểm `Image.sprite`/`color` qua code vẫn đúng 100%) — chỉ cần
    `Canvas.ForceUpdateCanvases()` (hoặc toggle `SetActive` panel) trước khi chụp lại là ra đúng.
  - TeamSelect: Panel chiếm full-bleed 880×480 (offset vượt khỏi vùng nhìn thấy của canvas ở độ
    phân giải nhỏ) nên viền 9-slice nằm ngoài khung hình, không thấy viền vàng quanh mép màn hình —
    xác nhận qua code (`sprite`/`color` đúng) đây là layout có sẵn từ trước, không phải lỗi mới.
- [x] `run_tests(mode:"EditMode")` sau toàn bộ đợt này: **668/668 xanh**.

## §3.9. Icon skill thật + sửa vị trí badge SP đè lên fringe — "UI Screen Battle chưa giống sample"

Người dùng phản hồi ngắn gọn sau khi xem lại Battle screen thật: "UI Screen Battle chưa giống
sample". Vào Play mode thật, ép 1 lượt hero thật xảy ra (`_autoPlay=false` trên cả
`BattleHudScreen`/`BattleSceneInstaller` qua reflection để dừng lại đúng lúc, không để AI tự chạy
hết trận trước khi kịp chụp), zoom vào đúng vùng thẻ skill — phát hiện đúng gốc rễ: khung thẻ
(`card_gold` 9-slice) đã lên màu vàng-mận đồng bộ từ §3.5, nhưng **icon BÊN TRONG khung vẫn là
glyph trắng phẳng 1 màu** (`Art/UI/Icons/Skills/icon_skill_*.png`, 9 file dùng chung theo
"archetype" từ `SkillSlotView.IconKeyFor()` — không phải icon riêng từng skill) — chưa từng có art
thật từ lúc dựng hệ thống, đây chính là phần đã được liệt kê "chưa đụng" ở cuối §3.7 nhưng chưa
làm. Đồng thời phát hiện thêm 1 bug thật qua zoom: badge số SP cost (`_cost`) đặt ở góc dưới-trái
(anchor y 0.02–0.18) đè thẳng lên dải tua rua (fringe) cong ở đáy `card_gold` — số bị cắt/lẫn vào
viền răng cưa, khó đọc.

- [x] `Tools/pixel-art-pipeline/scripts/draw_skill_icons.py` (mới) — vẽ lại cả 9 icon
      (`slash`/`power_strike`/`magic_bolt`/`heal`/`shield`/`haste`/`cleanse`/`aoe_burst`/`ultimate`)
      bằng Pillow thuần (không AI/không crop), đúng phong cách phẳng+viền tối 1px đã dùng cho
      `item_icons.py`/`nav_icons.py` — mỗi icon 1 tông màu riêng từ `Tools/palette.json` để vừa đẹp
      vừa phân biệt nhanh (thép/thường, cam-đỏ/mạnh, lam/phép, lá/hồi máu, lam-bạc/khiên,
      vàng/tốc độ, cyan/giải trừ, cam/diện rộng, vàng lớn+hào quang/ULT). Ghi đè trực tiếp lên 9 file
      PNG cũ tại đúng path cũ (`Assets/_Project/Resources/Art/UI/Icons/Skills/`) — giữ nguyên GUID/
      `.meta` (Point filter, Tight mesh, PPU 32 đã đúng sẵn từ trước), không cần sửa gì ở
      `Resources.Load` call.
- [x] `SkillSlotView.cs` — badge Cost (SP) chuyển từ góc dưới-trái (anchor y 0.02–0.18, đè lên
      fringe) sang **góc trên-trái** (anchor y 0.74–0.90, dưới vùng Label, đè nhẹ lên góc icon —
      kiểu "mana cost" quen thuộc trong game thẻ bài, không phải lỗi). `_elementGlyph` (glyph mù màu,
      cùng lỗi hệt Cost vì đối xứng nhau) sửa tương tự sang góc trên-phải.
- [x] Verify Play mode thật: zoom crop đúng vùng 5 thẻ skill sau khi ép 1 lượt hero thật — icon màu
      hiện rõ đúng loại kỹ năng, số cost (8/14/10) nằm gọn góc trên không còn đè lên fringe, badge
      cooldown tròn vẫn hoạt động bình thường. 668/668 test xanh (không đụng logic Combat, chỉ
      icon asset + vị trí RectTransform).

**Còn lại sau đợt này (art, không phải chrome, độ ưu tiên thấp hơn — không thuộc "UI"):** nền trận
đấu vẫn là tile gạch phẳng, không có khung cảnh khí quyển (rừng/hang) như ảnh mẫu; item slot (5 ô
nâu dưới hàng skill) vẫn trống khi hero test không có item — đây là dữ liệu, không phải bug hiển thị.

## §3.10. Redesign layout đợt 2 — "thiết kế đúng như ví dụ mẫu tôi gửi"

Sau §3.9, người dùng gửi lại đúng ảnh mẫu kèm yêu cầu thẳng: "thiết kế đúng như ví dụ mẫu tôi gửi".
Trước khi làm, hỏi lại phạm vi qua AskUserQuestion (3 lựa chọn: chỉ nâng cấp phần trực quan nhất /
redesign toàn bộ layout dùng dữ liệu thật / xây thêm dữ liệu game mới để khớp 100%) — vì ảnh mẫu là
1 dungeon-crawler khác hẳn gameplay turn-based JRPG của dự án (có Equipment loadout, bộ đếm
"SWAPS[3/3]", dải "STATS/DAMAGE 4-6" — data không tồn tại trong `CombatUnit`/`BattleState` hiện có).
**Người dùng chọn "Redesign toàn bộ layout"** — dùng dữ liệu thật sẵn có, KHÔNG bịa hệ thống mới,
KHÔNG crop ảnh mẫu (đúng [[feedback_no_jpeg_crop_sprites]] — luôn vẽ tay/procedural).

- [x] `Tools/pixel-art-pipeline/scripts/draw_pill_bar.py` (mới) — khung+fill thanh "viên thuốc"
      (`bar_pill_frame.png`/`bar_pill_fill.png`, 64×24, bán kính 12 = nửa chiều cao → 2 đầu bo tròn
      hoàn toàn khi 9-slice với border trái/phải = 12) thay khung chữ nhật `healthbar_hp_frame.png`
      cũ — đúng silhouette thanh HP/SP trong ảnh mẫu.
- [x] `BattleHudScreen.Bar()` — thêm overload MỚI `Bar(..., out TextMeshProUGUI valueText)` dùng
      sprite viên thuốc + text "cur/max" hiện NGAY GIỮA thanh (trắng, đậm) — chỉ dùng cho HP/SP hero
      (2 thanh quan trọng nhất, đáng có số lớn dễ đọc); overload CŨ (không số, khung chữ nhật) vẫn
      giữ nguyên cho ULT + 5 dòng HP địch (thanh nhỏ 150×7, viên thuốc bo tròn 12px sẽ méo/không
      còn đọc được ở kích thước đó — cố tình KHÔNG đổi 2 chỗ này).
- [x] `BuildHeroPanel()` viết lại: portrait TRÒN nhỏ (`BuildAvatar`, đã xoá) → portrait **VUÔNG**
      64×64 khung `panel_gold` (`BuildSquarePortrait`, mới) + bảng tên riêng ngay dưới portrait —
      đúng bố cục ảnh mẫu (portrait vuông là điểm nhấn chính, không phải viền tròn nhỏ cạnh chữ).
      Nguyên tố hero giờ hiện qua 1 chấm tròn nhỏ góc dưới-phải khung portrait (`CircleSprite()` cũ
      tái dùng, đổi mục đích) thay vì cả viền tròn đổi màu. HP/SP chuyển hẳn sang 2 thanh viên
      thuốc có số nhúng; `_heroStats` bỏ dòng "HP x/y SP x/y" (đã có trên thanh, tránh lặp), thêm
      "Lv{n}" vào đầu dòng ATK/DEF/SPD thay cho việc từng ghép vào tên (tên+Lv thường dài hơn bảng
      tên rộng 64px, luôn bị "..." cắt — xác nhận qua screenshot, sửa bằng cách tách Lv ra khỏi
      plaque tên). Panel cao 158→190 cho đủ chỗ bố cục mới.
- [x] `BuildItemColumn()` (mới) — 5 ô item tiêu hao dời từ hàng NGANG dưới thẻ skill (bên trong
      `SkillGrid`, hàng 1/3 cũ) sang **cột DỌC** riêng ở rìa trái màn hình, có tiêu đề "INV." —
      đúng cụm "INV." trong ảnh mẫu. Đặt vừa khít khe hở có sẵn giữa đáy `HeroPanel` (mới, cao hơn)
      và đỉnh `DamageMeterPanel` (cell=30/gap=3 để vừa đúng ~196px khe hở, tính tay từ 2 panel neo
      góc cố định). Logic Bind/Refresh/click (`RefreshItemSlots`, `HandleItemSlotClicked`) giữ
      NGUYÊN 100% — chỉ đổi nơi dựng UI, không đổi hành vi/dữ liệu.
- [x] `SkillGrid` rút từ 3 hàng (skill/item/tactic) xuống **2 hàng** (skill/tactic) sau khi item dời
      đi — `GRID_ROWS` 3→2, `BuildTacticRow()` bớt 1 hệ số nhân trong công thức `row2Y` (không còn
      hàng item chen giữa skill và tactic).
- [x] Verify Play mode thật (ép 1 lượt hero thật như §3.9): screenshot xác nhận cả 3 thay đổi hiển
      thị đúng — thanh HP/SP viên thuốc có số "753/828"/"86/86" rõ giữa thanh, portrait vuông khung
      vàng + chấm nguyên tố góc + bảng tên đầy đủ không bị cắt, cột "INV." dọc nằm gọn giữa 2 panel
      không đè lên bên nào, SkillGrid 2 hàng liền mạch. 668/668 test xanh.

**Cố tình CHƯA làm (đã báo người dùng trước khi bắt tay vào, qua AskUserQuestion):** nền trận đấu
(khung cảnh khí quyển như ảnh mẫu) — đây là art world-space/scene-authored (Battle.unity), không
phải HUD code, cần điều tra riêng an toàn hơn (tránh phá render pipeline scene) — CHƯA đụng tới,
vẫn là tile gạch phẳng cũ. Equipment loadout row / bộ đếm SWAPS[3/3] / dải STATS-DAMAGE range —
không có dữ liệu tương ứng trong `CombatUnit`/`BattleState`, người dùng đã tự chọn KHÔNG xây thêm hệ
thống mới cho phần này (chọn nhánh "redesign dùng dữ liệu thật", không chọn nhánh "thêm dữ liệu mới").

## §3.11. Nền khung cảnh trận đấu — "thêm nền khung cảnh cho battle screen"

Phần duy nhất bị hoãn ở §3.10 (art world-space, không phải HUD code). Điều tra `Battle.unity` qua
`manage_scene`/`execute_code` (không đoán) tìm ra: `BattleSceneInstaller/__Stage__/Background`
(`SpriteRenderer`, sprite `battle_arena_ember`, world size 16×9, sorting order −100) — file thật ở
`Assets/_Project/Resources/Art/Backgrounds/battle_arena_ember.png` (512×288, PPU 32). Đây chính là
"tường gạch phẳng + trời xanh lơ" thấy ở mọi screenshot — không có khí quyển như ảnh mẫu.

- [x] `Tools/pixel-art-pipeline/scripts/draw_battle_backdrop.py` (mới) — vẽ thủ tục bằng Pillow
      (KHÔNG AI, KHÔNG crop ảnh mẫu — đúng [[feedback_no_jpeg_crop_sprites]]): bầu trời gradient
      tím-mận tối có sao lấm tấm, 2 lớp núi/rặng cây silhouette ở chân trời (xa nhạt hơn, gần đậm
      hơn — tạo chiều sâu), nền đất gradient ấm dần xuống dưới + 1 quầng sáng mềm giữa nơi 2 phe
      đứng (thay campfire cứng cũ), vignette tối 4 góc.
- [x] Ghi đè TRỰC TIẾP `battle_arena_ember.png` (giữ đúng path/kích thước 512×288/GUID — không cần
      sửa `.meta` hay bất kỳ code C# nào, `SpriteRenderer` đã tham chiếu sẵn đúng sprite này).
- [x] **Bug thật phát hiện ở bản vẽ đầu tiên, tự sửa trước khi báo xong:** đặt đường chân trời quá
      thấp (row 168/288, gần đúng tỉ lệ ảnh cũ) khiến unit hero/enemy — tính ra qua
      `execute_code` đọc `transform.position` thật (Y ≈ −1.0 đến −1.8, camera ortho tâm Y=−1.6
      size 9 → quy đổi ra row ≈125–150) — LƠ LỬNG GIỮA BẦU TRỜI (rơi vào vùng row 0-168 = "trời")
      thay vì đứng trên "đất". Ảnh cũ không lộ bug này vì texture gạch tường trông "đặc" đều từ
      trên xuống, không có ranh giới trời/đất rõ như bản mới (sao + gradient) nên mắt không để ý.
      Sửa: tính ngược từ vị trí unit THẬT, đặt `HORIZON=95` (bầu trời chỉ còn ~1/3 ảnh) + quầng
      sáng dời tâm về đúng row 140 (giữa khoảng unit đứng) — verify lại qua screenshot Play mode
      thật: unit đứng rõ trong vùng đất ấm, dưới rặng núi, không còn lơ lửng.
- [x] Verify Play mode thật (queue battle 1 hero + 3 goblin, chụp ngay sau khi `LoadScene("Battle")`
      — càng ít round-trip trước khi chụp càng đỡ bị auto-battle tự đánh xong-quay-về-Meta trước khi
      kịp chụp, xem thêm ghi chú stale-frame ở §4). 668/668 test xanh (chỉ đổi 1 file PNG, không
      đụng code).

## §4. Ghi chú môi trường quan trọng (áp dụng cho MỌI việc UI sau này)

- `manage_camera screenshot` (kể cả không truyền `camera`) LUÔN tự chọn 1 Camera cụ thể để render —
  **BỎ QUA hoàn toàn mọi Canvas `Screen Space - Overlay`**, kể cả UI gốc của game
  (`MetaCanvas`/`UIRoot`). Muốn chụp ảnh xác minh bất kỳ UI Overlay nào (kể cả UI có sẵn từ trước,
  không riêng gì popup mới) → phải test qua world-space content HOẶC đổi tạm/vĩnh viễn sang
  `Screen Space - Camera`. Đây là phát hiện MỚI so với mọi ghi nhận trước đó trong dự án — bổ sung
  vào hiểu biết chung, không phải lỗi của popup system.
- Phiên Editor dùng chung với người dùng thật — 1 lần giữa chừng phát hiện đang ở Play mode dù mình
  vừa `stop` trước đó (người dùng tự bấm Play) — đúng mẫu "Shared Editor session quirks" đã ghi nhận
  nhiều lần, không phải bug.
- `manage_prefabs modify_contents` gọi dồn dập nhiều lệnh liên tiếp trong 1 message thỉnh thoảng gặp
  lỗi tạm thời "does not exist" (đụng độ ghi file) — gọi lại riêng lẻ là qua, không phải lỗi logic.
