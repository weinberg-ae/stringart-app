# Generates smooth skinned human hands (right hand; the game mirrors it for the left).
# Frame: +Z = fingers, +Y = thumb side, palm faces -X, back of the hand +X. Meters.
import numpy as np, struct, os, sys
from sdf import *

OUT = os.path.join(os.path.dirname(__file__), 'out'); os.makedirs(OUT, exist_ok=True)
PALM_N = np.array([-1.0, 0, 0])

VARIANTS = {
    'man':   dict(scale=1.00, rad=1.00, length=1.00, palm=1.00, thick=1.00, nail=1.00, wrist=1.00),
    'woman': dict(scale=0.93, rad=0.84, length=1.04, palm=0.90, thick=0.88, nail=1.35, wrist=0.84),
    'child': dict(scale=0.72, rad=1.10, length=0.86, palm=1.00, thick=1.10, nail=0.9, wrist=1.05),
}

def build(var):
    P = VARIANTS[var]
    R, L = P['rad'], P['length']
    knuck = {1: (0.013, 0.027 * P['palm'], -0.002), 2: (0.013, 0.007 * P['palm'], 0.002), 3: (0.013, -0.013 * P['palm'], -0.001), 4: (0.012, -0.031 * P['palm'], -0.010)}
    dirs = {1: (0, 0.06, 1), 2: (0, 0, 1), 3: (0, -0.05, 1), 4: (0, -0.12, 1)}
    lens = {1: [0.043, 0.025, 0.022], 2: [0.047, 0.029, 0.024], 3: [0.044, 0.027, 0.023], 4: [0.035, 0.021, 0.020]}
    rads = {1: [0.0098, 0.0090, 0.0082, 0.0072], 2: [0.0100, 0.0092, 0.0084, 0.0074], 3: [0.0094, 0.0087, 0.0079, 0.0070], 4: [0.0086, 0.0079, 0.0071, 0.0063]}
    curl0 = [8, 10, 6]
    # Joints: 0 root, then thumb (1..3), index (4..6), middle (7..9), ring (10..12), pinky (13..15)
    joints = [np.zeros(3)]; parents = [-1]; axes = [np.array([0, 1.0, 0])]
    segs = []   # (bone index, start, end, r0, r1, finger, j)
    tips = {}
    def chain(finger, start, d, ls, rs, curls):
        d = norm(d); p = np.asarray(start, float); parent = 0
        for j in range(3):
            ax = norm(np.cross(d, PALM_N))
            d = rot(d, ax, curls[j])
            q = p + d * ls[j]
            idx = len(joints); joints.append(p.copy()); parents.append(parent); axes.append(ax)
            segs.append((idx, p.copy(), q.copy(), rs[j], rs[j + 1], finger, j))
            parent = idx; p = q
        tips[finger] = (p.copy(), d.copy())
    tl = [0.040 * L, 0.032 * L, 0.027 * L]
    tr = [0.014 * R, 0.0118 * R, 0.0105 * R, 0.0090 * R]
    chain(0, (0.004, 0.020 * P['palm'], -0.075), (-0.32, 0.48, 0.82), tl, tr, [0, 10, 10])
    for f in (1, 2, 3, 4):
        chain(f, knuck[f], dirs[f], [x * L for x in lens[f]], [x * R for x in rads[f]], curl0)

    th = P['thick']
    def sdf(p):
        d = roundbox(p, (0.012, -0.004 * P['palm'], -0.050), (0.0115 * th, 0.037 * P['palm'], 0.050), 0.010)
        d = smin(d, ellipsoid(p, (0.016, -0.004, -0.048), (0.012 * th, 0.040 * P['palm'], 0.055)), 0.01)
        # thenar (thumb ball) and hypothenar (outer palm edge)
        d = smin(d, capsule(p, (0.0, 0.016 * P['palm'], -0.080), segs[0][2] + np.array([0.004, -0.004, -0.004]), 0.017 * R, 0.012 * R), 0.012)
        d = smin(d, capsule(p, (0.006, -0.034 * P['palm'], -0.092), (0.007, -0.033 * P['palm'], -0.025), 0.012 * th, 0.010 * th), 0.012)
        # wrist and forearm (flattened)
        w = P['wrist']
        for yy in (-0.007, 0.007):
            d = smin(d, capsule(p, (0.012, -0.006 + yy * w, -0.095), (0.012, -0.006 + yy * w, -0.180), 0.020 * w, 0.023 * w), 0.016)
        # knuckle bumps
        for f in (1, 2, 3, 4):
            k = np.asarray(knuck[f]) + (0.008 * th, 0, 0)
            d = smin(d, ellipsoid(p, k, (0.007, 0.0085 * R, 0.009)), 0.006)
        # fingers
        for (idx, a, b, r0, r1, finger, j) in segs:
            kk = 0.012 if j == 0 else 0.004
            if finger == 0 and j == 0: kk = 0.016
            d = smin(d, capsule(p, a, b, r0, r1), kk)
        # nails (slightly raised)
        for finger, (tip, dd) in tips.items():
            d = smin(d, nail_sdf(p, finger, tip, dd, 0), 0.0012)
        return d

    def nail_frame(finger, tip, dd):
        seg = [s for s in segs if s[5] == finger and s[6] == 2][0]
        ax = axes[seg[0]]
        dorsal = norm(np.cross(dd, ax))
        side = norm(np.cross(dorsal, dd))
        r = seg[4]
        ln = np.linalg.norm(seg[2] - seg[1])
        c = tip - dd * ln * 0.36 + dorsal * r * 0.70 + dd * 0.0025 * (P['nail'] - 1) * 3
        rr = (ln * 0.40 * P['nail'], r * 0.66, 0.0024)
        return c, np.stack([dd, side, dorsal]), rr, dorsal
    def nail_sdf(p, finger, tip, dd, grow):
        c, B, rr, _ = nail_frame(finger, tip, dd)
        return ellipsoid(p, c, (rr[0] + grow, rr[1] + grow, rr[2] + grow), B)

    v, n, f = mesh_from_sdf(sdf, (-0.040, -0.065, -0.215), (0.050, 0.105, 0.135), 0.0018)

    # Nail triangles (separate submesh)
    nailv = np.zeros(len(v), bool)
    for finger, (tip, dd) in tips.items():
        c, B, rr, dorsal = nail_frame(finger, tip, dd)
        inside = nail_sdf(v, finger, tip, dd, 0.0008) < 0.0
        nailv |= inside & (n @ dorsal > 0.45)
    nailf = nailv[f].all(1)

    # Skin weights: nearest bone + its parent/child, by inverse distance^4
    nb = len(joints)
    D = np.full((len(v), nb), 9.0)
    core = roundbox(v, (0.012, -0.004, -0.050), (0.004, 0.030 * P['palm'], 0.042), 0.002)
    wd, _ = seg_dist(v, (0.012, -0.006, -0.095), (0.012, -0.006, -0.20))
    D[:, 0] = np.maximum(np.minimum(core, wd - 0.012), 0.0) + 0.002
    for (idx, a, b, r0, r1, finger, j) in segs:
        dd, h = seg_dist(v, a, b)
        D[:, idx] = np.maximum(dd - r0 * 0.6, 0) + 0.002
    children = {i: [k for k in range(nb) if parents[k] == i] for i in range(nb)}
    near = D.argmin(1)
    W = np.zeros((len(v), 2)); I = np.zeros((len(v), 2), int)
    for i in range(len(v)):
        b0 = near[i]
        cand = [parents[b0]] + children[b0] if parents[b0] >= 0 else children[b0]
        cand = [c for c in cand if c >= 0]
        b1 = min(cand, key=lambda c: D[i, c]) if cand else b0
        w0 = 1 / D[i, b0] ** 4; w1 = 1 / D[i, b1] ** 4 if b1 != b0 else 0
        s = w0 + w1
        I[i] = (b0, b1); W[i] = (w0 / s, w1 / s)

    # UV: back-of-hand projection for tattoos; palm side pushed outwards (white border of the texture).
    u = (v[:, 1] + 0.045) / 0.090; vv = (v[:, 2] + 0.110) / 0.125
    uv = np.stack([u, vv], 1)
    back = n[:, 0] > 0.25
    cen = np.array([0.5, 0.5])
    uv[~back] = cen + (uv[~back] - cen) * 3.0
    s = P['scale']
    v = v * s
    J = np.array(joints) * s
    return v, n, f, nailf, uv.astype(np.float32), I, W.astype(np.float32), J.astype(np.float32), parents, np.array(axes, np.float32)

def save(var, data):
    v, n, f, nailf, uv, I, W, J, parents, axes = data
    skin = f[~nailf].ravel(); nails = f[nailf].ravel()
    with open(os.path.join(OUT, 'hand_%s.bytes' % var), 'wb') as fh:
        fh.write(struct.pack('<5i', 0x504D4831, len(v), len(skin), len(nails), len(J)))
        for i in range(len(J)):
            fh.write(struct.pack('<i3f3f', parents[i], *J[i], *axes[i]))
        fh.write(v.astype('<f4').tobytes()); fh.write(n.astype('<f4').tobytes()); fh.write(uv.astype('<f4').tobytes())
        fh.write(I.astype('<i4').tobytes()); fh.write(W.astype('<f4').tobytes())
        fh.write(skin.astype('<i4').tobytes()); fh.write(nails.astype('<i4').tobytes())
    return len(v), len(f)

if __name__ == '__main__':
    for var in sys.argv[1:] or VARIANTS:
        data = build(var)
        print(var, save(var, data))
        v, n, f, nailf = data[0], data[1], data[2], data[3]
        col = np.tile([[0.93, 0.76, 0.64]], (len(f), 1)); col[nailf] = [0.9, 0.3, 0.4]
        preview(v, f, col, os.path.join(OUT, 'hand_%s.png' % var), views=((30, -40), (-20, 150)), title=var)
