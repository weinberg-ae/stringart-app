# Tileable light beech wood texture.
import numpy as np
from PIL import Image
N = 512
y, x = np.mgrid[0:N, 0:N] / N
rng = np.random.default_rng(3)
def per_noise(k):
    acc = np.zeros((N, N))
    for i in range(k):
        fx, fy = rng.integers(1, 4), rng.integers(1, 6)
        acc += np.sin(2 * np.pi * (fx * x + fy * y) + rng.uniform(0, 6)) / (i + 1)
    return acc
warp = per_noise(6) * 0.05
rings = np.sin(2 * np.pi * (y * 11 + warp * 1.6 + 0.03 * np.sin(2 * np.pi * x * 2))) ** 3
fine = np.sin(2 * np.pi * (y * 70 + warp * 4)) * 0.5 + 0.5
pores = rng.random((N, N)) < 0.004
v = 0.5 + 0.18 * rings + 0.10 * (fine - 0.5)
base = np.array([0.93, 0.80, 0.66]); dark = np.array([0.76, 0.58, 0.44])
col = base[None, None] * v[..., None] + dark[None, None] * (1 - v[..., None])
col[pores] *= 0.85
Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).save('tex_wood.png')
