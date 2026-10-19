"""Draws the masthead for embassy.ua, the ExtraNet board of the Embassy of the United Americas.

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/EmbassyUA.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/EmbassyUA/banner.png and emblem.png.

Everything is drawn at 1x on the pixel grid with no antialiasing, then upscaled 2x with nearest neighbour.
The seal carries the twelve-point star of the UA flag on deep blue, ringed by thirteen stars and a gold
band. The wordmark is a hand-built bold bitmap face; the small lines use a 5x7 and a 3x5 bitmap face.
The livery is the mission's blue, off-white and gold, and a hanging flag on the right carries
the UA flag's canton and stripes.
"""
import math
import os

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "EmbassyUA")

SCALE = 2
W, H = 276, 72

BLUE = "#0B2545"      # site background
BLUE_MID = "#13315C"  # seal star band, ribbon shadow
BLUE_DEEP = "#071a33"  # drop shadows
WHITE = "#EEF4ED"
GOLD = "#F4D35E"
GOLD_HI = "#FBE9A6"
GOLD_LO = "#C9A63A"
STEEL = "#8DA9C4"
STEEL_LO = "#5F7C9A"
FOLD = "#3F5C7C"

# Bold wordmark face: 10 rows, two pixel strokes.
BIG = {
    "U": ["##...##"] * 8 + ["#######", ".#####."],
    "N": ["##...##", "###..##", "###..##", "####.##", "####.##",
          "##.####", "##.####", "##..###", "##..###", "##...##"],
    "I": ["######", "######"] + ["..##.."] * 6 + ["######", "######"],
    "T": ["########", "########"] + ["...##..."] * 8,
    "E": ["#######", "#######", "##.....", "##.....", "######.",
          "######.", "##.....", "##.....", "#######", "#######"],
    "D": ["######.", "#######"] + ["##...##"] * 6 + ["#######", "######."],
    "A": [".#####.", "#######", "##...##", "##...##", "#######",
          "#######", "##...##", "##...##", "##...##", "##...##"],
    "M": ["##.....##", "###...###", "####.####", "##.###.##", "##..#..##",
          "##.....##", "##.....##", "##.....##", "##.....##", "##.....##"],
    "R": ["######.", "#######", "##...##", "##...##", "#######",
          "######.", "##..##.", "##..###", "##...##", "##...##"],
    "C": [".######", "#######"] + ["##....."] * 6 + ["#######", ".######"],
    "S": [".######", "#######", "##.....", "##.....", "######.",
          ".######", ".....##", ".....##", "#######", "######."],
    " ": ["....."] * 10,
}

# 5x7 capitals.
F5 = {
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
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "-": ["....", "....", "....", "####", "....", "....", "...."],
    " ": ["...", "...", "...", "...", "...", "...", "..."],
}

# 3x5 capitals for the footer line, wider where three columns cannot carry the letter.
F3 = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"], "C": [".##", "#..", "#..", "#..", ".##"],
    "D": ["##.", "#.#", "#.#", "#.#", "##."], "E": ["###", "#..", "##.", "#..", "###"],
    "I": ["###", ".#.", ".#.", ".#.", "###"], "L": ["#..", "#..", "#..", "#..", "###"],
    "M": ["#...#", "##.##", "#.#.#", "#...#", "#...#"], "N": ["#..#", "##.#", "#.##", "#..#", "#..#"],
    "O": [".#.", "#.#", "#.#", "#.#", ".#."], "P": ["##.", "#.#", "##.", "#..", "#.."],
    "R": ["##.", "#.#", "##.", "#.#", "#.#"], "S": [".##", "#..", ".#.", "..#", "##."],
    "T": ["###", ".#.", ".#.", ".#.", ".#."], "U": ["#.#", "#.#", "#.#", "#.#", "###"],
    "-": ["...", "...", "###", "...", "..."], " ": ["..", "..", "..", "..", ".."],
}

STAR5 = ["..#..", ".###.", "#####", ".###.", ".#.#."]


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

    def bitmap(self, rows, x, y, c):
        for gy, row in enumerate(rows):
            for gx, bit in enumerate(row):
                if bit == "#":
                    self.set(x + gx, y + gy, c(gy) if callable(c) else c)

    def text(self, font, x, y, text, c, gap=1, shadow=None):
        if shadow:
            self.text(font, x + 1, y + 1, text, shadow, gap)
        for ch in text:
            g = font[ch]
            self.bitmap(g, x, y, c)
            x += len(g[0]) + gap

    def save(self, path):
        big = self.img.resize((self.w * SCALE, self.h * SCALE), Image.NEAREST)
        big.save(path)


def text_width(font, text, gap=1):
    return sum(len(font[ch][0]) + gap for ch in text) - gap


def in_polygon(px, py, pts):
    inside = False
    j = len(pts) - 1
    for i in range(len(pts)):
        xi, yi = pts[i]
        xj, yj = pts[j]
        if (yi > py) != (yj > py) and px < (xj - xi) * (py - yi) / (yj - yi) + xi:
            inside = not inside
        j = i
    return inside


def burst(cx, cy, outer, inner, points=12):
    """The flag's star as a polygon, first point straight up."""
    pts = []
    for k in range(points * 2):
        a = -math.pi / 2 + k * math.pi / points
        r = outer if k % 2 == 0 else inner
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def seal(cv, x0, y0, size=56):
    """The federal seal: gold rim, a blue band of thirteen stars, a thin gold ring and the flag's
    twelve-point star on deep blue. Every pixel is classified by its centre, so it is symmetric."""
    c = size / 2
    star_pts = burst(c, c, 12.5, 6.2)
    for y in range(size):
        for x in range(size):
            px, py = x + 0.5, y + 0.5
            d = math.hypot(px - c, py - c)
            if d > 28:
                continue
            if d > 26.6:
                col = GOLD_LO if py > c + 8 else GOLD
            elif d > 25.6:
                col = GOLD_HI if py < c - 8 else GOLD
            elif d > 24.6:
                col = BLUE_DEEP
            elif d > 16.6:
                col = BLUE_MID
            elif d > 15.6:
                col = GOLD
            elif in_polygon(px, py, star_pts):
                col = WHITE
            else:
                col = BLUE
            cv.set(x0 + x, y0 + y, col)
    # Thirteen stars around the band, the first at the top.
    for k in range(13):
        a = -math.pi / 2 + k * 2 * math.pi / 13
        sx = round(c + 20.6 * math.cos(a) - 2.5)
        sy = round(c + 20.6 * math.sin(a) - 2.5)
        cv.bitmap(STAR5, x0 + sx, y0 + sy, WHITE if k else GOLD)


def ribbon(cv, x0, x1, y, text):
    """An off-white ribbon with its tails folded behind, swallow-tailed, and the motto on it."""
    h = 13
    tail = 12
    # Tails sit lower and behind the body.
    for side in (-1, 1):
        ex = x0 if side < 0 else x1
        for i in range(tail):
            tx = ex - (i + 1) if side < 0 else ex + (i + 1)
            for ty in range(y + 3, y + h + 3):
                cut = abs((ty - (y + 3)) - (h - 1) / 2)
                # Swallowtail notch at the outer end.
                if i >= tail - 4 and cut < (i - (tail - 5)) * 1.2:
                    continue
                if i < 2:
                    col = FOLD  # where the tail turns behind the body
                elif ty < y + h + 1:
                    col = STEEL
                else:
                    col = STEEL_LO
                cv.set(tx, ty, col)
    # Body.
    cv.rect(x0, y, x1, y + h - 1, WHITE)
    cv.rect(x0, y, x1, y, GOLD)
    cv.rect(x0, y + h - 1, x1, y + h - 1, GOLD)
    cv.rect(x0, y + h - 2, x1, y + h - 2, STEEL)
    tw = text_width(F5, text)
    cv.text(F5, (x0 + x1 + 1 - tw) // 2, y + 3, text, BLUE)


def pennant(cv, x0, x1, y0, y1):
    """A hanging flag in the mission livery: the UA flag's canton and stripes, a gold staff above,
    gold edging and a swallowtail foot."""
    w = x1 - x0 + 1
    notch = w // 2
    # Staff with finials.
    cv.rect(x0 - 2, y0, x1 + 2, y0 + 1, GOLD)
    cv.set(x0 - 3, y0, GOLD_LO)
    cv.set(x1 + 3, y0, GOLD_LO)
    top = y0 + 2
    canton = top + 15
    for y in range(top, y1 + 1):
        for x in range(x0, x1 + 1):
            # Swallowtail: a V cut rising from the foot.
            rise = y1 - y
            off = abs(x - (x0 + x1) / 2)
            if rise < notch and off < notch - rise:
                continue
            edge = x in (x0, x1)
            if edge:
                col = GOLD
            elif y < canton:
                col = BLUE_MID
            else:
                band = (y - canton) // 3
                col = WHITE if band % 2 == 0 else STEEL_LO
            cv.set(x, y, col)
    # Gold edging along the swallowtail cut.
    for y in range(top, y1 + 1):
        rise = y1 - y
        if rise < notch:
            for x in range(x0, x1 + 1):
                off = abs(x - (x0 + x1) / 2)
                if notch - rise - 1 <= off < notch - rise:
                    cv.set(x, y, GOLD)
    cv.rect(x0, canton - 1, x1, canton - 1, GOLD)
    # The canton's star: the flag's burst, cut down to eight rays to read at this size.
    ray = ["...#...", ".#.#.#.", "..###..", "#######", "..###..", ".#.#.#.", "...#..."]
    cv.bitmap(ray, (x0 + x1) // 2 - 3, top + 4, WHITE)


def banner():
    cv = Canvas(W, H, BLUE)
    # Livery rules top and bottom.
    cv.rect(0, 2, W - 1, 3, GOLD)
    cv.rect(0, 5, W - 1, 5, STEEL)
    cv.rect(0, H - 6, W - 1, H - 6, STEEL)
    cv.rect(0, H - 4, W - 1, H - 3, GOLD)

    seal(cv, 8, 8)

    left = 72
    right = 240
    mid = (left + right + 1) // 2

    head = "EMBASSY OF THE"
    cv.text(F5, mid - text_width(F5, head, 2) // 2, 11, head, STEEL, gap=2)

    name = "UNITED AMERICAS"
    ramp = [GOLD_HI, GOLD_HI, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD_LO, GOLD_LO]
    nw = text_width(BIG, name, 2)
    cv.text(BIG, mid - nw // 2, 21, name, lambda gy: ramp[gy], gap=2, shadow=BLUE_DEEP)

    # Gold rule with a star at its centre, under the name.
    cv.rect(mid - nw // 2, 35, mid - 5, 35, GOLD_LO)
    cv.rect(mid + 4, 35, mid - nw // 2 + nw - 1, 35, GOLD_LO)
    cv.bitmap(STAR5, mid - 2, 33, GOLD)

    ribbon(cv, left + 10, right - 10, 40, "ONE PEOPLE - MANY WORLDS")

    foot = "DIPLOMATIC MISSION - NEROID SECTOR"
    cv.text(F3, mid - text_width(F3, foot) // 2, 59, foot, STEEL)

    pennant(cv, W - 27, W - 9, 8, 63)
    return cv


def emblem():
    cv = Canvas(56, 56)
    seal(cv, 0, 0)
    return cv


def main():
    os.makedirs(OUT, exist_ok=True)
    banner().save(os.path.join(OUT, "banner.png"))
    emblem().save(os.path.join(OUT, "emblem.png"))


if __name__ == "__main__":
    main()
