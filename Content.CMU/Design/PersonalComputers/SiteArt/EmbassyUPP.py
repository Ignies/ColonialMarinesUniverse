"""Draws the ExtraNet masthead and emblem for the Embassy of the Union of Progressive Peoples (embassy.upp).

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/EmbassyUPP.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/EmbassyUPP/{banner,emblem}.png.

Socialist-realist poster livery: a bevelled gold star on a red disc with a burst of gold rays, faint
red rays fanning across the masthead, gold and red livery bands, and hand-built bitmap lettering.
Everything is drawn at 1x on the pixel grid without antialiasing, then upscaled 2x with NEAREST.
Pillow only.
"""
import math
import os

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "EmbassyUPP")
SCALE = 2

# Site palette.
BG = "#2A0A0A"        # deep red, the page background
RAY = "#360E0C"       # faint background rays
CREAM = "#F0D9C4"
GOLD = "#FFCC33"
GOLD_DK = "#D99A22"   # shaded half of the star arms, lower half of the big letters
CORAL = "#FF7A5C"
RED = "#B3201E"       # emblem disc and livery band
RED_DK = "#7A1412"
SHADOW = "#140404"

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
    "J": ["..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.."],
    "K": ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "M": ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    "N": ["#...#", "##..#", "##..#", "#.#.#", "#..##", "#..##", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "Q": [".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#"],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "V": ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"],
    "X": ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "Z": ["#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"],
    ".": [".", ".", ".", ".", ".", ".", "#"],
    "-": ["...", "...", "...", "###", "...", "...", "..."],
    "*": [".....", "..#..", "#####", ".###.", ".#.#.", "#...#", "....."],
    " ": ["...", "...", "...", "...", "...", "...", "..."],
}

# 3x5 capitals (wider where three columns cannot carry the letter).
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
    ".": [".", ".", ".", ".", "#"], "-": ["...", "...", "###", "...", "..."],
    " ": ["..", "..", "..", "..", ".."],
}

# Bold condensed 6x10 poster capitals with two pixel stems, only the letters the masthead needs.
BIG = {
    "U": ["##..##"] * 8 + ["######", ".####."],
    "N": ["##..##", "###.##", "###.##", "######", "######", "##.###", "##.###", "##..##", "##..##", "##..##"],
    "I": ["##"] * 10,
    "O": [".####.", "######"] + ["##..##"] * 6 + ["######", ".####."],
    "F": ["######", "######", "##....", "##....", "#####.", "#####.", "##....", "##....", "##....", "##...."],
    "P": ["#####.", "######", "##..##", "##..##", "######", "#####.", "##....", "##....", "##....", "##...."],
    "R": ["#####.", "######", "##..##", "##..##", "######", "#####.", "##.##.", "##..##", "##..##", "##..##"],
    "G": [".####.", "######", "##..##", "##....", "##....", "##.###", "##.###", "##..##", "######", ".####."],
    "E": ["######", "######", "##....", "##....", "#####.", "#####.", "##....", "##....", "######", "######"],
    "S": [".####.", "######", "##..##", "##....", "#####.", ".#####", "....##", "##..##", "######", ".####."],
    "V": ["##..##"] * 6 + [".####.", ".####.", "..##..", "..##.."],
    "L": ["##...."] * 8 + ["######", "######"],
    " ": ["..."] * 10,
}


def rgba(c):
    c = c.lstrip("#")
    return (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), 255)


class Canvas:
    def __init__(self, w, h, bg=None):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), rgba(bg) if bg else (0, 0, 0, 0))

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.img.putpixel((x, y), rgba(c))

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def text(self, font, x, y, s, colour, gap=1, shadow=None, ramp=None):
        """Draws s with its top left at (x, y). ramp maps a glyph row to a colour."""
        if shadow:
            self.text(font, x + 1, y + 1, s, shadow, gap)
        for ch in s:
            g = font.get(ch, font[" "])
            for gy, row in enumerate(g):
                for gx, bit in enumerate(row):
                    if bit == "#":
                        self.set(x + gx, y + gy, ramp(gy) if ramp else colour)
            x += len(g[0]) + gap
        return x - gap

    def save(self, path):
        big = self.img.resize((self.w * SCALE, self.h * SCALE), Image.NEAREST)
        big.save(path)


def text_width(font, s, gap=1):
    return sum(len(font.get(ch, font[" "])[0]) + gap for ch in s) - gap


def in_triangle(px, py, a, b, c):
    def side(p, q, r):
        return (p[0] - r[0]) * (q[1] - r[1]) - (q[0] - r[0]) * (p[1] - r[1])
    d1, d2, d3 = side((px, py), a, b), side((px, py), b, c), side((px, py), c, a)
    neg = d1 < 0 or d2 < 0 or d3 < 0
    pos = d1 > 0 or d2 > 0 or d3 > 0
    return not (neg and pos)


def polar(cx, cy, r, a):
    """Point at radius r and angle a (0 = straight up, clockwise) around a continuous centre."""
    return (cx + r * math.sin(a), cy - r * math.cos(a))


def emblem(cv, ex, ey, disc=21, burst=(29, 25), star=15, rays=24):
    """The Union star: a burst of gold rays, a gold-rimmed red disc and a bevelled five point star.
    (ex, ey) is the centre pixel; sampling at pixel centres keeps it mirror symmetric."""
    cx, cy = ex + 0.5, ey + 0.5
    reach = burst[0] + 1
    spikes, rim = set(), set()
    for y in range(ey - reach, ey + reach + 1):
        for x in range(ex - reach, ex + reach + 1):
            px, py = x + 0.5, y + 0.5
            d = math.hypot(px - cx, py - cy)
            # Burst: thin gold spikes, long and short alternating, growing out of the rim.
            if disc + 0.5 < d <= burst[0]:
                for i in range(rays):
                    a = i * 2 * math.pi / rays
                    tip = burst[0] if i % 2 == 0 else burst[1]
                    half = 0.085 if i % 2 == 0 else 0.065
                    base_l = polar(cx, cy, disc, a - half)
                    base_r = polar(cx, cy, disc, a + half)
                    if in_triangle(px, py, base_l, polar(cx, cy, tip, a), base_r):
                        spikes.add((x, y))
                        break
            if d <= disc + 0.5:
                if d > disc - 1.0:
                    cv.set(x, y, GOLD)
                    rim.add((x, y))
                elif d > disc - 2.0:
                    cv.set(x, y, RED_DK)
                elif d > disc - 3.0:
                    cv.set(x, y, GOLD_DK)
                else:
                    cv.set(x, y, RED)
    # Keep only spike pixels 8-connected to the rim, so no lone specks float off the thin rays.
    keep, todo = set(), list(rim)
    while todo:
        x, y = todo.pop()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                q = (x + dx, y + dy)
                if q in spikes and q not in keep:
                    keep.add(q)
                    todo.append(q)
    for x, y in keep:
        cv.set(x, y, GOLD)
    # Star: each arm split down its spine, left half lit, right half shaded.
    inner = star * 0.40
    for y in range(ey - star - 1, ey + star + 2):
        for x in range(ex - star - 1, ex + star + 2):
            px, py = x + 0.5, y + 0.5
            for i in range(5):
                a = i * 2 * math.pi / 5
                tip = polar(cx, cy, star, a)
                left = polar(cx, cy, inner, a - math.pi / 5)
                right = polar(cx, cy, inner, a + math.pi / 5)
                if in_triangle(px, py, (cx, cy), left, tip):
                    cv.set(x, y, GOLD)
                    break
                if in_triangle(px, py, (cx, cy), tip, right):
                    cv.set(x, y, GOLD_DK)
                    break


def background_rays(cv, ex, ey, y0, y1, count=36):
    cx, cy = ex + 0.5, ey + 0.5
    for y in range(y0, y1 + 1):
        for x in range(cv.w):
            a = math.atan2(x + 0.5 - cx, cy - (y + 0.5)) % (2 * math.pi)
            if int(a / (2 * math.pi / count)) % 2 == 0:
                cv.set(x, y, RAY)


def ribbon(cv, x0, x1, y0, y1, fill, edge):
    """A gold banner ribbon with swallowtail notches cut into both ends."""
    mid = (y0 + y1) // 2
    for y in range(y0, y1 + 1):
        notch = (y1 - y0) // 2 - abs(y - mid)
        cv.rect(x0 + notch, y, x1 - notch, y, fill)
    for x in range(x0 + (y1 - y0) // 2 + 1, x1 - (y1 - y0) // 2):
        cv.set(x, y0 + 1, edge)
        cv.set(x, y1 - 1, edge)


def banner():
    W, H = 280, 78
    cv = Canvas(W, H, BG)
    ex, ey = 40, 38
    body0, body1 = 7, 70
    background_rays(cv, ex, ey, body0, body1)

    # Livery bands: gold over red at the top, red over gold at the bottom.
    cv.rect(0, 0, W - 1, 1, GOLD)
    cv.rect(0, 3, W - 1, 5, RED)
    cv.rect(0, H - 6, W - 1, H - 4, RED)
    cv.rect(0, H - 2, W - 1, H - 1, GOLD)

    emblem(cv, ex, ey)

    # Text column, centred between the emblem and the right edge.
    left, right = 78, W - 6
    mid = (left + right) // 2

    def centre(font, s, gap=1):
        return mid - text_width(font, s, gap) // 2

    top = "EMBASSY OF THE"
    cv.text(F5, centre(F5, top, 2), 13, top, CREAM, gap=2, shadow=SHADOW)

    name = "UNION OF PROGRESSIVE PEOPLES"
    cv.text(BIG, centre(BIG, name), 24, name, GOLD, shadow=SHADOW,
            ramp=lambda gy: GOLD if gy < 6 else GOLD_DK)

    # Double rule with a small star break in the middle.
    name_x0 = centre(BIG, name)
    for x in range(name_x0, name_x0 + text_width(BIG, name)):
        if abs(x - mid) > 5:
            cv.set(x, 38, GOLD)
            cv.set(x, 40, RED)
    cv.text(F5, mid - 2, 35, "*", GOLD)

    sub = "DIPLOMATIC MISSION TO THE COLONY"
    cv.text(F3, centre(F3, sub), 45, sub, CORAL, shadow=SHADOW)

    motto = "UNITY * LABOUR * PROGRESS"
    rw = text_width(F5, motto) + 28
    rx0 = mid - rw // 2
    ribbon(cv, rx0, rx0 + rw, 54, 64, GOLD, GOLD_DK)
    cv.text(F5, centre(F5, motto), 56, motto, RED_DK)
    return cv


def emblem_png():
    cv = Canvas(62, 62)
    emblem(cv, 30, 30)
    return cv


def main():
    os.makedirs(OUT, exist_ok=True)
    banner().save(os.path.join(OUT, "banner.png"))
    emblem_png().save(os.path.join(OUT, "emblem.png"))


if __name__ == "__main__":
    main()
