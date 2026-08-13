# TASK-HERO-ROSTER.md — Mở rộng roster 6 → 24 hero (plan.md §5.8)

> Thêm 18 hero còn lại trong bảng plan.md §5.8 (trước đó chỉ có tên/class/element/rarity, chưa có
> stat/skill/data thật) — dùng đúng cơ chế CSV → ScriptableObject sẵn có
> (`Tools/Import Game Data`), KHÔNG tạo asset tay qua Editor MCP như các hệ thống trước.

---

## 0. Phát hiện quan trọng trước khi làm

- **Pipeline CSV → SO đã tồn tại và hoạt động đầy đủ** (`Assets/Tools/DataImport/
  CsvToScriptableObject.cs`, menu `Tools/Import Game Data`) — 6 hero mẫu hiện có ĐÃ được sinh qua
  đường này (`Assets/_Project/Data/CSV/heroes.csv`/`skills.csv`), KHÔNG phải author tay. Import
  idempotent (ghi đè theo id, không tạo trùng) — an toàn chạy lại nhiều lần.
- **plan.md §5.8 tự nói rõ:** "18 hero còn lại — dùng làm template cho 18 hero còn lại" nghĩa là
  chính plan.md đã định hướng dùng 6 hero mẫu làm khuôn, không cần thiết kế 18 skill kit hoàn toàn
  mới. Quyết định: mỗi hero mới dùng NGUYÊN stat + 4 skill đầu (slot 0-3) của hero cùng class đã
  có, chỉ thêm 1 skill Ultimate (slot 4) MỚI, riêng cho từng hero — cân bằng giữa "đủ khác biệt để
  chơi được" và "không phải thiết kế 90 skill từ đầu".
- **`DataValidator.ValidateAll()`** (`Tools/Validate Game Data`) tự chạy cuối `ImportAll()` — báo
  lỗi tham chiếu skillId chết, id trùng, thiếu nameKey. Dùng để bắt lỗi ngay khi import, không cần
  tự viết validation riêng.
- **`GachaSystem.HeroPool(rarity)` đọc trực tiếp `Resources.LoadAll<HeroDefinitionSO>("Data/
  Heroes")` lọc theo Rarity** — hero mới KHÔNG cần đăng ký gì thêm, tự động vào pool gacha ngay
  khi asset tồn tại đúng Rarity.
- **`LocalPlayerRepository.CreateNewProfile` dùng mảng hard-code 6 defId cố định** (không phải
  "load hết heroes.csv") — xác nhận KHÔNG cần sửa: người chơi mới vẫn chỉ có 6 hero khởi điểm, 18
  hero còn lại vào qua Gacha đúng như thiết kế (comment sẵn có: "P4 (Gacha) sẽ là nguồn hero chính
  thức").
- **`AwakeningCatalog`/`InnatePassiveCatalog` chỉ phủ 6 hero cũ** — `Get()` trả về an toàn
  (không throw) cho hero chưa có entry, đúng pattern "dư địa mở rộng có chủ đích" đã dùng ở
  task-ascend.md §10. KHÔNG mở rộng 2 catalog này cho 18 hero mới ở lượt này — ghi rõ ngoài phạm
  vi (mục 4).

## 1. Thiết kế 18 skill Ultimate mới

- [x] 3 hero/class × 6 class = 18 skill mới, mỗi skill bám khuôn Ultimate của hero mẫu cùng class
      (Type/DamageType/Target/CommandType=Charge giống hệt), chỉ đổi Element (theo hero), giá trị
      Power/Poise nhích theo Rarity, và status áp dụng đổi theo Element (Fire→Burn, Water/Wind→
      SpdDown, Earth→DefDown, Dark→Curse/Blind, Light→AtkDown/Blind — tái dùng đúng bảng status
      plan.md §4.11, không bịa loại mới):
      `skill_bastion_upheaval`/`skill_tide_surge`/`skill_storm_bulwark` (Vanguard),
      `skill_dance_of_blades`/`skill_crimson_harvest`/`skill_quarry_smash` (Slayer),
      `skill_pyroclasm`/`skill_terra_wrath`/`skill_void_cataclysm` (Arcanist),
      `skill_verdant_rebirth`/`skill_lunar_rebirth`/`skill_spring_revival` (Warden, kiểu revive
      giống `skill_rebirth_dawn`),
      `skill_umbral_flurry`/`skill_static_barrage`/`skill_mirage_onslaught` (Trickster),
      `skill_wild_stampede`/`skill_infernal_summoning`/`skill_astral_convergence` (Summoner).
- [x] Thêm 18 dòng vào `Assets/_Project/Data/CSV/skills.csv` (giữ nguyên 47 dòng cũ).

## 2. Thêm 18 hero vào `heroes.csv`

- [x] 18 dòng mới trong `Assets/_Project/Data/CSV/heroes.csv`, đúng ID/Class/Element/Rarity theo
      bảng plan.md §5.8: `hero_iron_bastion`(Vanguard/Earth/Rare), `hero_tide_warden`(Vanguard/
      Water/Common), `hero_stormguard`(Vanguard/Wind/Legendary), `hero_blade_dancer`(Slayer/Wind/
      Epic), `hero_crimson_reaver`(Slayer/Fire/Rare), `hero_stone_breaker`(Slayer/Earth/Common),
      `hero_pyromancer`(Arcanist/Fire/Rare), `hero_terra_seer`(Arcanist/Earth/Common),
      `hero_void_scholar`(Arcanist/Dark/Legendary), `hero_grove_keeper`(Warden/Earth/Epic),
      `hero_moon_priestess`(Warden/Light/Legendary), `hero_spring_medic`(Warden/Water/Common),
      `hero_night_stalker`(Trickster/Dark/Epic), `hero_spark_runner`(Trickster/Wind/Common),
      `hero_mirage_fox`(Trickster/Light/Legendary), `hero_beast_tamer`(Summoner/Earth/Rare),
      `hero_flame_binder`(Summoner/Fire/Epic), `hero_star_weaver`(Summoner/Light/Epic).
- [x] Stat gốc (STR/CON/INT/DEX/AUR/LUK) + PoiseMax = NGUYÊN VĂN theo hero mẫu cùng class (không
      bịa số riêng — đúng quyết định mục 0).
- [x] `skillIds` = 4 skill đầu của hero mẫu cùng class + 1 Ultimate mới của chính hero đó.
- [x] `spriteFolder` = đúng defId (chưa có art thật — xem mục 4 ngoài phạm vi).

## 3. Import & verify

- [x] Chạy `Game.Tools.DataImport.CsvToScriptableObject.ImportAll()` qua `execute_code` — kết quả
      **65 skill, 24 hero, 65 enemy, 14 equipment**, `DataValidator`: **0 lỗi, 0 cảnh báo**.
- [x] Verify build unit thật: `BuildUnitFromDefinition("hero_stormguard", ..., star: 6)` qua
      reflection — ra đúng 5 skill (kể cả slot 4 Ultimate mới `skill_storm_bulwark`, power 2.3,
      AoE), stat/HP tính đúng.
- [x] Verify `GachaSystem.HeroPool` — 24 hero chia đúng theo Rarity: Common 6 · Rare 6 · Epic 7 ·
      Legendary 5 (khớp chính xác plan.md §5.8: 2 hero mẫu cũ + hero mới cộng đúng từng bậc).
- [x] Verify `HeroDisplayUtil.FormatName` ra tên hiển thị đúng cho hero mới (VD "Iron Bastion",
      "Moon Priestess") — không cần sửa gì (hàm đã tổng quát theo defId, không hard-code danh
      sách).
- [x] Verify `AwakeningCatalog.Get`/`InnatePassiveCatalog.Get` không throw cho hero mới (trả rỗng
      an toàn).
- [x] Chạy full EditMode suite: **247/247 xanh** (không đổi số test — roster không cần test riêng,
      hành vi đã được `DataValidator` + build-unit smoke test bao phủ).

## 4. Awakening + Innate Passive cho 18 hero mới — xây thật (lượt sau, đã làm)

**Phát hiện quan trọng trước khi làm:** `AscendSystem.CanAscend` chỉ kiểm `hero.Star < MAX_STAR`
(6) — **KHÔNG enforce star cap theo rarity** như plan.md §5.4 mô tả (Common lẽ ra chỉ tới ★4, Rare
★5). Nghĩa là MỌI hero, kể cả 5 hero Common vừa thêm, đều có thể lên ★6 và kích hoạt Awakening
trong code hiện tại — không có hero nào "không bao giờ cần Awakening" để bỏ qua.

- [x] **Quyết định thiết kế:** mỗi hero mới GIỮ NGUYÊN trigger + hình dạng hiệu ứng
      (Modifiers/Applies) của hero mẫu CÙNG CLASS cho cả Awakening lẫn Innate — đúng tinh thần
      "class quyết định cơ chế" (plan.md §5.1), chỉ chỉnh số liệu theo rarity (Legendary nhỉnh
      hơn 1 bậc: +1 stack hoặc +modifier tương ứng) và, riêng Arcanist Awakening (OnHitDealt),
      đổi hẳn StatusId theo hệ để giữ đúng phối hệ đã dùng cho skill Ultimate (Water→SpdDown,
      Fire→Burn, Earth→DefDown, Dark→Curse).
- [x] `AwakeningCatalog.cs` — thêm 18 case (18 hero mới), mỗi case `Id` riêng
      (`awaken_{heroid}_{tên riêng}`), không trùng.
- [x] `InnatePassiveCatalog.cs` — thêm 18 case tương tự (`innate_{heroid}_{tên riêng}`), Innate
      luôn NHẸ HƠN Awakening cùng hero (đúng nguyên tắc gốc từ task-innate-passive.md).
- [x] `PassiveProcessorTests.cs` — đổi `AwakeningCatalog_AllSixHeroes_HaveAPassive`/
      `InnatePassiveCatalog_AllSixHeroes_HaveAPassive` thành `...AllTwentyFourHeroes...`, thêm 18
      `[TestCase]` mỗi bên (36 test case mới tổng cộng).
- [x] Verify qua `execute_code` (không cần Play mode — thuần data lookup + build unit thật):
      build `hero_void_scholar` ★6 → `unit.Awakening`/`unit.Passive` đúng cả 2, không null; quét
      toàn bộ 18 hero mới → 0 null, 0 `Id` trùng ở cả Awakening lẫn Innate.
- [x] Chạy full EditMode suite: **283/283 xanh** (247 trước lượt này + 36 test case mới).

## 5. Art/sprite cho 18 hero mới — xây thật (lượt sau, đã làm)

**Phát hiện quan trọng trước khi làm:** thực tế game chỉ dùng **1 sprite tĩnh 32×32 mỗi hero**
(`hero_{id}_v1_00.png`, `Resources.Load<Sprite>` trực tiếp trong `BattleSceneInstaller`/
`TeamSelectScreen`/`HeroDetailScreen`) — KHÔNG phải bộ 7 animation clip (.aseprite) như plan.md
§2.2 mô tả, và cũng KHÔNG có portrait 64×64 riêng (plan.md §2.1) — cả 6 hero gốc cũng chỉ có đúng
1 file này. Phạm vi lượt này bám theo THỰC TẾ đã triển khai, không theo plan.md đầy đủ.

- [x] Dùng đúng skill `pixel-art-pipeline` đã cài sẵn trong dự án (`Tools/pixel-art-pipeline/`,
      cùng bộ script đã tạo 6 hero gốc — không phải cài mới). Checkpoint duy nhất có sẵn:
      `PixelartSpritesheet_V.1.ckpt` — ghi nhận checkpoint này LUÔN sinh lưới 4 nhân vật/ảnh
      (turnaround-style) dù prompt không yêu cầu — 6 hero gốc cũng vậy, phải cắt lấy 1 ô.
- [x] `Tools/art_catalog.json` — thêm 18 mục hero mới (giữ đúng format/style prompt của 6 hero
      gốc: `chất liệu/màu áo giáp, vũ khí, phụ kiện`, `cfg:9.0, steps:34, size:512×512,
      variants:3`), seed riêng mỗi hero. File này giờ là nguồn sự thật DUY NHẤT cho cả 24 hero
      (không còn file `_v2` tách riêng).
- [x] Sinh 54 ảnh raw qua ComfyUI (`comfy_gen.py --catalog`, chạy nền — mất khoảng vài phút).
- [x] **Bắt buộc xem lại trước khi đi tiếp** (đúng luật skill) — phát hiện 2 lỗi, sửa bằng cách
      **đổi prompt/seed rồi sinh lại**, KHÔNG cố cứu bằng hậu xử lý:
      - `hero_beast_tamer`: cả 3 variant ra khối lông/tóc không rõ hình người (prompt gốc
        "wild hair" + "fur cloak" lấn át toàn bộ silhouette). Sinh lại với prompt tường minh hơn
        ("leather vest", "short spiky hair", "humanoid, visible arms and legs") → ra nhân vật rõ
        ràng.
      - `hero_grove_keeper`: cả 3 variant có NỀN MÀU XANH LÁ trùng màu áo choàng lá cây → bước
        tách nền (flood-fill) ăn luôn 1 phần nhân vật (mảnh trôi nổi rời rạc). Sinh lại với prompt
        ép "plain gray background, no forest/foliage background" + negative prompt tương ứng →
        nền xám sạch.
- [x] **Phát hiện quy trình quan trọng** (không có trong tài liệu skill, phải tự suy ra bằng cách
      đối chiếu ngược với `hero_ember_knight_v1_00.png` đã có sẵn): lệnh `post_process.py --slice`
      với `--canvas` áp dụng canvas-fit NGAY khi cắt (lúc nền còn chưa trong suốt, `trim()` chưa
      có tác dụng) → nhân vật bị co nhỏ sai tỉ lệ trong khung. Quy trình ĐÚNG là 2 bước tách biệt:
      (1) `--slice --cols 4` KHÔNG kèm `--canvas` → 4 ảnh cột riêng (nền còn đục); (2) chạy pipeline
      đầy đủ (`--in <ảnh đã chọn> --target-height 32 --canvas 32 --anchor bottom --palette
      Tools/palette.json --key auto`) — tách nền bằng flood-fill (`--key auto`, không phải
      magenta vì checkpoint này luôn ra nền xám) rồi mới trim/scale/canvas. Verify bằng cách tái
      tạo lại đúng `hero_ember_knight_v1_00.png` từ raw gốc và so khớp — ra gần như giống hệt bản
      đã ship trước khi áp dụng cho 18 hero mới.
- [x] Luôn lấy Ô 0 (trái nhất) trong lưới 4 nhân vật cho cả 18 hero — đã verify ô này chất lượng
      ổn định qua bài test tái tạo `ember_knight` ở trên.
- [x] `python3 post_process.py --verify-palette` — cả 18 file: **0 màu ngoài palette** (đúng
      `Tools/palette.json`, palette 48 màu plan.md §2.1).
- [x] Copy vào đúng cấu trúc `Assets/_Project/Resources/Art/Characters/Heroes/hero_{id}/
      hero_{id}_v1_00.png` — khớp 100% quy ước đường dẫn code đang đọc, không cần sửa code.
- [x] Set `TextureImporter` qua Unity API (`execute_code`, không hand-craft `.meta` YAML) khớp
      chính xác asset tham chiếu: `textureType=Sprite`, `spriteImportMode=Single`,
      `spritePixelsPerUnit=32`, `filterMode=Point`, `alphaIsTransparency=true`,
      `mipmapEnabled=false`, `textureCompression=Uncompressed`.
- [x] **Verify Play mode thật**: gán tạm 18 hero mới vào profile (chỉ trong RAM, không save — tự
      mất khi thoát Play mode, không làm bẩn save thật), mở `TeamSelectScreen` qua UI thật (click
      node → panel hero list), chụp màn hình — cả 18 hero hiện đúng sprite, đúng màu viền theo
      rarity (`RarityColor()` có sẵn), phân biệt rõ ràng với nhau và với 6 hero gốc.
- [x] Full EditMode suite sau khi thêm asset: **283/283 xanh** (không đổi số test — thuần asset,
      không có test riêng cho art).

## 6. Ngoài phạm vi (ghi rõ)

- **Animation đầy đủ (idle/walk/attack/skill/hit/down/victory theo plan.md §2.2)** — vẫn chỉ có 1
  pose tĩnh mỗi hero, giống 6 hero gốc. Không phải thiếu sót riêng của lượt này — code hiện tại
  (`BattleSceneInstaller`/`UnitView`) cũng chưa có hệ animation state machine để dùng nhiều clip
  dù có art. Khối lượng lớn (24 hero × 7 clip = 168 file + Animator Controller), để riêng hẳn.
- **Portrait 64×64 riêng biệt (plan.md §2.1)** — không cần, code dùng chung 1 sprite 32×32 cho cả
  battle lẫn portrait UI (đã xác nhận ở phần phát hiện đầu mục 6).
- **Lore/mô tả chi tiết** — chỉ có `nameKey` (dùng để hiển thị tên), chưa có mô tả tiểu sử/skill
  desc riêng biệt cho từng hero mới (skill desc nói chung toàn game cũng chưa có, không phải thiếu
  sót riêng của lượt này).
- **Cân bằng qua Balance Harness** — chưa chạy lại `Tools/Balance Harness` với roster 24 hero (dữ
  liệu cũ vẫn hợp lệ vì roster không ảnh hưởng công thức Ascend/Gacha pity, chỉ ảnh hưởng SỐ
  LƯỢNG hero mỗi bậc rarity — đã verify đúng ở mục 3, không cần harness riêng cho việc này).
- **18 hero mới dùng CHUNG stat/skill 0-3 với hero mẫu cùng class** — nghĩa là 4 hero cùng class
  hiện chỉ khác nhau ở Element + skill Ultimate, chưa có nhánh build/playstyle khác biệt sâu như
  plan.md có thể ngụ ý ở phần lore từng hero. Đây là đánh đổi có chủ đích (mục 0) để mở khoá roster
  nhanh — tinh chỉnh sự khác biệt sâu hơn (nếu cần) để lượt sau, khi có Awakening/Passive riêng.
