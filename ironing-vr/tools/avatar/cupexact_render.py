# Preview of the cup built on the user's lines (cupexact.py -> out/cup_exact_2d.npz).
# Row 1: the same three views as her drawing, with her black lines on top in red (to check the match).
# Row 2: on the cup pad (creased / ironed / the view she drew on), with her lines there too.
import numpy as np, sys
sys.path.insert(0, '.')
from PIL import Image
from tools3d import make, CUP_C, CUP_R, chest_pad
from toon import parts_for_render
from render import render, sheet, look_basis
from padmap import place, normal

d = np.load('out/cup_exact_2d.npz')
OUT, HSEAM, VSEAM, APEX = d['OUT'], d['HSEAM'], d['VSEAM'], d['apex']
DEPTH = 0.05
S = float(sys.argv[1]) if len(sys.argv) > 1 else 1.6      # real size: the drawing's shape, scaled to cover the pad dome
Rx, Ry, Rz = CUP_R
CC = np.array(CUP_C)
PADSDF = chest_pad()[0]
m = make('chestpad')
PAD = parts_for_render(m, [(0.97, 0.80, 0.84), (0.86, 0.72, 0.58), (0.3, 0.3, 0.3)], {0: 0.15, 1: 0.35})
PADCOL = np.array([0.97, 0.80, 0.84])

def ray_len_all(A):
    P = OUT - APEX; Q = np.roll(P, -1, 0); e = Q - P
    res = np.zeros(len(A))
    for i, a in enumerate(A):
        dd = np.array([np.cos(a), np.sin(a)])
        den = dd[0] * (-e[:, 1]) - dd[1] * (-e[:, 0])
        ok = np.abs(den) > 1e-12
        tt = np.where(ok, (P[:, 0] * (-e[:, 1]) - P[:, 1] * (-e[:, 0])) / np.where(ok, den, 1), -1)
        ss = np.where(ok, (dd[0] * P[:, 1] - dd[1] * P[:, 0]) / np.where(ok, den, 1), -1)
        mm = ok & (tt > 0) & (ss >= 0) & (ss <= 1)
        res[i] = tt[mm].min()
    return res

def dist_poly(x, y, P):
    best = np.full(x.shape, 9.0)
    for a, b in zip(P[:-1], P[1:]):
        e = b - a; l2 = e @ e
        t = np.clip(((x - a[0]) * e[0] + (y - a[1]) * e[1]) / l2, 0, 1)
        best = np.minimum(best, np.hypot(x - a[0] - t * e[0], y - a[1] - t * e[1]))
    return best

def cup(onpad, wrinkled, n=170):
    A = np.linspace(-np.pi, np.pi, n + 1)
    L = ray_len_all(A)
    rho = np.linspace(0, 1, n + 1)
    AA, RR = np.meshgrid(A, rho)
    LL = np.tile(L, (n + 1, 1))
    x = APEX[0] + np.cos(AA) * RR * LL
    y = APEX[1] + np.sin(AA) * RR * LL
    # seams = her two lines; panels: above the horizontal seam / below it, left or right of the vertical seam
    dh = dist_poly(x, y, HSEAM); dv = dist_poly(x, y, VSEAM)
    hy = np.interp(x, HSEAM[:, 0], HSEAM[:, 1])
    vx = np.interp(-y, -VSEAM[:, 1], VSEAM[:, 0])
    upper = y > hy
    panel = np.where(upper, 0, np.where(x < vx, 1, 2))
    dv = np.where(upper, 9, dv)
    dseam = np.minimum(dh, dv)
    seam = np.exp(-(dseam / 0.0022) ** 2)
    ridge = np.exp(-((dseam - 0.0035) / 0.0018) ** 2)
    depth = DEPTH * (1 - RR ** 2.0) ** 0.7
    puff = 0.0025 * np.clip(dseam / 0.02, 0, 1) * np.sin(np.pi * RR)
    edge = np.clip((RR - 0.92) / 0.08, 0, 1)
    z = depth + puff - 0.0012 * seam + 0.0008 * ridge + 0.0015 * edge
    if wrinkled:
        r = np.random.default_rng(7); h = 0
        for k in range(6):
            a = r.uniform(0, np.pi); f = r.uniform(80, 160); p = r.uniform(0, 6)
            h = h + np.exp(-((np.sin((np.cos(a) * x + np.sin(a) * y) * f + p)) / 0.2) ** 2) / (1 + 0.3 * k)
        z = z + 0.0025 * h * np.sin(np.pi * RR)
    # tapes on the seams, the underwire channel along the U, binding along her top edge
    seg3 = np.sqrt(np.diff(x, axis=0) ** 2 + np.diff(y, axis=0) ** 2 + np.diff(depth, axis=0) ** 2)
    g3 = np.vstack([np.zeros((1, n + 1)), np.cumsum(seg3, axis=0)])
    edge_d = g3[-1:] - g3                                    # along the cloth to the edge
    on_u = (np.abs(np.angle(np.exp(1j * (AA + np.pi / 2)))) < 0) | (y < np.interp(x, OUT[:200, 0], OUT[:200, 1]) + 0.012)
    channel = (edge_d < 0.009) & on_u
    binding = (edge_d < 0.006) & ~on_u
    tapes = (dseam < 0.0032) | channel | binding
    z = z + 0.0008 * tapes
    if onpad:
        # laid over the pad like cloth: each line from the apex runs over the pad's surface with its own
        # length (padmap.place, a geodesic walk); then lifted by the fabric thickness + its relief
        seg = np.sqrt(np.diff(x * S, axis=0) ** 2 + np.diff(y * S, axis=0) ** 2 + np.diff(depth * S, axis=0) ** 2)
        g = np.vstack([np.zeros((1, n + 1)), np.cumsum(seg, axis=0)])
        flat = place(AA.reshape(-1), g.reshape(-1), (0.0, 0.0, 0.0))
        nrm = normal(flat)
        bump = (z - depth).reshape(-1) * S
        P = (flat + nrm * (0.003 + bump)[:, None]).reshape(x.shape + (3,))
    else:
        P = np.stack([x, y, z], -1) * S
    nude = np.array([0.95, 0.88, 0.80]); tape = np.array([0.93, 0.84, 0.74])
    ang = np.choose(panel, [0.0, 1.0, -1.0])
    gx = (x * np.cos(ang) - y * np.sin(ang)) * 900; gy = (x * np.sin(ang) + y * np.cos(ang)) * 900
    net = np.where((np.floor(gx * 2) % 2 == 0) ^ (np.floor(gy * 2) % 2 == 0), 0.94, 1.03)
    see = 0.42 if onpad else 0.25
    under = PADCOL if onpad else np.array([0.80, 0.78, 0.80])
    col = (nude * (1 - see) + under * see) * net[..., None]
    col = np.where(tapes[..., None], tape * (0.97 + 0.05 * np.sin(gx * 0.7))[..., None], col)
    vv = P.reshape(-1, 3).astype(np.float32); cc = col.reshape(-1, 3)
    F = []
    for a_ in range(n):
        for b in range(n):
            i = a_ * (n + 1) + b; F += [[i, i + n + 2, i + n + 1], [i, i + 1, i + n + 2]]
    F = np.array(F)
    nn = np.zeros_like(vv, dtype=float); fn = np.cross(vv[F[:, 1]] - vv[F[:, 0]], vv[F[:, 2]] - vv[F[:, 0]])
    for k in range(3): np.add.at(nn, F[:, k], fn)
    nn /= np.maximum(np.linalg.norm(nn, axis=1, keepdims=True), 1e-12)
    ref = CC if onpad else np.array([0, 0, -0.1])
    if np.mean(np.sum(nn * (vv - ref), 1)) < 0: F = F[:, [0, 2, 1]]; nn = -nn
    return [(vv, nn.astype(np.float32), F, cc, 0.35), (vv, (-nn).astype(np.float32), F[:, [0, 2, 1]], cc * 0.8, 0.2)]

# her drawing (black strokes) per tile of the 3x2 sheet, to lay over my renders
hi = np.array(Image.open('../drawpdf/hi-1.png').convert('RGB')).astype(int)
k = hi.shape[1] / 1140.0
blk = (hi.max(2) < 60) & (np.ptp(hi, axis=2) < 16)
def her(tile):
    c, r = tile % 3, tile // 3
    sub = blk[int(r * 340 * k): int((r + 1) * 340 * k), int(c * 380 * k): int((c + 1) * 380 * k)]
    ys, xs = np.nonzero(sub)
    return xs / k, ys / k
def overlay(im, tile):
    a = np.array(im)
    xs, ys = her(tile)
    xi = np.clip(xs.astype(int), 0, 379); yi = np.clip(ys.astype(int), 0, 339)
    a[yi, xi] = (220, 30, 40)
    return Image.fromarray(a)

bg = (232, 228, 236)
fc = tuple(np.array([0, -0.01, 0.02]) * S)
free = cup(False, False)
ims = [overlay(render(free, size=(380, 340), yaw=15, pitch=-8, center=fc, height=0.2 * S, bg=bg), 0),
       overlay(render(free, size=(380, 340), yaw=55, pitch=-5, center=fc, height=0.2 * S, bg=bg), 1),
       render(free, size=(380, 340), yaw=160, pitch=25, center=fc, height=0.2 * S, bg=bg)]
clean = [render(free, size=(380, 340), yaw=15, pitch=-8, center=fc, height=0.2 * S, bg=bg),
         render(free, size=(380, 340), yaw=55, pitch=-5, center=fc, height=0.2 * S, bg=bg)]
pc = (-0.07, 0.26, 0)
onw, on = cup(True, True), cup(True, False)
ims += [render(PAD + onw, size=(380, 340), yaw=25, pitch=45, center=pc, height=0.23, bg=bg),
        render(PAD + on, size=(380, 340), yaw=25, pitch=45, center=pc, height=0.23, bg=bg),
        overlay(render(PAD + on, size=(380, 340), yaw=-60, pitch=35, center=pc, height=0.23, bg=bg), 5)]
clean += [render(PAD + on, size=(380, 340), yaw=-60, pitch=35, center=pc, height=0.23, bg=bg)]
tag = sys.argv[2] if len(sys.argv) > 2 else 'cup_exact'
sheet(ims, 'out/%s_check.png' % tag, cols=3)
sheet(clean + ims[3:5], 'out/%s.png' % tag, cols=3)
