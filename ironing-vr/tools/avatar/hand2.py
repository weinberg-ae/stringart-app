# Hands v3, modelled on the reference sheet: a broad soft palm, smooth even fingers (no knobbly joints)
# with clear gaps, a thick thumb with a round ball, a narrow wrist and a short forearm stub that closes
# smoothly inside the arm. Curled poses get corrective blend shapes: the skinned mesh is projected onto the
# posed shape, so bending never tears or folds the skin.
# Frame: +Z = fingers, +Y = thumb side, palm faces -X, back of the hand +X. Meters. Right hand.
import numpy as np, struct, os, sys
from sdf import norm, smin, capsule, seg_dist, ellipsoid, roundbox, rot, mesh_from_sdf

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out'); os.makedirs(OUT, exist_ok=True)
PALM_N = np.array([-1.0, 0, 0])
MAXA = [62.0, 92.0, 60.0]      # finger curl at grip/trigger = 1 (same as PM_Hand.Pose)
THUMB = [24.0, 34.0]
OPP = 45.0      # the thumb's base turns towards the palm in a grip
FRAMES = [0.34, 0.67, 1.0]

VARIANTS = {
    'man':   dict(scale=1.00, rad=1.00, length=1.00, palm=1.00, thick=1.00, nail=1.00, wrist=1.00),
    'woman': dict(scale=0.92, rad=0.88, length=1.02, palm=0.92, thick=0.90, nail=1.30, wrist=0.86),
    'child': dict(scale=0.72, rad=1.08, length=0.90, palm=1.00, thick=1.08, nail=0.9, wrist=1.05),
    'toonm': dict(scale=1.04, rad=1.04, length=0.96, palm=1.02, thick=1.02, nail=1.00, wrist=1.00),
    'toonf': dict(scale=0.97, rad=0.92, length=0.98, palm=0.94, thick=0.92, nail=1.25, wrist=0.88),
}

def smax(a, b, k): return -smin(-a, -b, k)

def axis_angle(ax, deg):
    ax = np.asarray(ax, float); ax = ax / np.linalg.norm(ax); t = np.radians(deg)
    K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
    return np.eye(3) + np.sin(t) * K + (1 - np.cos(t)) * K @ K

class Hand:
    def __init__(self, var):
        P = self.P = VARIANTS[var]
        R, L, pm = P['rad'], P['length'], P['palm']
        self.knuck = {1: (0.010, 0.0310 * pm, -0.001), 2: (0.010, 0.0104 * pm, 0.003), 3: (0.010, -0.0104 * pm, 0.000), 4: (0.009, -0.0295 * pm, -0.009)}
        dirs = {1: (0, 0.08, 1), 2: (0, 0.01, 1), 3: (0, -0.07, 1), 4: (0, -0.16, 1)}
        lens = {1: [0.037, 0.023, 0.019], 2: [0.041, 0.025, 0.020], 3: [0.039, 0.024, 0.019], 4: [0.030, 0.019, 0.017]}
        rads = {1: [0.0086, 0.0083, 0.0079, 0.0073], 2: [0.0089, 0.0086, 0.0082, 0.0075], 3: [0.0085, 0.0082, 0.0078, 0.0072], 4: [0.0076, 0.0073, 0.0069, 0.0064]}
        curl0 = [4, 7, 5]
        self.joints = [np.zeros(3)]; self.parents = [-1]; self.axes = [np.array([0, 1.0, 0])]
        self.segs = []   # (bone, r0, r1, finger, j)
        self.tips = {}
        def chain(finger, start, d, ls, rs, curls):
            d = norm(d); p = np.asarray(start, float); parent = 0
            for j in range(3):
                ax = norm(np.cross(d, PALM_N))
                d = rot(d, ax, curls[j])
                idx = len(self.joints); self.joints.append(p.copy()); self.parents.append(parent); self.axes.append(ax)
                self.segs.append((idx, rs[j], rs[j + 1], finger, j))
                q = p + d * ls[j]
                parent = idx; p = q
            self.tips[finger] = (len(self.joints) - 1, ls[2])
        # thumb: from the base of the palm, spread out like on the reference
        tl = [0.036 * L, 0.031 * L, 0.027 * L]
        tr = [0.0128 * R, 0.0116 * R, 0.0104 * R, 0.0092 * R]
        chain(0, (0.003, 0.020 * pm, -0.070), (-0.40, 0.58, 0.71), tl, tr, [0, 12, 12])
        for f in (1, 2, 3, 4):
            chain(f, self.knuck[f], dirs[f], [x * L for x in lens[f]], [x * R for x in rads[f]], curl0)
        self.axes[1] = norm((-0.47, 0.41, 0.79))   # the thumb turns (opposition) around its own length in a grip
        self.joints = np.array(self.joints); self.axes = np.array(self.axes)
        self.bonelen = {}
        for (idx, r0, r1, finger, j) in self.segs:
            self.bonelen[idx] = (lens[finger][j] * L) if finger > 0 else tl[j]
        # rest direction of every bone (in the bone's parent space it is fixed; we keep world rest dirs)
        self.restdir = {}
        for (idx, r0, r1, finger, j) in self.segs:
            nxt = [s for s in self.segs if s[3] == finger and s[4] == j + 1]
            if nxt: self.restdir[idx] = norm(self.joints[nxt[0][0]] - self.joints[idx])
        for f in range(5):
            last = [s for s in self.segs if s[3] == f and s[4] == 2][0][0]
            prev = [s for s in self.segs if s[3] == f and s[4] == 1][0][0]
            d = norm(self.joints[last] - self.joints[prev])
            self.restdir[last] = norm(rot(d, self.axes[last], [0, 12, 12][2] if f == 0 else curl0[2]))
        self.wrist = np.array([0.011, -0.001, -0.125])

    # ---- the shape for a set of bone transforms (rotation, translation of each bone in hand space)
    def palm_sdf(self, p):
        P = self.P; pm, th, w = P['palm'], P['thick'], P['wrist']
        x, y, z = p[:, 0], p[:, 1], p[:, 2]
        # outline (seen from the palm): a rounded trapezoid, full width under the fingers, narrower at the wrist
        s = np.clip((z + 0.085) / 0.07, 0, 1); s = s * s * (3 - 2 * s)
        half = (0.0300 + 0.0100 * s) * pm
        top = 0.004 - 0.010 * np.clip(-(y - 0.004) / 0.03, 0, 1) ** 2       # the pinky side is a little lower
        rc = 0.011
        qy = np.abs(y + 0.0005) - (half - rc); qz = np.maximum(z - (top - rc), (-0.090 - z) - rc + rc)
        qz = z - (top - rc)
        d2 = np.where(qz > 0, np.sqrt(np.maximum(qy, 0) ** 2 + qz ** 2) - rc, np.maximum(qy - rc, -0.2))
        d2 = np.maximum(d2, -0.088 - z)
        # thickness: a soft pillow, fuller at the heel of the hand
        heel = np.clip((-0.02 - z) / 0.06, 0, 1)
        tb = (0.0108 + 0.0010 * heel) * th           # back side
        tf = (0.0100 + 0.0035 * heel) * th           # palm side
        xc = 0.011
        dx = np.where(x > xc, x - xc - tb, xc - x - tf)
        d = smax(d2, dx, 0.009)
        # thumb ball (thenar) and the outer pad on the palm side
        d = smin(d, ellipsoid(p, (0.004, -0.022 * pm, -0.058), (0.0100 * th, 0.013 * pm, 0.030)), 0.010)
        # wrist and a short forearm stub that ends round (inside the arm)
        t = np.clip((-0.072 - z) / 0.06, 0, 1)
        rx = (0.0168 + 0.0035 * t) * w
        ry = (0.0258 - 0.0030 * t) * w
        qx = (x - 0.011) / rx; qy2 = (y + 0.001) / ry
        e = (np.sqrt(qx * qx + qy2 * qy2) - 1) * np.minimum(rx, ry)
        e = smax(e, -0.152 - z, 0.014)
        e = smax(e, z + 0.05, 0.01)
        d = smin(d, e, 0.016)
        return d

    def finger_sdf(self, p, finger, WR, WT):
        d = 9.0 + 0 * p[:, 0]
        for (idx, r0, r1, f, j) in self.segs:
            if f != finger: continue
            a = WT[idx]; b = a + WR[idx] @ self.restdir[idx] * self.bonelen[idx]
            d = np.minimum(d, capsule(p, a, b, r0, r1))
        if finger == 0:   # the thumb ball turns with the thumb
            P = self.P
            c = WT[1] + WR[1] @ (np.array([0.002, 0.016 * P['palm'], -0.060]) - self.joints[1])
            B = WR[1].T
            d = smin(d, ellipsoid(p, c, (0.0120 * P['thick'], 0.019 * P['palm'], 0.028), B), 0.012)
        return d

    def nail_frame(self, finger, WR, WT):
        idx = [s for s in self.segs if s[3] == finger and s[4] == 2][0]
        b = idx[0]; r = idx[2]
        dd = WR[b] @ self.restdir[b]; ax = WR[b] @ self.axes[b]
        dorsal = norm(np.cross(dd, ax)); side = norm(np.cross(dorsal, dd))
        ln = self.bonelen[b]
        tip = WT[b] + dd * ln
        c = tip - dd * ln * 0.34 + dorsal * r * 0.72 + dd * 0.0025 * (self.P['nail'] - 1) * 3
        rr = (ln * 0.40 * self.P['nail'], r * 0.66, 0.0022)
        return c, np.stack([dd, side, dorsal]), rr, dorsal

    def nail_sdf(self, p, finger, WR, WT, grow=0.0):
        c, B, rr, _ = self.nail_frame(finger, WR, WT)
        return ellipsoid(p, c, (rr[0] + grow, rr[1] + grow, rr[2] + grow), B)

    def full_finger(self, p, finger, WR, WT):
        return smin(self.finger_sdf(p, finger, WR, WT), self.nail_sdf(p, finger, WR, WT), 0.0012)

    def sdf(self, p, WR, WT, only=None):
        palm = self.palm_sdf(p)
        if only is not None and only >= 0:
            return smin(palm, self.full_finger(p, only, WR, WT), 0.016 if only == 0 else 0.008)
        fingers = self.full_finger(p, 1, WR, WT)
        for f in (2, 3, 4):
            fingers = np.minimum(fingers, self.full_finger(p, f, WR, WT))
        d = smin(palm, fingers, 0.008)          # one smooth web under all the fingers
        return smin(d, self.full_finger(p, 0, WR, WT), 0.016)

    def bone_xf(self, g, t):
        jc = len(self.joints)
        Rl = [np.eye(3) for _ in range(jc)]
        for f in range(1, 5):
            c = t if f == 1 else g
            for j in range(3):
                b = 1 + f * 3 + j
                Rl[b] = axis_angle(self.axes[b], MAXA[j] * c)
        Rl[1] = axis_angle(self.axes[1], OPP * g); Rl[2] = axis_angle(self.axes[2], THUMB[0] * g); Rl[3] = axis_angle(self.axes[3], THUMB[1] * g)
        WR, WT = [None] * jc, [None] * jc
        for i in range(jc):
            p = self.parents[i]
            if p < 0: WR[i] = Rl[i]; WT[i] = self.joints[i]
            else: WR[i] = WR[p] @ Rl[i]; WT[i] = WT[p] + WR[p] @ (self.joints[i] - self.joints[p])
        return WR, WT

    def rest(self): return self.bone_xf(0.0, 0.0)

def project(fn, x, iters=6):
    e = 0.0004
    for _ in range(iters):
        d = fn(x)
        g = np.stack([fn(x + [e, 0, 0]) - fn(x - [e, 0, 0]), fn(x + [0, e, 0]) - fn(x - [0, e, 0]), fn(x + [0, 0, e]) - fn(x - [0, 0, e])], 1) / (2 * e)
        gn = np.maximum(np.linalg.norm(g, axis=1, keepdims=True), 1e-6)
        x = x - (d[:, None]) * g / gn ** 2
    g = np.stack([fn(x + [e, 0, 0]) - fn(x - [e, 0, 0]), fn(x + [0, e, 0]) - fn(x - [0, e, 0]), fn(x + [0, 0, e]) - fn(x - [0, 0, e])], 1)
    return x, g / np.maximum(np.linalg.norm(g, axis=1, keepdims=True), 1e-9)

def build(var, step=0.0014):
    H = Hand(var)
    WR0, WT0 = H.rest()
    fn = lambda p: H.sdf(p, WR0, WT0)
    v, n, f = mesh_from_sdf(fn, (-0.072, -0.075, -0.17), (0.05, 0.11, 0.13), step)
    v = v.astype(float); n = n.astype(float)

    # region of every vertex: -1 palm, 0..4 finger
    palm = H.palm_sdf(v)
    fd = np.stack([H.full_finger(v, k, WR0, WT0) for k in range(5)], 1)
    best = fd.argmin(1); bd = fd.min(1)
    region = np.where(bd < palm - 0.0015, best, -1)

    # nails
    nailv = np.zeros(len(v), bool)
    for k in range(5):
        c, B, rr, dorsal = H.nail_frame(k, WR0, WT0)
        inside = H.nail_sdf(v, k, WR0, WT0, 0.0008) < 0.0
        nailv |= inside & (n @ dorsal > 0.45)
    nailf = nailv[f].all(1)

    # skin weights: hard labels (palm = root, finger = the bone along the finger), then diffused over the
    # surface so every joint blends smoothly (no tearing between the palm and the fingers)
    nbone = len(H.joints)
    lab = np.zeros(len(v), int)
    for k in range(5):
        chain = [s_ for s_ in H.segs if s_[3] == k]
        sel = np.nonzero(region == k)[0]
        if len(sel) == 0: continue
        x = v[sel]
        dists = []; hs = []
        for (idx, r0, r1, f2, j) in chain:
            a_ = WT0[idx]; b_ = a_ + WR0[idx] @ H.restdir[idx] * H.bonelen[idx]
            dd, h = seg_dist(x, a_, b_)
            ab = b_ - a_; dists.append(dd); hs.append(((x - a_) @ ab) / (ab @ ab))
        dists = np.stack(dists, 1); hs = np.stack(hs, 1)
        nb = dists.argmin(1)
        u = nb + np.clip(hs[np.arange(len(sel)), nb], 0, 1)
        lab[sel] = np.array([c[0] for c in chain])[np.clip(np.floor(u), 0, 2).astype(int)]
    import scipy.sparse as sp
    e = np.concatenate([f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]]])
    A_ = sp.coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(len(v), len(v))).tocsr()
    A_ = ((A_ + A_.T) > 0).astype(float)
    deg = np.asarray(A_.sum(1)).ravel()
    Wf = np.zeros((len(v), nbone)); Wf[np.arange(len(v)), lab] = 1.0
    M = sp.diags(1.0 / np.maximum(deg, 1)) @ A_
    for _ in range(int(28 * (0.0014 / step) ** 2)):
        Wf = 0.5 * Wf + 0.5 * (M @ Wf)
    # nails ride on the last bone
    order = np.argsort(-Wf, 1)[:, :2]
    I = order; W = np.take_along_axis(Wf, order, 1)
    W = W / np.maximum(W.sum(1, keepdims=True), 1e-9)

    WRj = np.stack(WR0); WTj = np.stack(WT0)

    # corrective blend shapes
    def lbs_mats(WR, WT):
        WR = np.stack(WR); WT = np.stack(WT)
        A = np.zeros((len(v), 3, 3)); b = np.zeros((len(v), 3))
        for c in range(2):
            bi = I[:, c]; w = W[:, c][:, None]
            Rb = WR[bi]; Tb = WT[bi] - np.einsum('nij,nj->ni', Rb, H.joints[bi])
            A += w[:, :, None] * Rb; b += w * Tb
        return A, b
    shapes = []
    for name, gi in (('grip', True), ('index', False)):
        frames = []
        for fw in FRAMES:
            g, t = (fw, 0.0) if gi else (0.0, fw)
            WR, WT = H.bone_xf(g, t)
            A, bb = lbs_mats(WR, WT)
            S = np.einsum('nij,nj->ni', A, v) + bb
            T = S.copy(); NT = np.zeros_like(n)
            moving = [0, 2, 3, 4] if gi else [1]
            for k in [-1] + moving:
                sel = np.nonzero(region == k)[0]
                if k == -1:
                    # palm skin near the fingers follows the whole hand; elsewhere only palm + thumb
                    nearf = fd[sel][:, 1:].min(1) < 0.008
                    x1, n1 = project(lambda p, WR=WR, WT=WT: H.sdf(p, WR, WT), S[sel[nearf]])
                    x2, n2 = project(lambda p, WR=WR, WT=WT: H.sdf(p, WR, WT, only=0), S[sel[~nearf]])
                    T[sel[nearf]] = x1; NT[sel[nearf]] = n1; T[sel[~nearf]] = x2; NT[sel[~nearf]] = n2
                    continue
                else:
                    fnk = lambda p, k=k, WR=WR, WT=WT: H.sdf(p, WR, WT, only=k)
                x, nn = project(fnk, S[sel])
                T[sel] = x; NT[sel] = nn
            # vertices of fingers that do not move: rigid, keep
            still = ~np.isin(region, [-1] + moving)
            T[still] = S[still]
            Ainv = np.linalg.inv(A)
            dv = np.einsum('nij,nj->ni', Ainv, T - bb) - v
            dv[still] = 0
            # smooth the correction over the surface so neighbours never pull apart (no cracks)
            for _ in range(12):
                dv = 0.5 * dv + 0.5 * (M @ dv)
            # limit: never move a vertex more than 6 mm in bind space (safety)
            ln = np.linalg.norm(dv, axis=1, keepdims=True); dv = dv * np.minimum(1, 0.009 / np.maximum(ln, 1e-9))
            nb_ = np.einsum('nji,nj->ni', A, NT)   # A^T n
            nb_ /= np.maximum(np.linalg.norm(nb_, axis=1, keepdims=True), 1e-9)
            moved = (np.linalg.norm(dv, axis=1) > 2e-5) & ~still
            dn = np.where(moved[:, None], nb_ - n, 0.0)
            for _ in range(4):
                dn = 0.5 * dn + 0.5 * (M @ dn)
            frames.append((fw * 100.0, dv.astype(np.float32), dn.astype(np.float32)))
        shapes.append((name, frames))

    # UV: back-of-hand projection for tattoos; palm side pushed outwards
    u = (v[:, 1] + 0.045) / 0.090; vv = (v[:, 2] + 0.110) / 0.125
    uv = np.stack([u, vv], 1)
    back = n[:, 0] > 0.25
    cen = np.array([0.5, 0.5]); uv[~back] = cen + (uv[~back] - cen) * 3.0

    s = H.P['scale']
    shapes = [(nm, [(fw, dv * s, dn) for (fw, dv, dn) in fr]) for nm, fr in shapes]
    return dict(v=(v * s).astype(np.float32), n=n.astype(np.float32), f=f, nailf=nailf, uv=uv.astype(np.float32), I=I, W=W.astype(np.float32),
                J=(H.joints * s).astype(np.float32), parents=H.parents, axes=H.axes.astype(np.float32), wrist=(H.wrist * s).astype(np.float32),
                shapes=shapes, region=region)

def save(var, d):
    skin = d['f'][~d['nailf']].ravel(); nails = d['f'][d['nailf']].ravel()
    J = d['J']; v = d['v']
    with open(os.path.join(OUT, 'hand_%s.bytes' % var), 'wb') as fh:
        fh.write(struct.pack('<5i', 0x504D4832, len(v), len(skin), len(nails), len(J)))
        fh.write(struct.pack('<3f', *d['wrist']))
        for i in range(len(J)):
            fh.write(struct.pack('<i3f3f', d['parents'][i], *J[i], *d['axes'][i]))
        fh.write(v.astype('<f4').tobytes()); fh.write(d['n'].astype('<f4').tobytes()); fh.write(d['uv'].astype('<f4').tobytes())
        fh.write(d['I'].astype('<i4').tobytes()); fh.write(d['W'].astype('<f4').tobytes())
        fh.write(skin.astype('<i4').tobytes()); fh.write(nails.astype('<i4').tobytes())
        fh.write(struct.pack('<i', len(d['shapes'])))
        for nm, frames in d['shapes']:
            b = nm.encode('ascii'); fh.write(struct.pack('<i', len(b))); fh.write(b)
            fh.write(struct.pack('<i', len(frames)))
            for fw, dv, dn in frames:
                # sparse: only the vertices that move
                idx = np.nonzero((np.abs(dv).max(1) > 1e-5) | (np.abs(dn).max(1) > 2e-3))[0]
                fh.write(struct.pack('<fi', fw, len(idx))); fh.write(idx.astype('<i4').tobytes())
                fh.write(dv[idx].astype('<f4').tobytes()); fh.write(dn[idx].astype('<f4').tobytes())
    return len(v), len(d['f'])

# ---- preview: same pose math as PM_Hand (LBS + blend shapes)
def posed(d, g, t, corrective=True):
    H = Hand(d['var']) if 'var' in d else None
    v = d['v'].astype(float).copy(); n = d['n'].astype(float).copy()
    if corrective:
        for nm, frames in d['shapes']:
            wv = (g if nm == 'grip' else t) * 100.0
            prev_w, prev_v, prev_n = 0.0, 0 * v, 0 * n
            for fw, dv, dn in frames:
                if wv <= fw:
                    a = (wv - prev_w) / (fw - prev_w)
                    v += prev_v + (dv - prev_v) * a; n += prev_n + (dn - prev_n) * a
                    break
                prev_w, prev_v, prev_n = fw, dv, dn
    s = VARIANTS[d['var']]['scale']
    H = Hand(d['var'])
    WR, WT = H.bone_xf(g, t)
    WR = np.stack(WR); WT = np.stack(WT) * s; Jj = H.joints * s
    out = np.zeros_like(v); on = np.zeros_like(n)
    for c in range(2):
        bi = d['I'][:, c]; w = d['W'][:, c:c + 1]
        out += w * (np.einsum('nij,nj->ni', WR[bi], v - Jj[bi]) + WT[bi])
        on += w * np.einsum('nij,nj->ni', WR[bi], n)
    on /= np.maximum(np.linalg.norm(on, axis=1, keepdims=True), 1e-9)
    return out, on

if __name__ == '__main__':
    from render import render, sheet
    vars_ = sys.argv[1:] or list(VARIANTS)
    for var in vars_:
        d = build(var); d['var'] = var
        print(var, save(var, d), 'regions', np.bincount(d['region'] + 1))
        np.save(os.path.join(OUT, 'hand2_%s.npy' % var), np.array([d], dtype=object), allow_pickle=True)
        ims = []
        skin = d['f'][~d['nailf']]; nails = d['f'][d['nailf']]
        for g, t, yaws in ((0.0, 0.0, (180, 0)), (0.55, 0.5, (150, 60)), (1.0, 1.0, (150, 60))):
            pv, pn = posed(d, g, t)
            R = np.stack([pv[:, 1], pv[:, 2], -pv[:, 0]], 1).astype(np.float32)
            Nn = np.stack([pn[:, 1], pn[:, 2], -pn[:, 0]], 1).astype(np.float32)
            P = [(R, Nn, skin, (0.80, 0.76, 0.76), 0.25), (R, Nn, nails, (0.92, 0.86, 0.86), 0.6)]
            for yaw in yaws:
                ims.append(render(P, size=(300, 420), yaw=yaw, pitch=5, center=(0.0, -0.035, 0.0), height=0.30, bg=(140, 140, 140)))
        sheet(ims, os.path.join(OUT, 'hand2_%s.png' % var), cols=6)
