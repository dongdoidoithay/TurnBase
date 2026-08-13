# TASK-CHAPTERS.md — 5 chương (plan.md §8.2) — rà soát, KHÔNG phải xây từ đầu

> Yêu cầu ban đầu: "4 chương còn lại". Trước khi code, rà soát lại thấy `roadmap.md`/
> `object-map.md` đang ghi **SAI** — 5 chương thực ra đã chơi được từ trước, không phải chỉ 1.
> Lượt này chủ yếu là **sửa tài liệu sai** + vá 1 gap thật (node Mystery) + verify bằng chứng cụ
> thể, không phải xây nội dung 4 chương từ số 0.

---

## 0. Phát hiện quan trọng — tài liệu cũ SAI

`roadmap.md` §0.1 (viết 2026-08-10, cùng ngày) ghi: *"node map generator chạy được nhưng chỉ 1
chương chơi thật (kế hoạch 5)"*. Kiểm tra lại thấy nhận định này **sai**, dựa trên suy đoán không
đủ chứng cứ (chỉ đếm tổng số enemy, không kiểm phân bố theo `Chapter`). Bằng chứng thật sau khi
rà kỹ:

- **65 enemy đã phân bố đủ cả 5 chương**: ch1=11, ch2=13, ch3=13, ch4=14, ch5=14 (đếm trực tiếp
  field `Chapter` trong asset, không phải suy đoán).
- **Cả 5 boss đã tồn tại và scale hợp lý theo chương** — `boss_alpha_wolf`(ch1, Poise 140, CON 45)
  → `boss_goblin_king`(ch2) → `boss_lich`(ch3) → `boss_magma_drake`(ch4) → `boss_void_king`(ch5,
  Poise 180, CON 60) — độ khó tăng dần đúng thiết kế, KHÔNG phải placeholder yếu như nhau.
- **`MetaSceneInstaller.PickBoss`/`PickEnemies`/`NodeMapGenerator.Generate` đã chapter-aware đầy
  đủ** từ trước — verify qua `execute_code`: sinh map + chọn địch + chọn boss cho cả 5 chương,
  không lỗi, đúng enemy/boss theo từng chương (VD ch2 ra `enemy_poison_toad`/`swamp_troll` khớp
  biome Đầm Lầy, ch4 ra `enemy_volcanic_crab`/`molten_brute` khớp Núi Lửa).
- **Bằng chứng thực tế mạnh nhất**: save profile hiện tại của chính dự án này có
  `Progress.ChapterUnlocked = 6` — nghĩa là qua các phiên chơi/test trước đây, người dùng ĐÃ hạ
  cả 5 boss và đi tới chương 6 (vượt cả phạm vi 5 chương v1.0). 5 chương không chỉ "chơi được" mà
  **đã thực sự được chơi qua**.
- Verify thêm bằng cách giả lập thắng boss 5 lần liên tiếp qua `ApplyPendingBattleResult` (đúng
  hàm production, không phải code test riêng) — cả 5 lần `ChapterUnlocked` tăng đúng, không
  exception.

**Kết luận:** không cần "xây 4 chương còn lại" — chúng đã tồn tại. Việc thật cần làm là (a) sửa
tài liệu cho đúng, (b) rà tìm gap THẬT còn sót (có, tìm thấy 1 — mục 1), (c) ghi rõ những phần
CHƯA đầy đủ so với plan.md để không ai lại hiểu nhầm lần nữa (mục 2).

## 1. Gap thật tìm thấy — node Mystery chưa xử lý (đã vá)

`OnNodeClicked` switch có case cho Battle/Elite/Boss/Rest/Treasure/Event/Shop — **KHÔNG có case
Mystery**, rơi vào `default: ResolveSimple(node, "Not available in this demo — skipping this
node.")`. Mystery chỉ 2% tỉ lệ node (plan.md §8.1) nên không chặn hẳn việc chơi qua chương nào,
nhưng vẫn là 1 loại node "vỡ trải nghiệm" nếu người chơi bốc trúng.

- [x] Thêm `case NodeType.Mystery: ResolveMystery(node); break;`.
- [x] `ResolveMystery(node)` — đúng plan.md §8.1 "ngẫu nhiên trong các loại trên": roll theo tỉ lệ
      6 loại còn lại (Battle+Elite gộp 61%, Treasure 10%, Event 12%, Shop 8%, Rest 9% — chuẩn hoá
      lại từ bảng gốc, bỏ Mystery/Boss). KHÔNG mutate `node.Type` đã lưu trên map (giữ nguyên hiển
      thị Mystery), chỉ chọn ngẫu nhiên NGAY LÚC BẤM.
      **Quyết định đơn giản hoá:** gộp Elite vào tỉ lệ Battle thường thay vì tách riêng — vì
      `LaunchBattle` đọc thẳng `node.Type` để quyết định Elite hay Battle thường, mà node vẫn giữ
      Type=Mystery ở nhánh này (không mutate map đã lưu) nên không thể trực tiếp trigger nhánh
      Elite — chấp nhận đơn giản hoá cho 2% node hiếm gặp này thay vì thêm cơ chế truyền cờ riêng.
- [x] Verify qua `execute_code` (Play mode thật, gọi đúng method production qua reflection): roll
      `ResolveMystery` 10 lần liên tiếp → 0 exception, dispatch đúng tới các resolver khác nhau.
- [x] Full EditMode suite sau khi sửa: **283/283 xanh** (không đổi số test — thuần logic điều
      hướng node, đã có test node map ở tầng khác; không thêm test riêng vì hành vi ngẫu nhiên khó
      assert xác định mà không đổi kiến trúc — chấp nhận verify bằng execute_code thay vì unit test).

## 2. Vẫn CHƯA đầy đủ so với plan.md — ghi rõ để không hiểu nhầm lần 2

- **Background/tileset riêng theo biome trong trận (Battle scene)** — `BattleSceneInstaller`
  KHÔNG có logic đổi background theo chương; mọi trận nhìn giống hệt nhau bất kể đang ở Đồng Cỏ
  hay Núi Lửa. Art THÔ đã tồn tại (`Tools/raw/background/bg_meadow_*.png`,
  `bg_swamp_*.png`, `bg_volcano_*.png`, `bg_crypt_*.png` — sinh qua pixel-art-pipeline trước đây)
  nhưng CHƯA hậu xử lý/import vào `Resources` — chỉ có màu tint nhẹ (`ChapterAccent`) trên UI
  Node Map, không phải background thật trong trận.
- **Event/Rest node chỉ 1 kết quả cố định** — `ResolveEvent` là 1 lần roll 60/40 vàng, `ResolveRest`
  là hồi máu + phí cố định — KHÔNG phải "2-3 lựa chọn có rủi ro" × 30 sự kiện như plan.md §8.1/§5.1
  mô tả. Chức năng, không placeholder-vỡ, nhưng nội dung nông hơn nhiều so với thiết kế gốc.
- **Loot table chỉ có bảng wildcard `Chapter=0`** (task-loottable.md đã ghi từ trước, nhắc lại ở
  đây cho đủ ngữ cảnh) — vật liệu rơi ra không đổi theo chương dù enemy/độ khó có đổi.
- **`ArchetypeId`/AI profile đa dạng ra sao chưa audit** — 65 enemy có đủ nhưng chưa kiểm từng
  con có AI rule ý nghĩa hay dùng chung 1 profile generic.

## 3. Sửa tài liệu (roadmap.md, object-map.md)

- [x] `roadmap.md` §0.1 dòng P5 — xoá câu sai "chỉ 1 chương chơi thật", thay bằng số liệu thật.
- [x] `object-map.md` §12.1 — thêm phát hiện tương tự, tránh lặp lại đánh giá sai ở lượt sau.
