# Converts toon_out/*.bytes (v2: positions, normals, uv, int32 indices) into the compact v3 format:
# positions only (normals and box UVs are rebuilt in Unity) and 16-bit indices when possible.
import glob, os, struct, sys, numpy as np
src = sys.argv[1]; dst = sys.argv[2]
os.makedirs(dst, exist_ok=True)
tot = 0
for f in sorted(glob.glob(os.path.join(src, 'toon_*.bytes')) + glob.glob(os.path.join(src, 'tool_*.bytes'))):
    b = open(f, 'rb').read()
    magic, vc, ns = struct.unpack_from('<3i', b, 0); o = 12
    extra = struct.unpack_from('<4f', b, o); o += 16
    counts = struct.unpack_from('<%di' % ns, b, o); o += 4 * ns
    v = np.frombuffer(b, '<f4', vc * 3, o); o += vc * 12 * 2 + vc * 8
    idx = [np.frombuffer(b, '<i4', c, o + 4 * sum(counts[:i])) for i, c in enumerate(counts)]
    small = vc < 65536
    with open(os.path.join(dst, os.path.basename(f)), 'wb') as fh:
        fh.write(struct.pack('<3i', 0x504D4D33, vc, ns))
        fh.write(struct.pack('<4f', *extra))
        fh.write(struct.pack('<%di' % ns, *counts))
        fh.write(struct.pack('<i', 1 if small else 0))
        fh.write(v.astype('<f4').tobytes())
        for i in idx: fh.write(i.astype('<u2' if small else '<i4').tobytes())
    tot += os.path.getsize(os.path.join(dst, os.path.basename(f)))
print('total MB', tot / 1e6)
