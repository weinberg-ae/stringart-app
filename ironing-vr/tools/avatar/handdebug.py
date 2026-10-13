import numpy as np, os, sys
from handpose import load, pose, OUT
from render import render, sheet
d = load(sys.argv[1])
v, n, I, W, skin, nails, par, J, A = d
pal = np.array([[0.8,0.8,0.8],[1,0,0],[1,0.5,0],[1,1,0],[0,1,0],[0,0.6,0],[0,0.3,0],[0,1,1],[0,0.6,0.6],[0,0.3,0.3],[0,0,1],[0,0,0.6],[0,0,0.3],[1,0,1],[0.6,0,0.6],[0.3,0,0.3]])
dom = np.where(W[:,0] >= W[:,1], I[:,0], I[:,1])
col = pal[dom % len(pal)]
ims = []
for g in (0.0, 0.6, 1.0):
    pv = pose(d, g, g)
    R = np.stack([pv[:, 1], pv[:, 2], -pv[:, 0]], 1).astype(np.float32)
    Nn = np.stack([n[:, 1], n[:, 2], -n[:, 0]], 1).astype(np.float32)
    P = [(R, Nn, np.concatenate([skin, nails]), col, 0.2)]
    for yaw in (0, 60):
        ims.append(render(P, size=(360, 420), yaw=yaw, pitch=10, center=(0.0, -0.02, 0.0), height=0.24))
sheet(ims, os.path.join(OUT, 'handdebug.png'), cols=6)
# boundary edges (holes)
f = np.concatenate([skin, nails])
e = np.sort(np.concatenate([f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]]]), 1)
u, c = np.unique(e, axis=0, return_counts=True)
print('open edges', (c == 1).sum())
