"""Draws the banner for Gone Quiet (missing.deep), a hidden forum that tracks colonies that went silent.

Run: python Content.CMU/Design/PersonalComputers/SiteArt/Deep/Missing.py [--preview DIR]
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Deep/Missing/banner.png.

The look is a dead relay: a lattice mast whose signal arcs break off on one side, the title in
quarantine yellow with a slipped scanline, a waveform that goes flat and quarantine tape along the
bottom. Everything is drawn at 1x on the pixel grid with no antialiasing, then upscaled 2x nearest.
"""
import argparse
import math
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers",
                   "Sites", "Deep", "Missing")
SCALE = 2
W, H = 256, 64

BG = "#0F1316"
BG_SCAN = "#0C1013"
SPECK = "#1F272B"
SPECK_HI = "#2E383D"
TEXT = "#B7C1C4"
DIM = "#5F6B70"
YELLOW = "#E2C04A"
YELLOW_LO = "#7A6826"
YELLOW_DK = "#3F3716"
TAPE_INK = "#15181A"
CYAN = "#79C2CF"
RED = "#C94A3A"
STEEL = "#8C989C"
STEEL_LO = "#4A5458"

# Title face: 2 pixel strokes, 9 rows, drawn doubled so each cell is 2x2 at 1x.
BIG = {
    "G": [".#####.", "##...##", "##.....", "##.....", "##..###", "##...##", "##...##", "##...##", ".#####."],
    "O": [".#####.", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", ".#####."],
    "N": ["##...##", "###..##", "####.##", "##.####", "##..###", "##...##", "##...##", "##...##", "##...##"],
    "E": ["#######", "##.....", "##.....", "######.", "##.....", "##.....", "##.....", "##.....", "#######"],
    "Q": [".#####.", "##...##", "##...##", "##...##", "##...##", "##...##", "##.####", "##..###", ".######"],
    "U": ["##...##", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", ".#####."],
    "I": ["######", "..##..", "..##..", "..##..", "..##..", "..##..", "..##..", "..##..", "######"],
    "T": ["######", "..##..", "..##..", "..##..", "..##..", "..##..", "..##..", "..##..", "..##.."],
    " ": ["...", "...", "...", "...", "...", "...", "...", "...", "..."],
}

# Small print: 3x5, wider where three columns cannot carry the letter.
SMALL = {
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
    "4": ["#.#", "#.#", "###", "..#", "..#"], "5": ["###", "#..", "##.", "..#", "##."],
    "6": [".##", "#..", "###", "#.#", "###"], "7": ["###", "..#", ".#.", ".#.", ".#."],
    "8": ["###", "#.#", "###", "#.#", "###"], "9": ["###", "#.#", "###", "..#", "##."],
    ".": [".", ".", ".", ".", "#"], "/": ["..#", "..#", ".#.", "#..", "#.."],
    "-": ["...", "...", "###", "...", "..."], ":": [".", "#", ".", "#", "."],
    " ": ["..", "..", "..", "..", ".."],
}


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = {}

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[(x, y)] = c

    def get(self, x, y):
        return self.px.get((x, y))

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def line(self, x0, y0, x1, y1, c):
        """Bresenham line, one pixel wide."""
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.set(x0, y0, c)
            if x0 == x1 and y0 == y1:
                return
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def image(self):
        img = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        for (x, y), c in self.px.items():
            c = c.lstrip("#")
            img.putpixel((x, y), (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), 255))
        return img


class Rng:
    """Small deterministic generator so the static and tears are the same on every run."""

    def __init__(self, seed):
        self.s = seed & 0xFFFFFFFF

    def next(self):
        self.s = (self.s * 1664525 + 1013904223) & 0xFFFFFFFF
        return self.s / 0x100000000


def text_width(font, text, gap=1, cell=1):
    return (sum(len(font.get(ch, font[" "])[0]) + gap for ch in text) - gap) * cell


def glyphs(cv, font, x, y, text, c, gap=1, cell=1):
    for ch in text:
        g = font.get(ch, font[" "])
        for gy, row in enumerate(g):
            for gx, bit in enumerate(row):
                if bit == "#":
                    cv.rect(x + gx * cell, y + gy * cell, x + gx * cell + cell - 1, y + gy * cell + cell - 1, c)
        x += (len(g[0]) + gap) * cell
    return x


def background(cv):
    rng = Rng(1758)
    for y in range(H):
        for x in range(W):
            cv.set(x, y, BG_SCAN if y % 2 else BG)
    for y in range(H):
        for x in range(W):
            r = rng.next()
            if r < 0.035:
                cv.set(x, y, SPECK)
            elif r < 0.042:
                cv.set(x, y, SPECK_HI)


def stripes(cv, y0, y1, rng, tears):
    """Diagonal quarantine stripes between rows y0 and y1, with ragged tears where the tape is gone."""
    for y in range(y0, y1 + 1):
        for x in range(W):
            band = (x + y) // 4
            cv.set(x, y, YELLOW if band % 2 == 0 else TAPE_INK)
    for tx, tw in tears:
        for y in range(y0, y1 + 1):
            jag = int(rng.next() * 3)
            for x in range(tx - jag, tx + tw + jag):
                cv.set(x, y, BG_SCAN if y % 2 else BG)


def tape(cv):
    rng = Rng(2175)
    stripes(cv, 0, 2, rng, [(118, 9), (201, 5)])
    stripes(cv, 54, 63, rng, [(22, 6), (236, 12)])
    # Plain labels on the bottom tape, black on yellow, like a printed seal.
    for lx, label in ((52, "QUARANTINE - DO NOT RELAY"), (178, "QUARANTINE")):
        lw = text_width(SMALL, label)
        cv.rect(lx - 3, 56, lx + lw + 2, 62, YELLOW)
        glyphs(cv, SMALL, lx, 57, label, TAPE_INK)


def mast(cv):
    """A lattice relay mast. Its arcs carry on the left and break up, fade and stop on the right."""
    cx, top, base = 32, 22, 50
    half = 11
    for y in range(top, base + 1):
        spread = (y - top) * half // (base - top)
        cv.set(cx - spread, y, STEEL)
        cv.set(cx + spread, y, STEEL)
    # Cross bracing between the legs.
    level = top + 6
    while level + 6 <= base:
        a = (level - top) * half // (base - top)
        b = (level + 6 - top) * half // (base - top)
        cv.line(cx - a, level, cx + b, level + 6, STEEL_LO)
        cv.line(cx + a, level, cx - b, level + 6, STEEL_LO)
        cv.rect(cx - a, level, cx + a, level, STEEL_LO)
        level += 6
    cv.rect(cx - half - 2, base + 1, cx + half + 2, base + 1, STEEL)
    # Antenna spike and a dead warning lamp.
    cv.rect(cx, top - 5, cx, top - 1, STEEL)
    cv.rect(cx - 1, top - 7, cx + 1, top - 6, RED)
    cv.set(cx, top - 8, RED)
    # Signal arcs around the lamp.
    ox, oy = cx, top - 6
    for i, r in enumerate((5, 9, 13)):
        steps = r * 8
        for s in range(steps + 1):
            # Left side: steady arcs from the upper left down to the side.
            ang = math.radians(200 - 80 * s / steps)
            x = ox + round(r * math.cos(ang))
            y = oy - round(r * math.sin(ang))
            cv.set(x, y, YELLOW if i == 0 else YELLOW_LO if i == 1 else YELLOW_DK)
        for s in range(steps + 1):
            # Right side: broken, the outer arcs nearly gone.
            ang = math.radians(20 + 80 * s / steps)
            if (s // (2 + i)) % (2 + i) != 0:
                continue
            x = ox + round(r * math.cos(ang))
            y = oy - round(r * math.sin(ang))
            cv.set(x, y, YELLOW_LO if i < 2 else YELLOW_DK)


def title(cv, x, y):
    """GONE QUIET with a cyan and a red ghost and two slipped scanlines."""
    text = "GONE QUIET"
    layer = Canvas(W, H)
    glyphs(layer, BIG, x, y, text, YELLOW, cell=2)
    ghost_c = Canvas(W, H)
    ghost_r = Canvas(W, H)
    for (px, py) in layer.px:
        ghost_c.set(px - 1, py, CYAN)
        ghost_r.set(px + 1, py, RED)
    for (px, py), c in ghost_c.px.items():
        if py in range(y + 2, y + 6):
            cv.set(px, py, c)
    for (px, py), c in ghost_r.px.items():
        if py in range(y + 11, y + 15):
            cv.set(px, py, c)
    for (px, py), c in layer.px.items():
        cv.set(px, py, c)
    # Slip two rows to the right, as if the picture lost sync for a line.
    for row, shift in ((y + 10, 2), (y + 11, 2)):
        cells = sorted((px, c) for (px, py), c in layer.px.items() if py == row)
        for px, _ in cells:
            cv.set(px, row, BG_SCAN if row % 2 else BG)
        for px, c in cells:
            cv.set(px + shift, row, c)
    return x + text_width(BIG, text, cell=2)


def waveform(cv, x0, x1, mid):
    """A carrier that falls off to a flat line ending in a red dot."""
    rng = Rng(83)
    flat_at = x0 + (x1 - x0) * 55 // 100
    prev = mid
    for x in range(x0, x1 + 1):
        if x < flat_at:
            k = 1 - (x - x0) / (flat_at - x0)
            amp = 1 + 5 * k
            yy = mid + round(amp * math.sin((x - x0) * 0.9) + (rng.next() - 0.5) * 3 * k)
        else:
            yy = mid
        yy = max(mid - 6, min(mid + 6, yy))
        lo, hi = sorted((prev, yy))
        for y in range(lo, hi + 1):
            cv.set(x, y, TEXT if x < flat_at else DIM)
        prev = yy
    cv.rect(x1 + 2, mid - 1, x1 + 4, mid + 1, RED)
    # Tick marks under the trace.
    for x in range(x0, x1 + 1, 8):
        cv.set(x, mid + 8, STEEL_LO)


def draw():
    cv = Canvas(W, H)
    background(cv)
    tape(cv)
    mast(cv)
    tx = 66
    end = title(cv, tx, 8)
    waveform(cv, tx, 238, 35)
    line = "MISSING.DEEP // WHO STOPPED TRANSMITTING"
    glyphs(cv, SMALL, tx, 46, line, TEXT)
    cv.rect(end + 6, 10, end + 6, 25, STEEL_LO)
    glyphs(cv, SMALL, end + 10, 10, "CH 07", DIM)
    glyphs(cv, SMALL, end + 10, 17, "NO", RED)
    glyphs(cv, SMALL, end + 10, 23, "CARRIER", DIM)
    return cv.image()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", help="also write a 4x preview to this directory")
    args = parser.parse_args()
    os.makedirs(OUT, exist_ok=True)
    img = draw()
    img.resize((W * SCALE, H * SCALE), Image.NEAREST).save(os.path.join(OUT, "banner.png"))
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)
        img.resize((W * 4, H * 4), Image.NEAREST).save(os.path.join(args.preview, "missing_banner_4x.png"))


if __name__ == "__main__":
    main()
