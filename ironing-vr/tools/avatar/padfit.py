# Find how the cup sits on the pad in her tile-6 drawing: apex position on the dome, turn, size.
import numpy as np, sys, itertools, time
sys.path.insert(0, '.')
from PIL import Image
from scipy.spatial import cKDTree
from padmap import place
from render import look_basis
d = np.load('out/cup_exact_2d.npz'); OUT, HS, VS, APEX = d['OUT'], d['HSEAM'], d['VSEAM'], d['apex']
DEPTH = 0.05
def ray_len(a):
    P = OUT - APEX; Q = np.roll(P, -1, 0); e = Q - P
    res = []
    for ang in np.atleast_1d(a):
        dd = np.array([np.cos(ang), np.sin(ang)])
        den = dd[0] * (-e[:, 1]) - dd[1] * (-e[:, 0]); ok = np.abs(den) > 1e-12
        tt = np.where(ok, (P[:, 0] * (-e[:, 1]) - P[:, 1] * (-e[:, 0])) / np.where(ok, den, 1), -1)
        ss = np.where(ok, (dd[0] * P[:, 1] - dd[1] * P[:, 0]) / np.where(ok, den, 1), -1)
        m = ok & (tt > 0) & (ss >= -1e-9) & (ss <= 1 + 1e-9); res.append(tt[m].min())
    return np.array(res)
def polar(pts):
    v = pts - APEX; a = np.arctan2(v[:, 1], v[:, 0]); r = np.linalg.norm(v, axis=1)
    Lr = ray_len(a); g = []
    for ri, li in zip(r, Lr):
        rr = np.linspace(0, min(ri / li, 1), 60)
        z = DEPTH * (1 - rr ** 2) ** 0.7
        g.append(np.sum(np.hypot(np.diff(rr) * li, np.diff(z))))
    return a, np.array(g)
def sub(P, n):
    dd = np.r_[0, np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))]; s = np.linspace(0, dd[-1], n)
    return np.stack([np.interp(s, dd, P[:, 0]), np.interp(s, dd, P[:, 1])], 1)
CURVES = np.concatenate([sub(np.vstack([OUT, OUT[:1]]), 70), sub(HS, 25), sub(VS[1:], 15)])
CA, CG = polar(CURVES)
np.savez('out/cup_curves_polar.npz', A=CA, G=CG, n=[70, 25, 15])
hi = np.array(Image.open('../drawpdf/hi-1.png').convert('RGB')).astype(int); k = hi.shape[1] / 1140.0
blk = (hi.max(2) < 60) & (np.ptp(hi, axis=2) < 16)
s6 = blk[int(340 * k):int(680 * k), int(760 * k):int(1140 * k)]
ys, xs = np.nonzero(s6); HER = np.stack([xs / k, ys / k], 1)[::7]
tree = cKDTree(HER)
right, up, fwd = look_basis(-60, 35); pc = np.array([-0.07, 0.26, 0]); sc = 340 / 0.23
def proj(P):
    q = P - pc; return np.stack([q @ right * sc + 190, 170 - q @ up * sc], 1)
def cost(prm, S, ret=False):
    P = place(CA, CG * S, prm)
    xy = proj(P)
    d1 = tree.query(xy)[0]; d2 = cKDTree(xy).query(HER)[0]
    c = np.mean(np.minimum(d1, 40)) + np.mean(np.minimum(d2, 40))
    return (c, xy) if ret else c
if __name__ == '__main__':
    t = time.time(); print(cost((0, 0, 0), 1.4), time.time() - t)
    best = []
    for ox, oz, psi, S in itertools.product([-0.04, -0.02, 0, 0.02, 0.04], [-0.04, -0.02, 0, 0.02, 0.04], np.radians(np.arange(-150, 180, 30)), [1.1, 1.4, 1.7]):
        best.append((cost((ox, oz, psi), S), ox, oz, psi, S))
    best.sort()
    for b in best[:8]: print(np.round(b, 3), np.degrees(b[3]))
    np.save('out/padfit_grid.npy', np.array(best))
