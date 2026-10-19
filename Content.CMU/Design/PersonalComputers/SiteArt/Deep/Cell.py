"""Draws The Cell (cell.deep) masthead and emblem for the deep web.

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/Deep/Cell.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Deep/Cell/{banner,emblem}.png.

The look is a fly-posted bill: the name sprayed through a crude stencil in agit red on a torn
newsprint sheet, taped to a soot-dark wall, with paint runs under the letters. The emblem is the
CLF roundel (green sky, black band, red field, two stars and a course of bricks). A couple of
scanline slices are knocked sideways to make it look like a bad copy pulled off a dead relay.

Everything is drawn at 1x on the pixel grid with no antialiasing, then upscaled 2x with nearest
neighbour. Random touches use a fixed seed so the art is the same on every run.
"""
import os
import random

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "Deep", "Cell")
SCALE = 2

# -- Palette (site style: background #15110D, text #D8CDB2, heading #E2412B, link #86C25B) ------
BG = "#15110D"
SOOT = "#0A0806"
WALL = "#1E1812"
PAPER = "#CFC3A3"
PAPER_LO = "#A89B7B"
PAPER_HI = "#E4DAC0"
TAPE = "#8C7F5A"
TAPE_HI = "#ABA076"
RED = "#E2412B"
RED_LO = "#8E2216"
RED_DK = "#4E140C"
GREEN = "#86C25B"
GREEN_LO = "#4C7A30"
BAND = "#1A1A18"
BONE = "#D8CDB2"
DIM = "#7A6E58"

# -- Stencil capitals on a coarse 6x9 grid, each cell 3x3 pixels ---------------------------------
COARSE = {
    "T": ["######", "######", "..##..", "..##..", "..##..", "..##..", "..##..", "..##..", "..##.."],
    "H": ["##..##", "##..##", "##..##", "##..##", "######", "##..##", "##..##", "##..##", "##..##"],
    "E": ["######", "######", "##....", "##....", "#####.", "##....", "##....", "######", "######"],
    "C": [".#####", "######", "##....", "##....", "##....", "##....", "##....", "######", ".#####"],
    "L": ["##....", "##....", "##....", "##....", "##....", "##....", "##....", "######", "######"],
    " ": ["...."] * 9,
}
CELL = 3

# -- 3x5 small print ------------------------------------------------------------------------------
F3 = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"], "B": ["##.", "#.#", "##.", "#.#", "##."],
    "C": [".##", "#..", "#..", "#..", ".##"], "D": ["##.", "#.#", "#.#", "#.#", "##."],
    "E": ["###", "#..", "##.", "#..", "###"], "F": ["###", "#..", "##.", "#..", "#.."],
    "G": [".##", "#..", "#.#", "#.#", ".##"], "H": ["#.#", "#.#", "###", "#.#", "#.#"],
    "I": ["###", ".#.", ".#.", ".#.", "###"], "J": ["..#", "..#", "..#", "#.#", ".#."],
    "K": ["#.#", "#.#", "##.", "#.#", "#.#"], "L": ["#..", "#..", "#..", "#..", "###"],
    "M": ["#...#", "##.##", "#.#.#", "#...#", "#...#"], "N": ["#..#", "##.#", "#.##", "#..#", "#..#"],
    "O": [".#.", "#.#", "#.#", "#.#", ".#."], "P": ["##.", "#.#", "##.", "#..", "#.."],
    "R": ["##.", "#.#", "##.", "#.#", "#.#"], "S": [".##", "#..", ".#.", "..#", "##."],
    "T": ["###", ".#.", ".#.", ".#.", ".#."], "U": ["#.#", "#.#", "#.#", "#.#", "###"],
    "V": ["#.#", "#.#", "#.#", "#.#", ".#."], "W": ["#...#", "#...#", "#.#.#", "##.##", "#...#"],
    "X": ["#.#", "#.#", ".#.", "#.#", "#.#"], "Y": ["#.#", "#.#", ".#.", ".#.", ".#."],
    "Z": ["###", "..#", ".#.", "#..", "###"],
    "0": ["###", "#.#", "#.#", "#.#", "###"], "1": [".#.", "##.", ".#.", ".#.", "###"],
    "4": ["#.#", "#.#", "###", "..#", "..#"], "6": [".##", "#..", "###", "#.#", "###"],
    "-": ["...", "...", "###", "...", "..."], ".": [".", ".", ".", ".", "#"],
    "/": ["..#", "..#", ".#.", "#..", "#.."], ":": [".", "#", ".", "#", "."],
    " ": ["..", "..", "..", "..", ".."],
}

STAR = [
    ".....#.....",
    "....###....",
    "....###....",
    "###########",
    ".#########.",
    "..#######..",
    "..#######..",
    ".####.####.",
    ".###...###.",
    ".##.....##.",
]
STAR_SMALL = ["..#..", ".###.", "#####", ".###.", ".#.#."]


def rgba(c):
    c = c.lstrip("#")
    return int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), 255


class Canvas:
    def __init__(self, w, h, bg):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), rgba(bg) if bg else (0, 0, 0, 0))

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

    def bitmap(self, x, y, rows, c):
        for gy, row in enumerate(rows):
            for gx, bit in enumerate(row):
                if bit == "#":
                    self.set(x + gx, y + gy, c)

    def text(self, font, x, y, text, c, gap=1):
        for ch in text:
            g = font.get(ch, font[" "])
            self.bitmap(x, y, g, c)
            x += len(g[0]) + gap
        return x

    def save(self, path):
        big = self.img.resize((self.w * SCALE, self.h * SCALE), Image.NEAREST)
        big.save(path)


def text_width(font, text, gap=1):
    return sum(len(font.get(ch, font[" "])[0]) + gap for ch in text) - gap


def coarse_width(text, gap=4):
    return sum(len(COARSE[ch][0]) * CELL + gap for ch in text) - gap


def coarse_mask(text, gap=4):
    """Pixel set of the stencil name, origin at 0,0."""
    pts = set()
    x = 0
    for ch in text:
        g = COARSE[ch]
        for gy, row in enumerate(g):
            for gx, bit in enumerate(row):
                if bit == "#":
                    for dy in range(CELL):
                        for dx in range(CELL):
                            pts.add((x + gx * CELL + dx, gy * CELL + dy))
        x += len(g[0]) * CELL + gap
    return pts


# -- Emblem: the CLF roundel ------------------------------------------------------------------------
def roundel(cv, cx, cy, r):
    r2 = r * r
    # Drop shadow and a bone rim.
    for y in range(-r - 1, r + 2):
        for x in range(-r - 1, r + 2):
            if x * x + y * y <= (r + 1) * (r + 2):
                cv.set(cx + x + 1, cy + y + 1, SOOT)
    for y in range(-r - 1, r + 2):
        for x in range(-r - 1, r + 2):
            if x * x + y * y <= (r + 1) * (r + 2):
                cv.set(cx + x, cy + y, BONE)
    band_top = cy - r // 5
    band_bot = cy + r // 7
    for y in range(-r, r + 1):
        for x in range(-r, r + 1):
            if x * x + y * y <= r2 + r:
                py = cy + y
                if py < band_top:
                    c = GREEN
                elif py <= band_bot:
                    c = BAND
                else:
                    c = RED
                cv.set(cx + x, py, c)
    # Dark keyline between the fields.
    for x in range(-r, r + 1):
        for py in (band_top, band_bot + 1):
            y = py - cy
            if x * x + y * y <= r2 + r:
                cv.set(cx + x, py, SOOT)
    # Bricks: two staggered courses across the red field.
    by = band_bot + (r - (band_bot - cy)) // 2 + 1
    for row, off in ((0, 0), (3, 3)):
        y = by + row
        for x in range(-r, r + 1):
            if (x + r + off) % 6 < 4:
                for dy in (0, 1):
                    yy = y + dy - cy
                    if x * x + yy * yy <= r2 - r * 2:
                        cv.set(cx + x, y + dy, BONE)
    # Stars over the black band, and the small star in the green sky.
    sy = band_top - 3
    for dx in (-r // 2 - 1, r // 2 - 9):
        cv.bitmap(cx + dx + 1, sy + 1, STAR, SOOT)
        cv.bitmap(cx + dx, sy, STAR, BONE)
    cv.bitmap(cx - 2, cy - r + 5, STAR_SMALL, BONE)


# -- Banner -------------------------------------------------------------------------------------------
def torn_edge(rng, x0, x1, base, depth):
    """Per column offsets for a ragged paper edge."""
    out = {}
    v = 0
    for x in range(x0, x1 + 1):
        v = max(0, min(depth, v + rng.choice((-1, 0, 0, 1))))
        out[x] = base + v
    return out


def tape(cv, x, y, lean):
    for i in range(7):
        for j in range(3):
            xx = x + i
            yy = y + j + (i // 3) * lean
            cv.set(xx, yy, TAPE_HI if j == 0 else TAPE)


def banner():
    W, H = 262, 70
    rng = random.Random(1964)
    cv = Canvas(W, H, BG)

    # Wall: faint render blotches.
    for _ in range(420):
        cv.set(rng.randrange(W), rng.randrange(H), WALL)

    # Emblem on the left.
    roundel(cv, 28, 31, 21)

    # The poster sheet.
    px0, px1 = 60, W - 8
    top = torn_edge(rng, px0, px1, 4, 2)
    bot = torn_edge(rng, px0, px1, 38, 2)
    for x in range(px0, px1 + 1):
        cv.vline(x + 1, top[x] + 1, bot[x] + 1, SOOT)
    for x in range(px0, px1 + 1):
        cv.vline(x, top[x], bot[x], PAPER)
        cv.set(x, top[x], PAPER_HI)
        cv.set(x, bot[x], PAPER_LO)
    # Fold crease and copier grain.
    crease = px0 + (px1 - px0) * 2 // 3
    cv.vline(crease, top[crease] + 1, bot[crease] - 1, PAPER_LO)
    for _ in range(140):
        x = rng.randrange(px0 + 1, px1)
        y = rng.randrange(top[x] + 1, bot[x])
        cv.set(x, y, PAPER_LO)
    for _ in range(25):
        x = rng.randrange(px0 + 1, px1)
        y = rng.randrange(top[x] + 1, bot[x])
        cv.set(x, y, PAPER_HI)

    # Name sprayed through the stencil: overspray dots, shadow, paint, then runs.
    name = "THE CELL"
    nw = coarse_width(name)
    nx = px0 + (px1 - px0 - nw) // 2 + 1
    ny = 8
    mask = coarse_mask(name)
    for (x, y) in mask:
        for _ in range(1):
            if rng.random() < 0.18:
                cv.set(nx + x + rng.choice((-2, -1, 1, 2)), ny + y + rng.choice((-2, -1, 1, 2)), RED_LO)
    for (x, y) in mask:
        if (x + 1, y + 1) not in mask:
            cv.set(nx + x + 1, ny + y + 1, RED_DK)
    for (x, y) in mask:
        cv.set(nx + x, ny + y, RED)
    # Stencil bridges: 1 pixel paper gaps across the heavy bars.
    gx = 0
    for ch in name:
        cols = len(COARSE[ch][0]) * CELL
        if ch in "TE":
            for y in range(0, 2 * CELL):
                cv.set(nx + gx + cols // 2 + (2 if ch == "T" else -1), ny + y, PAPER)
        if ch == "H":
            cv.vline(nx + gx + cols // 2, ny + 4 * CELL, ny + 5 * CELL - 1, PAPER)
        if ch in "CL":
            cv.vline(nx + gx + 3 * CELL, ny + 7 * CELL, ny + 9 * CELL - 1, PAPER)
        gx += cols + 4
    # Paint runs from the bottom of the strokes.
    bottoms = {}
    for (x, y) in mask:
        if (x, y + 1) not in mask:
            bottoms.setdefault(x, []).append(y)
    for x, ys in sorted(bottoms.items()):
        for y in ys:
            if rng.random() < 0.10:
                length = rng.randint(2, 6)
                for d in range(1, length + 1):
                    yy = ny + y + d
                    if yy < bot[nx + x] - 1:
                        cv.set(nx + x, yy, RED if d < length else RED_LO)
                if length > 4:
                    cv.set(nx + x, ny + y + length + 1, RED_LO) if ny + y + length + 1 < bot[nx + x] - 1 else None

    # Tape at the corners.
    tape(cv, px0 - 2, top[px0] - 1, 0)
    tape(cv, px1 - 5, top[px1] - 1, 0)
    tape(cv, px0 - 2, bot[px0] - 2, 0)
    tape(cv, px1 - 5, bot[px1] - 2, 0)

    # Small print under the sheet.
    line1 = "COLONIAL LIBERATION FRONT // CONTACT BOARD"
    cv.text(F3, px0 + 1, 45, line1, BONE)
    addr = "CELL.DEEP"
    ax = cv.text(F3, px0 + 1, 53, addr, GREEN)
    cv.rect(ax + 1, 53, ax + 3, 57, GREEN)
    tag = "NO NAMES - NO FACES - NO RADIO"
    cv.text(F3, px1 - text_width(F3, tag), 53, tag, RED)

    # Brick course along the foot of the wall.
    by = H - 7
    cv.rect(0, by, W - 1, H - 1, SOOT)
    for row in range(2):
        y = by + 1 + row * 3
        off = 0 if row == 0 else 4
        for x in range(W):
            if (x + off) % 8 < 7:
                cv.set(x, y, RED_LO)
                cv.set(x, y + 1, RED_DK)

    # Glitch: a few scanline slices knocked sideways, and one green ghost line.
    snapshot = cv.img.copy()
    for (y0, y1, shift) in ((14, 15, 3), (27, 28, -2), (36, 36, 2)):
        for y in range(y0, y1 + 1):
            for x in range(W):
                sx = x - shift
                if 0 <= sx < W:
                    cv.img.putpixel((x, y), snapshot.getpixel((sx, y)))
    for y in range(ny + 3, ny + 6):
        for x in range(px0, px1):
            if snapshot.getpixel((x, y)) == rgba(RED) and snapshot.getpixel((x - 1, y)) != rgba(RED):
                cv.set(x - 1, y, GREEN_LO)
    return cv


def emblem():
    cv = Canvas(48, 48, None)
    roundel(cv, 23, 23, 21)
    return cv


def main():
    os.makedirs(OUT, exist_ok=True)
    banner().save(os.path.join(OUT, "banner.png"))
    emblem().save(os.path.join(OUT, "emblem.png"))
    print("wrote", OUT)


if __name__ == "__main__":
    main()
