"""Draws the banner and emblem for Marshal Watch (marshalwatch.deep), a hidden deep web site that keeps
case files on corrupt Colonial Marshals.

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/Deep/MarshalWatch.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Deep/MarshalWatch/{banner,emblem}.png.
Pass --preview DIR to also write 4x previews there.

The banner is a manila case folder on a dark desk: a cracked Colonial Marshal badge on the left (the game's own
badge sprite, cmb_marshal.rsi, at 2x in a brass ring, never redrawn), the name typed
in worn block capitals, redacted lines, a red COMPROMISED stamp and a couple of torn scan lines.
Everything is drawn at 1x on the pixel grid without antialiasing, then upscaled 2x with nearest neighbour.
Small print uses the colony ATM generator's 3x5 face (Design/ColonyEconomy/AtmPixelArt), with the few
glyphs it lacks added here.
"""
import argparse
import importlib.util
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import rsi_sprites as RS  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "Deep",
                   "MarshalWatch")
ATM_ART = os.path.join(REPO, "Content.CMU", "Design", "ColonyEconomy", "AtmPixelArt", "atm_pixel_art.py")
SCALE = 2
W, H = 260, 66

# Palette. The site's pages are the folder itself: MANILA background, INK text, RED_DEEP headings and
# CARBON links, so the banner reads as a scan of the folder lying on a dark desk.
DESK = "#14110C"
RED_DEEP = "#9E1F17"
CARBON = "#1E3A78"
DESK_LO = "#0A0806"
MANILA = "#C9B07A"
MANILA_HI = "#DCC793"
MANILA_LO = "#9C8450"
MANILA_EDGE = "#6E5A32"
SHEET = "#E6DCC1"
INK = "#2A1E14"
INK_FADE = "#5A4630"
TYPE_LINE = "#8E7A52"
REDACT = "#16110B"
RED = "#B8281E"
RED_HI = "#E04A3A"
RED_FADE = "#C25A44"
BRASS = "#D9A441"
BRASS_HI = "#F2D27A"
BRASS_LO = "#8A6420"
BRASS_DARK = "#4E3812"
STEEL = "#9AA3A8"
STEEL_LO = "#5E666B"
GLITCH_CYAN = "#5FD3D0"


def load_atm_art():
    spec = importlib.util.spec_from_file_location("atm_pixel_art", ATM_ART)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ATM = load_atm_art()

# Small print: the ATM's 3x5 face plus the glyphs this banner needs that it does not carry.
SMALL = dict(ATM.F3)
SMALL.update({
    "3": ["##.", "..#", ".#.", "..#", "##."], "4": ["#.#", "#.#", "###", "..#", "..#"],
    "5": ["###", "#..", "##.", "..#", "##."], "6": [".##", "#..", "###", "#.#", "###"],
    "7": ["###", "..#", ".#.", ".#.", ".#."], "8": ["###", "#.#", "###", "#.#", "###"],
    "9": ["###", "#.#", "###", "..#", "##."], "X": ["#.#", "#.#", ".#.", "#.#", "#.#"],
    ".": [".", ".", ".", ".", "#"], "/": ["..#", "..#", ".#.", "#..", "#.."],
    ":": [".", "#", ".", "#", "."], "#": [".#.#.", "#####", ".#.#.", "#####", ".#.#."],
})

# Title and stamp face: 5x7 block capitals. The title draws each bit as a 2x2 block.
BLOCK = {
    "M": ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "C": [".####", "#....", "#....", "#....", "#....", "#....", ".####"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    " ": ["..."] * 7,
}


class Rng:
    """A tiny LCG so every render is identical."""

    def __init__(self, seed):
        self.state = seed

    def next(self):
        self.state = (self.state * 1103515245 + 12345) & 0x7FFFFFFF
        return self.state

    def chance(self, p):
        return (self.next() % 1000) < p * 1000


def rgba(c):
    c = c.lstrip("#")
    return int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), 255


class Canvas:
    def __init__(self, w, h, bg=None):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), rgba(bg) if bg else (0, 0, 0, 0))
        self.draw = ImageDraw.Draw(self.img)

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.img.putpixel((x, y), rgba(c))

    def get(self, x, y):
        return self.img.getpixel((x, y))

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def hline(self, x0, x1, y, c):
        self.rect(x0, y, x1, y, c)

    def vline(self, x, y0, y1, c):
        self.rect(x, y0, x, y1, c)

    def text(self, font, x, y, text, c, gap=1):
        for ch in text:
            g = font.get(ch, font[" "])
            for gy, row in enumerate(g):
                for gx, bit in enumerate(row):
                    if bit == "#":
                        self.set(x + gx, y + gy, c)
            x += len(g[0]) + gap
        return x


def text_width(font, text, gap=1):
    return sum(len(font.get(ch, font[" "])[0]) + gap for ch in text) - gap


BADGE_RSI = "_RMC14/Objects/CMIDs/cmb_marshal.rsi"


def badge(cv, cx, cy):
    """The game's Colonial Marshal badge in a brass ring, split by a red crack."""
    r = 20
    # Ring: dark rim, brass band, highlight on the upper left.
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            d2 = (x - cx) ** 2 + (y - cy) ** 2
            if d2 <= r * r:
                if d2 > (r - 1) ** 2:
                    c = BRASS_DARK
                elif d2 > (r - 4) ** 2:
                    c = BRASS_HI if (x - cx) + (y - cy) < -10 else (BRASS_LO if (x - cx) + (y - cy) > 12 else BRASS)
                elif d2 > (r - 5) ** 2:
                    c = BRASS_DARK
                else:
                    c = "#2B2116"
                cv.set(x, y, c)
    # Studs around the ring.
    for x, y in ((cx, cy - r + 2), (cx, cy + r - 2), (cx - r + 2, cy), (cx + r - 2, cy)):
        cv.set(x, y, BRASS_HI)

    # The Colonial Marshal badge itself is the game's sprite (cmb_marshal.rsi, icon), at 2x.
    img = RS.scaled(RS.trim(RS.frame(BADGE_RSI, "icon")), 2)
    RS.paste(cv.img, img, cx - img.width // 2, cy + 1 - img.height // 2)
    # The crack: a jagged red line from upper right to lower left, with a dark lip.
    crack = [(cx + 11, cy - 14), (cx + 7, cy - 8), (cx + 8, cy - 5), (cx + 3, cy - 1), (cx + 4, cy + 2),
             (cx - 2, cy + 6), (cx - 1, cy + 9), (cx - 6, cy + 14), (cx - 8, cy + 17)]
    for (x0, y0), (x1, y1) in zip(crack, crack[1:]):
        cv.draw.line([(x0 + 1, y0), (x1 + 1, y1)], fill=rgba(BRASS_DARK))
        cv.draw.line([(x0, y0), (x1, y1)], fill=rgba(RED))
    for x, y in crack[1:-1:2]:
        cv.set(x, y, RED_HI)


def title(cv, x, y, text, rng):
    """Typed block capitals, each bit a 2x2 block, with a struck-light pixel here and there and a
    letter that sat a pixel high in the typebar."""
    for i, ch in enumerate(text):
        g = BLOCK[ch]
        dy = -1 if ch == "S" and i < 6 else 0
        if i == 10:
            dy = 1
        for gy, row in enumerate(g):
            for gx, bit in enumerate(row):
                if bit != "#":
                    continue
                for sy in range(2):
                    for sx in range(2):
                        c = INK_FADE if rng.chance(0.07) else INK
                        cv.set(x + gx * 2 + sx, y + dy + gy * 2 + sy, c)
        x += len(g[0]) * 2 + 2
    return x


def stamp(cv, x0, y0, text, rng):
    """COMPROMISED in a double ruled box, struck unevenly so the ink drops out in places."""
    tw = text_width(BLOCK, text)
    x1, y1 = x0 + tw + 7, y0 + 7 + 7
    ink = Canvas(cv.w, cv.h)
    ink.rect(x0, y0, x1, y0, RED)
    ink.rect(x0, y1, x1, y1, RED)
    ink.rect(x0, y0, x0, y1, RED)
    ink.rect(x1, y0, x1, y1, RED)
    ink.rect(x0 + 2, y0 + 2, x1 - 2, y0 + 2, RED)
    ink.rect(x0 + 2, y1 - 2, x1 - 2, y1 - 2, RED)
    letters = Canvas(cv.w, cv.h)
    letters.text(BLOCK, x0 + 4, y0 + 4, text, RED)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if ink.get(x, y)[3]:
                # The ruled box takes the uneven strike: ink drops out here and there.
                if rng.chance(0.12):
                    continue
                cv.set(x, y, RED_HI if rng.chance(0.08) else RED)
            elif letters.get(x, y)[3]:
                # Letters keep every pixel so the word stays legible; only the ink weight varies.
                cv.set(x, y, RED_FADE if rng.chance(0.10) else (RED_HI if rng.chance(0.06) else RED))


def paper_clip(cv, x, y):
    """A steel clip over the folder's top edge."""
    cv.vline(x, y, y + 12, STEEL)
    cv.vline(x + 4, y + 2, y + 14, STEEL)
    cv.hline(x + 1, x + 3, y - 1, STEEL)
    cv.hline(x + 1, x + 3, y + 15, STEEL_LO)
    cv.vline(x + 2, y + 2, y + 10, STEEL_LO)
    cv.set(x, y + 13, STEEL_LO)
    cv.set(x + 4, y + 15, STEEL_LO)


def glitch(cv, rows, rng):
    """Tears a few scan lines sideways, with a cyan ghost on the leading edge."""
    for y, shift in rows:
        line = [cv.get(x, y) for x in range(cv.w)]
        for x in range(cv.w):
            src = x - shift
            if 0 <= src < cv.w:
                cv.img.putpixel((x, y), line[src])
        edge = 0 if shift > 0 else cv.w - 1
        for i in range(abs(shift)):
            cv.img.putpixel((edge + (i if shift > 0 else -i), y), rgba(DESK))
        if abs(shift) < 2:
            continue
        # A cyan sliver where the tear starts.
        start = 30 + rng.next() % 140
        for x in range(start, start + 6 + rng.next() % 10):
            if cv.get(x, y) != rgba(DESK):
                cv.set(x, y, GLITCH_CYAN)


def banner():
    rng = Rng(117)
    cv = Canvas(W, H, DESK)

    # Desk grain.
    for y in range(H):
        for x in range(W):
            if rng.chance(0.03):
                cv.set(x, y, DESK_LO)

    # Folder shadow, then the folder with its tab.
    cv.rect(7, 12, 257, 64, DESK_LO)
    cv.rect(10, 2, 96, 9, MANILA_LO)
    cv.hline(11, 95, 2, MANILA)
    cv.rect(4, 9, 254, 61, MANILA)
    # A sheet sticking out of the folder's top right.
    cv.rect(150, 4, 244, 9, SHEET)
    cv.hline(150, 244, 4, "#F4EDD8")
    cv.hline(156, 200, 7, TYPE_LINE)
    cv.hline(204, 228, 7, REDACT)
    # Folder edges and bevel.
    cv.hline(4, 254, 9, MANILA_HI)
    cv.hline(4, 254, 61, MANILA_EDGE)
    cv.vline(4, 9, 61, MANILA_EDGE)
    cv.vline(254, 9, 61, MANILA_EDGE)
    cv.vline(5, 10, 60, MANILA_HI)
    # Coffee ring and fold wear on the manila.
    for y in range(10, 61):
        for x in range(5, 254):
            if rng.chance(0.025):
                cv.set(x, y, MANILA_LO)
    import math
    for a in range(0, 360, 3):
        if 200 < a < 250:
            continue
        x = round(232 + 11 * math.cos(math.radians(a)))
        y = round(24 + 9 * math.sin(math.radians(a)))
        cv.set(x, y, MANILA_LO)

    # Tab label.
    label = "MARSHALWATCH.DEEP"
    cv.text(SMALL, 53 - text_width(SMALL, label) // 2, 4, label, INK)

    # Emblem panel.
    badge(cv, 33, 35)

    # Name, a file line under it, and redacted lines.
    tx = 64
    end = title(cv, tx, 15, "MARSHAL WATCH", rng)
    cv.hline(tx, end - 3, 31, INK_FADE)
    line = "FILES ON THE MARSHALS WHO SOLD THE STAR"
    cv.text(SMALL, tx, 34, line, INK)
    case = "CMB/NEROID  FILE 117  COPY 3"
    cv.text(SMALL, tx, 42, case, INK_FADE)
    # Typed lines with redaction bars.
    cv.hline(tx, tx + 30, 51, TYPE_LINE)
    cv.rect(tx + 33, 49, tx + 70, 52, REDACT)
    cv.hline(tx + 73, tx + 96, 51, TYPE_LINE)
    cv.hline(tx, tx + 12, 56, TYPE_LINE)
    cv.rect(tx + 15, 54, tx + 44, 57, REDACT)
    cv.hline(tx + 47, tx + 84, 56, TYPE_LINE)

    # Stamp over the lower right.
    stamp(cv, 180, 43, "COMPROMISED", rng)

    paper_clip(cv, 238, 4)

    # Torn scan lines, sparingly.
    glitch(cv, [(25, 1), (58, 4), (59, 4)], rng)
    return cv.img


def emblem():
    cv = Canvas(46, 46)
    badge(cv, 23, 22)
    return cv.img


def save(img, path, scale=SCALE):
    img.resize((img.width * scale, img.height * scale), Image.NEAREST).save(path)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", help="also write 4x previews to this folder")
    args = parser.parse_args()
    os.makedirs(OUT, exist_ok=True)
    art = {"banner": banner(), "emblem": emblem()}
    for name, img in art.items():
        save(img, os.path.join(OUT, name + ".png"))
        if args.preview:
            os.makedirs(args.preview, exist_ok=True)
            save(img, os.path.join(args.preview, name + "_4x.png"), 4)
    m = RS.meta(BADGE_RSI)
    nl = chr(10)
    with open(os.path.join(OUT, "attributions.yml"), "w", encoding="utf-8", newline=nl) as f:
        f.write('- files: ["banner.png", "emblem.png"]' + nl)
        f.write(f'  license: "{m["license"]}"' + nl)
        f.write('  copyright: "Pixel-art Marshal Watch banner and emblem drawn for CMU, generated by '
                'Content.CMU/Design/PersonalComputers/SiteArt/Deep/MarshalWatch.py. Includes the game sprite '
                f'cmb_marshal.rsi ({m["copyright"]})"' + nl)
        f.write('  source: "https://github.com/Ignies/ColonialMarinesUniverse"' + nl)
    print("wrote", ", ".join(art), "to", OUT)


if __name__ == "__main__":
    main()
