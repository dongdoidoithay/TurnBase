# Hướng Dẫn Sử Dụng & Nâng Cấp Pixel Character Generator

Tài liệu này hướng dẫn bạn cách sử dụng, tùy chỉnh và nâng cấp file script `pixel_character_generator_v2.py` để tạo ra những nhân vật Pixel Art mượt mà, nhiều hiệu ứng và đẹp mắt hơn.

---

## 1. Cách Chạy Script Cơ Bản

1. Mở Terminal (hoặc Command Prompt).
2. Di chuyển đến thư mục chứa code:
   ```bash
   cd /Users/hainx/__Data/__Unity/__2D/Art_python
   ```
3. Chạy lệnh:
   ```bash
   python3 pixel_character_generator_v2.py
   ```
4. Code sẽ tự động render các Sprite Sheet (ví dụ: `Smooth_Walk_x4.png`, `Mage_Awakening_x4.png`) ngay trong cùng thư mục.

---

## 2. Hướng Dẫn Chỉnh Sửa Hình Dáng (Thiết Kế Nhân Vật)

Các bộ phận của nhân vật (Đầu, Thân, Tay, Chân) được vẽ bằng **Mảng Ký Tự (Grid Array)**. Bạn có thể coi đây là một bảng tính, mỗi ký tự là 1 pixel.

### Thay đổi Hình dáng
Mở code và tìm đến các hàm như `get_head()`, `get_body()`.
Ví dụ, để đổi kiểu tóc cho nhân vật, bạn chỉnh sửa mảng `t` trong `get_head()`:
```python
# '0' = Trong suốt, '1' = Viền đen, '3' = Màu tóc/nón, '2' = Da
t = [
    "001111100",
    "013333310", # Thêm tóc nhô cao lên
    "133333331",
    "133311331",
    "133122131",
    "131222210",
]
```
Bạn chỉ cần gõ thay đổi các số này, hình dáng nhân vật sẽ lập tức thay đổi. **Lưu ý:** Nếu làm mảng to ra (rộng hơn/cao hơn), bạn nhớ cập nhật lại tham số điểm neo `(pivot_x, pivot_y)` ở cuối hàm.

### Thêm Màu Sắc Mới (Palette)
Tìm đến biến `self.colors` trong hàm `__init__`. Bạn có thể thêm bất kỳ màu RGBA nào:
```python
self.colors = {
    # ... các màu cũ ...
    "9": (0, 255, 0, 255),       # Xanh lá cây (Áo giáp mới)
    "S": (255, 215, 0, 255),     # Vàng Gold (Kiếm ánh sáng)
    "T": (255, 0, 0, 150),       # Đỏ bán trong suốt (Hiệu ứng máu)
}
```
Sau đó dùng ký tự `"9"`, `"S"`, `"T"` vào trong mảng vẽ.

---

## 3. Nâng Cấp Hoạt Ảnh (Animation) Đẹp Hơn

Hệ thống hoạt ảnh hiện tại sử dụng **Hàm Sóng Sin (Sine Wave)** để tạo chuyển động nhịp nhàng.

### Cách chỉnh sửa dáng đi (Walk Cycle)
Trong hàm `generate_smooth_walk()`:
* **Chỉnh biên độ vung chân/tay:** Đổi số `45` hoặc `40` thành số lớn hơn để nhân vật sải bước dài hơn.
  ```python
  leg_r_angle = math.sin(t) * 60  # Vung hất chân cao lên 60 độ
  ```
* **Chỉnh độ nhấp nhô của cơ thể:**
  ```python
  # Thay đổi số * 2 cuối cùng để cơ thể nhảy cao hơn khi bước
  body_y = 20 + abs(math.sin(t * 2)) * 5 
  ```

### Viết một Hành Động (Action) Mới
Bạn có thể copy hàm `generate_smooth_walk`, đổi tên thành `generate_jump` và đặt các chỉ số cứng (Keyframe) thay vì dùng hàm Sin.
Ví dụ:
* Frame 0-2: `body_y = 25`, `leg_angle = -45` (Lấy đà)
* Frame 3-5: `body_y = 10`, `leg_angle = 10` (Vút lên không trung)

---

## 4. Cách Tối Ưu Hiệu Ứng VFX (Hạt/Aura/Sét)

Code đã tích hợp các hàm vẽ Hạt. Bạn có thể sáng tạo thêm VFX như sau:

### Vẽ Quả Cầu Lửa (Fireball)
Dùng vòng lặp lồng nhau vẽ các pixel màu Đỏ/Cam xung quanh tay vũ khí. Sử dụng `random` để hạt lửa chập chờn:
```python
# Gọi trong hàm assemble_frame
for i in range(20): # 20 hạt lửa
    px = hand_r_x + random.randint(-5, 5)
    py = hand_r_y + random.randint(-5, 5)
    self.draw_particle(frame, px, py, size=1, color_code="F")
```

### Đuôi sáng (Trail/Smear)
Khi nhân vật chém kiếm nhanh, bạn vẽ nhiều mũi kiếm (vũ khí) mờ dần ở các góc độ trước đó:
```python
# Giảm Alpha (độ đục) để tạo ảo ảnh
paste_part(frame, weap_img_mờ, pivot, arm_angle - 10, target_pos) 
paste_part(frame, weap_img_mờ, pivot, arm_angle - 20, target_pos)
```

### Làm Ánh sáng (Glow) mịn hơn
Trong hàm `draw_aura`, thay vì chỉ giảm Alpha, bạn có thể thay đổi kích thước hạt nhiễu (noise) hoặc vẽ nhiều vòng elip với mã màu chuyển dần từ Trắng (Lõi) -> Cyan -> Xanh Đậm (Viền ngoài).

---

## 5. Đưa Nhân Vật Vào Game (Unity)
Các file `_x4.png` (đã phóng to) chỉ dùng để preview cho đẹp.
Khi đưa vào Unity/Godot làm game thật:
1. Đặt `scale_factor = 1` ở cuối file Python để xuất ra ảnh gốc (nhỏ xíu nhưng sắc nét).
2. Ném file ảnh vào Unity.
3. Chọn file ảnh trong Unity, đổi **Filter Mode** từ `Bilinear` thành `Point (no filter)`.
4. Đổi **Compression** thành `None` để ảnh không bị nhòe.
5. Cắt (Slice) ảnh theo Grid width = 48 hoặc 64 tùy kích thước frame bạn đã khai báo trong Python.
