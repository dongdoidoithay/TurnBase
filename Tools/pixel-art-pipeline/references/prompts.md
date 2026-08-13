# Công thức prompt theo loại asset

Mỗi prompt = `[mô tả riêng] + [style theo category] + [negative chung]`.
Phần style và negative đã nằm sẵn trong `comfy_gen.py` — ở đây chỉ viết phần mô tả riêng.

## Nguyên tắc chung

| Luật | Lý do |
|---|---|
| Mô tả **hình khối trước, màu sau** | Model bám hình tốt hơn bám màu |
| Tối đa 12–15 từ cho phần riêng | Dài hơn thì model bắt đầu bỏ qua từ khoá |
| Nêu rõ **tư thế** | Không nêu thì mỗi lần sinh một kiểu, không ghép animation được |
| Không nêu tên game/nghệ sĩ có bản quyền | Rủi ro pháp lý |
| Một chủ thể một ảnh | "two knights" ra ảnh không dùng được |

## Nhân vật (`character`)

Cấu trúc: `[giáp/trang phục] + [vũ khí] + [màu chủ đạo] + [tư thế]`

```
armored fire knight, flaming greatsword, heavy shield, red orange armor, standing idle
hooded dark assassin, twin curved daggers, black cloak purple trim, crouching ready
ice mage, long crystal staff, layered blue robes, casting stance arms raised
holy priestess healer, golden staff, white gold robes, gentle open palm pose
wind rogue, short blade, green scarf, light leather armor, dashing forward
necromancer summoner, skull topped staff, tattered purple robe, arms outstretched
```

**Tư thế nên dùng:** `standing idle` · `combat ready stance` · `attacking mid-swing` · `casting stance`
**Tránh:** `dynamic pose`, `action shot` — ra ảnh cắt cụt hoặc góc lạ.

## Quái (`monster`)

```
small green goblin warrior, rusty club, angry expression
green goblin archer, short bow, leather quiver
blue slime creature, glossy round body, simple face
grey dire wolf, snarling, thick fur, side profile
purple giant bat, spread wings, glowing single eye
huge brown ogre, massive stone club, tusks
white bone skeleton warrior, rusty sword, small round shield
armored orc, huge tower shield, defensive stance
```

## Boss (`boss`)

Thêm từ khoá quy mô: `large`, `imposing`, `detailed`, `intimidating`.

```
giant alpha dire wolf, scarred face, wind aura, howling, imposing
obese goblin king, golden crown, jagged sceptre, stone throne, imposing
skeletal lich, floating, tattered robes, glowing green eyes, dark magic aura
massive lava dragon, molten cracks in scales, spread wings, roaring
```

## Background (`background`)

Sinh **3 lớp riêng** cho parallax, không sinh 1 ảnh rồi cắt:

```
lớp xa:   distant mountain range silhouette, hazy atmosphere, muted colors
lớp giữa: forest tree line, layered foliage, mid tones
lớp gần:  foreground grass and rocks, dark silhouette, high contrast
```

Luôn kèm: `no characters, seamless horizontal tiling`.

## VFX (`vfx`)

Sinh trên **nền đen tuyền** để blend cộng (additive) trong engine:

```
fire explosion burst, orange yellow core, radial
ice shatter shards, cyan white, radial burst
lightning strike, jagged white blue bolt, vertical
slash arc, white crescent trail, diagonal
heal sparkle, green white particles, rising
dark void implosion, purple black swirl
shield hexagon barrier, blue glow, circular
poison cloud, green bubbles, drifting
```

## Tile (`tile`)

```
grass texture, top down, short blades, varied green
stone floor, top down, cracked slabs, grey
dirt path, top down, small pebbles, brown
swamp water, top down, murky green, ripples
lava rock, top down, glowing cracks, dark red
```

Luôn kèm: `seamless tileable texture, repeating pattern, no border`.
Sau khi sinh, **bắt buộc** chạy `compose.py tileset --make-seamless`.

## Prop / vật phẩm (`prop`)

```
red health potion bottle, cork stopper, glass shine
steel longsword, leather grip, simple crossguard
wooden treasure chest, iron bands, closed
gold coin stack, shiny
```

## Xử lý khi kết quả không đạt

| Vấn đề | Chỉnh gì |
|---|---|
| Ra ảnh 3D/realistic | Tăng CFG lên 9–10; thêm `flat colors, no shading gradient` |
| Nhân vật cụt chân/đầu | Thêm `full body visible, head to feet, margin around subject` |
| Nhiều nhân vật trong 1 ảnh | Thêm negative `multiple characters, crowd, duplicate` |
| Pixel không sắc | Checkpoint không chuyên pixel — đổi checkpoint hoặc thêm LoRA pixel-art |
| Nền không phẳng | Thêm `plain flat background, studio backdrop`; giảm steps xuống 24 |
| Tư thế mỗi lần một khác | Cố định seed và chỉ đổi phần mô tả tư thế |

## Giữ nhất quán giữa các asset

1. **Cùng seed cho cùng nhóm** — 6 hero dùng seed cách nhau đúng 7919, không random.
2. **Cùng cụm từ mô tả ánh sáng** — ví dụ mọi hero đều có `lit from upper left`.
3. **Cùng chiều cao đầu ra** — hero 48px thì toàn bộ hero 48px, quái 32–40px, boss 64–96px.
4. **Khoá palette ở bước hậu xử lý** — đây là thứ quan trọng nhất để mọi thứ trông cùng một game.
