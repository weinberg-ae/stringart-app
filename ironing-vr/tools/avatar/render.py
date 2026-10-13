# Small software renderer for previews: z-buffer, smooth normals, soft light + rim + specular.
import numpy as np
from PIL import Image

def look_basis(yaw_deg, pitch_deg):
    y = np.radians(yaw_deg); p = np.radians(pitch_deg)
    fwd = np.array([np.sin(y) * np.cos(p), np.sin(p), np.cos(y) * np.cos(p)])   # camera looks along -fwd (from fwd side)
    right = np.cross([0, 1, 0], fwd); right /= np.linalg.norm(right)
    up = np.cross(fwd, right)
    return right, up, fwd

def render(parts, path=None, size=(520, 760), yaw=20, pitch=8, center=None, height=None, bg=(238, 240, 245), img=None):
    """parts: list of (v, n, f, color(3) or per-vertex colors, gloss)"""
    W, H = size
    allv = np.concatenate([p[0] for p in parts])
    mn, mx = allv.min(0), allv.max(0)
    c = (mn + mx) / 2 if center is None else np.asarray(center, float)
    hgt = (mx[1] - mn[1]) * 1.08 if height is None else height
    right, up, fwd = look_basis(yaw, pitch)
    scale = H / hgt
    zbuf = np.full((H, W), np.inf); col = np.zeros((H, W, 3)); col[:] = np.array(bg) / 255.0
    light = np.array([0.45, 0.75, 0.6]); light /= np.linalg.norm(light)
    fill = np.array([-0.6, 0.2, 0.3]); fill /= np.linalg.norm(fill)
    for v, n, f, color, gloss in parts:
        q = v - c
        sx = q @ right * scale + W / 2; sy = H / 2 - q @ up * scale; sz = -(q @ fwd)   # smaller = closer
        nn = n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-9)
        diff = np.clip(nn @ light, 0, 1) * 0.75 + np.clip(nn @ fill, 0, 1) * 0.18 + 0.28
        view = fwd
        rim = np.clip(1 - np.abs(nn @ view), 0, 1) ** 3 * 0.25
        h = light + view; h /= np.linalg.norm(h)
        spec = np.clip(nn @ h, 0, 1) ** (20 + 80 * gloss) * (0.15 + 0.6 * gloss)
        cc = np.asarray(color, float)
        if cc.ndim == 1: cc = np.tile(cc, (len(v), 1))
        shade = np.clip(cc * diff[:, None] + rim[:, None] + spec[:, None], 0, 1)
        # back-face culling (camera looks from +fwd towards -fwd)
        a, b, d = f[:, 0], f[:, 1], f[:, 2]
        cr = (sx[b] - sx[a]) * (sy[d] - sy[a]) - (sy[b] - sy[a]) * (sx[d] - sx[a])
        keep = cr < 0
        for t in np.nonzero(keep)[0]:
            i0, i1, i2 = f[t]
            xs = np.array([sx[i0], sx[i1], sx[i2]]); ys = np.array([sy[i0], sy[i1], sy[i2]])
            x0 = max(int(np.floor(xs.min())), 0); x1 = min(int(np.ceil(xs.max())), W - 1)
            y0 = max(int(np.floor(ys.min())), 0); y1 = min(int(np.ceil(ys.max())), H - 1)
            if x1 < x0 or y1 < y0: continue
            X, Y = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            den = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
            if abs(den) < 1e-12: continue
            w0 = ((ys[1] - ys[2]) * (X - xs[2]) + (xs[2] - xs[1]) * (Y - ys[2])) / den
            w1 = ((ys[2] - ys[0]) * (X - xs[2]) + (xs[0] - xs[2]) * (Y - ys[2])) / den
            w2 = 1 - w0 - w1
            m = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
            if not m.any(): continue
            z = w0 * sz[i0] + w1 * sz[i1] + w2 * sz[i2]
            zb = zbuf[y0:y1 + 1, x0:x1 + 1]
            m &= z < zb
            if not m.any(): continue
            zb[m] = z[m]
            cb = col[y0:y1 + 1, x0:x1 + 1]
            cb[m] = (w0[m, None] * shade[i0] + w1[m, None] * shade[i1] + w2[m, None] * shade[i2])
    im = Image.fromarray((col * 255).astype(np.uint8))
    if path: im.save(path)
    return im

def sheet(images, path, cols=None):
    cols = cols or len(images)
    rows = (len(images) + cols - 1) // cols
    w, h = images[0].size
    out = Image.new('RGB', (w * cols, h * rows), (255, 255, 255))
    for i, im in enumerate(images): out.paste(im, ((i % cols) * w, (i // cols) * h))
    out.save(path)
    return out
