import math
import random
from PIL import Image

class SkeletalPixelGenerator:
    def __init__(self):
        self.frame_width = 64
        self.frame_height = 64
        
        self.colors = {
            "0": (0, 0, 0, 0),         # Trong suốt
            "1": (0, 0, 0, 255),       # Viền đen
            "2": (245, 206, 173, 255), # Màu da
            "3": (192, 192, 192, 255), # Bạc (Nón/Kiếm)
            "4": (50, 100, 200, 255),  # Xanh lam (Áo)
            "5": (120, 70, 40, 255),   # Nâu (Giày/Găng)
            "6": (128, 50, 160, 255),  # Tím (Mage)
            "7": (150, 100, 50, 255),  # Gậy gỗ
            "8": (100, 200, 255, 255), # Ngọc băng
            "F": (255, 100, 50, 255),  # Lửa / Dung nham (Đỏ cam)
            "Y": (255, 220, 50, 255),  # Vàng sáng (Sét/Hào quang)
            "C": (50, 255, 255, 255),  # Cyan (Phép thuật băng/nước)
            "A": (150, 50, 255, 255),  # Tím Aura (Phép thuật)
        }

    def render_part(self, template):
        h = len(template)
        w = len(template[0])
        img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        pixels = img.load()
        for y in range(h):
            for x in range(w):
                char = template[y][x]
                if char in self.colors:
                    pixels[x, y] = self.colors[char]
        return img

    def get_mage_head(self):
        t = [
            "000111000",
            "001666100",
            "016666610",
            "166666661",
            "166122161",
            "161222216",
            "112222221",
            "012222221",
            "001111110",
        ]
        return self.render_part(t), (4, 8)

    def get_mage_body(self):
        t = [
            "01111110",
            "16666661",
            "16666661",
            "16666661",
            "16666661",
            "11166111",
            "15511551",
            "01100110",
        ]
        return self.render_part(t), (4, 4)
        
    def get_arm(self):
        t = ["0110", "1661", "1661", "1221", "1551", "1551", "0110"]
        return self.render_part(t), (2, 1)

    def get_leg(self):
        t = ["01110", "16661", "16661", "15551", "15551", "15511", "01100"]
        return self.render_part(t), (2, 1)

    def get_staff(self):
        t = [
            "00100",
            "01810",
            "18881",
            "01810",
            "01710",
            "01710",
            "01710",
            "01710",
            "01710",
            "01710",
            "01710",
            "00100",
        ]
        return self.render_part(t), (2, 7)

    def rotate_image(self, img, angle, pivot):
        w, h = img.size
        pad_size = max(w, h) * 3
        canvas = Image.new("RGBA", (pad_size, pad_size), (0,0,0,0))
        center_x = pad_size // 2
        center_y = pad_size // 2
        paste_x = center_x - pivot[0]
        paste_y = center_y - pivot[1]
        canvas.paste(img, (paste_x, paste_y))
        rotated = canvas.rotate(angle, resample=Image.NEAREST)
        return rotated, (center_x, center_y)

    def draw_pixel_line(self, frame, x0, y0, x1, y1, color):
        """Vẽ đường thẳng pixel (Bresenham) cho tia sét"""
        pixels = frame.load()
        dx = abs(x1 - x0)
        dy = abs(y1 - y0)
        x, y = x0, y0
        sx = -1 if x0 > x1 else 1
        sy = -1 if y0 > y1 else 1
        if dx > dy:
            err = dx / 2.0
            while x != x1:
                if 0 <= x < self.frame_width and 0 <= y < self.frame_height:
                    pixels[x, y] = color
                err -= dy
                if err < 0:
                    y += sy
                    err += dx
                x += sx
        else:
            err = dy / 2.0
            while y != y1:
                if 0 <= x < self.frame_width and 0 <= y < self.frame_height:
                    pixels[x, y] = color
                err -= dx
                if err < 0:
                    x += sx
                    err += dy
                y += sy
        if 0 <= x < self.frame_width and 0 <= y < self.frame_height:
            pixels[x, y] = color

    def draw_lightning(self, frame, cx, cy, radius, frame_idx):
        """Vẽ tia sét zíc zắc ngẫu nhiên tỏa ra từ tâm"""
        random.seed(frame_idx * 10) # Seed theo frame để sét nháy đổi liên tục
        num_bolts = random.randint(2, 4)
        for _ in range(num_bolts):
            angle = random.uniform(0, 2 * math.pi)
            x, y = cx, cy
            for step in range(3):
                # Tia sét lệch ngẫu nhiên
                next_x = int(x + math.cos(angle) * (radius / 3) + random.randint(-4, 4))
                next_y = int(y + math.sin(angle) * (radius / 3) + random.randint(-4, 4))
                
                # Glow mờ (Cyan) - dày 3 pixel
                self.draw_pixel_line(frame, x, y+1, next_x, next_y+1, (50, 255, 255, 120))
                self.draw_pixel_line(frame, x, y-1, next_x, next_y-1, (50, 255, 255, 120))
                self.draw_pixel_line(frame, x+1, y, next_x+1, next_y, (50, 255, 255, 120))
                
                # Lõi sáng (Trắng)
                self.draw_pixel_line(frame, x, y, next_x, next_y, (255, 255, 255, 255))
                
                x, y = next_x, next_y
                angle += random.uniform(-0.5, 0.5)

    def draw_aura(self, frame, cx, cy, radius, intensity):
        """Vẽ nội lực (Aura) rực sáng bằng cách pha trộn (Alpha Compositing) các hình elip"""
        aura_layer = Image.new("RGBA", (self.frame_width, self.frame_height), (0,0,0,0))
        aura_pixels = aura_layer.load()
        
        # Vẽ vầng hào quang tím
        for dy in range(-radius, radius):
            for dx in range(-radius, radius):
                # Tính khoảng cách hình elip (dẹt theo trục Y)
                dist = math.sqrt(dx*dx + (dy*1.5)**2)
                if dist < radius:
                    # Càng xa tâm càng mờ
                    alpha = int(255 * (1 - dist/radius) * intensity)
                    if alpha > 0:
                        px, py = cx + dx, cy + dy
                        if 0 <= px < self.frame_width and 0 <= py < self.frame_height:
                            # Trộn màu (Blending) hạt mịn lấp lánh (dùng nhiễu)
                            noise = random.randint(-20, 20)
                            r = max(0, min(255, 150 + noise))
                            g = max(0, min(255, 50 + noise))
                            b = 255
                            aura_pixels[px, py] = (r, g, b, alpha)
                            
        # Dán layer aura lên trên background, dưới nhân vật
        frame.paste(aura_layer, (0, 0), aura_layer)
        return frame

    def generate_magic_awakening(self, scale_factor=4):
        """Tạo Animation tụ năng lượng (Aura + Sét điện)"""
        num_frames = 10
        spritesheet_w = self.frame_width * num_frames
        spritesheet = Image.new("RGBA", (spritesheet_w, self.frame_height), (0, 0, 0, 0))
        
        # Lấy modules
        head_img, head_pivot = self.get_mage_head()
        body_img, body_pivot = self.get_mage_body()
        arm_img, arm_pivot = self.get_arm()
        leg_img, leg_pivot = self.get_leg()
        staff_img, staff_pivot = self.get_staff()
        
        for i in range(num_frames):
            frame = Image.new("RGBA", (self.frame_width, self.frame_height), (0, 0, 0, 0))
            bx, by = self.frame_width//2, 35
            
            # --- 1. VẼ HIỆU ỨNG NỘI LỰC (AURA Ở LỚP DƯỚI CÙNG) ---
            intensity = 0.3 + 0.7 * (i / num_frames) # Aura sáng dần
            radius = 15 + i * 2 # Aura bành trướng dần
            # Cột năng lượng dâng lên
            y_shift = int(math.sin(i * 1.5) * 3)
            self.draw_aura(frame, bx, by + 5 + y_shift, int(radius), intensity)
            
            # --- 2. VẼ NHÂN VẬT ---
            # Cơ thể nổi lơ lửng trên không trung (Levitation)
            by -= int(i * 1.2) 
            
            def paste_part(canvas, img, pivot, angle, target_pos):
                rot_img, rot_center = self.rotate_image(img, angle, pivot)
                tx, ty = target_pos[0] - rot_center[0], target_pos[1] - rot_center[1]
                canvas.paste(rot_img, (tx, ty), rot_img)

            # Tay giang rộng niệm chú
            arm_angle = -45 - i * 5 
            leg_angle = 15
            
            # Render Z-Index
            paste_part(frame, arm_img, arm_pivot, arm_angle, (bx - 2, by - 4))
            paste_part(frame, leg_img, leg_pivot, -leg_angle, (bx - 2, by + 3))
            paste_part(frame, body_img, body_pivot, 0, (bx, by))
            paste_part(frame, leg_img, leg_pivot, leg_angle, (bx + 2, by + 3))
            paste_part(frame, head_img, head_pivot, 0, (bx, by - 6))
            
            # Tay phải cầm gậy giơ cao
            hand_r_x = (bx + 3) - int(4 * math.sin(math.radians(-arm_angle)))
            hand_r_y = (bx - 4) + int(4 * math.cos(math.radians(-arm_angle)))
            
            paste_part(frame, staff_img, staff_pivot, -arm_angle, (hand_r_x + 5, hand_r_y - 15))
            paste_part(frame, arm_img, arm_pivot, -arm_angle, (bx + 3, by - 4))
            
            # --- 3. VẼ TIA SÉT XUNG QUANH (LỚP TRÊN CÙNG) ---
            if i > 2:
                # Sét tập trung quanh người và đầu gậy
                self.draw_lightning(frame, bx, by, 15, i)
                # Sét trên đầu gậy phép
                self.draw_lightning(frame, hand_r_x + 5, hand_r_y - 22, 10, i + 100)

            spritesheet.paste(frame, (i * self.frame_width, 0))
            
        scaled = spritesheet.resize((spritesheet_w * scale_factor, self.frame_height * scale_factor), Image.NEAREST)
        return scaled

if __name__ == "__main__":
    gen = SkeletalPixelGenerator()
    magic_sheet = gen.generate_magic_awakening(scale_factor=4)
    magic_sheet.save("Mage_Awakening_x4.png")
    print("Đã tạo thành công Mage_Awakening_x4.png với hiệu ứng Aura và Sét điện!")
