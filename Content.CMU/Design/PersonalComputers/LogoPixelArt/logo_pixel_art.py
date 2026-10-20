"""Draws the boot splash logos for the personal computer operating systems.

Run from the repo root: python Content.CMU/Design/PersonalComputers/LogoPixelArt/logo_pixel_art.py [--preview <dir>]
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Logos/<name>.png and Icons/<os id>_<program>.png at
1x, and both folders' attributions.yml.
The splash upscales them by whole numbers, so every shape is drawn on the pixel grid without antialiasing.
All small text uses the colony ATM generator's 3x5 font, and the Weyland-Yutani logo is the ATM's own boot
mark (Design/ColonyEconomy/AtmPixelArt), so the company looks the same on every screen it boots.

The faction logos (FACTION_LOGOS) set a headline in CAPS, a 5x7 alphabet drawn at two pixels a stroke (BIG), beside
an emblem. Where the game has the emblem as a sprite - a uniform patch, an ID badge, a medal, a floor tile, a
letterhead - its pixels are lifted out of the RSI (SiteArt/rsi_sprites.py) and composited at 1x or a whole multiple,
recoloured by lightness at most; LOGO_SOURCES credits each one in Logos/attributions.yml. Only what the game lacks
(the TWE crown, the IASF wings and roundel, the security shield, the rangers' ring) is drawn here.

Program icons are one drawing per program recoloured with each OS's palette (ICON_PALETTES), except the CLF's,
which are their own crude, taped-up drawings (CLF_ICONS): hacked colony machines.
"""
import importlib.util
import os
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Logos")
ATM_ART = os.path.join(REPO, "Content.CMU", "Design", "ColonyEconomy", "AtmPixelArt", "atm_pixel_art.py")

# Seegson's rewire mark: slab letters on a 5x5 grid, rows alternating thick bars and thin joins.
SLAB = {
    "S": ["#####", "#....", "#####", "....#", "#####"],
    "E": ["#####", "#....", "####.", "#....", "#####"],
    "G": ["#####", "#....", "#..##", "#...#", "#####"],
    # Solid with a punched slot, as on the Seegson boot screen.
    "O": ["#####", "#####", "##..#", "#####", "#####"],
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#"],
}
SLAB_ROWS = (3, 2, 3, 2, 3)
SLAB_COL = 3

# Sevastolink's wordmark: 5x7 capitals drawn with two pixel strokes.
WIDE = {
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "V": ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    "K": ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
}


def load_atm_art():
    spec = importlib.util.spec_from_file_location("atm_pixel_art", ATM_ART)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ATM = load_atm_art()
RSI_SPRITES = os.path.join(REPO, "Content.CMU", "Design", "PersonalComputers", "SiteArt", "rsi_sprites.py")


def italic(layer, height, slope):
    """Leans a layer right: row y moves (height - 1 - y) // slope pixels, so the base stays put."""
    leaned = ATM.Layer()
    for (x, y), c in layer.px.items():
        leaned.set(x + (height - 1 - y) // slope, y, c)
    return leaned


def ramp_colour(ramp, y, height):
    return ramp[min(len(ramp) - 1, y * len(ramp) // height)]


def weyland():
    """The Weyland-Yutani mark from the colony ATM's boot screen, in the corporate terminal's amber,
    laid out as the ATM's boot_logo holds it."""
    hot, main, dim = "#ffe2a0", "#ffb547", "#a8681c"
    mark = ATM.Layer()
    width = ATM.wy_logo(mark, 0, 0, h=13, w=8, m=3, gold=main, grey=dim)
    name, motto = "WEYLAND-YUTANI", "BUILDING BETTER WORLDS"
    canvas = max(width, ATM.text_width(ATM.F3, motto)) + 2
    ox = (canvas - width) // 2

    art = ATM.Layer()
    for (px, py), c in mark.px.items():
        art.set(ox + px, py, hot if c == ATM.rgba(ATM.C["gold_hi"]) else c)
    art.glyphs(ATM.F3, ATM.centred(ATM.F3, name, canvas // 2), 19, name, main)
    art.glyphs(ATM.F3, ATM.centred(ATM.F3, motto, canvas // 2), 27, motto, dim)
    return art.image((0, 0, canvas, 32))


def seegson():
    """SEEGSON in leaning yellow slab letters over REWIRE SYSTEM BOOT: the screen a Seegson
    terminal shows while its system comes up."""
    ramp = ("#fff7a8", "#f4e76e", "#f4e76e", "#e2d35a", "#c9bc4a")
    word, gap = "SEEGSON", 3
    height = sum(SLAB_ROWS)
    letters = ATM.Layer()
    x = 0
    for ch in word:
        glyph = SLAB[ch]
        y = 0
        for row, bits in enumerate(glyph):
            for col, bit in enumerate(bits):
                if bit == "#":
                    for dy in range(SLAB_ROWS[row]):
                        colour = ramp_colour(ramp, y + dy, height)
                        letters.rect(x + col * SLAB_COL, y + dy, x + col * SLAB_COL + SLAB_COL - 1, y + dy, colour)
            y += SLAB_ROWS[row]
        x += len(glyph[0]) * SLAB_COL + gap
    word_width = x - gap
    lean = 4
    art = italic(letters, height, lean)
    canvas = word_width + height // lean + 2

    tag = "REWIRE SYSTEM BOOT"
    art.glyphs(ATM.F3, ATM.centred(ATM.F3, tag, canvas // 2), height + 6, tag, "#e2d35a")
    return art.image((0, 0, canvas, height + 11))


def sevastolink():
    """SEVASTOLINK, Seegson's operating system: a leaning green wordmark with scan gaps through its
    strokes and AN LM-LINK PRODUCT under its tail. The splash draws the progress bar beneath it."""
    main, scan = "#3fe05a", "#1f8a33"
    word, gap = "SEVASTOLINK", 2
    height = 7 * 2
    letters = ATM.Layer()
    x = 0
    for ch in word:
        glyph = WIDE[ch]
        for row, bits in enumerate(glyph):
            for col, bit in enumerate(bits):
                if bit != "#":
                    continue
                for dy in range(2):
                    y = row * 2 + dy
                    # Every fourth row runs dim, like the raster lines through the original lettering.
                    letters.rect(x + col * 2, y, x + col * 2 + 1, y, scan if y % 4 == 2 else main)
        x += len(glyph[0]) * 2 + gap
    word_width = x - gap
    lean = 3
    art = italic(letters, height, lean)
    canvas = word_width + height // lean + 2

    tag = "AN LM-LINK PRODUCT"
    art.glyphs(ATM.F3, canvas - ATM.text_width(ATM.F3, tag) - 2, height + 3, tag, main)
    return art.image((0, 0, canvas, height + 8))


# ── Faction logos ─────────────────────────────────────────────────────────────────────────────────────────────────
# A 5x7 capital alphabet. Headlines draw it through BIG: two pixels a stroke, rows two tall but the middle one, so
# bars through the middle of E, H, A or S are a pixel thinner than the rest, like the Seegson slab.
CAPS = {
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "B": ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
    "C": [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "F": ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
    "G": [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###."],
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "K": ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "M": ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".###.", "#...#", "#....", ".###.", "....#", "#...#", ".###."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    " ": ["..", "..", "..", "..", "..", "..", ".."],
}
BIG = dict(cols=2, rows=(2, 2, 2, 1, 2, 2, 2))
# The CLF's spray-painted capitals and Prodigy's tall ones: the same letters, drawn bigger.
HUGE = dict(cols=3, rows=(3, 3, 3, 2, 3, 3, 3))
TALL = dict(cols=2, rows=(3, 3, 3, 1, 3, 3, 3))


class Canvas(ATM.Layer):
    """The ATM's layer without its bounds, cropped to what is drawn when saved."""

    def set(self, x, y, c):
        self.px[(x, y)] = ATM.rgba(c)

    def fill(self, pixels, c):
        for x, y in pixels:
            self.set(x, y, c)

    def put(self, img, x0, y0, colour=None):
        """Lays a sprite's opaque pixels down at (x0, y0), through `colour` if given."""
        for y in range(img.height):
            for x in range(img.width):
                p = img.getpixel((x, y))
                if p[3] >= 128:
                    self.set(x0 + x, y0 + y, colour(p) if colour else p[:3] + (255,))

    def size(self):
        xs, ys = [x for x, _ in self.px], [y for _, y in self.px]
        return min(xs), min(ys), max(xs) + 1, max(ys) + 1

    def crop(self):
        x0, y0, x1, y1 = self.size()
        from PIL import Image

        img = Image.new("RGBA", (x1 - x0, y1 - y0), (0, 0, 0, 0))
        for (x, y), c in self.px.items():
            img.putpixel((x - x0, y - y0), c)
        return img


def lettering(text, scale=None, gap=2):
    """The pixels of `text` in CAPS at `scale` (BIG, HUGE or TALL), with each letter's left edge and width."""
    cols, rows = (scale or dict(cols=1, rows=(1,) * 7)).values()
    pixels, letters = set(), []
    x = 0
    for ch in text:
        glyph = CAPS[ch]
        letters.append((x, len(glyph[0]) * cols))
        y = 0
        for r, bits in enumerate(glyph):
            for c, bit in enumerate(bits):
                if bit == "#":
                    for dy in range(rows[r]):
                        for dx in range(cols):
                            pixels.add((x + c * cols + dx, y + dy))
            y += rows[r]
        x += len(glyph[0]) * cols + gap
    return pixels, letters, x - gap, sum(rows)


def moved(pixels, dx, dy):
    return {(x + dx, y + dy) for x, y in pixels}


def ring(pixels, diagonal=True):
    """The pixels just outside a shape."""
    steps = [(dx, dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if (dx or dy) and (diagonal or not (dx and dy))]
    return {(x + dx, y + dy) for x, y in pixels for dx, dy in steps} - pixels


def leaned(pixels, height, slope):
    """Leans a shape right: row y moves (height - 1 - y) // slope pixels, so the base stays put."""
    return {(x + (height - 1 - y) // slope, y) for x, y in pixels}


def rounded(pixels):
    """Takes the outer corner pixel off every bar at least three pixels long both ways, so the letters read as
    machined rather than stamped. Stems two pixels wide keep their square ends."""
    out = set(pixels)
    for x, y in pixels:
        for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            if (x - sx, y) in pixels or (x, y - sy) in pixels:
                continue
            if all((x + sx * i, y) in pixels and (x, y + sy * i) in pixels for i in (1, 2)):
                out.discard((x, y))
    return out


def stencilled(pixels, letters):
    """Cuts the bridge a stencil needs through every bar that crosses a letter's middle two columns."""
    out = set(pixels)
    for x0, w in letters:
        mid = (x0 + w // 2 - 1, x0 + w // 2)
        for x, y in pixels:
            if x in mid and (mid[0] - 1, y) in pixels and (mid[1] + 1, y) in pixels:
                out.discard((x, y))
    return out


def by_row(L, pixels, ramp):
    """Paints a shape top to bottom through `ramp`."""
    top = min(y for _, y in pixels)
    height = max(y for _, y in pixels) - top + 1
    for x, y in pixels:
        L.set(x, y, ramp_colour(ramp, y - top, height))


def headline(L, pixels, x, y, ramp, edge=None, shadow=None):
    """A headline at (x, y): an optional dark edge all round, an optional drop shadow, then the letters."""
    pixels = moved(pixels, x, y)
    if shadow:
        L.fill(moved(pixels, 1, 1) - pixels, shadow)
    if edge:
        L.fill(ring(pixels | (moved(pixels, 1, 1) if shadow else set())), edge)
    by_row(L, pixels, ramp)
    return pixels


def spaced(L, text, x, y, width, colour):
    """A line of the 3x5 font tracked out to `width`: the spare pixels shared between the gaps, wider ones first."""
    font = ATM.F3
    bare = ATM.text_width(font, text, gap=0)
    gaps = len(text) - 1
    spare = max(gaps, width - bare)
    for i, ch in enumerate(text):
        L.glyphs(font, x, y, ch, colour)
        x += len(font.get(ch, font[" "])[0]) + spare // gaps + (1 if i < spare % gaps else 0)


def ruled(L, text, x, y, width, colour, rule):
    """A line of the 3x5 font tracked a pixel wider than usual and centred in `width`, with a hairline out to
    either side: the small line a badge's name sits under."""
    tw = ATM.text_width(ATM.F3, text, gap=2)
    tx = x + (width - tw) // 2
    L.glyphs(ATM.F3, tx, y, text, colour, gap=2)
    if tx - x >= 6:
        L.rect(x, y + 2, tx - 4, y + 2, rule)
        L.rect(tx + tw + 3, y + 2, x + width - 1, y + 2, rule)


def disc(L, cx, cy, r, colour):
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                L.set(x, y, colour)


_rsi = None


def sprite(rsi, state="icon"):
    """A game sprite's frame, trimmed to its pixels (SiteArt/rsi_sprites.py finds the RSI in either texture root)."""
    global _rsi
    if _rsi is None:
        spec = importlib.util.spec_from_file_location("rsi_sprites", RSI_SPRITES)
        _rsi = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(_rsi)
    img = _rsi.frame(rsi, state)
    return img.crop(img.getbbox())


def keep(img, test):
    """A copy of a sprite with only the pixels that pass `test`: lifts a mark off the plate or floor it is on."""
    out = img.copy()
    for y in range(img.height):
        for x in range(img.width):
            if not test(img.getpixel((x, y))):
                out.putpixel((x, y), (0, 0, 0, 0))
    return out


def tinted(ramp, lo=0, hi=255):
    """Recolours a sprite pixel by its lightness onto `ramp`, the way the PC casings are recoloured."""
    def colour(p):
        v = (0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2] - lo) / max(1, hi - lo)
        return ramp[max(0, min(len(ramp) - 1, int(v * len(ramp))))]
    return colour


# Every game sprite a logo is built round, with the RSI's licence and copyright, for attributions.yml.
LOGO_SOURCES = {
    "upp": ("CMU14/Clothing/Patches/cec_patch.rsi", "icon"),
    "uscm": ("CMU14/Clothing/Patches/uscm_patch.rsi", "icon"),
    "twe": ("CMU14/Clothing/Patches/twe_patch.rsi", "icon"),
    "hyperdyne": ("CMU14/Clothing/Patches/hyperdyne_patch.rsi", "icon"),
    "lasalle": ("_RMC14/Objects/Misc/paper.rsi", "laselle_letterhead"),
    "clf": ("_RMC14/Objects/Clothing/Accessory/Patches/CLF/clf_patch.rsi", "icon"),
    "rangers": ("_RMC14/Objects/Medals/gold_star.rsi", "icon"),
    "nspa": ("_RMC14/Tiles/hybrisa/NSPAemblem.png", None),
    "cmb": ("_RMC14/Objects/CMIDs/cmb_marshal.rsi", "icon"),
}
# Where an RSI's copyright line credits far more than the one state used, the part that covers that state.
SOURCE_CREDITS = {
    "lasalle": "Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/"
               "9ab207cd7ffba86a0411d7058645fb8f2a7895f3/icons/obj/items/paper.dmi",
    "nspa": "RMC14, from the CM-SS13 Hybrisa tiles",
}


def upp():
    """The UPP's lockup, a Soviet poster's: the CEC patch's gold star on red between two gold rules, a leaning red
    UPP beside it, and the Union's name tracked out underneath."""
    L = Canvas()
    patch = sprite(*LOGO_SOURCES["upp"])
    word, _, ww, wh = lettering("UPP", BIG)
    word = leaned(word, wh, 4)
    ww += (wh - 1) // 4
    sub = "UNION OF PROGRESSIVE PEOPLES"
    width = ATM.text_width(ATM.F3, sub)
    group = patch.width + 5 + ww
    gx = (width - group) // 2
    L.put(patch, gx, 0)
    headline(L, word, gx + patch.width + 5, 3, ("#ff7a5c", "#f2412f", "#d92a21", "#b31b16", "#8c1410"),
             edge="#2a0705")
    for x0, x1 in ((0, gx - 4), (gx + group + 3, width - 1)):
        L.rect(x0, 7, x1, 7, "#f0c050")
        L.rect(x0, 9, x1, 10, "#c8282a")
    L.glyphs(ATM.F3, 0, 21, sub, "#e8b84a")
    return L.crop()


def uscm():
    """The Colonial Marines: their patch's eagle roundel, USCM in stencilled steel and the corps' name below in the
    olive of a stencilled crate."""
    L = Canvas()
    patch = sprite(*LOGO_SOURCES["uscm"])
    word, letters, ww, wh = lettering("USCM", BIG)
    word = stencilled(word, letters)
    sub = "UNITED STATES COLONIAL MARINES"
    width = ATM.text_width(ATM.F3, sub)
    group = patch.width + 6 + ww
    gx = (width - group) // 2
    L.put(patch, gx, 0)
    headline(L, word, gx + patch.width + 6, 3, ("#ffffff", "#e3e9ee", "#c3ccd5", "#9aa7b3", "#7a8794"),
             edge="#0a121c", shadow="#24364a")
    # Stencil ticks either side, as on a marine's kit.
    for x0 in (gx - 9, gx + group + 3):
        for i in range(3):
            L.rect(x0 + i * 2, 8, x0 + i * 2, 11, "#8a9a6a")
    spaced(L, sub, 0, 21, width, "#a9b98a")
    return L.crop()


CROWN = [
    ".....a.....",
    "....aba....",
    "a....b....a",
    "ba..aba..ab",
    "bbaabbbaabb",
    "bcbbdbdbbcb",
    "eeeeeeeeeee",
]
CROWN_COLOURS = {"a": "#fff0a8", "b": "#e2b33c", "c": "#d8263a", "d": "#3a6ad8", "e": "#9a6a14"}


def twe():
    """The Three World Empire's crest: the TWE patch's roundel as the globe under a gold crown with its jewels, and
    TWE in gold beside it over the Empire's name."""
    L = Canvas()
    patch = sprite(*LOGO_SOURCES["twe"])
    cw = len(CROWN[0])
    cx = (patch.width - cw) // 2
    L.put(patch, 0, 5)
    for y, row in enumerate(CROWN):
        for x, ch in enumerate(row):
            if ch != ".":
                L.set(cx + x, y, CROWN_COLOURS[ch])
    word, _, ww, wh = lettering("TWE", BIG, gap=3)
    sub = "THREE WORLD EMPIRE"
    tx = patch.width + 6
    headline(L, word, tx, 3, ("#fff4c0", "#f6d36a", "#e2b33c", "#c8962a", "#9a6a14"), edge="#1a0c06",
             shadow="#5a1a24")
    sw = ATM.text_width(ATM.F3, sub)
    L.rect(tx, 19, tx + sw - 1, 19, "#8a2a36")
    L.glyphs(ATM.F3, tx, 21, sub, "#e2b33c")
    return L.crop()


def roundel(L, cx, cy):
    """The Imperial roundel: navy, white and red rings with a dark rim, centred on (cx, cy)."""
    for r, c in ((6.6, "#0a1426"), (5.8, "#24468e"), (4.1, "#f2f4f6"), (2.3, "#d0202c")):
        disc(L, cx, cy, r, c)
    L.set(int(cx) - 2, int(cy) - 3, "#ffffff")


def wing(L, x0, y0, side):
    """A pilot's wing out of the roundel: five feathered rows, longest on top, curling up at the tip."""
    rows = (24, 22, 19, 15, 11, 7)
    ramp = ("#ffffff", "#e2e7ec", "#c4ccd4", "#a3adb8", "#86919e", "#6a7582")
    for r, n in enumerate(rows):
        for d in range(n):
            y = y0 + r - (1 if d >= n - 3 and r < 3 else 0) - (1 if d >= n - 1 and r == 0 else 0)
            c = ramp[r]
            # The feather's quill: a darker pixel every fourth step along the lower rows.
            if r and (d + r) % 4 == 0:
                c = "#4c5764"
            L.set(x0 + side * d, y, c)
    for r, n in enumerate(rows):
        L.set(x0 + side * n, y0 + r - (1 if r < 3 else 0), "#1a222c")


def iasf():
    """The IASF, the Empire's space force: the roundel on a pair of silver pilot's wings over IASF in white, and
    the service's name in the roundel's blue."""
    L = Canvas()
    sub = "IMPERIAL ARMED SPACE FORCE"
    width = ATM.text_width(ATM.F3, sub)
    cx = width // 2
    wing(L, cx - 7, 3, -1)
    wing(L, cx + 6, 3, 1)
    roundel(L, cx, 6.5)
    word, _, ww, wh = lettering("IASF", BIG, gap=3)
    headline(L, word, cx - ww // 2, 16, ("#ffffff", "#eef2f6", "#d2dae2", "#aab6c2"), edge="#0a1426",
             shadow="#24468e")
    L.glyphs(ATM.F3, 0, 33, sub, "#7fa2e0")
    return L.crop()


def hyperdyne():
    """Hyperdyne Systems: the wing mark from the company's uniform patch, lifted off its black plate and turned to
    the soft blue of its labs, over a clean machined HYPERDYNE and SYSTEMS tracked out under it."""
    L = Canvas()
    plate = sprite(*LOGO_SOURCES["hyperdyne"])
    mark = plate.crop((2, 2, plate.width - 2, plate.height - 3))
    mark = keep(mark, lambda p: p[0] - p[2] > 60)
    mark = mark.crop(mark.getbbox())
    word, _, ww, wh = lettering("HYPERDYNE", BIG)
    word = rounded(word)
    L.put(mark, (ww - mark.width) // 2, 0, tinted(("#3f78b0", "#6fa6dc", "#a8d0f4"), 150, 240))
    headline(L, word, 0, mark.height + 3, ("#ffffff", "#f2f6f9", "#dde5ec", "#c2ced9"), edge="#0e1a26")
    y = mark.height + 3 + wh + 3
    spaced(L, "SYSTEMS", 18, y, ww - 36, "#8fc0ec")
    L.rect(0, y + 2, 13, y + 2, "#4f86bc")
    L.rect(ww - 14, y + 2, ww - 1, y + 2, "#4f86bc")
    return L.crop()


def prodigy():
    """Prodigy: tall machined white capitals spaced wide, the O a ring in the one accent colour, and a hairline of
    that colour under the word."""
    accent = "#38d0ff"
    L = Canvas()
    word, letters, ww, wh = lettering("PRODIGY", TALL, gap=4)
    word = rounded(word)
    ox, ow = letters[2]
    o = {(x, y) for x, y in word if ox <= x < ox + ow}
    headline(L, word - o, 0, 0, ("#ffffff", "#ffffff", "#e9edf2", "#cfd6de"))
    L.fill(o, accent)
    L.fill({(x, y) for x, y in o if y < 3}, "#a8ecff")
    L.rect(0, wh + 2, ww - 1, wh + 2, "#5c6670")
    L.rect(ox, wh + 2, ox + ow - 1, wh + 2, accent)
    return L.crop()


def kelland():
    """Kelland Mining: KELLAND leaning in hazard yellow with a black edge, over a hazard-striped bar that carries
    MINING on a yellow plate."""
    L = Canvas()
    word, _, ww, wh = lettering("KELLAND", BIG)
    word = leaned(word, wh, 5)
    ww += (wh - 1) // 5
    headline(L, word, 1, 1, ("#fff3a0", "#ffd83a", "#f4bd12", "#d89a08", "#b07a06"), edge="#0c0b08",
             shadow="#3a3226")
    y0, y1 = wh + 3, wh + 11
    L.rect(0, y0, ww + 1, y1, "#0c0b08")
    for y in range(y0 + 1, y1):
        for x in range(1, ww + 1):
            if (x + y) // 3 % 2 == 0:
                L.set(x, y, "#f4bd12")
    pw = ATM.text_width(ATM.F3, "MINING", gap=2) + 8
    px0 = (ww + 2 - pw) // 2
    L.rect(px0 - 1, y0, px0 + pw, y1, "#0c0b08")
    L.rect(px0, y0 + 1, px0 + pw - 1, y1 - 1, "#f4bd12")
    L.glyphs(ATM.F3, px0 + 4, y0 + 2, "MINING", "#0c0b08", gap=2)
    return L.crop()


def lasalle():
    """LaSalle Bionational: the company's mark from its letterhead at twice its size in green, then LASALLE in
    machined teal over BIONATIONAL."""
    L = Canvas()
    mark = sprite(*LOGO_SOURCES["lasalle"])
    mark = mark.resize((mark.width * 2, mark.height * 2), 0)
    word, _, ww, wh = lettering("LASALLE", BIG)
    word = rounded(word)
    my = (wh + 8 - mark.height) // 2
    L.put(mark, 0, my, lambda p: "#5fe0a8")
    tx = mark.width + 5
    headline(L, word, tx, 0, ("#c8fff0", "#7ff0d0", "#3fd2b0", "#1fa890", "#16806e"), edge="#04140f")
    spaced(L, "BIONATIONAL", tx, wh + 3, ww, "#8fe8b8")
    return L.crop()


def clf():
    """The Colonial Liberation Front: their patch, and CLF sprayed through a stencil in red, overspray and runs and
    all, with the Front's name stencilled underneath a letter at a time, each sitting a little off the line."""
    L = Canvas()
    patch = sprite(*LOGO_SOURCES["clf"])
    rng = ATM._Rng(14)
    word, letters, ww, wh = lettering("CLF", HUGE, gap=3)
    word = stencilled(word, letters)
    sub = "COLONIAL LIBERATION FRONT"
    width = ATM.text_width(ATM.F3, sub)
    group = patch.width + 6 + ww
    gx = (width - group) // 2
    L.put(patch, gx, (wh - patch.height) // 2)
    word = moved(word, gx + patch.width + 6, 0)
    # Overspray: loose drops a pixel or two off the strokes, thickest under them.
    near = ring(word)
    for x, y in sorted(near | ring(near | word)):
        if rng.next() < (0.12 if (x, y) in near else 0.04):
            L.set(x, y, "#8a1618")
    L.fill(word, "#d42a24")
    L.fill({(x, y) for x, y in word if rng.next() < 0.1}, "#f04a3a")
    L.fill({(x, y) for x, y in word if rng.next() < 0.06}, "#a81e1e")
    # Runs where the paint pooled at the foot of a stroke.
    for x, y in sorted(word):
        if (x, y + 1) not in word and y > 6 and rng.next() < 0.18:
            for d in range(1, 2 + int(rng.next() * 4)):
                L.set(x, y + d, "#b01c1c" if d > 1 else "#d42a24")
    x = 0
    for ch in sub:
        L.glyphs(ATM.F3, x, wh + 6 + (1 if rng.next() < 0.3 else 0), ch, "#e6e0c8")
        x += ATM.text_width(ATM.F3, ch) + 1
    return L.crop()


def shield(width, height, shoulder):
    """A badge's shield: square shoulders `shoulder` rows down from a point in the middle of the top, straight
    sides, then a curve in to a point at the foot."""
    cx = (width - 1) / 2
    pixels = set()
    for y in range(height):
        if y < shoulder:
            half = cx * (0.35 + 0.65 * y / shoulder)
        elif y < height * 0.55:
            half = cx
        else:
            t = (y - height * 0.55) / (height * 0.45)
            half = cx * (1 - t ** 1.6)
        for x in range(width):
            if abs(x - cx) <= half + 0.01:
                pixels.add((x, y))
    return pixels


def badge_shield(L, x0, y0, width, height, rim, rim_dark, field):
    """A metal shield badge: a dark edge, a two-pixel rim lit along its top left, and the field inside."""
    outer = moved(shield(width, height, 4), x0, y0)
    inner = {p for p in outer if all(q in outer for q in ring({p}))}
    core = {p for p in inner if all(q in inner for q in ring({p}))}
    L.fill(outer, rim_dark)
    for x, y in inner:
        L.set(x, y, rim if x - x0 + y - y0 < (width + height) // 2 else rim_dark)
    by_row(L, core, field)
    return outer, core


def security():
    """Colony security: a silver police shield with the force's blue field and a white star, then COLONY tracked
    out over SECURITY in white with a blue shadow."""
    L = Canvas()
    w, h = 17, 21
    _, core = badge_shield(L, 0, 0, w, h, "#eef2f6", "#7d8794", ("#3a74d0", "#2c5fb8", "#1d4590"))
    L.glyphs(dict(ATM.F3, **{"*": ["..#..", "#####", ".###.", ".#.#.", "#...#"]}), (w - 5) // 2, 7, "*", "#ffffff")
    word, _, ww, wh = lettering("SECURITY", BIG)
    tx = w + 5
    ruled(L, "COLONY", tx, 1, ww, "#8fb6f0", "#2c5fb8")
    headline(L, word, tx, 8, ("#ffffff", "#f2f5f8", "#d8e0e8", "#b8c4d0"), edge="#08142c", shadow="#2c5fb8")
    return L.crop()


def rangers():
    """CWPS rangers: the star off the service medal set in a khaki ring on the ranger's green - the old ranger's
    star-in-a-wheel - then CWPS tracked out over RANGERS in khaki."""
    L = Canvas()
    medal = sprite(*LOGO_SOURCES["rangers"])
    # The medal's star hangs under its ribbon: keep the star.
    star = medal.crop((0, medal.height - 11, medal.width, medal.height))
    star = star.crop(star.getbbox())
    d = 19
    disc(L, d / 2, d / 2, d / 2, "#2a2418")
    disc(L, d / 2, d / 2, d / 2 - 1, "#dccb93")
    disc(L, d / 2, d / 2, d / 2 - 2, "#8a7c52")
    disc(L, d / 2, d / 2, d / 2 - 3, "#22341d")
    L.set(5, 3, "#fff2c4")
    L.set(4, 4, "#fff2c4")
    L.put(star, (d - star.width) // 2, (d - star.height) // 2 + 1)
    word, _, ww, wh = lettering("RANGERS", BIG)
    tx = d + 5
    ruled(L, "CWPS", tx, 0, ww, "#b8d49a", "#4a6242")
    headline(L, word, tx, 7, ("#fff4cc", "#ecdca6", "#dccb93", "#bfae76", "#9a8a58"), edge="#0c140b",
             shadow="#374e32")
    return L.crop()


def nspa():
    """The NSPA: the gold blossom of the authority's floor emblem lifted off its concrete onto a black shield with
    a red rim, the constable's colours, then NSPA in white over the authority's name."""
    from PIL import Image

    L = Canvas()
    tile = Image.open(os.path.join(REPO, "Resources", "Textures", LOGO_SOURCES["nspa"][0])).convert("RGBA")
    tile = keep(tile, lambda p: p[0] - p[2] > 30 and p[0] > 70)
    emblem = tile.crop(tile.getbbox())
    w, h = emblem.width + 4, 39
    badge_shield(L, 0, 0, w, h, "#e04450", "#5c1218", ("#16161a", "#0f0f12", "#08080a"))
    L.put(emblem, 2, 4)
    word, _, ww, wh = lettering("NSPA", BIG, gap=3)
    tx = w + 5
    headline(L, word, tx, 9, ("#ffffff", "#f4f4f6", "#dcdce2", "#bcbcc6"), edge="#140406", shadow="#a8202c")
    L.glyphs(ATM.F3, tx, 26, "NEROID SECTOR", "#e8d08a")
    L.glyphs(ATM.F3, tx, 32, "POLICING AUTHORITY", "#e8d08a")
    return L.crop()


def cmb():
    """The Colonial Marshal Bureau: the marshal's own badge from the bureau ID, then COLONIAL tracked out over
    MARSHALS in silver on the bureau's navy."""
    L = Canvas()
    badge = sprite(*LOGO_SOURCES["cmb"])
    word, _, ww, wh = lettering("MARSHALS", BIG)
    L.put(badge, 0, 0)
    tx = badge.width + 5
    ruled(L, "COLONIAL", tx, 2, ww, "#e2b45a", "#33497a")
    headline(L, word, tx, 9, ("#ffffff", "#e6ebf2", "#c3cbd7", "#98a3b4"), edge="#060a14", shadow="#33497a")
    return L.crop()


FACTION_LOGOS = {
    "upp": upp, "uscm": uscm, "twe": twe, "iasf": iasf, "hyperdyne": hyperdyne, "prodigy": prodigy,
    "kelland": kelland, "lasalle": lasalle, "clf": clf, "security": security, "rangers": rangers, "nspa": nspa,
    "cmb": cmb,
}


# Program icons, 16x16, one set per OS palette: (light, main, dim, dark). The desktop draws them at 2x,
# the taskbar, start menu and title bars at 1x.
ICON_PALETTES = {
    "cmuosseegson": ("#b8ffc6", "#3fe05a", "#1f8a33", "#0f3d18"),
    "cmuosweyland": ("#ffe2a0", "#ffb547", "#a8681c", "#4a2c08"),
    # Red with the star's gold for its highlights.
    "cmuosupp": ("#ffd77a", "#e8463a", "#8e1e16", "#2c0806"),
    # The marines' olive-drab terminals.
    "cmuosuscm": ("#e2f0c8", "#9cbf6e", "#4c6a34", "#141e0c"),
    # Imperial gold on navy.
    "cmuostwe": ("#fff0b8", "#e2b33c", "#8a6418", "#0e1634"),
    # Aerospace silver.
    "cmuosiasf": ("#ffffff", "#c4ccd6", "#6c7886", "#161a22"),
    # Laboratory white and pale blue.
    "cmuoshyperdyne": ("#ffffff", "#e6eef4", "#7fb0dc", "#16263a"),
    # Black and white, the accent where the highlights fall.
    "cmuosprodigy": ("#38d0ff", "#eef0f4", "#6a6e78", "#0a0a0e"),
    # Hazard yellow on black.
    "cmuoskelland": ("#fff4a8", "#f4c21a", "#8a6a0c", "#16140e"),
    # Biotech teal.
    "cmuoslasalle": ("#d4fff0", "#4fd8b0", "#1f7a64", "#08241e"),
    # Police blue, the checker's white.
    "cmuossecurity": ("#ffffff", "#4a86f0", "#1d4590", "#081430"),
    # Khaki on ranger green.
    "cmuosrangers": ("#f4ecc4", "#d4c07e", "#4a6242", "#101a0c"),
    # The constable's red on black.
    "cmuosnspa": ("#ffffff", "#e8505a", "#7a1a22", "#140608"),
    # Bureau sky blue, the star's gold for highlights.
    "cmuoscmb": ("#ffe6a0", "#8fc4ff", "#33497a", "#0a1120"),
}


def icon_browser(light, main, dim, dark):
    """A wireframe globe."""
    from PIL import Image, ImageDraw

    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([1, 1, 14, 14], fill=dark, outline=main)
    d.ellipse([5, 1, 10, 14], outline=dim)
    d.line([(1, 7), (14, 7)], fill=main)
    d.line([(3, 4), (12, 4)], fill=dim)
    d.line([(3, 11), (12, 11)], fill=dim)
    d.line([(7, 1), (7, 14)], fill=main)
    d.point([(4, 3), (3, 4)], fill=light)
    return img


def icon_files(light, main, dim, dark):
    """A folder with a sheet sticking out of it."""
    from PIL import Image, ImageDraw

    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([1, 3, 6, 4], fill=main)
    d.rectangle([7, 0, 13, 7], fill=light, outline=dim)
    d.line([(9, 2), (11, 2)], fill=dim)
    d.line([(9, 4), (11, 4)], fill=dim)
    d.rectangle([1, 5, 14, 14], fill=dark, outline=main)
    d.line([(2, 6), (13, 6)], fill=light)
    d.line([(2, 13), (13, 13)], fill=dim)
    return img


def icon_system(light, main, dim, dark):
    """A monitor with a prompt on its screen."""
    from PIL import Image, ImageDraw

    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([1, 1, 14, 11], fill=dim, outline=main)
    d.rectangle([3, 3, 12, 9], fill=dark)
    d.line([(4, 5), (7, 5)], fill=light)
    d.line([(4, 7), (9, 7)], fill=main)
    d.point([(11, 7)], fill=light)
    d.rectangle([6, 12, 9, 12], fill=main)
    d.rectangle([3, 13, 12, 14], fill=main)
    d.line([(3, 13), (12, 13)], fill=light)
    return img


ICONS = {"browser": icon_browser, "files": icon_files, "system": icon_system}

# The CLF's programs run on colony machines they have broken into, so their icons are the colony's own broken and
# bodged: cracked, patched with tape, sprayed with the Front's red; the browser is the colony globe hacked about
# in code, the others drawn by hand. Drawn in their own colours: k ink, w spray white,
# g and G the flag's green, r and R its red, t and T tape.
CLF_COLOURS = {"k": "#121410", "w": "#e8e2c8", "g": "#3c7a2a", "G": "#6cc04a", "r": "#8e1a1a", "R": "#e0342a",
               "t": "#8a8268", "T": "#c8bea0"}
CLF_ICONS = {
    # A folder with CLF sprayed across it, the tab torn and taped.
    "files": [
        "................",
        ".......wwwwww...",
        ".......wkkkkw...",
        ".TTt...wkwwkw...",
        "tTTTGG.wkkkkw...",
        "GgGGGGGGGGGGGGG.",
        "GkkkkkkkkkkkkkG.",
        "GkRRkRkkkRRRkkG.",
        "GRkkkRkkkRkkkkG.",
        "GRkkkRkkkRRkkkG.",
        "GRkkkRkkkRkkkkG.",
        "GkRRkRRRkRkkkkG.",
        "GkkkkkkkkkkkkrG.",
        "GggggggggggggrG.",
        "GGGGGGGGGGGGGGr.",
        ".............r..",
    ],
    # A monitor with its screen cracked, a bodged prompt, tape across a corner and its wires hanging out.
    "system": [
        "................",
        "TTtGGGGGGGGGGGG.",
        "tTTTgggggggggGG.",
        "GgTTTtkkkkkkkgG.",
        "GgkkTTkkwkkkkgG.",
        "GgkRRkkkkwkkkgG.",
        "GgkkkkkkkkwkkgG.",
        "GgkRRRRkkkwwkgG.",
        "GgkkkkkkkwkkkgG.",
        "GgkRkkkkkkwkkgG.",
        "GgggggggggggggG.",
        "GGGGGGGGGGGGGGG.",
        "....RgGGg.......",
        "...R.GGGGG......",
        "..R.GGGGGGGG....",
        "..r.............",
    ],
}


CLF_STAR = ("..#..", "#####", ".###.", ".#.#.", "#...#")


def icon_clf(app):
    from PIL import Image

    c = CLF_COLOURS
    if app == "browser":
        # The colony's globe, cracked across the equator and glued back a pixel out, a strip of tape over the
        # break and the Front's star sprayed on.
        base = icon_browser(c["w"], c["G"], c["g"], c["k"])
        img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        img.paste(base.crop((0, 0, 16, 8)), (0, 0))
        img.paste(base.crop((0, 8, 15, 16)), (1, 8))
        for x in range(0, 10):
            y = 5 + x // 2
            img.putpixel((x, y), ATM.rgba(c["T"]))
            img.putpixel((x, y + 1), ATM.rgba(c["T"] if x % 4 else c["t"]))
            img.putpixel((x, y + 2), ATM.rgba(c["t"]))
        for y, row in enumerate(CLF_STAR):
            for x, bit in enumerate(row):
                if bit == "#":
                    img.putpixel((10 + x, 10 + y), ATM.rgba(c["R"]))
        return img
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    assert len(CLF_ICONS[app]) == 16 and all(len(row) == 16 for row in CLF_ICONS[app]), app
    for y, row in enumerate(CLF_ICONS[app]):
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x, y), ATM.rgba(CLF_COLOURS[ch]))
    return img


PREVIEW_BACKGROUNDS = {
    "weyland": (18, 10, 2),
    "seegson": (44, 38, 34),
    "sevastolink": (10, 14, 18),
}
LICENSE = "CC-BY-SA-3.0"
SOURCE = "https://github.com/Ignies/ColonialMarinesUniverse"
GENERATOR = "Content.CMU/Design/PersonalComputers/LogoPixelArt/logo_pixel_art.py"
# The logos drawn before the faction ones, credited as they always were.
CLASSIC_ATTRIBUTIONS = [
    (["weyland.png"], LICENSE,
     "Weyland-Yutani boot logo for CMU, the colony ATM's boot mark redrawn in amber by " + GENERATOR),
    (["seegson.png", "sevastolink.png"], LICENSE,
     "Pixel-art Seegson rewire and Sevastolink boot logos drawn for CMU, generated by " + GENERATOR),
]
# Logos with nothing taken from a game sprite.
DRAWN = ("iasf", "prodigy", "kelland", "security")


def source_licence(name):
    """The licence and copyright of the sprite a logo was built round, from its RSI's meta.json (the NSPA tile, a
    bare PNG, comes from the same CM-SS13 Hybrisa set as the rest of RMC14's tiles)."""
    import json

    rsi, _ = LOGO_SOURCES[name]
    if rsi.endswith(".png"):
        return LICENSE, SOURCE_CREDITS[name]
    for root in (os.path.join(REPO, "Content.CMU", "Resources", "Textures"), os.path.join(REPO, "Resources", "Textures")):
        path = os.path.join(root, rsi, "meta.json")
        if os.path.exists(path):
            with open(path, encoding="utf-8-sig") as f:
                meta = json.load(f)
            return meta["license"], SOURCE_CREDITS.get(name, meta["copyright"])
    raise FileNotFoundError(rsi)


def yaml_entries(entries):
    def quoted(s):
        return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'

    blocks = []
    for files, licence, copyright in entries:
        blocks.append(f'- files: [{", ".join(quoted(f) for f in files)}]\n'
                      f"  license: {quoted(licence)}\n"
                      f"  copyright: {quoted(copyright)}\n"
                      f"  source: {quoted(SOURCE)}\n")
    return "\n".join(blocks)


def logo_attributions():
    entries = list(CLASSIC_ATTRIBUTIONS)
    entries.append(([f"{n}.png" for n in DRAWN], LICENSE,
                    "Pixel-art faction boot logos drawn for CMU, generated by " + GENERATOR))
    for name in FACTION_LOGOS:
        if name in DRAWN:
            continue
        rsi, state = LOGO_SOURCES[name]
        licence, copyright = source_licence(name)
        where = rsi if state is None else f"{rsi} state {state}"
        # Shared under the source sprite's own licence.
        entries.append(([f"{name}.png"], licence,
                        f"Faction boot logo drawn for CMU by {GENERATOR}, built round the game sprite {where} "
                        f"({copyright})"))
    with open(os.path.join(OUT, "attributions.yml"), "w", encoding="utf-8", newline="\n") as f:
        f.write(yaml_entries(entries))


def main():
    from PIL import Image

    os.makedirs(OUT, exist_ok=True)
    builds = [("weyland", weyland), ("seegson", seegson), ("sevastolink", sevastolink)] + list(FACTION_LOGOS.items())
    for name, build in builds:
        img = build()
        if name in FACTION_LOGOS:
            assert 64 <= img.width <= 140 and 20 <= img.height <= 40, (name, img.size)
        img.save(os.path.join(OUT, f"{name}.png"))
        if "--preview" in sys.argv:
            ground = PREVIEW_BACKGROUNDS.get(name, (8, 10, 12))
            big = Image.new("RGBA", (img.width + 8, img.height + 8), ground + (255,))
            big.alpha_composite(img, (4, 4))
            big.resize((big.width * 5, big.height * 5), Image.NEAREST).save(
                os.path.join(sys.argv[-1], f"{name}_preview.png"))
    logo_attributions()


def icons():
    from PIL import Image

    out = os.path.join(os.path.dirname(OUT), "Icons")
    os.makedirs(out, exist_ok=True)
    sheet = []
    files = []
    sets = [(os_id, {app: (lambda draw=draw, palette=palette: draw(*palette)) for app, draw in ICONS.items()})
            for os_id, palette in ICON_PALETTES.items()]
    sets.append(("cmuosclf", {app: (lambda app=app: icon_clf(app)) for app in ICONS}))
    for os_id, apps in sets:
        for app, draw in apps.items():
            img = draw()
            img.save(os.path.join(out, f"{os_id}_{app}.png"))
            files.append(f"{os_id}_{app}.png")
            sheet.append(img)
    # deeps.png is the ExtraNet's Deeps site icon, kept in the same entry it has always been in.
    with open(os.path.join(out, "attributions.yml"), "w", encoding="utf-8", newline="\n") as f:
        f.write(yaml_entries([(files + ["deeps.png"], LICENSE,
                               "Pixel-art personal computer program icons drawn for CMU, generated by " + GENERATOR)]))
    if "--preview" in sys.argv:
        strip = Image.new("RGBA", (len(sheet) * 20, 20), (12, 12, 14, 255))
        for i, img in enumerate(sheet):
            strip.alpha_composite(img, (i * 20 + 2, 2))
        strip.resize((strip.width * 6, strip.height * 6), Image.NEAREST).save(os.path.join(sys.argv[-1], "icons_preview.png"))


if __name__ == "__main__":
    icons()
    main()
