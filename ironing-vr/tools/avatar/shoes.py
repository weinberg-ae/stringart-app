# Shoe pairs for the avatar (feet at y=0, toes +Z). Submeshes: 0 upper, 1 sole, 2 accent.
import numpy as np, os, sys
from sdf import *
from body import build, save, OUT
NS = 3

def pair(fn):
    def f(p):
        return np.minimum(fn(p - np.array([-0.095, 0, 0])), fn(p - np.array([0.095, 0, 0])))
    return f

def footprint(p, grow, L):
    # 2D outline of a foot (toe + heel ellipses), distance in the XZ plane
    q = p.copy(); q[:, 1] = 0
    a = ellipsoid(q, (0, 0, 0.06 * L), (0.05 + grow, 1, 0.085 * L + grow))
    b = ellipsoid(q, (0, 0, -0.06 * L), (0.043 + grow, 1, 0.075 * L + grow))
    return smin(a, b, 0.03)

def sole_of(p, L, h, grow=0.004):
    return smin(np.maximum(footprint(p, grow, L), np.maximum(-p[:, 1], p[:, 1] - h)), 9.0, 0.001)

def on_surface(d_surface, thick):
    return np.abs(d_surface + 0.0015) - thick

def sneaker(L):
    def up(p):
        d = ellipsoid(p, (0, 0.045, 0.055 * L), (0.046, 0.036, 0.085 * L))
        d = smin(d, ellipsoid(p, (0, 0.062, -0.045 * L), (0.044, 0.056, 0.075 * L)), 0.03)
        d = np.maximum(d, 0.022 - p[:, 1])
        d = np.maximum(d, p[:, 1] - 0.112)
        return np.maximum(d, -ellipsoid(p, (0, 0.112, -0.04 * L), (0.034, 0.02, 0.05 * L)))
    def accent(p):
        u = up(p)
        laces = np.maximum(on_surface(u, 0.0025), np.maximum(np.abs(p[:, 0]) - 0.019, 0.06 - p[:, 1]))
        laces = np.maximum(laces, np.abs(np.mod(p[:, 2] + 0.01, 0.02) - 0.01) - 0.004)
        laces = np.maximum(laces, np.maximum(p[:, 2] - 0.085 * L, -0.02 - p[:, 2]))
        stripe = np.maximum(on_surface(u, 0.002), np.abs(p[:, 1] - 0.05 + (p[:, 2]) * 0.3) - 0.007)
        stripe = np.maximum(stripe, np.maximum(np.abs(p[:, 0]) - 0.06, 0.03 - np.abs(p[:, 0])))
        stripe = np.maximum(stripe, np.abs(p[:, 2] + 0.01) - 0.06 * L)
        tab = roundbox(p, (0, 0.088, -0.118 * L), (0.011, 0.022, 0.006), 0.003)
        return np.minimum(np.minimum(laces, stripe), tab)
    return {0: pair(up), 1: pair(lambda p: sole_of(p, L, 0.026)), 2: pair(accent)}

def boot(L):
    def up(p):
        d = ellipsoid(p, (0, 0.05, 0.055 * L), (0.047, 0.04, 0.085 * L))
        d = smin(d, capsule(p, (0, 0.05, -0.04 * L), (0, 0.2, -0.032 * L), 0.054, 0.05), 0.04)
        d = np.maximum(d, 0.03 - p[:, 1])
        return np.maximum(d, p[:, 1] - 0.215)
    def accent(p):
        u = up(p)
        laces = np.maximum(on_surface(u, 0.0025), np.maximum(np.abs(p[:, 0]) - 0.016, 0.07 - p[:, 1]))
        laces = np.maximum(laces, np.abs(np.mod(p[:, 1], 0.024) - 0.012) - 0.0035)
        laces = np.maximum(laces, -p[:, 2] - 0.005)
        return laces
    return {0: pair(up), 1: pair(lambda p: sole_of(p, L, 0.034, 0.005)), 2: pair(accent)}

def classic(L):
    def up(p):
        d = ellipsoid(p, (0, 0.04, 0.062 * L), (0.044, 0.032, 0.092 * L))
        d = smin(d, ellipsoid(p, (0, 0.055, -0.045 * L), (0.042, 0.05, 0.07 * L)), 0.03)
        d = np.maximum(d, 0.018 - p[:, 1])
        d = np.maximum(d, p[:, 1] - 0.097)
        return np.maximum(d, -ellipsoid(p, (0, 0.097, -0.03 * L), (0.033, 0.02, 0.06 * L)))
    def accent(p):
        u = up(p)
        panel = np.maximum(on_surface(u, 0.002), np.maximum(np.abs(p[:, 0]) - 0.018, np.abs(p[:, 2] - 0.035 * L) - 0.03))
        return np.maximum(panel, 0.055 - p[:, 1])
    return {0: pair(up), 1: pair(lambda p: sole_of(p, L, 0.02, 0.003)), 2: pair(accent)}

COLS = np.array([[0.95, 0.95, 0.95], [0.3, 0.3, 0.3], [0.1, 0.9, 1.0]])
if __name__ == '__main__':
    for name, fn in (('sneaker', sneaker), ('boot', boot), ('classic', classic)):
        g = fn(1.0)
        v, n, faces, tl, subs = build(g, (-0.17, -0.004, -0.2), (0.17, 0.24, 0.2), 0.0035)
        save('shoes_' + name, v, n, subs[:NS], [0, 0, 0, 0])
        print(name, len(v))
        preview(v, faces, COLS[np.clip(tl, 0, 2)], os.path.join(OUT, 'shoes_%s.png' % name), views=((25, -40), (10, 60)), title=name, zoom=2.0)
