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
