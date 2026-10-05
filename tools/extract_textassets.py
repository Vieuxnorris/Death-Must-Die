"""Extract named TextAssets from a Unity .assets file without dependencies.
TextAsset layout (Unity 2021): m_Name (int32 len + bytes, 4-aligned), m_Script (int32 len + bytes)."""
import struct, sys, pathlib

src = pathlib.Path(sys.argv[1]); out = pathlib.Path(sys.argv[2]); names = sys.argv[3:]
data = src.read_bytes(); out.mkdir(parents=True, exist_ok=True)
for name in names:
    nb = name.encode()
    pat = struct.pack('<i', len(nb)) + nb
    pos = data.find(pat)
    if pos < 0:
        print('missing', name); continue
    p = pos + 4 + len(nb); p = (p + 3) & ~3
    (ln,) = struct.unpack_from('<i', data, p)
    body = data[p + 4:p + 4 + ln]
    (out / f'{name}.csv').write_bytes(body)
    print(f'{name}: {ln} bytes, {body.count(b"\n")} lines')
