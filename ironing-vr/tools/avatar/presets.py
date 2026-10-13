# Renders the ready-made characters with the same palettes as PM_Toon.cs
import sys, os, numpy as np
import toon
from toon import *
from render import render, sheet
HAIR = ["buzz", "crop", "quiff", "curly", "bob", "long", "ponytail", "spacebuns", "afro"]
TOPS = ["tee", "hoodie", "denim", "crop", "puffer", "bomber"]
BOTS = ["jeans", "cargo", "skirt", "shorts", "joggers"]
HATS = [None, "cap", "beanie", "bucket"]
ACCS = ["glasses", "sunglasses", "earrings", "necklace", "headphones", "apron"]
SHOES = ["shoes_sneaker", "shoes_boot", "shoes_classic"]
SKINS = [(0.98,0.84,0.74),(0.93,0.74,0.60),(0.82,0.60,0.45),(0.66,0.45,0.31),(0.47,0.31,0.21),(0.31,0.20,0.14)]
HC = [(0.07,0.055,0.05),(0.30,0.17,0.08),(0.86,0.68,0.36),(0.72,0.24,0.08),(0.96,0.45,0.70),(0.30,0.55,0.98),(0.62,0.40,0.95),(0.88,0.88,0.92)]
EYES = [(0.22,0.52,0.92),(0.25,0.62,0.32),(0.42,0.25,0.12),(0.62,0.48,0.22),(0.55,0.35,0.85)]
TC = [[(0.97,0.97,0.97),(0.12,0.12,0.14),(0.10,0.85,0.90)],[(0.10,0.10,0.12),(0.95,0.95,0.95),(0.98,0.36,0.58)],[(0.98,0.52,0.68),(0.98,0.95,0.92),(0.98,0.86,0.30)],
      [(0.66,0.56,0.96),(0.97,0.97,0.99),(0.30,0.92,0.86)],[(0.08,0.66,0.70),(0.95,0.95,0.95),(0.98,0.62,0.20)],[(0.98,0.55,0.18),(0.15,0.15,0.17),(0.95,0.95,0.95)],
      [(0.36,0.45,0.26),(0.90,0.86,0.74),(0.98,0.80,0.30)],[(0.80,0.12,0.18),(0.97,0.97,0.97),(0.98,0.86,0.30)]]
BC = [(0.25,0.36,0.62),(0.10,0.10,0.12),(0.80,0.72,0.56),(0.38,0.42,0.30),(0.95,0.95,0.95),(0.72,0.62,0.92)]
# name, woman, hair, hairColor, hat, hatColor, top, topColor, bottom, bottomColor, skin, eyes, accs, shoes
PRESETS = [("noya", 1, 5, 0, 0, 1, 3, 2, 2, 1, 1, 2, (1 << 2) | (1 << 3), 0),
           ("shira", 1, 7, 4, 0, 1, 1, 3, 4, 4, 0, 0, 1 << 4, 0),
           ("maya", 1, 8, 0, 0, 1, 2, 0, 1, 2, 4, 2, (1 << 1) | (1 << 2), 1),
           ("itay", 0, 2, 1, 0, 1, 5, 1, 0, 0, 2, 2, 1 << 3, 0),
           ("daniel", 0, 1, 0, 1, 4, 1, 6, 1, 2, 3, 2, 1 << 0, 0),
           ("omer", 0, 0, 2, 2, 1, 4, 5, 4, 1, 1, 1, 1 << 4, 1)]
SHOE_COLS = [[(0.95,0.95,0.96),(0.85,0.86,0.88),(0.1,0.95,1.0)],[(0.45,0.27,0.14),(0.12,0.1,0.09),(0.78,0.62,0.4)],[(0.07,0.07,0.08),(0.22,0.13,0.07),(0.8,0.8,0.82)]]

def load(name):
    import struct as st
    d = OUT if name.startswith('toon') else os.path.join(os.path.dirname(OUT), 'out')
    b = open(os.path.join(d, name + '.bytes'), 'rb').read()
    magic, vc, ns = st.unpack_from('<3i', b, 0); o = 12 + 16
    counts = st.unpack_from('<%di' % ns, b, o); o += 4 * ns
    v = np.frombuffer(b, '<f4', vc * 3, o).reshape(-1, 3).copy(); o += vc * 12
    n = np.frombuffer(b, '<f4', vc * 3, o).reshape(-1, 3).copy(); o += vc * 12 + vc * 8
    subs = []
    for c in counts: subs.append(np.frombuffer(b, '<i4', c, o)); o += c * 4
    while len(subs) < 6: subs.append(np.zeros(0, int))
    return v, n, subs

def figure(pr):
    name, w, hair, hcol, hat, hatc, top, tcol, bot, bcol, skin, eyes, accs, shoes = pr
    g = 'f' if w else 'm'; k = 0.97 if w else 1.0
    sk = SKINS[skin]
    P = []
    def add(m, cols, kind, gloss=None, hs=False):
        v, n, s = m
        if hs: v = (HEAD_C + (v - HEAD_C) * k)
        v = cartoon(v, kind)
        P.extend(parts_for_render((v, n, s), cols, gloss))
    lip = tuple(np.array(sk) * (0.25 if w else 0.55) + np.array([0.78, 0.22, 0.32]) * (0.75 if w else 0.45))
    add(load('toon_head_' + g), [sk, EYES[eyes], lip, (1, 1, 1), (0.03, 0.03, 0.04), HC[hcol]], 'h', {1: 0.9, 3: 0.9, 4: 0.9})
    add(load('toon_hair_' + HAIR[hair]), [HC[hcol], (0.95, 0.35, 0.45)] + [(1, 1, 1)] * 4, 'h', {0: 0.5}, True)
    fits = [True, True, False, False, True, True, True, False, False][hair]
    if hat and fits: add(load('toon_hat_' + HATS[hat]), [TC[hatc][0], TC[hatc][0], TC[hatc][1], (0.96, 0.78, 0.36), (0.06, 0.06, 0.07), (1, 1, 1)], 'h', None, True)
    gold = (0.96, 0.78, 0.36); black = (0.06, 0.06, 0.07)
    for i, a in enumerate(ACCS):
        if not accs & (1 << i): continue
        nm = 'toon_acc_' + a + ('_' + g if a == 'apron' else '')
        cols = {'glasses': [black, black, (0.08, 0.08, 0.1), gold, black, black], 'sunglasses': [black, black, (0.08, 0.08, 0.1), gold, black, black],
                'earrings': [gold] * 6, 'necklace': [gold] * 6, 'headphones': [black, (0.95, 0.35, 0.45), (0.95, 0.35, 0.45), gold, black, black],
                'apron': [sk, (0.07, 0.24, 0.29), TC[tcol][1], gold, black, black]}[a]
        add(load(nm), cols, 'h' if a in ('glasses', 'sunglasses', 'earrings') else 'b', {3: 0.9, 2: 0.9}, a in ('glasses', 'sunglasses', 'earrings'))
    tc = TC[tcol]
    t1 = tc[0] if TOPS[top] != 'denim' else tuple(np.array(tc[0]) * 0.35 + np.array([0.35, 0.48, 0.72]) * 0.65)
    add(load('toon_top_%s_%s' % (TOPS[top], g)), [sk, t1, tc[1], tc[2], (0.1, 0.09, 0.08), (1, 1, 1)], 'b', {1: 0.6 if TOPS[top] == 'puffer' else 0.3})
    b3 = (0.97, 0.97, 0.97) if BOTS[bot] == 'joggers' else (0.85, 0.70, 0.32)
    b2 = tuple(np.array(BC[bcol]) * 0.85) if BOTS[bot] == 'joggers' else (0.97, 0.97, 0.97)
    add(load('toon_bottom_%s_%s' % (BOTS[bot], g)), [sk, BC[bcol], b2, b3, (0.12, 0.10, 0.09), (1, 1, 1)], 'b')
    add(load(SHOES[shoes]), SHOE_COLS[shoes] + [(1, 1, 1)] * 3, 's')
    return P

if __name__ == '__main__':
    idx = [int(a) for a in sys.argv[1:]] or range(len(PRESETS))
    for i in idx:
        P = figure(PRESETS[i])
        ims = [render(P, size=(330, 700), yaw=y, pitch=4, center=(0, 0.88, 0), height=1.85, bg=(232, 236, 244)) for y in (18, 200)]
        sheet(ims, os.path.join(OUT, 'preset_%d.png' % i))
