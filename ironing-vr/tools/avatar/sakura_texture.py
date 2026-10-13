# Tileable sakura pattern for the kimono (indigo background, pink blossoms, dark branches).
import math, random
from PIL import Image, ImageDraw, ImageFilter
N = 1024; OUT = 512
random.seed(4)
im = Image.new('RGB', (N, N), (28, 36, 84))
d = ImageDraw.Draw(im)
def wrap(fn):
    for ox in (-N, 0, N):
        for oy in (-N, 0, N): fn(ox, oy)
# soft lighter waves in the background
for k in range(6):
    y0 = random.uniform(0, N); amp = random.uniform(20, 60); ph = random.uniform(0, 6)
    pts = [(x, y0 + amp * math.sin(x / N * 2 * math.pi * 2 + ph)) for x in range(0, N + 1, 8)]
    wrap(lambda ox, oy: d.line([(x + ox, y + oy) for x, y in pts], fill=(38, 50, 108), width=14))
# branches
branches = []
for b in range(3):
    x, y = random.uniform(0, N), random.uniform(0, N); ang = random.uniform(-0.6, 0.6)
    pts = [(x, y)]
    for i in range(40):
        ang += random.uniform(-0.25, 0.25); x += math.cos(ang) * 22; y += math.sin(ang) * 22 * 0.6
        pts.append((x, y))
    branches.append(pts)
    for j, (px, py) in enumerate(pts[:-1]):
        w = max(3, 12 - j // 4)
        qx, qy = pts[j + 1]
        wrap(lambda ox, oy: d.line([(px + ox, py + oy), (qx + ox, qy + oy)], fill=(58, 34, 30), width=w))
def blossom(cx, cy, r, rot, col):
    def draw(ox, oy):
        for k in range(5):
            a = rot + k * 2 * math.pi / 5
            px, py = cx + ox + math.cos(a) * r * 0.55, cy + oy + math.sin(a) * r * 0.55
            d.ellipse([px - r * 0.5, py - r * 0.5, px + r * 0.5, py + r * 0.5], fill=col)
            nx, ny = cx + ox + math.cos(a) * r * 0.95, cy + oy + math.sin(a) * r * 0.95   # notch
            d.ellipse([nx - r * 0.09, ny - r * 0.09, nx + r * 0.09, ny + r * 0.09], fill=(28, 36, 84))
        d.ellipse([cx + ox - r * 0.22, cy + oy - r * 0.22, cx + ox + r * 0.22, cy + oy + r * 0.22], fill=(250, 228, 236))
        for k in range(5):
            a = rot + 0.6 + k * 2 * math.pi / 5
            sx, sy = cx + ox + math.cos(a) * r * 0.3, cy + oy + math.sin(a) * r * 0.3
            d.ellipse([sx - r * 0.07, sy - r * 0.07, sx + r * 0.07, sy + r * 0.07], fill=(246, 190, 70))
    wrap(draw)
pinks = [(248, 183, 205), (244, 160, 190), (252, 214, 226), (236, 134, 170)]
for pts in branches:
    for (px, py) in pts[2::3]:
        for _ in range(2):
            blossom(px + random.uniform(-40, 40), py + random.uniform(-40, 40), random.uniform(22, 36), random.uniform(0, 6), random.choice(pinks))
for _ in range(26):   # single blossoms and petals in the wind
    blossom(random.uniform(0, N), random.uniform(0, N), random.uniform(14, 24), random.uniform(0, 6), random.choice(pinks))
for _ in range(60):
    x, y = random.uniform(0, N), random.uniform(0, N); a = random.uniform(0, 6)
    wrap(lambda ox, oy: d.ellipse([x + ox - 7, y + oy - 4, x + ox + 7, y + oy + 4], fill=random.choice(pinks)))
im = im.filter(ImageFilter.GaussianBlur(0.8)).resize((OUT, OUT), Image.LANCZOS)
im.save('tex_sakura.png')
t = Image.new('RGB', (OUT * 2, OUT * 2)); [t.paste(im, (x, y)) for x in (0, OUT) for y in (0, OUT)]; t.resize((512, 512)).save('tile_check.png')
