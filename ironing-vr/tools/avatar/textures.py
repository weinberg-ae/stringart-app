# Tileable fabric textures for the avatar clothes (512 px, repeat every 25 cm on the body).
import numpy as np, os
from PIL import Image, ImageFilter
OUT = os.path.join(os.path.dirname(__file__), 'out'); S = 512
y, x = np.mgrid[0:S, 0:S].astype(float)
rng = np.random.default_rng(3)
def noise(scale, amp):
    # tileable: sum of waves with whole-number frequencies across the texture
    f = max(1, S // scale); n = np.zeros((S, S))
    for k in range(24):
        fx, fy = rng.integers(-f, f + 1, 2); ph = rng.random() * 6.283
        n += np.sin(2 * np.pi * (fx * x + fy * y) / S + ph) / (1 + np.hypot(fx, fy) / f)
    n = (n - n.min()) / (n.max() - n.min())
    return (n - 0.5) * amp
def save(name, rgb):
    Image.fromarray(np.clip(rgb * 255, 0, 255).astype(np.uint8)).save(os.path.join(OUT, 'tex_%s.png' % name))
# denim: 3/1 twill diagonals, indigo warp with white weft speckles
tw = (np.sin((x + y) * 2 * np.pi / 8) * 0.5 + 0.5)
fine = (np.sin(x * 2 * np.pi / 4) * 0.5 + 0.5) * 0.3
weft = (rng.random((S, S)) > 0.82) * 0.35
base = np.stack([0.16, 0.27, 0.48]) [None, None, :]
d = base * (0.75 + 0.35 * tw[..., None] + fine[..., None] * 0.2) + weft[..., None] * 0.45 + noise(16, 0.08)[..., None]
save('denim', d)
# plaid flannel: red / black / white checks (period 128 px)
def band(c, w, period=128): return (np.abs(((c % period) - period / 2)) < w).astype(float)
red = np.array([0.62, 0.08, 0.1]); black = np.array([0.08, 0.08, 0.09]); white = np.array([0.9, 0.88, 0.85])
v = band(x, 30); h = band(y, 30)
mix = np.where((v * h)[..., None] > 0, black, np.where(((v + h) > 0)[..., None], black * 0.5 + red * 0.5, red))
thin = np.maximum(band(x + 64, 3), band(y + 64, 3))[..., None]
p = mix * (1 - thin) + white * thin
p = p * (0.9 + 0.1 * (np.sin((x - y) * 2 * np.pi / 8)[..., None] * 0.5 + 0.5)) + noise(32, 0.05)[..., None]
save('plaid', p)
# knit (jersey): rows of small V loops, neutral light gray (tinted in the game)
k = np.abs(np.sin(x * 2 * np.pi / 8 + np.abs(np.sin(y * 2 * np.pi / 16)) * 2.0))
kn = 0.78 + 0.18 * k + noise(8, 0.06)
save('knit', np.stack([kn, kn, kn], -1))
# chino: fine twill, neutral
c = 0.82 + 0.1 * (np.sin((x - 2 * y) * 2 * np.pi / 8) * 0.5 + 0.5) + noise(16, 0.05)
save('chino', np.stack([c, c, c], -1))
# canvas (apron): plain weave, coarse
cv = 0.78 + 0.1 * np.sin(x * 2 * np.pi / 8) * np.sin(y * 2 * np.pi / 8) + noise(24, 0.08) + 0.05 * (rng.random((S, S)) - 0.5)
save('canvas', np.stack([cv, cv, cv], -1))
# leather: soft grain
lg = 0.85 + noise(4, 0.12) + noise(32, 0.06)
lg = np.asarray(Image.fromarray(np.clip(lg * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))) / 255.0
save('leather', np.stack([lg, lg, lg], -1))
print('ok')
