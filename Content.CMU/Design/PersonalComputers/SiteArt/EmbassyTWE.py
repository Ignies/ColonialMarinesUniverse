"""Draws the masthead and emblem for embassy.twe, the Three World Empire's embassy on the ExtraNet.

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/EmbassyTWE.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/EmbassyTWE/{banner,emblem}.png.
Everything is drawn pixel by pixel at 1x with hand-built bitmap letters, then upscaled 2x with
nearest-neighbour so the page shows hard pixel edges.

The emblem is a crown over a roundel with the sun rising from a horizon, and three orbs below it for
Earth, Mars and Titan. The livery is navy with red, white and light blue bands.
"""
import math
import os

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites",
                   "EmbassyTWE")
SCALE = 2

NAVY = "#0C1638"
NAVY_DEEP = "#070D24"
NAVY_LIFT = "#16245A"
NAVY_RIM = "#22336F"
WHITE = "#E6E9F2"
WHITE_DIM = "#AEB5CC"
RED = "#E0403A"
RED_DEEP = "#9C2621"
BLUE = "#9DB4FF"
BLUE_DEEP = "#5D74C4"

# 5x7 capitals for the small lines (I is three wide, punctuation narrower).
SMALL = {
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "B": ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
    "C": [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "F": ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
    "G": [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####"],
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "M": ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    "N": ["#...#", "##..#", "##..#", "#.#.#", "#..##", "#..##", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "0": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "2": [".###.", "#...#", "....#", "..##.", ".#...", "#....", "#####"],
    "8": [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
    ".": [".", ".", ".", ".", ".", ".", "#"],
    " ": ["...", "...", "...", "...", "...", "...", "..."],
}

# 11 row Roman capitals for the name: two pixel stems, one pixel hairlines, one pixel serifs.
BIG = {
    "T": ["########", "#..##..#", "...##...", "...##...", "...##...", "...##...",
          "...##...", "...##...", "...##...", "...##...", "..####.."],
    "H": ["###..###", ".##..##.", ".##..##.", ".##..##.", ".##..##.", ".######.",
          ".##..##.", ".##..##.", ".##..##.", ".##..##.", "###..###"],
    "R": ["######..", ".##..##.", ".##..##.", ".##..##.", ".##..##.", ".#####..",
          ".##.##..", ".##..##.", ".##..##.", ".##..##.", "###..###"],
    "E": ["#######", ".##...#", ".##....", ".##....", ".##..#.", ".#####.",
          ".##..#.", ".##....", ".##....", ".##...#", "#######"],
    "W": ["###....###", ".##....##.", ".##....##.", ".##....##.", ".##.##.##.", ".##.##.##.",
          ".##.##.##.", ".##.##.##.", ".########.", "..##..##..", "..#....#.."],
    "O": ["..####..", ".##..##.", "##....##", "##....##", "##....##", "##....##",
          "##....##", "##....##", "##....##", ".##..##.", "..####.."],
    "L": ["####...", ".##....", ".##....", ".##....", ".##....", ".##....",
          ".##....", ".##....", ".##....", ".##...#", "#######"],
    "D": ["######..", ".##..##.", ".##...##", ".##...##", ".##...##", ".##...##",
          ".##...##", ".##...##", ".##...##", ".##..##.", "######.."],
    "M": ["###....###", ".###..###.", ".##.##.##.", ".##.##.##.", ".##.##.##.", ".##....##.",
          ".##....##.", ".##....##.", ".##....##.", ".##....##.", "###....###"],
    "P": ["######..", ".##..##.", ".##..##.", ".##..##.", ".##..##.", ".#####..",
          ".##.....", ".##.....", ".##.....", ".##.....", "####...."],
    "I": ["####", ".##.", ".##.", ".##.", ".##.", ".##.", ".##.", ".##.", ".##.", ".##.", "####"],
    " ": ["....."] * 11,
}

# Crown, 21x11: three orbed points over a jewelled band. W white, w dim white, R red, B light blue.
CROWN = [
    "..W.......W.......W..",
    ".WWW.....WWW.....WWW.",
    "..W.......W.......W..",
    "..WW.....WWW.....WW..",
    "..WWW...WWWWW...WWW..",
    "..WWWW.WWWRWWW.WWWW..",
    "..WWWWWWWRRRWWWWWWW..",
    "..WWWWWWWWRWWWWWWWW..",
    ".wwwwwwwwwwwwwwwwwww.",
    ".WRWWWBWWWRWWWBWWWRW.",
    ".wwwwwwwwwwwwwwwwwww.",
]
CROWN_COLOURS = {"W": WHITE, "w": WHITE_DIM, "R": RED, "B": BLUE}


def rgba(c):
    c = c.lstrip("#")
    return (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), 255)


class Canvas:
    def __init__(self, w, h, background=None):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), rgba(background) if background else (0, 0, 0, 0))

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

    def bitmap(self, x, y, rows, colours):
        for gy, row in enumerate(rows):
            for gx, ch in enumerate(row):
                if ch in colours:
                    self.set(x + gx, y + gy, colours[ch])

    def text(self, font, x, y, text, c, gap=1, shadow=None):
        if shadow:
            self.text(font, x + 1, y + 1, text, shadow, gap)
        for ch in text:
            g = font[ch]
            self.bitmap(x, y, g, {"#": c})
            x += len(g[0]) + gap
        return x

    def save(self, path):
        big = self.img.resize((self.w * SCALE, self.h * SCALE), Image.NEAREST)
        big.save(path)


def text_width(font, text, gap=1):
    return sum(len(font[ch][0]) + gap for ch in text) - gap


def emblem(cv, ox, oy):
    """Crown over a rising-sun roundel with the three worlds below the horizon. 39 wide, 48 tall."""
    cx, cy, r = ox + 19, oy + 28, 19          # roundel centre and radius
    horizon = cy + 4
    sun_r = 7
    # Roundel: red rim, white line, then the field.
    for y in range(cy - r - 1, cy + r + 2):
        for x in range(cx - r - 1, cx + r + 2):
            d = math.hypot(x - cx, y - cy)
            if d > r + 0.5:
                continue
            if d > r - 0.7:
                cv.set(x, y, RED)
            elif d > r - 1.7:
                cv.set(x, y, WHITE)
            elif y < horizon:
                # Sky: alternating rays fanning from the sun, cut into 13 wedges over the upper half.
                # dx is measured from the centre column so the fan mirrors exactly about it.
                ang = math.atan2(horizon - y + 0.5, x - cx)
                wedge = int(ang / (math.pi / 13))
                cv.set(x, y, RED if wedge % 2 == 0 else NAVY_LIFT)
            elif y == horizon:
                cv.set(x, y, WHITE)
            else:
                cv.set(x, y, NAVY)
    # The sun, half risen, with a white rim.
    for y in range(horizon - sun_r - 1, horizon):
        for x in range(cx - sun_r - 1, cx + sun_r + 2):
            d = math.hypot(x - cx, y - horizon + 0.5)
            if d <= sun_r - 0.5:
                cv.set(x, y, RED)
            elif d <= sun_r + 0.6:
                cv.set(x, y, WHITE)
    # Three worlds below the horizon: Earth, Mars, Titan.
    disc = [".###.", "#####", "#####", "#####", ".###."]
    for i, col in enumerate((BLUE, RED, WHITE_DIM)):
        wx = cx - 12 + i * 10
        cv.bitmap(wx, horizon + 4, disc, {"#": col})
        cv.set(wx + 1, horizon + 5, WHITE)
    # Crown resting on the rim.
    cv.bitmap(cx - 10, oy, CROWN, CROWN_COLOURS)


def livery(cv, y, flip=False):
    """Red, white and blue bands with navy pinstripes between."""
    bands = [(RED, 3), (WHITE, 1), (NAVY, 1), (BLUE, 1)]
    if flip:
        bands = bands[::-1]
    for c, n in bands:
        cv.rect(0, y, cv.w - 1, y + n - 1, c)
        y += n


def banner():
    W, H = 270, 72
    cv = Canvas(W, H, NAVY)
    livery(cv, 0)
    livery(cv, H - 6, flip=True)

    # Emblem panel on the left with a two tone divider. The emblem's left margin matches the text's
    # right margin, and the divider sits midway between emblem and text.
    emblem(cv, 16, 12)
    cv.vline(69, 14, 57, WHITE)
    cv.vline(71, 14, 57, RED)

    # Text block in the space right of the divider.
    name = "THREE WORLD EMPIRE"
    name_w = text_width(BIG, name, gap=2)
    x0 = W - 16 - name_w

    top = "EMBASSY OF THE"
    cv.text(SMALL, x0, 15, top, BLUE, gap=2)
    est = "EST. 2088"
    cv.text(SMALL, x0 + name_w - text_width(SMALL, est), 15, est, WHITE_DIM)

    cv.text(BIG, x0, 26, name, WHITE, gap=2, shadow=NAVY_DEEP)

    cv.hline(x0, x0 + name_w - 1, 40, RED)
    cv.hline(x0, x0 + name_w - 1, 41, RED_DEEP)
    cv.hline(x0, x0 + name_w - 1, 43, WHITE_DIM)

    # The three founding worlds, spread under the name with a small red sun between each.
    a, b, c = "EARTH", "MARS", "TITAN"
    xa = x0
    xc = x0 + name_w - text_width(SMALL, c)
    xb = x0 + (name_w - text_width(SMALL, b)) // 2
    cv.text(SMALL, xa, 47, a, WHITE)
    cv.text(SMALL, xb, 47, b, WHITE)
    cv.text(SMALL, xc, 47, c, WHITE)
    for l, r in ((xa + text_width(SMALL, a), xb), (xb + text_width(SMALL, b), xc)):
        mx = (l + r) // 2
        cv.bitmap(mx - 1, 49, [".#.", "###", ".#."], {"#": RED})
    cv.save(os.path.join(OUT, "banner.png"))


def emblem_png():
    # Square canvas so the lore pages lay it out as a square tile; the emblem is 39x48 inside it.
    cv = Canvas(50, 50)
    emblem(cv, 5, 1)
    cv.save(os.path.join(OUT, "emblem.png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    banner()
    emblem_png()


if __name__ == "__main__":
    main()
