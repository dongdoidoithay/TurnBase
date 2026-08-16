# Task: UI polish (theo UI_01/UI_02) + VFX skill đẹp hơn + animation mượt hơn

Yêu cầu người dùng (nguyên văn): "Sửa lại UI như UI_01, UI_02 các VFX combat skill sửa đẹp hơn.
animation mượt hơn". Qua AskUserQuestion (3 câu), người dùng chọn phạm vi TỐI ĐA cả 3 câu: (1) kết
hợp cả 2 phong cách UI_01 (bóng bẩy, gradient teal/gold, mobile RPG hiện đại) + UI_02 (pixel-art
khung gỗ/đá, dungeon crawler retro), (2) sửa TOÀN BỘ 11 màn UI, (3) VFX — cả cải thiện code
animation lẫn vẽ VFX mới. Việc RẤT LỚN, chắc chắn trải dài nhiều lượt "tiếp tục cho tôi" — viết task
file trước khi code, cập nhật liên tục (đúng quy ước dự án, xem task-animation-pilot.md làm mẫu).

## §0. Findings

- **`_Reference/UI_SAMPLE/UI_01.jpg` / `UI_02.jpg`** — đã xem trực tiếp. UI_01: panel bo tròn lớn,
  gradient teal-navy + gold, viền sáng/glow quanh card, nút bấm gloss 3D, banner ruy-băng, khung
  nhân vật lớn có ánh sáng rim. UI_02: hoàn toàn khác — khung gỗ/đá pixel-art thô, icon vuông nhỏ,
  bảng màu tối trầm, chữ pixel font. Hai phong cách xung khắc nhau về độ chi tiết (UI_01 mượt/
  gradient, UI_02 cứng/pixel) — quyết định kết hợp nghĩa là: giữ NỀN TẢNG pixel-art của dự án hiện
  tại (đúng identity game, KHÔNG chuyển sang smooth-shaded như UI_01) nhưng nâng cấp bố cục/glow/
  gradient-dithered kiểu UI_01 lên TRÊN pixel-art (viền sáng, gradient dithered, đổ bóng, banner)
  thay vì vẽ phẳng đơn sắc như UI_02 gốc.
- **KHÔNG có central theme/palette C# nào** — mỗi màn hardcode `Color` riêng (VD
  `BattleHudScreen.cs:34-44`). Không có prefab nút bấm chung. Xây theme tập trung là việc RIÊNG,
  đụng tới toàn bộ 11+ script màn hình — để dành 1 giai đoạn sau, KHÔNG làm ngay giai đoạn 1.
- **10/11 màn (trừ BattleHudScreen dựng code) + 2 widget prefab (`UI_HeroCard`, `UI_GearSlotRow`)
  dùng CHUNG 1 sprite** — `Assets/_Project/Art/UI/Frames/ui_rounded_panel.png` (9-slice, border
  14/14/14/14). **Đòn bẩy lớn nhất trong toàn bộ task**: nâng cấp riêng file này (giữ nguyên path/
  GUID, giữ nguyên border 9-slice) tự động cải thiện gần hết UI mà KHÔNG cần sửa prefab nào.
- **`BattleHudScreen.cs`** không dùng prefab — panel/border vẽ runtime bằng
  `RoundedSprite()` (texture 32×32 procedural, radius 9, bilinear) tô màu theo hardcode Color. Đây
  là màn hình chính lúc combat — nơi VFX skill cũng hiển thị — ưu tiên cao dù cách sửa khác (sửa
  code C# vẽ texture, không phải thay file .png).
- **Asset "chưa dùng" tồn tại nhưng CHẤT LƯỢNG THẤP, không dùng được**: `Art/UI/Buttons/
  btn_primary.png`/`btn_secondary.png`/`btn_endturn.png`, `Art/UI/Frames/frame_panel.png`/
  `frame_slot.png`/`frame_tooltip.png`/`frame_rarity_{common,rare,epic,legendary,mythic}.png`,
  các `bar_*.png` — đã xem trực tiếp 3 file: chỉ là hình chữ nhật phẳng 1-2 màu viền cam thô, KHÔNG
  đạt chất lượng "đẹp hơn" mong muốn. Không tái dùng trực tiếp — cần vẽ lại thật, không phải chỉ
  "wire vào" là xong.
- **`Tools/palette.json`** ("TurnBase 48", 12 nhóm 4 màu dark→light: neutral/purple, gray, orange/
  wood, red, blue, green, gold, violet, brown/tan, teal, brown/tan #2). Nguồn màu DUY NHẤT nên dùng
  cho mọi asset UI mới — có sẵn đủ family teal+gold (khớp UI_01) và wood/brown+stone-gray (khớp
  UI_02), không cần bịa màu mới.
- **Không có DOTween/animation package nào** — mọi "animation" UI hiện tại là `Mathf.Lerp`/
  `Color.Lerp` tay trong `BattleHudScreen.cs` (HP bar) và `SkillSlotView.cs` (pulse glow). Animation
  UI mượt hơn = viết thêm easing/tween tay (coroutine), KHÔNG thêm dependency mới (đúng nguyên tắc
  dự án, xem CLAUDE-level "don't add feature flags/deps khi có thể tự viết").
- **`VfxPlayer.cs` — ĐÃ SỬA giai đoạn 1 (xem §2 dưới)**: bản gốc chỉ swap 4 sprite theo
  `WaitForSeconds` cứng, KHÔNG scale/fade — "animation mượt hơn" cho VFX combat có căn cứ thật, không
  phải chỉ cảm tính.
- **VFX/animation nhân vật đã làm RẤT ĐẦY ĐỦ ở task-animation-pilot.md** (đọc lại trước khi tưởng
  đây là việc mới): 24/24 hero + 66/66 enemy/boss đã có rig 5-trạng-thái (idle/attack/move/damage/
  die), 13 VFX skill đã phủ đủ mọi element, 1 skill (`inferno_bulwark`) đã có HDR Bloom thật. Việc
  "VFX đẹp hơn" trong task NÀY nghĩa là: (a) polish playback code (đã làm), (b) cân nhắc mở rộng
  Bloom HDR sang nhiều skill Ultimate khác (không phải vẽ lại từ đầu).

## §1. Scope quyết định (qua AskUserQuestion)

**Trong phạm vi:**
1. VFX combat skill — cả code (easing/juice trong `VfxPlayer.cs`) lẫn xét mở rộng art khi cần.
2. UI — sửa TOÀN BỘ 11 màn (Shop/Codex/HeroDetail/Summon/Mail/TeamSelect/NodeChoice/Quest/
   Inventory/Tower/TrialBoss) + BattleHudScreen, theo hướng kết hợp UI_01 (gradient/glow/gloss) lên
   trên nền pixel-art hiện tại (không đổi hẳn sang UI_02 thô hay UI_01 smooth-shaded thuần).
3. Animation mượt hơn — cả VFX lẫn UI transition (panel mở/đóng, nút bấm phản hồi).

**Ngoài phạm vi (ghi rõ, không làm trong task này trừ khi được yêu cầu thêm):**
- KHÔNG vẽ lại nhân vật/enemy (đã đủ 5 trạng thái từ task-animation-pilot.md).
- KHÔNG thêm DOTween hay package animation ngoài.
- KHÔNG đổi cấu trúc code/luồng dữ liệu màn hình — chỉ đổi lớp trình bày (art + timing).

## §2. Giai đoạn 1 — VFX playback polish (code, không cần art mới): XONG

`Assets/_Project/Scripts/CombatView/Effects/VfxPlayer.cs` — bản cũ chỉ đổi sprite theo
`WaitForSeconds(FRAME_TIME)` cứng, bật/tắt `SetActive` không có chuyển tiếp. Đã thêm lớp "juice"
mượt PHỦ NGOÀI (không đụng/nhoè pixel gốc, đúng nguyên tắc đã chốt ở task-animation-pilot.md §3 —
bloom/juice phải tách lớp, không bake vào sprite):
- Scale "nảy vào" (ease-out-back, từ 0.55x vượt nhẹ qua 1x rồi ổn định) trong 35% đầu thời lượng.
- Alpha mờ dần (ease-in) ở 35% cuối thay vì cắt cứng `SetActive(false)`.
- Nội suy mỗi `Update()` (không phải mỗi lần đổi frame) — frame pixel-art vẫn đổi rời rạc đúng
  `FRAME_TIME`, chỉ transform (scale/alpha) mượt liên tục.
- Reset đầy đủ `color`/`scale`/`rotation` khi cấp phát từ pool (an toàn khi tái sử dụng object).

Verify: compile sạch (0 lỗi console), không có test nào tham chiếu `VfxPlayer` (grep xác nhận —
không có rủi ro regression test). Cần verify thêm bằng mắt (Play-mode) ở lượt sau.

- [x] Sửa `VfxPlayer.cs` — scale pop-in + fade-out, giữ nguyên API `Play(key, worldPos, scale)`.
- [ ] Verify Play-mode thật (screenshot 1 trận, xem VFX nảy/mờ mượt).
- [ ] Cân nhắc mở rộng `MATERIAL_OVERRIDES` (HDR Bloom) sang thêm vài Ultimate skill khác ngoài
      `inferno_bulwark` (cần chọn 1-2 skill đại diện, không làm hết 65 skill).

## §3. Giai đoạn 2 — UI kit dùng chung (đòn bẩy lớn nhất): XONG PHẦN LÕI

**Phát hiện quan trọng khi bắt tay vào:** `Assets/_Project/Art/UI/Frames/ui_rounded_panel.png`
KHÔNG chỉ là nền Panel — grep prefab thật xác nhận **CHÍNH sprite này được TẤT CẢ Panel/Fill/
BuyButton/CloseButton ở UI_Shop (và tương tự các màn khác) dùng chung**, mỗi nơi tự nhân màu riêng
qua `Image.color` (m_Type: 1 = Sliced). Ảnh gốc xác nhận bằng Read pixel thật: tâm ảnh
`(255,255,255,255)` — TRẮNG THUẦN. Đây là hệ "1 sprite neutral + tint theo Instance" chuẩn của
Unity UI. **Bài học sau 1 lần làm sai:** thử đầu tiên vẽ gradient MÀU THẬT (teal+viền vàng) trực
tiếp vào sprite — mô phỏng nhân màu bằng Python (`Image.color` thật của Panel=cam/BuyButton=xanh/
CloseButton=xám) cho kết quả BÙN, sai hẳn ý đồ màu gốc của từng màn. Sửa lại: vẽ sprite bằng
GRAYSCALE THUẦN (trắng→xám, viền bevel trắng/đen, không có hue riêng) — nhân với BẤT KỲ tint nào
vẫn ra đúng hue mong muốn, chỉ THÊM gradient/gloss/bevel/góc bracket mà bản trắng-phẳng-đơn-điệu cũ
không có. Verify bằng mô phỏng Python nhân đúng 4 màu tint thật lấy từ `UI_Shop.prefab` (cam/xanh/
xám/tím-đen) — cả 4 đều ra card gloss đẹp, đúng hue gốc, có chiều sâu/bevel/góc sáng rõ ràng.

- [x] Viết `Tools/pixel-art-pipeline/scripts/ui_glow_frame.py` (mới, không sửa `compose.py` gốc)
      — `make_glow_frame`/`make_glow_button`/`make_glow_button_flat`, tất cả NEUTRAL (trắng/xám,
      không bịa màu), gradient nội suy tuyến tính (không blur) + viền bevel 2 lớp + highlight cạnh
      trên + ngoặc góc sáng (bracket, thay notch đơn thuần của `compose.py` gốc).
- [x] Ghi đè `Art/UI/Frames/ui_rounded_panel.png` (giữ nguyên 64×64/border 14/GUID — 0 thay đổi
      .meta, 0 sửa prefab nào) — **tự động cải thiện Panel/Fill/Button ở CẢ 10/11 màn + 2 widget
      cùng lúc**, đúng đòn bẩy lớn nhất đã nhắm.
- [x] Ghi đè `Art/UI/Buttons/btn_primary.png` (64×28/border 6, neutral) — sẵn sàng nếu sau này có
      màn nào tách riêng khỏi hệ `ui_rounded_panel.png` dùng chung (hiện CHƯA có prefab nào tham
      chiếu file này, xác nhận qua grep — để dành, không lãng phí công vì asset thật đã tách khỏi
      logic, đổi lại rẻ).
- [x] `refresh_unity` assets — Unity nạp lại đúng texture mới (verify runtime: `Image.sprite.name
      == "ui_rounded_panel"`, đúng resource path).
- [x] Chạy full EditMode suite sau khi đổi asset — 632/632 xanh, không có gì gãy (đúng kỳ vọng, đây
      là thay đổi asset thuần, không đụng code logic).
- [~] Verify bằng mắt (Play-mode screenshot) — **BỊ CHẶN bởi lỗi môi trường đã biết** (task-
      animation-pilot.md từng ghi nhận "MCP Play-mode frame-stall"): `manage_camera screenshot`
      trong phiên MCP này luôn trả về ảnh từ `BattleCamera` (nền trận Battle.unity), KHÔNG bao gồm
      Canvas ScreenSpaceOverlay dù đã xác nhận qua code Canvas đó `active=true, sortingOrder=310`
      và `Time.frameCount` đang tăng thật (không đứng hình). Đã thử: mở Prefab Stage, instantiate
      trực tiếp trong Play mode, chờ thật (sleep) cho frame chạy — cả 3 cách đều không chụp được
      overlay UI, có vẻ giới hạn thật của công cụ chụp màn hình MCP trong môi trường này (không
      phải lỗi asset/code). **Đã verify thay thế bằng mô phỏng Python nhân màu chính xác theo giá
      trị tint THẬT đọc từ prefab** (xem trên) — đủ tin cậy vì thuật toán 9-slice + multiply-tint
      của Unity là phép toán đơn giản, xác định, không có yếu tố khó lường. **Người dùng dùng
      chung Editor session (feedback_shared_editor_session.md) — có thể tự mở UI_Shop hoặc chạy
      Play thật để xem trực tiếp, khuyến khích làm vậy để xác nhận bằng mắt thật thay vì chỉ tin mô
      phỏng.**

### §3.1. 2 lỗi layout THẬT phát hiện qua screenshot người dùng gửi trực tiếp — ĐÃ SỬA

Người dùng gửi 3 ảnh chụp Play Mode thật (không qua tool chụp của tôi — Editor session dùng
chung, xem [[feedback_shared_editor_session]]) — lộ 2 lỗi layout CÓ THẬT, không phải cảm tính:

1. **`UI_Quest`/`UI_Tower`/`UI_TrialBoss`/`UI_Dungeon.prefab` — khoảng trắng chết ~107px** giữa
   `WalletLabel` (subtitle "This week: X · All-time: Y") và `RowListContainer` (danh sách thưởng) —
   đúng khoảng trống rỗng thấy rõ trong ảnh popup TOWER/TRIAL BOSS người dùng gửi. Đo tay qua
   `execute_code`: cả 4 prefab (nhân bản từ 1 template) đều có `RowListContainer.anchoredPosition.y
   = -190` trong khi `WalletLabel` kết thúc ở `y ≈ -83` → dư ra ~107px không dùng. **Sửa: đưa
   `RowListContainer.y` về `-110`** ở cả 4 file (qua `PrefabUtility.LoadPrefabContents`/
   `SaveAsPrefabAsset`, không sửa YAML tay). `TowerScreen.cs`/`TrialBossScreen.cs` không đụng gì
   (chỉ đọc cấu trúc có sẵn, không set lại vị trí).
2. **`TopBar` (Boot.unity, tĩnh — KHÔNG phải prefab trong `Resources/Prefabs/UI/`) —
   `MailButton`/`InventoryButton`/`QuestButton` chồng lấn chữ lên nhau thật** (đo span tuyệt đối:
   Mail kết thúc ở x=5, Inventory bắt đầu ở x=-16 → chồng 21px; Inventory kết thúc x=74, Quest bắt
   đầu x=65 → chồng 9px) — đúng nguyên nhân mảnh chữ "HA" (còn sót của "MAIL" bị `InventoryButton`
   đè lên) thấy trong ảnh người dùng gửi. **Sửa: thu hẹp cả 3 nút (90/90/110 → 80/80/90px) + xếp
   lại vị trí sát nhau có khoảng cách 6-7px đều nhau** — verify bằng code tính lại span, 0 chồng
   lấn (gap dương ở mọi cặp liền kề: Codex→Mail=6, Mail→Inventory=6, Inventory→Quest=6,
   Quest→Summon=7). Lưu `Boot.unity` (phải `set_active_scene` trước — `manage_scene save` mặc định
   lưu SCENE ĐANG ACTIVE, không phải scene truyền tên, ghi chú lại cho lần sau).

**Phát hiện thêm, CHƯA sửa (ghi lại, không tự ý mở rộng thêm phạm vi mù không kiểm chứng được):**
- `TitleLabel` ("CHAPTER", `TopBar`) rộng 300px, neo CENTER — chồng lấn `InventoryButton`
  (span cũ [2,82] nằm trọn trong span Title cũ [-136,164]) → chữ "CHAPTER" gần như KHÔNG hiển thị
  trong ảnh người dùng gửi (bị `InventoryButton` đè/vẽ sau). Thử tính co hẹp Title vẫn không đủ vì
  toàn bộ cụm nút bên trái xếp gần đúng khu vực giữa bar — bar 780px không đủ chỗ cho 7 nút chữ +
  Title + Summon/Wallet/Settings ở độ rộng dễ đọc (đã tính tổng chiều rộng tối thiểu cần ~780-880px,
  vượt khung). **Kết luận: cần thiết kế lại TopBar bằng NÚT ICON (như UI_01 tham khảo thật dùng
  icon, không phải pill chữ dài) thay vì tiếp tục ép số — việc này cần vẽ icon mới, để dành giai
  đoạn riêng, không đoán mù thêm.**
- Không chụp được Play-mode screenshot qua tool của tôi trong suốt session (xem §3 — giới hạn môi
  trường). Toàn bộ Giai đoạn 3.1 verify qua: (a) đọc số RectTransform thật bằng `execute_code`
  (không đoán), (b) tính span/gap bằng code, (c) đối chiếu trực tiếp với ảnh thật người dùng gửi.
  632/632 test xanh sau khi sửa (thay đổi thuần vị trí/kích thước, không đụng code logic).

### §3.2. TopBar icon hoá — XONG (giải quyết dứt điểm, không chỉ chèn thêm chỗ)

Qua AskUserQuestion, người dùng chọn làm ĐỒNG THỜI cả icon TopBar lẫn rà soát 11 màn. Đã xong phần
icon trước (nhỏ hơn, xong gọn trong 1 lượt):

- [x] Vẽ 7 icon mới 24×24 (`Tools/pixel-art-pipeline/scripts/nav_icons.py`, Pillow thuần, NEUTRAL
      trắng — cùng triết lý tint như `ui_glow_frame.py`): tower (tháp răng cưa), trial (kiếm chéo),
      dungeon (cổng vòm), codex (sách), mail (phong bì), items (rương), quest (cuộn giấy). Xem qua
      Read (dựng ảnh strip 6× NEAREST) — đạt ngay lần đầu, không lặp.
      → `Assets/_Project/Art/UI/Icons/Nav/icon_{tower,trial,dungeon,codex,mail,items,quest}.png`.
- [x] Viết lại 7 nút TopBar (`Boot.unity`, tĩnh): thu nhỏ 90-110px → 42×40px đồng nhất, thêm child
      `Icon` (18×18, Point filter) phía trên + label cũ co còn caption 8pt phía dưới (không xoá,
      giữ text hỗ trợ đọc — không phải icon-only mù nghĩa). Xếp lại vị trí — verify bằng code tính
      span TUYỆT ĐỐI cho cả 8 phần tử (7 icon-button + SummonButton): **0 chồng lấn, gap dương mọi
      cặp (4px giữa các icon, 10px trước Summon)**.
- [x] Sửa luôn `TitleLabel` ("CHAPTER") — nguyên nhân bị `InventoryButton` đè mất tăm (§3.1): đổi
      neo từ CENTER (rộng 300px, đúng ngay giữa cụm nút) sang LEFT-anchor gọn 130px, đặt vào vùng
      trống bên trái cụm icon mới (giờ dư hẳn ra ~250px sau khi 7 nút co lại) — gap thật tới
      TowerButton = 107px, không còn chồng lấn.
      **Bài học:** thu hẹp Title tại chỗ (giữ center-anchor) KHÔNG đủ — trung tâm bar chính là nơi
      cụm nút hiện diện; phải ĐỔI HẲN kiểu neo (center→left) mới thật sự tách 2 khu vực.
- [x] `MailBadge` (badge số mail chưa đọc, con của `MailButton`) kiểm tra còn nguyên — neo góc
      (anchoredPosition nhỏ, không đổi theo kiểu absolute) nên tự động đúng vị trí dù nút mẹ co lại.
- [x] Lưu `Boot.unity` (nhớ `set_active_scene` trước — `manage_scene save` chỉ lưu scene ĐANG
      active, không nhận tham số chọn scene khác dù truyền `scene_name`). 632/632 test xanh.
- [ ] Verify bằng mắt — vẫn CHƯA chụp được overlay UI qua tool của tôi (giới hạn môi trường, xem
      §3.1). Người dùng nên tự xem TopBar mới 1 lần khi tiện.

### §3.3. Lỗi gốc thật sự của TopBar — ĐÃ SỬA: thiếu hẳn sprite (không phải chỉ do icon)

Người dùng gửi ảnh sau khi §3.2 xong + 1 ảnh tham khảo mới (HUD bar phong cách gỗ ấm, pill bo tròn
đồng bộ) kèm nhận xét "icon chưa sửa hết và không đều không chuyên nghiệp". Điều tra bằng
`execute_code` đọc thật `Image.sprite` của cả 10 phần tử TopBar (Fill + 9 nút) — **TẤT CẢ đều
`sprite=NULL`** (chưa bao giờ được gán `ui_rounded_panel.png` như 10/11 màn khác dùng, xem §3) —
TopBar từ trước tới giờ luôn vẽ bằng hình chữ nhật phẳng mặc định của Unity, không bo góc/viền/
gradient gì cả. Đây mới là nguyên nhân gốc "không chuyên nghiệp", không phải icon.

**Sự cố vận hành xen giữa (ghi lại làm bài học):** lần gán sprite ĐẦU TIÊN chạy trong lúc
`Application.isPlaying == true` (người dùng đã bấm Play ở phiên Editor dùng chung —
[[feedback_shared_editor_session]]) — `manage_scene save` báo "thành công" nhưng thực chất KHÔNG
ghi gì xuống đĩa (thay đổi Play-mode luôn mất khi Stop, kể cả khi lệnh Save "báo" thành công).
Phát hiện bằng cách đọc THẲNG file `Boot.unity` trên đĩa (grep `m_Sprite`) thay vì tin log tool —
thấy vẫn `fileID: 0`. Sửa: `manage_editor stop` trước, xác nhận `isPlaying=False` NGAY TRONG code
sẽ chạy (không chỉ trước đó), làm lại, rồi verify LẦN NỮA bằng cách đọc trực tiếp file trên đĩa
(đếm guid `ui_rounded_panel` xuất hiện đúng 10 lần) — lần này xác nhận thật đã lưu.
**Quy tắc rút ra: khi sửa scene tĩnh (Boot/Battle.unity) qua `execute_code`, luôn kiểm
`Application.isPlaying` ngay trong đoạn code sẽ ghi, và luôn verify kết quả `save` bằng cách đọc
lại file trên đĩa — không tin thông báo "saved successfully" của tool khi làm việc trong phiên
Editor dùng chung với người dùng.**

- [x] Gán `ui_rounded_panel.png` (Sliced) cho cả 10 Image (Fill + Tower/TrialBoss/Dungeon/Codex/
      Mail/Inventory/Quest/Summon/Settings) — giữ nguyên màu tint đã có sẵn của từng nút (vàng cho
      nav, tím cho Summon, nâu cho Settings, tối cho Fill) — tự động ra dạng pill bo góc/viền/
      gradient/gloss đúng ngôn ngữ chung với mọi màn khác, không cần chỉnh màu tay.
- [x] Verify bằng đọc trực tiếp `Boot.unity` trên đĩa (không qua GameObject.Find sống — tránh lặp
      lại sự cố Play-mode) — 10/10 đúng guid+fileID. 632/632 test xanh.
- [ ] Vẫn CHƯA xem được bằng mắt qua tool chụp của tôi (giới hạn môi trường cũ). Người dùng nên xem
      lại TopBar 1 lần — border 9-slice (14px) so với nút nhỏ 42px có thể hơi dày tỷ lệ, nếu thấy
      "viền quá to so với nút" thì cần 1 bản `ui_rounded_panel` border mỏng hơn riêng cho nút nhỏ
      (chưa làm, để dành nếu người dùng xác nhận cần).

## §3.4. SỰ CỐ NGHIÊM TRỌNG — mất dữ liệu 15 file UI, khôi phục 1 phần + dựng lại 11 file

**Nguyên nhân:** yêu cầu "100% giống UI_02" → cần bake màu bronze/navy thật vào
`ui_rounded_panel.png` (thay vì neutral trắng ở §3.2) → phải reset `Image.color` về trắng ở MỌI
prefab dùng chung sprite này (đúng hướng kỹ thuật, xem §3.2 lý do). Viết script Python regex tự
động reset — **bug 1**: mutate string trong lúc dùng `finditer` trên string GỐC (stale offset) làm
lệch vị trí, ghi đè sai chỗ → hỏng field `m_Color`/`m_RaycastTarget` (dữ liệu vẫn còn nhưng
lẫn/gãy). Phát hiện qua grep (không tin ngay), thử sửa tự động — **bug 2 (nặng hơn nhiều)**: regex
sửa dùng `re.DOTALL` khiến `.*?` khớp XUYÊN qua hàng trăm dòng không liên quan giữa 2 điểm mốc xa
nhau trong file → XOÁ MẤT phần lớn nội dung. Hậu quả xác nhận qua `git diff --stat` +
`AssetDatabase.LoadAssetAtPath`: `Boot.unity` 1506→206 dòng, 10/11 file `Screens/*.prefab` (trừ
`UI_Inventory` còn sót 1 phần) trở thành GameObject RỖNG (chỉ còn root, 0 children).

**Khôi phục:**
- 4 file có trong git (`Boot.unity`, `UI_TeamSelect.prefab`, `UI_GearSlotRow.prefab`,
  `UI_HeroCard.prefab`) → `git checkout` về `bc8c66f Initial commit` — khôi phục ĐÚNG cấu trúc gốc,
  nhưng cũng xoá theo mọi việc CHƯA COMMIT trước đó trên 4 file này (VD `UI_TeamSelect.prefab` có
  1 tính năng scroll hero-list — `RectMask2D`+`ScrollRect`+`HeroListViewport` — đang làm dở, mất
  luôn, cần làm lại nếu cần).
- 10 file KHÔNG có trong git, KHÔNG có Time Machine/backup nào truy cập được (đã kiểm tra kỹ:
  `com.unity.collab-proxy` có cài nhưng không có workspace Plastic/Unity VCS thật nào cấu hình,
  `tmutil` không thấy ổ backup nào mount) → **hỏi thẳng người dùng, được xác nhận "xây lại từ đầu"**
  (chấp nhận mất layout gốc, không giả vờ đó là "khôi phục"). Dựng lại bằng `execute_code` (Unity
  API thật — `GameObject`/`RectTransform`/`PrefabUtility.SaveAsPrefabAsset` — KHÔNG viết YAML tay
  nữa, loại bỏ hẳn rủi ro lặp lại sự cố) — đọc từng script điều khiển (`ShopScreen.cs`,
  `TowerScreen.cs`, `CodexScreen.cs`, `SummonScreen.cs`, `MailScreen.cs`, `NodeChoiceScreen.cs`,
  `HeroDetailScreen.cs`...) để lấy ĐÚNG path/tên GameObject bắt buộc, verify từng file bằng
  `transform.Find()` thật sau khi build (không đoán). Kích thước/canvas/layout dùng lại các giá trị
  ĐÃ ĐO ĐƯỢC thật từ đầu phiên (Panel 620×420, Row 330×30 spacing 34, v.v. — trùng khớp UI_Shop gốc
  49 object, xác nhận số liệu đáng tin) cho khung Quest/Tower/TrialBoss/Dungeon/Shop/Codex/Mail/
  Inventory/NodeChoice; riêng `UI_HeroDetail`/`UI_Summon` không có số liệu cũ, dựng layout MỚI hợp
  lý theo đúng yêu cầu field của script (không giống bản gốc, đã báo trước khi làm).
  **11/11 file dựng lại — verify từng path bắt buộc theo script = true, 632/632 test xanh.**

**Bài học (đã lưu vào memory `feedback_shared_editor_session.md`):**
1. KHÔNG BAO GIỜ dùng `re.finditer(text)` rồi mutate biến `text` trong vòng lặp dựa trên
   `match.start()/end()` — offset lệch ngay từ lần sửa thứ 2. Dùng `re.sub`/`re.subn` với callback
   (tự quản lý offset đúng) hoặc build list thay thế rồi áp dụng NGƯỢC từ cuối file lên đầu.
2. TUYỆT ĐỐI không dùng `re.DOTALL` cho quy tắc "khớp từ mốc A đến mốc B gần nhất" khi A/B có thể
   lặp lại nhiều lần trong file — không có gì chặn nó khớp xuyên qua nội dung không liên quan ở xa.
3. Sau MỌI script sửa hàng loạt trên file YAML thật (scene/prefab), verify bằng
   `git diff --stat` (nếu có git) HOẶC đếm dòng trước/sau (`wc -l`) — không chỉ tin "compile sạch"/
   "test xanh" (không đụng tới các prefab UI này).
4. Sửa hàng loạt Scene/Prefab nên làm qua Unity API thật (`execute_code`, `PrefabUtility`) thay vì
   sed/regex tay trên YAML — an toàn hơn nhiều, Unity tự đảm bảo serialize đúng.

### §3.5. `git checkout` phục hồi Boot.unity gây regression MỚI — đã sửa

Người dùng báo `NullReferenceException` tại `MetaSceneInstaller.BindCanvasRefs():886` (
`topBar.Find("SummonButton")` trả null). Điều tra: `bc8c66f Initial commit` (mốc `git checkout`
dùng ở §3.1) LÀ TỪ TRƯỚC KHI 8 nút TopBar (Summon/Quest/Dungeon/TrialBoss/Tower/Mail/Codex/
Inventory) + MailBadge từng được thêm vào — toàn bộ là việc CHƯA COMMIT của các phiên trước, không
chỉ "vài giờ làm dở" như đánh giá ban đầu ở §3.1. `git checkout` vô tình lùi xa hơn nhiều so với dự
kiến.

**Sửa:** dựng lại 8 nút + MailBadge/BadgeLabel qua `execute_code` (Unity API thật, verify
`isPlaying=False` ngay trong code trước khi sửa/lưu — đúng bài học §3.4) dùng ĐÚNG số liệu đã đo
thật đầu phiên trước khi có sự cố nào (`anchoredPosition`/`sizeDelta`/`fontSize`/text mỗi nút).
Verify bằng đọc trực tiếp `Boot.unity` trên đĩa sau khi save — đủ 11/11 tên xuất hiện đúng 1 lần.
`MapRoot`/`Toast`/`ToastLabel` (2 dòng tiếp theo trong `BindCanvasRefs`) xác nhận vẫn còn nguyên từ
initial commit — không cần dựng thêm. 632/632 test xanh.

**Bài học bổ sung:** `git checkout` về 1 commit cũ để "sửa lỗi mới gây ra" có thể lùi xa hơn dự kiến
rất nhiều nếu commit đó cũ hơn tưởng — LUÔN kiểm tra khoảng cách thời gian/tính năng giữa commit
đích và trạng thái hiện tại trước khi checkout (VD `git log --stat`/xem tuổi commit), không giả định
"initial commit" chỉ thiếu vài thay đổi gần nhất.

**Tiếp tục lộ ra sau khi người dùng Play thật (2 lượt nữa) — CÙNG 1 NGUYÊN NHÂN gốc (`git checkout`
ở §3.1 lùi quá xa), khác vị trí:**
- `TeamSelectScreen.BuildShell():127` NRE — `HeroListViewport/HeroListContainer` không tồn tại.
  `UI_TeamSelect.prefab` (1 trong 4 file dùng `git checkout`) mất tính năng cuộn hero-list
  (`RectMask2D`+`ScrollRect`, task cũ chưa commit, tên task `task_26720454` thấy trong comment code)
  — MAY MẮN đã tự chụp lại đúng cấu trúc này qua `git diff` LÚC PHÁT HIỆN sự cố (§3.4), nên dựng lại
  chính xác: bọc `HeroListContainer` cũ (giữ nguyên vị trí/kích thước 0,0/360×380) vào
  `HeroListViewport` mới (`RectMask2D` + `ScrollRect` dọc), `HeroListContainer` reset về (0,0) neo
  top-left làm `content` của ScrollRect. Verify đủ path `BuildShell()` cần, 632/632 xanh.
- `UI_GearSlotRow.prefab` (Widget, cũng bị `git checkout` lùi) thiếu hẳn `ReforgeButton` (tính năng
  Reforge sub-stat, task-phase-5-gaps.md Phần C, cũng chưa từng commit) — code
  `TeamSelectScreen.cs:413-436` cần `ReforgeButton`+`ReforgeButton/ReforgeLabel`. Dựng mới: row cao
  44→60 (khớp `rowH=60` code đã dùng để xếp chồng dòng), `EquipButton`/`EnhanceButton` dời lên góc
  trên-phải, `ReforgeButton` mới (174×26, màu xanh EQUIP_BLUE-ish) chiếm nửa dưới — không có số liệu
  gốc để đối chiếu (khác 2 trường hợp trên), tự thiết kế hợp lý dựa spec code. Verify đủ path,
  632/632 xanh. `UI_HeroCard.prefab` (widget còn lại bị checkout) — kiểm tra thấy ĐỦ mọi path
  `TeamSelectScreen.cs` cần, không có gì thiếu (may mắn không dính tính năng nào mới hơn commit).

**Bài học CHỐT:** sau 1 lần `git checkout` diện rộng, đừng chỉ tin "file load được là xong" — PHẢI
đối chiếu TỪNG path mà mọi script điều khiển liên quan (`.Find(...)`) yêu cầu, cho CẢ 4 file đã
checkout, trước khi báo "đã xong" — không đợi người dùng tự bấm trúng từng chỗ hỏng rồi báo tiếp.

**Vòng thứ 3 (đã tự chủ động soát thay vì đợi báo tiếp):** `TeamSelectScreen.RefreshHeroList():279`
NRE — `portraitRing.GetComponent<Button>()` null. Đợt kiểm trước ("vòng chốt" ở trên) CHỈ soát
`.Find(...) != null` (GameObject có tồn tại), KHÔNG soát ĐÚNG LOẠI COMPONENT script cần trên
GameObject đó — lỗ hổng thật: `PortraitRing` (bấm avatar mở `HeroDetailScreen`, tính năng thêm sau
initial commit) tồn tại trong `UI_HeroCard.prefab` nhưng THIẾU hẳn component `Button`. Sửa: thêm
`Button` (transition=None, đã có màu viền rarity riêng theo code, không cần hiệu ứng hover mặc
định). Đồng thời chủ động quét lại TOÀN BỘ `GetComponent<T>()` (không chỉ `Find`) trong
`TeamSelectScreen.cs` cho cả `UI_HeroCard`/`UI_GearSlotRow`/`UI_TeamSelect`, VÀ `HeroDetailScreen.cs`
cho `UI_HeroDetail` (màn mở tiếp theo khi bấm avatar — phòng lỗi trước khi bị báo) — tất cả còn lại
đều đúng loại component. 632/632 xanh.

**Bài học tinh chỉnh:** kiểm tra `.Find(path) != null` là CHƯA ĐỦ — phải kiểm cả
`.Find(path).GetComponent<T>() != null` đúng theo TỪNG dòng code gọi, vì 1 GameObject có thể tồn
tại (sống sót/dựng lại đúng tên) nhưng THIẾU component mà 1 tính năng SAU initial-commit đã gắn
thêm vào (không chỉ cấu trúc/tên object mới đáng ngờ — component thiếu trên object CŨ cũng đáng
ngờ y hệt).

### §3.6. Người dùng tự thiết kế UI_Inventory tay + đưa bộ texture pixel-art riêng làm chuẩn chung

Phát hiện `UI_Inventory.prefab` đổi cấu trúc hoàn toàn (`CharacterBox`/`InventoryGridBg` (Grid 24 ô)/
`StatsBg`, dùng TextMeshPro) — KHÔNG phải do tôi, mà người dùng tự sửa tay trong Editor dùng chung,
kèm 1 script Editor riêng (`PixelArtUIGenerator.cs`, không nằm trong dự án — chỉ nhận qua chat) sinh
3 texture pixel-art thật: `pixel_metal_panel.png` (32×32, border 8, xám kim loại), `pixel_bronze_frame.png`
(16×16, border 6, khung đồng viền TRONG SUỐT ở giữa), `pixel_green_panel.png`/`pixel_blue_panel.png`
(16×16, border 4). Đã HỎI TRƯỚC khi đụng gì (đúng bài học §3.4/§3.5 — không tự ý ghi đè việc người
khác đang làm) — được xác nhận 2 việc:

1. **Tôi sửa `InventoryScreen.cs` khớp cấu trúc mới** — viết lại hoàn toàn `BuildShell()`/`Refresh()`:
   gộp ITEMS+MATERIALS thành 1 danh sách 11 mục (bỏ hẳn tab, `SwitchTabButton` không còn tồn tại
   trong thiết kế mới); `StatsText` (TMP) hiện đủ tên+số lượng cả 11 mục (vì lưới icon CHƯA đủ ô có
   con "Icon" để hiện nhãn riêng — chỉ vài ô đầu có, code tự bỏ qua an toàn ô nào thiếu, không
   crash); tô màu phẳng phân biệt Item/Material tạm thay icon thật (CHƯA có asset icon riêng từng
   loại). **Giới hạn còn lại, không tự ý thêm:** `UI_Inventory.prefab` hiện KHÔNG có `CloseButton`
   nào — màn này chưa có cách người chơi tự đóng qua UI, cần người dùng tự thêm khi tiện.
   Thêm `"Unity.TextMeshPro"` vào `Game.Meta.asmdef` (trước đó chưa có, cần cho `TextMeshProUGUI`).
   Compile sạch, 632/632 xanh.
2. **Dùng bộ texture người dùng làm chuẩn chung** — copy `pixel_metal_panel.png` đè lên
   `ui_rounded_panel.png` (giữ nguyên GUID, chỉ đổi nội dung — cùng đòn bẩy §3.2/§3.3: MỌI Panel/
   Fill/Button ở 10 màn + TopBar tự động đổi sang texture mới, 0 sửa prefab), cập nhật `.meta`
   khớp ảnh mới (64×64→32×32, border 14→8, filterMode Bilinear→Point — đúng tinh thần pixel-art
   cứng nét của script người dùng, không dùng Bilinear như bản gradient cũ của tôi nữa). Verify
   `Sprite.rect`/`border`/`filterMode` qua `execute_code` đọc thật. 632/632 xanh.

Chưa làm (để dành, chưa được yêu cầu): gán `pixel_bronze_frame.png` (khung viền overlay) hay
`pixel_green_panel.png`/`pixel_blue_panel.png` (biến thể màu) vào vai trò riêng — hiện mọi nơi vẫn
dùng chung 1 texture metal xám qua tint như trước, chưa phân vai 4 texture theo ngữ cảnh khác nhau.

### §3.7. `UI_TeamSelect.prefab` bị thay cấu trúc LẦN NỮA (không phải người dùng cố ý) — dựng lại

Người dùng báo "mất hết logic chọn character" — kiểm tra thấy `UI_TeamSelect.prefab` (vừa dựng lại
scroll-view ở §3.4) giờ chỉ còn `CenterBox/InnerBlue/TitleText` (TMP) — khớp NGUYÊN VĂN kiểu đặt tên
"CharacterBox/InnerBlue/PlaceholderText" ở §3.6 (UI_Inventory), nhiều khả năng do 1 công cụ generate
chung bị chạy lại đè lên, không phải người dùng tự tay xoá (đã hỏi xác nhận). Dựng lại TOÀN BỘ
`Panel/Content/HeroListViewport(RectMask2D+ScrollRect)/HeroListContainer/GearPanelContainer(GearTitle+
SlotsContainer)/Footer(SelectedLabel+BackButton+StartButton/StartLabel)` qua `execute_code` (Unity
API, không YAML tay) — verify đủ path `TeamSelectScreen.cs` cần, 632/632 xanh.

**Rủi ro thật đang lộ rõ:** ít nhất 2 lần trong phiên này (`UI_Inventory`, `UI_TeamSelect`), 1 prefab
bị GHI ĐÈ TOÀN BỘ ngoài ý muốn giữa lúc làm việc — nghi vấn hợp lý nhất: công cụ/generator riêng của
người dùng (có `MenuItem` "Generate...") có thể đang tạo SCAFFOLD (CenterBox/InnerBlue placeholder)
cho NHIỀU prefab cùng lúc khi chạy, kể cả những cái đã có nội dung thật. **Khuyến nghị mạnh: commit
git sớm nhất có thể** — toàn bộ `Assets/_Project/Resources/Prefabs/UI/` hiện KHÔNG có lịch sử git
nào (xem §3.4), nghĩa là MỌI lần ghi đè ngoài ý muốn tiếp theo (dù do ai/công cụ nào) đều không có gì
để phục hồi ngoài dựng lại thủ công như 2 lần vừa qua.

### §3.8. 10 màn bị reset đồng loạt về scaffold "CenterBox/InnerBlue" — XÁC NHẬN chủ ý, xây nội dung thật

Ngay sau §3.7, phát hiện `UI_Shop.prefab` (và soát tiếp thấy CẢ 9 màn khác — Codex/Summon/Dungeon/
HeroDetail/Quest/Mail/TrialBoss/NodeChoice/Tower) đều bị reset về CÙNG 1 scaffold tối giản 4 object
(`CenterBox`[sprite `pixel_bronze_frame`, Sliced, neo stretch 5%-95%] > `InnerBlue`[sprite
`pixel_blue_panel`, Sliced, lấp đầy trừ 16px viền] > `TitleText`[TMP, cỡ 48, đã tự set đúng tên màn]).
Hỏi lại — người dùng XÁC NHẬN đây là bước RESET CHỦ Ý (tự tay hoặc qua tool) để bắt đầu build UI_02
thật từ nền sạch, không phải sự cố.

**Nhận ra scaffold này TỐT hơn hệ cũ của tôi:** `CenterBox` dùng `pixel_bronze_frame.png` làm khung
9-slice THẬT qua neo stretch + `Image.type=Sliced` (không phải ghép bitmap tay như tôi thử compose
Python trước đó — thử ghép tay bị lỗi viền đôi/rối, còn cách neo-stretch của Unity xử lý đúng, sạch).
`InnerBlue` lồng bên trong làm nền nội dung. **Quyết định: GIỮ NGUYÊN 2 lớp này, chỉ THÊM nội dung
mỗi màn cần vào bên trong** (không phá lại từ đầu theo hệ `ui_rounded_panel.png` cũ).

**Vướng mắc kỹ thuật phát hiện:** mọi script cũ (`QuestScreen.cs`, `ShopScreen.cs`,...) đều gọi
`_root.transform.Find("Panel")` — nhưng scaffold mới gọi lớp khung là `"CenterBox"`, không phải
`"Panel"` → đổi tên `CenterBox` → `"Panel"` (1 dòng, giữ nguyên mọi con bên trong kể cả `InnerBlue`)
ở TỪNG file — cách rẻ nhất khớp lại toàn bộ code cũ mà không phải sửa 10 script.

**Dựng nội dung thật (Unity API qua `execute_code`, KHÔNG YAML tay — đúng kỷ luật §3.4/§3.5) cho
đủ 10 màn, tái dùng bộ texture người dùng theo vai trò nhất quán:**
- `pixel_green_panel.png` — nút CLAIM/reward-type (hành động nhận thưởng, tích cực).
- `pixel_blue_panel.png` — nút BUY/CHOOSE/UPGRADE-type (hành động trung tính).
- `pixel_metal_panel.png` — nút CLOSE/phụ + nền hàng skill.
- `pixel_bronze_frame.png` — khung Portrait (HeroDetail), thêm 1 vai trò MỚI ngoài panel chính.
- Legacy `UnityEngine.UI.Text` cho nội dung ĐỘNG (giữ nguyên, khớp code cũ không sửa) — TMP
  `TitleText` gốc của scaffold giữ lại làm tiêu đề trang trí ở màn KHÔNG cần "Title" động qua code;
  2 màn CẦN "Title" động (`NodeChoice`/`HeroDetail`) tắt TMP cũ, thêm `Text` legacy tên "Title" riêng.
- Quest/Tower: 6 row. TrialBoss: 4 row (3 tier+1 attack). Dungeon: 4 row (KHÔNG cần WalletLabel —
  `DungeonScreen.cs` không gọi). Mail: 6 row + `ClaimAllButton`. Shop: 10 row (`BuyButton`). Codex:
  6 row (ẩn `ClaimButton`) + `SwitchTabButton`/`PrevButton`/`NextButton`. NodeChoice: 3 row + `Title`
  riêng + `WalletLabel`=mô tả + `CloseButton` nhãn "CONTINUE". Summon: không row —
  `PullOneButton`/`PullTenButton`/`ResultsText`. HeroDetail: đủ `Title`/`LevelLabel`/`ExpBar`
  (Fill kiểu Filled dùng `pixel_green_panel`)/`PortraitFrame`(bronze)+Mask+Sprite/`StatsContainer`
  (6 stat 2 cột)/`SkillListContainer`(5 dòng)/`AscendButton`/`CloseButton`.

**Verify:** đối chiếu ĐẦY ĐỦ path từng script cần qua `execute_code` cho cả 10 màn (không chỉ
`.Find()!=null` — cả đúng loại component, đúng bài học §3.4 vòng 3) — 100% khớp. Quét lại TOÀN BỘ
14 file 1 lần cuối (transforms + Panel) xác nhận ổn định, không file nào bị đè lại giữa chừng.
632/632 test xanh.

**Nhắc lại khuyến nghị committg git** — vẫn CHƯA có commit nào cho `Assets/_Project/Resources/
Prefabs/UI/` sau tất cả việc này; nếu có lần overwrite tiếp theo (dù chủ ý hay không), vẫn phải dựng
lại thủ công như 3 lần vừa qua.

### §3.9. Không phải UI_TeamSelect — 2 widget con (`UI_HeroCard`/`UI_GearSlotRow`) cũng bị reset

Người dùng báo lại NRE tại `RefreshHeroList():262` tưởng là `UI_TeamSelect.prefab` "chưa được
revert" — kiểm tra thấy `UI_TeamSelect.prefab` THẬT RA vẫn nguyên vẹn (15 transform, có `Panel`).
Thủ phạm thật: `UI_HeroCard.prefab` (widget 1 dòng hero, `Instantiate` từ `TeamSelectScreen.cs`)
cũng bị đợt reset ở §3.8 quét trúng — còn lại `UI_HeroCard > InnerBlue > NameText` (TMP), thiếu
`Fill`/`PortraitRing`/`LevelLabel`/`GearLabel`/`Toggle`. Sửa: đổi `InnerBlue`→`"Fill"` (khớp tên code
cần), tắt `NameText` TMP cũ, thêm đủ `PortraitRing`(bronze frame)+Mask+Sprite/`NameLabel`/
`LevelLabel`/`GearLabel`/`Toggle`(green)+`ToggleLabel` (legacy Text, khớp code cũ).

**Chủ động soát thêm** (đúng bài học §3.7 — 1 lần reset thường quét trúng NHIỀU file cùng lúc, không
chỉ file bị báo lỗi): `UI_GearSlotRow.prefab` cũng bị đổi nội dung (thành `Slot_0..4` — mẫu lưới ô
vuông không liên quan gì tới gear-row, có thể do tool áp nhầm 1 mẫu chung) — dù CHƯA kịp crash (nằm
sau bước hero-list trong luồng) nhưng chắc chắn sẽ hỏng ngay khi người dùng chọn xong hero. Xoá
`Slot_0..4`, dựng lại đúng `SlotNameLabel`/`ItemLabel`/`EquipButton`+`EquipLabel`/`EnhanceButton`+
`EnhanceLabel`/`ReforgeButton`+`ReforgeLabel` (glue: blue=Equip/Reforge, metal nâu=Enhance).

Verify đủ loại component (không chỉ tồn tại object) cho cả 2 widget, 632/632 xanh.

### §3.10. TopBar đồng nhất màu + icon leader + icon nhân vật ở Codex

Yêu cầu: "sửa lại cho ngay ngắn giống product hơn và bổ sung icon charactor. UIRoot -> Topbar cũng
sửa lại cho đồng nhất". Hỏi rõ vị trí icon nhân vật qua AskUserQuestion — chọn CẢ HAI (Codex + TopBar).

- [x] **TopBar đồng bộ màu/texture** — `Fill` trước đó `sprite=none` (màu phẳng, không khớp 10 màn
      dùng texture pixel-art) → wire `pixel_metal_panel`. Nút điều hướng (Tower/TrialBoss/Dungeon/
      Mail/Codex/Inventory/Quest) đổi từ tint vàng phẳng trên `ui_rounded_panel` → sprite
      `pixel_blue_panel` trắng (khớp ngôn ngữ "hành động trung tính" đã dùng ở BuyButton/UpgradeButton
      trong 10 màn). `SummonButton` (tím cũ) → `pixel_green_panel` (khớp "hành động tích cực/thưởng"
      của ClaimButton). `SettingsButton` giữ `pixel_metal_panel` tint xám (phụ).
- [x] **Icon leader trên TopBar** — `LeaderPortrait` mới (khung `pixel_bronze_frame` 36×36 + Mask +
      Sprite), đặt ngay sau `TitleLabel` (x=150, vùng trống đo thật còn 107px trước cụm nút Tower).
      `MetaSceneInstaller.cs`: field `_leaderPortrait`, bind trong `BindCanvasRefs()`, method mới
      `RefreshLeaderPortrait()` (đọc `_profile.Heroes[0].DefId`, load portrait cùng quy ước
      `HeroDetailScreen`) gọi trong `RefreshMap()` (chạy mỗi lần vào Meta/đổi map — đủ tần suất, hero
      đầu roster hiếm khi đổi nên không cần hook vào mọi callback đổi profile).
- [x] **Icon nhân vật trong Codex** — thêm `Icon` (Image) vào cả 6 `Row_i` (đẩy `NameLabel` sang phải
      34px nhường chỗ). `CodexScreen.cs`: field `_icons[]`, bind trong `BuildShell()`, helper mới
      `SetIcon(icon, folder, defId, unlocked)` — dùng ĐÚNG quy ước portrait tĩnh
      `Art/Characters/{Heroes,Enemies}/{defId}/{defId}_v1_00` (xác nhận qua `find` thật: enemy CŨNG
      có file này, không chỉ hero) — ẩn icon khi chưa mở khoá (khớp tinh thần "???" của tên, không lộ
      hình trước khi gặp/sở hữu thật).
- [x] Compile sạch, verify path/component thật qua `execute_code` (không đoán), 632/632 xanh.

### §3.11. TopBar full-width + icon-only + phát hiện thêm 1 chỗ bị revert âm thầm

Yêu cầu: "TopBar thay các text bằng các icon và thanh bar kéo full độ rộng màn hình". Kiểm tra
trước khi sửa (đúng kỷ luật đã rút ra) — phát hiện các nút TopBar đã tự lặng lẽ quay về bản CHỮ +
kích thước gốc to (90×44...), KHÔNG còn icon compact 42×40 đã làm ở §3.2 — do `git checkout` khôi
phục Boot.unity ở §3.4 dùng số liệu ĐÃ ĐO TỪ ĐẦU (bản chữ gốc) để dựng lại phần bị mất, không phải
áp lại bản icon (lúc đó ưu tiên sửa crash trước, chưa quay lại áp lại icon — nay làm luôn).

- [x] `TopBar` chuyển từ neo cố định 780px → neo stretch 2 mép (`anchorMin={0,1}`, `anchorMax={1,1}`)
      — tự co giãn theo màn hình thật thay vì để trống lề 2 bên (90px mỗi bên trước đó).
- [x] Vẽ thêm 2 icon mới (`icon_gear.png`, `icon_summon.png`, cùng `Tools/pixel-art-pipeline/scripts/
      nav_icons.py`) — đủ bộ 9 icon cho toàn bộ nút TopBar.
- [x] Toàn bộ 9 nút (Tower/TrialBoss/Dungeon/Codex/Mail/Inventory/Quest/Settings/Summon) chuyển
      hẳn icon-only — xoá `Label` chữ, `Icon` full khung trừ lề 7px. Tính lại vị trí tận dụng
      chiều rộng dư ra (960 thay vì 780) — verify 0 chồng lấn qua code tính span tuyệt đối (như mọi
      lần trước), không đoán.
- [x] **Phát hiện thêm khi verify:** `TitleLabel` CŨNG đã âm thầm quay lại kiểu neo CENTER rộng
      300px (đúng lỗi đã sửa 1 lần ở §3.2, bị `git checkout` cuốn theo) — nếu không bắt kịp sẽ đè
      lên cả cụm icon trái lẫn `WalletLabel` khi bar full-width. Sửa lại y hệt lần trước (neo trái,
      rộng 130px).
- [x] Lưu `Boot.unity`, verify trên đĩa (đếm GUID icon mới + `anchorMin/anchorMax` TopBar), 632/632
      xanh.

Chưa làm (nhỏ, để dành nếu cần): `WalletLabel` vẫn thuần chữ số (Gold/Gem) — chưa có icon đồng
tiền/gem riêng đặt trước số, do chưa có asset icon currency.

## §4. Giai đoạn 3 — layout 11 màn (yêu cầu MỚI, mở rộng phạm vi) — ĐANG LÀM

Qua AskUserQuestion, người dùng chọn làm CẢ 11 màn (không chỉ vài màn ưu tiên), thứ tự đề xuất:
TeamSelect → Shop → Inventory → HeroDetail → Summon → còn lại (Mail/Quest/Codex/NodeChoice/Tower/
Dungeon/TrialBoss — riêng Tower/TrialBoss/Dungeon đã có sẵn bố cục dùng chung, xem §3.1, chỉ cần
polish thêm không phải làm lại từ đầu). Việc RẤT LỚN — chưa bắt đầu màn nào ở giai đoạn này, ghi
tiến độ tại đây mỗi khi xong 1 màn, KHÔNG đợi xong hết 11 màn mới cập nhật.

Người dùng nhắn thêm giữa lượt (nguyên văn): "Sửa luôn bố cục layout cho các màn hình chuyên nghiệp
như mẫu UI_01, UI_02" — mở rộng từ "chỉ đổi màu/texture" sang **đổi cả BỐ CỤC** (cấu trúc
GameObject/RectTransform trong prefab, không chỉ tô lại sprite có sẵn). Việc này LỚN HƠN nhiều
Giai đoạn 2 — mỗi màn cần thiết kế lại cấu trúc riêng (VD `UI_Shop` hiện chỉ là danh sách dọc
NameLabel+BuyButton phẳng, không giống layout dạng lưới/card có icon của UI_01) và có thể cần thêm
asset icon/khung mới cho từng loại nội dung — CHƯA bắt đầu, cần 1 lượt riêng để khảo sát từng màn
+ thiết kế bố cục mới trước khi sửa prefab hàng loạt. Ghi tiến độ tại đây khi bắt đầu — nhóm theo độ
ưu tiên (Battle HUD trước, vì đó là nơi VFX cũng đang sửa cùng lúc > TeamSelect/HeroDetail > Shop/
Inventory > còn lại), cập nhật checklist mỗi lượt, không đợi xong hết 11 màn mới cập nhật 1 lần.

### §4.1. `UI_TeamSelect` — 2 lỗi CÓ THẬT người dùng báo ("thiếu icon hero", "gear bị xô lệch") — ĐÃ SỬA

Người dùng báo 2 việc, cả hai xác nhận là bug thật qua `execute_code` đọc/dựng `TeamSelectScreen`
thật với `LocalPlayerRepository.CreateNew()` và cả save file thật trên đĩa (24 hero, DefId khớp
100% art folder — loại trừ giả thuyết thiếu asset), KHÔNG đoán:

1. **`UI_GearSlotRow.prefab` root có sẵn 1 `HorizontalLayoutGroup`** (`childControlWidth/Height=True,
   childForceExpandWidth/Height=True, spacing=10`) — KHÔNG do tôi hay lịch sử task này thêm (không
   khớp bất kỳ note nào ở §3.9). Component này CHỈ tác động sau 1 lần layout pass thật (không thấy
   ngay lúc `Instantiate` — phải gọi `Canvas.ForceUpdateCanvases()` mới lộ), nên các lần verify
   trước bằng cách đọc `RectTransform` ngay sau dựng đều "xanh" giả. Verify thật: dựng `SlotsContainer`
   sống rồi ép layout — 5 phần tử (`SlotNameLabel/ItemLabel/EquipButton/EnhanceButton/ReforgeButton`)
   bị ép co giãn ngang thành 1 dải chồng khít nhau, hoàn toàn khác bố cục 2 tầng đã thiết kế — đúng
   "xô lệch" người dùng thấy. Sửa: xoá hẳn `HorizontalLayoutGroup` qua `PrefabUtility.LoadPrefabContents`
   (không sửa YAML tay, đúng kỷ luật §3.4).
2. **Lỗi thứ 2, nặng hơn, PHÁT HIỆN THÊM khi đo (không phải người dùng báo riêng)**: dù không có
   `HorizontalLayoutGroup`, 6 dòng gear × `rowH=60` (giá trị đã "chốt" ở task-phase-5-gaps.md) vẫn
   ĐÈ vào `FormationRow` runtime — đo qua `GetWorldCorners()` sau khi dựng màn thật: dòng cuối kết
   thúc ở content-top offset 392, `FormationRow` bắt đầu ở 374 → chồng 18px. Nguyên nhân: `Content`
   SỐNG hiện tại cao 456px, khác con số 480px dùng để chốt `rowH=60` trước đây (lệch do các lần
   scaffold-reset §3.7-3.9 dựng lại `UI_TeamSelect.prefab` không đo lại ràng buộc `FormationRow` —
   component này được TẠO Ở RUNTIME bởi script, không nằm trong prefab nên ai dựng lại prefab cũng
   không thấy được để tránh). Sửa đồng bộ: `rowH` 60→52 (cả hằng số trong
   `TeamSelectScreen.RefreshGearPanel()` lẫn `sizeDelta` gốc của `UI_GearSlotRow.prefab`), co lại
   `EquipButton/EnhanceButton/ReforgeButton` (26px→20px cao) + dịch `ReforgeButton` lên khớp. Verify
   lại bằng đúng phép đo trên: dòng cuối kết thúc ở 368, `FormationRow` bắt đầu 374 → cách 6px, hết
   chồng lấn. **Rủi ro tương tự đã xảy ra 1 lần trước (task-teamselect-start-button-fix.md) — lần
   này khác chỗ (gear panel thay vì footer) nhưng CÙNG 1 nguyên nhân gốc: `FormationRow` không nằm
   trong prefab nên mọi lần dựng/sửa prefab UI_TeamSelect cần đo lại ràng buộc này bằng
   `execute_code`, không suy diễn từ số cũ.**
3. **"Thiếu icon hero"** — hỏi lại người dùng qua `AskUserQuestion` (icon nhỏ 52px trong danh sách
   hero bên trái ĐÃ hoạt động đúng, verify bằng dựng màn thật với save file thật) — xác nhận ý người
   dùng là **bảng Gear bên phải hoàn toàn KHÔNG có chân dung hero nào**, chỉ có chữ tiêu đề. Thêm
   `GearPortrait` (44×44, tái dùng đúng mẫu `PortraitRing/PortraitMask/PortraitSprite` của
   `UI_HeroCard` — khung `pixel_bronze_frame`, `Mask` ẩn `showMaskGraphic`) vào đầu `GearPanelContainer`,
   dịch `GearTitle` sang phải + cao lên khớp dải header 52px mới. `TeamSelectScreen.cs`: field
   `_gearPortraitImg`, bind trong `BuildShell()`, set `sprite`/`enabled` trong `RefreshGearPanel()`
   theo hero đang xem (tắt khi không có hero nào được chọn). **Bắt lỗi tay 1 lần khi làm**: đặt sai
   dấu `anchoredPosition.y` của `SlotsContainer` (dùng `+56` thay vì `-56` — pivot/anchor TOP nghĩa
   là Y càng ÂM càng xuống thấp) khiến dòng gear đầu tiên đè lên header mới — bắt được NGAY nhờ đo
   lại bằng `execute_code` sau mỗi bước thay vì tin theo logic tay, sửa lại đúng.
4. **Phát hiện thêm khi verify text**: đo `Text.cachedTextGeneratorForLayout.GetPreferredHeight`
   thật cho `ItemLabel` (2 dòng: rarity+tên, main+sub-stat) — dòng 2 dòng chuẩn (1-2 sub-stat, phổ
   biến nhất) cần ~34px chiều cao, trong khi bản gốc TỪ TRƯỚC (60px-row, `ItemLabel` cao 32) đã THIẾU
   2px, và bản đầu tôi thử co (30px) thiếu tới 4px. Chỉnh lại `SlotNameLabel`/`ItemLabel` để
   `ItemLabel` được 33px (chỉ thiếu ~1px so với nhu cầu — mức chấp nhận được, tốt hơn bản gốc). Món
   đồ Mythic tối đa 4 sub-stat (hiếm, không phải ca thường gặp) cần tới 51px — không đủ chỗ ngay
   cả ở thiết kế GỐC, đây là giới hạn tồn tại từ trước, KHÔNG phải regression của lần sửa này, ghi
   nhận lại nhưng không mở rộng phạm vi sửa thêm (chưa được yêu cầu, cần 1 thiết kế row khác hẳn để
   xử lý — ví dụ chữ nhỏ hơn hoặc rút gọn hiển thị sub-stat — để dành).

**Verify:** dựng `TeamSelectScreen` thật qua reflection (cả với `LocalPlayerRepository.CreateNew()`
lẫn dữ liệu chính xác từ save file thật trên đĩa), đo `RectTransform`/`GetWorldCorners()` sau
`Canvas.ForceUpdateCanvases()` (không tin số liệu TRƯỚC khi layout chạy — bài học từ lỗi #1), đo
`Text.cachedTextGeneratorForLayout` cho `ItemLabel` ở cả 4 rarity. `refresh_unity` compile sạch,
0 lỗi console. **632/632 test xanh** (không đổi khỏi baseline — thay đổi thuần UI/prefab, không đụng
logic có test).

Chưa làm (để dành, chưa được yêu cầu thêm): rút gọn hiển thị `ItemLabel` cho món Mythic nhiều
sub-stat nhất (xem mục 4 trên); các phần còn lại của "chuyên nghiệp như UI_01/UI_02" (bố cục tổng
thể TeamSelect, không chỉ 2 lỗi này) vẫn CHƯA làm — đây chỉ là sửa 2 bug cụ thể người dùng báo,
không phải làm hết Giai đoạn 3 cho màn này.

### §4.2. Audit "action UI không gắn chức năng nào" (người dùng hỏi giữa lượt) — 1 lỗi thật, đã sửa

Người dùng hỏi thẳng: "các action trên UI phải gắn với 1 chức năng cụ thể, nhiều chức năng trên UI
đang không gắn với chức năng nào cả?" — audit thật (không đoán): grep mọi field `Button` +
`onClick.AddListener` trong cả 13 script màn hình (`ShopScreen`/`InventoryScreen`/`HeroDetailScreen`/
`SummonScreen`/`MailScreen`/`QuestScreen`/`CodexScreen`/`NodeChoiceScreen`/`TowerScreen`/
`DungeonScreen`/`TrialBossScreen`/`TeamSelectScreen`/`SettingsScreen`) + `MetaSceneInstaller`
(9 nút TopBar) + grep tên GameObject `*Button*` trong toàn bộ 14 prefab UI, đối chiếu 2 chiều.

**Kết quả:** 12/13 màn — MỌI `Button` field đều có `onClick.AddListener` thật, không có nút nào
"chết" (mồ côi) trong prefab lẫn code. **1 lỗi thật duy nhất: `UI_Inventory.prefab`** — hoàn toàn
KHÔNG có `Button` nào (grep tên GameObject xác nhận 0 match), dù `InventoryScreen.cs` có sẵn
`ActionBg` (dải nền dưới đáy `Grid`, rõ ràng dựng sẵn để chứa nút) nhưng **0 con bên trong** —
`InventoryScreen.Close()` tồn tại nhưng KHÔNG có đường gọi nào (xác nhận thêm:
`MetaSceneInstaller._inventoryButton` gọi `_inventoryScreen.Open(_profile, null)` — callback đóng
cũng `null`) — người chơi mở Inventory ra thì KHÔNG có cách nào tự đóng qua UI.

**Đã sửa:** thêm `CloseButton` thật vào `ActionBg` (`pixel_metal_panel` — đúng vai trò "CLOSE/phụ"
đã định ở §3.8) qua `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset` (không sửa YAML tay).
`InventoryScreen.cs`: field `_closeButton` mới, bind `ActionBg/CloseButton` trong `BuildShell()`,
`onClick.AddListener(Close)`. Verify: `validate_script` 0 lỗi, `refresh_unity` force+compile 0 lỗi
console, **632/632 test xanh**.

**Ghi nhận thêm, CHƯA sửa (thuộc phạm vi redesign Inventory đầy đủ ở lượt sau, không phải action mồ
côi):** `CharacterBox` (nửa trái màn, ~47% bề rộng) hiện chỉ có `PlaceholderText` hiện chữ
"INVENTORY" — không có chân dung/stat hero nào dù không gian đủ lớn; `Grid` 24 ô nhưng chỉ 5 ô đầu
(`Slot_0..4`) có con `Icon` thật, 19 ô còn lại là placeholder trống vô hại (không phải bug — do
`GridLayoutGroup` tự xếp lúc runtime, không phải chồng lấn).

### §4.3. `UI_Shop` — chuyển từ list phẳng 10 dòng sang lưới thẻ (card grid) 2×5 — XONG

Bố cục cũ: `RowListContainer` xếp dọc 10 `Row_i` (chỉ `NameLabel`+`BuyButton`, không nền, không
icon) — đúng kiểu "danh sách" bị chê trong §4 (yêu cầu người dùng: bố cục chuyên nghiệp như
UI_01/UI_02, không phải danh sách phẳng).

- [x] Đo hình học thật bằng `execute_code` trước khi sửa (đúng kỷ luật đã rút ra nhiều lần) —
      phát hiện `WalletLabel`/`RowListContainer`/`CloseButton` là con của `Panel`, KHÔNG phải con
      của `InnerBlue` như đoán ban đầu (gây 1 lần NRE khi thử `innerBlue.Find("RowListContainer")`
      — sửa lại đúng cha `panel.Find(...)`).
- [x] Tính lại toàn bộ layout bằng số (không đoán): `InnerBlue` 848×470 (Panel 864×486 trừ viền
      16px, Panel = 90% của Canvas 960×540 cố định) → Title chiếm 15% trên cùng, `WalletLabel`
      giữ nguyên vị trí cũ (đã đủ cách Title), lưới thẻ 2 cột × 5 hàng bắt đầu ngay dưới Wallet
      (gap 12px) và kết thúc trên `CloseButton` (gap 16px) — verify không chồng lấn bằng cộng trừ
      tay trên toạ độ local `InnerBlue` (tâm 0, nửa cao 235), không cần đoán qua `GetWorldCorners`
      vì không có `LayoutGroup` nào can thiệp (đã kiểm `GetComponents<Component>()` trên cả
      `RowListContainer` lẫn `Row_0` — chỉ có `RectTransform`/`Image`, an toàn hơn hẳn trường hợp
      từng gây lỗi ở TeamSelect gear row).
- [x] Mỗi `Row_i` (380×46) nay là 1 THẺ thật: nền `pixel_metal_panel` tint tối bán trong suốt
      (tách khỏi nền `InnerBlue`), `IconSlot` mới (34×34, `pixel_blue_panel`) tint theo loại —
      4 dòng đầu (Essence/Core, giá Gem) tím `MATERIAL_TINT`, 6 dòng sau (vật phẩm tiêu hao, giá
      Gold) xanh dương `ITEM_TINT` — dùng ĐÚNG 2 màu đã chốt ở `InventoryScreen` cho nhất quán
      toàn game (ánh xạ tĩnh theo thứ tự `CATALOG` cố định trong `ShopScreen.cs`, không cần đổi
      code vì thứ tự không đổi runtime). `NameLabel`/`BuyButton` co lại vừa khung thẻ hẹp hơn
      (fontSize 12→10, `NameLabel` bật `Wrap` phòng tên dài "Elemental Bomb").
      Không cần sửa `ShopScreen.cs` — mọi path `Find()` cũ (`NameLabel`/`BuyButton/Label`/
      `CloseButton`) không đổi tên/cấp cha.
- [~] Verify bằng mắt: thử `open_prefab_stage` + `manage_camera screenshot` — gặp lại ĐÚNG giới
      hạn môi trường đã ghi ở §3 (trả về `BattleCamera`/nền Battle scene thay vì Canvas prefab
      đang mở isolation) — không phải lỗi mới. Verify thay thế bằng số liệu hình học thật (trên) +
      xác nhận không có `LayoutGroup` gây bất ngờ.
- [x] `validate_script` 0 lỗi (không đụng code), `refresh_unity` force+compile 0 lỗi console,
      **632/632 test xanh**.

### §4.4. `UI_Inventory` — `CharacterBox` từ chữ phủ kín sang leader showcase thật — XONG

Tiếp nối §4.2 (đã sửa `CloseButton`) — phần còn lại đã ghi nhận nhưng chưa sửa: `CharacterBox`
(gần nửa màn) trước đó chỉ có `PlaceholderText` với `anchorMin=(0,0)/anchorMax=(1,1)` (phủ KÍN cả
box) hiện đúng 1 chữ "INVENTORY" khổng lồ giữa màn, không có chân dung/thông tin gì khác dù không
gian đủ lớn.

- [x] Thu `PlaceholderText` về dải 15% trên cùng (đúng quy ước `TitleText` mọi màn khác — trước đó
      là ngoại lệ duy nhất phủ kín 100%).
- [x] Thêm **leader showcase**: `PortraitRing/PortraitMask/PortraitSprite` (160×160, mirror ĐÚNG
      cấu trúc `PortraitRing` đã có sẵn ở `UI_HeroCard.prefab` — bronze frame + `Mask` ẩn
      `showMaskGraphic`, không phát minh lại) + `LeaderNameLabel`/`LeaderLevelLabel` bên dưới, dữ
      liệu lấy từ `profile.Heroes[0]` — ĐÚNG khái niệm "leader" đã dùng ở TopBar
      (`MetaSceneInstaller.RefreshLeaderPortrait`), không phải khái niệm mới.
- [x] `InventoryScreen.cs`: field `_leaderPortrait`/`_leaderNameLabel`/`_leaderLevelLabel` +
      `ILocalizationService _loc` mới, bind trong `BuildShell()`, method `RefreshLeader()` mới gọi
      đầu `Refresh()` — tên hero qua `_loc.GetName(defId, LocalizedNameKind.Hero)` với fallback
      `HeroDisplayUtil.FormatName` (ĐÚNG pattern đã chốt ở `CodexScreen`, không tự nghĩ cách khác),
      sprite qua `Resources.Load<Sprite>($"Art/Characters/Heroes/{defId}/{defId}_v1_00")` (ĐÚNG
      path đã dùng ở `RefreshLeaderPortrait`).
- [x] Tính hình học tay xác nhận không chồng lấn: Portrait/Name/Level đều nằm trong nửa trên
      `InnerBlue` (470px cao), cách Title 13.5px, cách nhau 10px/2px — không đụng đáy box.
- [x] Verify: `Resources.Load<Sprite>("Art/Characters/Heroes/hero_ember_knight/hero_ember_knight_v1_00")`
      trả về sprite thật (không null) qua `execute_code`; `HeroDisplayUtil.FormatName("hero_ember_knight")`
      → `"Ember Knight"` đúng. Đối chiếu ĐẦY ĐỦ path + đúng loại component (không chỉ `Find()!=null`
      — đúng bài học §3.5 "vòng 3") cho cả `PortraitSprite`/`LeaderNameLabel`/`LeaderLevelLabel`/
      `CloseButton`/`StatsText` — 5/5 khớp. `validate_script` 0 lỗi, `refresh_unity` force+compile
      0 lỗi console, **632/632 test xanh**.

Chưa làm (để dành, chưa yêu cầu thêm): 19/24 ô lưới vẫn chưa có `Icon` con (đã ghi ở §4.2, không
phải regression của lượt này); icon thật theo từng loại vật phẩm (vẫn tô màu phẳng tạm).

### §4.5. `UI_HeroDetail` — bố cục vốn đã tốt, chỉ 1 bug chồng lấn thật + đồng bộ màu — XONG

Khác Shop/Inventory, màn này bố cục CHÍNH đã hợp lý từ trước (portrait+level+exp+stat cột trái,
skill list card-style cột phải, đúng tinh thần UI_02 2 cột) — đo tay `execute_code` toàn bộ 8 khối
con của `Panel` trước khi kết luận cần sửa gì, tránh sửa mù chỉ vì "làm cho đẹp hơn":

- [x] Phát hiện 1 bug hình học THẬT: `StatsContainer` (đáy tính ra `y=-167`) chồng lên
      `AscendButton` (đỉnh `y=-163`) đúng 4px — do `sizeDelta.y=110` khai báo dư so với nội dung
      thật (3 hàng × 30 spacing + 26 cao hàng cuối = 86). Các `Row_STR..LUK` định vị theo
      `anchoredPosition` riêng của từng hàng (không phụ thuộc `sizeDelta` container) nên hạ
      `sizeDelta.y` 110→90 KHÔNG dịch hàng nào, chỉ kéo đáy box lên đúng chỗ — gap sau sửa = 16px
      (verify lại bằng đúng phép cộng trừ toạ độ Panel-local, không đoán).
- [x] Thêm nền thẻ (`pixel_metal_panel`, tint `(0.25,0.25,0.3,0.55)`) cho `StatsContainer` — khớp
      ĐÚNG tint đã dùng sẵn cho mỗi `Row_i` của `SkillListContainer` (màn này vốn đã "card hoá"
      skill list từ trước, chỉ khối stat bên trái bị bỏ sót), cho 2 khối cân xứng thị giác.
- [x] Đồng bộ màu `CloseButton`: trước đó đỏ `(0.5,0.2,0.2)` — khác quy ước trung tính
      `(0.42,0.40,0.38)` đã dùng cho đúng vai trò CLOSE ở `UI_Shop`/`UI_Inventory` (phát hiện qua
      soát tính nhất quán, đúng tinh thần câu hỏi "TopBar phải đồng nhất" trước đó).
- [x] Verify: đối chiếu ĐỦ 35 path+loại component (`Title`/`LevelLabel`/`ExpBar`/`Portrait`/6 dòng
      Stat/5 dòng Skill×4 phần tử/`AscendButton`×3/`CloseButton`) mà `HeroDetailScreen.cs` cần —
      35/35 khớp. Không sửa `HeroDetailScreen.cs` (thuần đổi số liệu/màu tĩnh trong prefab).
      `refresh_unity` force+compile 0 lỗi console, **632/632 test xanh**.

### §4.6. `UI_Summon` — kết quả gacha tô màu theo rarity thật + khung nền cho kết quả — XONG

Đo hình học trước (đúng kỷ luật): màn này KHÔNG có bug chồng lấn nào (`ResultsText`/`CloseButton`/
2 nút Pull đều cách nhau ≥8px) — khác Shop/Inventory, việc cần làm ở đây là chất lượng trình bày,
không phải sửa lỗi vị trí.

- [x] Phát hiện: `TeamSelectScreen.cs:530` đã có sẵn `RarityColor(Rarity)` (bảng màu
      Common/Rare/Epic/Legendary/Mythic dùng cho nhãn độ hiếm trang bị) — đổi `private`→`internal`
      (cùng assembly `Game.Meta`, không cần lớp chia sẻ mới) để `SummonScreen` TÁI DÙNG đúng bảng
      màu này cho kết quả pull, tránh bịa ra bảng màu rarity thứ 2 trong cùng 1 game.
- [x] `SummonScreen.Pull()`: mỗi dòng kết quả nay bọc `<color=#RRGGBB>{Rarity} · {Name}</color>`
      (rich text, `ColorUtility.ToHtmlStringRGB`) thay vì chữ trắng đồng loạt — người chơi phân biệt
      ngay Legendary (cam vàng) khác Common (xám) mà không cần đọc chữ.
- [x] Bọc `ResultsText` vào `ResultsPanel` mới (nền `pixel_metal_panel`, tint card giống Shop) —
      trước đó chữ trôi nổi trực tiếp trên `InnerBlue`, không tách khối; `ResultsText` dời vào bên
      trong, lấp đầy trừ padding 10px, `supportRichText=true`. Cập nhật path trong
      `SummonScreen.cs`: `panel.Find("ResultsText")` → `panel.Find("ResultsPanel/ResultsText")`.
- [x] Đồng bộ màu `CloseButton` (đỏ→trung tính `(0.42,0.40,0.38)`) — cùng bug đã thấy lặp lại ở
      `UI_HeroDetail`.
- [x] Verify: đối chiếu path+component `ResultsPanel`/`ResultsText`(`richText=True`)/`WalletLabel`/
      `PullOneButton`/`PullTenButton`/`CloseButton` — 6/6 khớp. `validate_script` 0 lỗi cả
      `SummonScreen.cs` lẫn `TeamSelectScreen.cs`, `refresh_unity` force+compile 0 lỗi console
      (chứng minh `internal` đủ quyền truy cập xuyên 2 file cùng assembly), **632/632 test xanh**.

### §4.7. `UI_Mail` — card hoá 6 dòng + phân biệt trực quan đã nhận/chưa nhận — XONG

Đo hình học trước: KHÔNG có bug chồng lấn (giống Summon, khác Shop/Inventory) — nhưng
`WalletLabel`/`RowListContainer`/`CloseButton`/`ClaimAllButton` là con TRỰC TIẾP của `Panel`
(không phải `InnerBlue`) — đúng mẫu đã xác nhận ở Shop, tính lại mốc theo `Panel` (864×486, nửa
243) thay vì `InnerBlue` (nhắc lại để không lặp lại nhầm lẫn ban đầu ở §4.3).

- [x] 6 `Row_i` trước đó KHÔNG có nền (chữ trôi nổi trực tiếp, đúng kiểu "danh sách phẳng" như Shop
      trước khi sửa) — thêm `Image` nền `pixel_metal_panel` tint card giống Shop
      `(0.15,0.13,0.12,0.55)`.
- [x] Thêm phân biệt **đã nhận/chưa nhận bằng mắt**: `MailScreen.cs` field `_rowCards` mới (bind
      trong `BuildShell()`), `Refresh()` set màu thẻ mờ hơn hẳn khi `m.Claimed`
      (`CARD_CLAIMED` alpha 0.25 so với `CARD_UNCLAIMED` alpha 0.55) — trước đó chỉ phân biệt qua
      chữ "CLAIMED" nhỏ trong `ProgressLabel` + nút Claim bị khoá, dễ bỏ sót khi liếc nhanh 6 dòng.
- [x] Verify: đối chiếu đủ path+component cho cả 6 `Row_i` (Image/NameLabel/ProgressLabel/
      ClaimButton) + `ClaimAllButton`/`CloseButton` — 100% khớp. `validate_script` 0 lỗi (1 warning
      chung "null-check GetComponent" — cùng mẫu chấp nhận được ở mọi màn khác, không phải lỗi
      mới), `refresh_unity` force+compile 0 lỗi console, **632/632 test xanh**.

### §4.8. `UI_Quest` — card hoá 6 dòng + tint phân biệt Daily/Achievement — XONG

Cấu trúc gốc giống hệt Mail trước khi sửa (Mail vốn clone từ chính prefab này) — không có bug
chồng lấn, `CloseButton` đã trung tính sẵn (không dính lỗi đỏ như HeroDetail/Summon).

- [x] Card hoá 6 `Row_i` như Mail (`pixel_metal_panel`), nhưng KHÔNG dùng 1 tint đồng nhất — tận
      dụng đặc điểm riêng của Quest: `ROW_COUNT=6` cố định V1 gồm ĐÚNG 3 Daily (`Row_0..2`) + 3
      Achievement (`Row_3..5`), thứ tự không đổi runtime (`QuestScreen.cs` comment "cố định V1") —
      cùng điều kiện đã cho phép bake tint tĩnh ở Shop (thứ tự `CATALOG` cố định). Daily nhận tint
      xanh dương nhạt, Achievement nhận tint tím nhạt — ĐÚNG 2 gia đình màu
      `ITEM_TINT`/`MATERIAL_TINT` đã dùng ở `InventoryScreen` cho ý nghĩa tương đồng (thường
      xuyên/reset vs đặc biệt/vĩnh viễn), pha loãng alpha 0.28 để chỉ là lớp phủ nhẹ, không lấn chữ.
      Không cần sửa `QuestScreen.cs` — tint bake tĩnh trong prefab, code không đụng màu row.
- [x] Verify: đối chiếu path+component cả 6 `Row_i` + `CloseButton` — 100% khớp. `refresh_unity`
      force+compile 0 lỗi console, **632/632 test xanh**.

### §4.9. `UI_Codex` — card hoá 6 dòng + đồng bộ màu CloseButton (bug đỏ lần thứ 3) — XONG

Cấu trúc gốc = clone `UI_Quest` + `Icon`/`SwitchTabButton`/`PrevButton`/`NextButton` (task-codex.md).
Không có bug chồng lấn (8px gap giữa hàng nút dưới với `CloseButton`, đo tay xác nhận).

- [x] Card hoá 6 `Row_i` — cùng tint `pixel_metal_panel (0.15,0.13,0.12,0.55)` đã dùng ở Mail/Quest
      (Codex không có nhóm phụ như Daily/Achievement nên dùng 1 tint đồng nhất, không như Quest).
- [x] `CloseButton` đỏ `(0.5,0.2,0.2)` → trung tính `(0.42,0.40,0.38)` — ĐÂY LÀ LẦN THỨ 3 gặp đúng
      bug này (sau HeroDetail §4.5, Summon §4.6) — cả 3 màn đều clone/dựng cùng 1 giai đoạn scaffold
      §3.8, cùng dính 1 lỗi màu mặc định. Chủ động soát luôn `UI_NodeChoice`/`UI_Tower`/`UI_Dungeon`/
      `UI_TrialBoss` (chưa tới lượt sửa layout, chỉ đọc màu `CloseButton`) thay vì đợi phát hiện lại
      từng cái — kết quả: `Tower`/`Dungeon`/`TrialBoss` ĐÃ trung tính sẵn `(0.42,0.40,0.38)` (không
      dính bug này), riêng `NodeChoice` là trắng thuần `(1,1,1,1)` — khác 2 kiểu lỗi kia, để dành sửa
      đúng lúc tới lượt màn đó (§4.10) thay vì sửa lạc phạm vi ở đây.
- [x] Verify: đối chiếu path+component 6 `Row_i` (Image/Icon/NameLabel/ProgressLabel/ClaimButton) +
      `SwitchTabButton`(+Label)/`PrevButton`/`NextButton`/`CloseButton` — 100% khớp. `refresh_unity`
      force+compile 0 lỗi console, **632/632 test xanh**.
