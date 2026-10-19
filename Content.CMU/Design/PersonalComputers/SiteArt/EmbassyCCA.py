"""Draws the masthead and emblem for the CCA Diplomatic Mission ExtraNet site (embassy.cca).

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/EmbassyCCA.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/EmbassyCCA/{banner,emblem}.png.

Everything is drawn at 1x on the pixel grid with no antialiasing, from hand-built bitmap glyphs and
per-pixel shapes, then upscaled 2x with nearest neighbour. Palette follows the site style in boards.yml:
warm dark brown ground, sand text, gold headings, amber links, plus the confederation's livery of
gold, green and red earth.

The emblem is the confederation's sun: a gold disc with a ring of rays, the continent of Africa laid
over it in green, all inside a gold ring.
"""
import math
import os

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "EmbassyCCA")
SCALE = 2

BG = "#2B2118"
BG_HI = "#362A1F"
SAND = "#EFE4D2"
GOLD = "#D9A441"
GOLD_DK = "#9C6F25"
AMBER = "#E8C27A"
GREEN = "#3E7D3A"
GREEN_DK = "#24502A"
GREEN_HI = "#5C9E4C"
RED = "#A4472B"
RED_DK = "#6B2C1C"
SHADOW = "#170F09"

# 5x7 capitals.
F5 = {
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "B": ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
    "C": [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "F": ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "M": ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    " ": ["...", "...", "...", "...", "...", "...", "..."],
}

# 3x5 capitals for the motto line, wider where three columns cannot carry the letter.
F3 = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"],
    "E": ["###", "#..", "##.", "#..", "###"],
    "L": ["#..", "#..", "#..", "#..", "###"],
    "M": ["#...#", "##.##", "#.#.#", "#...#", "#...#"],
    "N": ["#..#", "##.#", "#.##", "#..#", "#..#"],
    "O": ["###", "#.#", "#.#", "#.#", "###"],
    "P": ["##.", "#.#", "##.", "#..", "#.."],
    "S": [".##", "#..", ".#.", "..#", "##."],
    "U": ["#.#", "#.#", "#.#", "#.#", "###"],
    "Y": ["#.#", "#.#", ".#.", ".#.", ".#."],
    ",": [".", ".", ".", ".", "#", "#"],  # drops one pixel below the baseline
    " ": ["..", "..", "..", "..", ".."],
}

# The continent, row by row as inclusive (left, right) spans on a 20 x 22 grid, plotted from the real
# coastline at about 3.5 degrees a pixel: the Maghreb, the notch of the Gulf of Sidra, the Horn, the
# step of the Gulf of Guinea and the Cape. Madagascar sits apart across the Mozambique Channel.
AFRICA = [
    [(3, 8)], [(2, 9), (11, 14)], [(1, 14)], [(0, 14)], [(0, 15)], [(0, 15)], [(0, 16)], [(0, 19)],
    [(1, 18)], [(2, 18)], [(7, 17)], [(7, 16)], [(8, 16)], [(8, 16)], [(8, 16)], [(8, 16)],
    [(8, 15)], [(9, 15)], [(9, 14)], [(10, 13)], [(10, 12)], [(10, 11)],
]
MADAGASCAR = [(14, 19, 19), (15, 18, 19), (16, 18, 19), (17, 18, 18)]


def rgb(c):
    c = c.lstrip("#")
    return tuple(int(c[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


class Canvas:
    def __init__(self, w, h, fill=None):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), rgb(fill) if fill else (0, 0, 0, 0))
        self.px = self.img.load()

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = rgb(c)

    def get(self, x, y):
        return self.px[x, y]

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def glyphs(self, font, x, y, text, c, gap=1, scale=1):
        for ch in text:
            g = font[ch]
            for gy, row in enumerate(g):
                for gx, bit in enumerate(row):
                    if bit == "#":
                        self.rect(x + gx * scale, y + gy * scale, x + gx * scale + scale - 1, y + gy * scale + scale - 1, c)
            x += (len(g[0]) + gap) * scale

    def paste(self, other, x, y):
        for oy in range(other.h):
            for ox in range(other.w):
                p = other.get(ox, oy)
                if p[3]:
                    self.px[x + ox, y + oy] = p

    def save(self, path):
        big = self.img.resize((self.w * SCALE, self.h * SCALE), Image.NEAREST)
        big.save(path)


def text_width(font, text, gap=1, scale=1):
    return (sum(len(font[ch][0]) + gap for ch in text) - gap) * scale


def emblem():
    """48 x 48: a gold ring, a rayed sun, and the continent over it."""
    size = 48
    e = Canvas(size, size)
    c = (size - 1) / 2

    def d2(x, y):
        return (x - c) ** 2 + (y - c) ** 2

    for y in range(size):
        for x in range(size):
            r = d2(x, y) ** 0.5
            if 21.6 <= r < 23.4:
                e.set(x, y, GOLD)
            elif 20.6 <= r < 21.6:
                e.set(x, y, SHADOW)
            elif r < 20.6:
                e.set(x, y, BG_HI)

    # Sixteen rays, alternating long and short, between the sun's disc and the inner ring.
    for i in range(16):
        a = i * math.pi / 8
        long_ray = i % 2 == 0
        r0, r1 = 16.0, (19.6 if long_ray else 18.0)
        steps = 24
        for s in range(steps + 1):
            r = r0 + (r1 - r0) * s / steps
            x = int(round(c + r * math.cos(a)))
            y = int(round(c + r * math.sin(a)))
            e.set(x, y, GOLD if long_ray else AMBER)

    for y in range(size):
        for x in range(size):
            r = d2(x, y) ** 0.5
            if r < 13.6:
                e.set(x, y, GOLD)
            elif r < 14.6:
                e.set(x, y, GOLD_DK)

    # The continent in green on the sun, lit from the upper left, with a dark-gold drop shadow so it
    # lifts off the disc and the channel to Madagascar stays open.
    ox, oy = 14, 13
    land = set()
    for row, spans in enumerate(AFRICA):
        for l, r in spans:
            for x in range(l, r + 1):
                land.add((ox + x, oy + row))
    for row, l, r in MADAGASCAR:
        for x in range(l, r + 1):
            land.add((ox + x, oy + row))
    for (x, y) in land:
        if (x + 1, y + 1) not in land:
            e.set(x + 1, y + 1, GOLD_DK)
    for (x, y) in land:
        lit = (x - 1, y) not in land or (x, y - 1) not in land
        dark = (x + 1, y) not in land or (x, y + 1) not in land
        e.set(x, y, GREEN_HI if lit and not dark else GREEN_DK if dark and not lit else GREEN)
    return e


def woven_band(cv, y0, height):
    """A woven strip in the livery colours: gold, green and red-earth blocks split by dark stitches."""
    pattern = [GOLD] * 7 + [BG] + [GREEN] * 3 + [BG] + [RED] * 7 + [BG] + [GREEN] * 3 + [BG]
    for x in range(cv.w):
        col = pattern[x % len(pattern)]
        for y in range(y0, y0 + height):
            if col == BG:
                continue
            # Every other row of the gold and red blocks steps in a shade for a woven look.
            shade = col
            if (y - y0) in (0, height - 1):
                shade = {GOLD: GOLD_DK, RED: RED_DK, GREEN: GREEN_DK}[col]
            cv.set(x, y, shade)


def banner():
    W, H = 272, 78
    cv = Canvas(W, H, BG)

    # Livery bands across the top: gold, green, red earth.
    cv.rect(0, 0, W - 1, 1, GOLD)
    cv.rect(0, 2, W - 1, 3, GREEN)
    cv.rect(0, 4, W - 1, 5, RED)

    em = emblem()
    big = "CCA"
    big_w = text_width(F5, big, gap=1, scale=3)
    lines = ["CENTRAL CONFEDERATION", "OF AFRICA", "DIPLOMATIC MISSION"]
    block_w = max(text_width(F5, t) for t in lines)
    motto = "ONE PEOPLE, MANY SUNS"
    gap_a, gap_b = 9, 8
    total = em.w + gap_a + big_w + gap_b + 1 + gap_b + block_w
    x = (W - total) // 2

    top, bottom = 6, 66  # content area between the bands
    ey = top + (bottom - top - em.h) // 2 + 2
    cv.paste(em, x, ey)
    x += em.w + gap_a

    by = top + (bottom - top - 21) // 2 + 1
    cv.glyphs(F5, x + 1, by + 1, big, RED_DK, scale=3)
    cv.glyphs(F5, x, by, big, GOLD, scale=3)
    # A short three-colour rule under the initials.
    third = big_w // 3
    ry = by + 24
    cv.rect(x, ry, x + third - 1, ry, GOLD)
    cv.rect(x + third + 1, ry, x + 2 * third - 1, ry, GREEN_HI)
    cv.rect(x + 2 * third + 1, ry, x + big_w - 1, ry, RED)
    x += big_w + gap_b

    cv.rect(x, ey + 4, x, ey + em.h - 5, GOLD_DK)
    x += 1 + gap_b

    ty = ey + 5
    cv.glyphs(F5, x, ty, lines[0], SAND)
    cv.glyphs(F5, x, ty + 10, lines[1], SAND)
    cv.rect(x, ty + 20, x + block_w - 1, ty + 20, GOLD_DK)
    cv.glyphs(F5, x, ty + 24, lines[2], AMBER)
    cv.glyphs(F3, x, ty + 34, motto, GOLD)

    woven_band(cv, 70, 5)
    cv.rect(0, 76, W - 1, 77, GOLD)
    return cv, em


def main():
    os.makedirs(OUT, exist_ok=True)
    cv, em = banner()
    cv.save(os.path.join(OUT, "banner.png"))
    em.save(os.path.join(OUT, "emblem.png"))
    print("wrote", OUT)


if __name__ == "__main__":
    main()
