# Floor-length champagne satin gown for the dress form (like the museum reference: high boat neck,
# flutter sleeves, a sash with a jewelled clasp, a bias-cut skirt that pools on the floor).
# Built in the form's normalized space (PM_Garment.Show: height 1.6, bottom y=0, centred on the bounds' x/z).
# Bodice = offset surface of the form itself (so it fits and never lets the form poke through);
# skirt and sleeves = open cloth surfaces with an inner side (real thin hems).
# Output gown.bytes: 'PMG1', vc, positions, normals, wrinkle weight, crumple value, submeshes
#   0 satin (steamable), 1 satin lining / sash (static), 2 jewels.
import numpy as np, re, os, struct, sys
from scipy.spatial import cKDTree
from scipy.ndimage import gaussian_filter, map_coordinates
from skimage.measure import marching_cubes

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out')
s = open(os.path.join(HERE, '..', 'pkg', 'Assets', 'Resources', 'PM_Garments', 'mannequin.dae')).read()
V = np.array(re.search(r'<float_array id="meshId0-positions-array" count="\d+">([^<]*)<', s).group(1).split(), float).reshape(-1, 3)
TRI = np.array(re.search(r'<p>([^<]*)</p>', s).group(1).split(), int).reshape(-1, 3)
mn, mx = V.min(0), V.max(0)
K = 1.6 / (mx[1] - mn[1])
FORM = (V - np.array([(mn[0] + mx[0]) / 2, mn[1], (mn[2] + mx[2]) / 2])) * K

# ---- signed distance to the form's torso (dense samples on the triangles, sign from the face normal)
def surface_samples(P, T, spacing=0.003):
    pts, nrm = [], []
    for a, b, c in T:
        A, B, C = P[a], P[b], P[c]
        n = np.cross(B - A, C - A); ar = np.linalg.norm(n)
        if ar < 1e-12: continue
        n /= ar
        m = max(2, int(np.ceil(max(np.linalg.norm(B - A), np.linalg.norm(C - A), np.linalg.norm(C - B)) / spacing)))
        i, j = np.meshgrid(np.arange(m + 1), np.arange(m + 1), indexing='ij'); keep = i + j <= m
        u = i[keep] / m; v = j[keep] / m
        pts.append(A + np.outer(u, B - A) + np.outer(v, C - A)); nrm.append(np.tile(n, (len(u), 1)))
    return np.concatenate(pts), np.concatenate(nrm)
torso_t = TRI[FORM[TRI].mean(1)[:, 1] > 0.70]
SP, SN = surface_samples(FORM, torso_t)
tree = cKDTree(SP)
def form_sdf(p):
    d, i = tree.query(p)
    sg = np.sign(np.sum((p - SP[i]) * SN[i], 1)); sg[sg == 0] = 1
    return d * sg

STEP = 0.006
LO = np.array([-0.30, 0.66, -0.16]); HI = np.array([0.30, 1.50, 0.32])
xs, ys, zs = [np.arange(LO[i], HI[i] + 1e-9, STEP) for i in range(3)]
GX, GY, GZ = np.meshgrid(xs, ys, zs, indexing='ij')
GP = np.stack([GX.ravel(), GY.ravel(), GZ.ravel()], 1)
F = form_sdf(GP).reshape(GX.shape)
G = gaussian_filter(F, 1.8)
E = G                             # smooth: bridged over the hollows and the facets of the low-poly form

def field(vol, p):
    q = (p - LO) / STEP
    return map_coordinates(vol, q.T, order=1, mode='nearest')

NECK_Y = 1.452
WAIST = None
# the waist: the narrowest band of the form between 0.95 and 1.15
def width_at(y):
    j = int(round((y - LO[1]) / STEP))
    sl = E[:, j, :] < 0
    xi = np.nonzero(sl.any(1))[0]
    return (xi.max() - xi.min()) * STEP if len(xi) else 9
yy = np.arange(0.95, 1.16, 0.006)
WAIST = float(yy[np.argmin([width_at(y) for y in yy])])
print('waist at', WAIST)

OFF = 0.014
def bodice_vol():
    B = E - OFF
    B = np.maximum(B, GY - NECK_Y)                   # high boat neckline
    B = np.maximum(B, 0.745 - GY)                    # ends inside the skirt
    return B

# cross-section radius of a field level set at height y along angle th (from the torso axis)
ZC = 0.058
def radius(vol, y, th, level=0.0, rmax=0.45):
    r = np.linspace(0.0, rmax, 400)
    p = np.stack([np.sin(th) * r, np.full_like(r, y), ZC + np.cos(th) * r], 1)
    f = field(vol, p) - level
    out = np.nonzero(f > 0)[0]
    inside = np.nonzero(f <= 0)[0]
    if len(inside) == 0: return 0.0
    last = inside.max()
    return r[min(last + 1, len(r) - 1)]

def mc(vol, lo, step):
    v, f, _, _ = marching_cubes(vol, 0.0, spacing=(step, step, step))
    v = v + lo
    return v, f

# ---------------- skirt (open cloth with an inner side)
NT, NY = 200, 120
Y_TOP, Y_HEM = 0.95, 0.012
def skirt_surface():
    th = np.linspace(0, 2 * np.pi, NT, endpoint=False)
    B = bodice_vol()
    def rb_at(y):
        rb = np.array([radius(B, y, tt) for tt in th[::5]])
        return np.interp(th, np.append(th[::5], 2 * np.pi), np.append(rb, rb[0]))
    r_top = rb_at(Y_TOP) - 0.004
    ys_ = np.linspace(Y_TOP, Y_HEM, NY)
    R = np.zeros((NY, NT)); RB = np.zeros((NY, NT))
    for i, y in enumerate(ys_):
        t = (Y_TOP - y) / (Y_TOP - Y_HEM)
        back = np.clip(-np.cos(th), 0, 1)                      # the back flows a little longer (small train)
        Rf = 0.30 + 0.035 * back ** 2
        Rz = Rf * 0.93
        ell = 1.0 / np.sqrt((np.sin(th) / Rf) ** 2 + (np.cos(th) / Rz) ** 2)
        flare = t ** 1.45
        pool = np.clip((t - 0.93) / 0.07, 0, 1) ** 2 * 0.035     # the hem spreads on the floor
        R[i] = r_top + (ell - r_top) * flare + pool
        if 0.70 < y < Y_TOP - 0.01: RB[i] = rb_at(y) + 0.006
    # never inside the bodice / the hips of the form; smooth along the height so there is no step
    from scipy.ndimage import gaussian_filter1d
    hip = int(np.argmax(RB.mean(1)))
    for _ in range(4):
        R = np.maximum(R, RB)
        for i in range(hip + 1, NY): R[i] = np.maximum(R[i], R[i - 1])   # below the hips the skirt only widens
        R = gaussian_filter1d(R, 6.0, axis=0, mode='nearest')
    R = np.maximum(R, RB)
    R[0] = r_top
    P = np.zeros((NY, NT, 3))
    for i, y in enumerate(ys_):
        t = (Y_TOP - y) / (Y_TOP - Y_HEM)
        # bias-cut folds: soft, deeper towards the floor, slowly wandering
        amp = 0.024 * np.clip((t - 0.12) / 0.88, 0, 1) ** 1.25
        folds = amp * (0.65 * np.sin(8 * th + 0.8 * np.sin(3 * y) + 0.3) + 0.35 * np.sin(13 * th + 1.7 * y + 1.1))
        r = R[i] + np.maximum(folds, -0.5 * amp) if y > 0.70 else R[i] + folds
        yy_ = y + (0.006 * np.sin(5 * th + 0.5) if t > 0.999 else 0)
        P[i, :, 0] = np.sin(th) * r; P[i, :, 1] = yy_; P[i, :, 2] = ZC + np.cos(th) * r
    return P

def grid_tris(nr, nc, off, wrap=True, flip=False):
    tri = []
    for i in range(nr - 1):
        for j in range(nc if wrap else nc - 1):
            a = off + i * nc + j; b = off + i * nc + (j + 1) % nc; c = a + nc; d = b + nc
            t1, t2 = (a, c, b), (b, c, d)
            if flip: t1, t2 = (a, b, c), (b, d, c)
            tri += [t1, t2]
    return tri

def normals(v, f):
    n = np.zeros_like(v)
    fn = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    for k in range(3): np.add.at(n, f[:, k], fn)
    return n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)

# ---------------- flutter sleeves
SH_Y = 1.400
def sleeve(sign):
    # attach at the form's arm cap
    a0 = np.array([sign * 0.150, SH_Y + 0.010, ZC + 0.005])
    d = np.array([sign * 0.62, -0.78, 0.04]); d /= np.linalg.norm(d)
    side = np.array([0, 0, 1.0]); side -= d * (side @ d); side /= np.linalg.norm(side)
    up = np.cross(side, d)
    nu, nl = 72, 26
    P = np.zeros((nl, nu, 3))
    for i in range(nl):
        t = i / (nl - 1)
        L = 0.03 + 0.17 * t
        c = a0 + d * L * 1.0
        ph = np.linspace(0, 2 * np.pi, nu, endpoint=False)
        r = 0.068 + 0.060 * t ** 1.4
        # flutter: soft waves that grow towards the hem; the sleeve opens more on the outer side
        r = r + 0.016 * t ** 1.5 * np.sin(6 * ph + 0.7) + 0.02 * t * np.clip(np.cos(ph), 0, 1)
        # the outer edge hangs lower than the inner one (cape-like)
        drop = 0.035 * t * (0.5 + 0.5 * np.cos(ph))
        for j in range(nu):
            P[i, j] = c + (up * np.cos(ph[j]) + side * np.sin(ph[j])) * r[j] - np.array([0, drop[j], 0])
    return P

def cloth(P, wrap_cols=True, thick=0.0035, hem_rows=('last',)):
    """outer + inner (offset inwards) + a strip joining them at the hem. Returns v, f_outer, f_inner"""
    nr, nc, _ = P.shape
    vo = P.reshape(-1, 3)
    fo = np.array(grid_tris(nr, nc, 0, wrap_cols))
    no = normals(vo, fo)
    # orient: normals point away from the cloth's axis (the centre of each row)
    cen = P.mean(1, keepdims=True).repeat(nc, 1).reshape(-1, 3)
    if np.mean(np.sum(no * (vo - cen), 1)) < 0:
        fo = fo[:, [0, 2, 1]]; no = -no
    vi = vo - no * thick
    fi = fo[:, [0, 2, 1]] + len(vo)
    v = np.concatenate([vo, vi])
    # hem strip (last row of outer to last row of inner)
    i = nr - 1; strip = []
    for j in range(nc):
        a = i * nc + j; b = i * nc + (j + 1) % nc; c = a + len(vo); dd = b + len(vo)
        strip += [(a, b, c), (b, dd, c)]
    strip = np.array(strip)
    # orient the strip outwards/down
    sn = np.cross(v[strip[:, 1]] - v[strip[:, 0]], v[strip[:, 2]] - v[strip[:, 0]])
    if np.mean(sn[:, 1]) > 0: strip = strip[:, [0, 2, 1]]
    return v, fo, np.concatenate([fi, strip])

def sphere_mesh(c, r, n=10):
    th = np.linspace(0, np.pi, n); ph = np.linspace(0, 2 * np.pi, 2 * n, endpoint=False)
    T, Pp = np.meshgrid(th, ph, indexing='ij')
    v = np.stack([np.sin(T) * np.cos(Pp), np.cos(T), np.sin(T) * np.sin(Pp)], -1).reshape(-1, 3) * r + c
    f = np.array(grid_tris(n, 2 * n, 0, True))
    nn = normals(v, f)
    if np.mean(np.sum(nn * (v - c), 1)) < 0: f = f[:, [0, 2, 1]]
    return v, f

def crumple(p, seed=3):
    """crease field in [-1, 1]: sharp ridges along random directions, warped (like crushed satin)"""
    rng = np.random.default_rng(seed)
    h = np.zeros(len(p))
    w = p + 0.03 * np.sin(p[:, [1, 2, 0]] * 23.0 + 1.3)
    for k in range(10):
        d = rng.normal(size=3); d /= np.linalg.norm(d)
        fr = rng.uniform(14, 34); ph = rng.uniform(0, 6.28)
        h += (1 - 2 * np.abs(np.sin(w @ d * fr + ph))) ** 3 / (1 + 0.25 * k)
    return h / np.abs(h).max()

def build():
    parts = []   # (v, f, sub, wrinkle weight per vertex)
    # bodice
    B = bodice_vol()
    v, f = mc(B, LO, STEP)
    # light smoothing of the marching cubes surface (keeps it on the shape)
    import scipy.sparse as sp
    e = np.concatenate([f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]]])
    A = sp.coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(len(v), len(v))).tocsr(); A = ((A + A.T) > 0).astype(float)
    M = sp.diags(1.0 / np.maximum(np.asarray(A.sum(1)).ravel(), 1)) @ A
    for _ in range(10): v = 0.5 * v + 0.5 * (M @ v)
    yv = v[:, 1]
    wb = np.clip((1.36 - yv) / 0.15, 0.25, 1.0) * 0.75          # the bodice creases less than the skirt
    parts.append((v, f, 0, wb))
    # sash around the waist (static) with a jewelled clasp in front
    sash = np.maximum(np.abs(E - OFF - 0.006) - 0.0055, np.abs(GY - WAIST) - 0.021)
    v2, f2 = mc(sash, LO, STEP)
    for _ in range(3): pass
    parts.append((v2, f2, 1, np.zeros(len(v2))))
    zf = radius(B, WAIST, 0.0) + ZC + 0.012
    rng = np.random.default_rng(7)
    for i, x in enumerate(np.linspace(-0.07, 0.07, 9)):
        for k in range(2):
            c = np.array([x + rng.uniform(-0.006, 0.006), WAIST + (k - 0.5) * 0.016 + rng.uniform(-0.003, 0.003), 0])
            th = np.arcsin(np.clip(c[0] / 0.2, -1, 1))
            rr = radius(B, WAIST, th)
            c[2] = ZC + np.cos(th) * rr + 0.010
            c[0] = np.sin(th) * (rr + 0.010)
            vv, ff = sphere_mesh(c, rng.uniform(0.0075, 0.0105), 7)
            parts.append((vv, ff, 2, np.zeros(len(vv))))
    # skirt
    P = skirt_surface()
    v3, fo, fi = cloth(P, True, 0.004)
    w3 = np.zeros(len(v3)); w3[:NT * NY] = 1.0
    parts.append((v3, fo, 0, w3)); parts.append((v3, fi, 1, w3 * 0))
    # sleeves
    for sg in (-1, 1):
        S = sleeve(sg)
        v4, fo4, fi4 = cloth(S, True, 0.003)
        w4 = np.zeros(len(v4)); w4[:S.shape[0] * S.shape[1]] = 1.0
        parts.append((v4, fo4, 0, w4)); parts.append((v4, fi4, 1, w4 * 0))
    # merge (parts that share vertex arrays are merged once)
    allv, alln, allw, subs = [], [], [], {0: [], 1: [], 2: []}
    seen = {}; off = 0
    for v, f, sub, w in parts:
        key = id(v)
        if key not in seen:
            seen[key] = off
            allv.append(v); allw.append(w); off += len(v)
        subs[sub].append(f + seen[key])
    V_ = np.concatenate(allv); W_ = np.concatenate(allw)
    Fs = {k: np.concatenate(v) for k, v in subs.items()}
    Nn = normals(V_, np.concatenate([Fs[0], Fs[1], Fs[2]]))
    H = crumple(V_)
    return V_.astype(np.float32), Nn.astype(np.float32), W_.astype(np.float32), H.astype(np.float32), Fs

def save(path, V_, N_, W_, H_, Fs):
    with open(path, 'wb') as fh:
        fh.write(struct.pack('<4i', 0x31474D50, len(V_), 3, 0))
        fh.write(V_.astype('<f4').tobytes()); fh.write(N_.astype('<f4').tobytes())
        fh.write(W_.astype('<f4').tobytes()); fh.write(H_.astype('<f4').tobytes())
        for k in range(3):
            fh.write(struct.pack('<i', Fs[k].size)); fh.write(Fs[k].astype('<i4').ravel().tobytes())

if __name__ == '__main__':
    sys.path.insert(0, HERE)
    from render import render, sheet
    V_, N_, W_, H_, Fs = build()
    print('vertices', len(V_), {k: len(f) for k, f in Fs.items()})
    B = bodice_vol(); fs = field(B, SP[(SP[:, 1] > 0.78) & (SP[:, 1] < NECK_Y - 0.01)])
    print('form inside the bodice: worst %.4f (must be < 0)' % fs.max())
    save(os.path.join(OUT, 'gown.bytes'), V_, N_, W_, H_, Fs)
    np.save(os.path.join(OUT, 'gown_dbg.npy'), np.array([dict(V=V_, N=N_, W=W_, H=H_, F=Fs)], dtype=object), allow_pickle=True)
    # preview: smooth and wrinkled, with the form
    fn = np.cross(FORM[TRI[:, 1]] - FORM[TRI[:, 0]], FORM[TRI[:, 2]] - FORM[TRI[:, 0]])
    vn = np.zeros_like(FORM)
    for j in range(3): np.add.at(vn, TRI[:, j], fn)
    vn /= np.maximum(np.linalg.norm(vn, axis=1, keepdims=True), 1e-9)
    form = [(FORM.astype(np.float32), vn.astype(np.float32), TRI, (0.9, 0.9, 0.88), 0.2)]
    champagne = (0.95, 0.86, 0.76)
    ims = []
    for amt in (0.0, 1.0):
        Vw = V_ + N_ * (W_ * H_ * 0.010 * amt)[:, None]
        Nw = normals(Vw, np.concatenate([Fs[0], Fs[1], Fs[2]]))
        P = [(Vw, Nw, Fs[0], champagne, 0.9), (Vw, Nw, Fs[1], champagne, 0.8), (Vw, Nw, Fs[2], (0.85, 0.88, 0.92), 0.95)]
        for yaw in ((15, 100, 190) if amt == 0 else (15,)):
            ims.append(render(P + form, size=(380, 640), yaw=yaw, pitch=5, center=(0, 0.82, 0.05), height=1.72, bg=(70, 70, 76)))
    sheet(ims, os.path.join(OUT, 'gown2.png'))
