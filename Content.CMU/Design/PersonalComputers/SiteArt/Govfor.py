"""Draws the Garrison Command (command.govfor.mil) masthead and crest for the ExtraNet.

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/Govfor.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Govfor/{banner,emblem}.png.

Everything is drawn at 1x on the pixel grid with no antialiasing, then upscaled 2x with nearest
neighbour. The name is set in hand-built stencil capitals (2 pixel strokes, 1 pixel stencil bridges)
and the small print in a 3x5 terminal font. The crest is deliberately generic, a walled tower on a
shield over a rank chevron, because the force that garrisons the colony changes from round to round.
Background is the site's olive drab, so the banner sits flush on the page.
"""
import os

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "Govfor")
SCALE = 2

# ── Palette (site style: background #1B2214, text #C9D6A3, heading #E8E2B0, link #9FC46A) ──────────
BG = "#1B2214"
SHADOW = "#0E1209"
PANEL = "#242D1A"
OLIVE = "#3B4A27"
OLIVE_HI = "#56693A"
DIM = "#6E8048"
TEXT = "#C9D6A3"
PALE = "#E8E2B0"
GREEN = "#9FC46A"
KHAKI = "#B9AE74"

# ── Stencil capitals: 11 rows, 2 pixel strokes, 1 pixel bridges cut through the bars ───────────────
STENCIL = {
    "G": [".##.###.", "###.####", "##....##", "##......", "##......", "##..####",
          "##..####", "##....##", "##....##", "###.####", ".##.###."],
    "A": [".##.###.", "###.####", "##....##", "##....##", "##....##", "########",
          "########", "##....##", "##....##", "##....##", "##....##"],
    "R": ["##.####.", "##.#####", "##....##", "##....##", "##.#####", "##.####.",
          "##..##..", "##..###.", "##...##.", "##...###", "##....##"],
    "I": ["######", "######", "..##..", "..##..", "..##..", "..##..",
          "..##..", "..##..", "..##..", "######", "######"],
    "S": [".##.###.", "###.####", "##......", "##......", "#######.", ".#######",
          "......##", "......##", "......##", "###.####", ".##.###."],
    "O": [".##.###.", "###.####", "##....##", "##....##", "##....##", "##....##",
          "##....##", "##....##", "##....##", "###.####", ".##.###."],
    "N": ["##....##", "###...##", "####..##", "####..##", "##.##.##", "##.##.##",
          "##.##.##", "##..####", "##..####", "##...###", "##....##"],
    "C": [".##.###.", "###.####", "##....##", "##......", "##......", "##......",
          "##......", "##......", "##....##", "###.####", ".##.###."],
    "M": ["##......##", "###....###", "####..####", "##.####.##", "##..##..##", "##......##",
          "##......##", "##......##", "##......##", "##......##", "##......##"],
    "D": ["##.####.", "##.#####", "##....##", "##....##", "##....##", "##....##",
          "##....##", "##....##", "##....##", "##.#####", "##.####."],
    " ": ["...."] * 11,
}

# ── 3x5 terminal font (wider where three columns cannot carry the letter) ─────────────────────────
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
    "U": ["#.#", "#.#", "#.#", "#.#", "###"], "V": ["#...#", "#...#", ".#.#.", ".#.#.", "..#.."],
    "W": ["#...#", "#...#", "#.#.#", "##.##", "#...#"], "X": ["#.#", "#.#", ".#.", "#.#", "#.#"],
    "Y": ["#.#", "#.#", ".#.", ".#.", ".#."], "Z": ["###", "..#", ".#.", "#..", "###"],
    "0": ["###", "#.#", "#.#", "#.#", "###"], "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["##.", "..#", ".#.", "#..", "###"], "3": ["##.", "..#", ".#.", "..#", "##."],
    "4": ["#.#", "#.#", "###", "..#", "..#"], "5": ["###", "#..", "##.", "..#", "##."],
    "6": [".##", "#..", "###", "#.#", "###"], "7": ["###", "..#", ".#.", ".#.", ".#."],
    "8": ["###", "#.#", "###", "#.#", "###"], "9": ["###", "#.#", "###", "..#", "##."],
    "-": ["...", "...", "###", "...", "..."], ".": [".", ".", ".", ".", "#"],
    ":": [".", "#", ".", "#", "."], "/": ["..#", "..#", ".#.", "#..", "#.."],
    " ": ["..", "..", "..", "..", ".."],
}


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


# ── Crest ─────────────────────────────────────────────────────────────────────────────────────────
# A heater shield, 31 wide, 37 tall: pale rim, olive field, a crenellated tower over a mountain
# line and a single rank chevron across the base. Drawn from a half profile so it is symmetric.
SHIELD_W = 31
SHIELD_H = 37


def shield_half_width(y):
    """Half width (from the centre column, exclusive) of shield row y."""
    flat = 22
    if y < flat:
        return SHIELD_W // 2
    t = y - flat
    taper = (0, 0, 1, 1, 2, 2, 3, 4, 5, 6, 8, 10, 12, 14, 15)
    return SHIELD_W // 2 - taper[min(t, len(taper) - 1)]


def crest(cv, ox, oy, ribbon=True):
    cx = ox + SHIELD_W // 2
    # Drop shadow, then the pale rim, a dark keyline, and the field.
    for y in range(SHIELD_H):
        hw = shield_half_width(y)
        cv.hline(cx - hw + 1, cx + hw + 1, oy + y + 1, SHADOW)
    for y in range(SHIELD_H):
        hw = shield_half_width(y)
        cv.hline(cx - hw, cx + hw, oy + y, PALE)
    for y in range(1, SHIELD_H - 1):
        hw = shield_half_width(y) - 1
        if hw >= 0:
            cv.hline(cx - hw, cx + hw, oy + y, SHADOW)
    for y in range(2, SHIELD_H - 2):
        hw = shield_half_width(y) - 2
        if hw >= 0:
            cv.hline(cx - hw, cx + hw, oy + y, OLIVE)
    # Chief: a darker band across the top with three studs.
    for y in range(2, 7):
        hw = shield_half_width(y) - 2
        cv.hline(cx - hw, cx + hw, oy + y, PANEL)
    cv.hline(cx - (SHIELD_W // 2 - 2), cx + (SHIELD_W // 2 - 2), oy + 7, SHADOW)
    for dx in (-8, 0, 8):
        cv.rect(cx + dx - 1, oy + 4, cx + dx + 1, oy + 4, KHAKI)
        cv.set(cx + dx, oy + 3, KHAKI)
        cv.set(cx + dx, oy + 5, KHAKI)

    # Mountain line behind the tower.
    ridge = [(-12, 21), (-9, 18), (-6, 19), (-3, 16), (3, 16), (6, 18), (9, 17), (12, 21)]
    for (x0, y0), (x1, y1) in zip(ridge, ridge[1:]):
        for x in range(x0, x1 + 1):
            t = (x - x0) / max(1, x1 - x0)
            y = round(y0 + (y1 - y0) * t)
            cv.vline(cx + x, oy + y, oy + 21, OLIVE_HI)

    # The tower: a keep with three merlons, a gate and an arrow slit, standing on a wall course.
    tower = [
        "##.###.##",
        "##.###.##",
        "#########",
        ".#######.",
        ".#######.",
        ".###.###.",
        ".###.###.",
        ".#######.",
        ".#######.",
        ".##...##.",
        ".#.....#.",
        ".#.....#.",
    ]
    tx, ty = cx - 4, oy + 10
    for gy, row in enumerate(tower):
        for gx, bit in enumerate(row):
            if bit == "#":
                cv.set(tx + gx + 1, ty + gy + 1, SHADOW)
    cv.bitmap(tx, ty, tower, PALE)
    # Wall course either side of the keep.
    for side in (-1, 1):
        for i in range(5, 12):
            x = cx + side * i
            cv.set(x, oy + 20, KHAKI)
            cv.set(x, oy + 21, KHAKI)
            if i % 2 == 1:
                cv.set(x, oy + 19, KHAKI)
    cv.hline(cx - 12, cx + 12, oy + 22, SHADOW)

    # Rank chevron across the base: 45 degree arms, four pixels thick, with a keyline under it.
    for i in range(0, 13):
        for th in range(5):
            y = oy + 23 + i + th
            c = SHADOW if th == 4 else GREEN
            for x in (cx - i, cx + i):
                if abs(x - cx) <= shield_half_width(y - oy) - 2 and y <= oy + SHIELD_H - 3:
                    cv.set(x, y, c)

    if ribbon:
        motto = "VIGILANT"
        rw = text_width(F3, motto) + 8
        rx = cx - rw // 2
        ry = oy + SHIELD_H - 3
        cv.rect(rx + 1, ry + 1, rx + rw, ry + 8, SHADOW)
        cv.rect(rx, ry, rx + rw - 1, ry + 7, KHAKI)
        # Swallowtail ends folded behind.
        for side, ex in ((-1, rx - 1), (1, rx + rw)):
            for dy in range(1, 9):
                if dy != 4 and dy != 5:
                    cv.set(ex, ry + dy, DIM)
                    if dy in (1, 2, 7, 8):
                        cv.set(ex + side, ry + dy, DIM)
        cv.hline(rx, rx + rw - 1, ry, PALE)
        cv.text(F3, rx + 4, ry + 2, motto, SHADOW)


# ── Livery stripes ──────────────────────────────────────────────────────────────────────────────
def hazard(cv, x0, y0, x1, y1, on, off, width=3):
    """Diagonal hazard stripes leaning right."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            cv.set(x, y, on if ((x + y) // width) % 2 == 0 else off)


def chevrons(cv, x0, y0, x1, y1, on, pitch=6, thick=2):
    """A run of right-pointing chevrons filling the band."""
    mid = (y0 + y1) / 2
    for y in range(y0, y1 + 1):
        off = int(abs(y - mid))
        for x in range(x0, x1 + 1):
            if (x - x0 + off) % pitch < thick:
                cv.set(x, y, on)


def bracket(cv, x, y, dx, dy, c, n=4):
    cv.hline(min(x, x + dx * (n - 1)), max(x, x + dx * (n - 1)), y, c)
    cv.vline(x, min(y, y + dy * (n - 1)), max(y, y + dy * (n - 1)), c)


# ── Banner ──────────────────────────────────────────────────────────────────────────────────────
def banner():
    W, H = 268, 76
    cv = Canvas(W, H, BG)

    # Top livery: hazard stripes either side of a dark plate carrying the net designation.
    tag = "GOVFOR GARRISON NET"
    tw = text_width(F3, tag)
    px0 = W - 10 - tw - 6
    hazard(cv, 0, 0, px0 - 3, 6, KHAKI, SHADOW)
    hazard(cv, px0 + tw + 9, 0, W - 1, 6, KHAKI, SHADOW)
    cv.rect(px0 - 2, 0, px0 + tw + 8, 6, SHADOW)
    cv.text(F3, px0 + 3, 1, tag, KHAKI)
    cv.hline(0, W - 1, 7, OLIVE)

    # Crest on the left.
    crest(cv, 10, 13)

    # Name in stencil capitals with a drop shadow.
    name = "GARRISON COMMAND"
    nx, ny = 54, 18
    x = nx
    for ch in name:
        g = STENCIL[ch]
        cv.bitmap(x + 1, ny + 1, g, SHADOW)
        x += len(g[0]) + 2
    x = nx
    for ch in name:
        g = STENCIL[ch]
        cv.bitmap(x, ny, g, PALE)
        x += len(g[0]) + 2
    name_end = x - 2

    # Rule under the name, two pixels deep, ending in a solid arrow tip.
    ry = ny + 14
    tip = W - 12
    cv.hline(nx, tip - 3, ry, GREEN)
    cv.hline(nx, tip - 3, ry + 1, DIM)
    for i in range(3):
        cv.vline(tip - 2 + i, ry - 1 + i, ry + 2 - i, GREEN)

    # Small print.
    cv.text(F3, nx, ry + 5, "GOVERNMENT FORCES - COLONIAL GARRISON", TEXT)
    cv.text(F3, nx, ry + 13, "OFFICIAL BULLETINS AND SECURITY NOTICES", KHAKI)
    addr = "COMMAND.GOVFOR.MIL"
    ax = cv.text(F3, nx, ry + 21, addr, GREEN)
    cv.rect(ax + 1, ry + 21, ax + 3, ry + 25, GREEN)  # terminal cursor

    # Status block to the right of the name: a framed readout.
    sx0, sy0 = name_end + 8, ny - 2
    sx1 = W - 10
    if sx1 - sx0 >= 24:
        cv.rect(sx0, sy0, sx1, sy0 + 13, PANEL)
        bracket(cv, sx0, sy0, 1, 1, GREEN)
        bracket(cv, sx1, sy0, -1, 1, GREEN)
        bracket(cv, sx0, sy0 + 13, 1, -1, GREEN)
        bracket(cv, sx1, sy0 + 13, -1, -1, GREEN)
        mid = (sx0 + sx1) // 2
        for i, (word, c) in enumerate((("NET", KHAKI), ("OPEN", GREEN))):
            cv.text(F3, mid - text_width(F3, word) // 2 + 1, sy0 + 2 + i * 6, word, c)

    # Right column under the rule: three stacked rank chevrons, point up, 45 degree arms
    # two pixels thick with a keyline shadow, brightest on top.
    ccx = (sx0 + sx1) // 2 if sx1 - sx0 >= 24 else W - 24
    for k, c in enumerate((GREEN, DIM, OLIVE_HI)):
        top = ry + 6 + k * 6
        for i in range(0, 9):
            for th in range(3):
                y = top + i + th
                col = SHADOW if th == 2 else c
                for x in (ccx - i, ccx + i):
                    if col == SHADOW and cv.img.getpixel((x, y)) != rgba(BG):
                        continue
                    cv.set(x, y, col)

    # Bottom livery: a chevron band between keylines.
    by = H - 9
    cv.hline(0, W - 1, by - 1, OLIVE)
    cv.rect(0, by, W - 1, by + 6, SHADOW)
    chevrons(cv, 0, by + 1, W - 1, by + 5, OLIVE_HI)
    cv.hline(0, W - 1, by + 7, OLIVE)
    cv.hline(0, W - 1, H - 1, BG)
    return cv


def emblem():
    cv = Canvas(48, 48, None)
    crest(cv, 8, 1)
    return cv


def main():
    os.makedirs(OUT, exist_ok=True)
    banner().save(os.path.join(OUT, "banner.png"))
    emblem().save(os.path.join(OUT, "emblem.png"))
    print("wrote", OUT)


if __name__ == "__main__":
    main()
