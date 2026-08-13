# Palette

Palette là thứ khiến toàn bộ asset trông **cùng một game**, kể cả khi sinh từ nhiều lần chạy khác nhau. Khoá palette ở bước hậu xử lý là bắt buộc.

## Cách dùng

Tạo `palette.json` rồi truyền vào `post_process.py --palette palette.json`:

```json
{
  "name": "Ten palette",
  "colors": [
    "#2B1B2E", "#3A2233", "#5A4A5E", "#F2E8CF",
    "#F4A259", "#E63946", "#457B9D", "#7BC950"
  ]
}
```

Kiểm tra sau khi xử lý:

```bash
python3 post_process.py --verify-palette clean/ --palette palette.json
```

## Palette mặc định 48 màu (tông tối ấm, hợp RPG pixel)

Dùng khi dự án chưa có palette riêng. Chia theo vai trò, mỗi nhóm 4 sắc độ tối→sáng.

**Nền / khung (8)**
`#1A0F1C` `#2B1B2E` `#3A2233` `#4A2F42` `#5A4A5E` `#736377` `#9A8B9E` `#C4B8C7`

**Trung tính / chữ (4)**
`#0D080E` `#5C5C5C` `#9A9A9A` `#F2E8CF`

**Cam — viền UI, Ultimate (4)**
`#7A3D14` `#B85C1E` `#F4A259` `#FFD9A0`

**Đỏ — HP, Fire (4)**
`#5C1220` `#A32335` `#E63946` `#FF8A94`

**Xanh dương — SP, Water (4)**
`#12304A` `#2A5A80` `#457B9D` `#8FC0D9`

**Xanh lá — hồi phục, Wind (4)**
`#1B3D1F` `#3D7A2E` `#7BC950` `#B9E89A`

**Vàng — Poise, Light, tiền (4)**
`#6B5210` `#B8901E` `#FFD166` `#FFF0B8`

**Tím — Dark, Epic (4)**
`#2A1B3A` `#5A3080` `#9B5DE5` `#CBA5F0`

**Nâu — Earth, gỗ, da (4)**
`#3A2416` `#6B4526` `#A67142` `#D4A574`

**Lam nhạt — băng, Freeze (4)**
`#1B3A42` `#2E6B78` `#4EC3D9` `#A5E8F0`

**Da người (4)**
`#6B4530` `#A6714F` `#D9A277` `#F2CDA7`

## Màu theo vai trò game

Cố định những màu này, đừng đổi giữa chừng — người chơi học theo màu.

| Vai trò | Màu | Ghi chú |
|---|---|---|
| Nền panel | `#2B1B2E` | |
| Viền panel | `#F4A259` | |
| Chữ chính | `#F2E8CF` | |
| Chữ mờ | `#9A8B9E` | |
| HP | `#E63946` | |
| SP / MP | `#457B9D` | |
| Poise / Break | `#FFD166` | |
| EXP | `#7BC950` | |
| Ultimate | `#FFD166` viền nhấp nháy | |
| Damage thường | `#F2E8CF` | |
| Damage crit | `#F4A259` | to hơn 1.6× |
| Hồi máu | `#7BC950` | |
| Miss / Resist | `#9A8B9E` | |

## Màu độ hiếm

| Hiếm | Viền | Nền |
|---|---|---|
| Common | `#9A9A9A` | `#2E2E33` |
| Rare | `#4EA8DE` | `#1B2A3A` |
| Epic | `#9B5DE5` | `#2A1B3A` |
| Legendary | `#F4A259` | `#3A2A1B` |
| Mythic | `#E63946` | `#3A1B22` |

## Màu nguyên tố

| Hệ | Màu | Hình dạng icon (cho người mù màu) |
|---|---|---|
| Fire | `#E63946` | giọt lửa nhọn |
| Water | `#457B9D` | giọt nước tròn |
| Earth | `#A67142` | khối vuông |
| Wind | `#7BC950` | xoáy cong |
| Light | `#FFD166` | tia sao |
| Dark | `#9B5DE5` | trăng khuyết |

> **Quan trọng:** icon nguyên tố phải phân biệt được bằng **hình dạng**, không chỉ màu. Khoảng 8% nam giới bị mù màu đỏ-lục.

## Lấy palette từ ảnh có sẵn

Nếu dự án đã có concept art:

```python
from PIL import Image
img = Image.open("concept.png").convert("RGB")
pal = img.quantize(colors=48, method=Image.MEDIANCUT).getpalette()[:48*3]
colors = ["#%02X%02X%02X" % tuple(pal[i:i+3]) for i in range(0, 144, 3)]
```
