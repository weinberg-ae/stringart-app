# Laying the cup on the cup pad like real cloth: from the apex, every line of the cup runs over the pad's
# surface with its own length (a geodesic walk), past the dome's base it lies on the plate level.
import numpy as np, sys
sys.path.insert(0, '.')
from tools3d import chest_pad, CUP_C, CUP_R
PADSDF = chest_pad()[0]
BASE_Y = 0.205
CC = np.array(CUP_C); Rx, Ry, Rz = CUP_R

def surf(p):
    return np.minimum(PADSDF(p), p[:, 1] - BASE_Y)

def grad(p, e=1e-4):
    g = np.zeros_like(p)
    for i in range(3):
        d = np.zeros(3); d[i] = e
        g[:, i] = (surf(p + d) - surf(p - d)) / (2 * e)
    return g

def project(p, it=4):
    for _ in range(it):
        g = grad(p); f = surf(p)
        p = p - (f / np.maximum((g * g).sum(1), 1e-9))[:, None] * g
    return p

def normal(p):
    g = grad(p); return g / np.maximum(np.linalg.norm(g, axis=1, keepdims=True), 1e-9)

def walk(apex, t0, gmax, h=0.002):
    """apex point (3,), t0 (k,3) tangent start directions; returns (steps+1, k, 3) positions and the lengths"""
    k = len(t0); n = int(np.ceil(gmax / h))
    P = np.zeros((n + 1, k, 3)); p = np.tile(apex, (k, 1)); t = t0.copy()
    P[0] = p
    for s in range(n):
        q = project(p + t * h, 3)
        d = q - p
        nr = normal(q)
        d = d - (d * nr).sum(1, keepdims=True) * nr
        t = d / np.maximum(np.linalg.norm(d, axis=1, keepdims=True), 1e-9)
        p = q; P[s + 1] = p
    return P, np.arange(n + 1) * h

def frame(ox, oz, psi):
    apex = project(np.array([[CC[0] + ox, CC[1] + Ry + 0.01, CC[2] + oz]]), 8)[0]
    n0 = normal(apex[None])[0]
    c, s = np.cos(psi), np.sin(psi)
    ex = np.array([c, 0, -s]); ey = np.array([-s, 0, -c])        # cup +x and cup +y (up) before tilting to the surface
    ex = ex - (ex @ n0) * n0; ex /= np.linalg.norm(ex)
    ey = np.cross(n0, ex)
    if ey @ np.array([-s, 0, -c]) < 0: ey = -ey
    return apex, ex, ey

def place(a, g, prm, h=0.002):
    """cup polar coords (angle a around the apex, cloth length g, already scaled) -> points on the pad"""
    ox, oz, psi = prm
    apex, ex, ey = frame(ox, oz, psi)
    ua, inv = np.unique(np.round(a, 6), return_inverse=True)
    t0 = np.cos(ua)[:, None] * ex + np.sin(ua)[:, None] * ey
    P, G = walk(apex, t0, g.max() + h, h)
    out = np.zeros((len(a), 3))
    for j in range(len(ua)):
        m = inv == j
        for i in range(3): out[m, i] = np.interp(g[m], G, P[:, j, i])
    return out
