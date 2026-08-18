# Task: Content Localization — NodeChoice, Shop, Mail

plan.md §10.7 nhóm "Nội dung còn hardcode". Người dùng chọn làm thật qua AskUserQuestion (cùng đợt
Arena/AI diversity/Accessibility). 3 mảng nội dung còn tiếng Anh cứng: option/kết quả NodeChoice
(Rest/Event), tên vật phẩm Shop, nội dung thư chào mừng.

## §1. NodeChoiceSystem / NodeChoiceScreen

`NodeChoiceOption` (struct, pure — không có `ILocalizationService`) thêm `LabelKey`/`FlavorKey` song
song với `Label`/`Flavor` gốc (giữ làm fallback tiếng Anh khi `_loc == null`). `NodeChoiceResult`
tương tự thêm `ResultKey`/`Args` — kết quả có SỐ ĐỘNG (gold nhận/mất, tên hero, slot skill) nên
không thể chỉ đổi 1 string tĩnh, phải mang theo `(key, args[])` để UI gọi `_loc.Get(key, args)`.

`NodeChoiceScreen` (có `_loc`, lấy qua `ServiceLocator.TryGet` trong `Open()`) đọc `LabelKey`/
`FlavorKey`/`ResultKey` khi `_loc != null`, fallback English gốc khi không có service — đúng mẫu đã
lặp lại nhiều lần trong dự án (Arena/Accessibility/...).

Biết trước: `nodechoice.result.rest_train` truyền `chosenHero.DefId` RAW (không qua `_loc.GetName`)
làm arg — giới hạn có sẵn từ trước, không làm xấu thêm bởi lượt sửa này (đổi cần thêm tham chiếu
`HeroDisplayUtil`/`GetName` vào 1 lớp pure hiện không có, việc riêng).

## §2. ShopScreen — CATALOG

`CatalogItem` (struct) thêm `LabelKey` song song `Label`. `CATALOG` (10 dòng: 3 Essence + Core +
6 vật phẩm tiêu hao) mỗi dòng thêm 1 key `shop.item.*`. Dòng render duy nhất (`Refresh()`, gán
`_rowNameLabels[i].text`) đổi sang `_loc != null ? _loc.Get(item.LabelKey) : item.Label`.

Giá tiền (`{item.Price} {item.PriceCurrency}`) KHÔNG đổi — tên enum `CurrencyType`/`PriceCurrency`
không phải nội dung hiển thị cho người chơi theo đúng nghĩa "tên vật phẩm", để nguyên ngoài phạm vi.

## §3. Mail — thư chào mừng

Thư "welcome" được construct ở `LocalPlayerRepository.CreateNew()` (`Game.Services`, KHÔNG được
tham chiếu `Game.Meta` — comment sẵn trong file giải thích lý do Star/Level hero phía trên cũng
không gọi thẳng `HeroLevelSystem`). Thay vì bake string đã dịch vào `profile.Mail` lúc tạo profile
(sẽ đóng băng ngôn ngữ tại thời điểm tạo, không đổi được nếu người chơi đổi ngôn ngữ sau), thêm field
mới `MailDto.TitleKey` (string, mặc định `""` — JsonUtility tự điền cho save cũ, không cần
migration) chỉ lưu KEY, resolve thật ở `MailScreen.Refresh()` lúc render — cùng mẫu `ResultKey`/
`LabelKey` ở §1/§2.

`Body` KHÔNG có `BodyKey` — xác nhận qua grep dự án, `MailDto.Body` không được hiển thị ở đâu (dead
field theo UI hiện tại), không thêm hạ tầng cho thứ chưa dùng.

## §4. Verify

- `validate_script` cả 6 file sửa (NodeChoiceSystem/NodeChoiceScreen/ShopScreen/MailScreen/
  PlayerProfileDto/LocalPlayerRepository) 0 lỗi. Compile toàn project 0 lỗi (`refresh_unity` +
  `read_console`). **647/647 test xanh** (không đổi hành vi combat lõi/test hiện có — không test nào
  construct trực tiếp các struct/DTO đổi signature).
- Functional thật: dựng `new LocalizationService()` (constructor tự `Load()` từ
  `Resources/Localization/strings.csv`), tra 10 key `shop.item.*`, `mail.welcome.title`, vài key
  `nodechoice.*` — xác nhận trả đúng bản dịch VI lẫn EN (qua `SetLanguage("en")`), KHÔNG rơi về key
  thô. Xác nhận `Get(key, args)` format đúng số động (VD `"Bad luck... -25 gold."`).
- CSV parser: `mail.welcome.title` là dòng ĐẦU TIÊN trong `strings.csv` có dấu phẩy bên trong giá
  trị (`"Chào mừng, Chỉ huy!"`) — phải quote đúng chuẩn CSV. Đã đọc code parser
  (`LocalizationService.ParseLines`) xác nhận có xử lý field trong ngoặc kép thật (không phải
  `Split(',')` ngây thơ) TRƯỚC khi viết dòng này, rồi verify lại bằng lượt tra key thật ở trên — trả
  đúng "Chào mừng, Chỉ huy!" nguyên vẹn, không bị cắt ở dấu phẩy.

## §5. Ngoài phạm vi

- Giá tiền Shop (`CurrencyType` enum tên) — không phải nội dung "tên vật phẩm" theo yêu cầu ban đầu.
- `MailDto.Body` — trường chết theo UI hiện tại, không thêm `BodyKey`.
- Mail nội dung KHÁC ngoài "welcome" — hiện dự án chỉ có 1 chỗ tạo `MailDto` (`CreateNew()`), không
  có hệ thống mail động khác cần localize.
- `NodeChoiceResult.rest_train`'s hero name arg vẫn RAW `DefId`, không qua `GetName` — giới hạn có
  từ trước, xem §1.
