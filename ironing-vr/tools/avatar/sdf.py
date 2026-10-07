import numpy as np
from skimage.measure import marching_cubes

def norm(v): v = np.asarray(v, float); return v / np.linalg.norm(v)

def smin(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)

def capsule(p, a, b, ra, rb):
    a = np.asarray(a, float); b = np.asarray(b, float)
    pa = p - a; ba = b - a
    h = np.clip((pa @ ba) / (ba @ ba), 0, 1)
    r = ra + (rb - ra) * h
    return np.linalg.norm(pa - h[:, None] * ba, axis=1) - r

def seg_dist(p, a, b):
    a = np.asarray(a, float); b = np.asarray(b, float)
    pa = p - a; ba = b - a
    h = np.clip((pa @ ba) / (ba @ ba), 0, 1)
    return np.linalg.norm(pa - h[:, None] * ba, axis=1), h

def ellipsoid(p, c, r, basis=None):
    q = p - np.asarray(c, float)
    if basis is not None: q = q @ np.asarray(basis, float).T   # rows = local axes
    r = np.asarray(r, float)
    k0 = np.linalg.norm(q / r, axis=1)
    k1 = np.linalg.norm(q / (r * r), axis=1)
    return k0 * (k0 - 1) / np.maximum(k1, 1e-9)

def roundbox(p, c, half, rad):
    q = np.abs(p - np.asarray(c, float)) - np.asarray(half, float) + rad
    return np.linalg.norm(np.maximum(q, 0), axis=1) + np.minimum(q.max(axis=1), 0) - rad

def rot(v, axis, deg):
    axis = norm(axis); v = np.asarray(v, float); t = np.radians(deg)
    return v * np.cos(t) + np.cross(axis, v) * np.sin(t) + axis * (axis @ v) * (1 - np.cos(t))

def mesh_from_sdf(fn, lo, hi, step):
    xs = np.arange(lo[0], hi[0], step); ys = np.arange(lo[1], hi[1], step); zs = np.arange(lo[2], hi[2], step)
    X, Y, Z = np.meshgrid(xs, ys, zs, indexing='ij')
    P = np.stack([X.ravel(), Y.ravel(), Z.ravel()], 1)
    vol = fn(P).reshape(X.shape)
    v, f, _, _ = marching_cubes(vol, 0.0, spacing=(step, step, step))
    v = v + np.asarray(lo, float)
    # normals from the SDF gradient
    e = step * 0.5
    g = np.stack([fn(v + [e, 0, 0]) - fn(v - [e, 0, 0]), fn(v + [0, e, 0]) - fn(v - [0, e, 0]), fn(v + [0, 0, e]) - fn(v - [0, 0, e])], 1)
    n = g / np.maximum(np.linalg.norm(g, axis=1, keepdims=True), 1e-9)
    # Unity: cross(b-a, c-a) must point outwards
    a, b, c = v[f[:, 0]], v[f[:, 1]], v[f[:, 2]]
    cr = np.cross(b - a, c - a)
    if np.mean(np.sum(cr * n[f[:, 0]], 1)) < 0: f = f[:, [0, 2, 1]]
    return v.astype(np.float32), n.astype(np.float32), f.astype(np.int32)

def preview(v, f, colors, path, views=((20, -60), (20, 120)), title='', zoom=2.6):
    import matplotlib; matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d.art3d import Poly3DCollection
    fig = plt.figure(figsize=(7 * len(views), 7))
    light = norm([0.4, 0.8, 0.5])
    for i, (el, az) in enumerate(views):
        ax = fig.add_subplot(1, len(views), i + 1, projection='3d')
        tri = v[f][:, :, [0, 2, 1]]   # show Unity Y as up
        nrm = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
        nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-12)
        sh = np.clip(np.abs(nrm @ light), 0.25, 1)[:, None]
        col = np.clip(colors * sh, 0, 1)
        pc = Poly3DCollection(tri, facecolors=col, edgecolors='none')
        ax.add_collection3d(pc)
        mn, mx = v.min(0), v.max(0); c = (mn + mx) / 2; r = (mx - mn).max() / zoom
        ax.set_xlim(c[0] - r, c[0] + r); ax.set_ylim(c[2] - r, c[2] + r); ax.set_zlim(c[1] - r, c[1] + r)
        ax.view_init(el, az); ax.set_axis_off()
    fig.suptitle(title)
    plt.tight_layout(); plt.savefig(path, dpi=80); plt.close(fig)
