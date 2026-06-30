"""Draw a 32x32 xianxia sect-emblem pixel icon with PIL (code-native pixel art).

Layers (outer -> inner): gold rim, cyan ring with cloud speckles, violet disc
with inner glow, a glowing crescent moon, scattered star sparks. Transparent bg.
"""
from PIL import Image
import math

W = H = 32
cx = cy = 16.0  # pixel-center coordinate system; x in [0.5, 31.5]

# palette (cool xianxia: cyan / violet / white, gold accent)
T        = (0, 0, 0, 0)
GOLD     = (245, 196, 84, 255)
GOLD_D   = (168, 110, 30, 255)
CYAN     = (56, 189, 248, 255)
CYAN_D   = (3, 105, 161, 255)
CYAN_L   = (186, 230, 253, 255)
PURPLE   = (109, 40, 217, 255)
PURPLE_L = (167, 139, 250, 255)
MOON     = (247, 251, 255, 255)
GLOW     = (103, 232, 249, 255)
STAR     = (253, 230, 138, 255)

def dist(ax, ay, bx, by):
    return math.hypot(ax - bx, ay - by)

# bright cloud speckles riding on the cyan ring (r ~ 12.6)
clouds = set()
for ang in (25, 70, 110, 200, 250, 335):
    a = math.radians(ang)
    for d in (12.2, 12.6):
        gx = round(cx + d * math.cos(a))
        gy = round(cy - d * math.sin(a))
        if 0 <= gx < W and 0 <= gy < H:
            clouds.add((gx, gy))

# star sparks inside the violet disc, away from the moon (lower-right area)
stars = {(22, 9), (23, 10), (9, 22), (10, 23), (24, 18)}

img = Image.new("RGBA", (W, H), T)
px = img.load()

for y in range(H):
    for x in range(W):
        sx, sy = x + 0.5, y + 0.5
        d = dist(sx, sy, cx, cy)
        c = T

        # gold rim
        if 14.9 < d <= 15.4:
            c = GOLD_D
        elif 13.7 < d <= 14.9:
            c = GOLD
        # cyan ring
        elif 11.3 < d <= 13.7:
            c = CYAN
            if 11.3 < d <= 11.7 or 13.3 < d <= 13.7:
                c = CYAN_D
        # violet disc
        elif d <= 11.3:
            c = PURPLE
            if 9.2 < d <= 11.0:
                c = PURPLE_L

        # cloud highlights on the ring
        if (x, y) in clouds and c == CYAN:
            c = CYAN_L

        # crescent moon: bright disc minus an offset shadow disc -> opens upper-right
        dl = dist(sx, sy, 14.3, 16.7)
        ds = dist(sx, sy, 16.7, 14.0)
        if dl <= 5.2 and ds > 4.9:
            c = MOON
        # soft glow just outside the moon (only over the disc)
        if 5.2 < dl <= 6.4 and d < 10.6 and ds > 5.0:
            c = GLOW

        # star sparks
        if (x, y) in stars and d <= 10.6:
            c = STAR

        px[x, y] = c

img.save("generated_images/sect-emblem-32.png")

# nearest-neighbor upscale for preview (keeps crisp pixels)
img.resize((256, 256), Image.NEAREST).save("generated_images/sect-emblem-32-preview.png")
print("saved 32x32 + 256 preview")
