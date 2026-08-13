# Task: AssemblyRuleTests (plan.md §11.2 + R8)

Yêu cầu: test kiểm tra đồ thị phụ thuộc assembly theo plan.md §11.2 không bị vi phạm.
Rủi ro R8: "Deterministic bị phá âm thầm" — `AssemblyRuleTests` chạy mỗi push để bắt sớm.

## §0. Findings

**Assembly graph thực tế (đọc từ asmdef):**
```
Game.Core       ← []                                        ✓
Game.Data       ← [Core]                                    ✓
Game.Combat     ← [Core, Data]           ⛔ CẤM: UI, CombatView, Meta, Services
Game.Services   ← [Core, Data]           ⛔ CẤM: Combat, Meta, CombatView, UI
Game.Meta       ← [Core, Data, Combat, Services]            ✓
Game.CombatView ← [Core, Data, Combat, Services, UI, Meta]  ✓
Game.UI         ← [Core, Data, Combat, Meta, Services]      ✓
Game.Bootstrap  ← all                                       ✓
```

**Kỹ thuật test:**
- `AppDomain.CurrentDomain.GetAssemblies()` → tìm assembly theo tên
- `Assembly.GetReferencedAssemblies()` → danh sách assembly tham chiếu trực tiếp
- `Assembly.GetTypes()` + reflection → quét field/property/method để tìm dùng type cấm

**Cấm dùng type (không cấm import assembly vì nằm trong CoreModule):**
- `UnityEngine.Random` — phải dùng `IRandomSource`/`XorShiftRandom` thay thế
- `UnityEngine.Time` — combat thuần C#, không cần Time

**Không kiểm tra được qua reflection thông thường:**
- Calls bên trong method body (cần Mono.Cecil / IL inspection)
- Kiểm tra field/property/method signature là đủ cho mục tiêu regression

## §1. Scope

**Trong phạm vi:** `AssemblyRuleTests.cs` (~9 tests) trong `Architecture/`

**Ngoài phạm vi:**
- IL-level call scanning (cần Mono.Cecil, không có sẵn)
- PlayMode assembly tests

## §2. Design — 9 tests

| Test | Assert |
|---|---|
| `Combat_DoesNotReference_CombatView` | refs("Game.Combat") không chứa "Game.CombatView" |
| `Combat_DoesNotReference_UI` | refs không chứa "Game.UI" |
| `Combat_DoesNotReference_Meta` | refs không chứa "Game.Meta" |
| `Combat_DoesNotReference_Services` | refs không chứa "Game.Services" |
| `Combat_NoTypeMember_UsesUnityEngineRandom` | quét field/prop/method signature Game.Combat |
| `Combat_NoTypeMember_UsesUnityEngineTime` | quét field/prop/method signature Game.Combat |
| `Core_DoesNotReference_AnyGameAssembly` | refs("Game.Core") không có "Game.*" nào |
| `Data_DoesNotReference_HigherLayers` | refs("Game.Data") không có Combat/Meta/Services/... |
| `Services_DoesNotReference_UpperLayers` | refs("Game.Services") không có Combat/Meta/... |

Helper `ScanTypeMembersForType(asm, forbiddenType)` → List<string> violations (field/prop/param/return).

## §3. Implementation Checklist

- [x] Viết `task-assembly-rules.md` (file này)
- [x] Viết `AssemblyRuleTests.cs` vào `Assets/Tests/EditMode/Architecture/`
- [x] `run_tests` → **494/494 xanh** (486 cũ + 9 AssemblyRuleTests; tổng 494 = baseline + 8 net do 1 Addressable package test không xuất hiện ở lần chạy thứ 2)
- [x] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`
