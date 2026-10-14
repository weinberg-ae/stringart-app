import numpy as np, os, sys
from hand2 import *
from render import render, sheet
var = sys.argv[1]
d = np.load(os.path.join(OUT, 'hand2_%s.npy' % var), allow_pickle=True)[0]
ims=[]
for g in (0.4, 0.75, 1.0):
    pv, pn = posed(d, g, g, True)
    R = np.stack([pv[:, 1], pv[:, 2], -pv[:, 0]], 1).astype(np.float32)
    Nn = np.stack([pn[:, 1], pn[:, 2], -pn[:, 0]], 1).astype(np.float32)
    sk = d['f'][~d['nailf']]; nl = d['f'][d['nailf']]
    P = [(R, Nn, sk, (0.86, 0.68, 0.56), 0.2), (R, Nn, nl, (0.95, 0.8, 0.8), 0.6)]
    for yaw, pitch in ((200, -55), (150, 15), (-30, 20), (90, 10)):
        ims.append(render(P, size=(300, 300), yaw=yaw, pitch=pitch, center=(0.0, -0.01, 0.0), height=0.17, bg=(140, 140, 140)))
sheet(ims, os.path.join(OUT, 'h2grip.png'), cols=4)
