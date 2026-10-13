# Cartoon ("toon") avatar kit: big-eyed heads, hairstyles, hats, tops, bottoms and accessories.
# Units: meters, feet at y=0, +Z forward, +X right. Every part is a separate mesh file with submeshes
# whose colors are chosen at runtime. Shared submesh meaning:
#   heads:   0 skin, 1 iris, 2 mouth, 3 white (eyes, highlights), 4 dark (pupils, lashes), 5 brows (hair color)
#   hair:    0 hair, 1 accent (scrunchie / clips)
#   clothes: 0 skin (shown parts of the body), 1 main color, 2 second color (rib, inner tee), 3 accent (zip, strings, logo), 4 dark (belt, soles)
#   hats / accessories: 1 main, 2 second, 3 accent (metal, lens), 4 dark
import numpy as np, struct, os, sys
from sdf import *
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'toon_out')
os.makedirs(OUT, exist_ok=True)
NSUB = 6

EYE_Y = 1.60          # eye height of the model (the runtime scales the figure to the player's eyes)
HEAD_C = np.array([0.0, 1.645, -0.01])

def yaw_basis(deg, pitch=0.0):
    a = np.radians(deg); b = np.radians(pitch)
    z = np.array([np.sin(a) * np.cos(b), -np.sin(b), np.cos(a) * np.cos(b)])
    x = np.array([np.cos(a), 0.0, -np.sin(a)])
    y = np.cross(z, x)
    return np.stack([x, y, z])

def sphere(p, c, r): return np.linalg.norm(p - np.asarray(c, float), axis=1) - r

def torus_y(p, c, R, r):
    q = p - np.asarray(c, float)
    return np.linalg.norm(np.stack([np.linalg.norm(q[:, [0, 2]], axis=1) - R, q[:, 1]], 1), axis=1) - r

def torus_z(p, c, R, r, basis=None):
    q = p - np.asarray(c, float)
    if basis is not None: q = q @ np.asarray(basis, float).T
    return np.linalg.norm(np.stack([np.linalg.norm(q[:, [0, 1]], axis=1) - R, q[:, 2]], 1), axis=1) - r

def arc(p, pts, r):
    d = 9.0
    for a, b in zip(pts[:-1], pts[1:]): d = np.minimum(d, capsule(p, a, b, r, r))
    return d

def shell(d, t): return np.abs(d) - t

# ------------------------------------------------------------------ heads
def head_skin(p, woman):
    k = 0.97 if woman else 1.0
    c = HEAD_C
    d = ellipsoid(p, c, np.array([0.115, 0.119, 0.111]) * k)                                   # big round skull
    jaw = (0.084, 0.070, 0.084) if woman else (0.092, 0.074, 0.088)
    d = smin(d, ellipsoid(p, (0, 1.565, 0.012), np.array(jaw) * k), 0.05)                       # cheeks + chin
    if not woman: d = smin(d, roundbox(p, (0, 1.53, 0.03), (0.05, 0.02, 0.045), 0.02), 0.03)    # firmer chin
    d = smin(d, ellipsoid(p, (0, 1.586, 0.109 * k), (0.017, 0.014, 0.016)), 0.012)              # button nose
    for s in (-1, 1):
        d = smin(d, ellipsoid(p, (s * 0.111 * k, 1.605, -0.005), (0.016, 0.030, 0.022)), 0.012)  # ears
        d = smin(d, ellipsoid(p, (s * 0.055 * k, 1.565, 0.075), (0.03, 0.025, 0.025)), 0.03)     # round cheeks
    d = smin(d, capsule(p, (0, 1.36, -0.005), (0, 1.52, 0.0), 0.045 if woman else 0.052, 0.046 if woman else 0.053), 0.03)  # neck
    return d

EYE_YAW = 24

def eye_frame(s, woman):
    k = 0.97 if woman else 1.0
    B = yaw_basis(s * EYE_YAW, 4)
    c = np.array([s * 0.044 * k, 1.607, 0.086 * k])
    R = np.array([0.029, 0.034, 0.018]) if woman else np.array([0.027, 0.031, 0.017])
    return B, c, R

def on_eye(B, c, R, direction):
    """point where a ray from the eye center along direction leaves the eye white"""
    dl = B @ direction
    t = 1.0 / np.linalg.norm(dl / R)
    return c + direction * t

def eye_parts(woman):
    parts = {1: [], 3: [], 4: []}
    for s in (-1, 1):
        B, c, R = eye_frame(s, woman)
        parts[3].append(('ell', c, R, B))                                                        # eye white
        L = yaw_basis(s * 7, 3)                                                                  # the eyes look forward
        n = L[2]
        sc = R[0] / 0.029
        ic = on_eye(B, c, R, n) - n * 0.0045
        parts[1].append(('ell', ic, np.array([0.0215, 0.0245, 0.0060]) * sc, L))                 # big iris
        pc = ic + n * 0.0030
        parts[4].append(('ell', pc, np.array([0.0105, 0.0125, 0.0040]) * sc, L))                 # pupil
        parts[3].append(('sph', pc + n * 0.0040 + L[0] * 0.0085 + L[1] * 0.0095, 0.0050 * sc))   # sparkles (light from the upper right)
        parts[3].append(('sph', pc + n * 0.0035 - L[0] * 0.0065 - L[1] * 0.0085, 0.0024 * sc))
    return parts

def head_groups(woman):
    k = 0.97 if woman else 1.0
    ep = eye_parts(woman)
    def make(lst):
        def f(p):
            d = 9.0
            for it in lst:
                if it[0] == 'ell': d = np.minimum(d, ellipsoid(p, it[1], it[2], it[3]))
                else: d = np.minimum(d, sphere(p, it[1], it[2]))
            return d
        return f
    eye_white, iris, pupil = make(ep[3]), make(ep[1]), make(ep[4])
    def skin(p):
        d = head_skin(p, woman)
        for s in (-1, 1):   # shallow sockets so the big eyes sit in the face
            B, c, R = eye_frame(s, woman)
            d = np.maximum(d, -ellipsoid(p, c + B[2] * 0.004, R * np.array([1.0, 1.0, 0.75]), B))
        return d
    def dark(p):
        d = pupil(p)
        for s in (-1, 1):
            B, c, R = eye_frame(s, woman)
            pts = []
            for ang in np.linspace(-1.35, 1.25, 10) * (1 if s > 0 else -1):
                x, y = np.sin(ang), np.cos(ang)
                pts.append(c + B[0] * x * R[0] * 1.02 + B[1] * y * R[1] * 1.02 + B[2] * R[2] * 0.45)
            if s < 0: pts = pts[::-1]
            if woman:   # cat-eye wing at the outer corner
                o = pts[0] if s < 0 else pts[-1]
                pts_w = [o, o + B[0] * s * 0.008 + B[1] * 0.0045]
                d = np.minimum(d, arc(p, pts_w, 0.0022))
            d = np.minimum(d, arc(p, pts, 0.0030 if woman else 0.0024))
        return d
    def brows(p):
        d = 9.0
        for s in (-1, 1):
            B = yaw_basis(s * 22)
            c = np.array([s * 0.046 * k, 1.660 if woman else 1.654, 0.094 * k])
            thick = 0.0040 if woman else 0.0060
            pts = [c + B[0] * x * 0.033 + B[1] * (0.007 - 8 * (x * 0.03) ** 2 + (0.003 if x * s > 0 else 0)) for x in np.linspace(-1, 1, 7)]
            d = np.minimum(d, arc(p, pts, thick))
        return d
    def mouth(p):
        w = 0.030 if woman else 0.028
        pts = [np.array([x, 1.537 + 14 * x * x, 0.104 - 2.4 * x * x]) for x in np.linspace(-w, w, 11)]
        d = arc(p, pts, 0.0034)
        if woman:
            d = np.minimum(d, ellipsoid(p, (0, 1.532, 0.100), (0.016, 0.0055, 0.008)))   # lower lip
        return d
    return {0: skin, 1: iris, 2: mouth, 3: eye_white, 4: dark, 5: brows}

# ------------------------------------------------------------------ build helpers
def build(groups, lo, hi, step, nsub=NSUB):
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
    tri = np.array([np.bincount(r, minlength=nsub).argmax() for r in fl]) if len(fl) else np.zeros(0, int)
    subs = [faces[tri == i].ravel() for i in range(nsub)]
    return v, n, subs

def merge(*meshes, nsub=NSUB):
    vs, ns, subs = [], [], [[] for _ in range(nsub)]
    off = 0
    for v, n, s in meshes:
        vs.append(v); ns.append(n)
        for i in range(nsub): subs[i].append(s[i] + off)
        off += len(v)
    return np.concatenate(vs), np.concatenate(ns), [np.concatenate(x).astype(np.int32) for x in subs]

def box_uv(v, n, scale=4.0):
    a = np.abs(n); uv = np.zeros((len(v), 2), np.float32)
    mx = (a[:, 0] >= a[:, 1]) & (a[:, 0] >= a[:, 2]); my = (a[:, 1] > a[:, 0]) & (a[:, 1] >= a[:, 2]); mz = ~(mx | my)
    uv[mx] = v[mx][:, [2, 1]]; uv[my] = v[my][:, [0, 2]]; uv[mz] = v[mz][:, [0, 1]]
    return uv * scale

def save(name, mesh, extra=(EYE_Y, 0, 0, 0)):
    v, n, subs = mesh
    uv = box_uv(v, n)
    with open(os.path.join(OUT, name + '.bytes'), 'wb') as fh:
        fh.write(struct.pack('<3i', 0x504D4D32, len(v), len(subs)))
        fh.write(struct.pack('<4f', *extra))
        fh.write(struct.pack('<%di' % len(subs), *[len(s) for s in subs]))
        fh.write(v.astype('<f4').tobytes()); fh.write(n.astype('<f4').tobytes()); fh.write(uv.astype('<f4').tobytes())
        for s in subs: fh.write(s.astype('<i4').tobytes())
    return mesh

def parts_for_render(mesh, colors, gloss=None):
    v, n, subs = mesh
    out = []
    for i, s in enumerate(subs):
        if len(s) == 0: continue
        out.append((v, n, s.reshape(-1, 3), colors[i], (gloss or {}).get(i, 0.25)))
    return out

def make_head(woman):
    g = head_groups(woman)
    skin = build({0: g[0]}, (-0.15, 1.33, -0.15), (0.15, 1.79, 0.15), 0.0042)
    face = build({k: g[k] for k in (1, 2, 3, 4, 5)}, (-0.09, 1.515, 0.05), (0.09, 1.69, 0.13), 0.0011)
    return merge(skin, face)

# ------------------------------------------------------------------ hair (made for the man's head; the runtime scales it 0.97 for the woman's head)
SKULL_R = np.array([0.115, 0.119, 0.111])

def skull(p, grow):
    return ellipsoid(p, HEAD_C + np.array([0, 0.004, -0.004]), SKULL_R + grow)

def face_cut(d, p, front_y=1.705, side_y=1.635, back_y=1.56, temple=2.6):
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    yf = front_y - temple * x * x
    d = np.maximum(d, np.minimum(z - 0.03, yf - y))            # forehead
    d = np.maximum(d, np.minimum(z + 0.03, side_y - y))        # around the ears
    d = np.maximum(d, back_y - y)                              # nape
    return d

def bumps(p, amp, freq):
    x, y, z = p[:, 0] * freq, p[:, 1] * freq, p[:, 2] * freq
    return amp * (np.sin(x * 1.7 + np.sin(y * 1.3)) * np.sin(y * 1.9 + 0.5) * np.sin(z * 1.6 + np.sin(x * 1.1)))

def strands(p, amp, n=70):
    q = p - HEAD_C
    ang = np.arctan2(q[:, 0], q[:, 2])
    return amp * np.sin(ang * n)

def hair_buzz(p):
    return face_cut(skull(p, 0.004), p, 1.712, 1.64, 1.575)

def hair_crop(p):
    d = skull(p, 0.010) + bumps(p, 0.0022, 60)
    sides = skull(p, 0.006)
    d = np.where(p[:, 1] > 1.69, d, np.minimum(d, sides))
    d = face_cut(d, p, 1.708, 1.64, 1.57)
    for i, x in enumerate(np.linspace(-0.07, 0.07, 7)):     # fringe tufts swept to one side
        a = (0.0 + x + 0.012, 1.708 - 0.012 * np.cos(x * 20), 0.098 - 3 * x * x)
        d = smin(d, ellipsoid(p, a, (0.022, 0.012, 0.018)), 0.01)
    return d

def hair_quiff(p):
    d = face_cut(skull(p, 0.006), p, 1.712, 1.64, 1.57)
    top = np.maximum(skull(p, 0.016) + bumps(p, 0.002, 80), 1.69 - p[:, 1])
    d = smin(d, top, 0.03)
    B = np.array([[1, 0, 0], [0, np.cos(0.5), np.sin(0.5)], [0, -np.sin(0.5), np.cos(0.5)]])
    d = smin(d, ellipsoid(p, (0.012, 1.762, 0.055), (0.072, 0.042, 0.07), B) + strands(p, 0.0015, 50), 0.03)
    return d

def hair_curly(p):
    d = face_cut(skull(p, 0.007), p, 1.712, 1.64, 1.57)
    top = np.maximum(skull(p, 0.016), 1.675 - p[:, 1])
    rng = np.random.default_rng(7)
    curls = 9.0
    for _ in range(140):
        u = rng.normal(size=3); u[1] = abs(u[1]) + 0.5; u /= np.linalg.norm(u)
        c = HEAD_C + np.array([0, 0.004, -0.004]) + u * (SKULL_R + 0.014)
        if c[1] < 1.675 or (c[2] > 0.06 and c[1] < 1.70): continue
        curls = np.minimum(curls, sphere(p, c, rng.uniform(0.012, 0.017)))
    top = smin(top, curls, 0.006)
    return smin(d, top, 0.012)

def hair_bob(p):
    cap = skull(p, 0.015) + strands(p, 0.0010, 80)
    y = p[:, 1]
    q = p - (HEAD_C + np.array([0, 0, -0.012]))
    t = np.clip((1.66 - y) / 0.14, 0, 1)
    rx, rz = 0.126 + 0.012 * t, 0.122 + 0.010 * t
    r = np.sqrt((q[:, 0] / rx) ** 2 + (q[:, 2] / rz) ** 2)
    outer = (r - 1) * 0.125 + strands(p, 0.0010, 80)
    curtain = np.maximum(outer, -((r - 0.80) * 0.125))
    curtain = smin(np.maximum(curtain, y - 1.70), 9.0, 0.01)
    bottom = 1.515 + 0.03 * np.clip(-q[:, 2] / 0.12, 0, 1) * 0
    curtain = -smin(-curtain, -(bottom - y), 0.012)              # rounded lower edge
    d = smin(cap, curtain, 0.025)
    d = np.maximum(d, -ellipsoid(p, (0, 1.585, 0.10), (0.088, 0.112, 0.09)))     # the face stays free
    bangs = np.maximum(skull(p, 0.017) + strands(p, 0.0010, 90), np.maximum(1.678 - y, 0.04 - p[:, 2]))
    return smin(d, bangs, 0.012)

def hair_long(p):
    cap = skull(p, 0.013) + strands(p, 0.0012)
    y = p[:, 1]
    t = np.clip((1.64 - y) / 0.34, 0, 1)                           # a bit wider towards the ends
    q = p - (HEAD_C + np.array([0, 0, -0.03]))
    rx, rz = 0.126 + 0.028 * t, 0.12 + 0.02 * t
    r = np.sqrt((q[:, 0] / rx) ** 2 + (q[:, 2] / rz) ** 2)
    wave = 0.004 * np.sin(y * 40)
    outer = (r - 1) * 0.12 + strands(p, 0.0014, 60) + wave
    curtain = np.maximum(outer, -((r - 0.74) * 0.12))              # hollow inside
    curtain = np.maximum(curtain, y - 1.70)
    curtain = -smin(-curtain, -(1.30 + 0.04 * np.abs(q[:, 0]) / 0.15 - y), 0.015)   # rounded ends
    front = p[:, 2] - (0.06 - np.clip(1.56 - y, 0, 1) * 0.6)       # in front only near the face, then behind the shoulders
    curtain = -smin(-curtain, -front, 0.015)
    d = smin(cap, curtain, 0.02)
    d = np.maximum(d, -ellipsoid(p, (0, 1.585, 0.10), (0.092, 0.118, 0.095)))   # the face stays free
    part = np.maximum(np.abs(p[:, 0]) - 0.003, np.maximum(1.72 - y, -p[:, 2] - 0.02))
    return np.maximum(d, -part)                                     # middle part

def hair_ponytail(p):
    d = face_cut(skull(p, 0.010) + strands(p, 0.0010, 80), p, 1.712, 1.645, 1.565)
    pts = [np.array(a) for a in [(0, 1.738, -0.10), (0, 1.765, -0.15), (0, 1.73, -0.205), (0.01, 1.64, -0.225), (0.02, 1.55, -0.21), (0.015, 1.47, -0.19), (0.0, 1.42, -0.17)]]
    rads = [0.036, 0.046, 0.048, 0.044, 0.036, 0.024, 0.010]
    tail = 9.0
    for a, b, ra, rb in zip(pts[:-1], pts[1:], rads[:-1], rads[1:]): tail = smin(tail, capsule(p, a, b, ra, rb), 0.01)
    return smin(d, tail + strands(p, 0.0016, 30), 0.02)

def acc_ponytail(p):
    B = np.array([[1, 0, 0], [0, 0.6, 0.8], [0, -0.8, 0.6]])
    return torus_z(p, (0, 1.738, -0.112), 0.030, 0.010, B)

def hair_spacebuns(p):
    d = face_cut(skull(p, 0.009) + strands(p, 0.0010, 80), p, 1.712, 1.645, 1.565)
    for s in (-1, 1):
        c = np.array([s * 0.068, 1.752, -0.03])
        q = p - c
        ang = np.arctan2(q[:, 1], q[:, 0] * s) + np.linalg.norm(q, axis=1) * 120
        d = smin(d, sphere(p, c, 0.044) + 0.0025 * np.sin(ang * 3), 0.015)
    d = np.maximum(d, -np.maximum(np.abs(p[:, 0]) - 0.0025, 1.70 - p[:, 1]) + 0.0)   # center part line
    return d

def hair_afro(p):
    c = HEAD_C + np.array([0, 0.035, -0.02])
    d = ellipsoid(p, c, (0.162, 0.148, 0.156)) + bumps(p, 0.007, 60) + bumps(p * 1.6, 0.002, 60)
    d = face_cut(d, p, 1.700, 1.625, 1.56, temple=2.2)
    d = np.maximum(d, -ellipsoid(p, (0, 1.585, 0.10), (0.09, 0.115, 0.095)))
    return d

def acc_afro(p):   # head band
    band = np.maximum(shell(skull(p, 0.013), 0.005), np.abs(p[:, 1] - 1.700 - 0.25 * p[:, 2] * (p[:, 2] < 0)) - 0.014)
    return band

HAIRS = {
    'buzz': (hair_buzz, None), 'crop': (hair_crop, None), 'quiff': (hair_quiff, None), 'curly': (hair_curly, None),
    'bob': (hair_bob, None), 'long': (hair_long, None), 'ponytail': (hair_ponytail, acc_ponytail),
    'spacebuns': (hair_spacebuns, None), 'afro': (hair_afro, acc_afro),
}

def make_hair(name, step=0.0040):
    h, acc = HAIRS[name]
    g = {0: h}
    if acc is not None: g[1] = acc
    return build(g, (-0.2, 1.27, -0.25), (0.2, 1.86, 0.19), step)

# ------------------------------------------------------------------ hats
def hat_cap(p):
    crown = np.maximum(shell(skull(p, 0.022), 0.005), 1.665 - p[:, 1] - 0.20 * np.maximum(-p[:, 2], 0))
    return crown

def hat_cap_visor(p):
    q = p - np.array([0, 1.676, 0.118])
    B = np.array([[1, 0, 0], [0, np.cos(-0.38), -np.sin(-0.38)], [0, np.sin(-0.38), np.cos(-0.38)]])
    vis = ellipsoid(p, (0, 1.686, 0.150), (0.105, 0.0085, 0.085), B)
    vis = np.maximum(vis, -(skull(p, 0.020)))
    button = sphere(p, HEAD_C + np.array([0, 0.004 + SKULL_R[1] + 0.025, -0.004]), 0.011)
    return np.minimum(vis, button)

def hat_beanie(p):
    q = p - HEAD_C
    ang = np.arctan2(q[:, 0], q[:, 2])
    body = np.maximum(shell(skull(p, 0.022) + 0.0012 * np.sin(ang * 40), 0.006), 1.665 - p[:, 1])
    body = smin(body, sphere(p, HEAD_C + np.array([0, 0.155, -0.01]), 0.03) + bumps(p, 0.004, 120), 0.01)   # pom-pom
    return body

def hat_beanie_cuff(p):
    return np.maximum(shell(skull(p, 0.030), 0.007), np.abs(p[:, 1] - 1.688) - 0.024)

def hat_bucket(p):
    q = p - np.array([0, 0, -0.008])
    r2 = np.sqrt((q[:, 0] / 0.134) ** 2 + (q[:, 2] / 0.130) ** 2)
    taper = 2.4 * np.clip(p[:, 1] - 1.67, 0, 1)                      # soft crown, narrower towards the top
    cyl = (r2 - 1 + taper) * 0.13
    cyl = -smin(-cyl, -(p[:, 1] - 1.79), 0.03)                       # rounded top
    crown = np.maximum(shell(cyl, 0.005), 1.672 - p[:, 1])
    q = p - np.array([0, 1.665, -0.01])
    r = np.linalg.norm(q[:, [0, 2]], axis=1)
    brim = np.maximum(np.abs(q[:, 1] + (r - 0.12) * 0.45) - 0.0045, np.maximum(r - 0.195, 0.118 - r))
    return np.minimum(crown, brim)

def hat_bucket_band(p):
    return np.maximum(shell(skull(p, 0.031), 0.004), np.abs(p[:, 1] - 1.684) - 0.011)

HATS = {'cap': {1: hat_cap, 2: hat_cap_visor}, 'beanie': {1: hat_beanie, 2: hat_beanie_cuff}, 'bucket': {1: hat_bucket, 2: hat_bucket_band}}

def make_hat(name):
    return build(HATS[name], (-0.23, 1.55, -0.24), (0.23, 1.86, 0.23), 0.0036)

# cartoon proportions, applied at runtime too: body 0.9 tall, the head group 1.18x around the neck base
BODY_Y = 0.9
HEAD_SCALE = 1.18
NECK = np.array([0.0, 1.40, 0.0])
def cartoon(v, kind):
    if kind == 'h': return ((v - NECK) * HEAD_SCALE + NECK * np.array([1, BODY_Y, 1])).astype(np.float32)
    if kind == 'b': return (v * np.array([1, BODY_Y, 1])).astype(np.float32)
    return v
EYE_RUNTIME = NECK[1] * BODY_Y + (1.607 - NECK[1]) * HEAD_SCALE

# ------------------------------------------------------------------ body (no arms: the hands float on the controllers)
def torso(p, woman, grow=0.0):
    if woman:
        d = ellipsoid(p, (0, 1.235, 0.0), (0.140 + grow, 0.140 + grow, 0.095 + grow))
        d = smin(d, capsule(p, (-0.068, 1.325, -0.006), (0.068, 1.325, -0.006), 0.060 + grow, 0.060 + grow), 0.07)
        d = smin(d, ellipsoid(p, (0, 1.05, 0.0), (0.112 + grow, 0.11 + grow, 0.084 + grow)), 0.06)
        for s_ in (-1, 1):
            d = smin(d, ellipsoid(p, (s_ * 0.05, 1.232, 0.058), (0.056 + grow, 0.050 + grow, 0.046 + grow)), 0.035)
    else:
        d = ellipsoid(p, (0, 1.235, 0.0), (0.158 + grow, 0.150 + grow, 0.105 + grow))
        d = smin(d, capsule(p, (-0.085, 1.325, -0.006), (0.085, 1.325, -0.006), 0.068 + grow, 0.068 + grow), 0.07)
        d = smin(d, ellipsoid(p, (0, 1.05, 0.0), (0.138 + grow, 0.12 + grow, 0.094 + grow)), 0.06)
    d = smin(d, capsule(p, (0, 1.36, -0.005), (0, 1.43, 0.0), 0.05 + grow, 0.05 + grow), 0.03)   # base of the neck
    return d

def hips(p, woman, grow=0.0):
    r = (0.150, 0.105, 0.098) if woman else (0.140, 0.100, 0.096)
    return ellipsoid(p, (0, 0.945, -0.005), (r[0] + grow, r[1] + grow, r[2] + grow))

LEG_X = 0.095
def legs(p, woman, grow=0.0, flare=0.0, bottom=0.10):
    d = 9.0
    for s_ in (-1, 1):
        x = s_ * LEG_X
        thigh = capsule(p, (s_ * 0.085, 0.92, 0.0), (x, 0.52, 0.01), (0.078 if woman else 0.076) + grow, 0.055 + grow)
        shin = capsule(p, (x, 0.52, 0.01), (x, bottom, 0.0), 0.054 + grow, 0.042 + grow + flare)
        d = np.minimum(d, smin(thigh, shin, 0.06))
    return smin(d, hips(p, woman, grow), 0.05)

def cut_below(d, p, y): return np.maximum(d, y - p[:, 1])
def cut_above(d, p, y): return np.maximum(d, p[:, 1] - y)

# tops -------------------------------------------------------------
def top_tee(woman):
    def main(p):
        d = cut_below(torso(p, woman, 0.009), p, 0.95)
        return np.maximum(d, -torso(p, woman, -0.03) )   # (solid enough)
    def collar(p):
        return np.maximum(torus_y(p, (0, 1.405, 0.004), 0.058, 0.0085), -9)
    def logo(p):   # lightning bolt on the chest
        z = 0.105 if woman else 0.112
        pts = [(0.025, 1.30, z), (-0.008, 1.245, z + 0.004), (0.014, 1.245, z + 0.004), (-0.022, 1.18, z)]
        return arc(p, [np.array(q) for q in pts], 0.0075)
    return {1: main, 2: collar, 3: logo}

def top_hoodie(woman):
    def main(p):
        d = cut_below(torso(p, woman, 0.024), p, 0.975)
        pocket = np.maximum(shell(torso(p, woman, 0.030), 0.004), np.maximum(np.abs(p[:, 0]) - 0.085 + (p[:, 1] - 1.02) * 0.25, np.maximum(np.abs(p[:, 1] - 1.055) - 0.05, -p[:, 2])))
        d = smin(d, pocket, 0.004)
        hood = ellipsoid(p, (0, 1.43, -0.075), (0.085, 0.045, 0.05))      # the hood lies behind the neck
        return smin(d, hood, 0.03)
    def rib(p):
        return np.maximum(shell(torso(p, woman, 0.027), 0.006), np.abs(p[:, 1] - 0.975) - 0.025)
    def strings(p):
        z = 0.135 if woman else 0.145
        d = 9.0
        for s_ in (-1, 1):
            d = np.minimum(d, capsule(p, (s_ * 0.028, 1.40, 0.06), (s_ * 0.032, 1.24, z), 0.0045, 0.0045))
            d = np.minimum(d, capsule(p, (s_ * 0.032, 1.245, z), (s_ * 0.032, 1.225, z), 0.0065, 0.0065))
        return d
    return {1: main, 2: rib, 3: strings}

def top_denim(woman):
    def tee(p):
        return cut_below(torso(p, woman, 0.008), p, 0.95)
    def jacket(p):
        d = cut_below(shell(torso(p, woman, 0.03), 0.009), p, 0.985)
        open_front = np.maximum(np.abs(p[:, 0]) - (0.022 + (1.36 - p[:, 1]) * 0.12), -p[:, 2])
        d = np.maximum(d, -open_front)
        for s_ in (-1, 1):   # collar flaps
            c = np.array([s_ * 0.07, 1.385, 0.05])
            d = smin(d, ellipsoid(p, c, (0.05, 0.035, 0.012), yaw_basis(s_ * 35, -40)), 0.01)
            z = 0.118 if woman else 0.125
            d = smin(d, roundbox(p, (s_ * 0.075, 1.27, z - 0.005), (0.035, 0.012, 0.01), 0.004), 0.004)   # pocket flaps
        hem = np.maximum(shell(torso(p, woman, 0.033), 0.008), np.abs(p[:, 1] - 1.0) - 0.018)
        return np.minimum(d, np.maximum(hem, -open_front))
    def buttons(p):
        d = 9.0
        z = 0.128 if woman else 0.136
        for s_ in (-1, 1):
            d = np.minimum(d, sphere(p, (s_ * 0.075, 1.262, z), 0.006))
        for y in (1.2, 1.12, 1.04):
            d = np.minimum(d, sphere(p, (0.045 + (1.36 - y) * 0.12, y, (0.11 if woman else 0.12) + 0.012), 0.0055))
        return d
    return {1: jacket, 2: tee, 3: buttons}

def top_crop(woman):
    def skin(p):
        return cut_below(torso(p, woman, 0.0), p, 0.97)
    def top(p):
        d = np.maximum(torso(p, woman, 0.010) + 0.0007 * np.sin(p[:, 1] * 300), np.maximum(1.13 - p[:, 1], p[:, 1] - 1.32 - 0.25 * np.maximum(-p[:, 2], 0) * 0))
        straps = 9.0
        for s_ in (-1, 1):
            straps = np.minimum(straps, np.maximum(shell(torso(p, woman, 0.006), 0.004), np.maximum(np.abs(p[:, 0] - s_ * 0.075) - 0.012, 1.30 - p[:, 1])))
        return np.minimum(d, straps)
    return {0: skin, 1: top}

def top_puffer(woman):
    def main(p):
        base = torso(p, woman, 0.045)
        quilt = 0.012 * np.abs(np.sin((p[:, 1] - 0.97) * np.pi / 0.075))
        d = cut_below(base + 0.012 - quilt, p, 0.97)
        collar = capsule(p, (0, 1.39, -0.005), (0, 1.475, 0.0), 0.078, 0.074)
        collar = np.maximum(shell(collar, 0.012), -(p[:, 1] - 1.38))
        return smin(d, collar, 0.02)
    def zipper(p):
        z = 0.15 if woman else 0.16
        q = p.copy()
        front = np.maximum(np.abs(p[:, 0]) - 0.004, np.abs(torso(p, woman, 0.058)) - 0.004)
        return np.maximum(front, np.maximum(0.98 - p[:, 1], p[:, 1] - 1.48 + 0 * q[:, 0]))
    return {1: main, 3: zipper}

def top_bomber(woman):
    def main(p):
        return cut_below(torso(p, woman, 0.028), p, 1.0)
    def rib(p):
        hem = np.maximum(shell(torso(p, woman, 0.030), 0.008), np.abs(p[:, 1] - 0.99) - 0.03)
        collar = np.maximum(shell(capsule(p, (0, 1.38, -0.005), (0, 1.44, 0.0), 0.066, 0.064), 0.008), -(p[:, 1] - 1.375))
        return np.minimum(hem, collar)
    def accent(p):
        stripes = np.maximum(shell(torso(p, woman, 0.039), 0.0025), np.minimum(np.abs(p[:, 1] - 0.985) - 0.005, np.abs(p[:, 1] - 1.005) - 0.004))
        zipper = np.maximum(np.abs(p[:, 0]) - 0.0035, np.maximum(np.abs(torso(p, woman, 0.033)) - 0.0035, np.maximum(0.96 - p[:, 1], p[:, 1] - 1.44)))
        z = 0.125 if woman else 0.132
        patch = ellipsoid(p, (-0.07, 1.27, z), (0.024, 0.024, 0.006), yaw_basis(-25))
        return np.minimum(np.minimum(stripes, zipper), patch)
    return {1: main, 2: rib, 3: accent}

def top_shirt(woman):   # white button-up shirt
    def main(p):
        d = cut_below(torso(p, woman, 0.011), p, 0.95)
        placket = np.maximum(np.abs(torso(p, woman, 0.016)) - 0.004, np.maximum(np.abs(p[:, 0]) - 0.016, np.maximum(0.95 - p[:, 1], -p[:, 2])))
        d = np.minimum(d, placket)
        for s_ in (-1, 1):   # pointed collar
            c = np.array([s_ * 0.048, 1.405, 0.048])
            d = smin(d, ellipsoid(p, c, (0.045, 0.032, 0.009), yaw_basis(s_ * 32, -55)), 0.006)
        d = smin(d, torus_y(p, (0, 1.42, 0.0), 0.056, 0.011), 0.01)                       # collar band
        z = 0.11 if woman else 0.12
        d = smin(d, roundbox(p, (-0.07, 1.25, z - 0.004), (0.03, 0.033, 0.006), 0.004), 0.003)   # chest pocket
        return d
    def buttons(p):
        d = 9.0
        for y in (1.34, 1.27, 1.19, 1.11, 1.03):
            z = torso_front_z(woman, y) + 0.018
            d = np.minimum(d, ellipsoid(p, (0, y, z), (0.0065, 0.0065, 0.003)))
        return d
    return {1: main, 3: buttons}

def torso_front_z(woman, y):
    zs = np.linspace(0.0, 0.25, 600)
    P = np.stack([np.zeros_like(zs), np.full_like(zs, y), zs], 1)
    d = torso(P, woman, 0.0)
    i = np.nonzero(d > 0)[0]
    return zs[i[0]] if len(i) else 0.1

def top_bighoodie(woman):   # oversized hoodie with a huge hood
    def main(p):
        base = torso(p, woman, 0.050)
        low = np.maximum(ellipsoid(p, (0, 0.98, 0.0), (0.205 if woman else 0.215, 0.20, 0.15)), -9)   # long, loose body
        d = smin(base, low, 0.06)
        d = cut_below(d, p, 0.865)
        pocket = np.maximum(shell(smin(torso(p, woman, 0.056), low + 0.006, 0.06), 0.004),
                            np.maximum(np.abs(p[:, 0]) - 0.11 + (p[:, 1] - 0.96) * 0.3, np.maximum(np.abs(p[:, 1] - 0.97) - 0.06, -p[:, 2])))
        d = smin(d, pocket, 0.004)
        hood = ellipsoid(p, (0, 1.455, -0.105), (0.14, 0.085, 0.085))
        hood = smin(hood, torus_y(p, (0, 1.425, -0.01), 0.085, 0.032), 0.04)      # thick hood rim around the neck
        hood = np.maximum(hood, -ellipsoid(p, (0, 1.47, 0.02), (0.07, 0.07, 0.07)))  # neck opening
        return smin(d, hood, 0.04)
    def rib(p):
        low = ellipsoid(p, (0, 0.98, 0.0), (0.212 if woman else 0.222, 0.205, 0.155))
        return np.maximum(shell(smin(torso(p, woman, 0.056), low, 0.06), 0.007), np.abs(p[:, 1] - 0.875) - 0.022)
    def strings(p):
        d = 9.0
        for s_ in (-1, 1):
            z0 = torso_front_z(woman, 1.25) + 0.06
            d = np.minimum(d, capsule(p, (s_ * 0.035, 1.42, 0.07), (s_ * 0.042, 1.17, z0), 0.0055, 0.0055))
            d = np.minimum(d, capsule(p, (s_ * 0.042, 1.175, z0), (s_ * 0.042, 1.145, z0), 0.008, 0.008))
        return d
    return {1: main, 2: rib, 3: strings}

def top_silkdress(woman):   # one piece: slip dress with thin straps (legs included)
    def skin(p):
        legs_ = cut_above(cut_below(legs(p, woman, 0.0, bottom=0.10), p, 0.10), p, 0.62)
        return np.minimum(torso(p, woman, 0.0), legs_)
    def dress(p):
        y = p[:, 1]
        bod = torso(p, woman, 0.009)
        neck = 1.30 - np.maximum(0.065 - np.abs(p[:, 0]), 0) * 1.3 * (p[:, 2] > 0)
        bod = np.maximum(bod, y - neck)
        bod = cut_below(bod, p, 0.98)
        q = p - np.array([0, 0, -0.005])
        t = np.clip((1.04 - y) / 0.50, 0, 1)
        rx, rz = 0.150 + 0.10 * t, 0.105 + 0.08 * t
        ang = np.arctan2(q[:, 0], q[:, 2])
        r = np.sqrt((q[:, 0] / rx) ** 2 + (q[:, 2] / rz) ** 2)
        folds = 0.010 * t * np.sin(ang * 7 + 0.6 * np.sin(y * 9))
        skirt = np.maximum((r - 1) * 0.12 + folds, np.maximum(0.55 - y - 0.015 * np.sin(ang * 3), y - 1.05))
        skirt = np.maximum(skirt, -((r - 0.80) * 0.12))
        d = smin(bod, skirt, 0.04)
        for s_ in (-1, 1):
            d = np.minimum(d, np.maximum(shell(torso(p, woman, 0.005), 0.003), np.maximum(np.abs(p[:, 0] - s_ * 0.07) - 0.006, 1.29 - y)))
        return d
    return {0: skin, 1: dress}

def top_kimono(woman):   # one piece: robe to the ankles, crossed collar, obi belt with a bow (sakura texture at runtime)
    def robe_d(p, grow):
        y = p[:, 1]
        q = p - np.array([0, 0, -0.005])
        t = np.clip((1.02 - y) / 0.88, 0, 1)
        rx, rz = 0.165 + 0.035 * t + grow, 0.120 + 0.030 * t + grow
        r = np.sqrt((q[:, 0] / rx) ** 2 + (q[:, 2] / rz) ** 2)
        low = np.maximum((r - 1) * 0.13, np.maximum(0.13 - y, y - 1.06))
        return smin(cut_below(torso(p, woman, 0.022 + grow), p, 0.95), low, 0.05)
    def robe(p):
        d = robe_d(p, 0.0)
        return np.maximum(d, -(robe_d(p, -0.03)))
    def collar(p):
        y = p[:, 1]
        surf = np.abs(robe_d(p, 0.004)) - 0.004
        d = 9.0
        for s_ in (-1, 1):
            xline = s_ * 0.075 * np.clip((y - 1.10) / 0.30, 0, 1)
            band = np.maximum(surf, np.maximum(np.abs(p[:, 0] - xline) - 0.018, np.maximum(1.10 - y, np.maximum(y - 1.44, -p[:, 2] + 0.02))))
            d = np.minimum(d, band)
        behind = np.maximum(torus_y(p, (0, 1.425, -0.01), 0.062, 0.014), p[:, 2] - 0.02)    # around the back of the neck
        seam = np.maximum(surf, np.maximum(np.abs(p[:, 0] - 0.03 * (1.10 - y)) - 0.006, np.maximum(y - 1.10, 0.13 - y + 0 * p[:, 2])))
        seam = np.maximum(seam, -p[:, 2])
        return np.minimum(np.minimum(d, behind), seam)
    def obi(p):
        y = p[:, 1]
        belt = np.maximum(np.abs(robe_d(p, 0.012)) - 0.010, np.abs(y - 1.045) - 0.055)
        bz = -0.15 if woman else -0.16
        bow = np.minimum(ellipsoid(p, (-0.05, 1.06, bz), (0.055, 0.035, 0.025)), ellipsoid(p, (0.05, 1.06, bz), (0.055, 0.035, 0.025)))
        bow = smin(bow, ellipsoid(p, (0, 1.05, bz + 0.01), (0.022, 0.03, 0.022)), 0.01)
        return np.minimum(belt, bow)
    def cord(p):
        return np.maximum(np.abs(robe_d(p, 0.024)) - 0.0035, np.abs(p[:, 1] - 1.045) - 0.0045)
    return {1: robe, 2: collar, 3: obi, 4: cord}

ONE_PIECE = {'silkdress', 'kimono'}

TOPS = {'tee': top_tee, 'hoodie': top_hoodie, 'denim': top_denim, 'crop': top_crop, 'puffer': top_puffer, 'bomber': top_bomber,
        'shirt': top_shirt, 'bighoodie': top_bighoodie, 'silkdress': top_silkdress, 'kimono': top_kimono}

def make_top(name, woman):
    if name in ONE_PIECE:
        return build(TOPS[name](woman), (-0.28, 0.06, -0.22), (0.28, 1.52, 0.24), 0.0068)
    if name == 'bighoodie':
        return build(TOPS[name](woman), (-0.27, 0.82, -0.24), (0.27, 1.58, 0.25), 0.0062)
    return build(TOPS[name](woman), (-0.25, 0.90, -0.18), (0.25, 1.52, 0.22), 0.0062)

# bottoms -----------------------------------------------------------
def belt(woman, y=0.985):
    def f(p):
        return np.maximum(shell(hips(p, woman, 0.016) if False else torso(p, woman, 0.016), 0.006), np.abs(p[:, 1] - y) - 0.016)
    def buckle(p):
        z = 0.11 if woman else 0.118
        return roundbox(p, (0, y, z), (0.022, 0.016, 0.005), 0.003)
    return f, buckle

def bot_jeans(woman):
    b, buckle = belt(woman)
    def main(p):
        d = legs(p, woman, 0.008)
        d = smin(d, cut_above(torso(p, woman, 0.01), p, 1.0), 0.03)
        return cut_below(d, p, 0.10)
    return {1: main, 3: buckle, 4: b}

def bot_cargo(woman):
    b, buckle = belt(woman)
    def main(p):
        d = legs(p, woman, 0.026, flare=0.01) + bumps(p, 0.0008, 30)
        d = smin(d, cut_above(torso(p, woman, 0.012), p, 1.0), 0.03)
        for s_ in (-1, 1):
            d = smin(d, roundbox(p, (s_ * 0.165, 0.66, 0.0), (0.016, 0.06, 0.05), 0.012), 0.008)   # side pockets
        return cut_below(d, p, 0.10)
    return {1: main, 3: buckle, 4: b}

def bot_skirt(woman):
    def skin(p):
        return cut_above(cut_below(legs(p, woman, 0.0, bottom=0.10), p, 0.10), p, 0.80)
    def skirt(p):
        y = p[:, 1]
        q = p - np.array([0, 0, -0.005])
        t = np.clip((1.0 - y) / 0.27, 0, 1)
        rx, rz = 0.148 + 0.06 * t, 0.104 + 0.05 * t
        ang = np.arctan2(q[:, 0], q[:, 2])
        r = np.sqrt((q[:, 0] / rx) ** 2 + (q[:, 2] / rz) ** 2)
        pleat = 0.006 * t * np.abs(np.sin(ang * 9))
        d = np.maximum((r - 1) * 0.11 + pleat, np.maximum(0.73 - y, y - 1.02))
        return np.maximum(d, -((r - 0.82) * 0.11))
    def band(p):
        return np.maximum(shell(torso(p, woman, 0.012), 0.006), np.abs(p[:, 1] - 1.0) - 0.018)
    def socks(p):
        return np.maximum(legs(p, woman, 0.004, bottom=0.10), np.maximum(0.10 - p[:, 1], p[:, 1] - 0.20))
    return {0: skin, 1: skirt, 2: socks, 4: band}

def bot_shorts(woman):
    b, buckle = belt(woman)
    def skin(p):
        return cut_above(cut_below(legs(p, woman, 0.0, bottom=0.10), p, 0.10), p, 0.70)
    def shorts(p):
        d = 9.0
        for s_ in (-1, 1):
            d = np.minimum(d, capsule(p, (s_ * 0.085, 0.92, 0.0), (s_ * 0.10, 0.66, 0.01), 0.095, 0.082))
        d = smin(d, hips(p, woman, 0.016), 0.04)
        d = smin(d, cut_above(torso(p, woman, 0.012), p, 1.0), 0.03)
        return np.maximum(d, 0.64 - p[:, 1])
    def socks(p):
        return np.maximum(legs(p, woman, 0.004, bottom=0.10), np.maximum(0.10 - p[:, 1], p[:, 1] - 0.22))
    return {0: skin, 1: shorts, 2: socks, 3: buckle, 4: b}

def bot_joggers(woman):
    def main(p):
        d = legs(p, woman, 0.020)
        d = smin(d, cut_above(torso(p, woman, 0.012), p, 1.0), 0.03)
        return cut_below(d, p, 0.14)
    def rib(p):
        cuffs = np.maximum(legs(p, woman, 0.006, bottom=0.08), np.maximum(0.10 - p[:, 1], p[:, 1] - 0.15))
        band = np.maximum(shell(torso(p, woman, 0.016), 0.007), np.abs(p[:, 1] - 0.99) - 0.02)
        return np.minimum(cuffs, band)
    def strings(p):
        z = 0.112 if woman else 0.12
        return np.minimum(capsule(p, (-0.012, 0.98, z), (-0.02, 0.90, z + 0.01), 0.004, 0.004), capsule(p, (0.012, 0.98, z), (0.02, 0.91, z + 0.01), 0.004, 0.004))
    return {1: main, 2: rib, 3: strings}

def bot_tutu(woman):   # colorful layered tulle skirt (tiers: 1, 3, 5 = three colors)
    def skin(p):
        return cut_above(cut_below(legs(p, woman, 0.0, bottom=0.10), p, 0.10), p, 0.80)
    def tier(top_y, bottom_y, r_bottom, phase):
        def f(p):
            y = p[:, 1]
            q = p - np.array([0, 0, -0.005])
            t = np.clip((top_y - y) / (top_y - bottom_y), 0, 1)
            rx = 0.15 + (r_bottom - 0.15) * np.sqrt(t)
            rz = 0.105 + (r_bottom * 0.78 - 0.105) * np.sqrt(t)
            ang = np.arctan2(q[:, 0], q[:, 2])
            r = np.sqrt((q[:, 0] / rx) ** 2 + (q[:, 2] / rz) ** 2)
            puff = 0.007 * np.sin(ang * 11 + phase) * t
            edge = bottom_y + 0.012 * np.sin(ang * 16 + phase)
            d = np.maximum(np.abs((r - 1) * 0.12 + puff) - 0.016, np.maximum(edge - y, y - top_y))
            return d
        return f
    t1, t2, t3 = tier(1.0, 0.85, 0.235, 0.0), tier(0.99, 0.80, 0.26, 1.3), tier(0.98, 0.75, 0.28, 2.6)
    def band(p):
        return np.maximum(shell(torso(p, woman, 0.012), 0.007), np.abs(p[:, 1] - 1.0) - 0.02)
    def socks(p):
        return np.maximum(legs(p, woman, 0.004, bottom=0.10), np.maximum(0.10 - p[:, 1], p[:, 1] - 0.20))
    return {0: skin, 1: lambda p: np.minimum(t1(p), band(p)), 2: socks, 3: t2, 5: t3}

BOTTOMS = {'jeans': bot_jeans, 'cargo': bot_cargo, 'skirt': bot_skirt, 'shorts': bot_shorts, 'joggers': bot_joggers, 'tutu': bot_tutu}

def make_bottom(name, woman):
    if name == 'tutu':
        return build(BOTTOMS[name](woman), (-0.31, 0.06, -0.26), (0.31, 1.06, 0.26), 0.0055)
    return build(BOTTOMS[name](woman), (-0.27, 0.06, -0.18), (0.27, 1.06, 0.2), 0.0072)

# accessories -------------------------------------------------------
def eye_center(s): return np.array([s * 0.044, 1.607, 0.086])

def acc_glasses(sun):
    def frame(p):
        d = 9.0
        for s_ in (-1, 1):
            c = np.array([s_ * 0.046, 1.607, 0.125])
            B = yaw_basis(s_ * 10)
            d = np.minimum(d, torus_z(p, c, 0.034, 0.0036 if not sun else 0.0045, B))
            d = np.minimum(d, arc(p, [c + np.array([s_ * 0.034, 0.008, -0.004]), np.array([s_ * 0.112, 1.618, 0.06]), np.array([s_ * 0.118, 1.612, -0.01])], 0.0028))
        d = np.minimum(d, arc(p, [np.array([-0.013, 1.617, 0.127]), np.array([0, 1.622, 0.129]), np.array([0.013, 1.617, 0.127])], 0.003))
        return d
    def lens(p):
        d = 9.0
        for s_ in (-1, 1):
            c = np.array([s_ * 0.046, 1.607, 0.125])
            d = np.minimum(d, ellipsoid(p, c, (0.033, 0.033, 0.003), yaw_basis(s_ * 10)))
        return d
    g = {4 if sun else 3: frame}
    if sun: g[2] = lens
    return g

def acc_earrings():
    def hoops(p):
        d = 9.0
        for s_ in (-1, 1):
            B = np.array([[0, 0, 1], [0, 1, 0], [1, 0, 0]], float)
            d = np.minimum(d, torus_z(p, (s_ * 0.121, 1.565, 0.0), 0.014, 0.0024, B))
        return d
    return {3: hoops}

def acc_necklace():
    def chain(p):
        B = np.array([[1, 0, 0], [0, np.cos(0.35), -np.sin(0.35)], [0, np.sin(0.35), np.cos(0.35)]])
        q = (p - np.array([0, 1.392, 0.012])) @ B.T + np.array([0, 1.392, 0.012])
        d = torus_y(q, (0, 1.392, 0.012), 0.078, 0.0032)
        c = np.array([0, 1.34, 0.115])   # star pendant
        q2 = p - c
        ang = np.arctan2(q2[:, 1], q2[:, 0])
        rr = 0.011 + 0.006 * np.cos(ang * 5)
        star = np.maximum(np.linalg.norm(q2[:, :2], axis=1) - rr, np.abs(q2[:, 2]) - 0.003)
        return np.minimum(d, star)
    return {3: chain}

def acc_headphones():
    def band(p):
        q = p - np.array([0, 1.405, -0.01])
        B = np.array([[1, 0, 0], [0, np.cos(-0.5), -np.sin(-0.5)], [0, np.sin(-0.5), np.cos(-0.5)]])
        q = q @ B.T
        r = np.sqrt((q[:, 0] / 0.11) ** 2 + (q[:, 2] / 0.095) ** 2)
        d = np.maximum(np.abs((r - 1) * 0.1) - 0.007, np.abs(q[:, 1]) - 0.012)
        return np.maximum(d, q[:, 2] - 0.02)
    def cups(p):
        d = 9.0
        for s_ in (-1, 1):
            B = np.array([[0, 1, 0], [0, 0, 1], [1, 0, 0]], float) @ np.eye(3)
            d = np.minimum(d, ellipsoid(p, (s_ * 0.095, 1.395, 0.045), (0.038, 0.038, 0.02), yaw_basis(s_ * 90 - s_ * 30, 60)))
        return d
    return {4: band, 1: cups}

def acc_apron(woman):
    g = 0.055
    def cloth(p):
        y = p[:, 1]
        front = np.maximum(shell(torso(p, woman, g), 0.005), np.maximum(-p[:, 2] + 0.02, np.maximum(np.abs(p[:, 0]) - 0.12, np.maximum(y - 1.36, 1.0 - y))))
        t = np.clip((1.0 - y) / 0.45, 0, 1)
        q = p - np.array([0, 0, -0.01])
        rx, rz = 0.19 + 0.02 * t, 0.15 + 0.02 * t
        r = np.sqrt((q[:, 0] / rx) ** 2 + (q[:, 2] / rz) ** 2)
        skirt = np.maximum(np.abs((r - 1) * 0.15) - 0.004, np.maximum(-p[:, 2] + 0.03, np.maximum(0.55 - y, y - 1.01)))
        straps = 9.0
        for s_ in (-1, 1):
            straps = np.minimum(straps, arc(p, [np.array([s_ * 0.10, 1.355, (0.10 if woman else 0.11) + g]), np.array([s_ * 0.09, 1.44, 0.0]), np.array([s_ * 0.06, 1.40, -0.09])], 0.009))
        return np.minimum(np.minimum(front, skirt), straps)
    def pocket(p):
        z = (0.14 if woman else 0.15) + g * 0.5
        return roundbox(p, (0, 0.86, 0.0), (0.09, 0.05, 0.4), 0.01) * 0 + np.maximum(np.abs(np.sqrt((p[:, 0] / 0.2) ** 2 + ((p[:, 2] + 0.01) / 0.162) ** 2) - 1) * 0.15 - 0.0075, np.maximum(np.abs(p[:, 0]) - 0.085, np.maximum(np.abs(p[:, 1] - 0.83) - 0.055, -p[:, 2] + 0.05)))
    return {1: cloth, 2: pocket}

ACCS = {'glasses': lambda w: acc_glasses(False), 'sunglasses': lambda w: acc_glasses(True), 'earrings': lambda w: acc_earrings(),
        'necklace': lambda w: acc_necklace(), 'headphones': lambda w: acc_headphones(), 'apron': acc_apron}
ACC_PER_BODY = {'apron'}

def make_acc(name, woman):
    if name in ('glasses', 'sunglasses', 'earrings'):
        return build(ACCS[name](woman), (-0.14, 1.53, -0.03), (0.14, 1.68, 0.15), 0.0012)
    if name in ('necklace', 'headphones'):
        return build(ACCS[name](woman), (-0.16, 1.30, -0.13), (0.16, 1.48, 0.16), 0.0022)
    return build(ACCS[name](woman), (-0.26, 0.5, -0.16), (0.26, 1.48, 0.25), 0.0062)

if __name__ == '__main__':
    from render import render, sheet
    what = sys.argv[1] if len(sys.argv) > 1 else 'heads'
    if what == 'heads':
        ims = []
        for woman in (True, False):
            m = save('toon_head_' + ('f' if woman else 'm'), make_head(woman))
            cols = [(0.93, 0.74, 0.62), (0.25, 0.55, 0.85), (0.62, 0.22, 0.28), (1, 1, 1), (0.06, 0.06, 0.08), (0.30, 0.18, 0.10)]
            P = parts_for_render(m, cols, {1: 0.9, 3: 0.9, 4: 0.9})
            print('head', woman, len(m[0]))
            for yaw in (0, 30):
                ims.append(render(P, size=(420, 480), yaw=yaw, pitch=3, center=(0, 1.6, 0), height=0.42))
        sheet(ims, os.path.join(OUT, 'heads.png'), cols=4)
    if what in ('hair', 'hats'):
        head = make_head(False)
        hc = [(0.93, 0.74, 0.62), (0.25, 0.55, 0.85), (0.62, 0.22, 0.28), (1, 1, 1), (0.06, 0.06, 0.08), (0.30, 0.18, 0.10)]
        HP = parts_for_render(head, hc, {1: 0.9, 3: 0.9, 4: 0.9})
        names = sys.argv[2:] or (list(HAIRS) if what == 'hair' else list(HATS))
        ims = []
        for nm in names:
            if what == 'hair':
                m = save('toon_hair_' + nm, make_hair(nm))
                P = parts_for_render(m, [(0.35, 0.2, 0.1), (0.95, 0.35, 0.55)] + [(1, 1, 1)] * 4, {0: 0.45})
            else:
                m = save('toon_hat_' + nm, make_hat(nm))
                P = parts_for_render(m, [(1, 1, 1), (0.95, 0.4, 0.2), (0.15, 0.15, 0.2)] + [(1, 1, 1)] * 3)
            print(nm, len(m[0]))
            for yaw in (25, 160):
                ims.append(render(HP + P, size=(300, 340), yaw=yaw, pitch=8, center=(0, 1.6, -0.02), height=0.6))
        sheet(ims, os.path.join(OUT, what + '.png'), cols=6)
    if what == 'look':   # full characters: python toon.py look f long crop skirt sneaker glasses,earrings
        import body as oldbody
        g = sys.argv[2] == 'f'
        hair, top, bot, shoe = sys.argv[3:7]
        accs = sys.argv[7].split(',') if len(sys.argv) > 7 and sys.argv[7] != '-' else []
        def load(name):
            import struct as st
            b = open(os.path.join(OUT if name.startswith('toon') else os.path.join(os.path.dirname(OUT), 'out'), name + '.bytes'), 'rb').read()
            magic, vc, ns = st.unpack_from('<3i', b, 0); o = 12 + 16
            counts = st.unpack_from('<%di' % ns, b, o); o += 4 * ns
            v = np.frombuffer(b, '<f4', vc * 3, o).reshape(-1, 3); o += vc * 12
            n = np.frombuffer(b, '<f4', vc * 3, o).reshape(-1, 3); o += vc * 12 + vc * 8
            subs = []
            for c in counts: subs.append(np.frombuffer(b, '<i4', c, o)); o += c * 4
            return v.copy(), n.copy(), subs
        def scaled(m, k):
            v, n, subs = m
            return (HEAD_C + (v - HEAD_C) * k).astype(np.float32), n, subs
        k = 0.97 if g else 1.0
        sk = (0.80, 0.58, 0.45)
        P = []; heads = []
        add = lambda parts, kind: (P.extend(parts), heads.extend([kind] * len(parts)))
        add(parts_for_render(load('toon_head_' + ('f' if g else 'm')), [sk, (0.35, 0.6, 0.3), (0.7, 0.25, 0.3), (1, 1, 1), (0.05, 0.05, 0.07), (0.1, 0.07, 0.05)], {1: 0.9, 3: 0.9, 4: 0.9}), 'h')
        add(parts_for_render(scaled(load('toon_hair_' + hair), k), [(0.12, 0.08, 0.06), (0.95, 0.4, 0.6)] + [(1, 1, 1)] * 4, {0: 0.5}), 'h')
        tc = [sk, (0.95, 0.42, 0.55), (0.98, 0.95, 0.9), (0.15, 0.85, 0.85), (0.1, 0.1, 0.1), (1, 1, 1)]
        bc = [sk, (0.22, 0.32, 0.55), (0.97, 0.97, 0.97), (0.85, 0.7, 0.3), (0.12, 0.1, 0.09), (1, 1, 1)]
        add(parts_for_render(load('toon_top_%s_%s' % (top, 'f' if g else 'm')), tc), 'b')
        add(parts_for_render(load('toon_bottom_%s_%s' % (bot, 'f' if g else 'm')), bc), 'b')
        sh = load('shoes_' + shoe)
        add(parts_for_render((sh[0], sh[1], sh[2] + [np.zeros(0, int)] * 3), [(0.97, 0.97, 0.97), (0.85, 0.85, 0.88), (0.1, 0.9, 1)] + [(1, 1, 1)] * 3), 's')
        for a in accs:
            nm = 'toon_acc_%s' % a + ('_' + ('f' if g else 'm') if a in ACC_PER_BODY else '')
            m = load(nm)
            if a in ('glasses', 'sunglasses', 'earrings'): m = scaled(m, k)
            add(parts_for_render(m, [(1, 1, 1), (0.95, 0.35, 0.45), (0.1, 0.1, 0.12), (0.95, 0.78, 0.3), (0.08, 0.08, 0.1), (1, 1, 1)], {3: 0.9, 2: 0.9}), 'h' if a in ('glasses', 'sunglasses', 'earrings') else 'b')
        P = [(cartoon(v, head), n, f, c, gl) for (v, n, f, c, gl), head in zip(P, heads)]
        ims = [render(P, size=(360, 760), yaw=y, pitch=4, center=(0, 0.88, 0), height=1.85) for y in (20, -35, 180)]
        sheet(ims, os.path.join(OUT, 'look_' + '_'.join(sys.argv[2:7]) + '.png'))
    if what in ('tops', 'bottoms', 'accs'):
        names = sys.argv[2:] or list({'tops': TOPS, 'bottoms': BOTTOMS, 'accs': ACCS}[what])
        for nm in names:
            for g in (True, False):
                if what == 'accs' and nm not in ACC_PER_BODY:
                    if not g: continue
                    m = save('toon_acc_' + nm, make_acc(nm, g))
                elif what == 'accs':
                    m = save('toon_acc_%s_%s' % (nm, 'f' if g else 'm'), make_acc(nm, g))
                elif what == 'tops':
                    m = save('toon_top_%s_%s' % (nm, 'f' if g else 'm'), make_top(nm, g))
                else:
                    m = save('toon_bottom_%s_%s' % (nm, 'f' if g else 'm'), make_bottom(nm, g))
                print(what, nm, g, len(m[0]))
