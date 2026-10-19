"""Draws The Pilot Sightings (pilot.deep) masthead and emblem for the deep web.

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/Deep/Pilot.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Deep/Pilot/{banner,emblem}.png.

The look is a long range survey scope pointed at something it should not have found: a cold
violet-black starfield, a round viewfinder with range ticks, and inside it a ribbed, horseshoe
shaped hull lying on a dark horizon. Above the hull a cave-chart constellation is daubed in ochre
pigment, the way the rumours say it turns up on cave walls. The name is set in tall doubled
capitals in bone white with a teal and rust colour split, and a couple of scanlines are knocked
sideways as if the frame came off a failing relay.

Everything is drawn at 1x on the pixel grid with no antialiasing, then upscaled 2x with nearest
neighbour. Random touches use a fixed seed so the art is the same on every run.
"""
import math
import os
import random

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "Deep", "Pilot")
SCALE = 2

# -- Palette (site style: background #0A0912, text #C4BFAE, heading #D9A44E, link #6FCFC0) ------
BG = "#0A0912"
VOID = "#040308"
PANEL = "#15122A"
STAR_DIM = "#2E2A48"
STAR_MID = "#5A5578"
STAR_HI = "#B8B4D0"
BONE = "#C4BFAE"
BONE_HI = "#ECE6D2"
BONE_LO = "#8A846F"
BONE_DK = "#4A4536"
OCHRE = "#D9A44E"
OCHRE_DK = "#7A5524"
TEAL = "#6FCFC0"
TEAL_DK = "#2B5A55"
RUST = "#A0453A"
GROUND = "#1C1922"
GROUND_HI = "#2C2734"

# -- Tall capitals: 5x7 glyphs, drawn doubled in both directions --------------------------------
BIG = {
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "G": [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###."],
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "N": ["#...#", "##..#", "##..#", "#.#.#", "#..##", "#..##", "#...#"],
    " ": ["..", "..", "..", "..", "..", "..", ".."],
}

# -- 3x5 terminal font -----------------------------------------------------------------------------
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
    "?": ["##.", "..#", ".#.", "...", ".#."], "+": ["...", ".#.", "###", ".#.", "..."],
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

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.img.getpixel((x, y))
        return None

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def hline(self, x0, x1, y, c):
        self.rect(min(x0, x1), y, max(x0, x1), y, c)

    def vline(self, x, y0, y1, c):
        self.rect(x, min(y0, y1), x, max(y0, y1), c)

    def line(self, x0, y0, x1, y1, c):
        """Bresenham line, one pixel wide."""
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.set(x0, y0, c)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def bitmap(self, x, y, rows, c, sx=1, sy=1):
        for gy, row in enumerate(rows):
            for gx, bit in enumerate(row):
                if bit == "#":
                    self.rect(x + gx * sx, y + gy * sy, x + gx * sx + sx - 1, y + gy * sy + sy - 1, c)

    def text(self, font, x, y, text, c, gap=1, clear=BG):
        if clear:
            self.rect(x - 1, y - 1, x + text_width(font, text, gap), y + len(font[" "]), clear)
        for ch in text:
            g = font.get(ch, font[" "])
            self.bitmap(x, y, g, c)
            x += len(g[0]) + gap
        return x

    def shift_row(self, y, x0, x1, dx, fill):
        """Slides one scanline of [x0, x1] sideways by dx, filling the gap it leaves."""
        row = [self.get(x, y) for x in range(x0, x1 + 1)]
        for x in range(x0, x1 + 1):
            src = x - dx
            if x0 <= src <= x1:
                self.img.putpixel((x, y), row[src - x0])
            else:
                self.set(x, y, fill)

    def save(self, path):
        big = self.img.resize((self.w * SCALE, self.h * SCALE), Image.NEAREST)
        big.save(path)


def text_width(font, text, gap=1):
    return sum(len(font.get(ch, font[" "])[0]) + gap for ch in text) - gap


def big_width(text, gap=1):
    """Width of a word in the doubled capitals, gap counted in source pixels."""
    return sum(len(BIG[ch][0]) * 2 + gap * 2 for ch in text) - gap * 2


def big_text(cv, x, y, text, c, gap=1):
    for ch in text:
        g = BIG[ch]
        cv.bitmap(x, y, g, c, 2, 2)
        x += len(g[0]) * 2 + gap * 2
    return x


# -- Starfield -------------------------------------------------------------------------------------
def starfield(cv, x0, y0, x1, y1, seed, density=0.035):
    rng = random.Random(seed)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            r = rng.random()
            if r < density * 0.08:
                cv.set(x, y, STAR_HI)
            elif r < density * 0.3:
                cv.set(x, y, STAR_MID)
            elif r < density:
                cv.set(x, y, STAR_DIM)


def twinkle(cv, x, y, c, arm=2):
    """A small cross star."""
    cv.set(x, y, c)
    for i in range(1, arm + 1):
        k = STAR_MID if i == arm else c
        cv.set(x - i, y, k)
        cv.set(x + i, y, k)
        cv.set(x, y - i, k)
        cv.set(x, y + i, k)


# -- Emblem: a scope view of the derelict --------------------------------------------------------
SCOPE_R = 28


def scope(cv, cx, cy, seed=7):
    """A round viewfinder centred on (cx, cy): stars, horizon, the hull, the cave chart, ticks."""
    r = SCOPE_R
    ground_y = cy + 10

    def inside(x, y, rad):
        return (x - cx) ** 2 + (y - cy) ** 2 <= rad * rad

    # Field of view: a darker disc with its own stars.
    rng = random.Random(seed)
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            if not inside(x, y, r - 2):
                continue
            if y >= ground_y:
                # Ground: banded dust with a lit crest along the horizon.
                cv.set(x, y, GROUND_HI if y == ground_y or (y - ground_y) % 4 == 1 and (x + y) % 7 == 0 else GROUND)
            else:
                cv.set(x, y, VOID)
                p = rng.random()
                if p < 0.012:
                    cv.set(x, y, STAR_MID)
                elif p < 0.05:
                    cv.set(x, y, STAR_DIM)

    hull(cv, cx, ground_y)
    chart(cv, cx, cy)

    # Rim: a two pixel bone ring with a dark keyline inside, and a gap at the top for the tick.
    for y in range(cy - r - 1, cy + r + 2):
        for x in range(cx - r - 1, cx + r + 2):
            d2 = (x - cx) ** 2 + (y - cy) ** 2
            if (r - 1) ** 2 < d2 <= r * r + r:
                cv.set(x, y, BONE_LO)
            elif (r - 2) ** 2 < d2 <= (r - 1) ** 2:
                cv.set(x, y, VOID)
    # Range ticks every 30 degrees, long ones on the cardinal points.
    for k in range(12):
        a = k * math.pi / 6
        long_tick = k % 3 == 0
        for t in range(r - (5 if long_tick else 3), r - 1):
            x = cx + round(math.cos(a) * t)
            y = cy + round(math.sin(a) * t)
            cv.set(x, y, TEAL if long_tick else TEAL_DK)
    # Outer crosshair stubs.
    for side in (-1, 1):
        cv.hline(cx + side * (r + 3), cx + side * (r + 6), cy, TEAL_DK)
        cv.vline(cx, cy + side * (r + 3), cy + side * (r + 6), TEAL_DK)


def hull(cv, cx, ground_y):
    """A ribbed horseshoe hull, both prongs buried in the ground, arching up from the horizon."""
    rx, ry = 18, 17
    hx = cx + 1
    base = ground_y + 3  # the arch's centre sits below the horizon, so both prongs are buried
    for y in range(base - ry - 6, ground_y + 1):
        for x in range(hx - rx - 6, hx + rx + 7):
            dx, dy = x - hx, y - base
            d = math.sqrt((dx / rx) ** 2 + (dy / ry) ** 2)
            ang = math.atan2(-dy, dx)  # 0 at right prong, pi/2 at crown, pi at left prong
            # Thick at the crown, tapering towards the prongs; the right prong a little thinner.
            crown = math.sin(max(0.0, min(math.pi, ang)))
            thick = 0.09 + 0.19 * crown ** 1.4 - (0.03 if dx > 0 else 0)
            if abs(d - 1) > thick:
                continue
            # Light falls from the upper left: the outer edge there is bright, the inner dark.
            outer = d > 1
            rib = int(ang * 180 / math.pi) % 15 < 3
            if rib:
                c = BONE_DK
            elif outer and dx < 0:
                c = BONE_HI
            elif outer:
                c = BONE
            elif dx < 0:
                c = BONE_LO
            else:
                c = BONE_DK
            cv.set(x, y, c)
    # A small dark wound in the crown, where the hull was opened.
    for dx, dy in ((-1, 0), (0, 0), (1, 0), (0, 1), (-2, 1), (1, 1)):
        cv.set(hx - 5 + dx, base - ry - 1 + dy, VOID)
    # Shadow pooled under the arch.
    cv.hline(hx - rx + 4, hx + rx - 4, ground_y + 1, VOID)


CHART = [(-14, -21), (-8, -16), (-2, -19), (5, -14), (11, -20), (16, -12)]


def chart(cv, cx, cy):
    """A cave-chart constellation in ochre pigment over the hull: dots joined by a broken line."""
    pts = [(cx + x, cy + y) for x, y in CHART]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        # Dotted join, every other pixel, as if dabbed on with a finger.
        n = max(abs(x1 - x0), abs(y1 - y0))
        for i in range(2, n - 1, 2):
            cv.set(x0 + round((x1 - x0) * i / n), y0 + round((y1 - y0) * i / n), OCHRE_DK)
    for i, (x, y) in enumerate(pts):
        cv.rect(x, y, x + 1, y + 1, OCHRE)
        if i == 2:
            # The one star the figures always point at gets a ring.
            for ddx, ddy in ((-1, -1), (2, -1), (-1, 2), (2, 2)):
                cv.set(x + ddx, y + ddy, OCHRE_DK)


# -- Banner ----------------------------------------------------------------------------------------
def banner():
    W, H = 264, 72
    cv = Canvas(W, H, BG)
    starfield(cv, 0, 0, W - 1, H - 1, seed=426)
    for x, y in ((228, 9), (250, 30), (142, 6), (96, 62)):
        twinkle(cv, x, y, STAR_HI)

    # Viewfinder on the left.
    scx, scy = 36, 36
    scope(cv, scx, scy)

    nx = 76

    # Kicker: "THE" in ochre with a dotted rule running to the right edge.
    cv.text(F3, nx, 12, "THE", OCHRE)
    kx = nx + text_width(F3, "THE") + 4
    for x in range(kx, W - 30, 2):
        cv.set(x, 14, OCHRE_DK)
    cv.text(F3, W - 26, 12, "?/?", OCHRE_DK)

    # Name: doubled capitals with a teal ghost one pixel left and a rust ghost one pixel right.
    name = "PILOT SIGHTINGS"
    ny = 20
    cv.rect(nx - 3, ny - 2, nx + big_width(name) + 3, ny + 16, BG)
    big_text(cv, nx - 1, ny, name, TEAL_DK)
    big_text(cv, nx + 1, ny + 1, name, RUST)
    big_text(cv, nx, ny, name, BONE_HI)
    name_end = nx + big_width(name)
    # Lit tops on the bars, a bone shade on the lower half.
    for y in range(ny + 8, ny + 14):
        for x in range(nx, name_end + 1):
            if cv.get(x, y) == rgba(BONE_HI):
                cv.set(x, y, BONE)
    # Scanline glitch: two rows slide right, one slides left.
    cv.shift_row(ny + 5, nx - 4, W - 8, 2, BG)
    cv.shift_row(ny + 11, nx - 4, W - 8, -1, BG)

    # Rule under the name: teal, broken in three places like a damaged trace.
    ry = ny + 18
    for x in range(nx, W - 10):
        if x in range(nx + 58, nx + 61) or x in range(nx + 121, nx + 123) or x in range(nx + 150, nx + 155):
            continue
        cv.set(x, ry, TEAL)
    cv.hline(nx, W - 10, ry + 1, TEAL_DK)

    # Small print.
    cv.text(F3, nx, ry + 5, "UNCONFIRMED - UNREPORTED - UNRECOVERED", BONE)
    cv.text(F3, nx, ry + 12, "SURVEY RUMOURS FROM THE OUTER VEIL", BONE_LO)
    ax = cv.text(F3, nx, ry + 19, "PILOT.DEEP", TEAL)
    cv.rect(ax + 1, ry + 19, ax + 3, ry + 23, TEAL)  # terminal cursor
    # A scope readout tucked bottom right: bearing unknown, range unknown.
    tag = "BRG ??? RNG ???"
    cv.text(F3, W - 10 - text_width(F3, tag), ry + 19, tag, OCHRE_DK)

    # Frame: thin violet keylines top and bottom with corner brackets.
    cv.hline(0, W - 1, 0, PANEL)
    cv.hline(0, W - 1, H - 1, PANEL)
    for (x, y, dx, dy) in ((2, 2, 1, 1), (W - 3, 2, -1, 1), (2, H - 3, 1, -1), (W - 3, H - 3, -1, -1)):
        for i in range(5):
            cv.set(x + dx * i, y, STAR_MID)
            cv.set(x, y + dy * i, STAR_MID)
    return cv


def emblem():
    cv = Canvas(72, 72, None)
    scope(cv, 36, 36)
    return cv


def main():
    os.makedirs(OUT, exist_ok=True)
    banner().save(os.path.join(OUT, "banner.png"))
    emblem().save(os.path.join(OUT, "emblem.png"))
    print("wrote", OUT)


if __name__ == "__main__":
    main()
