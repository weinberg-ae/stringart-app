# Bakes the approved bra cup (cup_v7.png: drawn on the user's lines, cupexact.py) for the game:
#  - the cup lying on the cup pad as a 97x97 grid of points in the pad's own space (tools3d.py coordinates),
#  - its 256x256 texture (sheer tulle with the pad showing through, opaque tapes on the seams and edges),
#    alpha = 0 outside the cup.
# The grid is the cloth laid flat (distance and direction from the apex along the cloth), so the iron's
# footprint is round everywhere. Grid points outside the cup are tucked under its edge, into the pad.
# Output: ../pkg/Assets/Resources/PM_Garments/cup.bytes
import numpy as np, sys, struct
sys.path.insert(0, '.')
from padmap import place, normal

d = np.load('out/cup_exact_2d.npz')
OUT, HSEAM, VSEAM, APEX = d['OUT'], d['HSEAM'], d['VSEAM'], d['apex']
DEPTH = 0.05
N, RES = 96, 256
PADCOL = np.array([0.97, 0.80, 0.84])

def ray_len(A):
    P = OUT - APEX; Q = np.roll(P, -1, 0); e = Q - P
    res = np.zeros(len(A))
    for i, a in enumerate(A):
        dd = np.array([np.cos(a), np.sin(a)])
        den = dd[0] * (-e[:, 1]) - dd[1] * (-e[:, 0]); ok = np.abs(den) > 1e-12
        tt = np.where(ok, (P[:, 0] * (-e[:, 1]) - P[:, 1] * (-e[:, 0])) / np.where(ok, den, 1), -1)
        ss = np.where(ok, (dd[0] * P[:, 1] - dd[1] * P[:, 0]) / np.where(ok, den, 1), -1)
        m = ok & (tt > 0) & (ss >= -1e-9) & (ss <= 1 + 1e-9)
        res[i] = tt[m].min()
    return res

RR = np.linspace(0, 1, 400)
PROF = DEPTH * (1 - RR ** 2) ** 0.7
def cloth_to_cup(a, g):
    """direction a and cloth length g from the apex -> cup point (x, y), rr, inside flag, length to the edge"""
    L = ray_len(a)
    gt = np.cumsum(np.r_[0, np.hypot(np.diff(RR)[None, :] * L[:, None], np.diff(PROF)[None, :])], axis=1) if False else None
    rr = np.zeros(len(a)); gedge = np.zeros(len(a))
    for i in range(len(a)):
        tab = np.r_[0, np.cumsum(np.hypot(np.diff(RR) * L[i], np.diff(PROF)))]
        gedge[i] = tab[-1]
        rr[i] = np.interp(min(g[i], tab[-1]), tab, RR)
    x = APEX[0] + np.cos(a) * rr * L; y = APEX[1] + np.sin(a) * rr * L
    return x, y, rr, g <= gedge, gedge

def dist_poly(x, y, P):
    best = np.full(x.shape, 9.0)
    for a, b in zip(P[:-1], P[1:]):
        e = b - a; l2 = e @ e
        t = np.clip(((x - a[0]) * e[0] + (y - a[1]) * e[1]) / l2, 0, 1)
        best = np.minimum(best, np.hypot(x - a[0] - t * e[0], y - a[1] - t * e[1]))
    return best

def features(x, y, rr, gleft):
    dh = dist_poly(x, y, HSEAM); dv = dist_poly(x, y, VSEAM)
    hy = np.interp(x, HSEAM[:, 0], HSEAM[:, 1])
    vx = np.interp(-y, -VSEAM[:, 1], VSEAM[:, 0])
    upper = y > hy
    panel = np.where(upper, 0, np.where(x < vx, 1, 2))
    dseam = np.minimum(dh, np.where(upper, 9, dv))
    on_u = y < np.interp(x, OUT[:200, 0], OUT[:200, 1]) + 0.012
    tapes = (dseam < 0.0032) | ((gleft < 0.009) & on_u) | ((gleft < 0.006) & ~on_u)
    seam = np.exp(-(dseam / 0.0022) ** 2); ridge = np.exp(-((dseam - 0.0035) / 0.0018) ** 2)
    puff = 0.0025 * np.clip(dseam / 0.02, 0, 1) * np.sin(np.pi * rr)
    edge = np.clip((rr - 0.92) / 0.08, 0, 1)
    bump = puff - 0.0012 * seam + 0.0008 * ridge + 0.0015 * edge + 0.0008 * tapes
    return panel, tapes, bump

# the flat cloth: box around the cup laid flat (azimuthal: direction + length from the apex)
A0 = np.linspace(-np.pi, np.pi, 721)[:-1]
_, _, _, _, GE = cloth_to_cup(A0, np.full(len(A0), 9.0))
fx, fy = np.cos(A0) * GE, np.sin(A0) * GE
side = max(fx.max() - fx.min(), fy.max() - fy.min()) + 0.006
cxy = np.array([(fx.max() + fx.min()) / 2, (fy.max() + fy.min()) / 2])
lo = cxy - side / 2
print('flat cloth box %.3f m' % side)

def uv_to_polar(u, w):
    F = lo + np.stack([u, w], -1) * side
    return np.arctan2(F[:, 1], F[:, 0]), np.hypot(F[:, 0], F[:, 1])

# 1. grid points on the pad
u, w = np.meshgrid(np.linspace(0, 1, N + 1), np.linspace(0, 1, N + 1))     # row = w, column = u
a, g = uv_to_polar(u.ravel(), w.ravel())
x, y, rr, inside, gedge = cloth_to_cup(a, g)
beyond = np.maximum(g - gedge, 0)
g = np.minimum(g, gedge)
_, _, bump = features(x, y, rr, gedge - g)
P = place(a, g, (0.0, 0.0, 0.0))
nP = normal(P)
# grid points outside the cup: tucked under its edge into the pad (a hidden skirt), so no cell is flat/degenerate
P = P + nP * (0.003 + bump - np.minimum(beyond, 0.012))[:, None]
print('grid', P.shape, 'inside %.0f%%' % (100 * inside.mean()), 'y range', P[:, 1].min(), P[:, 1].max())

# 2. texture
tu, tw = np.meshgrid(np.arange(RES) / (RES - 1), np.arange(RES) / (RES - 1))
a, g = uv_to_polar(tu.ravel(), tw.ravel())
x, y, rr, inside, gedge = cloth_to_cup(a, g)
panel, tapes, _ = features(x, y, rr, np.maximum(gedge - g, 0))
X, Y = tu.ravel() * (RES - 1), tw.ravel() * (RES - 1)
ang = np.choose(panel, [0.0, 1.0, -1.0])                                    # the grain turns per panel
gx = X * np.cos(ang) - Y * np.sin(ang); gy = X * np.sin(ang) + Y * np.cos(ang)
net = np.where((np.floor(gx / 1.5) % 2 == 0) ^ (np.floor(gy / 1.5) % 2 == 0), 0.94, 1.03)
nude = np.array([0.95, 0.88, 0.80]); tape = np.array([0.93, 0.84, 0.74])
col = (nude * 0.58 + PADCOL * 0.42) * net[:, None]
tcol = tape * (0.97 + 0.05 * np.sin(gx * 0.7))[:, None]
col = np.where((tapes | ~inside)[:, None], tcol, col)
rgba = np.concatenate([np.clip(col * 255, 0, 255), (inside * 255)[:, None]], 1).astype(np.uint8)

out = '../pkg/Assets/Resources/PM_Garments/cup.bytes'
with open(out, 'wb') as f:
    f.write(struct.pack('<iiif', 0x31505543, N, RES, side))                # 'CUP1'
    f.write(P.astype('<f4').tobytes())
    f.write(rgba.tobytes())
np.save('out/cup_bake_P.npy', P)
from PIL import Image
Image.fromarray(rgba.reshape(RES, RES, 4)[::-1, :, :3]).save('out/cup_bake_tex.png')
print('saved', out)
