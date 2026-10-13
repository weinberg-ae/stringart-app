import numpy as np, os, sys
from handpose import load, pose, OUT
from render import render, sheet
ims = []
for var in sys.argv[1:]:
    d = load(var)
    for g, t, views in ((0.08, 0.05, (0, 180)), (0.6, 0.55, (40,)), (1.0, 1.0, (60,))):
        pv = pose(d, g, t)
        R = np.stack([pv[:, 1], pv[:, 2], -pv[:, 0]], 1).astype(np.float32)
        Nn = np.stack([d[1][:, 1], d[1][:, 2], -d[1][:, 0]], 1).astype(np.float32)
        P = [(R, Nn, d[4], (0.93, 0.74, 0.62), 0.3), (R, Nn, d[5], (0.97, 0.86, 0.86), 0.7)]
        for yaw in views:
            ims.append(render(P, size=(300, 420), yaw=yaw, pitch=5, center=(0.0, -0.04, 0.0), height=0.30))
sheet(ims, os.path.join(OUT, 'handview.png'), cols=4)
