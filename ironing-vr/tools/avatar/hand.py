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
    # cartoon hands for the toon characters: chubbier fingers, rounder palm, a bit bigger
    # hands of the cartoon characters: natural slender hands (separate tapered fingers), a little bigger
    'toonm': dict(scale=1.06, rad=1.00, length=1.00, palm=1.00, thick=1.00, nail=1.00, wrist=1.00),
    'toonf': dict(scale=1.00, rad=0.86, length=1.05, palm=0.92, thick=0.90, nail=1.30, wrist=0.86),
}

def build(var):
    P = VARIANTS[var]
    R, L = P['rad'], P['length']
    # Knuckles spread across the palm; fingers splay a little so there are gaps between them.
    knuck = {1: (0.012, 0.030 * P['palm'], -0.001), 2: (0.012, 0.0095 * P['palm'], 0.003), 3: (0.012, -0.0105 * P['palm'], -0.001), 4: (0.011, -0.0290 * P['palm'], -0.012)}
    dirs = {1: (0, 0.10, 1), 2: (0, 0.015, 1), 3: (0, -0.07, 1), 4: (0, -0.17, 1)}
    lens = {1: [0.044, 0.027, 0.021], 2: [0.048, 0.030, 0.023], 3: [0.045, 0.028, 0.022], 4: [0.035, 0.021, 0.018]}
    rads = {1: [0.0098, 0.0089, 0.0080, 0.0068], 2: [0.0100, 0.0091, 0.0082, 0.0070], 3: [0.0095, 0.0087, 0.0078, 0.0066], 4: [0.0085, 0.0078, 0.0070, 0.0060]}
    curl0 = [5, 8, 5]
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
    tl = [0.040 * L, 0.031 * L, 0.026 * L]
    tr = [0.0125 * R, 0.0110 * R, 0.0098 * R, 0.0084 * R]
    chain(0, (0.004, 0.024 * P['palm'], -0.072), (-0.35, 0.55, 0.76), tl, tr, [0, 10, 10])
    for f in (1, 2, 3, 4):
        chain(f, knuck[f], dirs[f], [x * L for x in lens[f]], [x * R for x in rads[f]], curl0)

    th = P['thick']
    def palm_sdf(p):
        pm = P['palm']
        d = ellipsoid(p, (0.012, 0.001 * pm, -0.043), (0.0125 * th, 0.040 * pm, 0.047))                    # soft palm
        d = smin(d, capsule(p, (0.011, 0.030 * pm, -0.004), (0.011, -0.028 * pm, -0.014), 0.0112 * th, 0.0105 * th), 0.016)   # knuckle line
        d = smin(d, capsule(p, (0.010, 0.016 * pm, -0.084), (0.010, -0.024 * pm, -0.084), 0.0135 * th, 0.0135 * th), 0.018)   # heel of the hand
        d = smin(d, capsule(p, (0.007, -0.030 * pm, -0.080), (0.008, -0.031 * pm, -0.026), 0.0105 * th, 0.0095 * th), 0.014)  # outer edge
        d = smin(d, capsule(p, (0.002, 0.014 * pm, -0.078), segs[0][2] + np.array([0.004, -0.004, -0.004]), 0.015 * R, 0.011 * R), 0.014)  # thumb ball
        # wrist and a tapered forearm (flattened a little)
        w = P['wrist']
        for yy in (-0.006, 0.006):
            d = smin(d, capsule(p, (0.012, -0.004 + yy * w, -0.092), (0.012, -0.004 + yy * w, -0.200), 0.0175 * w, 0.0225 * w), 0.018)
        for f in (1, 2, 3, 4):   # knuckle bumps on the back of the hand
            k = np.asarray(knuck[f]) + (0.008 * th, 0, 0)
            d = smin(d, ellipsoid(p, k, (0.0065, 0.0075 * R, 0.008)), 0.005)
        return d
    def finger_sdf(p, finger):
        d = 9.0
        for (idx, a, b, r0, r1, f, j) in segs:
            if f != finger: continue
            d = smin(d, capsule(p, a, b, r0, r1), 0.005)
        tip, dd = tips[finger]
        return smin(d, nail_sdf(p, finger, tip, dd, 0), 0.0012)
    def sdf(p):
        palm = palm_sdf(p)
        # each finger blends only with the palm, never with its neighbours: real gaps between the fingers
        d = smin(palm, finger_sdf(p, 0), 0.016)
        for f in (1, 2, 3, 4):
            d = np.minimum(d, smin(palm, finger_sdf(p, f), 0.009))
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

    v, n, f = mesh_from_sdf(sdf, (-0.075, -0.07, -0.215), (0.05, 0.11, 0.135), 0.0015)

    # Nail triangles (separate submesh)
    nailv = np.zeros(len(v), bool)
    for finger, (tip, dd) in tips.items():
        c, B, rr, dorsal = nail_frame(finger, tip, dd)
        inside = nail_sdf(v, finger, tip, dd, 0.0008) < 0.0
        nailv |= inside & (n @ dorsal > 0.45)
    nailf = nailv[f].all(1)

    # Skin weights: the nearest bone, blended smoothly with its parent around each joint
    # (half / half exactly at the joint), so bending fingers never tears the skin at the knuckles.
    nb = len(joints)
    D = np.full((len(v), nb), 9.0); Hh = np.zeros((len(v), nb)); Lr = np.ones(nb)
    core = roundbox(v, (0.012, -0.004, -0.050), (0.004, 0.030 * P['palm'], 0.042), 0.002)
    wd, _ = seg_dist(v, (0.012, -0.006, -0.095), (0.012, -0.006, -0.20))
    D[:, 0] = np.maximum(np.minimum(core, wd - 0.012), 0.0) + 0.002
    for (idx, a, b, r0, r1, finger, j) in segs:
        dd, h = seg_dist(v, a, b)
        D[:, idx] = np.maximum(dd - r0 * 0.6, 0) + 0.002
        # signed position along the bone (negative before the joint) for the blend
        ab = np.asarray(b) - np.asarray(a)
        Hh[:, idx] = ((v - np.asarray(a)) @ ab) / (ab @ ab)
    near = D.argmin(1)
    I = np.zeros((len(v), 2), int); W = np.zeros((len(v), 2))
    par = np.array(parents)
    for i in range(len(v)):
        b0 = near[i]
        if b0 == 0:
            I[i] = (0, 0); W[i] = (1.0, 0.0); continue
        t = np.clip((Hh[i, b0] + 0.22) / 0.44, 0.0, 1.0)
        sm = t * t * (3 - 2 * t)
        I[i] = (b0, max(par[b0], 0)); W[i] = (sm, 1.0 - sm)

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
