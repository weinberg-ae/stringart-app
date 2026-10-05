# Illustrated "from source to fiber" strips for every fiber (right -> left, Hebrew reading order).
import math, random, os
from PIL import Image, ImageDraw, ImageFilter

W, H = 2048, 1024
OUT = os.path.join(os.path.dirname(__file__), "out")
os.makedirs(OUT, exist_ok=True)
CELLS = [1700, 1024, 348]          # icon centers, right to left
CY = 512

def rgba(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)

def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c[:3]) + (c[3],)

def leaf_pts(cx, cy, length, width, ang, serr=0):
    pts = []
    ca, sa = math.cos(ang), math.sin(ang)
    n = 40
    side = []
    for i in range(n + 1):
        t = i / n
        w = math.sin(math.pi * t) ** 0.8 * width / 2
        if serr: w *= 1 + serr * (0.5 + 0.5 * math.sin(t * 60))
        side.append((t * length, w))
    for x, w in side: pts.append((x, w))
    for x, w in reversed(side): pts.append((x, -w))
    return [(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts]

def leaf(d, cx, cy, length, width, ang, col, serr=0):
    d.polygon(leaf_pts(cx, cy, length, width, ang, serr), fill=col, outline=shade(col, 0.6))
    ca, sa = math.cos(ang), math.sin(ang)
    d.line([(cx, cy), (cx + length * 0.92 * ca, cy + length * 0.92 * sa)], fill=shade(col, 0.55), width=4)

def circ(d, x, y, r, fill, outline=None, w=3):
    d.ellipse([x - r, y - r, x + r, y + r], fill=fill, outline=outline, width=w)

# ---------- icons ----------
def cotton_plant(d, cx, cy):
    d.line([(cx + 40, cy + 300), (cx + 10, cy + 120), (cx, cy + 10)], fill=rgba('6b4a2b'), width=16, joint='curve')
    d.line([(cx + 20, cy + 190), (cx + 150, cy + 140)], fill=rgba('6b4a2b'), width=10)
    for a in (-0.5, 0.0, 0.5):
        leaf(d, cx + 150, cy + 140, 120, 70, -0.4 + a, rgba('3f8f3a'))
    # bracts
    pts = []
    for i in range(10):
        r = 165 if i % 2 == 0 else 95
        a = i * math.pi / 5 + math.pi / 10
        pts.append((cx + math.cos(a) * r, cy + 20 + math.sin(a) * r * 0.8))
    d.polygon(pts, fill=rgba('5a3a1e'), outline=rgba('3a2410'))
    # fluffy lobes
    rnd = random.Random(3)
    for lx, ly in ((-70, -40), (60, -50), (-40, 50), (55, 40), (0, -5)):
        for k in range(14):
            x = cx + lx + rnd.uniform(-45, 45); y = cy + ly + rnd.uniform(-40, 40)
            r = rnd.uniform(28, 46)
            circ(d, x, y, r, rgba('f6f6f2'), rgba('d6d6d0'), 2)

def tuft(d, cx, cy, col='f4f4ee'):
    rnd = random.Random(7)
    for k in range(70):
        x0 = cx + rnd.uniform(-190, 190); y0 = cy + rnd.uniform(-110, 110)
        pts = []
        a = rnd.uniform(-0.4, 0.4); L = rnd.uniform(80, 200)
        for i in range(12):
            t = i / 11
            pts.append((x0 + math.cos(a) * L * t, y0 + math.sin(a) * L * t + math.sin(t * 9 + k) * 10))
        d.line(pts, fill=rgba(col, 230), width=5, joint='curve')

def spool(d, cx, cy, col):
    c = rgba(col)
    d.rectangle([cx - 130, cy - 150, cx + 130, cy + 150], fill=c)
    for i in range(-140, 150, 14):
        d.line([(cx - 130, cy + i), (cx + 130, cy + i + 6)], fill=shade(c, 0.75), width=4)
    d.rectangle([cx - 130, cy - 150, cx - 95, cy + 150], fill=shade(c, 1.15))
    wood = rgba('c8955a')
    for y in (cy - 175, cy + 175):
        d.rounded_rectangle([cx - 175, y - 25, cx + 175, y + 25], 18, fill=wood, outline=shade(wood, 0.6), width=4)
    d.line([(cx + 130, cy + 60), (cx + 210, cy + 120), (cx + 180, cy + 220)], fill=c, width=7, joint='curve')

def sheep(d, cx, cy):
    rnd = random.Random(11)
    for leg in (-110, -50, 60, 120):
        d.rounded_rectangle([cx + leg - 16, cy + 60, cx + leg + 16, cy + 230], 10, fill=rgba('2b2b30'))
    for k in range(60):
        a = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(0, 1) ** 0.5
        x = cx + 40 + math.cos(a) * r * 190; y = cy - 10 + math.sin(a) * r * 115
        circ(d, x, y, rnd.uniform(36, 52), rgba('f3eee2'), rgba('d8d0bd'), 3)
    d.ellipse([cx - 250, cy - 120, cx - 120, cy + 30], fill=rgba('2b2b30'))
    d.ellipse([cx - 160, cy - 140, cx - 90, cy - 90], fill=rgba('2b2b30'))
    circ(d, cx - 195, cy - 70, 10, rgba('ffffff'))
    for k in range(10):
        circ(d, cx - 140 + rnd.uniform(-30, 30), cy - 140 + rnd.uniform(-15, 15), 24, rgba('f3eee2'), rgba('d8d0bd'), 2)

def fleece(d, cx, cy):
    for k in range(9):
        y0 = cy - 120 + k * 30
        pts = [(cx - 200 + i * 10, y0 + math.sin(i * 0.9 + k) * 14) for i in range(41)]
        d.line(pts, fill=rgba('efe6cf'), width=9, joint='curve')

def yarn(d, cx, cy, col):
    c = rgba(col)
    circ(d, cx, cy, 165, c, shade(c, 0.6), 5)
    for k in range(9):
        a = k * 0.35
        box = [cx - 165 + k * 6, cy - 120 + k * 4, cx + 165 - k * 6, cy + 120 - k * 4]
        d.arc(box, 200 + k * 9, 340 + k * 9, fill=shade(c, 0.7), width=6)
    for k in range(6):
        d.arc([cx - 150, cy - 165 + k * 18, cx + 150, cy + 165 - k * 18], 20, 160, fill=shade(c, 0.8), width=5)
    d.line([(cx - 120, cy + 110), (cx - 220, cy + 190), (cx - 160, cy + 240)], fill=c, width=8, joint='curve')
    for x0, x1 in ((cx + 60, cx + 230), (cx + 110, cx + 200)):
        d.line([(x0, cy + 40), (x1, cy - 230)], fill=rgba('b0b6c0'), width=12)
        circ(d, x1, cy - 230, 16, rgba('e05a7a'))

def silkworm_leaf(d, cx, cy):
    leaf(d, cx + 170, cy + 150, 360, 260, math.pi + 0.35, rgba('4e9a3d'), serr=0.06)
    for i in range(10):
        x = cx + 120 - i * 26; y = cy - 10 + math.sin(i * 0.8) * 10
        circ(d, x, y, 30 - i * 0.6, rgba('efe9dc'), rgba('c8bfae'), 3)
    circ(d, cx + 150, cy - 14, 24, rgba('8a7f70'))

def cocoon(d, cx, cy):
    c = rgba('f2e3b3')
    d.ellipse([cx - 110, cy - 170, cx + 110, cy + 170], fill=c, outline=shade(c, 0.7), width=5)
    for k in range(14):
        d.arc([cx - 105 + k * 3, cy - 165 + k * 9, cx + 105 - k * 3, cy + 165 - k * 9], 190 + k * 13, 330 + k * 13, fill=shade(c, 0.82), width=3)
    d.line([(cx - 60, cy - 150), (cx - 160, cy - 230), (cx - 230, cy - 200)], fill=rgba('f7edc8'), width=4, joint='curve')

def oil_drop(d, cx, cy):
    pts = []
    for i in range(80):
        t = i / 79 * 2 * math.pi
        x = math.sin(t) * 120 * (1 - 0.0) 
        y = -math.cos(t) * 150
        if y < 0: x *= (1 + y / 150) ** 1.3 if y > -150 else 0
        pts.append((cx + x, cy + 40 + y - (0 if y >= 0 else 0)))
    top = [(cx, cy - 210)]
    shape = []
    for i in range(41):
        t = i / 40 * math.pi
        shape.append((cx + math.sin(t) * 140, cy + 60 - math.cos(t) * 140))
    poly = [(cx, cy - 230)] + [p for p in shape if p[1] >= cy - 30] 
    poly = [(cx, cy - 240)]
    for i in range(0, 181, 6):
        a = math.radians(i - 90)
        poly.append((cx + math.cos(a) * 140, cy + 70 + math.sin(a) * 140 + (0 if i > 90 else 0)))
    d.polygon([(cx, cy - 240)] + [(cx + math.cos(math.radians(a)) * 140, cy + 70 + math.sin(math.radians(a)) * 140) for a in range(-30, 211, 6)], fill=rgba('15161c'), outline=rgba('3a3f55'))
    d.ellipse([cx - 80, cy - 20, cx - 30, cy + 90], fill=rgba('5a6a8f', 200))

def bottle(d, cx, cy):
    g = rgba('9fd4ff', 150)
    d.rounded_rectangle([cx - 85, cy - 120, cx + 85, cy + 220], 40, fill=g, outline=rgba('d8f0ff'), width=5)
    d.polygon([(cx - 85, cy - 100), (cx - 40, cy - 190), (cx + 40, cy - 190), (cx + 85, cy - 100)], fill=g, outline=rgba('d8f0ff'))
    d.rectangle([cx - 35, cy - 230, cx + 35, cy - 190], fill=rgba('2f7de0'))
    d.rectangle([cx - 85, cy - 10, cx + 85, cy + 70], fill=rgba('2f7de0', 200))
    for y in (cy + 120, cy + 160): d.line([(cx - 80, y), (cx + 80, y)], fill=rgba('d8f0ff', 200), width=4)

def tree(d, cx, cy, crown='3f9a45', round_=False):
    d.polygon([(cx - 30, cy + 260), (cx + 30, cy + 260), (cx + 18, cy - 20), (cx - 18, cy - 20)], fill=rgba('6b4a2b'))
    rnd = random.Random(5)
    c = rgba(crown)
    rx, ry = (190, 170) if round_ else (150, 190)
    for k in range(40):
        a = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(0, 1) ** 0.6
        circ(d, cx + math.cos(a) * r * rx, cy - 110 + math.sin(a) * r * ry, rnd.uniform(45, 70), shade(c, rnd.uniform(0.8, 1.15)))

def eucalyptus(d, cx, cy):
    d.line([(cx + 200, cy - 220), (cx + 40, cy - 60), (cx - 160, cy + 200)], fill=rgba('8a5a3a'), width=12, joint='curve')
    for i, (x, y) in enumerate(((cx + 150, cy - 170), (cx + 90, cy - 110), (cx + 30, cy - 50), (cx - 40, cy + 30), (cx - 100, cy + 110))):
        leaf(d, x, y, 190, 50, 1.9 + (i % 2) * 0.8, rgba('7fb3a3'))
    for x, y in ((cx - 30, cy + 70), (cx - 10, cy + 95)):
        circ(d, x, y, 14, rgba('6e7f5a'))

def flask(d, cx, cy, liquid):
    glass = rgba('cfe8ff', 90)
    poly = [(cx - 40, cy - 220), (cx + 40, cy - 220), (cx + 40, cy - 90), (cx + 170, cy + 210), (cx - 170, cy + 210), (cx - 40, cy - 90)]
    d.polygon(poly, fill=glass, outline=rgba('e8f4ff'))
    lq = rgba(liquid, 210)
    d.polygon([(cx - 95, cy + 40), (cx + 95, cy + 40), (cx + 170, cy + 210), (cx - 170, cy + 210)], fill=lq)
    for x, y, r in ((cx - 40, cy + 120, 14), (cx + 30, cy + 90, 10), (cx + 70, cy + 160, 12), (cx - 90, cy + 175, 8), (cx, cy - 10, 9), (cx + 10, cy - 70, 7)):
        circ(d, x, y, r, None, rgba('ffffff', 220), 4)
    d.line(poly + [poly[0]], fill=rgba('e8f4ff'), width=6)

def recycle(d, cx, cy):
    c = rgba('5fe08a')
    for k in range(3):
        a0 = k * 120 + 10
        d.arc([cx - 170, cy - 170, cx + 170, cy + 170], a0, a0 + 95, fill=c, width=26)
        a = math.radians(a0 + 95)
        x, y = cx + math.cos(a) * 170, cy + math.sin(a) * 170
        t = a + math.pi / 2
        d.polygon([(x + math.cos(t) * 45, y + math.sin(t) * 45), (x + math.cos(t + 2.4) * 35, y + math.sin(t + 2.4) * 35), (x + math.cos(t - 2.4) * 35, y + math.sin(t - 2.4) * 35)], fill=c)

def drape(d, cx, cy, col, shiny):
    c = rgba(col)
    top = [(cx - 170, cy - 200), (cx + 170, cy - 200)]
    bottom = [(cx + 200 - i * 20, cy + 200 + math.sin(i * 1.1) * 25) for i in range(21)]
    d.polygon(top + bottom, fill=c, outline=shade(c, 0.6))
    for k in range(5):
        x = cx - 120 + k * 60
        d.line([(x, cy - 190), (x + (k - 2) * 15, cy + 190)], fill=shade(c, 0.78), width=8)
        if shiny: d.line([(x + 18, cy - 170), (x + 18 + (k - 2) * 12, cy + 120)], fill=rgba('ffffff', 150), width=6)
    d.rectangle([cx - 190, cy - 215, cx + 190, cy - 195], fill=rgba('9aa3b5'))

def stocking(d, cx, cy):
    c = rgba('d9a07a', 190)
    pts = [(cx - 50, cy - 230), (cx + 60, cy - 230), (cx + 55, cy + 60), (cx + 40, cy + 140), (cx - 120, cy + 200), (cx - 175, cy + 185), (cx - 165, cy + 140), (cx - 60, cy + 90)]
    d.polygon(pts, fill=c, outline=rgba('8a5a3f'))
    d.rectangle([cx - 52, cy - 235, cx + 62, cy - 195], fill=rgba('7a4a32'))
    for k in range(6):
        d.line([(cx - 40 + k * 16, cy - 190), (cx - 45 + k * 14, cy + 80)], fill=rgba('ffffff', 60), width=3)

def spring(d, cx, cy):
    pts = []
    for i in range(400):
        t = i / 399
        a = t * 2 * math.pi * 8
        pts.append((cx - 230 + 460 * t + math.cos(a) * 30, cy + math.sin(a) * 85))
    d.line(pts, fill=rgba('c9d3e0'), width=10, joint='curve')
    c = rgba('1af2ff')
    for s in (-1, 1):
        d.line([(cx, cy - 170), (cx + s * 220, cy - 170)], fill=c, width=12)
        d.polygon([(cx + s * 250, cy - 170), (cx + s * 210, cy - 200), (cx + s * 210, cy - 140)], fill=c)

def leggings(d, cx, cy):
    c = rgba('2a2d3a')
    d.polygon([(cx - 120, cy - 220), (cx + 120, cy - 220), (cx + 110, cy + 230), (cx + 30, cy + 230), (cx, cy - 40), (cx - 30, cy + 230), (cx - 110, cy + 230)], fill=c, outline=rgba('5c6380'))
    for x in (cx - 70, cx + 70):
        d.line([(x, cy - 120), (x + (8 if x > cx else -8), cy + 200)], fill=rgba('8a93b8', 140), width=6)
    d.rectangle([cx - 122, cy - 228, cx + 122, cy - 200], fill=rgba('e05a9a'))

def arrow(d, x, y):
    c = rgba('bff8ff')
    d.line([(x + 90, y), (x - 60, y)], fill=c, width=16)
    d.polygon([(x - 100, y), (x - 50, y - 38), (x - 50, y + 38)], fill=c)

def make(name, icons):
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for i, ic in enumerate(icons):
        ic(d, CELLS[i], CY)
    for i in range(len(icons) - 1):
        arrow(d, (CELLS[i] + CELLS[i + 1]) // 2, CY)
    # soft white halo so the drawing reads on the dark void
    alpha = img.split()[3]
    halo = Image.new('RGBA', (W, H), (190, 240, 255, 0))
    halo.putalpha(alpha.filter(ImageFilter.GaussianBlur(18)).point(lambda v: int(v * 0.45)))
    out = Image.alpha_composite(halo, img)
    out = out.resize((W // 2, H // 2), Image.LANCZOS)
    out.save(os.path.join(OUT, name + '.png'))

make('cotton', [cotton_plant, tuft, lambda d, x, y: spool(d, x, y, 'f2f2ee')])
make('wool', [sheep, fleece, lambda d, x, y: yarn(d, x, y, 'e9dcc0')])
make('silk', [silkworm_leaf, cocoon, lambda d, x, y: spool(d, x, y, 'f3d68a')])
make('polyester', [oil_drop, bottle, lambda d, x, y: spool(d, x, y, '3f86ff')])
make('viscose', [tree, lambda d, x, y: flask(d, x, y, 'f2e6c8'), lambda d, x, y: spool(d, x, y, 'ffd94a')])
make('modal', [lambda d, x, y: tree(d, x, y, '6cbf4f', True), lambda d, x, y: flask(d, x, y, 'f2e6c8'), lambda d, x, y: drape(d, x, y, 'e8d9f0', False)])
make('lyocell', [eucalyptus, recycle, lambda d, x, y: spool(d, x, y, 'a9e8d8')])
make('acetate', [tree, lambda d, x, y: flask(d, x, y, 'ff9a5a'), lambda d, x, y: drape(d, x, y, '7b5cd6', True)])
make('polyamide', [oil_drop, lambda d, x, y: flask(d, x, y, '8fd0ff'), stocking])
make('acrylic', [oil_drop, lambda d, x, y: flask(d, x, y, '8fd0ff'), lambda d, x, y: yarn(d, x, y, 'ff7aa8')])
make('elastane', [oil_drop, spring, leggings])
print('ok')
