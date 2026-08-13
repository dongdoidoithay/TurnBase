---
name: pixel-art-pipeline
description: Sinh và lắp ráp asset pixel-art 2D cho game. Dùng ComfyUI để sinh phần "vẽ tay" (nhân vật, quái, boss, background, hiệu ứng VFX), rồi dùng Python + Pillow để lắp ráp phần "kỹ thuật" (HUD, button, frame 9-slice, health bar, minimap, tileset, sprite sheet, icon, atlas). Kích hoạt khi người dùng yêu cầu tạo/sinh/vẽ asset game, sprite, tileset, spritesheet, icon, HUD, UI khung, thanh máu, hoặc nhắc tới ComfyUI cho art game. Cũng dùng khi cần hậu xử lý ảnh AI thành pixel-art đúng chuẩn (hạ pixel, khoá palette, tách nền, cắt frame).
---

# Pixel Art Pipeline

## Nguyên tắc cốt lõi

**Chia đôi công việc theo đúng thế mạnh:**

| Phần | Công cụ | Vì sao |
|---|---|---|
| Nội dung hữu cơ, khó vẽ tay: nhân vật, quái, boss, background, VFX | **ComfyUI** | Cần sáng tạo hình khối, giải phẫu, ánh sáng |
| Nội dung hình học, cần chính xác pixel: HUD, button, frame, health bar, minimap, tileset, spritesheet, icon | **Python + Pillow** | Cần đối xứng tuyệt đối, 9-slice co giãn đúng, kích thước bội số 2, lặp tile liền mạch |

> **Sai lầm thường gặp:** bảo AI vẽ khung UI hoặc thanh máu. Kết quả luôn méo, không 9-slice được, không co giãn được. Những thứ đó **phải** dựng bằng code.

## Luồng chuẩn

```
1. SINH (ComfyUI)          scripts/comfy_gen.py
      ↓  ảnh 512–1024px, nền phẳng
2. HẬU XỬ LÝ (Pillow)      scripts/post_process.py
      ↓  tách nền · hạ về pixel thật · khoá palette · trim · cắt frame
3. LẮP RÁP (Pillow)        scripts/compose.py
      ↓  frame 9-slice · button · health bar · icon có khung · HUD · tileset · atlas
4. IMPORT (game engine)    scripts/unity_import.py (nếu là Unity)
```

## Bước 0 — Chuẩn bị (làm một lần)

Kiểm tra ComfyUI đã chạy chưa:

```bash
curl -s http://127.0.0.1:8188/system_stats | head -c 200
```

Nếu chưa, khởi động (đường dẫn tuỳ máy, thường là `~/AI/ComfyUI`):

```bash
cd ~/AI/ComfyUI && nohup ./venv/bin/python main.py --listen 127.0.0.1 --port 8188 > /tmp/comfy.log 2>&1 &
```

Liệt kê checkpoint có sẵn:

```bash
ls ~/AI/ComfyUI/models/checkpoints/
```

Chọn checkpoint chuyên pixel-art nếu có (tên chứa `pixel`, `sprite`). Nếu không có, dùng SD1.5/SDXL thường rồi ép pixel ở bước hậu xử lý — chất lượng thấp hơn rõ rệt, nên báo cho người dùng biết.

**Chốt palette trước khi sinh bất cứ thứ gì.** Đọc `references/palette.md`. Nếu dự án đã có palette, ghi ra file `palette.json` và dùng xuyên suốt — đây là thứ khiến toàn bộ asset trông cùng một game.

## Bước 1 — Sinh bằng ComfyUI

```bash
python3 scripts/comfy_gen.py --catalog catalog.json --out raw/
```

`catalog.json` mô tả cần sinh gì (xem `references/catalog_example.json`). Mỗi mục:

```json
{
  "id": "hero_ember_knight",
  "category": "character",
  "prompt": "armored fire knight, flaming sword, heavy shield, red orange armor",
  "size": [768, 768],
  "seed": 20260807,
  "variants": 4
}
```

**Luật viết prompt** (chi tiết ở `references/prompts.md`):

- Luôn có: `pixel art, 16-bit sprite, crisp hard pixels, clean dark outline, limited palette`
- Luôn có: `flat solid magenta background` — nền magenta `#FF00FF` tách sạch hơn nền xám nhiều
- Luôn có negative: `blurry, antialiased, smooth gradient, 3d render, photo, text, watermark, drop shadow`
- Nhân vật: thêm `full body, centered, side view, T-pose neutral` (dễ cắt và ghép animation)
- Background: thêm `parallax layer, no characters, seamless horizontal tiling`
- VFX: thêm `on pure black background, additive glow, no character` (để blend cộng)

**Sinh nhiều variant rồi chọn.** Đặt `variants: 4`, xem tất cả, giữ cái tốt nhất. Đừng chấp nhận kết quả đầu tiên.

**BẮT BUỘC xem lại ảnh trước khi đi tiếp.** Dùng công cụ Read trên vài file đại diện. Nếu chất lượng không đạt (giải phẫu sai, mờ, không ra pixel, nền lẫn màu chủ thể) thì:
1. Chỉnh prompt hoặc đổi seed, sinh lại — đừng cố cứu bằng hậu xử lý
2. Xoá ảnh hỏng ngay, không để lẫn vào thư mục sạch
3. Báo người dùng nếu sau 2–3 lần vẫn không đạt — có thể cần checkpoint/LoRA khác

## Bước 2 — Hậu xử lý bằng Pillow

```bash
python3 scripts/post_process.py --in raw/ --out clean/ \
    --target-height 48 --palette palette.json --key magenta
```

Thứ tự xử lý (đúng thứ tự này, đảo là hỏng):

1. **Tách nền** — key theo màu magenta với ngưỡng, hoặc flood-fill từ 4 góc
2. **Trim** — cắt sát biên alpha, bỏ khoảng trống thừa
3. **Hạ pixel** — resize `Image.NEAREST` về chiều cao mục tiêu (32/48/64). Không bao giờ dùng LANCZOS/BICUBIC cho pixel-art
4. **Khoá palette** — quantize về palette dự án, dùng dithering `NONE`
5. **Làm sạch alpha** — alpha nhị phân (0 hoặc 255), xoá pixel mồ côi
6. **Canvas chuẩn** — đặt vào canvas bội số 2 (48×48, 64×64), căn đáy giữa để nhân vật đứng đúng mặt đất

Xem lại kết quả bằng Read. Ảnh sau bước này phải sắc nét từng pixel, không viền mờ, không màu lạ ngoài palette.

## Bước 3 — Lắp ráp bằng Pillow

`scripts/compose.py` có sẵn các generator. Mỗi cái nhận tham số kích thước/màu và **vẽ bằng code**, không dùng AI:

| Lệnh | Sinh ra |
|---|---|
| `frame` | Khung 9-slice (panel, tooltip, dialog) với góc/cạnh/giữa tách đúng |
| `button` | Nút 4 trạng thái: normal / hover / pressed / disabled |
| `healthbar` | Thanh máu: khung + fill + phần trăm + biến thể HP/MP/EXP/Poise |
| `icon-frame` | Bọc icon vào khung theo độ hiếm (viền màu + nền) |
| `hud` | Ghép HUD hoàn chỉnh từ các mảnh + preview layout |
| `minimap` | Minimap từ dữ liệu node/tile |
| `tileset` | Tileset 47-blob hoặc 16-tile từ vài tile gốc, đảm bảo lặp liền mạch |
| `spritesheet` | Gom frame rời thành sheet + xuất metadata JSON |
| `atlas` | Đóng gói nhiều sprite vào atlas + JSON toạ độ |

Ví dụ:

```bash
python3 scripts/compose.py frame --w 64 --h 64 --border 6 \
    --color "#F4A259" --fill "#2B1B2E" --out ui/frame_panel.png

python3 scripts/compose.py healthbar --w 96 --h 10 \
    --fill "#E63946" --bg "#3A2233" --border "#F4A259" --out ui/bar_hp.png

python3 scripts/compose.py spritesheet --in clean/hero_ember_knight/ \
    --cols 8 --out sheets/hero_ember_knight.png --meta
```

**9-slice:** luôn xuất kèm file `.9.json` ghi biên `{left, right, top, bottom}` để engine biết cắt ở đâu. Thiếu file này thì khung sẽ méo khi co giãn.

## Bước 4 — Import vào engine

Với Unity: `scripts/unity_import.py` sinh sẵn file `.meta` với đúng thiết lập pixel-art:

- `filterMode: 0` (Point)
- `textureCompression: 0` (None)
- `spritePixelsToUnits`: khớp PPU dự án
- `spriteMeshType: 0` (Full Rect) — bắt buộc cho 9-slice
- `spriteBorder` lấy từ file `.9.json`

Sau khi copy file vào `Assets/`, gọi refresh asset database của engine.

## Danh sách kiểm tra chất lượng

Trước khi coi asset là xong:

- [ ] Mọi màu nằm trong palette dự án (chạy `post_process.py --verify-palette`)
- [ ] Alpha nhị phân, không có pixel bán trong suốt ở viền
- [ ] Kích thước là bội số 2, nhân vật căn đáy giữa nhất quán
- [ ] Sprite cùng loại có cùng chiều cao (hero 48px thì tất cả hero đều 48px)
- [ ] Tileset lặp liền mạch — ghép 3×3 cùng một tile không thấy đường nối
- [ ] Frame 9-slice co giãn đúng ở 3 kích thước khác nhau
- [ ] Đã xem bằng mắt (Read) ít nhất 1 ảnh mỗi loại

## Lỗi hay gặp

| Triệu chứng | Nguyên nhân | Cách sửa |
|---|---|---|
| Viền sprite mờ, có màu lạ | Resize bằng LANCZOS thay vì NEAREST | Sinh lại từ raw với `--resample nearest` |
| Nền tách không sạch, còn rìa | Chủ thể có màu gần màu nền | Đổi sang nền magenta, sinh lại |
| Tile có đường nối khi lặp | Tile không seamless | Dùng `compose.py tileset --make-seamless` (mirror + blend biên) |
| Khung UI méo khi co giãn | Thiếu hoặc sai biên 9-slice | Kiểm tra file `.9.json`, biên phải nhỏ hơn nửa kích thước |
| Nhân vật "trôi" so với mặt đất | Trim rồi căn giữa theo tâm | Căn theo **đáy** giữa, không theo tâm |
| Ảnh AI ra 3D/realistic | Prompt thiếu từ khoá pixel, hoặc CFG quá thấp | Tăng CFG lên 7–9, thêm negative `3d render, photo` |

## Ghi chú về bản quyền

Ảnh sinh bằng AI dựa trên checkpoint cộng đồng: kiểm tra giấy phép của checkpoint trước khi dùng thương mại. Không đưa ảnh tham khảo có bản quyền của người khác vào prompt hoặc img2img.
