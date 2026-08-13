# Task: Animation pilot (Pillow procedural character + ComfyUI background)

Yêu cầu: 3/3 hạng mục lớn người dùng chọn làm ("thực hiện cả 3 mục trên") — Addressables xong
(task-addressables-pilot.md), Localization xong (task-localization-pilot.md), tới Animation. Sau
khi báo rủi ro thật (ComfyUI không đảm bảo nhất quán nhiều frame cùng 1 nhân vật) và hỏi lại, người
dùng chọn hướng RIÊNG: **dùng Pillow (code) vẽ nhân vật + frame animation, ComfyUI chỉ vẽ
background**. Tham khảo ảnh thật `_Reference/UI_SAMPLE/Character_01.png` (đã tạo bằng Pillow từ
trước) làm chuẩn phong cách. Thêm chỉ đạo art trực tiếp: "nhân vật ít màu thôi để các khối rõ ràng
và outline viền đen bên ngoài". Việc lớn, thử nghiệm, khác hẳn mọi task code thuần trước đó — viết
xong task file rồi mới code/sinh asset.

## §0. Findings

- **`UnitView.cs` (CombatView) hiện KHÔNG có animation frame-based nào cả** — mọi "chuyển động"
  hiện tại là code thuần túy: lerp vị trí (lunge/miss), đổi màu (hit flash), fade alpha (death).
  Chỉ 1 `SpriteRenderer` tĩnh, không `Animator`/`AnimatorController`/`AnimationClip` nào tồn tại.
  Xây animation thật nghĩa là thêm 1 hệ CHẠY FRAME SEQUENCE hoàn toàn mới, không có gì để "mở rộng"
  từ trước.
- **`Character_01.png`** (`_Reference/UI_SAMPLE/`, 32×32) — nhân vật chibi đơn giản: khối màu phẳng
  rõ ràng (mũ/giáp xanh lá, dải mắt/eo be, khiên xám, quần nâu, vũ khí nâu+trắng), viền đen 1px bao
  quanh toàn silhouette. **So sánh trực tiếp với `hero_ember_knight_v1_00.png`** (sprite hero thật
  hiện có, cũng 32×32, sinh từ ComfyUI ở task-hero-roster.md) — bản ComfyUI có shading/gradient mềm
  hơn, ít sắc nét hơn ở kích thước nhỏ. Xác nhận đúng nhận định người dùng: Pillow (vẽ code trực
  tiếp) cho kết quả sắc nét/rõ khối hơn AI generate ở độ phân giải 32×32 này.
- **`Assets/Tools/pixel-art-pipeline` (skill) mặc định chia việc**: ComfyUI cho "nội dung hữu cơ"
  (nhân vật/quái/background), Pillow chỉ cho "nội dung hình học" (UI/khung/thanh máu) — KHÔNG có
  sẵn generator vẽ nhân vật bằng Pillow trong `scripts/compose.py`. Nhiệm vụ này ĐI NGƯỢC mặc định
  đó theo đúng yêu cầu người dùng — viết 1 script MỚI (không sửa `compose.py` gốc, giữ nguyên hệ
  UI-generator đang chạy tốt) để vẽ nhân vật thủ tục (procedural) bằng Pillow.
- **`Tools/palette.json`** (dự án, 48 màu, "TurnBase 48") đã có sẵn — dùng làm nguồn màu, nhưng
  theo chỉ đạo mới "ít màu thôi" → mỗi nhân vật chỉ dùng ~5-6 màu CON trong bảng 48 màu đó (không
  dùng cả 48), + đen thuần cho outline.
- **ComfyUI ĐANG CHẠY thật** (`curl 127.0.0.1:8188/system_stats` trả 200) — sẵn sàng sinh
  background.
- **Không có Animator Controller/AnimationClip nào trong dự án cho unit chiến đấu** — cần 1 bộ máy
  chạy sprite-sequence MỚI (đơn giản: đổi `_sprite.sprite` theo mảng frame + timer), KHÔNG dùng
  Mecanim `Animator`/`.controller` (nặng hơn, cần asset riêng cho từng clip — không cần thiết cho
  pilot 1 hero, thêm sau nếu mở rộng thật).

## §1. Scope decision

**Trong phạm vi (pilot rất hẹp — CHỈ 1 hero để verify toàn bộ luồng trước khi mở rộng, đúng kỷ luật
Addressables/Localization đã dùng):**
1. Script Pillow MỚI (`Tools/pixel-art-pipeline/scripts/character_draw.py` hoặc tương đương) — vẽ
   thủ tục 1 nhân vật chibi 32×32 theo tham số (offset đầu/tay/chân theo frame), ~5-6 màu phẳng từ
   `palette.json` + viền đen 1px quanh silhouette, khớp phong cách `Character_01.png`.
2. Sinh 2 trạng thái animation cho **1 hero pilot** (`hero_ember_knight`, hero đã dùng làm ví dụ
   xuyên suốt session): `idle` (4 frame, bob nhẹ lên xuống) + `attack` (4 frame, vươn người ra
   trước). Xuất PNG rời + `spritesheet` qua `compose.py spritesheet` có sẵn (tái dùng đúng công cụ
   lắp ráp, không viết lại).
3. 1 background chiến đấu qua ComfyUI (`comfy_gen.py`, đúng luồng skill mặc định) — "thiết kế box
   layout" hiểu là bố cục theo lớp/khối rõ ràng (nền xa/giữa/gần tách lớp, không lộn xộn), KHÔNG
   phải khung wireframe placeholder. 1 ảnh, không làm full parallax 3 lớp (ngoài phạm vi pilot).
4. Import vào Unity (`unity_import.py` có sẵn) — sprite settings Point filter đúng chuẩn dự án.
5. Hệ chạy animation MỚI, nhỏ (`SpriteSequencePlayer` hoặc gộp trực tiếp vào `UnitView`) — đổi
   sprite theo mảng frame theo FPS, state `Idle`/`Attack`, fallback về sprite tĩnh cũ nếu hero
   KHÔNG có bộ frame mới (mọi hero khác giữ nguyên hành vi cũ, không vỡ gì).
6. Verify Play-mode thật: vào 1 trận có `hero_ember_knight`, xác nhận animation idle chạy, xác nhận
   chuyển sang attack lúc tấn công.

**Ngoài phạm vi (cố ý, ghi rõ — rất nhiều, đây là pilot hẹp nhất trong 3 pilot):**
- KHÔNG làm 23 hero còn lại, KHÔNG làm enemy/boss.
- KHÔNG làm đủ 7 clip plan.md §2.2 đòi hỏi (walk/skill/hit/down/victory) — chỉ idle+attack, đủ để
  verify pipeline kỹ thuật (sinh frame nhất quán + chạy runtime), không phải nội dung đầy đủ.
- KHÔNG dùng `Animator`/Mecanim thật — sprite-sequence tay đơn giản, đủ cho 2 state.
- KHÔNG làm full parallax background 3 lớp.
- KHÔNG động tới `PlayHit`/`PlayMiss`/`PlayDeath` hiện có (giữ nguyên code "juice" cũ, animation
  MỚI chạy song song cho state Idle/Attack, không thay thế toàn bộ hệ phản hồi hiện có).

## §2. Implementation checklist

- [x] Viết script Pillow vẽ nhân vật thủ tục — xem lại kết quả bằng Read TRƯỚC khi lắp ráp tiếp
      (bắt buộc theo skill). → `Tools/pixel-art-pipeline/scripts/character_draw.py`.
- [x] Sinh 4 frame idle + 4 frame attack cho `hero_ember_knight` — xem từng frame bằng Read (đạt
      style ngay lần đầu, không cần lặp lại/đổi seed).
- [x] `compose.py spritesheet` gộp frame + metadata (dùng để lưu trữ/tham khảo; import Unity thật
      dùng 8 file rời — xem ghi chú dưới).
- [x] `comfy_gen.py` sinh 1 background — xem bằng Read. 2/3 variant hỏng (AI vẽ lặp nhân vật dù có
      negative prompt) → loại. Hậu xử lý `post_process.py` LÀM HỎNG variant còn lại (mảng trắng lỗi
      keying) → bỏ qua hậu xử lý, dùng thẳng ảnh gốc ComfyUI (đã đạt chất lượng, đúng nguyên tắc
      skill "đừng cố cứu bằng hậu xử lý").
- [x] Import Unity — `unity_import.py` mà skill nhắc tới KHÔNG tồn tại thật trong dự án; import thủ
      công bằng cách viết `.meta` tay theo đúng mẫu sprite hero đã import trước đó (Point filter,
      Compression None, PPU 32), verify qua `execute_code` đọc `TextureImporter`/`Sprite` thật (9/9
      file đúng: 8 frame pivot bottom-center, 1 background pivot center).
- [x] Hệ sprite-sequence mới trong `UnitView.cs` — `LoadFrames`/`AnimState`/`AdvanceAnimFrame`,
      fallback đúng về sprite tĩnh cho 23 hero + mọi enemy chưa có frame (không đổi hành vi).
- [x] `refresh_unity` compile sạch (không lỗi).
- [x] Chạy full EditMode suite — 423/423 xanh, không đụng logic Combat lõi.
- [x] Verify Play-mode — lần đầu gặp lại MCP frame-stall (`Time.frameCount` đứng yên). Xác minh
      logic frame-cycle bằng reflection (`Bind()`/`PlayAttackLunge()`/`Update()` thủ công, đọc
      `Time.deltaTime` thật ~0.02s dù `frameCount` không tăng — idle lặp đúng 8fps, attack chạy hết
      4 frame ở 14fps rồi tự về `Idle`). **Sau đó wiring thêm background (xem mục dưới) và thử lại
      Play-mode — LẦN NÀY KHÔNG bị frame-stall** — chụp `manage_camera screenshot` thật, thấy trực
      tiếp `hero_ember_knight` (mũ xanh/áo đỏ, đúng màu đã vẽ) đứng trong trận cùng background mới,
      xác nhận bằng mắt thay vì chỉ suy luận qua reflection.
- [x] **(Bổ sung ngoài checklist gốc)** Wiring background vào Battle scene thật — background sinh ở
      §2 trước đó CHƯA từng được gắn vào bất kỳ scene nào (asset mồ côi, đúng mẫu lỗi hay gặp cả
      session). Kích thước sinh 512×288 ở PPU 32 dự án = đúng 16×9 world unit, khớp CHÍNH XÁC vùng
      nhìn thấy của `BattleCamera` (orthoSize 4.5, aspect 16:9) — không cần scale. Tạo GameObject
      `Background` tĩnh trên Hierarchy (con của `__Stage__` trong `Battle.unity`, đúng quy ước "Hierarchy
      nghĩa là tĩnh" — không tạo runtime), `SpriteRenderer` gán `battle_arena_ember`,
      `sortingOrder=-100` (dưới mọi unit/VFX). Lưu scene thật (`manage_scene save`).
- [x] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`.

## §3. Follow-up: so sánh kỹ thuật "skeletal rig" + VFX hậu kỳ

**Bối cảnh:** người dùng phát hiện `Tools/pixel-art-pipeline/charator/pixel_character_generator_v2.py`
(đã tồn tại sẵn trong repo, không do tôi tạo) — 1 kỹ thuật KHÁC hẳn `character_draw.py`: bộ phận
đầu/thân/tay/chân/vũ khí TÁCH RỜI, mỗi bộ phận có pivot và XOAY thật (`PIL Image.rotate`) trước khi
ghép — thay vì khối chữ nhật cố định dịch theo tham số. Hỏi có nên dùng để vẽ lại TOÀN BỘ
hero/enemy/boss cho nhất quán/sắc nét hơn, kèm VFX.

**Điều tra:** file đó trỏ tới 1 project RIÊNG ngoài dự án (`/Users/hainx/__Data/__Unity/__2D/
Art_python`) — kiểm tra kỹ thấy: (1) chỉ Mage+Warrior có part thật, Ranger/Rogue mới có ảnh tham
khảo; (2) "Smooth_Walk_x4.png"/rig Warrior đẹp quan sát được KHÔNG đến từ code hiện có trên đĩa (grep
xác nhận không có hàm `generate_smooth_walk`/`get_warrior_*` nào — bản trung gian đã mất, không port
được); (3) v2 build ở 64×64, gấp đôi PPU 32×32 dự án.

**Quyết định (qua AskUserQuestion):** Pilot nhỏ trước — port kỹ thuật (không sửa file gốc) vào
`Tools/pixel-art-pipeline/scripts/character_rig.py`, dựng lại ĐÚNG `hero_ember_knight` ở 32×32 bằng
palette cũ để so sánh công bằng (chỉ đổi kỹ thuật, không đổi nhận diện nhân vật).

**Lần review đầu — LỖI THẬT:** mỗi bộ phận tự vẽ viền riêng → tạo seam đen xấu giữa đầu/thân/tay/
chân/khiên (phá quy tắc "viền bọc toàn silhouette" đã lập từ `Character_01.png`). Đã sửa: bộ phận
render KHÔNG viền (`_PART_COLORS`, map "1"→trong suốt), viền thật vẽ 1 LẦN lên frame đã ghép xong
(`add_silhouette_outline`, thuật toán dilate y hệt `character_draw.add_outline`). Review lại: viền
sạch, liền mạch, không seam.

**Kết quả so sánh (`hero_ember_knight_attack` — bản gốc vs bản rig):**
- Điểm cộng rig: vũ khí XOAY thật qua cung liên tục (-70°→+50°) thay vì nội suy toạ độ tay thẳng
  hàng như `character_draw.py` — mượt hơn thấy rõ.
- Điểm trừ rig: silhouette nhỏ/chibi hơn hẳn bản gốc (đầu to hơn tỷ lệ, thân/chân ngắn hơn) — vấn đề
  NGÂN SÁCH PIXEL từng bộ phận, không phải lỗi kỹ thuật, cần tinh chỉnh thêm nếu muốn dùng thật.
- **Kết luận: chưa thay `character_draw.py`** — rig là kỹ thuật tốt hơn cho khớp nối, nhưng bản pilot
  hiện tại (silhouette) chưa đạt để adopt ngay. `hero_ember_knight` trong Unity vẫn dùng bộ frame từ
  `character_draw.py` (không đổi).

**Thêm 2 kỹ thuật hậu kỳ (giữ, KHÔNG làm nhoè pixel gốc) vào `character_rig.py`:**
- `draw_drop_shadow`: elip đen mờ dưới chân — LỚP RIÊNG, không đụng pixel nhân vật, bình thường ngay
  cả trong pixel art cứng nét.
- Motion-blur weapon trail: 1 bản vũ khí alpha=90 (KHÔNG blur, vẫn NEAREST) ở góc frame TRƯỚC đó.

**Người dùng tự viết thêm `pixel_character_generator_modern.py`** (Art_python, ngoài dự án) — 4 kỹ
thuật: Drop Shadow / Bloom (bóc pixel sáng + `GaussianBlur`) / Motion Blur / 24 frame + `BICUBIC`
cho khớp xoay. Đã xem ảnh thật (`Modern_Smooth_Run_x3.png`) — nhận xét kỹ thuật, không chỉ theo mô
tả:
- `BICUBIC` cho khớp xoay + `GaussianBlur` bloom BAKED vào PNG → đầu/thân (không xoay) giữ nét, tay/
  chân/vũ khí (có xoay) bị NHOÈ rõ rệt — không đồng nhất trong CÙNG 1 sprite, ngược lại phong cách
  "phẳng màu/viền cứng" đã chốt từ đầu task.
- Game thật (Dead Cells/Octopath) không bake blur vào sprite — bloom là HẬU KỲ REAL-TIME (extract
  pixel sáng của CẢ khung hình đã render, blur, cộng ngược) — đúng cách trong Unity là URP Bloom
  Volume + màu HDR/emissive trên riêng vật thể muốn sáng, sprite gốc giữ nguyên sắc nét.
- 24 frame/60fps vượt xa spec `plan.md §2.2` (4-8 frame/8-14fps) — game lượt (turn-based), không
  phải action platformer, không cần mượt cỡ đó.
- Drop shadow + motion-blur-trail: giữ được nguyên vẹn (không cần blur/BICUBIC), đã port sang
  `character_rig.py` như trên.

**Quyết định (qua AskUserQuestion, 2 câu):** (1) rig — sửa outline rồi so sánh lại (ĐÃ LÀM, xem
trên); (2) VFX — "áp dụng cả 1,2" = giữ drop-shadow+motion-blur-trail KHÔNG blur (ĐÃ LÀM) VÀ thử
dựng URP Bloom Volume thật (ĐÃ LÀM, xem dưới).

### §3.1. URP Bloom Volume — dựng thật, verify bằng pixel thật

**Phát hiện hạ tầng có sẵn:** URP đã cấu hình đúng từ trước (2D Renderer, `m_SupportsHDR: 1`,
`PostProcessData` đã wire) — `Assets/Settings/DefaultVolumeProfile.asset` THẬM CHÍ đã có sẵn 1
override Bloom (`active: 1`) nhưng `intensity: 0` (tắt hiệu quả), và `BattleCamera` có
`m_RenderPostProcessing: 0` — hạ tầng có sẵn, chưa ai bật, đúng mẫu "infra sẵn, chưa dùng" lặp lại
nhiều lần trong dự án.

**Đã xây:**
- `Assets/_Project/Shaders/HDREmissiveSprite.shader` (mới) — Unlit URP, property `[HDR] _Color` để
  Inspector cho kéo Intensity > 1 (bắt buộc để Bloom nhận diện pixel "đủ sáng" — sprite thường 0..1
  không bao giờ vượt ngưỡng dù đặt trắng tuyệt đối).
- `Assets/_Project/Resources/Art/VFX/Mat_HDREmissiveSprite.mat` (mới) — dùng shader trên, màu HDR
  ember (R3.2, G1.8, B0.4).
- Bật `BattleCamera.renderPostProcessing = true`.
- GameObject `GlobalVolume` tĩnh mới trong `Battle.unity` (`isGlobal=true`, dùng
  `DefaultVolumeProfile.asset`, bumped `Bloom.intensity` 0→3, `threshold=0.9` giữ nguyên).

**Verify:** gặp đúng loại lỗi môi trường đã biết (2 scene cùng loaded → 2 camera tranh render;
Play mode reload từ đĩa làm mất object chưa save; Game View báo kích thước lỗi trong môi trường
MCP) — xử lý bằng cách: lưu scene TRƯỚC khi vào Play, tắt camera cạnh tranh khi cần chụp, và cuối
cùng **render trực tiếp `BattleCamera` ra `RenderTexture` riêng qua code + `ReadPixels`** (không
phụ thuộc Game View) với nền ép về `SolidColor` đen tuyệt đối — loại bỏ hoàn toàn nhiễu từ art nền.
Kết quả: 1 sprite trắng nhỏ (8×8) gắn `Mat_HDREmissiveSprite` cho ra quầng sáng vàng ấm lan toả rõ
ràng, mềm, đối xứng quanh sprite trên nền đen tuyệt đối — **bằng chứng pixel thật, không suy đoán**,
xác nhận toàn bộ chuỗi Bloom hoạt động đúng. Sprite nhân vật/VFX khác trong game (LDR, không dùng
material này) KHÔNG bị ảnh hưởng — hạ tầng chỉ kích hoạt cho vật thể cố ý gắn material HDR.
Dọn sạch: xoá GameObject test khỏi `Battle.unity`, camera/background trả về mặc định
(Skybox + màu cũ), scene lưu sạch — không còn debris.

**Trạng thái cuối:** hạ tầng Bloom SẴN SÀNG DÙNG (Volume + shader + material đã có, verify thật) —
CHƯA gắn vào bất kỳ VFX/skill thật nào trong game (đó là việc riêng, ngoài phạm vi "thử setup").
423/423 test suite baseline không đổi (chỉ đụng scene/shader/material, không đụng code test được).

## §4. Yêu cầu MỚI: vẽ lại TOÀN BỘ nhân vật + animation đầy đủ + VFX skill

Người dùng yêu cầu (nguyên văn): "vẽ lại toàn bộ nhân vật và xây animation cho các nhân vật đó,
idle, move, action, dam, die... và các VFX cho các skill của nhân vật, quái với boss." Đã hỏi qua
AskUserQuestion nên bắt đầu từ đâu — người dùng chọn **cả 3 hướng cùng lúc** ("làm cả 4 cái, tạo
task .md cụ thể để check theo dõi"): (1) hoàn thiện rig + đủ 5 trạng thái cho 1 hero, (2) điều tra
số liệu thật (class/enemy/skill), (3) làm VFX skill thật dùng Bloom vừa xây. File này = task file
theo dõi, cập nhật liên tục qua các lượt "tiếp tục cho tôi" tiếp theo (việc quá lớn cho 1 lượt).

### §4.0. Audit số liệu thật (đọc CSV trực tiếp, không suy đoán)

- **Hero (`heroes.csv`, 24 dòng):** ĐÚNG 4 hero/class × 6 class (`Vanguard/Arcanist/Trickster/
  Warden/Slayer/Summoner`), và trong mỗi class, 4 hero gần như luôn khác nhau về `element` (VD
  Vanguard: `hero_ember_knight`=Fire, `hero_iron_bastion`=Earth, `hero_tide_warden`=Water,
  `hero_stormguard`=Wind). **Phát hiện quan trọng nhất:** đây là cấu trúc RẤT thuận cho chiến lược
  "6 rig thân (1/class) + recolor theo element" thay vì 24 thiết kế độc lập — khớp đúng cách
  task-hero-roster.md đã làm với STAT/SKILL (tái dùng template class, chỉ đổi Element + 1 Ultimate
  + art riêng).
- **Enemy (`enemies.csv`, 66 dòng):** có field `archetype` (11 loại:
  Skirmisher15/Brute9/Tank9/Grunt8/Caster6/Boss6/Debuffer4/Archer3/Bomber3/Healer2/Swarm1) và
  `element` (Dark31/Fire12/Neutral9/Earth8/Water5/Wind1 — LỆCH hẳn về Dark, khác hero's phân bố đều
  hơn). Đa dạng hơn hero nhiều — 11 archetype không chia đều, monster cùng archetype nhiều khả năng
  vẫn cần silhouette khác nhau thật (khác hero, "Brute lửa" và "Brute đất" thường là 2 con quái khác
  hẳn hình dáng, không chỉ đổi màu) — CẦN xem thêm tên/mô tả từng con trước khi quyết định nhóm
  được bao nhiêu, chưa kết luận vội.
- **Skill (`skills.csv`, 65 dòng):** field `element` chỉ 7 giá trị (Neutral19/Dark16/Fire8/Wind7/
  Water5/Light5/Earth5), `type` chỉ 4 giá trị (Physical23/Magical22/Support13/Heal7),
  `animTrigger` chỉ 3 giá trị (cast41/attack23/guard1 — KHÔNG đủ chi tiết để chọn VFX, chỉ phân loại
  animation thô). **Đối chiếu với 9 VFX asset đã có sẵn** (`Art/VFX/vfx_{fire_burst,ice_shatter,
  lightning,poison_cloud,dark,earth_spike,heal_sparkle,slash_arc,shield_barrier,break_shatter}`):
  Fire→fire_burst✓, Water→ice_shatter✓(gần đúng), Earth→earth_spike✓, Dark→dark✓, Heal→heal_sparkle✓,
  Support/shield→shield_barrier✓, Physical→slash_arc✓, Poise-break→break_shatter✓ (dùng chung mọi
  element). **Gap thật chỉ còn 2-3, KHÔNG phải 65**: chưa có VFX cho Light, Wind (lightning có thể
  tạm dùng nhưng không hẳn đúng "gió"), và 1 "magic bolt" trung tính chung cho Neutral+Magical.

**Kết luận audit:** việc "vẽ lại toàn bộ" thực tế nhỏ hơn nhiều so với đọc phẳng "24+66+65" —
hero có đòn bẩy tái dùng rất mạnh (6 rig × recolor), skill VFX chỉ thiếu ~2-3 archetype mới, phần
THẬT SỰ lớn và chưa có đòn bẩy rõ là 66 enemy (cần điều tra thêm) + việc TỰ vẽ 6 rig hero + tune 5
trạng thái mỗi rig (việc chính, tốn thời gian nhất).

### §4.1. Kế hoạch theo giai đoạn (cập nhật tiến độ tại đây mỗi lượt)

- [x] **Giai đoạn 1a — Tinh chỉnh tỷ lệ + dựng đủ 5 trạng thái (`hero_ember_knight`, rig):** tăng
      ngân sách pixel torso (8→10 hàng) + leg (7→9 hàng), giảm nhẹ head (8→7 hàng) — silhouette tổng
      ~26px cao, gần khớp bản `character_draw.py` cũ hơn hẳn lần review trước. Thêm `leg_angle_deg`
      (xoay chân thật quanh hông, mirror trái/phải) cho `move`; thêm `flash` (trắng silhouette) cho
      `damage`. Đủ 5 trạng thái: idle(4)/attack(4)/move(4, chân xoay thật thấy rõ luân phiên)/
      damage(3, frame đầu chớp trắng + giật lùi)/die(6, đầu sụp + tay rũ + lean tăng dần, giữ frame
      cuối). Review từng bộ bằng Read — TẤT CẢ đạt, không cần lặp lại.
      Output: `Tools/pixel-art-pipeline/clean/hero_ember_knight_rig/{idle,attack,move,damage,die}/`.
- [x] **Giai đoạn 1b — Quyết định adopt: THAY HẲN sang rig** (qua AskUserQuestion, người dùng chọn
      "Thay hẳn sang rig" — 1 kỹ thuật duy nhất xuyên suốt trước khi nhân rộng). Đã làm:
      - Xoá 8 file `character_draw.py` cũ (idle×4 + attack×4), copy 21 file rig mới (idle×4/
        attack×4/move×4/damage×3/die×6) vào `Assets/_Project/Resources/Art/Characters/Heroes/
        hero_ember_knight/Animations/`, đổi tên đúng quy ước `{defId}_{state}_{NN}.png` (bỏ hậu tố
        `_rig` chỉ dùng nội bộ thư mục pilot).
      - Viết `.meta` tay cho cả 21 file (đúng mẫu Point filter/Compression None/PPU32/pivot
        bottom-center đã dùng trước đó) — verify qua `execute_code` đọc `Sprite` thật: 21/21 OK.
      - Mở rộng `UnitView.cs`: `AnimState` thêm `Move/Damage/Die` (từ chỉ `Idle/Attack`),
        `FramesFor()`/`FpsFor()`/`Loops()` tổng quát hoá thay vì hard-code if/else riêng Attack —
        `Move` nạp sẵn nhưng CHƯA có điểm trigger gameplay thật (trận lượt không có "đi bộ" tự
        nhiên, để dành). `PlayHit()` trigger `Damage` (16fps, hết thì tự về Idle). `PlayDeath()`
        trigger `Die` (10fps, giữ nguyên frame cuối — KHÔNG về Idle) — chạy SONG SONG với fade-alpha
        có sẵn (gọi `AdvanceAnimFrame()` cả trong nhánh `_dying` của `Update()`), không thay thế
        fade cũ, chỉ cộng thêm chuyển động frame trong lúc fade.
      - Compile sạch, 423/423 test xanh. Verify runtime: `Bind()` thật nạp đúng cả 5 bộ (idle=4/
        attack=4/move=4/damage=3/die=6) qua `execute_code` đọc field thật (không đoán). Không lấy
        được screenshot Play-mode ổn định lần này (GameObject test bị mất giữa 2 lệnh `execute_code`
        — đúng kiểu bất ổn môi trường đã ghi nhận nhiều lần cả phiên, không phải lỗi code) — chấp
        nhận bằng chứng reflection + review ảnh tĩnh đã làm kỹ trước đó thay vì cố chụp thêm.
- [x] **Giai đoạn 2 — VFX skill thật đầu tiên: XONG.** Ultimate `hero_ember_knight` =
      `skill_inferno_bulwark` (slot 4, Fire/AoE/Charge, xác nhận qua `heroes.csv`/`skills.csv` —
      comment `BattleSceneInstaller.cs:477` xác nhận slot 4 = Ultimate; KHÔNG có field `IsUltimate`
      thật trong `SkillData`, hoàn toàn theo vị trí). Trước đây VFX chỉ chọn theo `Element` qua
      `VfxPlayer.KeyForElement` (`CombatPresenter.PresentDamage`) — không skill nào có VFX riêng dù
      `CombatEvent.StringValue` đã có sẵn comment "skillId, vfxKey..." làm placeholder CHẾT (đúng
      mẫu "hạ tầng có sẵn, chưa dùng" lặp lại). Đã nối thật:
      - `ActionResolver.cs` (4 điểm `Emit(CombatEventType.DamageDealt...)`, cả hit/miss/Counter/
        Reflect) nay truyền `stringValue: data.Id`/`skill.Data.Id` — 0 rủi ro (không test nào assert
        `StringValue`, xác nhận qua grep).
      - `VfxPlayer.cs`: thêm key `"inferno_bulwark"` (asset mới `vfx_inferno_bulwark`, 4 frame
        64×64, kỹ thuật burst lửa lõi trắng-nóng + tia ember Bresenham, sinh bằng
        `character_rig.py --vfx inferno_bulwark`) + `MATERIAL_OVERRIDES` (dict key→path material) —
        gán `Mat_HDREmissiveSprite` (Bloom thật, Giai đoạn trước) RIÊNG cho key này qua
        `ResolveMaterial()`, 9 key VFX cũ giữ nguyên `_defaultMaterial` không đổi.
      - `CombatPresenter.PresentDamage`: `e.StringValue=="skill_inferno_bulwark" && e.Status==None`
        (loại Counter/Reflect — StringValue vẫn mang skill gốc gây phản đòn, không phải bản thân
        inferno "đang" cast) → phát `"inferno_bulwark"` thay vì key theo Element.
      **Verify:** chạy 1 trận `CombatSimulation` THẬT (không mock, qua `execute_code`, hero dùng
      `skill_inferno_bulwark` slot 0 lặp lại nhiều lượt) — mọi `DamageDealt` event từ đòn đó đều có
      đúng `StringValue=skill_inferno_bulwark Status=None`, đúng điều kiện Presenter cần. Riêng
      resource path (4 frame VFX + material HDR) xác nhận `Resources.Load` thật đều trả về non-null
      qua `execute_code`. Compile sạch, 423/423 test xanh.
- [x] **Giai đoạn 3 — Nhân rộng hero còn lại theo class (6/6 KIT xong, 24/24 hero — HOÀN TẤT).**
      Refactor
      `character_rig.py` trước khi nhân rộng: tách `CharacterKit` (bó `get_head/get_torso/
      get_weapon/get_shield` RIÊNG theo class + `get_leg/get_arm` DÙNG CHUNG mọi class, chỉ đổi màu
      qua tham số `colors`) — `build_frame(..., kit=None)` mặc định `VANGUARD_KIT`, giữ nguyên
      100% hành vi `hero_ember_knight` cũ (verify bằng cách regenerate lại idle 4 frame, so ảnh y
      hệt trước refactor). CLI thêm `--kit`/`--hero` để chọn class + tên file xuất.
      **Arcanist = `hero_frost_sage`** (class template gốc, Water, xác nhận qua thứ tự 6 hero đầu
      trong `heroes.csv` — 18 hero sau tái dùng template). Thiết kế mới, silhouette khác hẳn
      Vanguard: mũ trùm nhọn (`get_arcanist_head`) thay mũ giáp phẳng, áo choàng loe (
      `get_arcanist_torso`) thay giáp thân, gậy phép dài (`get_arcanist_staff`) thay kiếm ngắn,
      KHÔNG khiên (`get_shield=None` — lớp phép không cầm khiên). Bảng màu băng giá riêng (
      `FROST_ROBE #2E6B78`/`FROST_HOOD #12304A`/`FROST_ICE #4EC3D9`/`FROST_ICE_BRIGHT #A5E8F0`, từ
      `Tools/palette.json`) — khác hẳn tông đỏ/xanh dương của Ember Knight. Dựng đủ 5 trạng thái
      (idle/attack/move/damage/die, cùng tham số pose y hệt Ember Knight — chỉ đổi `kit`), review
      từng bộ bằng Read — đạt ngay lần đầu. Import Unity (21 file vào
      `Assets/.../hero_frost_sage/Animations/`, `.meta` cùng mẫu) — **0 thay đổi code C#** (
      `UnitView.LoadFrames(defId, state)` đã tổng quát từ trước, tự nạp đúng theo `defId` mới).
      Verify qua `execute_code`: `Bind()` thật nạp đúng cả 5 bộ (idle=4/move=4/damage=3/die=6/
      attack=4). 423/423 test xanh.

      **Tiếp tục cùng phiên — 4 kit còn lại XONG, đủ 6/6 class:**
      - **Trickster = `hero_gale_thief`** (Wind) — khăn trùm thấp che mặt (khác mũ nhọn Arcanist),
        áo gọn nhẹ + khăn quàng vai bay, dao ngắn (2 cột, không phải gậy/kiếm dài), KHÔNG khiên.
        Bảng màu xanh lá rừng (`GALE_TUNIC #3D7A2E`/`GALE_HOOD #1B3D1F`/`GALE_SCARF #B9E89A`).
      - **Warden = `hero_dawn_cleric`** (Light) — khăn tu sĩ trắng ngà, áo loe vàng kim, **khiên
        TRÒN** (khác khiên chữ nhật Vanguard — silhouette khiên khác, không chỉ đổi màu), chuỳ đầu
        cầu sáng thay lưỡi sát thương (đúng vai trò hộ giáo/support). Bảng màu
        `DAWN_ROBE #B8901E`/`DAWN_HOOD #F2E8CF`/`DAWN_TRIM #FFF0B8`.
      - **Slayer = `hero_shadow_fang`** (Dark) — mũ trùm sát gọn mắt tím phát sáng, giáp da mỏng
        (hẹp hơn giáp tấm Vanguard), đại đao dài 2 cột (dài hơn hẳn vũ khí 3 class trước), KHÔNG
        khiên. Bảng màu tím thẫm/đỏ (`FANG_ARMOR #3A2233`/`FANG_GLOW #9B5DE5`/`FANG_TRIM #E63946`).
      - **Summoner = `hero_bone_caller`** (Dark) — khăn trùm tím có dấu xương trắng hình chữ thập
        trên trán, áo choàng RỘNG hơn Arcanist (viền xương trắng ở gấu), totem đầu lâu thay gậy-đá
        (silhouette vũ khí khác Arcanist dù cùng vai trò gậy phép), KHÔNG khiên. Bảng màu
        `BONE_ROBE #5A3080`/`BONE_TRIM #F2E8CF`/`BONE_GLOW #CBA5F0`.

      Cả 4 kit: đủ 5 trạng thái, review từng bộ bằng Read (đạt ngay lần đầu, không lặp lại lần
      nào), import Unity qua script MỚI `Tools/pixel-art-pipeline/scripts/import_hero_frames.py`
      (tách từ đoạn copy+meta lặp lại 3 lần tay trước đó — tham số hoá `--hero`/`--src`/`--dst`,
      giảm rủi ro gõ sai). Verify từng hero qua `execute_code` (`Bind()` nạp đúng 5/5 bộ) + chạy lại
      full suite sau MỖI hero — 423/423 xanh xuyên suốt, không có lần nào regress.

      **Kết quả: ĐỦ 6/6 class có rig + đủ 5 trạng thái, đã adopt thật trong Unity** (6 hero class
      template: `hero_ember_knight`/`hero_frost_sage`/`hero_gale_thief`/`hero_dawn_cleric`/
      `hero_shadow_fang`/`hero_bone_caller`).

      **Recolor 18 hero còn lại — XONG trong lượt tiếp theo.** Thêm 18 bảng màu element vào
      `character_rig.py` (dùng hàm `recolor_kit()` đã có, tái dùng silhouette hàm của class gốc,
      chỉ đổi dict màu). Sinh batch 18 hero × 5 state bằng 1 shell script (~30 giây). Review ảnh
      đại diện từng class (iron_bastion/void_scholar/night_stalker/moon_priestess/crimson_reaver/
      star_weaver) — đạt ngay lần đầu, không cần lặp. Import qua `import_hero_frames.py` batch.
      Verify `Resources.Load` qua `execute_code`: 90/90 state-sets OK (18 hero × 5 state), 0 FAIL.
      423/423 EditMode tests xanh (chỉ đụng art/asset, không đụng code C#). **24/24 hero now có
      đủ 5 animation states trong Unity.** `UnitView.LoadFrames(defId, state)` tự động nạp đúng
      theo defId — không cần thêm 1 dòng C# nào, đúng như thiết kế từ Phase 1.
- [x] **Giai đoạn 4 — Enemy/Boss: HOÀN TẤT.** 66/66 enemy (60 regular + 6 boss) có đủ 5 animation
      states (idle/attack/move/damage/die) trong Unity. Script `enemy_rig.py` (12 humanoid kits +
      10 creature draw functions) + `gen_all_enemies.sh` (batch gen) + `import_enemy_frames.py`
      (batch import). 1386 PNG + .meta. 18 visual archetypes: goblin/skeleton/zombie/brute/caster/
      knight (humanoid) + wolf/bat/slime/wisp/golem/serpent/spider/horror/toad/swarm + boss_lich/
      boss_drake. Không cần thêm C# — `UnitView.LoadFrames(defId, state)` đã đọc đúng từ
      Resources/Art/Characters/Enemies/{defId}/Animations/.
- [x] **Giai đoạn 5 — VFX skill còn lại: HOÀN TẤT.** Sinh 3 VFX mới (64×64, 4 frame mỗi loại):
      `vfx_wind_gust` (xoáy xanh lục), `vfx_light_radiant` (bùng sáng trắng/vàng), `vfx_magic_bolt`
      (cầu tím). Import vào `Art/VFX/`. `VfxPlayer.cs`: thêm 3 key "wind"/"light"/"magic" vào KEYS,
      fix `KeyForElement` (Wind→"wind", Light→"light" — trước đó sai: Wind→"slash", Light→"lightning").
      `CombatPresenter.cs`: refactor thành `ResolveVfxKey()`, thêm whitelist `_neutralMagicSkills`
      cho `skill_elemental_strike` + `skill_ultimate_nova` → "magic" VFX thay vì "slash". 423/423
      tests xanh. Toàn bộ 65 skill đã có VFX đúng element.

Ghi chú: mỗi giai đoạn là 1 khối việc lớn, khả năng trải dài NHIỀU lượt "tiếp tục cho tôi" tiếp
theo — cập nhật checklist trên + roadmap.md/object-map.md/memory sau MỖI giai đoạn hoàn tất, không
đợi xong hết mới cập nhật 1 lần.
