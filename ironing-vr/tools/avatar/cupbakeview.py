# What the game will show: the baked grid (bilinear, as PM_Game samples it) with the baked texture.
import numpy as np, sys, struct
sys.path.insert(0, '.')
from tools3d import make
from toon import parts_for_render
from render import render, sheet
b = open('../pkg/Assets/Resources/PM_Garments/cup.bytes', 'rb').read()
mg, N, RES, side = struct.unpack('<iiif', b[:16])
P = np.frombuffer(b[16:16 + (N + 1) ** 2 * 12], '<f4').reshape(N + 1, N + 1, 3)
tex = np.frombuffer(b[16 + (N + 1) ** 2 * 12:], np.uint8).reshape(RES, RES, 4)
M = 96
def samp(u, w):
    fu, fw = u * N, w * N
    i0 = np.clip(np.floor(fu).astype(int), 0, N - 1); j0 = np.clip(np.floor(fw).astype(int), 0, N - 1)
    tu, tw = (fu - i0)[..., None], (fw - j0)[..., None]
    return (P[j0, i0] * (1 - tu) * (1 - tw) + P[j0, i0 + 1] * tu * (1 - tw) + P[j0 + 1, i0] * (1 - tu) * tw + P[j0 + 1, i0 + 1] * tu * tw)
u, w = np.meshgrid(np.linspace(0, 1, M + 1), np.linspace(0, 1, M + 1))
V = samp(u, w).reshape(-1, 3)
C = tex[np.round(w * (RES - 1)).astype(int), np.round(u * (RES - 1)).astype(int), :3].reshape(-1, 3) / 255.0
F = []
for j in range(M):
    for i in range(M):
        k = j * (M + 1) + i; F += [[k, k + M + 1, k + 1], [k + 1, k + M + 1, k + M + 2]]
F = np.array(F)
nn = np.zeros_like(V); fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]]); fn = fn / np.maximum(np.linalg.norm(fn, axis=1, keepdims=True), 1e-15)
for q in range(3): np.add.at(nn, F[:, q], fn)
nn /= np.maximum(np.linalg.norm(nn, axis=1, keepdims=True), 1e-12)
if np.mean(nn[:, 1]) < 0: F = F[:, [0, 2, 1]]; nn = -nn
cup = [(V.astype(np.float32), nn.astype(np.float32), F, C, 0.35), (V.astype(np.float32), (-nn).astype(np.float32), F[:, [0, 2, 1]], C * 0.8, 0.2)]
m = make('chestpad'); PAD = parts_for_render(m, [(0.97, 0.80, 0.84), (0.86, 0.72, 0.58), (0.3, 0.3, 0.3)], {0: 0.15, 1: 0.35})
bg = (232, 228, 236); pc = (-0.07, 0.26, 0)
sheet([render(PAD + cup, size=(380, 340), yaw=25, pitch=45, center=pc, height=0.23, bg=bg),
       render(PAD + cup, size=(380, 340), yaw=-60, pitch=35, center=pc, height=0.23, bg=bg),
       render(PAD + cup, size=(380, 340), yaw=0, pitch=89, center=pc, height=0.26, bg=bg)], 'out/cup_bake_view.png', cols=3)
