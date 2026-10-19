"""Draws the WY Leaks (wyleaks.deep) banner and emblem for the deep web.

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/Deep/WyLeaks.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Deep/WyLeaks/{banner,emblem}.png.

Everything is drawn at 1x on the pixel grid with no antialiasing, then upscaled 2x with nearest
neighbour. The look is a photocopied leak: paper white capitals with a torn red and blue colour
split, black redaction bars over the small print, a manila folder bleeding red at the bottom edge,
and a torn strip of tractor-feed printout along the foot. Background is the site's toner black, so
the banner sits flush on the page.
"""
import os

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "Deep", "WyLeaks")
SCALE = 2
W, H = 264, 64

# -- Palette (site style: background #0C0C0A, text #CFCBBD, heading #F2EEE3, link #6FA8FF) ---------
BG = "#0C0C0A"
TONER = "#16160F"
SPECK = "#24241C"
BAR = "#3A3A36"
BAR_HI = "#4A4A44"
PAPER = "#BDB9AC"
WHITE = "#F2EEE3"
WHITE_LO = "#8F8C82"
PAPER_LO = "#9C988A"
INK = "#5C5A50"
RED = "#FF3B30"
RED_LO = "#8E1A14"
BLUE = "#6FA8FF"
BLUE_LO = "#2C4F8C"
MANILA = "#8C7A30"
MANILA_HI = "#B9A23E"
MANILA_LO = "#4E4317"
OUTLINE = "#060605"

# -- 5x7 capitals, drawn in 3x3 blocks for the name -------------------------------------------------
BIG = {
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "K": ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    " ": [".....", ".....", ".....", ".....", ".....", ".....", "....."],
}

# -- 3x5 small print (wider where three columns cannot carry the letter) ----------------------------
F3 = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"], "B": ["##.", "#.#", "##.", "#.#", "##."],
    "C": [".##", "#..", "#..", "#..", ".##"], "D": ["##.", "#.#", "#.#", "#.#", "##."],
    "E": ["###", "#..", "##.", "#..", "###"], "F": ["###", "#..", "##.", "#..", "#.."],
    "G": [".##", "#..", "#.#", "#.#", ".##"], "H": ["#.#", "#.#", "###", "#.#", "#.#"],
    "I": ["###", ".#.", ".#.", ".#.", "###"], "J": ["..#", "..#", "..#", "#.#", ".#."],
    "K": ["#.#", "#.#", "##.", "#.#", "#.#"], "L": ["#..", "#..", "#..", "#..", "###"],
    "M": ["#...#", "##.##", "#.#.#", "#...#", "#...#"], "N": ["#..#", "##.#", "#.##", "#..#", "#..#"],
    "O": [".#.", "#.#", "#.#", "#.#", ".#."], "P": ["##.", "#.#", "##.", "#..", "#.."],
    "Q": [".#.", "#.#", "#.#", "##.", ".##"], "R": ["##.", "#.#", "##.", "#.#", "#.#"],
    "S": [".##", "#..", ".#.", "..#", "##."], "T": ["###", ".#.", ".#.", ".#.", ".#."],
    "U": ["#.#", "#.#", "#.#", "#.#", "###"], "V": ["#.#", "#.#", "#.#", "#.#", ".#."],
    "W": ["#...#", "#...#", "#.#.#", "##.##", "#...#"], "X": ["#.#", "#.#", ".#.", "#.#", "#.#"],
    "Y": ["#.#", "#.#", ".#.", ".#.", ".#."], "Z": ["###", "..#", ".#.", "#..", "###"],
    "0": ["###", "#.#", "#.#", "#.#", "###"], "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["##.", "..#", ".#.", "#..", "###"], "3": ["##.", "..#", ".#.", "..#", "##."],
    "4": ["#.#", "#.#", "###", "..#", "..#"], "7": ["###", "..#", ".#.", ".#.", ".#."],
    "9": ["###", "#.#", "###", "..#", "##."],
    "-": ["...", "...", "###", "...", "..."], ".": [".", ".", ".", ".", "#"],
    "/": ["..#", "..#", ".#.", "#..", "#.."], ":": [".", "#", ".", "#", "."],
    " ": ["..", "..", "..", "..", ".."],
}


def rgba(c):
    c = c.lstrip("#")
    return (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), 255)


class Layer:
    def __init__(self):
        self.px = {}

    def set(self, x, y, c):
        if 0 <= x < W and 0 <= y < H:
            self.px[(x, y)] = rgba(c) if isinstance(c, str) else c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def outline(self, x0, y0, x1, y1, c):
        for x in range(x0, x1 + 1):
            self.set(x, y0, c)
            self.set(x, y1, c)
        for y in range(y0, y1 + 1):
            self.set(x0, y, c)
            self.set(x1, y, c)

    def paste(self, other, dx=0, dy=0):
        for (x, y), c in other.px.items():
            self.set(x + dx, y + dy, c)


def text_width(font, text, gap=1, block=1):
    return sum((len(font.get(ch, font[" "])[0]) * block) + gap for ch in text) - gap


def glyphs(L, font, x, y, text, c, gap=1, block=1):
    for ch in text:
        g = font.get(ch, font[" "])
        for gy, row in enumerate(g):
            for gx, bit in enumerate(row):
                if bit == "#":
                    L.rect(x + gx * block, y + gy * block, x + gx * block + block - 1, y + gy * block + block - 1, c)
        x += len(g[0]) * block + gap
    return x


class Rng:
    """Tiny LCG so every run draws the same noise."""

    def __init__(self, seed):
        self.s = seed

    def next(self):
        self.s = (self.s * 1103515245 + 12345) & 0x7FFFFFFF
        return self.s


def backdrop(L):
    L.rect(0, 0, W - 1, H - 1, BG)
    rng = Rng(419)
    # Photocopier toner: faint streaks and specks.
    for y in range(0, H):
        if y % 4 == 1:
            for x in range(0, W):
                L.set(x, y, TONER)
    for _ in range(170):
        x, y = rng.next() % W, rng.next() % (H - 8)
        L.set(x, y, SPECK)
    # Broken red rule along the top, with a glitch skip.
    for x in range(4, W - 4):
        if not (x // 23) % 5 == 3 and x % 37 not in (0, 1):
            L.set(x, 2, RED_LO)
    # A strip of tractor-feed printout torn off along the foot, sprocket holes and all.
    tear = [0, 0, 1, 1, 0, 1, 2, 1, 0, 0, 1, 0, 0, 1, 2, 2, 1, 0]
    for x in range(0, W):
        t = H - 7 + tear[(x * 7 // 5) % len(tear)]
        L.set(x, t, OUTLINE)
        for y in range(t + 1, H):
            L.set(x, y, PAPER_LO)
    for x in range(3, W - 2, 8):
        L.rect(x, H - 4, x + 1, H - 3, BG)
    # Perforation where the next sheet was torn away.
    for x in range(0, W, 3):
        L.set(x, H - 1, INK)


def folder(L, x0, y0):
    """A manila folder torn open, a redacted sheet sliding out, bleeding at the bottom edge."""
    # Back flap with its tab.
    L.rect(x0 + 1, y0 + 3, x0 + 40, y0 + 34, MANILA_LO)
    L.rect(x0 + 1, y0, x0 + 14, y0 + 3, MANILA_LO)
    L.outline(x0, y0 - 1, x0 + 15, y0 + 3, OUTLINE)
    L.rect(x0 + 1, y0, x0 + 14, y0 + 3, MANILA_LO)
    # The sheet, slightly askew (stepped one pixel every few rows).
    for i, y in enumerate(range(y0 + 1, y0 + 30)):
        shift = i // 10
        L.rect(x0 + 7 + shift, y, x0 + 35 + shift, y, PAPER)
        L.set(x0 + 6 + shift, y, OUTLINE)
        L.set(x0 + 36 + shift, y, PAPER_LO)
    # Typed lines and redaction bars on the sheet.
    rows = [(4, 18, None), (7, 26, (14, 22)), (10, 22, None), (13, 25, (9, 20)), (16, 20, None)]
    for dy, length, bar in rows:
        y = y0 + 1 + dy
        shift = dy // 10
        for x in range(x0 + 10 + shift, x0 + 10 + shift + length):
            L.set(x, y, INK)
        if bar:
            L.rect(x0 + 10 + shift + bar[0], y - 1, x0 + 10 + shift + bar[1], y + 1, OUTLINE)
    # Front cover, cut lower, with a ragged torn top edge.
    tear = [0, 1, 1, 2, 1, 0, 0, 1, 2, 3, 2, 1, 1, 0, 1, 2, 2, 1, 0, 0, 1, 1, 2, 3, 3, 2, 1, 0, 0, 1,
            2, 1, 1, 0, 0, 1, 2, 2, 1, 0, 0, 1]
    top = y0 + 17
    for i, x in enumerate(range(x0, x0 + 42)):
        t = top + tear[i]
        L.set(x, t, OUTLINE)
        L.set(x, t + 1, MANILA_HI)
        for y in range(t + 2, y0 + 36):
            L.set(x, y, MANILA)
    for y in range(top, y0 + 37):
        L.set(x0, y, OUTLINE)
        L.set(x0 + 41, y, OUTLINE)
    for x in range(x0, x0 + 42):
        L.set(x, y0 + 36, OUTLINE)
        L.set(x, y0 + 35, MANILA_LO)
    # Red stamp on the cover.
    sx, sy = x0 + 8, y0 + 24
    L.outline(sx, sy, sx + 25, sy + 8, RED)
    glyphs(L, F3, sx + 3, sy + 2, "LEAK", RED, gap=2)
    # Drips off the bottom edge: two pixel runs ending in a heavier drop.
    for dx, length in ((5, 3), (12, 8), (19, 4), (27, 7), (34, 5)):
        x = x0 + dx
        bottom = y0 + 37 + length
        for y in range(y0 + 37, bottom):
            L.set(x, y, RED)
            L.set(x + 1, y, RED_LO)
        L.rect(x - 1, bottom, x + 1, bottom + 1, RED)
        L.set(x + 2, bottom, RED_LO)
        L.set(x + 2, bottom + 1, RED_LO)
        L.rect(x, bottom + 2, x + 1, bottom + 2, RED_LO)


def title(x, y):
    """The name with a colour split, then two rows torn sideways like a bad sync."""
    name = "WY LEAKS"
    block, gap = 3, 3
    T = Layer()
    glyphs(T, BIG, x - 1, y, name, RED, gap=gap, block=block)
    glyphs(T, BIG, x + 1, y + 1, name, BLUE_LO, gap=gap, block=block)
    glyphs(T, BIG, x, y, name, WHITE, gap=gap, block=block)
    torn = Layer()
    for (px, py), c in T.px.items():
        shift = 0
        if y + 6 <= py <= y + 7:
            shift = 2
        elif py == y + 17:
            shift = -2
        torn.set(px + shift, py, c)
    return torn, text_width(BIG, name, gap=gap, block=block)


def banner():
    L = Layer()
    backdrop(L)
    folder(L, 12, 9)

    tx, ty = 70, 9
    name, width = title(tx, ty)
    L.paste(name)

    # Small print: the address, then a redacted line.
    y = ty + 27
    x = glyphs(L, F3, tx, y, "WYLEAKS.DEEP", BLUE)
    x = glyphs(L, F3, x + 5, y, "//", RED)
    glyphs(L, F3, x + 5, y, "INTERNAL MEMORANDA", PAPER)

    y += 8
    x = glyphs(L, F3, tx, y, "PROPERTY OF", PAPER_LO)
    L.rect(x + 3, y - 1, x + 3 + 66, y + 5, BAR)
    L.rect(x + 3, y - 1, x + 3 + 66, y - 1, BAR_HI)
    x = glyphs(L, F3, x + 3 + 66 + 4, y, "DO NOT COPY", RED)
    return L


def emblem():
    """The folder on its own, for page bodies."""
    global W, H
    W, H = 56, 64
    L = Layer()
    L.rect(0, 0, W - 1, H - 1, BG)
    folder(L, 7, 6)
    W, H = 264, 64
    return L, (56, 64)


def save(L, size, path):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    for (x, y), c in L.px.items():
        if x < size[0] and y < size[1]:
            img.putpixel((x, y), c)
    img = img.resize((size[0] * SCALE, size[1] * SCALE), Image.NEAREST)
    img.save(path)
    print("wrote", path, img.size)


def main():
    os.makedirs(OUT, exist_ok=True)
    save(banner(), (W, H), os.path.join(OUT, "banner.png"))
    layer, size = emblem()
    save(layer, size, os.path.join(OUT, "emblem.png"))


if __name__ == "__main__":
    main()
