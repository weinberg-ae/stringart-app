# Take the user's black lines from her drawing (front view tile of cup_options4) and turn them into the cup's
# 2D outline and seam lines (cup coordinates), using the exact camera of that render.
import numpy as np, sys, json
sys.path.insert(0, '.')
from PIL import Image
from render import look_basis
im = np.array(Image.open('../drawpdf/hi-1.png').convert('RGB')).astype(int)
k = im.shape[1] / 1140.0                          # hi-res px per sheet px
r, g, b = im[..., 0], im[..., 1], im[..., 2]
blk = (r < 45) & (g < 45) & (b < 45) & (np.abs(r - g) < 12) & (np.abs(g - b) < 14)
ys, xs = np.nonzero(blk[: int(340 * k), : int(380 * k)])
sx, sy = xs / k, ys / k                            # sheet px in tile 1
right, up, fwd = look_basis(15, -8)
scale = 340 / 0.2
c = np.array([0, -0.01, 0.02])
def unproject(px, py, z):
    a = (px - 190) / scale; bb = (170 - py) / scale
    # q = (x, y, z) - c ; a = q.right ; bb = q.up -> solve for x, y with z given
    M = np.array([[right[0], right[1]], [up[0], up[1]]])
    rhs = np.stack([a - right[2] * (z - c[2]), bb - up[2] * (z - c[2])], 0) + (M @ c[:2])[:, None]
    sol = np.linalg.solve(M, rhs)
    return sol[0], sol[1]
# classify strokes
def line_y(x): return 445 / k + (395 - 445) / (900 - 175) * (x * k - 175) / k * 1.0
seam_h = np.abs(sy - (445 + (395 - 445) * (sx * k - 175) / (900 - 175)) / k) < 14 / k
vert = (sx * k > 470) & (sx * k < 570) & (sy * k > 405)
arc = ~seam_h & ~vert & (sy * k < (445 + (395 - 445) * (sx * k - 175) / (900 - 175)) - 20)
def poly(mask, key, step):
    X, Y = sx[mask], sy[mask]
    if key == 'x':
        bins = np.arange(X.min(), X.max() + step, step)
        pts = [(X[(X >= a) & (X < a + step)].mean(), Y[(X >= a) & (X < a + step)].mean()) for a in bins if ((X >= a) & (X < a + step)).any()]
    else:
        bins = np.arange(Y.min(), Y.max() + step, step)
        pts = [(X[(Y >= a) & (Y < a + step)].mean(), Y[(Y >= a) & (Y < a + step)].mean()) for a in bins if ((Y >= a) & (Y < a + step)).any()]
    return np.array(pts)
# the arc: for the near-vertical right end use y-bins on that part
A = poly(arc & (sx * k < 840), 'x', 4)
Ar = poly(arc & (sx * k >= 840), 'y', 3)
Ar = Ar[np.argsort(Ar[:, 1])]
arc_px = np.concatenate([A, Ar])
H_px = poly(seam_h, 'x', 6)
V_px = poly(vert, 'y', 6)
out = {'arc_px': arc_px.tolist(), 'h_px': H_px.tolist(), 'v_px': V_px.tolist()}
json.dump(out, open('out/cup_lines_px.json', 'w'))
print(len(arc_px), len(H_px), len(V_px))
print('arc ends', arc_px[0], arc_px[-1], 'h', H_px[0], H_px[-1], 'v', V_px[0], V_px[-1])
