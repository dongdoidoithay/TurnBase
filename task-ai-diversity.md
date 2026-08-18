# Task: Enemy AI diversity theo Archetype

Tiếp nối audit ở object-map.md §12.1 (2026-08-18): 56/66 enemy (85%) dùng chung ĐÚNG 1
`AIProfile` ("ai_special") — Healer/Tank/Debuffer/Caster đều đánh giống hệt nhau
("55% skill ô 1, 40% đánh thường, luôn luôn") dù skill kit thật sự khác nhau. Người dùng chọn làm
thật qua AskUserQuestion (cùng đợt với Arena/Accessibility/content localization).

## Thiết kế

`BattleSceneInstaller.BuildAi(profileId, archetype)` — khi `profileId=="ai_special"`, dispatch qua
`BuildArchetypeAi(archetype)` mới thay vì 1 bộ luật cố định. Dùng ĐÚNG `AIConditionType` đã có sẵn
trong `AIController.cs` (không thêm condition mới):

| Archetype | Luật |
|---|---|
| Healer | `AllyHpBelow(50) → skill ô 1 (w=90)`, còn lại đánh thường (w=35) — `PickTarget` đã tự chọn đồng minh HP% thấp nhất cho skill `TargetsAllies` |
| Debuffer | Áp debuff ĐỊNH KỲ (`cooldown=3`, w=65) thay vì spam mỗi lượt |
| Caster/Bomber | Ưu tiên phép/AoE gần như luôn luôn (w=65, cooldown=1) — thuần tấn công |
| Tank | Đánh thường ổn định (w=45) — chỉ dùng skill riêng khi CHÍNH bản thân dưới 50% HP |
| Brute | Đòn nặng ĐỊNH KỲ (`cooldown=2`, w=75) — "tích rồi nện" |
| Archer/Skirmisher | Ưu tiên HẲN khi mục tiêu đã Break (`TargetIsBroken`, w=85) — cơ hội chủ nghĩa |
| Grunt/Swarm/Elite (default) | Giữ NGUYÊN hành vi ai_special cũ — lính thường/số đông không cần chiến thuật riêng |

`ai_basic`/`ai_boss` KHÔNG đổi. Method mới `BuildArchetypeAi` tách riêng, không sửa logic
`AIController`/`AICondition` nào (0 rủi ro cho hệ AI lõi đã có test).

## Verify

- `validate_script` + compile toàn project 0 lỗi. **647/647 test xanh** (không đụng
  `AIController`/`AICondition`, không test EditMode nào exercise `BattleSceneInstaller.BuildAi` nên
  không có gì để regress).
- Đọc thật `BuildArchetypeAi` qua reflection cho 6 archetype — xác nhận đúng luật đã thiết kế
  (weight/cooldown/condition khớp bảng trên).
- **Verify hành vi THẬT** (không chỉ đọc cấu trúc rule): dựng 1 `AIController` + `BattleState` tối
  giản với Healer thật (skill Heal `TargetsAllies`) — khi có đồng minh HP 30%, AI chọn ĐÚNG skill
  hồi máu nhắm đúng đồng minh đó; khi KHÔNG ai cần hồi, AI tự rơi về đánh thường thay vì spam hồi
  vô nghĩa. Cả 2 chiều đều đúng như thiết kế.

## Ngoài phạm vi

- Không sửa `AIConditionType`/`AIController` (đủ condition có sẵn cho thiết kế này).
- Không audit riêng skill kit từng enemy để xác nhận skill ô 1 luôn khớp ý nghĩa Archetype (đã
  spot-check 2 Healer thật có `skill_revive_ally` Type=Heal khớp đúng — tin cậy hợp lý cho phần còn
  lại dựa trên cùng quy ước đặt tên, không audit hết 56 enemy).
