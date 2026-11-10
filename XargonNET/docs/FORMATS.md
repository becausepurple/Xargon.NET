# Xargon on-disk formats (ground truth for the .NET port)

Derived from the original Turbo C source in `vanilla-xargon/` (file names lowercase, e.g.
`source/shapeman.c`, `source/graphics.c`; cited line numbers are unchanged). The original is **16-bit
large-model Turbo C**: `int` = 2 bytes, `long` = 4 bytes, far pointer = 4 bytes. All
multi-byte values are **little-endian**. Data files use the extension `.XR1`/`.XR2`/`.XR3`
for episodes 1/2/3 (and `.XR0` for shared/demo data).

## Video
- VGA mode 13h: 320x200, 256 colours, 1 byte/pixel indexed.
- Palette is 6-bit VGA DAC (components 0..63); scale `*255/63` for 8-bit display.
- Colour index **0 is transparent** when drawing shapes (`initcolortabs_vga`, GRAPHICS.C).

## Graphics container — `GRAPHICS.XR*` (SHAPEMAN.C)
- Header index (768 bytes): `128 x uint32` table offsets, then `128 x uint16` table lengths.
- A table is present iff its length != 0. Offsets are absolute file positions.

### Shape table (`xlate_table`)
Header: `numShapes u8, numRots u16, lenCGA u16, lenEGA u16, lenVGA u16, numColorBits u8,
flags u16`. For `numColorBits == 8` (the shipped VGA data) there is no inline palette.
Then, per shape: `xs u8, ys u8, storeType u8`, followed by `xs*ys` bytes of colour indices
when `storeType == 0` (st_byte — the only type present in shipped data).
Flags: `shm_fontf = 1`, `shm_blflag = 4`.

### Palette shape
The 256-colour palette is embedded as the shape with `numColorBits == 8 && xs == 64 &&
ys == 12` (768 bytes = 256 * 3 DAC triplets). Fallback: the hardcoded `vgapal` table in
GRAPHICS.C (a valid default palette).

### drawshape(n)
`table = (n >> 8) & 0x3f`, `shape = n & 0xff`. High byte bit `0x40` selects the "underlay"
colour-map table.

## Boards / levels — `BOARD_*`, `INTRO`, `MAP`, `STORY` (XARGON.C `loadboard`)
Raw, uncompressed, in this exact order:
1. `bd[128][64]` as `int16` — 16384 bytes. Stored column-major in C (`bd[x][y]`), so the
   file order is: for x in 0..127, for y in 0..63. Cell value: `& 0x3fff` = info index;
   the top bits are runtime "modified" flags and are not meaningful on disk.
2. `numobjs` : `int16`.
3. `objs[numobjs]` : each `objtype` is **31 bytes**, byte-packed:
   `objkind u8`, then in order `x, y, xd, yd, xl, yl, state, substate, statecount, counter,
   objflags` (11 x `int16`), the `inside` far pointer `uint32` (garbage on disk; only its
   non-zeroness matters), then `info1, zaphold` (2 x `int16`). 1 + 26 + 4 = 31 bytes.
4. `pl` : `pltype` = **96 bytes**: `level, health, emeralds, numinv` (4 x int16),
   `inv[30]` (int16), `ouched` (int16), `pad[22]`, `score` (`long` = uint32).
5. `cnt_fruit` : `u8`.
6. For each object whose on-disk `inside` pointer was non-zero, in object order:
   `len int16`, then `len + 1` bytes of text (NUL-terminated).

**Correctness assertion:** a correct parse consumes the file to *exactly* EOF.

## Tile / info table — `TILES.XR*` (X_INFO.C `init_info`)
Builds `info[900]`. Defaults for every entry: `sh = 0x4500`,
`flags = f_notvine | f_notstair | f_notwater`, name = "". Then records until EOF:
`index int16, sh int16, flagsXor int16, len u8, name[len]` →
`info[index].sh = sh; info[index].flags ^= flagsXor; info[index].name = name`.

### Rendering a board cell
`sh = info[cell & 0x3fff].sh` → `drawshape(sh)`. If `info[cell].flags & f_msgdraw`, the cell
is drawn by a block message handler instead (animated tiles).

## Object kinds — `X_OBJ.DEF`
`defobj(kind, fn, msgfn, xl, yl, name, flags, table, score)`. The `table` field is the shape
table used to draw that kind (e.g. player -> 6, emerald -> 30). Behaviour dispatched via
`kindmsg[kind]`.

## Flags (XARGON.H)
`f_playerthru=1, f_notstair=2, f_notvine=4, f_msgtouch=8, f_msgdraw=16, f_msgupdate=32,
f_inside=64, f_front=128, f_trigger=256, f_back=512, f_always=1024, f_killable=2048,
f_fireball=4096, f_water=8192, f_notwater/f_weapon=16384`.
A block is solid unless `f_playerthru` is set.

## Audio / text (AUDIO.XR*)
Little-endian header, absolute offsets (MUSIC.C):
| Offset | Field |
|---|---|
| 0 | `long vocposn[50]` (200 B) |
| 200 | `int16 voclen[50]` |
| 300 | `int16 vocrate[50]` |
| 400 | `long textposn[40]` (absolute file offsets) |
| 560 | `int16 textlen[40]` (0 = message absent) |
| 640 | end of header; sound data follows |

- Text message `n` (0..39) is `textlen[n]` raw bytes at `textposn[n]`, not NUL-terminated. Lines end
  with `\r\n`; the first character of a line is a colour digit `'0'..'7'` (line 0 is the title); the
  message ends with a final `\r\n` (possibly followed by padding). Parsing counts CRs and is bounded by the length.
- Sound effects are VOC PCM (index via `vocposn/voclen/vocrate`); playback is a later phase.
- `SONG_*.XR*`: CMF / MIDI OPL2 music, played through the WORX Adlib driver.
