# Tattoo textures for the back of the hand: white = skin, dark lines = ink. Center of the back of the hand ~ (0.46, 0.5).
import math, os
from PIL import Image, ImageDraw, ImageFilter
S = 512; OUT = os.path.join(os.path.dirname(__file__), 'out')
INK = (38, 42, 70)
def base():
    im = Image.new('RGB', (S * 2, S * 2), 'white'); return im, ImageDraw.Draw(im)
def P(u, v): return (u * S * 2, (1 - v) * S * 2)   # texture v goes up
def finish(im, name):
    im = im.filter(ImageFilter.GaussianBlur(1.2)).resize((S, S), Image.LANCZOS)
    im.save(os.path.join(OUT, 'tattoo_%s.png' % name))
cu, cv = 0.46, 0.50
# 1) needle and thread
im, d = base()
d.line([P(cu - 0.02, cv - 0.20), P(cu + 0.02, cv + 0.20)], fill=INK, width=14)
d.ellipse([*P(cu + 0.005, cv + 0.19), *P(cu + 0.035, cv + 0.15)], outline=INK, width=8)
pts = [P(cu + 0.02 + 0.14 * math.sin(t * 0.25) * math.cos(t * 0.11), cv + 0.17 - t * 0.012) for t in range(0, 30)]
d.line(pts, fill=INK, width=8, joint='curve')
for k in range(3):
    a = k * 2.1
    d.ellipse([*P(cu - 0.17 + 0.04 * k, cv - 0.02 + 0.03 * math.sin(a)), *P(cu - 0.12 + 0.04 * k, cv - 0.07 + 0.03 * math.sin(a))], outline=INK, width=6)
finish(im, 'needle')
# 2) flower (rose-like)
im, d = base()
for r in (0.04, 0.075, 0.11):
    for k in range(5):
        a0 = k * 72 + r * 400
        x0, y0 = P(cu - r, cv + r); x1, y1 = P(cu + r, cv - r)
        d.arc([x0, y0, x1, y1], a0, a0 + 60, fill=INK, width=9)
d.line([P(cu, cv - 0.11), P(cu - 0.03, cv - 0.26)], fill=INK, width=9)
for s in (-1, 1):
    pts = [P(cu - 0.015 + s * 0.06 * math.sin(t / 10 * math.pi), cv - 0.17 - t * 0.006) for t in range(11)]
    d.line(pts, fill=INK, width=7)
finish(im, 'flower')
# 3) geometric (mandala)
im, d = base()
for r in (0.05, 0.1, 0.16):
    x0, y0 = P(cu - r, cv + r); x1, y1 = P(cu + r, cv - r)
    d.ellipse([x0, y0, x1, y1], outline=INK, width=7)
for k in range(12):
    a = k * math.pi / 6
    d.line([P(cu + 0.05 * math.cos(a), cv + 0.05 * math.sin(a)), P(cu + 0.2 * math.cos(a), cv + 0.2 * math.sin(a))], fill=INK, width=6)
    d.ellipse([*P(cu + 0.2 * math.cos(a) - 0.012, cv + 0.2 * math.sin(a) + 0.012), *P(cu + 0.2 * math.cos(a) + 0.012, cv + 0.2 * math.sin(a) - 0.012)], fill=INK)
finish(im, 'geo')
print('ok')
