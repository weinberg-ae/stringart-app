# Pressing tools from the reference photos (IMINERA): chest & cuff pad, collar board (turns 360°),
# "donkey" (חמור / kabanchik), shoulder presser, wooden clapper. Units: meters, sitting on y=0, long axis = X.
# Submeshes: 0 padded cloth (color at runtime), 1 wood, 2 dark (screws).
import numpy as np, os, sys
from sdf import *
from toon import build, save, parts_for_render, sphere, OUT
NS = 3

def extrude(d2, y0, y1, r=0.004):
    """2D distance (in XZ) extruded between y0 and y1 with rounded edges"""
    def f(p):
        dd = d2(p)
        h = np.abs(p[:, 1] - (y0 + y1) / 2) - (y1 - y0) / 2
        q = np.stack([np.maximum(dd + r, 0), np.maximum(h + r, 0)], 1)
        return np.linalg.norm(q, axis=1) + np.minimum(np.maximum(dd + r, h + r), 0) - r
    return f

def ell2(p, cx, cz, rx, rz):
    q = np.stack([(p[:, 0] - cx) / rx, (p[:, 2] - cz) / rz], 1)
    k = np.linalg.norm(q, axis=1)
    return (k - 1) * min(rx, rz)

# 1. chest & cuff pad: pear shaped cushion on a thin wooden sole; the narrow end rises a little
def chest_pad():
    def outline(p):
        return smin(ell2(p, -0.075, 0, 0.095, 0.075), ell2(p, 0.075, 0, 0.09, 0.045), 0.06)
    def pad(p):
        x = p[:, 0]
        rise = 0.025 * np.clip((x - 0.02) / 0.14, 0, 1) ** 2
        q = p.copy(); q[:, 1] = q[:, 1] - rise
        d = smin(ellipsoid(q, (-0.075, 0.018, 0), (0.10, 0.095, 0.078)), capsule(q, (-0.02, 0.018, 0), (0.15, 0.018, 0), 0.06, 0.042), 0.05)
        return np.maximum(d, 0.02 - q[:, 1])
    return {0: pad, 1: extrude(outline, 0.0, 0.02)}

# 2. collar & corner board: padded board with a slot (two prongs), on a wooden cross stand that turns
def collar_board():
    def outline(p):
        d = smin(ell2(p, -0.06, 0, 0.13, 0.075), ell2(p, 0.10, 0, 0.09, 0.06), 0.04)
        slot = smin(ell2(p, 0.12, 0, 0.09, 0.016), 9.0 + 0 * p[:, 0], 0.001)
        return np.maximum(d, -slot)
    def board(p):
        return extrude(outline, 0.115, 0.14, 0.008)(p)
    def stand(p):
        legs = np.minimum(roundbox(p, (0, 0.012, 0), (0.12, 0.012, 0.016), 0.008), roundbox(p, (0, 0.012, 0), (0.016, 0.012, 0.09), 0.008))
        legs = np.maximum(legs, -0.0 + p[:, 1] - 0.024 - 0.03 * (1 - np.abs(p[:, 0]) / 0.12) * 0)
        post = roundbox(p, (0, 0.065, 0), (0.017, 0.05, 0.017), 0.006)
        under = roundbox(p, (-0.03, 0.108, 0), (0.09, 0.008, 0.02), 0.006)     # wooden rail under the board
        return np.minimum(np.minimum(legs, post), under)
    def screw(p):
        return sphere(p, (0, 0.142, 0), 0.006)
    return {0: board, 1: stand}   # (the screw on top looked like a dark blob)

# 3. donkey (חמור / kabanchik): pink cushion on a wooden top plate, one pillar with a curved brace, oval foot
def donkey():
    def foot(p):
        return smin(ell2(p, -0.06, 0, 0.12, 0.07), ell2(p, 0.10, 0, 0.09, 0.045), 0.05)
    def top_outline(p):
        return smin(ell2(p, -0.04, 0, 0.13, 0.07), ell2(p, 0.10, 0, 0.08, 0.055), 0.05)
    def cushion(p):
        q = p.copy()
        d = top_outline(q)
        h = 0.155 + 0.035 * np.clip(-d / 0.05, 0, 1) ** 0.5     # dome
        top = np.maximum(d - 0.004, np.maximum(q[:, 1] - h, 0.15 - q[:, 1]))
        return top
    def wood(p):
        base = extrude(foot, 0.0, 0.018)(p)
        plate = extrude(top_outline, 0.138, 0.152, 0.004)(p)
        pillar = roundbox(p, (-0.13, 0.078, 0), (0.016, 0.065, 0.03), 0.004)
        # curved brace between the pillar and the plate
        x, y = p[:, 0], p[:, 1]
        brace = np.maximum(np.abs(p[:, 2]) - 0.012, np.maximum(np.maximum(-0.13 - x, x + 0.03), np.maximum(-(y - 0.14), (0.14 - y) - 0.06 * np.clip((-0.03 - x) / 0.1, 0, 1) ** 0.6 * 1.0)))
        brace = np.maximum(brace, y - 0.15)
        return np.minimum(np.minimum(base, plate), np.minimum(pillar, brace))
    return {0: cushion, 1: wood}

# 4. shoulder presser: square base, a post, top plate and a long half-round blue cushion
def shoulder():
    def wood(p):
        base = roundbox(p, (0, 0.01, 0), (0.115, 0.01, 0.09), 0.006)
        post = roundbox(p, (0, 0.085, 0), (0.026, 0.07, 0.026), 0.004)
        plate = roundbox(p, (0, 0.16, 0), (0.105, 0.007, 0.055), 0.004)
        return np.minimum(np.minimum(base, post), plate)
    def cushion(p):
        q = p - np.array([0, 0.167, 0])
        e = np.sqrt((q[:, 2] / 0.054) ** 2 + (np.maximum(q[:, 1] - 0.02, 0) / 0.06) ** 2)
        d = np.maximum((e - 1) * 0.054, np.maximum(-q[:, 1], np.abs(q[:, 0]) - 0.10))
        return d
    return {0: cushion, 1: wood}

# 5. wooden clapper (reward): a rounded block with finger grooves along the sides
def clapper():
    def wood(p):
        d = roundbox(p, (0, 0.032, 0), (0.135, 0.032, 0.042), 0.02)
        groove = np.maximum(np.abs(np.abs(p[:, 2]) - 0.044) - 0.007, np.maximum(np.abs(p[:, 1] - 0.036) - 0.009, np.abs(p[:, 0]) - 0.095))
        return np.maximum(d, -groove)
    return {1: wood}

TOOLS = {'chestpad': chest_pad, 'collarboard': collar_board, 'donkey': donkey, 'shoulder': shoulder, 'clapper': clapper}

def make(name):
    return build(TOOLS[name](), (-0.2, -0.005, -0.12), (0.22, 0.26, 0.12), 0.003, nsub=NS)

if __name__ == '__main__':
    from render import render, sheet
    names = sys.argv[1:] or list(TOOLS)
    cols = {'chestpad': (0.55, 0.72, 0.95), 'collarboard': (0.90, 0.93, 0.97), 'donkey': (0.98, 0.80, 0.84), 'shoulder': (0.42, 0.62, 0.92), 'clapper': (1, 1, 1)}
    ims = []
    for nm in names:
        m = make(nm)
        v, n, s = m
        save('tool_' + nm, m)
        print(nm, len(v))
        P = parts_for_render(m, [cols[nm], (0.86, 0.72, 0.58), (0.3, 0.3, 0.32)], {0: 0.15, 1: 0.35})
        for yaw in (35, 150):
            ims.append(render(P, size=(360, 300), yaw=yaw, pitch=25, center=(0.01, 0.11, 0), height=0.40))
    sheet(ims, os.path.join(OUT, 'tools.png'), cols=4)
