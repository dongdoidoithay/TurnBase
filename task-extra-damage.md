# TASK-EXTRA-DAMAGE.md — Nối `PassiveData.ExtraDamagePercent` vào `DamageCalculator`

> Mảnh cuối cùng còn thiếu của `PassiveData` (task-ascend.md §11 mục 2). Quy mô nhỏ, thuần kỹ
> thuật — không cần hero/data mới, chỉ nối 1 field đã khai báo từ trước vào pipeline sát thương.

---

## 0. Bối cảnh — vì sao KHÔNG dùng `StatModifier(DmgBonusPct)` có sẵn

`PassiveData.Modifiers` (StatModifier[]) đã hỗ trợ `StatType.DmgBonusPct` — một passive muốn tăng
sát thương **vĩnh viễn** hoàn toàn có thể dùng `Modifiers` (tự động chảy qua
`CombatUnit.PassiveModifiers` → `ComputeStats()`, không cần code gì thêm, đã hoạt động).

`ExtraDamagePercent` khác về bản chất: nó gắn với `Trigger` cụ thể — ý đồ gốc (theo tên field và
comment trong `PassiveProcessor.cs`) là bonus sát thương áp cho **hit đang xảy ra tại thời điểm
trigger nổ**, không phải cộng dồn vĩnh viễn vào stat. `PassiveProcessor.Fire`/`Apply` hiện chạy
**sau** khi damage đã tính xong (`ActionResolver.ApplyOneHit` gọi `_passive.TriggerOnHitDealt`
sau `DamageCalculator.Calculate`), nên không thể sửa retroactive damage đã tính — phải đọc
`PassiveData` trực tiếp bên trong `DamageCalculator.Calculate`, không qua `PassiveProcessor`.

**Phạm vi tối thiểu:** chỉ áp dụng cho passive có `Trigger == PassiveTrigger.OnHitDealt` (ý nghĩa
"mỗi cú đánh trúng đều cộng thêm X% sát thương") — đúng 1 trường hợp sử dụng rõ ràng, không cần
xử lý mọi 10 trigger.

---

## 1. `DamageCalculator.Calculate` — thêm bước đọc `ExtraDamagePercent`

- [x] Trong `Assets/_Project/Scripts/Combat/Systems/DamageCalculator.cs`, bước 7 ("modifier
      tổng", ngay cạnh dòng `dmg *= 1f + atkStats.DmgBonus;`): thêm
      `dmg *= 1f + ExtraDamageFrom(attacker.Passive) + ExtraDamageFrom(attacker.Awakening);`
- [x] Helper `private static float ExtraDamageFrom(PassiveData p) => (p != null &&
      p.Trigger == PassiveTrigger.OnHitDealt) ? p.ExtraDamagePercent / 100f : 0f;` — private
      static, cạnh các helper khác trong file.
- [x] KHÔNG đổi thứ tự 9 bước hiện có (comment class ghi rõ "THỨ TỰ CÁC BƯỚC LÀ BẮT BUỘC") — chỉ
      chèn thêm 1 dòng vào bước 7 đã có sẵn phép nhân modifier tổng, không thêm bước mới.
- [x] Cập nhật comment trong `PassiveProcessor.cs` (hiện ghi "ExtraDamagePercent... CHƯA nối vào
      DamageCalculator") — xoá phần "CHƯA", ghi rõ chỉ áp dụng khi `Trigger == OnHitDealt`.

## 2. Nội dung — dùng field này ở đâu

- [x] KHÔNG bắt buộc phải gán `ExtraDamagePercent` cho hero nào trong `AwakeningCatalog`/
      `InnatePassiveCatalog` (mục task-innate-passive.md) lượt này — mục tiêu là nối kỹ thuật,
      không phải thêm nội dung. Nếu tiện thì cân nhắc 1 hero dùng thử (không bắt buộc).

## 3. Test

- [x] `DamageCalculatorTests.cs` (file có sẵn) — thêm test: `attacker.Passive` có
      `Trigger=OnHitDealt, ExtraDamagePercent=20` → damage tính ra cao hơn đúng 20% so với không
      có passive (so sánh 2 lần gọi `Calculate` cùng seed/rng, chỉ khác có/không gán Passive).
- [x] Test passive có `Trigger` KHÁC `OnHitDealt` (vd `OnKill`) nhưng vẫn có
      `ExtraDamagePercent` — phải KHÔNG áp dụng (đúng phạm vi tối thiểu ở mục 0).
- [x] Test `attacker.Passive == null` và `attacker.Awakening == null` — không lỗi, không đổi
      damage (regression cho unit thường/enemy chưa có passive nào).
- [x] Chạy full EditMode suite, phải xanh 100%.

## 4. Verification

- Chạy EditMode test qua `mcp__unityMCP__run_tests`.
- Không cần Play mode riêng cho mục này (thuần logic tính toán, đã có unit test đủ) — nhưng nếu
  đã gán `ExtraDamagePercent` cho 1 hero thật ở mục 2 thì verify nhanh qua Play mode như các mục
  trước.
