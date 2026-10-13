# Loads hand_<var>.bytes, applies the same pose as PM_Hand.Pose (grip g, trigger t) with linear blend skinning,
# renders open / half / fist so broken skin weights (a finger flying away) are visible.
import numpy as np, struct, os, sys
from render import render, sheet
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')

def load(var):
    b = open(os.path.join(OUT, 'hand_%s.bytes' % var), 'rb').read()
    magic, vc, ns, nn, jc = struct.unpack_from('<5i', b, 0); o = 20
    par, J, A = [], [], []
    for i in range(jc):
        p, *rest = struct.unpack_from('<i3f3f', b, o); o += 28
        par.append(p); J.append(rest[:3]); A.append(rest[3:])
    v = np.frombuffer(b, '<f4', vc * 3, o).reshape(-1, 3); o += vc * 12
    n = np.frombuffer(b, '<f4', vc * 3, o).reshape(-1, 3); o += vc * 12
    o += vc * 8
    I = np.frombuffer(b, '<i4', vc * 2, o).reshape(-1, 2); o += vc * 8
    W = np.frombuffer(b, '<f4', vc * 2, o).reshape(-1, 2); o += vc * 8
    skin = np.frombuffer(b, '<i4', ns, o).reshape(-1, 3); o += ns * 4
    nails = np.frombuffer(b, '<i4', nn, o).reshape(-1, 3)
    return v, n, I, W, skin, nails, par, np.array(J), np.array(A)

def axis_angle(ax, deg):
    ax = np.asarray(ax, float); ax /= np.linalg.norm(ax); t = np.radians(deg)
    K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
    return np.eye(3) + np.sin(t) * K + (1 - np.cos(t)) * K @ K

def pose(data, g, t):
    v, n, I, W, skin, nails, par, J, A = data
    jc = len(J)
    R = [np.eye(3) for _ in range(jc)]
    maxA = [62, 92, 60]
    for f in range(1, 5):
        c = t if f == 1 else g
        for j in range(3):
            bi = 1 + f * 3 + j
            R[bi] = axis_angle(A[bi], maxA[j] * c)
    R[2] = axis_angle(A[2], 18 * g); R[3] = axis_angle(A[3], 28 * g)
    WR, WT = [None] * jc, [None] * jc
    for i in range(jc):
        if par[i] < 0: WR[i] = R[i]; WT[i] = J[i]
        else:
            p = par[i]
            WR[i] = WR[p] @ R[i]; WT[i] = WT[p] + WR[p] @ (J[i] - J[p])
    out = np.zeros_like(v)
    for k in range(2):
        b = I[:, k]
        Rs = np.stack(WR)[b]; Ts = np.stack(WT)[b]
        pv = np.einsum('nij,nj->ni', Rs, v - J[b]) + Ts
        out += W[:, k:k + 1] * pv
    return out

if __name__ == '__main__':
    ims = []
    for var in sys.argv[1:]:
        d = load(var)
        for g, t in ((0.1, 0.08), (0.55, 0.5), (1.0, 1.0)):
            pv = pose(d, g, t)
            P = [(pv.astype(np.float32), d[1], d[4], (0.93, 0.74, 0.62), 0.3), (pv.astype(np.float32), d[1], d[5], (0.95, 0.85, 0.85), 0.6)]
            for yaw in (90, -60):
                ims.append(render(P, size=(260, 300), yaw=yaw, pitch=20, center=(0.01, 0.0, -0.03), height=0.26))
    sheet(ims, os.path.join(OUT, 'handpose.png'), cols=6)
