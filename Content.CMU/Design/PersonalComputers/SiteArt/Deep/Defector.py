"""Draws the masthead and emblem for Diary of a Defector (defector.deep), a hidden deep web site.

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/Deep/Defector.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Deep/Defector/{banner,emblem}.png.

A ruled notebook page in the dark: faint ink lines, a rust margin, a Union star scratched out with
pale ink, the title in worn hand-stamped capitals with one torn glitch slice, and a running tally of
days since the desertion. Everything is drawn at 1x on the pixel grid without antialiasing, then
upscaled 2x with NEAREST. Pillow only.
"""
import math
import os

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "Deep", "Defector")
SCALE = 2

# Site palette, shared with the pages' CMUNetStyle.
BG = "#12100C"         # burnt notebook black, the page background
RULE = "#1F1B14"       # faint ruled lines
MARGIN = "#4A1A12"     # the rust margin line
PAPER = "#CFC4A8"      # page text, the pale ink
PAPER_DIM = "#7D735E"
RUST = "#C9442C"       # headings, the Union red gone to rust
RUST_DK = "#7A2318"
RUST_HI = "#E0664A"
TEAL = "#7FB7A8"       # links, and the cold half of the glitch split
SHADOW = "#070604"

# 5x7 capitals.
F5 = {
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "C": [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "F": ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    " ": ["...", "...", "...", "...", "...", "...", "..."],
}

# 3x5 capitals and digits.
F3 = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"], "B": ["##.", "#.#", "##.", "#.#", "##."],
    "C": [".##", "#..", "#..", "#..", ".##"], "D": ["##.", "#.#", "#.#", "#.#", "##."],
    "E": ["###", "#..", "##.", "#..", "###"], "F": ["###", "#..", "##.", "#..", "#.."],
    "G": [".##", "#..", "#.#", "#.#", ".##"], "H": ["#.#", "#.#", "###", "#.#", "#.#"],
    "I": ["###", ".#.", ".#.", ".#.", "###"], "K": ["#.#", "#.#", "##.", "#.#", "#.#"],
    "L": ["#..", "#..", "#..", "#..", "###"], "M": ["#...#", "##.##", "#.#.#", "#...#", "#...#"],
    "N": ["#..#", "##.#", "#.##", "#..#", "#..#"], "O": [".#.", "#.#", "#.#", "#.#", ".#."],
    "P": ["##.", "#.#", "##.", "#..", "#.."], "R": ["##.", "#.#", "##.", "#.#", "#.#"],
    "S": [".##", "#..", ".#.", "..#", "##."], "T": ["###", ".#.", ".#.", ".#.", ".#."],
    "U": ["#.#", "#.#", "#.#", "#.#", "###"], "V": ["#.#", "#.#", "#.#", "#.#", ".#."],
    "W": ["#...#", "#...#", "#.#.#", "##.##", "#...#"], "Y": ["#.#", "#.#", ".#.", ".#.", ".#."],
    "0": ["###", "#.#", "#.#", "#.#", "###"], "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["##.", "..#", ".#.", "#..", "###"], "3": ["##.", "..#", ".#.", "..#", "##."],
    "4": ["#.#", "#.#", "###", "..#", "..#"], "5": ["###", "#..", "##.", "..#", "##."],
    "6": [".##", "#..", "###", "#.#", "###"], "7": ["###", "..#", ".#.", ".#.", ".#."],
    "8": ["###", "#.#", "###", "#.#", "###"], "9": ["###", "#.#", "###", "..#", "##."],
    ".": [".", ".", ".", ".", "#"], "-": ["...", "...", "###", "...", "..."],
    "/": ["..#", "..#", ".#.", "#..", "#.."], ":": [".", "#", ".", "#", "."],
    " ": ["..", "..", "..", "..", ".."],
}


def rgba(c):
    c = c.lstrip("#")
    return (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), 255)


class Rng:
    """Small deterministic LCG so every run draws the same wear."""

    def __init__(self, seed):
        self.s = seed

    def next(self):
        self.s = (self.s * 1103515245 + 12345) & 0x7FFFFFFF
        return self.s / 0x7FFFFFFF


class Canvas:
    def __init__(self, w, h, bg=None):
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

    def line(self, x0, y0, x1, y1, c):
        """Bresenham, no antialiasing."""
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

    def text(self, font, x, y, s, colour, gap=1, scale=1, wear=None, shadow=None):
        """Draws s with its top left at (x, y). wear (an Rng and a rate) chips pixels off the
        stamped letters, as a worn rubber stamp would."""
        if shadow:
            self.text(font, x + 1, y + 1, s, shadow, gap, scale)
        for ch in s:
            g = font.get(ch, font[" "])
            for gy, row in enumerate(g):
                for gx, bit in enumerate(row):
                    if bit != "#":
                        continue
                    for sy in range(scale):
                        for sx in range(scale):
                            if wear and wear[0].next() < wear[1]:
                                continue
                            self.set(x + gx * scale + sx, y + gy * scale + sy, colour)
            x += (len(g[0]) + gap) * scale
        return x - gap * scale

    def save(self, path):
        self.img.resize((self.w * SCALE, self.h * SCALE), Image.NEAREST).save(path)


def text_width(font, s, gap=1, scale=1):
    return sum((len(font.get(ch, font[" "])[0]) + gap) * scale for ch in s) - gap * scale


def in_triangle(px, py, a, b, c):
    def side(p, q, r):
        return (p[0] - r[0]) * (q[1] - r[1]) - (q[0] - r[0]) * (p[1] - r[1])
    d1, d2, d3 = side((px, py), a, b), side((px, py), b, c), side((px, py), c, a)
    return not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0))


def polar(cx, cy, r, a):
    return (cx + r * math.sin(a), cy - r * math.cos(a))


def star_mask(ex, ey, r):
    """Pixels of a five point star centred on pixel (ex, ey), sampled at pixel centres."""
    cx, cy = ex + 0.5, ey + 0.5
    inner = r * 0.40
    lit, shade = set(), set()
    for y in range(ey - r - 1, ey + r + 2):
        for x in range(ex - r - 1, ex + r + 2):
            px, py = x + 0.5, y + 0.5
            for i in range(5):
                a = i * 2 * math.pi / 5
                tip = polar(cx, cy, r, a)
                left = polar(cx, cy, inner, a - math.pi / 5)
                right = polar(cx, cy, inner, a + math.pi / 5)
                if in_triangle(px, py, (cx, cy), left, tip):
                    lit.add((x, y))
                    break
                if in_triangle(px, py, (cx, cy), tip, right):
                    shade.add((x, y))
                    break
    return lit, shade


def emblem(cv, ex, ey, r=20, seed=7):
    """The Union star in faded rust, its edges flaking, scratched out with three hard strokes of
    pale ink and a short tally of days under it."""
    rng = Rng(seed)
    lit, shade = star_mask(ex, ey, r)
    every = lit | shade
    for (x, y) in every:
        edge = any((x + dx, y + dy) not in every for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        if edge and rng.next() < 0.22:
            continue  # flaked paint along the rim
        if edge:
            cv.set(x, y, RUST_HI if (x, y) in lit else RUST)
        else:
            cv.set(x, y, RUST if (x, y) in lit else RUST_DK)
    # Scratches: two crossing strokes and a third underline, each two pixels wide in places.
    strokes = [
        (ex - r + 2, ey - r + 6, ex + r - 3, ey + r - 4),
        (ex + r - 2, ey - r + 4, ex - r + 4, ey + r - 3),
        (ex - r + 6, ey + 2, ex + r - 5, ey - 1),
    ]
    for i, (x0, y0, x1, y1) in enumerate(strokes):
        cv.line(x0 + 1, y0 + 1, x1 + 1, y1 + 1, SHADOW)
        cv.line(x0, y0, x1, y1, PAPER)
        if i < 2:
            cv.line(x0 + 1, y0, x1 + 1, y1, PAPER_DIM)


def tally(cv, x, y, count, h=7, colour=PAPER, strike=RUST):
    """Prison-wall tally marks, gates of five. Returns the x after the last mark."""
    rng = Rng(count * 31 + 5)
    full, rest = divmod(count, 5)
    for g in range(full + (1 if rest else 0)):
        marks = 4 if g < full else rest
        gx = x
        for m in range(marks):
            lean = 1 if rng.next() < 0.3 else 0
            cv.line(x + lean, y, x, y + h - 1, colour)
            x += 2
        if g < full:
            cv.line(gx - 1, y + h - 2, x - 1, y + 1, strike)
        x += 3
    return x


def glitch_slice(cv, x0, x1, y0, y1, shift):
    """Tears a band of rows sideways and splits its ink into a rust and a teal ghost."""
    band = {}
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            band[(x, y)] = cv.get(x, y)
    paper = rgba(PAPER)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            cv.set(x, y, BG if y % 3 else RULE)
    for (x, y), c in band.items():
        if c == paper:
            cv.set(x + shift - 1, y, RUST)
            cv.set(x + shift + 1, y, TEAL)
    for (x, y), c in band.items():
        if c == paper:
            cv.set(x + shift, y, PAPER)


def banner():
    W, H = 264, 66
    cv = Canvas(W, H, BG)

    # Ruled notebook page: faint lines every six rows and a rust margin.
    for y in range(5, H, 6):
        cv.rect(0, y, W - 1, y, RULE)
    cv.rect(62, 0, 62, H - 1, MARGIN)
    cv.rect(64, 0, 64, H - 1, MARGIN)

    # Torn top edge: a ragged strip of near black.
    rng = Rng(19)
    for x in range(W):
        for y in range(int(rng.next() * 3)):
            cv.set(x, y, SHADOW)

    emblem(cv, 31, 30)
    tally(cv, 11, 55, 14, h=6, colour=PAPER_DIM)

    left = 72
    cv.text(F5, left, 8, "DIARY OF A", PAPER_DIM, gap=2, shadow=SHADOW)
    title = "DEFECTOR"
    wear = (Rng(3), 0.035)
    end = cv.text(F5, left, 18, title, PAPER, gap=1, scale=3, wear=wear, shadow=None)
    # Underline in rust, slightly uneven, the pen pressed hard.
    for x in range(left, end + 1):
        cv.set(x, 41 + (1 if (x // 23) % 2 else 0), RUST)
    # One torn row band through the title.
    glitch_slice(cv, left - 2, end + 4, 27, 29, 3)

    sub = "WHAT THE UNION DOES NOT WANT WRITTEN"
    cv.text(F3, left, 47, sub, TEAL)
    days = "DAY 211 - STILL HERE"
    cv.text(F3, left, 56, days, PAPER_DIM)
    tx = left + text_width(F3, days) + 6
    tally(cv, tx, 55, 23, h=7)

    addr = "DEFECTOR.DEEP"
    cv.text(F3, W - 6 - text_width(F3, addr), 9, addr, RUST)
    return cv


def emblem_png():
    cv = Canvas(46, 46)
    emblem(cv, 22, 22)
    return cv


def main():
    os.makedirs(OUT, exist_ok=True)
    banner().save(os.path.join(OUT, "banner.png"))
    emblem_png().save(os.path.join(OUT, "emblem.png"))


if __name__ == "__main__":
    main()
