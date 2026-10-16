# Bra cup built exactly on the user's drawn lines (front view of cup_options4, tile 1):
#  - the lower U is the old cup's underwire edge (she kept it),
#  - the top edge is her arc (inner top -> high peak at the strap side -> down the outer side),
#  - the horizontal seam and the vertical seam are her two lines; they meet at the apex.
# Her lines are unprojected with the exact camera of that render (z taken from the cup surface, iterated).
import numpy as np, sys, json
sys.path.insert(0, '.')
from PIL import Image, ImageDraw
from tools3d import make, CUP_C, CUP_R
from toon import parts_for_render
from render import render, sheet, look_basis

L = json.load(open('out/cup_lines_px.json'))
ARC = np.array(L['arc_px']); HS = np.array(L['h_px']); VS = np.array(L['v_px'])

right, up, fwd = look_basis(15, -8)
SC = 340 / 0.2
CAM_C = np.array([0, -0.01, 0.02])
def unproject(px, py, z):
    a = (np.asarray(px) - 190) / SC; b = (170 - np.asarray(py)) / SC
    M = np.array([[right[0], right[1]], [up[0], up[1]]])
    z = np.broadcast_to(z, np.shape(a)).astype(float)
    rhs = np.stack([a - right[2] * (z - CAM_C[2]), b - up[2] * (z - CAM_C[2])], 0) + (M @ CAM_C[:2]).reshape(2, *([1] * np.ndim(a)))
    sol = np.linalg.solve(M, rhs.reshape(2, -1)).reshape(2, *np.shape(a))
    return sol[0], sol[1]

W0 = 0.078                      # the old cup (cup_options4) the drawing was made on
def old_u(k=200):
    t = np.linspace(0, np.pi, k)
    ux = -np.cos(t) * W0
    uy = 0.36 * W0 - np.sin(t) ** 0.8 * W0
    uy[ux > 0] += 0.10 * W0 * (ux[ux > 0] / W0) ** 2
    return np.stack([ux, uy], 1)

def resample(P, n):
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))]
    s = np.linspace(0, d[-1], n)
    return np.stack([np.interp(s, d, P[:, 0]), np.interp(s, d, P[:, 1])], 1)

def smooth(P, it=3):
    P = P.copy()
    for _ in range(it): P[1:-1] = 0.25 * P[:-2] + 0.5 * P[1:-1] + 0.25 * P[2:]
    return P

RIM_Z = 0.0015
# her arc, unprojected at the rim depth, ends snapped onto the U's top corners
ax, ay = unproject(ARC[:, 0], ARC[:, 1], RIM_Z)
arc = smooth(resample(np.stack([ax, ay], 1), 120), 4)
U = old_u()
for end, corner in ((0, U[0]), (-1, U[-1])):
    delta = corner - arc[end]
    w = np.exp(-np.linspace(0, 1, len(arc)) / 0.15) if end == 0 else np.exp(-np.linspace(1, 0, len(arc)) / 0.15)
    arc += w[:, None] * delta
OUT = np.concatenate([U, arc[::-1][1:-1]])       # counter-clockwise: U inner->outer, then her arc outer->inner
print('arc corners fix', np.round(U[0] - np.stack([ax, ay], 1)[0], 4), np.round(U[-1] - np.stack([ax, ay], 1)[-1], 4))

DEPTH = 0.05
def ray_len_all(A, apex):
    P = OUT - apex; Q = np.roll(P, -1, 0); e = Q - P
    res = np.zeros(len(A))
    for i, a in enumerate(A):
        d = np.array([np.cos(a), np.sin(a)])
        den = d[0] * (-e[:, 1]) - d[1] * (-e[:, 0])
        ok = np.abs(den) > 1e-12
        tt = np.where(ok, (P[:, 0] * (-e[:, 1]) - P[:, 1] * (-e[:, 0])) / np.where(ok, den, 1), -1)
        ss = np.where(ok, (d[0] * P[:, 1] - d[1] * P[:, 0]) / np.where(ok, den, 1), -1)
        m = ok & (tt > 0) & (ss >= 0) & (ss <= 1)
        res[i] = tt[m].min()
    return res

def depth_at(pts, apex):
    v = pts - apex
    a = np.arctan2(v[:, 1], v[:, 0]); r = np.linalg.norm(v, axis=1)
    rr = np.clip(r / ray_len_all(a, apex), 0, 1)
    return DEPTH * (1 - rr ** 2) ** 0.7

# apex = where her two seams meet (vertical seam's top), z = full depth; then the seams on the surface
apex = np.array(unproject(VS[0, 0], VS[0, 1], DEPTH)).ravel()
hz = np.zeros(len(HS)); vz = np.zeros(len(VS))
for it in range(12):
    hx, hy = unproject(HS[:, 0], HS[:, 1], hz); vx, vy = unproject(VS[:, 0], VS[:, 1], vz)
    hp = np.stack([hx, hy], 1); vp = np.stack([vx, vy], 1)
    hz = depth_at(hp, apex); vz = depth_at(vp, apex)
# extend each seam to the edge (her strokes stop just at the edge) and through the apex
def to_edge(P, apex, n):
    P = smooth(resample(P, n), 3)
    for end in (0, -1):
        d = P[end] - (P[end + 1] if end == 0 else P[end - 1]); d /= np.linalg.norm(d)
        q = P[end] - apex; a = np.arctan2(q[1], q[0])
        # step on until outside the outline
        ext = [P[end]]
        for s in range(1, 40):
            p = P[end] + d * 0.001 * s
            v = p - apex
            if np.linalg.norm(v) > ray_len_all([np.arctan2(v[1], v[0])], apex)[0] + 0.002: break
            ext.append(p)
        ext = np.array(ext).reshape(-1, 2)
        P = np.concatenate([ext[::-1][:-1], P]) if end == 0 else np.concatenate([P, ext[1:]])
    return P
HSEAM = to_edge(hp, apex, 60)
# the vertical seam starts at the horizontal seam (apex) and runs down to the wire
VSEAM = to_edge(vp, apex, 30)
VSEAM = VSEAM[np.argsort(-VSEAM[:, 1])]
VSEAM[0] = apex
np.savez('out/cup_exact_2d.npz', OUT=OUT, HSEAM=HSEAM, VSEAM=VSEAM, apex=apex)
print('apex', apex, 'outline x', OUT[:, 0].min(), OUT[:, 0].max(), 'y', OUT[:, 1].min(), OUT[:, 1].max())
