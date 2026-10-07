# Smooth avatar body (feet on y=0, +Z forward, +X right) and head (shown only in the mirror).
# Submeshes: 0 skin, 1 shirt, 2 pants, 3 shoes, 4 apron, 5 hair, 6 eyes.
import numpy as np, struct, os, sys
from sdf import *
OUT = os.path.join(os.path.dirname(__file__), 'out')
NSUB = 7

def groups_body(woman):
    g = {}
    s = 0.95 if woman else 1.0
    def Y(y): return y * s
    sh = 0.165 if woman else 0.19
    def shoes(p):
        return np.minimum(ellipsoid(p, (-0.1, Y(0.045), 0.04), (0.05, 0.045, 0.13)), ellipsoid(p, (0.1, Y(0.045), 0.04), (0.05, 0.045, 0.13)))
    def pants(p):
        d = 9.0
        for x in (-0.1, 0.1):
            hx = x * (1.05 if woman else 0.95)
            d = np.minimum(d, capsule(p, (x, Y(0.09), 0.0), (x, Y(0.50), 0.01), 0.052, 0.062))
            d = smin(d, capsule(p, (x, Y(0.50), 0.01), (hx, Y(0.90), 0.0), 0.062, 0.088 if woman else 0.082), 0.03)
        d = smin(d, ellipsoid(p, (0, Y(0.95), 0), (0.18 if woman else 0.165, 0.12, 0.115 if woman else 0.105)), 0.04)
        return d
    def shirt(p):
        d = ellipsoid(p, (0, Y(1.08), 0), (0.125, 0.12, 0.09) if woman else (0.15, 0.12, 0.10))
        d = smin(d, ellipsoid(p, (0, Y(1.30), 0), (0.155, 0.17, 0.11) if woman else (0.19, 0.17, 0.12)), 0.06)
        d = smin(d, capsule(p, (-sh, Y(1.415), -0.005), (sh, Y(1.415), -0.005), 0.06, 0.06), 0.06)
        if woman:
            for x in (-0.06, 0.06):
                d = smin(d, ellipsoid(p, (x, Y(1.285), 0.07), (0.065, 0.06, 0.055)), 0.03)
        d = smin(d, ellipsoid(p, (0, Y(1.00), 0), (0.16 if woman else 0.155, 0.08, 0.105)), 0.04)   # tucked into the pants
        return d
    def neck(p):
        return capsule(p, (0, Y(1.40), 0), (0, Y(1.53), 0.01), 0.05 if woman else 0.056, 0.048 if woman else 0.054)
    def apron(p):
        # Skirt part: a curved sheet hanging in front of the hips and legs; bib part: follows the chest.
        x, y, z = p[:, 0], p[:, 1], p[:, 2]
        a, b = (0.25, 0.16) if woman else (0.23, 0.15)
        e = (np.sqrt((x / a) ** 2 + ((z + 0.005) / b) ** 2) - 1) * b
        skirt = np.maximum(np.abs(e) - 0.006, np.maximum(-z + 0.02, np.maximum(y - Y(1.05), Y(0.55) - y)))
        bib = np.maximum(np.abs(shirt(p) - 0.016) - 0.006,
                         np.maximum(np.maximum(np.abs(x) - 0.12, 0.05 - z), np.maximum(y - Y(1.37), Y(1.02) - y)))
        d = np.minimum(skirt, bib)
        for xx in (-0.1, 0.1):
            d = np.minimum(d, capsule(p, (xx, Y(1.36), 0.10 if not woman else 0.12), (xx * 0.85, Y(1.47), -0.01), 0.010, 0.010))
        ba, bb = (0.19, 0.13) if woman else (0.18, 0.123)
        band = np.maximum(np.abs((np.sqrt((x / ba) ** 2 + (z / bb) ** 2) - 1) * bb) - 0.005, np.abs(y - Y(1.04)) - 0.017)
        d = np.minimum(d, band)
        return d
    g[0] = neck; g[1] = shirt; g[2] = pants; g[3] = shoes; g[4] = apron
    eye = Y(1.60); shoulders = (sh + 0.02, Y(1.40), -0.005)
    return g, eye, shoulders

def groups_head(woman):
    s = 0.95 if woman else 1.0
    def Y(y): return y * s
    k = 0.93 if woman else 1.0
    def skin(p):
        d = ellipsoid(p, (0, Y(1.625), 0.0), (0.075 * k, 0.10 * k, 0.095 * k))
        d = smin(d, ellipsoid(p, (0, Y(1.555), 0.035), (0.058 * k, 0.055 * k, 0.065 * k)), 0.03)
        d = smin(d, capsule(p, (0, Y(1.618), 0.088 * k), (0, Y(1.588), 0.104 * k), 0.012 * k, 0.014 * k), 0.012)
        d = smin(d, ellipsoid(p, (0, Y(1.648), 0.07 * k), (0.06 * k, 0.016, 0.03)), 0.015)
        for x in (-1, 1):
            d = smin(d, ellipsoid(p, (x * 0.077 * k, Y(1.61), 0.0), (0.012, 0.03, 0.02)), 0.008)
            d = smin(d, ellipsoid(p, (x * 0.035 * k, Y(1.585), 0.075 * k), (0.022, 0.016, 0.02)), 0.02)   # cheeks
        d = smin(d, ellipsoid(p, (0, Y(1.556), 0.096 * k), (0.022, 0.007, 0.01)), 0.006)                 # lips
        d = smin(d, capsule(p, (0, Y(1.40), 0), (0, Y(1.56), 0.01), 0.054 * k, 0.05 * k), 0.03)
        return d
    def hair(p):
        cap = ellipsoid(p, (0, Y(1.632), -0.008), (0.082 * k, 0.106 * k, 0.102 * k))
        cut = np.maximum(-(p[:, 1] - Y(1.63)), p[:, 2] - 0.06 * k)          # only above the ears and behind the forehead line
        cap = np.maximum(cap, np.minimum(-(p[:, 1] - Y(1.665)) , cut))
        d = cap
        if woman:
            d = smin(d, ellipsoid(p, (0, Y(1.545), -0.058), (0.08, 0.13, 0.05)), 0.03)                 # long hair at the back
            d = smin(d, ellipsoid(p, (0, Y(1.72), -0.07), (0.045, 0.045, 0.045)), 0.02)               # bun
        for x in (-1, 1):
            d = np.minimum(d, ellipsoid(p, (x * 0.033 * k, Y(1.652), 0.083 * k), (0.019, 0.0045, 0.008)))   # brows
        return d
    def eyes(p):
        return np.minimum(ellipsoid(p, (-0.032 * k, Y(1.627), 0.081 * k), (0.012, 0.008, 0.006)),
                          ellipsoid(p, (0.032 * k, Y(1.627), 0.081 * k), (0.012, 0.008, 0.006)))
    return {0: skin, 5: hair, 6: eyes}

def build(groups, lo, hi, step):
    keys = sorted(groups)
    def f(p):
        d = None
        for kk in keys:
            v = groups[kk](p)
            d = v if d is None else np.minimum(d, v)
        return d
    v, n, faces = mesh_from_sdf(f, lo, hi, step)
    vals = np.stack([groups[kk](v) for kk in keys], 1)
    lab = np.array(keys)[vals.argmin(1)]
    fl = lab[faces]
    tri_lab = np.array([np.bincount(r, minlength=NSUB).argmax() for r in fl])
    subs = [faces[tri_lab == i].ravel() for i in range(NSUB)]
    return v, n, faces, tri_lab, subs

def save(name, v, n, subs, extra):
    with open(os.path.join(OUT, name + '.bytes'), 'wb') as fh:
        fh.write(struct.pack('<3i', 0x504D4D31, len(v), NSUB))
        fh.write(struct.pack('<%df' % len(extra), *extra))
        fh.write(struct.pack('<%di' % NSUB, *[len(s) for s in subs]))
        fh.write(v.astype('<f4').tobytes()); fh.write(n.astype('<f4').tobytes())
        for s in subs: fh.write(s.astype('<i4').tobytes())

COL = np.array([[0.93, 0.76, 0.64], [0.15, 0.15, 0.18], [0.25, 0.27, 0.35], [0.08, 0.08, 0.08], [0.1, 0.35, 0.45], [0.35, 0.22, 0.12], [0.1, 0.1, 0.1]])
if __name__ == '__main__':
    for var in sys.argv[1:] or ('man', 'woman'):
        woman = var == 'woman'
        g, eye, shoulders = groups_body(woman)
        ap = g.pop(4)
        v, n, faces, tl, subs = build(g, (-0.32, -0.01, -0.2), (0.32, 1.58, 0.26), 0.011)
        av, an, af, atl, asubs = build({4: ap}, (-0.28, 0.45, -0.17), (0.28, 1.52, 0.2), 0.0055)
        faces = np.concatenate([faces, af + len(v)]); tl = np.concatenate([tl, atl])
        subs = [np.concatenate([subs[i], asubs[i] + len(v)]) for i in range(NSUB)]
        v = np.concatenate([v, av]); n = np.concatenate([n, an])
        save('body_' + var, v, n, subs, [eye, *shoulders])
        h = groups_head(woman)
        hv, hn, hf, htl, hsubs = build(h, (-0.12, 1.30, -0.16), (0.12, 1.80, 0.14), 0.0045)
        save('head_' + var, hv, hn, hsubs, [eye, 0, 0, 0])
        print(var, len(v), len(hv))
        allv = np.concatenate([v, hv]); allf = np.concatenate([faces, hf + len(v)]); alll = np.concatenate([tl, htl])
        preview(allv, allf, COL[alll], os.path.join(OUT, 'body_%s.png' % var), views=((10, -60), (10, 30)), title=var, zoom=2.0)
