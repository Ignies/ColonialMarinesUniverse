"""Pixel art for the deep web's ad pages: corporate product banners, animated badges and popup icons.

The deep web hides its real pages inside a maze of advertising pages. Every ad on them is plain in-universe
product advertising from the 2170s-2180s: Seegson, Weyland-Yutani, Hyperdyne, Lasalle Bionational and a few
colony brands, drawn in the chunky CRT-era commercial style of the used future.

Writes to Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Ads/:
  banner_NN.png   product banners, drawn at 160x40 and saved 3x with nearest neighbour (480x120)
  badges.rsi      32x32 animated badges, one state per strip, frames left to right
  billboards.rsi  96x32 animated mini commercials, one looping state per product, frames left to right
  icon_NN.png     24x24 static icons for the ad pages' in-window popups
  attributions.yml

The ad pages pick banners, badges, billboards and icons at random, so new art only needs adding to the lists in
CMUDeepMazeSystem. Everything is drawn at 1x on the pixel grid with no antialiasing: hand-built bitmap faces
(a bold 9 row display face, a 5x7 face and a 3x5 face), shaded and outlined product sprites, and ordered
dithering for gradients.

    python Ads.py
"""

import json
import math
import os
import random

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
OUT = os.path.join(ROOT, "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "Ads")
SCALE = 3
BW, BH = 160, 40
GENERATOR = "Content.CMU/Design/PersonalComputers/SiteArt/Ads/Ads.py"
SOURCE = "https://github.com/Ignies/ColonialMarinesUniverse"

# -- Colour ------------------------------------------------------------------------------------------


def rgba(c, a=255):
    if isinstance(c, tuple):
        return c if len(c) == 4 else c + (a,)
    c = c.lstrip("#")
    return int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), a


def mix(a, b, t):
    a, b = rgba(a), rgba(b)
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3)) + (255,)


def ramp(*colours):
    return [rgba(c) for c in colours]


B4 = ((0, 8, 2, 10), (12, 4, 14, 6), (3, 11, 1, 9), (15, 7, 13, 5))
B2 = ((0, 2), (3, 1))


def threshold(x, y, dither):
    if dither == 4:
        return (B4[y & 3][x & 3] + 0.5) / 16
    if dither == 2:
        return (B2[y & 1][x & 1] + 0.5) / 4
    return 0.5


def pick(rmp, v, x, y, dither=2):
    n = len(rmp)
    v = max(0.0, min(n - 1.0, v))
    i = int(v)
    if v - i > threshold(x, y, dither) and i < n - 1:
        i += 1
    return rmp[i]


# Palettes, dark to light.
INK = rgba("#0b0a10")
WHITE = rgba("#f4f1e8")
SKIN = ramp("#34343a", "#55545a", "#79766f", "#9c968c", "#bdb6a8", "#ddd6c6")
SKIN_OL = rgba("#1c1c22")
FLESH = ramp("#4a2a1e", "#8a5a40", "#c08a68", "#e6b896")
SUIT = ramp("#0f1c2c", "#18334f", "#21507a", "#2f6c9c", "#4a8cbd")
SUIT_OL = rgba("#08101a")
GLOW = ramp("#6a2a00", "#e06a00", "#ffb030", "#fff2c0")
SHELL = ramp("#3c434c", "#68717c", "#98a1ab", "#c6cdd4", "#eaeef1", "#ffffff")
SHELL_OL = rgba("#1d2229")
GLASS = ramp("#0a2433", "#164e68", "#2d84a6", "#62c0dc", "#b6ecf8")
METAL = ramp("#1a1c21", "#33363d", "#50545c", "#757a82", "#a3a8ae", "#d4d8dc")
CAN_RED = ramp("#3a0508", "#6c0c12", "#a3161c", "#d22c2a", "#ee5e4e", "#ffa898")
GOLD = ramp("#4a2c06", "#8a5a0e", "#c48a1a", "#ecbc34", "#ffe68a", "#fff8d8")
LIME = ramp("#0e2a06", "#2a5a0a", "#4e9612", "#80cc1e", "#c0f45a")
BLACKP = ramp("#08090a", "#15171a", "#24282d", "#3a4048", "#5a626c")
KHAKI = ramp("#2a2612", "#4a4422", "#6e6634", "#958c4c", "#bdb370", "#e2d9a0")
CYAN = ramp("#032a33", "#06535f", "#0b8d9c", "#26c8d6", "#8ef4f6", "#e6ffff")
GREEN_P = ramp("#031a0a", "#0a3a18", "#137028", "#2aac44", "#62e27a", "#c8ffd2")
PINK = ramp("#4a0a24", "#86163e", "#c22e60", "#ec5e8a", "#ff9ab8", "#ffd6e4")
YELLOW = ramp("#3a2a04", "#7a5a08", "#c49412", "#f0c41e", "#ffe468", "#fff6c0")
BEIGE = ramp("#3e352a", "#665a48", "#8f826a", "#b6a98c", "#d8ccae", "#f0e8d0")
BROWN = ramp("#1e0f08", "#3a1d0e", "#5e3016", "#8a4a22", "#b86e36", "#e09a5a")
TEAL = ramp("#052826", "#0a4842", "#106a62", "#1c8e82", "#3cb4a4", "#8ae0d0")
ORANGE = ramp("#3a1404", "#7a2a06", "#c04a0c", "#ec7418", "#ffa448", "#ffd29a")
LIGHT = (-0.55, -0.65, 0.52)
_ll = math.sqrt(sum(c * c for c in LIGHT))
LIGHT = tuple(c / _ll for c in LIGHT)

# -- Canvas ------------------------------------------------------------------------------------------
N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))
N8 = N4 + ((1, 1), (1, -1), (-1, 1), (-1, -1))


class Canvas:
    def __init__(self, w, h, bg=None):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), rgba(bg) if bg is not None else (0, 0, 0, 0))
        self.px = self.img.load()

    def inside(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h

    def set(self, x, y, c):
        if c is None or not self.inside(x, y):
            return
        c = rgba(c)
        if c[3] >= 255:
            self.px[x, y] = c
        elif c[3] > 0:
            b = self.px[x, y]
            if b[3] == 0:
                self.px[x, y] = c
            else:
                a = c[3] / 255
                self.px[x, y] = tuple(int(b[i] + (c[i] - b[i]) * a) for i in range(3)) + (max(b[3], c[3]),)

    def get(self, x, y):
        return self.px[x, y] if self.inside(x, y) else (0, 0, 0, 0)

    def rect(self, x0, y0, x1, y1, c):
        """Fills x0..x1-1, y0..y1-1."""
        for y in range(y0, y1):
            for x in range(x0, x1):
                self.set(x, y, c)

    def hline(self, x0, x1, y, c):
        for x in range(min(x0, x1), max(x0, x1) + 1):
            self.set(x, y, c)

    def vline(self, x, y0, y1, c):
        for y in range(min(y0, y1), max(y0, y1) + 1):
            self.set(x, y, c)

    def line(self, x0, y0, x1, y1, c):
        x0, y0, x1, y1 = int(round(x0)), int(round(y0)), int(round(x1)), int(round(y1))
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

    def fill(self, mask, c):
        for x, y in mask:
            self.set(x, y, c)

    def tint(self, mask, k):
        """Multiplies the colour of every pixel in mask by k (k < 1 darkens, k > 1 lightens)."""
        for x, y in mask:
            if not self.inside(x, y):
                continue
            r, g, b, a = self.px[x, y]
            self.px[x, y] = (min(255, int(r * k)), min(255, int(g * k)), min(255, int(b * k)), a)

    def paste(self, other, ox, oy):
        for y in range(other.h):
            for x in range(other.w):
                c = other.px[x, y]
                if c[3]:
                    self.set(ox + x, oy + y, c)

    def save(self, path, scale=SCALE):
        img = self.img if scale == 1 else self.img.resize((self.w * scale, self.h * scale), Image.NEAREST)
        img.save(path)


# -- Masks and shading -------------------------------------------------------------------------------


def m_rect(x0, y0, x1, y1):
    return {(x, y) for x in range(int(x0), int(x1)) for y in range(int(y0), int(y1))}


def m_ellipse(cx, cy, rx, ry):
    """Pixels whose centres fall inside the ellipse; cx, cy are continuous (a 6 wide disc at x 0..5 has cx 3)."""
    out = set()
    for y in range(int(math.floor(cy - ry)) - 1, int(math.ceil(cy + ry)) + 1):
        for x in range(int(math.floor(cx - rx)) - 1, int(math.ceil(cx + rx)) + 1):
            dx, dy = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            if dx * dx + dy * dy <= 1.0:
                out.add((x, y))
    return out


def m_poly(pts):
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    out = set()
    n = len(pts)
    for y in range(int(math.floor(min(ys))), int(math.ceil(max(ys))) + 1):
        py = y + 0.5
        for x in range(int(math.floor(min(xs))), int(math.ceil(max(xs))) + 1):
            px = x + 0.5
            inside = False
            j = n - 1
            for i in range(n):
                xi, yi = pts[i]
                xj, yj = pts[j]
                if (yi > py) != (yj > py) and px < (xj - xi) * (py - yi) / (yj - yi) + xi:
                    inside = not inside
                j = i
            if inside:
                out.add((x, y))
    return out


def m_rrect(x0, y0, x1, y1, r):
    out = set()
    for y in range(y0, y1):
        for x in range(x0, x1):
            cx = min(max(x + 0.5, x0 + r), x1 - r)
            cy = min(max(y + 0.5, y0 + r), y1 - r)
            if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                out.add((x, y))
    return out


def m_capsule(x0, y0, x1, y1, r):
    """Thick line from (x0, y0) to (x1, y1) with radius r."""
    out = set()
    vx, vy = x1 - x0, y1 - y0
    ll = vx * vx + vy * vy or 1
    for y in range(int(min(y0, y1) - r) - 1, int(max(y0, y1) + r) + 2):
        for x in range(int(min(x0, x1) - r) - 1, int(max(x0, x1) + r) + 2):
            px, py = x + 0.5, y + 0.5
            t = max(0, min(1, ((px - x0) * vx + (py - y0) * vy) / ll))
            dx, dy = px - (x0 + vx * t), py - (y0 + vy * t)
            if dx * dx + dy * dy <= r * r:
                out.add((x, y))
    return out


def m_burst(cx, cy, outer, inner, points, turn=0.0):
    out = set()
    for y in range(int(cy - outer) - 1, int(cy + outer) + 2):
        for x in range(int(cx - outer) - 1, int(cx + outer) + 2):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            a = (math.atan2(dy, dx) + turn) * points / (2 * math.pi)
            f = a - math.floor(a)
            tri = 1 - abs(f - 0.5) * 2
            if math.hypot(dx, dy) <= outer - (outer - inner) * tri:
                out.add((x, y))
    return out


def shift(mask, dx, dy):
    return {(x + dx, y + dy) for x, y in mask}


def edge(mask):
    return {p for p in mask if any((p[0] + dx, p[1] + dy) not in mask for dx, dy in N4)}


def halo(mask, diag=True):
    out = set()
    for x, y in mask:
        for dx, dy in (N8 if diag else N4):
            q = (x + dx, y + dy)
            if q not in mask:
                out.add(q)
    return out


def f_cyl(x0, x1, n, peak=0.3, lo=0.0):
    """Shading across a vertical cylinder lit from the left."""
    def f(x, y):
        u = (x + 0.5 - x0) / max(1, x1 - x0)
        if u < peak:
            light = 1 - (peak - u) / peak * 0.5
        else:
            light = 1 - (u - peak) / (1 - peak)
        return lo + (n - 1 - lo) * max(0.0, light)
    return f


def f_hcyl(y0, y1, n, peak=0.3, lo=0.0):
    """Shading across a horizontal cylinder lit from above."""
    g = f_cyl(y0, y1, n, peak, lo)
    return lambda x, y: g(y, x)


def f_sphere(cx, cy, r, n, amb=0.08, power=1.0):
    def f(x, y):
        nx, ny = (x + 0.5 - cx) / r, (y + 0.5 - cy) / r
        d = nx * nx + ny * ny
        nz = math.sqrt(max(0.0, 1 - min(1.0, d)))
        light = max(0.0, nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2])
        return (n - 1) * min(1.0, amb + (1 - amb) * light ** power)
    return f


def f_lin(x0, y0, x1, y1, v0, v1):
    vx, vy = x1 - x0, y1 - y0
    ll = vx * vx + vy * vy or 1

    def f(x, y):
        t = max(0, min(1, ((x + 0.5 - x0) * vx + (y + 0.5 - y0) * vy) / ll))
        return v0 + (v1 - v0) * t
    return f


def paint(cv, mask, rmp, fn=None, outline=None, dither=0, soft=None):
    """Fills mask with a ramp colour per pixel, then draws an outline on its inner edge.
    soft, when given, is used instead of outline where the edge faces the light (top and left)."""
    top = len(rmp) - 1
    for x, y in mask:
        v = fn(x, y) if fn else top * 0.6
        cv.set(x, y, pick(rmp, v, x, y, dither))
    if outline is not None:
        for x, y in edge(mask):
            lit = (x - 1, y) not in mask or (x, y - 1) not in mask
            dark = (x + 1, y) not in mask or (x, y + 1) not in mask
            cv.set(x, y, soft if (soft is not None and lit and not dark) else outline)


# -- Fonts -------------------------------------------------------------------------------------------
# Bold display face: 9 rows, two pixel stems.
BOLD = {
    "A": [".####.", "##..##", "##..##", "##..##", "######", "##..##", "##..##", "##..##", "##..##"],
    "B": ["#####.", "##..##", "##..##", "##..##", "#####.", "##..##", "##..##", "##..##", "#####."],
    "C": [".####.", "##..##", "##....", "##....", "##....", "##....", "##....", "##..##", ".####."],
    "D": ["#####.", "##..##", "##..##", "##..##", "##..##", "##..##", "##..##", "##..##", "#####."],
    "E": ["######", "##....", "##....", "##....", "#####.", "##....", "##....", "##....", "######"],
    "F": ["######", "##....", "##....", "##....", "#####.", "##....", "##....", "##....", "##...."],
    "G": [".####.", "##..##", "##....", "##....", "##.###", "##..##", "##..##", "##..##", ".#####"],
    "H": ["##..##"] * 4 + ["######"] + ["##..##"] * 4,
    "I": ["####"] + [".##."] * 7 + ["####"],
    "J": ["..####", "....##", "....##", "....##", "....##", "....##", "##..##", "##..##", ".####."],
    "K": ["##..##", "##..##", "##.##.", "####..", "###...", "####..", "##.##.", "##..##", "##..##"],
    "L": ["##...."] * 8 + ["######"],
    "M": ["##...##", "###.###", "#######", "##.#.##", "##.#.##", "##...##", "##...##", "##...##", "##...##"],
    "N": ["##..##", "###.##", "###.##", "######", "##.###", "##.###", "##..##", "##..##", "##..##"],
    "O": [".####."] + ["##..##"] * 7 + [".####."],
    "P": ["#####.", "##..##", "##..##", "##..##", "#####.", "##....", "##....", "##....", "##...."],
    "Q": [".####.", "##..##", "##..##", "##..##", "##..##", "##..##", "##.###", "##..##", ".##.##"],
    "R": ["#####.", "##..##", "##..##", "##..##", "#####.", "####..", "##.##.", "##..##", "##..##"],
    "S": [".####.", "##..##", "##....", "##....", ".####.", "....##", "....##", "##..##", ".####."],
    "T": ["######"] + ["..##.."] * 8,
    "U": ["##..##"] * 8 + [".####."],
    "V": ["##...##"] * 5 + [".##.##.", ".##.##.", "..###..", "...#..."],
    "W": ["##...##"] * 5 + ["##.#.##", "#######", "###.###", "##...##"],
    "X": ["##..##", "##..##", "##..##", ".####.", "..##..", ".####.", "##..##", "##..##", "##..##"],
    "Y": ["##..##", "##..##", "##..##", ".####."] + ["..##.."] * 5,
    "Z": ["######", "....##", "....##", "...##.", "..##..", ".##...", "##....", "##....", "######"],
    "0": [".####.", "##..##", "##..##", "##.###", "######", "###.##", "##..##", "##..##", ".####."],
    "1": ["..##..", ".###..", "####..", "..##..", "..##..", "..##..", "..##..", "..##..", "######"],
    "2": [".####.", "##..##", "....##", "....##", "...##.", "..##..", ".##...", "##....", "######"],
    "3": [".####.", "##..##", "....##", "....##", "..###.", "....##", "....##", "##..##", ".####."],
    "4": ["...##.", "..###.", ".####.", "##.##.", "##.##.", "######", "...##.", "...##.", "...##."],
    "5": ["######", "##....", "##....", "#####.", "....##", "....##", "....##", "##..##", ".####."],
    "6": [".####.", "##..##", "##....", "##....", "#####.", "##..##", "##..##", "##..##", ".####."],
    "7": ["######", "....##", "....##", "...##.", "...##.", "..##..", "..##..", "..##..", "..##.."],
    "8": [".####.", "##..##", "##..##", "##..##", ".####.", "##..##", "##..##", "##..##", ".####."],
    "9": [".####.", "##..##", "##..##", "##..##", ".#####", "....##", "....##", "##..##", ".####."],
    "-": ["....", "....", "....", "....", "####", "####", "....", "....", "...."],
    "!": ["##"] * 6 + ["..", "##", "##"],
    ".": [".."] * 7 + ["##", "##"],
    "'": ["##", "##", ".#"] + [".."] * 6,
    " ": ["..."] * 9,
}

# 5x7 face.
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
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "Q": [".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#"],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#"] * 6 + [".###."],
    "V": ["#...#", "#...#", "#...#", "#...#", ".#.#.", ".#.#.", "..#.."],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"],
    "X": ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "Z": ["#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"],
    "0": [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."],
    "1": [".#.", "##.", ".#.", ".#.", ".#.", ".#.", "###"],
    "2": [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
    "3": ["####.", "....#", "....#", ".###.", "....#", "....#", "####."],
    "4": ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
    "5": ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
    "6": ["..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###."],
    "7": ["#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."],
    "8": [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
    "9": [".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.."],
    ".": [".", ".", ".", ".", ".", ".", "#"],
    ",": ["..", "..", "..", "..", "..", ".#", "#."],
    "-": ["...", "...", "...", "###", "...", "...", "..."],
    "!": ["#", "#", "#", "#", "#", ".", "#"],
    "'": ["#", "#", ".", ".", ".", ".", "."],
    ":": [".", "#", ".", ".", ".", "#", "."],
    "/": ["....#", "...#.", "...#.", "..#..", ".#...", ".#...", "#...."],
    "&": [".##..", "#..#.", "#.#..", ".#...", "#.#.#", "#..#.", ".##.#"],
    "$": ["..#..", ".####", "#.#..", ".###.", "..#.#", "####.", "..#.."],
    "%": ["##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##"],
    "+": [".....", "..#..", "..#..", "#####", "..#..", "..#..", "....."],
    "?": [".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.."],
    "(": [".#", "#.", "#.", "#.", "#.", "#.", ".#"],
    ")": ["#.", ".#", ".#", ".#", ".#", ".#", "#."],
    ">": ["#...", ".#..", "..#.", "...#", "..#.", ".#..", "#..."],
    "*": [".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", "....."],
    " ": ["...", "...", "...", "...", "...", "...", "..."],
}

# 3x5 face.
F3 = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"],
    "B": ["##.", "#.#", "##.", "#.#", "##."],
    "C": [".##", "#..", "#..", "#..", ".##"],
    "D": ["##.", "#.#", "#.#", "#.#", "##."],
    "E": ["###", "#..", "##.", "#..", "###"],
    "F": ["###", "#..", "##.", "#..", "#.."],
    "G": [".##", "#..", "#.#", "#.#", ".##"],
    "H": ["#.#", "#.#", "###", "#.#", "#.#"],
    "I": ["###", ".#.", ".#.", ".#.", "###"],
    "J": ["..#", "..#", "..#", "#.#", ".#."],
    "K": ["#.#", "#.#", "##.", "#.#", "#.#"],
    "L": ["#..", "#..", "#..", "#..", "###"],
    "M": ["#...#", "##.##", "#.#.#", "#...#", "#...#"],
    "N": ["#..#", "##.#", "#.##", "#..#", "#..#"],
    "O": [".#.", "#.#", "#.#", "#.#", ".#."],
    "P": ["##.", "#.#", "##.", "#..", "#.."],
    "Q": [".#.", "#.#", "#.#", "##.", ".##"],
    "R": ["##.", "#.#", "##.", "#.#", "#.#"],
    "S": [".##", "#..", ".#.", "..#", "##."],
    "T": ["###", ".#.", ".#.", ".#.", ".#."],
    "U": ["#.#", "#.#", "#.#", "#.#", "###"],
    "V": ["#.#", "#.#", "#.#", "#.#", ".#."],
    "W": ["#...#", "#...#", "#.#.#", "##.##", "#...#"],
    "X": ["#.#", "#.#", ".#.", "#.#", "#.#"],
    "Y": ["#.#", "#.#", ".#.", ".#.", ".#."],
    "Z": ["###", "..#", ".#.", "#..", "###"],
    "0": ["###", "#.#", "#.#", "#.#", "###"],
    "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["##.", "..#", ".#.", "#..", "###"],
    "3": ["##.", "..#", ".#.", "..#", "##."],
    "4": ["#.#", "#.#", "###", "..#", "..#"],
    "5": ["###", "#..", "##.", "..#", "##."],
    "6": [".##", "#..", "###", "#.#", "###"],
    "7": ["###", "..#", ".#.", ".#.", ".#."],
    "8": ["###", "#.#", "###", "#.#", "###"],
    "9": ["###", "#.#", "###", "..#", "##."],
    ".": [".", ".", ".", ".", "#"],
    ",": ["..", "..", "..", ".#", "#."],
    "-": ["...", "...", "###", "...", "..."],
    "!": ["#", "#", "#", ".", "#"],
    "'": ["#", "#", ".", ".", "."],
    ":": [".", "#", ".", "#", "."],
    "/": ["..#", "..#", ".#.", "#..", "#.."],
    "&": [".#.", "#.#", ".#.", "#.#", ".##"],
    "$": [".##", "##.", ".#.", ".##", "##."],
    "%": ["#.#", "..#", ".#.", "#..", "#.#"],
    "+": ["...", ".#.", "###", ".#.", "..."],
    "*": ["...", "#.#", ".#.", "#.#", "..."],
    "?": ["##.", "..#", ".#.", "...", ".#."],
    ">": ["#..", ".#.", "..#", ".#.", "#.."],
    " ": ["..", "..", "..", "..", ".."],
}


def text_mask(font, text, gap=1, scale=1, italic=0):
    """Pixel set of text with its top left at 0, 0. italic shifts each row right by (rows above bottom) // italic."""
    out = set()
    x = 0
    rows = len(font["A"]) * scale
    for ch in text:
        g = font[ch]
        for gy, row in enumerate(g):
            for gx, bit in enumerate(row):
                if bit == "#":
                    for sy in range(scale):
                        for sx in range(scale):
                            py = gy * scale + sy
                            px = x + gx * scale + sx + ((rows - 1 - py) // italic if italic else 0)
                            out.add((px, py))
        x += (len(g[0]) + gap) * scale
    return out


def text_width(font, text, gap=1, scale=1, italic=0):
    w = sum((len(font[ch][0]) + gap) * scale for ch in text) - gap * scale
    if italic:
        w += (len(font["A"]) * scale - 1) // italic
    return w


def text_height(font, scale=1):
    return len(font["A"]) * scale


def draw_text(cv, font, x, y, text, fill, outline=None, shadow=None, gap=1, scale=1, italic=0, shadow_off=(1, 1),
              diag=True):
    """fill is a colour or f(row) -> colour, row counted from the top of the text."""
    mask = shift(text_mask(font, text, gap, scale, italic), x, y)
    body = mask | (halo(mask, diag) if outline is not None else set())
    if shadow is not None:
        cv.fill(shift(body, *shadow_off), shadow)
    if outline is not None:
        cv.fill(halo(mask, diag), outline)
    for px, py in mask:
        cv.set(px, py, fill(py - y) if callable(fill) else fill)
    return x + text_width(font, text, gap, scale, italic)


def draw_text_c(cv, font, cx, y, text, fill, **kw):
    w = text_width(font, text, kw.get("gap", 1), kw.get("scale", 1), kw.get("italic", 0))
    return draw_text(cv, font, cx - w // 2, y, text, fill, **kw)


def bands(*stops):
    """Row gradient for text: bands((rows, colour), ...)."""
    def f(row):
        acc = 0
        for n, c in stops:
            acc += n
            if row < acc:
                return c
        return stops[-1][1]
    return f


def bitmap(cv, x, y, rows, colours):
    """rows of characters; colours maps a character to a colour (missing characters are transparent)."""
    for gy, row in enumerate(rows):
        for gx, ch in enumerate(row):
            if ch in colours:
                cv.set(x + gx, y + gy, colours[ch])


# -- Shared decoration -------------------------------------------------------------------------------


def vgrad(cv, x0, y0, x1, y1, colours, dither=4):
    rmp = [rgba(c) for c in colours]
    for y in range(y0, y1):
        v = (y - y0) / max(1, y1 - y0 - 1) * (len(rmp) - 1)
        for x in range(x0, x1):
            cv.set(x, y, pick(rmp, v, x, y, dither))


def radial(cv, cx, cy, r, colours, x0=0, y0=0, x1=None, y1=None, dither=4):
    """Concentric gradient, colours from the centre outwards."""
    rmp = [rgba(c) for c in colours]
    x1 = cv.w if x1 is None else x1
    y1 = cv.h if y1 is None else y1
    for y in range(y0, y1):
        for x in range(x0, x1):
            d = math.hypot(x + 0.5 - cx, (y + 0.5 - cy) * 1.0) / r
            cv.set(x, y, pick(rmp, min(1.0, d) * (len(rmp) - 1), x, y, dither))


def scanlines(cv, x0, y0, x1, y1, k=0.82, period=2, phase=0):
    cv.tint({(x, y) for y in range(y0, y1) if (y + phase) % period == 0 for x in range(x0, x1)}, k)


def halftone(cv, x0, y0, x1, y1, colour, density, cell=4):
    """Offset dot grid; density(x, y) in 0..1 sets the dot size."""
    for gy, y in enumerate(range(y0, y1, cell)):
        off = (cell // 2) if gy % 2 else 0
        for x in range(x0 + off, x1, cell):
            d = density(x, y)
            if d < 0.15:
                continue
            pts = [(0, 0)]
            if d >= 0.45:
                pts += [(1, 0), (-1, 0), (0, 1), (0, -1)]
            if d >= 0.8:
                pts += [(1, 1), (-1, -1), (1, -1), (-1, 1)]
            for dx, dy in pts:
                if x0 <= x + dx < x1 and y0 <= y + dy < y1:
                    cv.set(x + dx, y + dy, colour)


def sunrays(cv, x0, y0, x1, y1, cx, cy, n, c1, c2, turn=0.0):
    for y in range(y0, y1):
        for x in range(x0, x1):
            a = (math.atan2(y + 0.5 - cy, x + 0.5 - cx) + turn) * n / (2 * math.pi)
            cv.set(x, y, c1 if int(math.floor(a)) % 2 == 0 else c2)


def starfield(cv, rng, x0, y0, x1, y1, n, colours=("#3a4a6a", "#7a8aaa", "#c8d4f0", "#ffffff")):
    for _ in range(n):
        x, y = rng.randrange(x0, x1), rng.randrange(y0, y1)
        k = rng.random()
        c = colours[min(len(colours) - 1, int(k * k * len(colours)))]
        cv.set(x, y, c)
        if k > 0.93:
            for dx, dy in N4:
                cv.set(x + dx, y + dy, colours[1])


def frame(cv, outer, hi, lo, x0=0, y0=0, x1=None, y1=None):
    x1 = cv.w - 1 if x1 is None else x1
    y1 = cv.h - 1 if y1 is None else y1
    cv.hline(x0, x1, y0, outer)
    cv.hline(x0, x1, y1, outer)
    cv.vline(x0, y0, y1, outer)
    cv.vline(x1, y0, y1, outer)
    if hi is not None:
        cv.hline(x0 + 1, x1 - 1, y0 + 1, hi)
        cv.vline(x0 + 1, y0 + 1, y1 - 1, hi)
    if lo is not None:
        cv.hline(x0 + 1, x1 - 1, y1 - 1, lo)
        cv.vline(x1 - 1, y0 + 1, y1 - 1, lo)


def sticker(cv, cx, cy, outer, inner, points, rmp, outline, lines, turn=0.0, shadow=None):
    """Starburst callout; lines is a list of (font, text, colour, y offset from the centre)."""
    m = m_burst(cx, cy, outer, inner, points, turn)
    if shadow is not None:
        cv.fill(shift(m, 1, 1), shadow)
    paint(cv, m, rmp, f_sphere(cx - 2, cy - 3, outer * 1.4, len(rmp), 0.3), outline)
    for font, text, colour, dy in lines:
        draw_text_c(cv, font, cx, cy + dy, text, colour, outline=None, shadow=rmp[0])


def price_tag(cv, x, y, w, h, rmp, outline, lines, string=None):
    """Swing tag pointing left with a punched hole; lines are (font, text, colour, x offset, y offset)."""
    m = m_poly([(x, y + h / 2), (x + 5, y), (x + w, y), (x + w, y + h), (x + 5, y + h)])
    paint(cv, m, rmp, f_lin(x, y, x + w * 0.3, y + h, len(rmp) - 1.2, len(rmp) - 2.6), outline)
    hole = m_ellipse(x + 4.5, y + h / 2, 1.5, 1.5)
    cv.fill(hole, outline)
    if string is not None:
        cv.line(x + 4, y + h // 2, string[0], string[1], rgba("#e8e0c8"))
    for font, text, colour, dx, dy in lines:
        draw_text(cv, font, x + dx, y + dy, text, colour)


def ribbon(cv, x0, x1, y, h, rmp, outline, text=None, font=None, colour=None, text_dy=None):
    """Banner ribbon with notched tails tucked behind each end."""
    tail = 5
    left = [(x0 - tail, y + 2), (x0 + 2, y + 2), (x0 + 2, y + h + 2), (x0 - tail, y + h + 2),
            (x0 - tail + 2.5, y + 2 + h / 2)]
    right = [(x1 - 2, y + 2), (x1 + tail, y + 2), (x1 + tail - 2.5, y + 2 + h / 2), (x1 + tail, y + h + 2),
             (x1 - 2, y + h + 2)]
    for pts in (left, right):
        paint(cv, m_poly(pts), rmp, lambda px, py: 1.0, outline)
    cv.fill(m_poly([(x0, y + h), (x0 + 2, y + h), (x0 + 2, y + h + 2)]), outline)
    cv.fill(m_poly([(x1 - 2, y + h), (x1, y + h), (x1 - 2, y + h + 2)]), outline)
    band = m_rect(x0, y, x1, y + h)
    paint(cv, band, rmp, f_hcyl(y, y + h, len(rmp), 0.3, 1.5), outline)
    if text:
        th = text_height(font)
        draw_text_c(cv, font, (x0 + x1) // 2, y + (text_dy if text_dy is not None else (h - th) // 2), text,
                    colour, shadow=rmp[0])


def blink(cv, x, y, c, big=False):
    cv.set(x, y, WHITE)
    for dx, dy in N4:
        cv.set(x + dx, y + dy, c)
    if big:
        for dx, dy in ((2, 0), (-2, 0), (0, 2), (0, -2)):
            cv.set(x + dx, y + dy, c)


# -- Logos ------------------------------------------------------------------------------------------
WY_MARK = [
    "##.........##",
    "###...#...###",
    ".##..###..##.",
    ".###.###.###.",
    "..#########..",
    "..###.#.###..",
    "...#.....#...",
]
WY_MARK_S = [
    "#.......#",
    "##..#..##",
    ".#.###.#.",
    ".#######.",
    "..#...#..",
]

SEEG_MARK = [
    "..#####..",
    ".##...##.",
    "##.###...",
    "#.##.....",
    "#..###..#",
    ".....##.#",
    "...###.##",
    ".##...##.",
    "..#####..",
]
HYPER_MARK = [
    "..###..",
    ".#...#.",
    "#.#.#.#",
    "#.###.#",
    "#.#.#.#",
    ".#...#.",
    "..###..",
]


def wy_logo(cv, x, y, c, small=False, shadow=None):
    rows = WY_MARK_S if small else WY_MARK
    if shadow is not None:
        bitmap(cv, x + 1, y + 1, rows, {"#": shadow})
    bitmap(cv, x, y, rows, {"#": c})
    return x + len(rows[0])


def seeg_logo(cv, cx, cy, r, ring, field, mark):
    disc = m_ellipse(cx, cy, r, r)
    cv.fill(disc, ring)
    cv.fill(m_ellipse(cx, cy, r - 1, r - 1), field)
    w = len(SEEG_MARK[0])
    bitmap(cv, int(cx - w / 2), int(cy - len(SEEG_MARK) / 2), SEEG_MARK, {"#": mark})


# -- Product sprites ---------------------------------------------------------------------------------


def working_joe(cv, ox, oy, eyes=1.0, suit=SUIT, bottom=36):
    """Seegson Working Joe bust, 30 wide, head top at oy+1."""
    cx = ox + 15
    body = m_poly([(ox, oy + bottom), (ox + 1, oy + 27), (ox + 4, oy + 23), (ox + 10, oy + 21), (ox + 20, oy + 21),
                   (ox + 26, oy + 23), (ox + 29, oy + 27), (ox + 30, oy + bottom)])
    paint(cv, body, suit, f_cyl(ox, ox + 30, len(suit), 0.3, 0.4), SUIT_OL)
    vee = m_poly([(cx - 5, oy + 21), (cx, oy + 28), (cx + 5, oy + 21)])
    cv.fill(vee, "#20242a")
    for i in range(7):
        cv.set(cx - 5 + int(i * 5 / 7), oy + 21 + i, suit[4])
        cv.set(cx + 4 - int(i * 5 / 7), oy + 21 + i, suit[2])
    cv.vline(cx, oy + 29, oy + bottom, suit[0])
    # Shoulder seams and the Seegson chest patch.
    cv.line(ox + 5, oy + 24, ox + 8, oy + bottom, suit[1])
    cv.line(ox + 25, oy + 24, ox + 22, oy + bottom, suit[1])
    cv.rect(ox + 18, oy + 29, ox + 23, oy + 33, WHITE)
    cv.rect(ox + 19, oy + 30, ox + 22, oy + 32, "#2f6db0")
    cv.set(ox + 20, oy + 30, WHITE)
    # Neck with its rubber rings.
    neck = m_rect(cx - 3, oy + 15, cx + 3, oy + 23)
    paint(cv, neck, SKIN, f_cyl(cx - 3, cx + 3, len(SKIN), 0.3, 0.5), SKIN_OL)
    for ry in (oy + 18, oy + 20):
        cv.hline(cx - 2, cx + 1, ry, SKIN[1])
    # Ears, then the head.
    for ex in (cx - 7.2, cx + 7.2):
        paint(cv, m_ellipse(ex, oy + 11, 1.6, 2.6), SKIN, f_sphere(ex - 1, oy + 10, 3, len(SKIN)), SKIN_OL)
    head = m_ellipse(cx, oy + 9.5, 7, 9)
    paint(cv, head, SKIN, f_sphere(cx - 1.5, oy + 7.5, 9.5, len(SKIN), 0.12), SKIN_OL, soft=SKIN[1])
    # Face plate seams.
    cv.vline(cx - 6, oy + 9, oy + 13, SKIN[2])
    cv.vline(cx + 5, oy + 9, oy + 13, SKIN[1])
    # Brow ridge, sockets and the glowing eyes.
    cv.hline(cx - 5, cx - 2, oy + 8, SKIN[4])
    cv.hline(cx + 1, cx + 4, oy + 8, SKIN[3])
    for sx in (cx - 3.5, cx + 3.5):
        cv.fill(m_ellipse(sx, oy + 10.5, 2.2, 1.4), SKIN[0])
        ex = int(sx - 1)
        if eyes > 0.5:
            cv.set(ex, oy + 10, GLOW[2])
            cv.set(ex + 1, oy + 10, GLOW[3])
            cv.set(ex - 1, oy + 10, GLOW[1])
            cv.set(ex + 2, oy + 10, GLOW[1])
        elif eyes > 0:
            cv.hline(ex - 1, ex + 2, oy + 10, GLOW[1])
        else:
            cv.hline(ex - 1, ex + 2, oy + 10, SKIN[2])
    # Nose, mouth and chin.
    cv.set(cx - 1, oy + 13, SKIN[2])
    cv.set(cx, oy + 13, SKIN[1])
    cv.hline(cx - 2, cx + 1, oy + 15, SKIN_OL)
    cv.hline(cx - 1, cx, oy + 16, SKIN[4])


def cryo_pod(cv, ox, oy, lid=0.0, glow=True, sleeper=True, L=46):
    """W-Y hypersleep chamber in side view, L x 21. lid is the canopy opening angle in degrees."""
    if glow:
        for i, c in enumerate(("#0f4a5c", "#0b3442")):
            cv.fill(m_ellipse(ox + L / 2, oy + 20.5, L / 2 - 1 - i * 4, 1.6 - i * 0.3), c)
    plinth = m_rect(ox + 4, oy + 16, ox + L - 4, oy + 20)
    paint(cv, plinth, METAL, f_hcyl(oy + 16, oy + 20, len(METAL), 0.2, 0.5), METAL[0])
    for x in range(ox + 6, ox + L - 6, 4):
        cv.set(x, oy + 18, "#3ad0f0")
    shell = m_rrect(ox + 1, oy + 8, ox + L - 1, oy + 17, 4)
    paint(cv, shell, SHELL, f_hcyl(oy + 8, oy + 17, len(SHELL), 0.25, 0.6), SHELL_OL)
    cv.hline(ox + 4, ox + L - 5, oy + 14, SHELL[2])
    cv.hline(ox + 4, ox + L - 5, oy + 15, SHELL[1])
    # Foot end control panel and head end vents.
    cv.rect(ox + L - 8, oy + 11, ox + L - 4, oy + 13, "#1d2229")
    cv.set(ox + L - 7, oy + 11, "#5aff8a")
    cv.set(ox + L - 5, oy + 11, "#ffb030")
    for vx in range(ox + 4, ox + 9, 2):
        cv.vline(vx, oy + 11, oy + 12, SHELL[2])
    # Canopy: a flattened dome hinged at the head end.
    c0, c1, base = ox + 4, ox + L - 6, oy + 10
    hinge = (c0 + 0.5, base)
    a = math.radians(lid)
    pts = []
    H = 6 + L * 0.08
    for k in range(29):
        t = k / 28
        h = H * math.sin(math.pi * t) ** 0.55
        pts.append((c0 + (c1 - c0) * t, base - h))
    rot = []
    for px, py in pts:
        dx, dy = px - hinge[0], py - hinge[1]
        rot.append((hinge[0] + dx * math.cos(a) + dy * math.sin(a), hinge[1] - dx * math.sin(a) + dy * math.cos(a)))
    canopy = m_poly(rot)
    head = m_ellipse(ox + L * 0.24, oy + 6.6, 2.4, 2.3)
    body = m_capsule(ox + L * 0.24 + 3.5, oy + 7.4, ox + L * 0.76, oy + 8, 2.1)
    cv.fill(m_rect(c0 + 1, oy + 8, c1, oy + 10), "#0a1a22")
    if sleeper:
        # The sleeper under a white sheet, head on a pillow at the hinge end.
        cv.fill(m_rect(int(ox + L * 0.24 - 3), oy + 8, int(ox + L * 0.24 + 2), oy + 10), SHELL[3])
        paint(cv, head, FLESH, f_sphere(ox + L * 0.24 - 1, oy + 5.8, 3.2, len(FLESH)), FLESH[0])
        for x, y in head:
            if y < oy + 6 and x < ox + L * 0.24:
                cv.set(x, y, "#3a2a1e")
        paint(cv, body, SHELL, f_hcyl(oy + 6, oy + 11, len(SHELL), 0.3, 1.5), SHELL_OL)
    if lid > 0:
        lo = min(y for _, y in canopy)
        paint(cv, canopy, GLASS, lambda x, y: 4.2 - (y - lo) * 0.4, GLASS[0], soft=GLASS[1])
    else:
        # Tinted glass over whatever is inside: blend instead of covering.
        rim = edge(canopy)
        for x, y in canopy:
            v = 4.0 - (y - (base - H)) / H * 3.0
            under = cv.get(x, y)
            g = pick(GLASS, v, x, y, 0)
            inside = sleeper and ((x, y) in head or (x, y) in body or y >= oy + 8)
            cv.set(x, y, mix(under, g, 0.3) if inside else g)
        for x, y in rim:
            cv.set(x, y, GLASS[0])
        # Glass glints along the top.
        gx = int(c0 + (c1 - c0) * 0.3)
        for i in range(5):
            cv.set(gx + i, int(base - H + 1.5), GLASS[4])
        cv.set(gx + 8, int(base - H + 1.5), GLASS[4])
        cv.set(gx + 9, int(base - H + 1.5), GLASS[3])
    cv.hline(ox + 3, ox + L - 4, oy + 10, SHELL_OL)


def beer_can(cv, ox, oy, can=CAN_RED, band=GOLD):
    """Aspen lager can, 14x27."""
    body = m_rect(ox + 1, oy + 3, ox + 13, oy + 24) | m_ellipse(ox + 7, oy + 24, 6, 1.6)
    fn = f_cyl(ox + 1, ox + 13, len(can), 0.3, 0.4)
    paint(cv, body, can, fn, can[0])
    gfn = f_cyl(ox + 1, ox + 13, len(band), 0.3, 0.6)
    for gy in (oy + 6, oy + 19):
        for x in range(ox + 2, ox + 12):
            cv.set(x, gy, pick(band, gfn(x, gy), x, gy))
            cv.set(x, gy + 1, pick(band, gfn(x, gy) - 1, x, gy + 1))
    # The Aspen mountain.
    mtn = m_poly([(ox + 2.5, oy + 17), (ox + 7, oy + 9.5), (ox + 11.5, oy + 17)])
    for x, y in mtn:
        cv.set(x, y, "#e8f0ff" if x < ox + 7 else "#9ab0d0")
    for x, y in m_poly([(ox + 5.4, oy + 12.2), (ox + 7, oy + 9.5), (ox + 8.6, oy + 12.2)]):
        cv.set(x, y, WHITE)
    cv.set(ox + 7, oy + 12, "#9ab0d0")
    # Lid and rims.
    rim = m_ellipse(ox + 7, oy + 3, 6, 1.8)
    paint(cv, rim, METAL, f_cyl(ox + 1, ox + 13, len(METAL), 0.3, 1.5), METAL[1])
    cv.fill(m_ellipse(ox + 7, oy + 3, 4, 0.9), METAL[2])
    cv.set(ox + 8, oy + 3, METAL[5])
    cv.set(ox + 9, oy + 3, METAL[4])
    for x in range(ox + 2, ox + 12):
        cv.set(x, oy + 23, pick(METAL, fn(x, oy + 23) * 0.9, x, oy + 23))
    for x in range(ox + 3, ox + 11):
        cv.set(x, oy + 25, pick(METAL, fn(x, oy + 25) * 0.6, x, oy + 25))
    # Condensation.
    rng = random.Random(ox * 7 + oy)
    for _ in range(9):
        x, y = rng.randrange(ox + 2, ox + 12), rng.randrange(oy + 5, oy + 22)
        if cv.get(x, y)[:3] in [c[:3] for c in can]:
            cv.set(x, y, can[5])
            cv.set(x, y + 1, can[1])


def power_cell(cv, ox, oy, band=LIME, mark="#0b0a10"):
    """Seegson power cell, 10x25."""
    cv.fill(m_rect(ox + 3, oy, ox + 7, oy + 2), METAL[4])
    cv.set(ox + 3, oy, METAL[5])
    cv.set(ox + 6, oy + 1, METAL[2])
    body = m_rect(ox, oy + 3, ox + 10, oy + 23) | m_ellipse(ox + 5, oy + 3, 5, 1.4) | m_ellipse(ox + 5, oy + 23, 5, 1.4)
    paint(cv, body, BLACKP, f_cyl(ox, ox + 10, len(BLACKP), 0.3, 0.3), INK)
    bfn = f_cyl(ox, ox + 10, len(band), 0.3, 0.6)
    for y in range(oy + 8, oy + 18):
        for x in range(ox + 1, ox + 9):
            cv.set(x, y, pick(band, bfn(x, y), x, y))
    bitmap(cv, ox + 3, oy + 9, ["..#", ".##", "##.", ".##", "##.", "#.."][:6] + ["...", "..."], {"#": mark})
    bitmap(cv, ox + 2, oy + 9, [".#..", "###.", ".#.."], {"#": band[4]})
    rim = m_ellipse(ox + 5, oy + 3, 5, 1.4)
    paint(cv, rim, METAL, f_cyl(ox, ox + 10, len(METAL), 0.3, 1.0), METAL[1])
    for x in range(ox + 1, ox + 9):
        cv.set(x, oy + 22, METAL[2] if x > ox + 5 else METAL[3])
    cv.vline(ox + 2, oy + 4, oy + 7, BLACKP[4])
    cv.vline(ox + 2, oy + 18, oy + 21, BLACKP[4])


def box3d(cv, x, y, w, h, dx, dy, front, top, side, outline, fv=None):
    F = m_rect(x, y + dy, x + w, y + dy + h)
    T = m_poly([(x, y + dy), (x + dx, y), (x + w + dx, y), (x + w, y + dy)])
    S = m_poly([(x + w, y + dy), (x + w + dx, y), (x + w + dx, y + h), (x + w, y + dy + h)])
    paint(cv, T, top, lambda px, py: len(top) - 1.4)
    paint(cv, S, side, f_lin(x + w, y, x + w + dx, y + h, len(side) - 2.2, len(side) - 3.6))
    paint(cv, F, front, fv or f_lin(x, y + dy, x + w * 0.4, y + dy + h, len(front) - 1.6, len(front) - 3.0))
    allm = F | T | S
    for px, py in edge(allm):
        cv.set(px, py, outline)
    cv.hline(x + 1, x + w - 1, y + dy, top[-1])
    cv.line(x + w, y + dy, x + w + dx - 1, y + 1, side[-1] if len(side) else top[-1])
    return F, T, S


def ration_box(cv, ox, oy):
    """W-Y ration pack box, 34x26."""
    box3d(cv, ox, oy, 24, 18, 9, 6, KHAKI, KHAKI, KHAKI[:5], "#16140a")
    # Label panel with the pack name and a red flavour band.
    cv.rect(ox + 2, oy + 9, ox + 22, oy + 16, "#d8cf98")
    cv.hline(ox + 2, ox + 21, oy + 16, "#8a8044")
    draw_text_c(cv, F3, ox + 12, oy + 10, "MRE", "#1a1a12")
    cv.rect(ox + 2, oy + 18, ox + 22, oy + 21, "#a01810")
    for x in range(ox + 3, ox + 21, 3):
        cv.set(x, oy + 19, "#ffd040")
    # The W-Y mark stencilled on the side.
    bitmap(cv, ox + 25, oy + 9, WY_MARK_S[:4], {"#": KHAKI[1]})
    cv.hline(ox + 26, ox + 30, oy + 16, KHAKI[1])
    cv.hline(ox + 26, ox + 29, oy + 19, KHAKI[1])
    # Strap.
    cv.line(ox + 6, oy + 6, ox + 15, oy, KHAKI[1])
    cv.vline(ox + 6, oy + 6, oy + 8, KHAKI[1])


def holo_head(cv, ox, oy, phase=0):
    """Hyperdyne hologram: a synthetic's head in profile facing left, drawn in cyan scanlines. 30x36."""
    pts = [(13, 1), (19, 1), (24, 4), (26, 9), (26, 15), (25, 19), (23, 22), (22, 25), (22, 29), (25, 34),
           (8, 34), (10, 30), (10, 27), (8, 26.5), (6, 25.5), (6.5, 23.5), (5.5, 22.5), (6.5, 21.5), (5.5, 20),
           (4, 19.2), (4.5, 18.4), (2.5, 17), (4.5, 13.5), (5, 11), (6, 9), (7, 5), (10, 2)]
    head = m_poly([(ox + x, oy + y) for x, y in pts])
    for x, y in head:
        if (y + phase) % 2 == 0:
            cv.set(x, y, CYAN[2] if x > ox + 14 else CYAN[3])
        else:
            cv.set(x, y, mix(cv.get(x, y), CYAN[0], 0.6))
    for x, y in edge(head):
        cv.set(x, y, CYAN[4] if x < ox + 16 else CYAN[3])
    # Hair line, eye, ear, lips.
    for x in range(ox + 8, ox + 26):
        hy = oy + 6 + int(abs(x - (ox + 15)) * 0.25)
        if (x, hy) in head:
            cv.set(x, hy, CYAN[4])
    cv.hline(ox + 7, ox + 9, oy + 12, CYAN[5])
    cv.set(ox + 7, oy + 12, CYAN[1])
    cv.hline(ox + 6, ox + 9, oy + 10, CYAN[4])
    for x, y in edge(m_ellipse(ox + 17.5, oy + 14.5, 2, 3)):
        if x >= ox + 17:
            cv.set(x, y, CYAN[4])
    cv.hline(ox + 6, ox + 8, oy + 22, CYAN[1])
    cv.hline(ox + 9, ox + 12, oy + 17, CYAN[1])
    cv.line(ox + 10, oy + 27, ox + 21, oy + 27, CYAN[3])
    # A glitch row slipping sideways.
    gy = oy + 15 + (phase * 5) % 12
    row = [cv.get(x, gy) for x in range(ox, ox + 30)]
    for i, c in enumerate(row):
        if c[3]:
            cv.set(ox + i + 2, gy, c)


def vial(cv, ox, oy, liquid=None):
    """Lasalle vaccine vial, 10x26."""
    liquid = liquid or ramp("#0a3a1a", "#147a34", "#22b04c", "#6ee68a", "#c4ffd0")
    cv.fill(m_rect(ox + 2, oy, ox + 8, oy + 4), "#b8bec6")
    cap = m_rect(ox + 2, oy, ox + 8, oy + 4)
    paint(cv, cap, METAL, f_cyl(ox + 2, ox + 8, len(METAL), 0.3, 1.5), METAL[1])
    cv.hline(ox + 2, ox + 7, oy + 2, METAL[2])
    neck = m_rect(ox + 3, oy + 4, ox + 7, oy + 6)
    glass = neck | m_rrect(ox, oy + 6, ox + 10, oy + 26, 2)
    glass_r = ramp("#2a3a44", "#4a6270", "#7c98a6", "#b8d4de", "#eaf8ff")
    paint(cv, glass, glass_r, f_cyl(ox, ox + 10, 5, 0.3, 0.5), "#1a2830")
    lq = {(x, y) for x, y in glass if y >= oy + 12 and (x, y) not in edge(glass)}
    paint(cv, lq, liquid, f_cyl(ox, ox + 10, len(liquid), 0.3, 0.4))
    cv.hline(ox + 1, ox + 8, oy + 12, liquid[4])
    # Label.
    cv.rect(ox + 1, oy + 15, ox + 9, oy + 22, WHITE)
    cv.hline(ox + 1, ox + 8, oy + 21, "#c8c4b8")
    bitmap(cv, ox + 3, oy + 16, [".#.", "###", ".#."], {"#": "#18a048"})
    cv.hline(ox + 2, ox + 7, oy + 20, "#606060")
    cv.vline(ox + 2, oy + 7, oy + 13, glass_r[4])
    cv.vline(ox + 2, oy + 23, oy + 24, glass_r[4])


def injector(cv, ox, oy):
    """Autoinjector pen lying flat, 26x7."""
    body = m_capsule(ox + 3, oy + 3, ox + 20, oy + 3, 3)
    paint(cv, body, SHELL, f_hcyl(oy, oy + 7, len(SHELL), 0.3, 0.8), SHELL_OL)
    cv.rect(ox + 8, oy + 1, ox + 14, oy + 6, "#18a048")
    cv.hline(ox + 8, ox + 13, oy + 1, "#6ee68a")
    cv.hline(ox + 8, ox + 13, oy + 5, "#0a5a24")
    cap = m_rect(ox + 20, oy + 1, ox + 25, oy + 6)
    paint(cv, cap, ORANGE, f_hcyl(oy + 1, oy + 6, len(ORANGE), 0.3, 1), ORANGE[0])
    cv.set(ox + 25, oy + 3, METAL[3])


def shuttle(cv, ox, oy, hull=SHELL, stripe="#e86a1a", flame=True):
    """Passenger shuttle flying left, 44x14."""
    if flame:
        for i, c in enumerate(("#ff6a00", "#ffb030", "#fff2c0")):
            cv.fill(m_ellipse(ox + 43 - i, oy + 8, 5 - i * 1.4, 2 - i * 0.5), c)
    body = m_poly([(ox + 1, oy + 8), (ox + 6, oy + 5), (ox + 14, oy + 3.5), (ox + 32, oy + 3.5), (ox + 40, oy + 5),
                   (ox + 40, oy + 11), (ox + 10, oy + 11), (ox + 4, oy + 10)])
    fin = m_poly([(ox + 30, oy + 4), (ox + 35, oy), (ox + 39, oy), (ox + 38, oy + 4)])
    wing = m_poly([(ox + 16, oy + 10), (ox + 30, oy + 10), (ox + 36, oy + 14), (ox + 24, oy + 14)])
    paint(cv, fin, hull, lambda x, y: 3, SHELL_OL)
    paint(cv, body, hull, f_hcyl(oy + 3, oy + 11, len(hull), 0.3, 0.6), SHELL_OL, soft=hull[2])
    paint(cv, wing, hull, lambda x, y: 1.8, SHELL_OL)
    cv.hline(ox + 6, ox + 39, oy + 8, stripe)
    cv.fill(m_poly([(ox + 4, oy + 6.5), (ox + 7, oy + 5), (ox + 12, oy + 4.5), (ox + 12, oy + 6.5)]), "#1a3a50")
    cv.set(ox + 8, oy + 5, "#9adcf0")
    cv.set(ox + 9, oy + 5, "#9adcf0")
    for wx in range(ox + 15, ox + 31, 3):
        cv.set(wx, oy + 6, "#ffe6a0")
    cv.vline(ox + 40, oy + 5, oy + 11, METAL[1])


def power_loader(cv, ox, oy):
    """Weyland-Yutani P-5000 powered work loader in side view facing right, 44x30."""
    YL = YELLOW
    OL = rgba("#1a1204")
    flat = lambda v: (lambda x, y: v)
    # Power pack behind the cage.
    pack = m_rrect(ox + 1, oy + 5, ox + 8, oy + 18, 1)
    paint(cv, pack, METAL, f_cyl(ox + 1, ox + 8, len(METAL), 0.3, 0.8), METAL[0], dither=0)
    for y in range(oy + 7, oy + 17, 2):
        cv.hline(ox + 2, ox + 6, y, METAL[1])
    # Legs: back leg in shadow, then the skid foot both stand on.
    for lx, v in ((ox + 15, 1.6), (ox + 8, 3.0)):
        leg = m_rect(lx, oy + 18, lx + 6, oy + 27)
        paint(cv, leg, YL, flat(v) if v < 2 else f_cyl(lx, lx + 6, len(YL), 0.3, 1.5), OL, dither=0)
        cv.rect(lx + 2, oy + 20, lx + 4, oy + 27, METAL[3] if v > 2 else METAL[2])
        cv.vline(lx + 2, oy + 20, oy + 26, METAL[5] if v > 2 else METAL[3])
    foot = m_rrect(ox + 3, oy + 26, ox + 26, oy + 30, 1)
    paint(cv, foot, METAL, f_hcyl(oy + 26, oy + 30, len(METAL), 0.2, 1.0), METAL[0], dither=0)
    for x in range(ox + 5, ox + 25, 3):
        cv.set(x, oy + 28, METAL[1])
    # Roll cage.
    cage = (m_rect(ox + 6, oy, ox + 8, oy + 13) | m_rect(ox + 22, oy, ox + 24, oy + 13) |
            m_rect(ox + 6, oy, ox + 24, oy + 2))
    paint(cv, cage, YL, flat(3.5), OL, dither=0)
    cv.hline(ox + 7, ox + 22, oy + 1, YL[5])
    # Operator in the cage.
    cv.fill(m_rect(ox + 8, oy + 2, ox + 22, oy + 13), "#24262a")
    cv.fill(m_ellipse(ox + 15, oy + 5.5, 2.6, 2.6), "#5a6070")
    cv.hline(ox + 13, ox + 15, oy + 4, "#8a90a0")
    cv.rect(ox + 16, oy + 5, ox + 18, oy + 8, "#d8a888")
    cv.set(ox + 17, oy + 6, "#3a2a20")
    cv.fill(m_rrect(ox + 11, oy + 8, ox + 19, oy + 13, 1), "#4e5e3c")
    cv.hline(ox + 12, ox + 17, oy + 8, "#6a7a50")
    cv.line(ox + 18, oy + 10, ox + 22, oy + 10, "#d8a888")
    # Chassis with hazard stripes.
    chassis = m_rrect(ox + 4, oy + 12, ox + 28, oy + 19, 1)
    paint(cv, chassis, YL, f_hcyl(oy + 12, oy + 19, len(YL), 0.3, 1.5), OL, dither=0)
    for x, y in chassis:
        if oy + 15 <= y <= oy + 17 and (x + y) % 6 < 3 and (x, y) not in edge(chassis):
            cv.set(x, y, "#16120a")
    cv.hline(ox + 5, ox + 26, oy + 13, YL[5])
    # Beacon.
    cv.rect(ox + 13, oy - 2, ox + 17, oy, "#ff8a00")
    cv.hline(ox + 13, ox + 14, oy - 2, "#ffe0a0")
    # Boom arm, wrist and an open claw.
    paint(cv, m_ellipse(ox + 25.5, oy + 11.5, 3.2, 3.2), METAL, f_sphere(ox + 24.5, oy + 10.5, 3.4, len(METAL)),
          METAL[0], dither=0)
    arm = m_rect(ox + 26, oy + 9, ox + 37, oy + 15)
    paint(cv, arm, YL, f_hcyl(oy + 9, oy + 15, len(YL), 0.3, 1.5), OL, dither=0)
    cv.hline(ox + 27, ox + 35, oy + 10, YL[5])
    for x in range(ox + 28, ox + 36, 3):
        cv.vline(x, oy + 12, oy + 13, YL[1])
    wrist = m_rect(ox + 36, oy + 8, ox + 39, oy + 16)
    paint(cv, wrist, METAL, f_cyl(ox + 36, ox + 39, len(METAL), 0.3, 1.0), METAL[0], dither=0)
    top = m_poly([(ox + 38, oy + 8), (ox + 42, oy + 5), (ox + 44, oy + 7), (ox + 44, oy + 9), (ox + 42, oy + 8),
                  (ox + 39, oy + 10)])
    bot = m_poly([(ox + 38, oy + 16), (ox + 42, oy + 19), (ox + 44, oy + 17), (ox + 44, oy + 15), (ox + 42, oy + 16),
                  (ox + 39, oy + 14)])
    paint(cv, top, METAL, flat(4), METAL[0], dither=0)
    paint(cv, bot, METAL, flat(2.6), METAL[0], dither=0)


def crt_terminal(cv, ox, oy, lines=("APOLLO", "READY"), cursor=True, phase=0, w=34):
    """Seegson desk terminal with keyboard, w x 30 (the screen holds w - 14 pixels of text)."""
    r = ox + w - 4
    case = m_rrect(ox + 2, oy, r, oy + 22, 3)
    paint(cv, case, BEIGE, f_lin(ox + 2, oy, ox + w - 12, oy + 22, 4.6, 2.4), BEIGE[0], soft=BEIGE[1])
    # Side depth.
    side = m_poly([(r, oy + 3), (r + 3, oy + 5), (r + 3, oy + 20), (r, oy + 21)])
    paint(cv, side, BEIGE, lambda x, y: 1.8, BEIGE[0])
    bezel = m_rrect(ox + 4, oy + 2, r - 2, oy + 18, 3)
    cv.fill(bezel, BEIGE[1])
    screen = m_rrect(ox + 5, oy + 3, r - 3, oy + 17, 3)
    mid = (ox + 5 + r - 3) / 2
    half = (r - 3 - ox - 5) / 2
    for x, y in screen:
        d = math.hypot((x + 0.5 - mid) / half, (y + 0.5 - (oy + 10)) / 7)
        cv.set(x, y, pick(ramp("#03140a", "#062a12", "#0a3a1a"), 2.2 - d * 2.2, x, y, 4))
    for i, t in enumerate(lines):
        draw_text(cv, F3, ox + 7, oy + 5 + i * 6, t, GREEN_P[4] if i == 0 else GREEN_P[3])
    if cursor:
        cx = ox + 7 + 4 * len(lines[-1])
        cy = oy + 5 + (len(lines) - 1) * 6
        cv.rect(cx, cy, cx + 3, cy + 5, GREEN_P[4])
    scanlines(cv, ox + 5, oy + 3, r - 3, oy + 17, 0.7, 2, phase)
    cv.set(ox + 7, oy + 4, "#8af0a0")
    cv.set(ox + 8, oy + 4, "#4ac060")
    # Badge, power light and vents on the chin.
    cv.rect(ox + 6, oy + 19, ox + 11, oy + 21, "#2f6db0")
    cv.set(r - 4, oy + 20, "#ff3a2a")
    for vx in range(ox + 14, r - 6, 2):
        cv.vline(vx, oy + 19, oy + 20, BEIGE[1])
    # Keyboard.
    kb = m_poly([(ox + 1, oy + 30), (ox + 5, oy + 24), (r + 1, oy + 24), (r + 4, oy + 30)])
    paint(cv, kb, BEIGE, f_lin(ox, oy + 24, ox, oy + 30, 4.4, 2.6), BEIGE[0])
    for ky in (oy + 26, oy + 28):
        for kx in range(ox + 5 - (ky - oy - 26) // 2, r + 1 + (ky - oy - 26) // 2, 2):
            cv.set(kx, ky, BEIGE[2])


def ciggie_pack(cv, ox, oy):
    """Souta Pinks pack with two sticks raised, 24x34."""
    for i, (sx, h) in enumerate(((ox + 5, 6), (ox + 10, 3))):
        stick = m_rect(sx, oy + 6 - h, sx + 3, oy + 8)
        paint(cv, stick, ramp("#8a8478", "#c8c2b4", "#f0ece2", "#ffffff"), f_cyl(sx, sx + 3, 4, 0.3, 0.6), "#5a5448")
        cv.rect(sx, oy + 6 - h + 2, sx + 3, oy + 6 - h + 5, "#d88a3a")
        cv.set(sx + 1, oy + 6 - h + 2, "#f0b060")
        cv.hline(sx, sx + 2, oy + 6 - h, "#5a5448")
    F, T, S = box3d(cv, ox, oy + 4, 17, 26, 6, 4, PINK, PINK, PINK[:5], "#3a0618")
    # White flip lid band and the round crest.
    cv.rect(ox + 1, oy + 9, ox + 16, oy + 14, WHITE)
    cv.hline(ox + 1, ox + 15, oy + 14, "#c8b8c0")
    cv.fill(m_ellipse(ox + 8.5, oy + 21.5, 5, 5), WHITE)
    cv.fill(m_ellipse(ox + 8.5, oy + 21.5, 4, 4), PINK[2])
    draw_text(cv, F5, ox + 7, oy + 18, "S", WHITE)
    draw_text(cv, F3, ox + 3, oy + 10, "SOUTA", PINK[1], gap=0)
    cv.rect(ox + 1, oy + 28, ox + 16, oy + 30, PINK[1])
    for x in range(ox + 2, ox + 16, 2):
        cv.set(x, oy + 28, "#ffd040")


def coffee_mug(cv, ox, oy, steam_phase=0):
    """Red Dust coffee mug with steam, 24x30."""
    handle = m_ellipse(ox + 18, oy + 18.5, 5, 5.5) - m_ellipse(ox + 18, oy + 18.5, 2.6, 3.2)
    mug_r = ramp("#2a0606", "#5a0e0c", "#8e1a14", "#c02e22", "#e85a40", "#ffa080")
    paint(cv, handle, mug_r, f_sphere(ox + 16, oy + 15, 7, len(mug_r)), mug_r[0])
    body = m_rect(ox + 1, oy + 11, ox + 18, oy + 27) | m_ellipse(ox + 9.5, oy + 27, 8.5, 2)
    paint(cv, body, mug_r, f_cyl(ox + 1, ox + 18, len(mug_r), 0.3, 0.4), mug_r[0])
    rim = m_ellipse(ox + 9.5, oy + 11, 8.5, 2.4)
    paint(cv, rim, mug_r, lambda x, y: 4.6, mug_r[0])
    cv.fill(m_ellipse(ox + 9.5, oy + 11.2, 7, 1.5), "#2a1408")
    cv.hline(ox + 6, ox + 9, oy + 11, "#5a3018")
    # White label with a red planet.
    cv.fill(m_ellipse(ox + 9, oy + 19.5, 4.5, 4.5), WHITE)
    paint(cv, m_ellipse(ox + 9, oy + 19.5, 3.2, 3.2), ORANGE, f_sphere(ox + 8, oy + 18.5, 3.6, len(ORANGE)))
    cv.hline(ox + 4, ox + 13, oy + 20, "#7a2a06")
    cv.vline(ox + 3, oy + 13, oy + 25, mug_r[5])
    # Steam wisps.
    for i, sx in enumerate((ox + 6, ox + 11)):
        for k in range(9):
            y = oy + 8 - k
            x = sx + int(round(math.sin((k + steam_phase + i * 3) * 0.75) * 1.5))
            cv.set(x, y, "#d8ccc0" if k < 5 else "#a8988a")


def hab_blueprint(cv, ox, oy, line, faint):
    """Prefab habitat module as white line art, 66x30."""
    body = m_rrect(ox + 4, oy + 9, ox + 52, oy + 23, 4)
    for x, y in edge(body):
        cv.set(x, y, line)
    roof = m_ellipse(ox + 28, oy + 10, 16, 7)
    for x, y in edge(roof):
        if y < oy + 9:
            cv.set(x, y, line)
    cv.hline(ox + 12, ox + 43, oy + 9, faint)
    for wx in range(ox + 9, ox + 40, 8):
        cv.fill(edge(m_rect(wx, oy + 12, wx + 5, oy + 16)), line)
        cv.set(wx + 2, oy + 14, faint)
    # Airlock with a porthole.
    cv.fill(edge(m_rect(ox + 42, oy + 11, ox + 49, oy + 22)), line)
    cv.fill(edge(m_ellipse(ox + 45.5, oy + 14.5, 2, 2)), line)
    # Stair, struts and pads.
    for i in range(4):
        cv.hline(ox + 50 + i * 2, ox + 52 + i * 2, oy + 23 + i * 2, line)
        cv.vline(ox + 52 + i * 2, oy + 23 + i * 2, oy + 25 + i * 2, line)
    for sx in (ox + 10, ox + 26, ox + 42):
        cv.line(sx, oy + 23, sx - 3, oy + 28, line)
        cv.line(sx, oy + 23, sx + 3, oy + 28, line)
        cv.hline(sx - 5, sx + 5, oy + 29, line)
    # Antenna and dish.
    cv.vline(ox + 28, oy, oy + 3, line)
    cv.hline(ox + 26, ox + 30, oy, line)
    # Dimension lines.
    y = oy + 32
    cv.hline(ox + 4, ox + 51, y, faint)
    for x in (ox + 4, ox + 51):
        cv.vline(x, y - 2, y + 2, faint)
    bitmap(cv, ox + 5, y - 1, ["#.", "##", "#."], {"#": faint})
    bitmap(cv, ox + 49, y - 1, [".#", "##", ".#"], {"#": faint})
    w = text_width(F3, "12.5 M")
    cv.rect(ox + 28 - w // 2 - 1, y - 2, ox + 28 + w // 2 + 2, y + 3, cv.get(ox + 2, oy + 2))
    draw_text_c(cv, F3, ox + 28, y - 2, "12.5 M", line)
    x = ox
    cv.vline(x, oy + 3, oy + 23, faint)
    cv.hline(x - 1, x + 1, oy + 3, faint)
    cv.hline(x - 1, x + 1, oy + 23, faint)
    # A colonist for scale.
    px, py = ox + 58, oy + 20
    cv.fill(edge(m_ellipse(px + 0.5, py + 1.5, 1.6, 1.6)), line)
    cv.vline(px, py + 3, py + 7, line)
    cv.line(px, py + 4, px - 2, py + 6, line)
    cv.line(px, py + 4, px + 2, py + 6, line)
    cv.line(px, py + 7, px - 2, py + 10, line)
    cv.line(px, py + 7, px + 2, py + 10, line)


def processor_tower(cv, ox, oy, ground, lights_on=True):
    """M-class atmosphere processor silhouette, rim lit from the left. Footprint about 56 wide."""
    D = ramp("#0c0810", "#1a1220", "#2a1e2e", "#3e2c3e")
    RIM = rgba("#f0904a")

    def part(mask):
        for x, y in mask:
            cv.set(x, y, D[1] if (x + y) % 9 else D[2])
        for x, y in edge(mask):
            if (x - 1, y) not in mask:
                cv.set(x, y, RIM)
            elif (x, y - 1) not in mask:
                cv.set(x, y, D[3])
            else:
                cv.set(x, y, D[0])

    part(m_rect(ox + 6, oy + 15, ox + 13, ground))
    part(m_rect(ox + 39, oy + 10, ox + 47, ground))
    part(m_rect(ox + 47, oy + 20, ox + 54, ground))
    part(m_rect(ox, oy + 26, ox + 56, ground))
    part(m_rect(ox + 18, oy + 2, ox + 34, oy + 6))
    part(m_rect(ox + 20, oy + 4, ox + 32, ground))
    part(m_rect(ox + 23, oy - 4, ox + 29, oy + 3))
    part(m_rect(ox + 13, oy + 19, ox + 20, oy + 22))
    part(m_rect(ox + 32, oy + 15, ox + 39, oy + 18))
    for by in range(oy + 9, ground - 4, 5):
        cv.hline(ox + 21, ox + 30, by, D[3])
        cv.set(ox + 20, by, RIM)
    # Lit windows and aviation lights.
    rng = random.Random(42)
    for _ in range(14):
        x, y = rng.randrange(ox + 2, ox + 54), rng.randrange(oy + 27, ground - 1)
        cv.set(x, y, "#ffc860" if rng.random() > 0.3 else "#ff9a30")
    for x in range(ox + 22, ox + 30, 2):
        cv.set(x, oy + 12, "#ffc860")
    if lights_on:
        for x, y in ((ox + 26, oy - 5), (ox + 9, oy + 14), (ox + 43, oy + 9)):
            cv.set(x, y, "#ff3a2a")
            cv.set(x, y - 1, "#ff9a8a")


def cloud_plume(cv, x0, y0, rng):
    """Billowing steam from a stack top, drifting up and right."""
    C = ramp("#4a3448", "#7a5a6a", "#a88a8a", "#d8bca8", "#f8e4cc")
    puffs = []
    x, y, r = x0, y0, 2.5
    for i in range(10):
        puffs.append((x, y, r))
        x += 4.5 + rng.random() * 2.5
        y += 0.4 - rng.random() * 0.9
        r += 0.45
    for px, py, pr in puffs:
        m = m_ellipse(px, py, pr * 1.2, pr)
        paint(cv, m, C, f_sphere(px - pr * 0.5, py + pr * 0.6, pr * 1.7, len(C), 0.25), None, 4)
        for x, y in m:
            if cv.inside(x, y) and (x, y + 1) not in m and rng.random() < 0.5:
                cv.set(x, y, C[1])


# -- Banners ------------------------------------------------------------------------------------------


def b_working_joe():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#123a64", "#0c2848", "#071a32", "#050f20"])
    radial(cv, 21, 22, 22, ["#2a6aa8", "#1c5288", "#14406e", "#0f325a"], 0, 0, 46, BH)
    # Re-blend the spotlight edge into the background.
    for y in range(BH):
        for x in range(46):
            if math.hypot(x - 21, y - 22) > 21:
                cv.set(x, y, pick(ramp("#123a64", "#0c2848", "#071a32", "#050f20"), y / (BH - 1) * 3, x, y, 4))
    scanlines(cv, 0, 0, BW, BH, 0.86)
    working_joe(cv, 6, 5)
    # Wordmark.
    blue_white = bands((3, "#ffffff"), (3, "#d8ecff"), (3, "#9cc8f0"))
    draw_text(cv, BOLD, 44, 4, "SEEGSON", blue_white, outline="#06101e", shadow="#2f6db0", shadow_off=(1, 1))
    cv.hline(44, 96, 15, "#2f6db0")
    cv.hline(44, 96, 16, "#1c4a80")
    draw_text(cv, F5, 44, 19, "WORKING JOE", "#ffb030", shadow="#3a1c00")
    draw_text(cv, F3, 44, 29, "ALWAYS ON SHIFT.", "#9cc8f0")
    sticker(cv, 136, 19, 17, 12, 14, ramp("#5a0808", "#a01010", "#d82018", "#ff4a2a", "#ff8a5a"), "#2a0404",
            [(F5, "NEW", WHITE, -8), (F3, "MODEL", "#ffe0a0", 1)], shadow="#020810")
    frame(cv, "#03080f", "#2f6db0", "#081a30")
    return cv


def b_hypersleep():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#01030a", "#030818", "#071228", "#0c1c3a"])
    rng = random.Random(7)
    starfield(cv, rng, 1, 1, BW - 1, BH - 1, 80)
    # A planet limb low on the left.
    planet = m_ellipse(14, 60, 40, 26)
    paint(cv, planet, ramp("#08141e", "#0e2a3a", "#16465a", "#2a6a7a", "#5aa0a8"),
          f_sphere(0, 34, 40, 5, 0.0, 1.4), None, 4)
    for x, y in halo(planet, False):
        if y < BH:
            cv.set(x, y, "#3a8aa0")
    cryo_pod(cv, 5, 14, L=52)
    wy_logo(cv, 62, 4, "#c8d4f0", small=True)
    draw_text(cv, F3, 73, 4, "WEYLAND-YUTANI", "#c8d4f0")
    draw_text(cv, BOLD, 62, 11, "HYPERSLEEP", bands((2, "#ffffff"), (3, "#d8f6ff"), (2, "#8ee0f4"), (2, "#3ab0d0")),
              outline="#020610", shadow="#0a3a50")
    draw_text(cv, F5, 62, 23, "WAKE REFRESHED", "#ffd27a", shadow="#2a1a00")
    draw_text(cv, F3, 62, 32, "DELTA-7 CRYO CHAMBERS", "#7a8aaa")
    frame(cv, "#000000", "#1c3a5a", None)
    return cv


def b_aspen():
    cv = Canvas(BW, BH)
    sunrays(cv, 0, 0, BW, BH, 118, 46, 24, rgba("#8a0c12"), rgba("#a8161c"), 0.07)
    # Vignette: darken towards the left and bottom.
    for y in range(BH):
        for x in range(BW):
            d = math.hypot((x - 118) / 120, (y - 30) / 40)
            if d > 0.6 + threshold(x, y, 4) * 0.25:
                cv.tint({(x, y)}, 0.72)
    halftone(cv, 0, 30, 100, BH, rgba("#5a060a"), lambda x, y: (y - 30) / 10 + 0.2)
    draw_text(cv, BOLD, 7, 3, "ASPEN", bands((4, "#fff6c0"), (5, "#ffe068"), (5, "#f0b628"), (4, "#c47e10")),
              outline="#2a0204", shadow="#2a0204", scale=2, italic=3, shadow_off=(2, 2))
    draw_text(cv, F5, 9, 24, "ICE COLD LAGER", WHITE, shadow="#3a0206")
    draw_text(cv, F3, 9, 33, "GLACIER-WATER BREWED", "#ffc8a0")
    # Two cans, the back one smaller and darker.
    back = Canvas(14, 27)
    beer_can(back, 0, 0)
    back.tint(m_rect(0, 0, 14, 27), 0.7)
    cv.paste(back, 96, 6)
    beer_can(cv, 107, 10)
    for x, y in ((95, 4), (123, 8), (104, 36)):
        blink(cv, x, y, "#ffc8a0")
    price_tag(cv, 125, 7, 32, 17, ramp("#8a6a10", "#c49a1a", "#f0c83a", "#ffe68a", "#fff8d0"), "#3a2a04",
              [(F3, "6-PACK", "#3a1a02", 7, 3), (F5, "$9.99", "#8a0c12", 5, 9)], string=(136, 1))
    frame(cv, "#200204", "#d84030", "#4a0408")
    return cv


def b_power_cells():
    cv = Canvas(BW, BH, "#021008")
    for x in range(0, BW, 6):
        cv.vline(x, 0, BH, "#06200e")
    for y in range(2, BH, 6):
        cv.hline(0, BW, y, "#06200e")
    radial(cv, 24, 22, 26, ["#0c3a18", "#082a12", "#041a0c", "#021008"], 0, 0, 50, BH)
    for x in range(0, 50, 6):
        cv.vline(x, 0, BH, "#0a3016")
    for y in range(2, BH, 6):
        cv.hline(0, 50, y, "#0a3016")
    for i, x in enumerate((8, 20, 32)):
        power_cell(cv, x, 9 - (i % 2) * 3)
    # Zap between the terminals.
    for x, y in ((14, 3), (15, 2), (16, 3), (17, 4), (26, 2), (27, 1), (28, 2), (29, 3)):
        cv.set(x, y, GREEN_P[5])
    draw_text(cv, F3, 52, 4, "SEEGSON", GREEN_P[3])
    draw_text(cv, F3, 82, 4, "APOLLO SERIES", GREEN_P[2])
    draw_text(cv, BOLD, 52, 11, "POWER CELLS", bands((1, GREEN_P[5]), (6, GREEN_P[4]), (2, GREEN_P[3])),
              outline="#010804", shadow=GREEN_P[1])
    # Charge meter.
    cv.fill(edge(m_rect(52, 24, 112, 31)), GREEN_P[3])
    for i in range(11):
        c = GREEN_P[4] if i < 10 else GREEN_P[1]
        cv.rect(54 + i * 5, 26, 58 + i * 5, 29, c)
    cv.rect(112, 26, 114, 29, GREEN_P[3])
    draw_text(cv, F3, 52, 33, "LASTS THREE SHIFTS LONGER", GREEN_P[3])
    # Price box.
    cv.rect(129, 8, 157, 31, "#031a0a")
    cv.fill(edge(m_rect(129, 8, 157, 31)), GREEN_P[4])
    cv.fill(edge(m_rect(131, 10, 155, 29)), GREEN_P[1])
    draw_text_c(cv, F3, 143, 12, "4-PACK", GREEN_P[3])
    draw_text_c(cv, BOLD, 143, 18, "4.50", GREEN_P[5])
    scanlines(cv, 0, 0, BW, BH, 0.72)
    frame(cv, "#000000", "#1a4a28", "#010804")
    for x, y in ((1, 1), (BW - 2, 1), (1, BH - 2), (BW - 2, BH - 2)):
        cv.set(x, y, "#000000")
    return cv


def b_rations():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#5a5430", "#4a4426", "#3a361e"])
    rng = random.Random(3)
    for _ in range(420):
        x, y = rng.randrange(BW), rng.randrange(BH)
        cv.tint({(x, y)}, 0.85 if rng.random() < 0.6 else 1.12)
    # Diagonal hazard band on the left.
    for y in range(BH):
        for x in range(0, 7):
            cv.set(x, y, "#e8c020" if (x + y) % 8 < 4 else "#16140a")
    cv.vline(7, 0, BH, "#16140a")
    wy_logo(cv, 12, 4, "#e2d9a0", small=True, shadow="#1a180c")
    draw_text(cv, F3, 23, 4, "WEYLAND-YUTANI", "#e2d9a0", shadow="#1a180c")
    draw_text(cv, BOLD, 12, 11, "RATIONS", bands((5, "#f4eccc"), (4, "#d4c890")), outline="#16140a",
              shadow="#16140a", shadow_off=(1, 1))
    draw_text(cv, F3, 12, 22, "HOT MEAL IN 90 SEC.", "#ffd040", shadow="#16140a")
    ribbon(cv, 17, 99, 29, 8, ramp("#4a0806", "#8a1410", "#c02418", "#e84a30", "#ff8a6a"), "#2a0404",
           "NOW ON COLONY SHELVES", F3, WHITE)
    ration_box(cv, 118, 10)
    sticker(cv, 105, 13, 12, 9, 12, ramp("#6a5004", "#b08a08", "#e8bc18", "#ffe048", "#fff4a0"), "#3a2a02",
            [(F5, "NEW", "#8a1410", -4)], shadow="#16140a")
    frame(cv, "#16140a", None, None)
    return cv


def b_hyperdyne():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#010308", "#020812", "#041020", "#06182c"])
    # Perspective floor grid under the hologram.
    for i in range(-6, 7):
        cv.line(130 + i * 3, 33, 130 + i * 9, BH - 1, "#0a2a3a")
    for y in (34, 36, 39):
        cv.hline(108, 154, y, "#0a2a3a")
    # Emitter beam cone.
    for y in range(4, 36):
        w = 4 + (36 - y) * 0.55
        for x in range(int(132 - w), int(132 + w)):
            if (x + y) % 3 == 0:
                cv.set(x, y, "#082a36")
    holo_head(cv, 117, 1)
    em = m_ellipse(132, 36, 10, 2.5)
    paint(cv, em, METAL, f_hcyl(33, 39, len(METAL), 0.2, 0.8), METAL[0])
    cv.hline(124, 140, 35, CYAN[4])
    bitmap(cv, 8, 4, HYPER_MARK, {"#": CYAN[3]})
    draw_text(cv, BOLD, 18, 3, "HYPERDYNE", bands((3, WHITE), (6, "#c8e8f0")), outline="#010308", shadow=CYAN[1])
    draw_text(cv, F3, 19, 14, "S Y S T E M S", CYAN[3], gap=1)
    cv.hline(8, 92, 21, "#123a4a")
    draw_text(cv, F5, 8, 24, "MODEL 341-B", "#ffb030", shadow="#2a1600")
    draw_text(cv, F3, 8, 33, "THE SYNTHETIC COMPANION", "#8ab4c4")
    frame(cv, "#000000", "#0a2a3a", None)
    return cv


def b_lasalle():
    cv = Canvas(BW, BH)
    split = lambda y: 98 + (BH - y) // 3
    vgrad(cv, 0, 0, BW, BH, ["#f6faf6", "#e4efe8", "#d0e2d8"])
    for y in range(BH):
        for x in range(split(y), BW):
            cv.set(x, y, pick(TEAL, 2.6 - math.hypot(x - 128, y - 18) / 18, x, y, 4))
        cv.set(split(y), y, "#18a048")
        cv.set(split(y) - 1, y, "#9ad8b0")
    # Soft rings behind the products.
    for r in (12, 16):
        for x, y in edge(m_ellipse(128, 20, r, r)):
            if x > split(y) + 1:
                cv.set(x, y, TEAL[3])
    vial(cv, 116, 6)
    vial(cv, 129, 9, ramp("#3a1a04", "#8a4a0a", "#d08a1a", "#ffc860", "#fff0c0"))
    injector(cv, 110, 31)
    # Cross logo.
    cross = m_rect(10, 6, 14, 18) | m_rect(6, 10, 18, 14)
    paint(cv, cross, ramp("#0a5a24", "#128a3a", "#18a048", "#5ad080"), f_lin(6, 6, 18, 18, 3, 1), "#064a1c")
    draw_text(cv, BOLD, 23, 4, "LASALLE", bands((4, "#106a62"), (5, "#0a4842")))
    draw_text(cv, F3, 24, 15, "BIONATIONAL", "#6a8078", gap=2)
    cv.hline(6, 86, 21, "#b8d0c4")
    draw_text(cv, F5, 6, 24, "COLONY HEALTH", "#0a4842")
    draw_text(cv, F3, 6, 33, "VACCINES & TONICS", "#4a6a60")
    frame(cv, "#0a3a34", None, None)
    return cv


def b_transit():
    cv = Canvas(BW, BH, "#0a1220")
    paper = ramp("#a89a7a", "#cfc3a2", "#e8dfc4", "#f6f0dc")
    tk = m_rect(2, 2, BW - 2, BH - 2)
    for nx in (116,):
        tk -= m_ellipse(nx + 0.5, 2, 3, 3)
        tk -= m_ellipse(nx + 0.5, BH - 2, 3, 3)
    for cx, cy in ((2, 2), (BW - 2, 2), (2, BH - 2), (BW - 2, BH - 2)):
        tk -= m_ellipse(cx, cy, 2, 2)
    paint(cv, tk, paper, f_lin(0, 0, 40, BH, 3.2, 2.4), "#5a4e36", 4)
    # Guilloche wave on the main part.
    for x in range(4, 114):
        for k in range(3):
            y = int(20 + math.sin(x * 0.22 + k * 2.1) * 5 + k * 0)
            if (x, y) in tk:
                cv.set(x, y, "#dccfae")
    # Orange header band.
    cv.rect(3, 3, 115, 12, "#d8601a")
    cv.hline(3, 114, 12, "#8a3a0a")
    draw_text(cv, F5, 6, 4, "COLONIAL TRANSIT", WHITE, shadow="#8a3a0a")
    # Route.
    draw_text(cv, F5, 6, 16, "GATEWAY", "#1e1a12")
    draw_text(cv, F5, 50, 16, ">", "#d8601a")
    draw_text(cv, F5, 58, 16, "NEROID", "#1e1a12")
    draw_text(cv, F3, 6, 25, "DEPARTS DAILY", "#5a4e36")
    draw_text(cv, F3, 6, 32, "HYPERSLEEP BERTHS", "#8a3a0a")
    # Printed shuttle in one ink.
    ink = Canvas(44, 15)
    shuttle(ink, 0, 0, flame=False)
    for y in range(15):
        for x in range(44):
            c = ink.get(x, y)
            if c[3]:
                lum = (c[0] + c[1] + c[2]) / 3
                cv.set(72 + x, 22 + y, "#d8601a" if lum > 120 else "#8a3a0a")
    # Perforation and stub.
    for y in range(5, BH - 5, 2):
        cv.set(116, y, "#8a7e62")
    draw_text(cv, F3, 120, 5, "ADMIT ONE", "#5a4e36")
    draw_text(cv, F3, 120, 12, "BERTH", "#5a4e36")
    draw_text(cv, F5, 120, 18, "14C", "#1e1a12")
    bx = 120
    for i, w in enumerate((1, 2, 1, 1, 3, 1, 2, 1, 1, 2, 1, 1, 2, 1, 1, 2, 3, 1, 1, 2, 1, 1, 2)):
        if i % 2 == 0:
            cv.rect(bx, 27, bx + w, 35, "#1e1a12")
        if bx > 152:
            break
        bx += w
    draw_text(cv, F3, 141, 20, "$340", "#d8601a")
    return cv


def b_atmo():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, 32, ["#140a26", "#2a1238", "#4e1a3e", "#82283a", "#b8442e", "#e07a34", "#f4b45a"])
    sun = m_ellipse(12, 27, 7, 7)
    paint(cv, sun, ramp("#f8c070", "#ffe0a0", "#fff4d0"), lambda x, y: 2 - math.hypot(x - 12, y - 27) / 4, None, 4)
    # Far ridge.
    for x in range(BW):
        h = int(26 + 2.5 * math.sin(x * 0.07) + 1.5 * math.sin(x * 0.21 + 1))
        cv.vline(x, h, BH, "#5a2440" if x < 90 else "#4a1e3a")
        cv.set(x, h, "#8a3a4a")
    rng = random.Random(11)
    cloud_plume(cv, 44, 2.5, rng)
    processor_tower(cv, 16, 5, 34)
    # Colony huts.
    for hx, hw, hh in ((74, 8, 4), (84, 6, 3), (92, 9, 5)):
        hut = m_rect(hx, 33 - hh, hx + hw, 33)
        cv.fill(hut, "#1a1220")
        for x, y in edge(hut):
            if (x - 1, y) not in hut:
                cv.set(x, y, "#f0904a")
        cv.set(hx + 2, 33 - hh + 2, "#ffc860")
    for x in range(BW):
        h = int(33 + math.sin(x * 0.13) * 1.2)
        cv.vline(x, h, BH, "#120a14")
        cv.set(x, h, "#2a1620")
    # Dark text panel on the right.
    for y in range(BH):
        for x in range(94, BW):
            k = (x - 94) / 8
            if k > 1 or threshold(x, y, 4) < k:
                cv.set(x, y, mix(cv.get(x, y), "#0c0612", 0.78))
    wy_logo(cv, 98, 4, "#f4b45a", small=True)
    draw_text(cv, F3, 109, 4, "TERRAFORMING", "#f4b45a")
    draw_text(cv, BOLD, 98, 11, "M-CLASS", bands((3, "#fff4d0"), (3, "#ffd27a"), (3, "#f0904a")),
              outline="#0c0612", shadow="#5a2440")
    draw_text(cv, F5, 98, 23, "ATMOSPHERE", "#f0e0d0")
    draw_text(cv, F3, 98, 32, "PROCESSORS", "#f0904a")
    frame(cv, "#080410", None, None)
    return cv


def b_souta():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#ff9ab8", "#f47aa0", "#e05a88"])
    halftone(cv, 0, 0, BW, BH, rgba("#ffc8d8"), lambda x, y: max(0, 1 - math.hypot(x - 150, y) / 70))
    halftone(cv, 0, 0, BW, BH, rgba("#c23a6a"), lambda x, y: max(0, (y - 26) / 14) * (1 if x < 120 else 0), 4)
    # White sweep behind the pack.
    for y in range(BH):
        for x in range(4, 36):
            if abs((x - 20) - (y - 20) * 0.4) < 12 + (1 if threshold(x, y, 4) > 0.5 else 0):
                cv.set(x, y, mix(cv.get(x, y), WHITE, 0.55))
    ciggie_pack(cv, 9, 3)
    draw_text(cv, BOLD, 40, 3, "SOUTA", bands((4, WHITE), (5, "#ffe6ee")), outline="#86163e", shadow="#4a0a24",
              scale=2, italic=4, shadow_off=(2, 2))
    draw_text(cv, F5, 117, 6, "PINKS", "#4a0a24")
    draw_text(cv, F3, 117, 15, "KING SIZE", "#86163e")
    draw_text(cv, F5, 40, 25, "SMOOTH AS A", "#4a0a24")
    draw_text(cv, F5, 40, 32, "COLONY SUNSET", WHITE, shadow="#86163e")
    sticker(cv, 142, 29, 10, 7, 12, ramp("#ff9ab8", "#ffd6e4", "#fff4f8", "#ffffff"), "#86163e",
            [(F3, "20S", "#86163e", -2)], shadow="#86163e")
    frame(cv, "#4a0a24", "#ffd6e4", None)
    return cv


def b_power_loader():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#3a3c40", "#2c2e32", "#1e2024"])
    # Corrugated wall.
    for x in range(0, BW, 4):
        cv.vline(x, 4, BH - 5, "#34363a")
        cv.vline(x + 1, 4, BH - 5, "#26282c")
    for band_y in (0, BH - 4):
        for y in range(band_y, band_y + 4):
            for x in range(BW):
                cv.set(x, y, "#f0c41e" if (x - y) % 10 < 5 else "#141210")
    cv.hline(0, BW, 4, "#000000")
    cv.hline(0, BW, BH - 5, "#000000")
    # Floor shadow under the loader.
    cv.fill(m_ellipse(22, 34.5, 18, 1.4), "#141518")
    power_loader(cv, 4, 6)
    draw_text(cv, F3, 50, 7, "WEYLAND-YUTANI HEAVY LIFT", "#c8c8c0")
    draw_text(cv, BOLD, 50, 13, "P-5000", bands((3, "#fff6c0"), (3, "#ffe468"), (3, "#f0c41e")), outline="#000000",
              shadow="#7a5a08", scale=1)
    draw_text(cv, F5, 93, 13, "POWER", WHITE, shadow="#000000")
    draw_text(cv, F5, 93, 21, "LOADER", WHITE, shadow="#000000")
    draw_text(cv, F3, 50, 29, "LIFTS 4000 KG - CLASS 2", "#a8a8a0")
    plate = m_rrect(131, 12, 157, 27, 2)
    paint(cv, plate, YELLOW, f_lin(131, 12, 143, 27, 4.4, 2.6), "#1a1204")
    draw_text_c(cv, F3, 144, 14, "LEASE", "#1a1204")
    draw_text_c(cv, F3, 144, 20, "OR BUY", "#1a1204")
    return cv


def b_apollo():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#f0e6cc", "#e4d8ba", "#d6c8a6"])
    # Seventies sweep stripes.
    for i, c in enumerate(("#e8a040", "#d8742a", "#b04a1e", "#2f6db0")):
        for x in range(BW):
            y = int(31 + i * 2 - 9 * math.exp(-((x - 128) / 22) ** 2))
            cv.vline(x, y, y + 1, c)
    crt_terminal(cv, 112, 3, w=40)
    draw_text(cv, F5, 8, 4, "SEEGSON", "#2f6db0")
    seeg_logo(cv, 57, 7.5, 5, "#2f6db0", "#1c4a80", WHITE)
    draw_text(cv, BOLD, 7, 12, "APOLLO", bands((3, "#3a2a1a"), (6, "#5a3a20")), shadow="#d8742a", scale=1)
    draw_text(cv, F5, 52, 14, "CORE", "#b04a1e")
    draw_text(cv, F3, 8, 25, "STATION MANAGEMENT AI", "#6a5a40")
    sticker(cv, 94, 13, 11, 8, 12, ramp("#1c4a80", "#2f6db0", "#4a8cbd", "#8ac0e8", "#d0ecff"), "#0c2848",
            [(F3, "NEW", WHITE, -2)], shadow="#8f826a")
    return cv


def b_prefab():
    cv = Canvas(BW, BH, "#123a74")
    for x in range(0, BW, 5):
        cv.vline(x, 0, BH, "#17447f" if x % 20 else "#1f5294")
    for y in range(0, BH, 5):
        cv.hline(0, BW, y, "#17447f" if y % 20 else "#1f5294")
    line, faint = rgba("#eaf4ff"), rgba("#8cb8ea")
    hab_blueprint(cv, 6, 1, line, faint)
    frame(cv, line, None, None, 2, 2, BW - 3, BH - 3)
    cv.fill(edge(m_rect(74, 2, BW - 2, BH - 2)), line)
    wy_logo(cv, 78, 6, line, small=True)
    draw_text(cv, F3, 89, 6, "WEYLAND-YUTANI", faint)
    draw_text(cv, BOLD, 78, 13, "PREFAB", line, shadow="#0a2450")
    draw_text(cv, F5, 122, 15, "HABS", faint)
    cv.hline(78, 154, 24, faint)
    draw_text(cv, F3, 78, 27, "MOVE IN BY MONDAY.", line)
    draw_text(cv, F3, 78, 32, "SHIPS FLAT-PACKED", faint)
    return cv


def b_coffee():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#3a1d0e", "#2a140a", "#1e0f08"])
    radial(cv, 22, 22, 24, ["#8a4a22", "#6a3618", "#4a2410", "#2a140a"], 0, 0, 50, BH)
    for y in range(BH):
        for x in range(0, 50):
            if math.hypot(x - 22, y - 22) > 23:
                cv.set(x, y, pick(ramp("#3a1d0e", "#2a140a", "#1e0f08"), y / (BH - 1) * 2, x, y, 4))
    halftone(cv, 46, 0, BW, BH, rgba("#4a2410"), lambda x, y: 0.5 if (x // 4 + y // 4) % 3 else 0.2)
    coffee_mug(cv, 10, 6)
    # Beans.
    for bx, by in ((36, 31), (40, 34), (33, 35)):
        b = m_ellipse(bx, by, 2.5, 1.8)
        paint(cv, b, BROWN, f_sphere(bx - 1, by - 1, 3, len(BROWN)), BROWN[0])
        cv.set(int(bx) - 1, int(by), BROWN[0])
        cv.set(int(bx), int(by), BROWN[0])
    draw_text(cv, BOLD, 50, 4, "RED DUST", bands((3, "#fff0d8"), (3, "#ffc888"), (3, "#e8823a")),
              outline="#140804", shadow="#8e1a14", italic=5)
    draw_text(cv, F5, 50, 17, "COFFEE", "#e8823a", gap=2)
    draw_text(cv, F3, 50, 27, "KEEPS THE NIGHT", "#d8b898")
    draw_text(cv, F3, 50, 33, "SHIFT HONEST", "#d8b898")
    price_tag(cv, 117, 19, 38, 16, ramp("#8a7a5a", "#c8b890", "#e8dcb8", "#f8f0dc", "#ffffff"), "#3a2a14",
              [(F3, "PER TIN", "#5a3a20", 8, 2), (F5, "$3.50", "#8e1a14", 9, 8)], string=(126, 3))
    frame(cv, "#0e0604", "#5e3016", None)
    return cv


# -- Second wave: palettes, helpers and sprites -----------------------------------------------------------
CASE = ramp("#16181c", "#2a2d33", "#40444b", "#585d65", "#767c85", "#9aa0a8")
AMBER = ramp("#2a1200", "#5a2a00", "#a85200", "#f08a10", "#ffc060", "#fff0c8")
NAVY = ramp("#050a18", "#0c1a34", "#16305a", "#24487e", "#3a68a8", "#6a98d0")
OLIVE = ramp("#1a1e0e", "#2e3418", "#454c24", "#5e6632", "#7c8546", "#a2aa66")
LEATHER = ramp("#1e0e06", "#3a1c0c", "#5a2e14", "#7e4420", "#a4602e", "#c88448")
CAN_BLUE = ramp("#06183a", "#0c2c5a", "#1c4a80", "#3a7ac0", "#7ab4ec", "#cfe6ff")
PURPLE = ramp("#1a0626", "#360c4a", "#5a1676", "#8a2aa6", "#c05ad8", "#eaa6ff")
RUST = ramp("#2a0e04", "#4e1c08", "#7a2e0e", "#a84618", "#d06a2a", "#f09a56")
HULL = ramp("#14161a", "#262a30", "#3c424a", "#58606a", "#7e8892", "#aab4bc")
MILK = ramp("#8a9aa8", "#b8c6d0", "#dde6ec", "#f4f8fa", "#ffffff")
LEAF = ramp("#0a2a0a", "#145a14", "#228a1e", "#44b830", "#8ae05a", "#d0ff9a")
CREAM = ramp("#8a7a5a", "#c0b08a", "#e4d8b8", "#f8f0dc")


def glow(cv, cx, cy, rx, ry, colour, strength=0.5, x0=0, y0=0, x1=None, y1=None):
    """Soft light mixed into what is already drawn, in three dithered steps, strongest at the centre."""
    x1 = cv.w if x1 is None else x1
    y1 = cv.h if y1 is None else y1
    for y in range(max(0, y0), min(cv.h, y1)):
        for x in range(max(0, x0), min(cv.w, x1)):
            d = math.hypot((x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry)
            if d >= 1:
                continue
            v = (1 - d) * 3
            i = int(v)
            if v - i > threshold(x, y, 4):
                i += 1
            if i:
                cv.set(x, y, mix(cv.get(x, y), colour, strength * i / 3))


def noise_tex(cv, x0, y0, x1, y1, rmp, seed, cell=6, lo=0.0, hi=None):
    """Blotchy value noise texture: a coarse random lattice, bilinearly smoothed, plus a little grain."""
    hi = len(rmp) - 1 if hi is None else hi
    rng = random.Random(seed)
    gw, gh = (x1 - x0) // cell + 2, (y1 - y0) // cell + 2
    grid = [[rng.random() for _ in range(gw)] for _ in range(gh)]
    for y in range(y0, y1):
        for x in range(x0, x1):
            gx, gy = (x - x0) / cell, (y - y0) / cell
            ix, iy = int(gx), int(gy)
            fx, fy = gx - ix, gy - iy
            fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
            a = grid[iy][ix] + (grid[iy][ix + 1] - grid[iy][ix]) * fx
            b = grid[iy + 1][ix] + (grid[iy + 1][ix + 1] - grid[iy + 1][ix]) * fx
            v = a + (b - a) * fy + (rng.random() - 0.5) * 0.25
            cv.set(x, y, pick(rmp, lo + (hi - lo) * v, x, y, 4))


def text_fill(cv, font, x, y, text, fn, scale=1, gap=1, italic=0, outline=None, depth=0, depth_c=None):
    """Text whose every pixel is coloured by fn(x, y) (absolute coordinates), with an optional block extrusion
    of depth pixels down and to the right and a one pixel outline."""
    mask = shift(text_mask(font, text, gap, scale, italic), x, y)
    body = mask | (halo(mask) if outline is not None else set())
    for k in range(depth, 0, -1):
        cv.fill(shift(body, k, k), depth_c)
    if outline is not None:
        cv.fill(halo(mask), outline)
    for px, py in mask:
        cv.set(px, py, fn(px, py))
    return x + text_width(font, text, gap, scale, italic)


def check_box(cv, x, y, box, tick):
    """5x5 tick box."""
    cv.fill(edge(m_rect(x, y, x + 5, y + 5)), box)
    for px, py in ((x + 1, y + 2), (x + 2, y + 3), (x + 3, y + 2), (x + 4, y + 1), (x + 5, y)):
        cv.set(px, py, tick)


def comterm(cv, ox, oy, lines=("READY", ">"), cursor=True, phase=0):
    """Seegson ComTerm wall terminal: amber screen, side keypad and a wall bracket, 44x31."""
    cv.fill(m_rect(ox + 12, oy - 3, ox + 32, oy + 1), CASE[1])
    cv.hline(ox + 12, ox + 31, oy - 3, CASE[3])
    for bx in (ox + 14, ox + 29):
        cv.set(bx, oy - 2, CASE[4])
    case = m_rrect(ox, oy, ox + 44, oy + 31, 3)
    paint(cv, case, CASE, f_lin(ox, oy, ox + 22, oy + 31, 4.4, 2.0), CASE[0], soft=CASE[2])
    cv.hline(ox + 3, ox + 40, oy + 1, CASE[5])
    cv.fill(m_rrect(ox + 3, oy + 3, ox + 30, oy + 24, 2), CASE[0])
    cv.hline(ox + 5, ox + 27, oy + 24, CASE[4])
    scr = m_rrect(ox + 5, oy + 5, ox + 28, oy + 22, 2)
    mid, half = ox + 16.5, 11.5
    for x, y in scr:
        d = math.hypot((x + 0.5 - mid) / half, (y + 0.5 - (oy + 13.5)) / 8.5)
        cv.set(x, y, pick(ramp("#120700", "#221000", "#3a1a02"), 2.2 - d * 2.2, x, y, 4))
    for i, t in enumerate(lines):
        draw_text(cv, F3, ox + 7, oy + 7 + i * 6, t, AMBER[4] if i == 0 else AMBER[3])
    if cursor:
        cx = ox + 7 + text_width(F3, lines[-1]) + 2
        cy = oy + 7 + (len(lines) - 1) * 6
        cv.rect(cx, cy, cx + 3, cy + 5, AMBER[4])
    scanlines(cv, ox + 5, oy + 5, ox + 28, oy + 22, 0.7, 2, phase)
    cv.set(ox + 7, oy + 6, AMBER[5])
    cv.set(ox + 8, oy + 6, AMBER[4])
    # Keypad.
    cv.fill(m_rrect(ox + 32, oy + 4, ox + 41, oy + 24, 1), CASE[1])
    for r in range(4):
        for c in range(3):
            kx, ky = ox + 33 + c * 3, oy + 5 + r * 5
            key = "#f08a10" if (r, c) == (3, 2) else "#c83a2a" if (r, c) == (3, 0) else None
            cv.rect(kx, ky, kx + 2, ky + 3, key or CASE[4])
            cv.hline(kx, kx + 1, ky, WHITE if key is None else AMBER[4])
            cv.hline(kx, kx + 1, ky + 2, CASE[2] if key is None else AMBER[1])
    # Name plate, status light and vents on the chin.
    draw_text(cv, F3, ox + 4, oy + 25, "SEEGSON", CASE[5], shadow=CASE[0], shadow_off=(0, 1))
    cv.set(ox + 33, oy + 26, "#ff3a2a")
    cv.set(ox + 33, oy + 27, "#8a1a10")
    for vx in range(ox + 36, ox + 41, 2):
        cv.vline(vx, oy + 25, oy + 28, CASE[1])


def freighter(cv, ox, oy):
    """Weyland-Yutani towing freighter flying left, about 86x22; the engines on the right glow."""
    for i, c in enumerate(("#1a4a8a", "#5aa0ff", "#d0ecff")):
        for ny in (oy + 8, oy + 12, oy + 16):
            cv.fill(m_ellipse(ox + 82 + 2 - i, ny + 0.5, 4.5 - i * 1.3, 1.6 - i * 0.4), c)
    hull = m_poly([(ox, oy + 13), (ox + 5, oy + 9), (ox + 18, oy + 7), (ox + 58, oy + 7), (ox + 63, oy + 5),
                   (ox + 78, oy + 5), (ox + 78, oy + 19), (ox + 56, oy + 19), (ox + 52, oy + 17), (ox + 18, oy + 17),
                   (ox + 6, oy + 16)])
    bridge = m_rect(ox + 12, oy + 3, ox + 24, oy + 8)
    spine = m_rect(ox + 28, oy + 5, ox + 52, oy + 8)
    pods = m_rect(ox + 24, oy + 16, ox + 34, oy + 20) | m_rect(ox + 38, oy + 16, ox + 48, oy + 20)
    for m in (spine, bridge, pods):
        paint(cv, m, HULL, f_hcyl(min(y for _, y in m), max(y for _, y in m) + 1, len(HULL), 0.2, 0.8), "#07080a",
              soft=HULL[3])
    paint(cv, hull, HULL, f_hcyl(oy + 5, oy + 19, len(HULL), 0.22, 0.5), "#07080a", soft=HULL[3])
    # Engine block, nozzles and panel lines.
    eng = m_rect(ox + 66, oy + 4, ox + 79, oy + 20)
    paint(cv, eng, HULL, f_cyl(ox + 66, ox + 79, len(HULL), 0.25, 0.5), "#07080a", soft=HULL[3])
    for ny in (oy + 7, oy + 11, oy + 15):
        cv.rect(ox + 79, ny, ox + 82, ny + 3, HULL[1])
        cv.vline(ox + 81, ny, ny + 2, "#9ad0ff")
    for px in range(ox + 22, ox + 64, 7):
        cv.vline(px, oy + 9, oy + 15, HULL[1])
    cv.hline(ox + 8, ox + 64, oy + 9, HULL[4])
    rng = random.Random(9)
    for px in range(ox + 10, ox + 62, 3):
        if rng.random() < 0.55:
            cv.set(px, oy + 12, "#ffd27a" if rng.random() < 0.7 else "#ff9a40")
    cv.hline(ox + 14, ox + 22, oy + 5, "#9adcf0")
    cv.hline(ox + 15, ox + 21, oy + 6, "#2a6a8a")
    # Hazard chevrons on the nose and masts on the spine.
    for k in range(3):
        cv.line(ox + 4 + k * 3, oy + 11, ox + 6 + k * 3, oy + 14, "#e8b020")
    for mx, h in ((ox + 32, 4), (ox + 44, 3)):
        cv.vline(mx, oy + 5 - h, oy + 4, HULL[3])
    cv.set(ox + 32, oy, "#ff3a2a")
    wy_logo(cv, ox + 67, oy + 8, HULL[4], small=True)


def ice_cube(cv, x, y, s=6):
    cube = m_rrect(x, y, x + s, y + s, 1)
    for px, py in cube:
        cv.set(px, py, mix(cv.get(px, py), "#e8f6ff", 0.55))
    for px, py in edge(cube):
        cv.set(px, py, "#ffffff" if px == x or py == y else "#8ac0e0")
    cv.set(x + 1, y + 1, WHITE)


def gateway_station(cv, cx, cy):
    """Gateway Station: a spindle through a turning habitat ring, about 38x30."""
    ring_o = m_ellipse(cx, cy, 18, 6.5)
    ring_i = m_ellipse(cx, cy, 13.5, 3.6)
    ring = ring_o - ring_i
    back = {(x, y) for x, y in ring if y + 0.5 < cy}
    front = ring - back
    paint(cv, back, SHELL, lambda x, y: 1.4, SHELL_OL)
    # Spokes behind the spindle.
    for a in (0.6, 2.5):
        cv.line(cx, cy, cx + math.cos(a + math.pi) * 15, cy + math.sin(a + math.pi) * 5, SHELL[1])
    spindle = m_rect(cx - 2, cy - 14, cx + 2, cy + 15)
    paint(cv, spindle, SHELL, f_cyl(cx - 2, cx + 2, len(SHELL), 0.3, 0.6), SHELL_OL)
    for my, h, w in ((cy - 11, 4, 4), (cy - 2, 4, 5), (cy + 9, 4, 4)):
        mod = m_rect(cx - w, my, cx + w, my + h)
        paint(cv, mod, SHELL, f_cyl(cx - w, cx + w, len(SHELL), 0.3, 0.8), SHELL_OL)
        cv.hline(cx - w + 1, cx + w - 2, my + 2, "#ffe6a0")
    cv.vline(cx, cy - 18, cy - 15, SHELL[3])
    cv.set(cx, cy - 18, "#ff3a2a")
    for a in (0.6, 2.5):
        cv.line(cx, cy, cx + math.cos(a) * 15, cy + math.sin(a) * 5, SHELL[2])
    paint(cv, front, SHELL, f_lin(cx - 18, cy, cx + 18, cy, 4.4, 1.6), SHELL_OL, soft=SHELL[2])
    for k in range(-14, 15, 3):
        x = cx + k
        ys = [y for (px, y) in front if px == x]
        if ys:
            cv.set(x, max(ys) - 1, "#ffe6a0")


# -- Second wave of banners ---------------------------------------------------------------------------


def b_comterm():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#24272c", "#1a1c20", "#121418", "#0a0b0e"])
    for x in range(0, BW, 20):
        cv.vline(x, 0, BH, "#0c0d10")
        cv.vline(x + 1, 0, BH, "#2c2f35")
        for y in (3, BH - 4):
            cv.set(x + 10, y, "#3e424a")
            cv.set(x + 11, y + 1, "#08090b")
    glow(cv, 132, 20, 36, 24, "#c06008", 0.28, 90)
    comterm(cv, 110, 5)
    cv.line(147, 36, 147, BH, "#08090b")
    cv.line(148, 36, 149, BH, "#2a2d33")
    seeg_logo(cv, 12, 10, 6, AMBER[3], "#1a0c02", AMBER[4])
    draw_text(cv, F5, 21, 4, "SEEGSON", AMBER[4], shadow="#2a1200")
    draw_text(cv, F3, 21, 12, "COMMUNICATIONS", "#a08a6a")
    draw_text(cv, BOLD, 6, 18, "COMTERM", bands((3, "#fff0c8"), (3, "#ffc060"), (3, "#f08a10")), outline="#0a0b0e",
              shadow="#5a2a00")
    pill = m_rrect(60, 18, 87, 27, 2)
    cv.fill(pill, "#1a0c02")
    cv.fill(edge(pill), AMBER[3])
    draw_text_c(cv, F5, 74, 19, "4100", AMBER[4])
    draw_text(cv, F3, 6, 31, "EVERY DECK. EVERY DOOR.", "#d8c8a8", shadow="#000000")
    frame(cv, "#050607", "#3a3e45", None)
    return cv


def b_freighter():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#02030a", "#050818", "#0a1026", "#101a36"])
    starfield(cv, random.Random(16), 1, 1, BW - 1, BH - 1, 70)
    giant = m_ellipse(142, -4, 30, 26)
    gas = ramp("#2a1004", "#5a2608", "#8a4214", "#c06a28", "#e89a4a", "#ffd090")
    sph = f_sphere(128, -14, 38, len(gas), 0.0, 1.2)
    for x, y in giant:
        v = sph(x, y) + 0.7 * math.sin(y * 0.9 + math.sin(x * 0.12) * 1.5)
        cv.set(x, y, pick(gas, v, x, y, 4))
    for x, y in halo(giant, False):
        if cv.inside(x, y):
            cv.set(x, y, "#5a2a14")
    freighter(cv, 70, 13)
    wy_logo(cv, 6, 4, "#c8d4f0", small=True)
    draw_text(cv, F3, 17, 4, "WEYLAND-YUTANI", "#c8d4f0")
    draw_text(cv, BOLD, 6, 11, "FREIGHT", bands((3, WHITE), (3, "#d8e4f8"), (3, "#9ab0d8")), outline="#02030a",
              shadow="#24487e")
    draw_text(cv, BOLD, 6, 22, "LINES", bands((3, "#ffe0a0"), (3, "#ffb04a"), (3, "#e0782a")), outline="#02030a",
              shadow="#5a2608")
    draw_text(cv, F3, 6, 33, "ORE. FUEL. CARGO.", "#8a9ac0")
    frame(cv, "#000000", "#1c2a4a", None)
    return cv


def b_synth_care():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#f4fbfc", "#e4f2f6", "#d0e6ec"])
    for x in range(0, BW, 4):
        for y in range(1, BH, 4):
            cv.set(x, y, "#c4dee6")
    disc = m_ellipse(19, 20, 15, 15)
    cv.fill(shift(disc, 1, 1), "#a8c8d2")
    paint(cv, disc, CYAN, f_sphere(15, 15, 20, len(CYAN), 0.35), CYAN[0], soft=CYAN[1])
    cv.fill(edge(m_ellipse(19, 20, 12.5, 12.5)), CYAN[3])
    drop = m_ellipse(19, 24, 6, 6) | m_poly([(13.4, 23), (19, 10.5), (24.6, 23)])
    paint(cv, drop, MILK, f_sphere(16.5, 19, 9, len(MILK), 0.2), "#1a5a6a")
    cv.vline(16, 21, 25, WHITE)
    cv.set(17, 19, WHITE)
    bitmap(cv, 38, 4, HYPER_MARK, {"#": CYAN[2]})
    draw_text(cv, F3, 47, 5, "HYPERDYNE", "#106a7a")
    draw_text(cv, BOLD, 38, 13, "CARE PLAN", bands((4, "#0e5a66"), (5, "#083e48")), shadow="#9ad0dc")
    draw_text(cv, F3, 38, 25, "SYNTHETIC SERVICE", "#4a7a84")
    x = draw_text(cv, F3, 38, 32, "FROM", "#4a7a84")
    x = draw_text(cv, F5, x + 3, 30, "$49", "#c0282e", shadow="#f0b8b8")
    draw_text(cv, F3, x + 3, 32, "A MONTH", "#4a7a84")
    panel = m_rrect(108, 3, 157, 37, 2)
    cv.fill(shift(panel, 1, 1), "#a8c8d2")
    cv.fill(panel, WHITE)
    cv.fill(edge(panel), CYAN[2])
    cv.rect(109, 4, 156, 11, CYAN[2])
    draw_text_c(cv, F3, 132, 5, "INCLUDES", WHITE)
    for i, t in enumerate(("DIAGNOSIS", "FLUIDS", "MEMORY", "REPAIRS")):
        check_box(cv, 111, 13 + i * 6, CYAN[2], "#c0282e")
        draw_text(cv, F3, 118, 13 + i * 6, t, "#0e5a66")
    frame(cv, CYAN[1], None, None)
    return cv


def b_water():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#e0f6fc", "#a8e0f2", "#62bce0", "#2c8ac0", "#165a90"])
    for k in range(4):
        for x in range(BW):
            y = int(8 + k * 8 + math.sin(x * 0.11 + k * 1.7) * 2 + math.sin(x * 0.29 + k) * 0.8)
            cv.set(x, y, mix(cv.get(x, y), WHITE, 0.35))
    rng = random.Random(18)
    for _ in range(14):
        bx, by, r = rng.randrange(104, 158), rng.randrange(12, 38), rng.choice((1, 1, 2))
        for x, y in edge(m_ellipse(bx + 0.5, by + 0.5, r + 0.6, r + 0.6)):
            cv.set(x, y, mix(cv.get(x, y), WHITE, 0.6))
    cross = m_rect(8, 3, 11, 10) | m_rect(6, 5, 13, 8)
    paint(cv, cross, ramp("#0a5a24", "#128a3a", "#18a048", "#5ad080"), f_lin(6, 3, 13, 10, 3, 1), "#064a1c")
    draw_text(cv, F3, 16, 4, "LASALLE BIONATIONAL", "#0a4842")
    draw_text(cv, BOLD, 6, 11, "PURA", bands((6, WHITE), (6, "#d8f6ff"), (6, "#8ee0f4")), outline="#0a3a5a",
              shadow="#165a90", scale=2, shadow_off=(2, 2))
    draw_text(cv, F5, 62, 13, "WATER", "#0a3a5a")
    draw_text(cv, F5, 62, 21, "FILTERS", "#0a3a5a")
    draw_text(cv, F3, 6, 32, "SAFE WATER FROM ANY TAP", WHITE, shadow="#0a3a5a")
    # The cartridge.
    shell = ramp("#5a7a88", "#98b8c6", "#d0e4ec", "#f2fafc", "#ffffff")
    cap = m_rrect(111, 3, 129, 9, 2)
    paint(cv, cap, TEAL, f_cyl(111, 129, len(TEAL), 0.3, 0.8), TEAL[0])
    cv.hline(113, 126, 4, TEAL[5])
    body = m_rect(112, 9, 128, 30)
    paint(cv, body, shell, f_cyl(112, 128, len(shell), 0.3, 0.6), "#3a5a68")
    for x in range(114, 127, 2):
        cv.vline(x, 11, 28, "#b0ccd8" if x < 121 else "#8aa8b6")
    cv.rect(113, 16, 127, 23, "#18a048")
    cv.hline(113, 126, 16, "#5ad080")
    cv.hline(113, 126, 22, "#0a5a24")
    bitmap(cv, 118, 17, ["..#..", "..#..", "#####", "..#..", "..#.."], {"#": WHITE})
    cv.vline(114, 10, 29, WHITE)
    bot = m_rrect(114, 30, 126, 33, 1)
    paint(cv, bot, TEAL, f_cyl(114, 126, len(TEAL), 0.3, 0.8), TEAL[0])
    cv.rect(119, 33, 122, 35, TEAL[1])
    for x, y in ((120, 37), (120, 38), (119, 38), (121, 38)):
        cv.set(x, y, "#d8f6ff")
    # A glass of clean water.
    cv.rect(137, 20, 151, 35, (122, 208, 240, 150))
    cv.hline(137, 150, 20, WHITE)
    for y in range(14, 37):
        cv.set(136, y, "#e8f8ff")
        cv.set(151, y, "#9ad0e8")
    cv.hline(136, 151, 36, "#e8f8ff")
    cv.hline(137, 150, 35, "#b8e4f4")
    cv.vline(138, 16, 33, (255, 255, 255, 170))
    for x, y in ((142, 30), (146, 26), (144, 23), (148, 31)):
        cv.set(x, y, WHITE)
    cv.fill(m_rrect(134, 3, 157, 11, 3), "#18a048")
    cv.fill(edge(m_rrect(134, 3, 157, 11, 3)), "#064a1c")
    draw_text_c(cv, F3, 146, 5, "99.9%", WHITE)
    frame(cv, "#0a3a5a", "#ffffff", None)
    return cv


def b_mining():
    cv = Canvas(BW, BH)
    noise_tex(cv, 0, 0, BW, BH, ramp("#0c0a0a", "#1a1614", "#2a2420", "#3a322c", "#4e443a"), 19, 5, 0.3, 3.2)
    rng = random.Random(19)
    for k in range(4):
        x, y = rng.randrange(0, 40), rng.randrange(4, 36)
        for _ in range(60):
            x += 1
            y += rng.choice((-1, 0, 0, 1))
            if not cv.inside(x, y):
                break
            cv.set(x, y, "#8a6a1a" if rng.random() < 0.7 else "#c89a2a")
            if rng.random() < 0.05:
                blink(cv, x, y, "#ffd27a")
        x += 40
    # The headlamp beam across the copy.
    for y in range(BH):
        for x in range(36, BW):
            t = (x - 36) / (BW - 36)
            half = 3 + t * 30
            d = abs(y + 0.5 - 15.5) / half
            if d < 1:
                k = (1 - d) * (1 - t * 0.55)
                v = k * 3
                i = int(v)
                if v - i > threshold(x, y, 4):
                    i += 1
                if i:
                    cv.set(x, y, mix(cv.get(x, y), "#fff0c0", 0.2 * i))
    # A rock ledge with a pick leaning on it, and the hard hat in profile, lamp to the right.
    ledge = m_poly([(0, 29), (8, 27), (30, 27), (40, 30), (42, BH), (0, BH)])
    paint(cv, ledge, ramp("#1a1614", "#2a2420", "#3e362e", "#5a4e42"), f_lin(0, 27, 0, BH, 2.6, 0.4), "#0c0a0a",
          dither=4)
    cv.hline(8, 30, 27, "#6a5e50")
    cv.fill(m_capsule(3, 6, 12, 27, 1.1), BROWN[3])
    cv.line(4, 6, 12, 26, BROWN[5])
    head = m_poly([(0, 7), (4, 3), (9, 2), (7, 5), (3, 8)])
    paint(cv, head, METAL, lambda x, y: 4, METAL[0])
    for x, y in m_ellipse(19, 27.5, 14, 1.2):
        cv.set(x, y, "#0c0a0a")
    dome = {(x, y) for x, y in m_ellipse(18, 24, 12, 13) if y < 23}
    paint(cv, dome, YELLOW, f_sphere(14, 14, 14, len(YELLOW), 0.25, 0.9), "#2a1e02", soft=YELLOW[1])
    for x, y in dome:
        if abs(x + 0.5 - (17 + (y - 11) * 0.15)) < 1.2 and y < 22 and (x, y) not in edge(dome):
            cv.set(x, y, YELLOW[5] if x < 18 else YELLOW[3])
    brim = m_poly([(4, 22), (32, 22), (37, 24), (36, 26), (5, 26), (3, 24)])
    paint(cv, brim, YELLOW, f_lin(0, 22, 0, 26, 4.4, 1.4), "#2a1e02")
    cv.hline(5, 33, 22, YELLOW[5])
    draw_text(cv, F3, 8, 16, "W-Y", YELLOW[1])
    lamp = m_rrect(27, 12, 34, 19, 1)
    paint(cv, lamp, METAL, f_hcyl(12, 19, len(METAL), 0.3, 0.8), METAL[0])
    cv.vline(34, 13, 17, "#fff6d0")
    cv.vline(35, 14, 16, WHITE)
    cv.set(28, 13, METAL[5])
    blink(cv, 36, 15, "#fff0c0", big=True)
    wy_logo(cv, 44, 4, "#e2d9a0", small=True)
    draw_text(cv, F3, 55, 4, "EXTRACTION DIVISION", "#c8b890")
    draw_text(cv, BOLD, 44, 11, "MINERS WANTED", bands((3, "#fff6c0"), (3, "#ffe468"), (3, "#f0c41e")),
              outline="#0c0a0a", shadow="#5a3a08")
    draw_text(cv, F5, 44, 23, "5-YEAR CONTRACTS", "#ffd27a", shadow="#0c0a0a")
    draw_text(cv, F3, 44, 32, "ROOM, BOARD & HAZARD PAY", "#c8b8a0", shadow="#0c0a0a")
    ore = m_poly([(144, 30), (147, 24), (152, 23), (156, 27), (155, 33), (148, 35)])
    paint(cv, ore, GOLD, f_sphere(147, 25, 11, len(GOLD), 0.45), GOLD[0])
    cv.line(147, 25, 151, 31, GOLD[1])
    blink(cv, 150, 26, "#fff8d8", big=True)
    frame(cv, "#000000", "#3a322c", None)
    return cv


def b_gateway():
    cv = Canvas(BW, BH, "#f0e6cc")
    # Airmail border.
    for y in range(BH):
        for x in range(BW):
            if 3 <= x < BW - 3 and 3 <= y < BH - 3:
                continue
            k = (x + y) % 12
            cv.set(x, y, "#c02a2a" if k < 4 else "#1c4a9a" if 6 <= k < 10 else "#f0e6cc")
    vgrad(cv, 4, 4, BW - 4, BH - 4, ["#04081a", "#081230", "#10204a", "#1a3a70"])
    starfield(cv, random.Random(20), 4, 4, BW - 4, BH - 4, 50)
    earth = m_ellipse(100, 92, 130, 58)
    earth_r = ramp("#0a2a5a", "#164a8a", "#2a6ab0", "#4a94d0", "#8ac8f0")
    for x, y in earth:
        if 4 <= x < BW - 4 and 4 <= y < BH - 4:
            v = 4 - (y - 34) * 0.9 + math.sin(x * 0.3) * 0.4
            cv.set(x, y, pick(earth_r, v, x, y, 4))
    for x, y in halo(earth, False):
        if 4 <= x < BW - 4 and 4 <= y < BH - 4:
            cv.set(x, y, "#9ae6ff")
            cv.set(x, y - 1, mix(cv.get(x, y - 1), "#4aa0e0", 0.6))
    gateway_station(cv, 134, 18)
    draw_text(cv, F3, 8, 5, "GREETINGS FROM", "#ffd27a", shadow="#0a1430")
    sunset = ramp("#fff6c0", "#ffe468", "#ffb030", "#ec7418", "#c04a0c")
    text_fill(cv, BOLD, 7, 12, "GATEWAY", lambda x, y: pick(sunset, (y - 12) / 17 * 4.4, x, y, 4), scale=2,
              outline="#ffffff", depth=2, depth_c="#0a1430")
    for x in range(7, 104):
        if cv.get(x, 14)[:3] == rgba(sunset[0])[:3] or cv.get(x, 14)[:3] == rgba(sunset[1])[:3]:
            cv.set(x, 14, WHITE)
    draw_text(cv, F5, 9, 30, "STATION", WHITE, outline="#0a1430")
    draw_text(cv, F3, 55, 32, "EARTH ORBIT TOURS", WHITE, outline="#0a1430")
    return cv


def b_aspen_light():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, 30, ["#2a6ab0", "#5a9ad8", "#9acaf0", "#d8eeff"])
    far = ramp("#6a8ab8", "#8aa8d0", "#b8d0ec")
    for x in range(BW):
        h = int(17 + 5 * abs(math.sin(x * 0.045 + 0.6)) + 2 * math.sin(x * 0.17))
        for y in range(h, BH):
            cv.set(x, y, pick(far, 2 - (y - h) * 0.2, x, y, 4))
    # Snow peaks: each column belongs to the highest slope over it; faces turned left catch the sun.
    peaks = [(22, 13, 0.75), (66, 8, 0.8), (92, 18, 0.9), (124, 12, 0.7), (156, 19, 0.8)]
    for x in range(BW):
        best = None
        for px, py, s in peaks:
            y = py + abs(x + 0.5 - px) * s
            if best is None or y < best[0]:
                best = (y, px, py)
        top, px, py = best
        for y in range(int(top), BH):
            lit = x + 0.5 < px
            cv.set(x, y, WHITE if lit else pick(ramp("#8aaad4", "#a8c4e4"), (y - top) * 0.25, x, y, 2))
        if x + 0.5 >= px:
            cv.set(x, int(top), "#9ab8dc")
    # Shadow creases down the shaded faces.
    for px, py, s in peaks:
        for k in range(3, 14):
            cv.set(int(px + k * 0.35 + 1), int(py + k), "#9ab8dc")
    for x in range(BW):
        y = int(31 + math.sin(x * 0.08) * 1.2)
        cv.vline(x, y, BH, "#f4f8ff")
        cv.set(x, y, "#c8dcf0")
    draw_text(cv, F5, 8, 3, "ASPEN", WHITE, outline="#0c2c5a")
    draw_text(cv, BOLD, 6, 12, "LIGHT", bands((5, WHITE), (6, "#e0f0ff"), (7, "#9acaf0")), outline="#0c2c5a",
              shadow="#0c2c5a", scale=2, italic=3, shadow_off=(2, 2))
    draw_text(cv, F3, 8, 33, "LESS CAL. SAME COLD.", "#0c2c5a")
    beer_can(cv, 103, 7, CAN_BLUE, METAL)
    drift = m_ellipse(110, 35.5, 16, 4)
    paint(cv, drift, ramp("#a8c4e4", "#d8e8f8", "#ffffff"), lambda x, y: 2.4 - (y - 32) * 0.3, None, 4)
    for x, y in ((99, 33), (119, 34), (104, 36)):
        ice_cube(cv, x, y, 5)
    for x, y in ((99, 8), (121, 14), (96, 22)):
        blink(cv, x, y, "#cfe6ff")
    sticker(cv, 141, 19, 14, 11, 20, ramp("#0c2c5a", "#1c4a80", "#3a7ac0", "#7ab4ec", "#cfe6ff"), "#06183a",
            [(F3, "ONLY", "#cfe6ff", -10), (F5, "95", WHITE, -4), (F3, "CAL", "#cfe6ff", 4)], shadow="#5a7aa0")
    frame(cv, "#0c2c5a", None, None)
    return cv


def b_colony_tv():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#120a24", "#0c0a1c", "#080612"])
    for x in range(42, BW, 3):
        cv.vline(x, 0, BH, "#140e28")
    # Rabbit ears, cabinet, screen, knobs and legs.
    cv.line(18, 4, 12, 0, METAL[4])
    cv.line(24, 4, 31, 0, METAL[4])
    cv.fill(m_ellipse(21, 4.5, 3, 1.5), METAL[2])
    for lx in (8, 34):
        cv.rect(lx, 34, lx + 2, 38, BROWN[1])
    cab = m_rrect(4, 5, 41, 35, 3)
    paint(cv, cab, BROWN, f_lin(4, 5, 30, 35, 4.6, 2.2), BROWN[0], soft=BROWN[2])
    for y in range(8, 33, 3):
        cv.hline(6, 38, y, mix(BROWN[3], BROWN[4], 0.3))
    bez = m_rrect(6, 7, 32, 32, 4)
    cv.fill(bez, "#14100c")
    scr = m_rrect(8, 9, 30, 30, 4)
    bars = ["#e8e8e8", "#e8d020", "#20d0d8", "#20c040", "#d030c0", "#d03020", "#2040d0"]
    for x, y in scr:
        cv.set(x, y, bars[min(6, (x - 8) * 7 // 22)] if y < 24 else ("#2040d0" if x < 19 else "#101010"))
    disc = m_ellipse(19, 16.5, 6, 6)
    cv.fill(disc, "#101018")
    cv.fill(edge(disc), WHITE)
    draw_text_c(cv, F5, 19, 13, "7", WHITE)
    scanlines(cv, 8, 9, 30, 30, 0.72)
    for x, y in scr:
        d = math.hypot((x - 19) / 11, (y - 19.5) / 10.5)
        if d > 0.85:
            cv.tint({(x, y)}, 0.7)
    cv.set(10, 11, WHITE)
    cv.set(11, 10, WHITE)
    cv.rect(33, 8, 39, 32, BROWN[1])
    for ky in (12, 19):
        paint(cv, m_ellipse(36, ky, 2.5, 2.5), METAL, f_sphere(35, ky - 1, 3, len(METAL)), METAL[0])
    for y in range(25, 31, 2):
        cv.hline(34, 38, y, BROWN[0])
    draw_text(cv, BOLD, 46, 4, "FRONTIER", bands((3, WHITE), (3, "#ffd6e4"), (3, "#ff9ab8")), outline="#080612",
              shadow="#86163e")
    seal = m_ellipse(109, 8.5, 6.5, 6.5)
    paint(cv, seal, CAN_RED, f_sphere(107, 6, 8, len(CAN_RED), 0.3), CAN_RED[0])
    draw_text_c(cv, F5, 109, 5, "7", WHITE, shadow=CAN_RED[0])
    draw_text(cv, F3, 119, 4, "COLONY", "#8ae0ff")
    draw_text(cv, F3, 119, 10, "NETWORK", "#8ae0ff")
    for i, c in enumerate(("#ffb030", "#ec5e8a", "#26c8d6")):
        cv.hline(46, BW - 3, 16 + i, c)
    for i, (t, s) in enumerate((("19:00", "COLONY NEWS"), ("20:00", "SPACE TRUCKERS"), ("21:00", "GATEWAY GAMES"))):
        draw_text(cv, F3, 46, 21 + i * 6, t, "#ffb030")
        draw_text(cv, F3, 66, 21 + i * 6, s, WHITE)
    box = m_rrect(126, 21, 157, 37, 2)
    glow(cv, 141.5, 29, 22, 14, "#ff3a2a", 0.3, 112)
    paint(cv, box, CAN_RED, f_lin(126, 21, 126, 37, 4.2, 2.2), "#2a0404")
    draw_text_c(cv, F3, 142, 23, "ON AIR", "#ffd6c8")
    draw_text_c(cv, F5, 142, 29, "LIVE", WHITE, shadow=CAN_RED[0])
    frame(cv, "#000000", "#2a1a4a", None)
    return cv


def bunk_bed(cv, ox, oy):
    """Fold-down double bunk with a ladder, 24x18."""
    for px in (ox, ox + 22):
        cv.fill(m_rect(px, oy, px + 2, oy + 18), METAL[3])
        cv.vline(px, oy, oy + 17, METAL[5])
        cv.vline(px + 1, oy, oy + 17, METAL[2])
    for my in (oy + 2, oy + 10):
        mat = m_rrect(ox + 2, my, ox + 22, my + 5, 1)
        paint(cv, mat, NAVY, f_hcyl(my, my + 5, len(NAVY), 0.3, 1.5), NAVY[0])
        cv.fill(m_rrect(ox + 3, my - 1, ox + 9, my + 2, 1), MILK[3])
        cv.hline(ox + 3, ox + 8, my + 2, MILK[1])
        cv.hline(ox + 2, ox + 21, my + 5, METAL[1])
        for x in range(ox + 11, ox + 21, 3):
            cv.set(x, my + 2, NAVY[5])
    cv.vline(ox + 17, oy + 6, oy + 17, METAL[4])
    cv.vline(ox + 20, oy + 6, oy + 17, METAL[4])
    for ry in (oy + 8, oy + 16):
        cv.hline(ox + 17, ox + 20, ry, METAL[4])


def egg_chair(cv, ox, oy):
    """Moulded seventies egg chair on a pedestal, 22x18."""
    cv.fill(m_ellipse(ox + 11, oy + 17.5, 5.5, 1.2), METAL[1])
    cv.fill(m_rect(ox + 10, oy + 14, ox + 13, oy + 18), METAL[3])
    cv.vline(ox + 10, oy + 14, oy + 17, METAL[5])
    shell = m_ellipse(ox + 11, oy + 8, 9.5, 8)
    paint(cv, shell, ORANGE, f_sphere(ox + 7, oy + 4, 13, len(ORANGE), 0.3), ORANGE[0], soft=ORANGE[2])
    hollow = m_ellipse(ox + 12, oy + 8.5, 6.5, 5.5)
    paint(cv, hollow & shell - edge(shell), ORANGE, f_lin(ox + 6, oy, ox + 18, oy, 0.8, 2.0), None)
    back = m_ellipse(ox + 12.5, oy + 6.5, 4, 3.5)
    paint(cv, back, CREAM, f_sphere(ox + 11, oy + 5, 5, len(CREAM), 0.3), CREAM[0])
    cush = m_rrect(ox + 6, oy + 9, ox + 19, oy + 14, 2)
    paint(cv, cush, CREAM, f_hcyl(oy + 9, oy + 14, len(CREAM), 0.3, 1.0), CREAM[0])


def floor_lamp(cv, ox, oy, on=True):
    """Arc floor lamp with a dome shade, 24x18."""
    if on:
        cone = m_poly([(ox + 13, oy + 8), (ox + 21, oy + 8), (ox + 24, oy + 18), (ox + 10, oy + 18)])
        for x, y in cone:
            if (x + y) % 2 == 0:
                cv.set(x, y, (255, 214, 120, 120))
        cv.fill(m_ellipse(ox + 17, oy + 17.6, 6, 1), (255, 214, 120, 150))
    cv.fill(m_ellipse(ox + 4, oy + 17, 4, 1.3), METAL[1])
    cv.hline(ox + 1, ox + 6, oy + 16, METAL[3])
    for k in range(25):
        t = k / 24
        x = (1 - t) ** 2 * (ox + 4) + 2 * (1 - t) * t * (ox + 4) + t * t * (ox + 17)
        y = (1 - t) ** 2 * (oy + 16) + 2 * (1 - t) * t * (oy - 2) + t * t * (oy + 2)
        cv.set(int(x), int(y), METAL[4])
        cv.set(int(x) + 1, int(y), METAL[2])
    shade = {(x, y) for x, y in m_ellipse(ox + 17, oy + 8, 5.5, 5.5) if y < oy + 8}
    paint(cv, shade, TEAL, f_sphere(ox + 15, oy + 4, 7, len(TEAL), 0.3), TEAL[0], soft=TEAL[2])
    cv.hline(ox + 12, ox + 21, oy + 8, "#fff2c0" if on else TEAL[1])


def flight_jacket(cv, ox, oy):
    """Synthleather flight jacket, front view, 34x32."""
    rib = ramp("#120804", "#24120a", "#3a1e10")
    for sx, sgn in ((ox + 5, -1), (ox + 29, 1)):
        arm = m_poly([(sx, oy + 4), (sx + sgn * 5, oy + 7), (sx + sgn * 6, oy + 28), (sx + sgn * 1, oy + 28),
                      (sx, oy + 12)])
        paint(cv, arm, LEATHER, f_cyl(min(sx, sx + sgn * 6), max(sx, sx + sgn * 6), len(LEATHER), 0.35, 0.4),
              LEATHER[0])
        cuff = {(x, y) for x, y in arm if y >= oy + 25}
        paint(cv, cuff, rib, lambda x, y: 1 + (x % 2), LEATHER[0])
    body = m_poly([(ox + 5, oy + 4), (ox + 12, oy + 2), (ox + 22, oy + 2), (ox + 29, oy + 4), (ox + 29, oy + 29),
                   (ox + 5, oy + 29)])
    paint(cv, body, LEATHER, f_cyl(ox + 5, ox + 29, len(LEATHER), 0.3, 0.6), LEATHER[0], soft=LEATHER[2])
    band = {(x, y) for x, y in body if y >= oy + 26}
    paint(cv, band, rib, lambda x, y: 1 + (x % 2), LEATHER[0])
    # Zip, pockets, collar, patches.
    cv.vline(ox + 17, oy + 9, oy + 28, METAL[4])
    cv.vline(ox + 16, oy + 9, oy + 28, LEATHER[1])
    for px in (ox + 8, ox + 20):
        cv.hline(px, px + 5, oy + 20, LEATHER[1])
        cv.hline(px, px + 5, oy + 21, LEATHER[4])
    fur = ramp("#8a7a5a", "#c8b890", "#e8dcb8", "#fff6e0")
    for sgn in (-1, 1):
        col = m_poly([(ox + 17, oy + 11), (ox + 17 + sgn * 9, oy + 1), (ox + 17 + sgn * 12, oy + 3),
                      (ox + 17 + sgn * 9, oy + 8), (ox + 17 + sgn * 2, oy + 13)])
        paint(cv, col, fur, f_lin(ox + 8, oy, ox + 20, oy + 12, 3.4, 1.4), "#5a4a30", dither=4)
    cv.fill(m_poly([(ox + 13, oy + 2), (ox + 21, oy + 2), (ox + 17, oy + 9)]), "#2a1a10")
    cv.fill(m_ellipse(ox + 24, oy + 15, 2.6, 2.6), "#1c3a6a")
    cv.set(ox + 24, oy + 14, "#ffd040")
    cv.rect(ox + 8, oy + 13, ox + 14, oy + 16, "#e8dcb8")
    cv.hline(ox + 9, ox + 12, oy + 14, "#5a4a30")


def squadron_patch(cv, cx, cy, r=10):
    disc = m_ellipse(cx, cy, r, r)
    cv.fill(shift(disc, 1, 1), (0, 0, 0, 120))
    paint(cv, disc, GOLD, f_sphere(cx - 3, cy - 3, r * 1.4, len(GOLD), 0.4), GOLD[0])
    paint(cv, m_ellipse(cx, cy, r - 2, r - 2), NAVY, f_sphere(cx - 2, cy - 3, r * 1.2, len(NAVY), 0.3), None)
    wing = ["..........##", "......######", "...#########", ".##########.", "######......"]
    for sgn in (-1, 1):
        for wy, row in enumerate(wing):
            for wx, ch in enumerate(row):
                if ch == "#":
                    x = cx + sgn * (wx - 12) - (1 if sgn < 0 else 0)
                    cv.set(int(x) + (0 if sgn < 0 else 0), int(cy - 3 + wy), WHITE if wy < 3 else MILK[1])
    cv.fill(m_burst(cx, cy + 3, 3.2, 1.4, 5, -math.pi / 2), "#e83a2a")


def milk_carton(cv, ox, oy):
    """Gable-top carton of synthetic milk, 21x32."""
    front = m_rect(ox, oy + 9, ox + 14, oy + 31)
    side = m_poly([(ox + 14, oy + 9), (ox + 21, oy + 6), (ox + 21, oy + 28), (ox + 14, oy + 31)])
    gable = m_poly([(ox, oy + 9), (ox + 7, oy + 3), (ox + 14, oy + 9)])
    roof = m_poly([(ox + 7, oy + 3), (ox + 14, oy), (ox + 21, oy + 6), (ox + 14, oy + 9)])
    paint(cv, side, MILK, lambda x, y: 1.6, MILK[0])
    paint(cv, roof, MILK, lambda x, y: 3.6, MILK[0])
    paint(cv, front, MILK, f_lin(ox, oy + 9, ox + 14, oy + 31, 4.2, 2.6), MILK[0])
    paint(cv, gable, MILK, lambda x, y: 3.0, MILK[0])
    for x, y in edge(front | side | gable | roof):
        cv.set(x, y, "#3a5a7a")
    cv.line(ox + 7, oy + 2, ox + 14, oy - 1, "#3a5a7a")
    cv.line(ox + 7, oy + 3, ox + 14, oy, WHITE)
    cv.rect(ox + 1, oy + 16, ox + 14, oy + 24, "#1c4a9a")
    cv.hline(ox + 1, ox + 13, oy + 16, "#5a8ad8")
    for x, y in ((ox + 3, oy + 18), (ox + 10, oy + 22), (ox + 7, oy + 19), (ox + 12, oy + 18)):
        cv.set(x, y, WHITE)
    draw_text(cv, F3, ox + 3, oy + 10, "MW", "#1c4a9a")
    cv.fill(m_poly([(ox + 14, oy + 16), (ox + 21, oy + 13), (ox + 21, oy + 21), (ox + 14, oy + 24)]), "#12326a")
    cv.hline(ox + 2, ox + 12, oy + 27, "#8a9aa8")
    cv.hline(ox + 2, ox + 9, oy + 29, "#8a9aa8")


def seed_packet(cv, ox, oy, band, crop):
    """Seed packet, 14x20; crop is tomato, carrot or lettuce."""
    paper = m_rect(ox, oy, ox + 14, oy + 20)
    paint(cv, paper, CREAM, f_lin(ox, oy, ox + 14, oy + 20, 3.4, 2.2), "#4a3a20")
    for x in range(ox + 1, ox + 13, 2):
        cv.set(x, oy, "#4a3a20")
    cv.rect(ox + 1, oy + 2, ox + 13, oy + 6, band[2])
    cv.hline(ox + 1, ox + 12, oy + 2, band[4])
    cx, cy = ox + 7, oy + 12
    if crop == "tomato":
        paint(cv, m_ellipse(cx, cy, 4, 3.5), CAN_RED, f_sphere(cx - 1.5, cy - 1.5, 5, len(CAN_RED), 0.3), CAN_RED[0])
        bitmap(cv, cx - 2, cy - 4, ["#.#.#", ".###."], {"#": LEAF[3]})
    elif crop == "carrot":
        paint(cv, m_poly([(cx - 3, cy - 3), (cx + 3, cy - 3), (cx, cy + 5)]), ORANGE, lambda x, y: 3.4 - (x - cx) * 0.4,
              ORANGE[0])
        bitmap(cv, cx - 2, cy - 6, ["#.#.#", ".###.", "..#.."], {"#": LEAF[3]})
    else:
        paint(cv, m_ellipse(cx, cy, 4.5, 4), LEAF, f_sphere(cx - 1.5, cy - 1.5, 6, len(LEAF), 0.3), LEAF[0])
        cv.line(cx, cy + 3, cx - 2, cy - 2, LEAF[1])
        cv.line(cx, cy + 3, cx + 2, cy - 1, LEAF[1])
    cv.hline(ox + 3, ox + 10, oy + 17, "#8a7a5a")


def colonial_marine(cv, ox, oy):
    """A Colonial Marine in M3 armour from the chest up, pulse rifle across the chest, 44x38."""
    rim = rgba("#ffd27a")
    torso = m_poly([(ox + 6, oy + 38), (ox + 7, oy + 23), (ox + 14, oy + 18), (ox + 30, oy + 18), (ox + 37, oy + 23),
                    (ox + 38, oy + 38)])
    paint(cv, torso, OLIVE, f_cyl(ox + 6, ox + 38, len(OLIVE), 0.3, 0.4), OLIVE[0], soft=rim)
    for sx in (ox + 10, ox + 34):
        pad = m_ellipse(sx, oy + 23, 6, 4.5)
        paint(cv, pad, OLIVE, f_sphere(sx - 2, oy + 21, 7, len(OLIVE), 0.25), OLIVE[0], soft=rim)
    plate = m_rrect(ox + 15, oy + 21, ox + 30, oy + 34, 2)
    paint(cv, plate, OLIVE, f_lin(ox + 15, oy + 21, ox + 30, oy + 34, 4.6, 2.2), OLIVE[0])
    cv.rect(ox + 18, oy + 23, ox + 27, oy + 26, "#2a2e1a")
    # Neck, face and the M10 helmet.
    cv.fill(m_rect(ox + 19, oy + 14, ox + 26, oy + 19), FLESH[1])
    face = m_rrect(ox + 16, oy + 6, ox + 28, oy + 18, 3)
    paint(cv, face, FLESH, f_sphere(ox + 19, oy + 9, 11, len(FLESH), 0.3), FLESH[0])
    cv.hline(ox + 18, ox + 20, oy + 11, "#2a1a10")
    cv.hline(ox + 23, ox + 25, oy + 11, "#2a1a10")
    cv.hline(ox + 20, ox + 24, oy + 15, FLESH[0])
    helm = {(x, y) for x, y in m_ellipse(ox + 22, oy + 9, 8.5, 8) if y < oy + 9}
    helm |= m_rect(ox + 13, oy + 7, ox + 31, oy + 10)
    paint(cv, helm, OLIVE, f_sphere(ox + 19, oy + 3, 11, len(OLIVE), 0.25), OLIVE[0], soft=rim)
    cv.hline(ox + 15, ox + 29, oy + 9, OLIVE[1])
    cv.fill(m_rect(ox + 29, oy + 6, ox + 33, oy + 10), METAL[2])
    cv.set(ox + 31, oy + 7, "#ff3a2a")
    # The pulse rifle across the chest.
    gun = m_capsule(ox + 2, oy + 36, ox + 40, oy + 22, 2.2)
    paint(cv, gun, METAL, f_lin(ox, oy + 20, ox, oy + 38, 3.6, 1.2), "#0a0b0e")
    cv.fill(m_poly([(ox + 18, oy + 31), (ox + 22, oy + 29), (ox + 25, oy + 35), (ox + 21, oy + 37)]), METAL[1])
    cv.line(ox + 5, oy + 34, ox + 38, oy + 22, METAL[4])
    for k in range(4):
        cv.set(ox + 30 + k * 2, oy + 26 - k, "#0a0b0e")
    hand = m_ellipse(ox + 31, oy + 26, 2.5, 2.2)
    paint(cv, hand, FLESH, f_sphere(ox + 30, oy + 25, 3, len(FLESH), 0.3), FLESH[0])
    hand2 = m_ellipse(ox + 13, oy + 32, 2.5, 2.2)
    paint(cv, hand2, FLESH, f_sphere(ox + 12, oy + 31, 3, len(FLESH), 0.3), FLESH[0])


def container(cv, x, y, w, h, rmp, label=None, label_c=WHITE):
    """Corrugated intermodal container side, w x h."""
    box = m_rect(x, y, x + w, y + h)
    paint(cv, box, rmp, lambda px, py: 3.0, "#0a0806")
    for px in range(x + 2, x + w - 2, 2):
        cv.vline(px, y + 2, y + h - 3, rmp[4] if px % 4 == 0 else rmp[2])
    cv.hline(x + 1, x + w - 2, y + 1, rmp[5])
    cv.hline(x + 1, x + w - 2, y + h - 2, rmp[1])
    for cx in (x + 1, x + w - 3):
        cv.rect(cx, y + 1, cx + 2, y + 3, METAL[2])
        cv.rect(cx, y + h - 3, cx + 2, y + h - 1, METAL[2])
    if label:
        lw = text_width(F3, label)
        lx, ly = x + (w - lw) // 2, y + (h - 5) // 2
        cv.rect(lx - 1, ly - 1, lx + lw + 1, ly + 6, rmp[1])
        draw_text(cv, F3, lx, ly, label, label_c)


def coffee_tin(cv, ox, oy):
    """Red Dust instant coffee tin, 22x26."""
    body = m_rect(ox, oy + 3, ox + 22, oy + 24) | m_ellipse(ox + 11, oy + 24, 11, 2)
    mug_r = ramp("#2a0606", "#5a0e0c", "#8e1a14", "#c02e22", "#e85a40", "#ffa080")
    paint(cv, body, mug_r, f_cyl(ox, ox + 22, len(mug_r), 0.3, 0.4), mug_r[0])
    lab = {(x, y) for x, y in body if oy + 9 <= y <= oy + 19 and (x, y) not in edge(body)}
    paint(cv, lab, CREAM, f_cyl(ox, ox + 22, len(CREAM), 0.3, 1.0))
    paint(cv, m_ellipse(ox + 8, oy + 14.5, 3.6, 3.6), ORANGE, f_sphere(ox + 7, oy + 13.5, 4, len(ORANGE)), ORANGE[0])
    cv.hline(ox + 4, ox + 12, oy + 15, "#7a2a06")
    draw_text(cv, F3, ox + 13, oy + 10, "RD", "#8e1a14")
    cv.hline(ox + 13, ox + 19, oy + 16, "#8e1a14")
    cv.hline(ox + 13, ox + 17, oy + 18, "#c0b08a")
    lid = m_ellipse(ox + 11, oy + 3, 11, 2.6)
    paint(cv, lid, METAL, f_cyl(ox, ox + 22, len(METAL), 0.3, 1.5), METAL[1])
    cv.fill(edge(m_ellipse(ox + 11, oy + 3, 8, 1.6)), METAL[2])
    cv.vline(ox + 3, oy + 5, oy + 22, mug_r[5])


def b_homeline():
    cv = Canvas(BW, BH, "#f0e6cc")
    for y in range(BH):
        for x in range(BW):
            if (x + y * 2) % 7 == 0:
                cv.set(x, y, "#e8dcbc")
    panel = m_rect(0, 0, 54, BH)
    paint(cv, panel, TEAL, f_lin(0, 0, 30, BH, 3.2, 1.6), None, 4)
    for y in range(BH):
        cv.set(54, y, "#ec7418")
        cv.set(55, y, "#ffa448")
    wy_logo(cv, 5, 4, "#8ae0d0", small=True)
    draw_text(cv, F3, 16, 4, "WEYLAND", "#8ae0d0")
    draw_text(cv, BOLD, 5, 11, "HOME", WHITE, shadow=TEAL[0])
    draw_text(cv, BOLD, 5, 21, "LINE", bands((3, "#ffd29a"), (3, "#ffa448"), (3, "#ec7418")), shadow=TEAL[0])
    draw_text(cv, F3, 35, 21, "FOR", "#8ae0d0")
    draw_text(cv, F3, 35, 27, "HABS", "#8ae0d0")
    draw_text(cv, F3, 5, 33, "FOLD-AWAY", "#d8f4ec")
    cells = (("BUNK", "$89", bunk_bed, 0), ("CHAIR", "$45", egg_chair, 1), ("LAMP", "$29", floor_lamp, 2))
    for i, (name, price, fn, k) in enumerate(cells):
        x0 = 59 + i * 33
        cell = m_rrect(x0, 3, x0 + 31, 36, 2)
        cv.fill(shift(cell, 1, 1), "#c8b890")
        cv.fill(cell, ("#fff8e8", "#fdf0dc", "#fff8e8")[i])
        cv.fill(edge(cell), "#8a7a5a")
        draw_text(cv, F3, x0 + 3, 5, name, "#106a62")
        fn(cv, x0 + 4 - (fn is floor_lamp), 11)
        cv.hline(x0 + 2, x0 + 28, 29, "#e4d4b0")
        tag = m_rrect(x0 + 7, 30, x0 + 24, 37, 1)
        cv.fill(tag, "#ec7418")
        cv.fill(edge(tag), "#7a2a06")
        draw_text_c(cv, F3, x0 + 16, 31, price, WHITE)
    frame(cv, "#0a4842", None, None)
    return cv


def b_jacket():
    cv = Canvas(BW, BH)
    noise_tex(cv, 0, 0, BW, BH, OLIVE, 24, 4, 1.0, 2.6)
    for y in range(BH):
        for x in range(0, 44):
            if (x - y) % 6 == 0:
                cv.set(x, y, mix(cv.get(x, y), OLIVE[0], 0.5))
    glow(cv, 22, 20, 24, 24, "#c8b070", 0.25, 0, 0, 44, BH)
    flight_jacket(cv, 5, 5)
    squadron_patch(cv, 144, 13)
    draw_text(cv, F3, 46, 4, "GENUINE ISSUE PATTERN", "#d8d0a0", shadow=OLIVE[0])
    draw_text(cv, BOLD, 46, 11, "FLIGHT JACKET", bands((3, "#fff0d0"), (3, "#e8c890"), (3, "#c89a5a")),
              outline="#0e1008", shadow=LEATHER[1])
    draw_text(cv, F5, 46, 23, "SYNTHLEATHER", "#ffd27a", shadow=OLIVE[0])
    draw_text(cv, F3, 46, 32, "AS WORN BY DROPSHIP PILOTS", "#c8c8a0", shadow=OLIVE[0])
    frame(cv, "#0e1008", OLIVE[4], None)
    return cv


def b_milk():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, 32, ["#2a7ad8", "#4a9ae8", "#7ab8f2", "#b0d8f8"])
    for cx, cy, r in ((108, 24, 3), (114, 25, 2.5), (156, 34, 4)):
        paint(cv, m_ellipse(cx, cy, r * 1.4, r), MILK, f_sphere(cx - 1, cy - 2, r * 1.6, len(MILK), 0.5), None, 4)
    for x in range(BW):
        y = int(31 + math.sin(x * 0.12) * 1.6 + math.sin(x * 0.31 + 1) * 0.6)
        for yy in range(y, BH):
            cv.set(x, yy, pick(MILK, 4 - (yy - y) * 0.35, x, yy, 4))
        cv.set(x, y - 1, MILK[1])
    for x, y in ((24, 27), (30, 24), (27, 21), (8, 25), (5, 22), (33, 27)):
        paint(cv, m_ellipse(x, y, 1.6, 1.6), MILK, lambda px, py: 3.2, MILK[1])
    milk_carton(cv, 7, 3)
    draw_text(cv, BOLD, 33, 3, "MILKWAY", bands((6, WHITE), (6, "#e4f4ff"), (6, "#bce0fa")), outline="#1c4a9a",
              shadow="#1c4a9a", scale=2, shadow_off=(2, 2))
    draw_text(cv, F5, 34, 22, "SYNTHETIC MILK", WHITE, outline="#1c4a9a")
    draw_text(cv, F3, 34, 33, "FORTIFIED. SHELF STABLE.", "#3a5a7a")
    sticker(cv, 143, 20, 15, 12.5, 18, ramp("#0c2c5a", "#1c4a9a", "#3a7ac0", "#7ab4ec", "#cfe6ff"), "#06183a",
            [(F3, "NO", WHITE, -9), (F5, "COW", WHITE, -3), (F3, "NEEDED", "#cfe6ff", 5)], shadow="#8a9aa8")
    frame(cv, "#1c4a9a", None, None)
    return cv


def b_seeds():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#1a0626", "#14051e", "#0e0416"])
    glow(cv, 24, 12, 30, 30, "#c05ad8", 0.4, 0, 0, 50, BH)
    # Grow light bar.
    bar = m_rrect(3, 2, 46, 7, 1)
    paint(cv, bar, METAL, f_hcyl(2, 7, len(METAL), 0.3, 0.8), METAL[0])
    for x in range(5, 45):
        cv.set(x, 6, "#ff6ad8" if x % 3 else "#8a9aff")
    for x in range(5, 45, 2):
        for y in range(8, 27):
            if (x + y) % 6 == 0:
                cv.set(x, y, (255, 120, 220, 70))
    # Tray, water and seedlings.
    tray = m_poly([(3, 28), (46, 28), (44, 35), (5, 35)])
    paint(cv, tray, MILK, f_lin(0, 28, 0, 35, 3.6, 1.4), "#3a3048")
    cv.hline(5, 44, 29, "#5ab4dc")
    for i, (sx, h, big) in enumerate(((9, 9, 0), (16, 14, 1), (24, 11, 0), (31, 17, 1), (39, 8, 0))):
        cv.vline(sx, 28 - h, 28, LEAF[2])
        cv.vline(sx + 1, 30 - h, 28, LEAF[1])
        for k, ly in enumerate(range(28 - h + 2, 26, 4)):
            side = 1 if (k + i) % 2 else -1
            leaf = m_ellipse(sx + side * 2.5 + 0.5, ly + 0.5, 2.2 + big * 0.4, 1.2)
            paint(cv, leaf, LEAF, lambda x, y: 3.6 if y < ly + 0.5 else 2.4, LEAF[1])
        top = m_ellipse(sx + 0.5, 28 - h, 2 + big, 1.6 + big * 0.4)
        paint(cv, top, LEAF, f_sphere(sx - 0.5, 27 - h, 3, len(LEAF), 0.3), LEAF[1])
        if big:
            paint(cv, m_ellipse(sx + 3.5, 22 - h // 3, 2, 2), CAN_RED, f_sphere(sx + 3, 21 - h // 3, 2.5, len(CAN_RED),
                                                                               0.3), CAN_RED[0])
    draw_text(cv, F3, 52, 4, "LASALLE AGRI-GENETICS", "#c8a0e0")
    draw_text(cv, BOLD, 52, 11, "VERDANT", bands((3, LEAF[5]), (3, LEAF[4]), (3, LEAF[3])), outline="#0e0416",
              shadow=PURPLE[2])
    draw_text(cv, F5, 52, 23, "SEED KITS", "#ff9ae8", shadow="#0e0416")
    draw_text(cv, F3, 52, 32, "GROWS IN ANY HAB", "#c8a0e0")
    for band, crop, ang, dx in ((LIME, "lettuce", 14, 118), (ORANGE, "carrot", 2, 132),
                                (CAN_RED, "tomato", -12, 146)):
        pk = Canvas(32, 32)
        seed_packet(pk, 9, 6, band, crop)
        rot = pk.img.rotate(ang, resample=Image.NEAREST, center=(16, 16))
        sub = Canvas(32, 32)
        sub.img, sub.px = rot, rot.load()
        for y in range(32):
            for x in range(32):
                if sub.px[x, y][3]:
                    cv.set(dx - 16 + x + 1, 4 + y + 1, (0, 0, 0, 120))
        cv.paste(sub, dx - 16, 4)
    frame(cv, "#06020a", PURPLE[2], None)
    return cv


def b_uscm():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#0c1a34", "#0a1428", "#060e1e"])
    for y in range(BH):
        for x in range(96, BW):
            a = math.atan2(y + 0.5 - 44, x + 0.5 - 134) * 18 / math.pi
            if int(math.floor(a)) % 2 == 0:
                k = 1 - math.hypot(x - 134, y - 44) / 70
                if k > threshold(x, y, 4) * 0.8:
                    cv.set(x, y, mix(cv.get(x, y), "#c49a1a", 0.35))
    for i in range(5):
        for x in range(0, 60):
            cv.set(x, 36 + i, "#a01818" if i % 2 == 0 else "#e8e0d0")
    colonial_marine(cv, 112, 3)
    draw_text(cv, F5, 6, 4, "JOIN THE", "#ffd27a", shadow="#000000")
    draw_text(cv, BOLD, 6, 13, "COLONIAL", bands((3, WHITE), (3, "#e8e8f0"), (3, "#b8c0d8")), outline="#04080f",
              shadow="#a01818")
    draw_text(cv, BOLD, 6, 24, "MARINES", bands((3, "#fff6c0"), (3, "#ffe068"), (3, "#f0b628")), outline="#04080f",
              shadow="#a01818")
    # Winged star.
    star_c = (86, 22)
    for sgn in (-1, 1):
        for k in range(4):
            y = star_c[1] - 4 + k * 2
            x0 = star_c[0] + sgn * 8
            x1 = star_c[0] + sgn * (21 - k * 3)
            cv.hline(x0, x1, y, "#ffd27a" if k == 0 else "#c49a1a")
            cv.hline(x0, x1 - sgn, y + 1, "#7a5a08")
    disc = m_ellipse(star_c[0], star_c[1], 8, 8)
    paint(cv, disc, GOLD, f_sphere(star_c[0] - 2, star_c[1] - 3, 11, len(GOLD), 0.4), GOLD[0])
    cv.fill(m_ellipse(star_c[0], star_c[1], 6, 6), "#0c1a34")
    cv.fill(m_burst(star_c[0], star_c[1] + 0.5, 5.5, 2.3, 5, -math.pi / 2), WHITE)
    draw_text(cv, F3, 63, 34, "ENLIST TODAY", WHITE, shadow="#000000")
    frame(cv, "#000000", "#c49a1a", None)
    return cv


def b_icsc():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#2a1838", "#5a2a40", "#a8483a", "#e08a4a"])
    # Gantry crane silhouette.
    cv.rect(0, 2, 104, 4, "#1a0e1a")
    for x in range(0, 104, 6):
        cv.line(x, 4, x + 3, 2, "#1a0e1a")
    cv.rect(8, 2, 11, BH, "#1a0e1a")
    cv.vline(86, 4, 8, "#0e080e")
    cv.rect(83, 8, 90, 10, "#0e080e")
    for y in range(BH - 3, BH):
        cv.hline(0, BW, y, "#140a10")
    container(cv, 2, 23, 33, 14, RUST, "ICSC")
    container(cv, 35, 23, 33, 14, NAVY, "W-Y")
    container(cv, 68, 23, 33, 14, ramp("#0a2a14", "#14482a", "#1e6a3a", "#2a8a4a", "#4aaa6a", "#8ad0a0"), "SEEGSON")
    container(cv, 14, 9, 33, 14, ORANGE, "ICSC")
    container(cv, 72, 10, 26, 13, TEAL, "07")
    frame(cv, "#0a0608", None, None)
    panel = m_rect(104, 1, BW - 1, BH - 1)
    cv.fill(panel, "#14101a")
    cv.vline(104, 1, BH - 2, "#e08a4a")
    draw_text(cv, BOLD, 108, 4, "ICSC", bands((3, WHITE), (6, "#ffd29a")), shadow="#7a2a06")
    draw_text(cv, F3, 108, 15, "CERTIFIED", "#e08a4a")
    draw_text(cv, F5, 108, 21, "SHIPPING", WHITE)
    draw_text(cv, F3, 108, 32, "BONDED LINE", "#d8b8a0")
    frame(cv, "#0a0608", None, None)
    return cv


def b_starlane():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#040a1a", "#06102a", "#08163a"])
    starfield(cv, random.Random(29), 1, 1, BW - 1, BH - 1, 40, ("#1a2a4a", "#3a4a6a", "#8a9ac0", "#ffffff"))
    mx0 = 88
    for x in range(mx0, BW, 8):
        cv.vline(x, 2, BH - 3, "#0e2244")
    for y in range(4, BH, 8):
        cv.hline(mx0, BW - 3, y, "#0e2244")
    # UPP space, hatched off in red.
    for y in range(2, BH - 2):
        for x in range(mx0, BW - 2):
            if x - mx0 + (BH - y) * 0.9 > 64:
                if (x + y) % 4 == 0:
                    cv.set(x, y, "#8a1a1a")
                if abs(x - mx0 + (BH - y) * 0.9 - 64) < 0.9:
                    cv.set(x, y, "#ff4a3a")
    draw_text(cv, F3, 139, 32, "UPP", "#ff6a5a")
    # Route.
    stops = [(98, 30, "#ffd040", "SOL"), (118, 13, "#4a94d0", "GATEWAY"), (138, 6, "#a8d8a0", "")]
    pts = [(98, 30), (104, 22), (118, 13), (130, 10), (138, 6)]
    k = 0
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = int(max(abs(x1 - x0), abs(y1 - y0)))
        for i in range(n):
            if k % 3 != 2:
                cv.set(int(x0 + (x1 - x0) * i / n), int(y0 + (y1 - y0) * i / n), "#ffb030")
            k += 1
    for x, y, c, name in stops:
        paint(cv, m_ellipse(x + 0.5, y + 0.5, 3, 3), ramp(mix(c, "#000000", 0.6), mix(c, "#000000", 0.3), c,
                                                             mix(c, "#ffffff", 0.5)),
              f_sphere(x - 0.5, y - 0.5, 4, 4, 0.2), None)
        if name:
            draw_text(cv, F3, x + 5, y - 2, name, "#c8d4f0")
    shuttle_s = Canvas(44, 15)
    shuttle(shuttle_s, 0, 0, hull=SHELL, stripe="#e86a1a", flame=False)
    small = shuttle_s.img.resize((22, 7), Image.NEAREST)
    sc = Canvas(22, 7)
    sc.img, sc.px = small, small.load()
    cv.paste(sc, 97, 15)
    shield = m_poly([(94, 3), (104, 3), (104, 9), (99, 13), (94, 9)])
    paint(cv, shield, NAVY, lambda x, y: 4, WHITE)
    draw_text(cv, F3, 95, 5, "UA", WHITE)
    draw_text(cv, F3, 6, 4, "STARLANE CRUISES", "#8ac8f0")
    draw_text(cv, BOLD, 6, 11, "SAFE ROUTES", bands((3, WHITE), (3, "#d8e8ff"), (3, "#8ab8f0")), outline="#02040a",
              shadow="#1c4a9a")
    draw_text(cv, F5, 6, 23, "UA SPACE ONLY", "#ffb030", shadow="#02040a")
    draw_text(cv, F3, 6, 32, "NO UPP CHECKPOINTS", "#8a9ac0")
    frame(cv, "#000000", "#1c3a6a", None)
    return cv


def b_instant():
    cv = Canvas(BW, BH)
    vgrad(cv, 0, 0, BW, BH, ["#3a1420", "#7a2a2a", "#c0503a", "#ec8a4a", "#ffc070"])
    sun = m_ellipse(128, 30, 10, 10)
    paint(cv, sun, ramp("#ffc070", "#ffe0a0", "#fff4d0"), lambda x, y: 2.2 - math.hypot(x - 128, y - 30) / 5, None,
          4)
    for k, (base, amp, c) in enumerate(((31, 3, "#7a2a20"), (34, 2, "#4a1814"), (37, 1.5, "#2a0c0a"))):
        for x in range(BW):
            y = int(base + amp * math.sin(x * (0.05 + k * 0.03) + k * 2))
            cv.vline(x, y, BH, c)
    for y in range(BH):
        for x in range(0, 100):
            k = (100 - x) / 30
            if k > 1 or threshold(x, y, 4) < k:
                cv.set(x, y, mix(cv.get(x, y), "#1e0a08", 0.55))
    coffee_tin(cv, 116, 10)
    scoop = m_ellipse(146, 33, 5, 2.4)
    paint(cv, scoop, METAL, f_sphere(144, 32, 5, len(METAL), 0.3), METAL[0])
    cv.fill(m_ellipse(146, 32.4, 3.6, 1.2), BROWN[2])
    cv.line(150, 32, 156, 29, METAL[4])
    draw_text(cv, F5, 6, 4, "RED DUST", "#ffe0c0", shadow="#1e0a08")
    draw_text(cv, F3, 56, 5, "COFFEE CO.", "#e8a080")
    draw_text(cv, BOLD, 5, 12, "INSTANT", bands((5, "#fff0d8"), (6, "#ffc888"), (7, "#e8823a")), outline="#140804",
              shadow="#8e1a14", scale=2, shadow_off=(2, 2))
    draw_text(cv, F3, 6, 33, "ONE SCOOP. FULL SHIFT.", "#ffd0b0", shadow="#1e0a08")
    frame(cv, "#0e0604", "#8e1a14", None)
    return cv


# Products that exist as items in the game get their ads from the item sprites instead (deep_ads.yml).
BANNERS = [
    b_working_joe,
    b_aspen,
    b_hyperdyne,
    b_lasalle,
    b_transit,
    b_atmo,
    b_souta,
    b_apollo,
    b_prefab,
    b_coffee,
    b_comterm,
    b_freighter,
    b_synth_care,
    b_water,
    b_mining,
    b_gateway,
    b_aspen_light,
    b_colony_tv,
    b_homeline,
    b_jacket,
    b_milk,
    b_seeds,
    b_icsc,
    b_starlane,
    b_instant,
]


# -- Billboards ---------------------------------------------------------------------------------------------
# 96x32 animated mini commercials, one looping state per product.
BBW, BBH = 96, 32
DROP = ramp("#141810", "#262c1e", "#3a4230", "#525c44", "#6e7a5c", "#949e80")
BEER = ramp("#5a2a04", "#9a5a0a", "#d4961c", "#f4c444", "#ffe890")


def walking_joe(cv, hx, floor, p):
    """Working Joe in profile walking right with a serving tray, about 24x27; p is the walk phase in radians."""
    bob = -1 if abs(math.sin(p)) < 0.45 else 0
    hip = (hx + 0.5, floor - 9 + bob)

    def leg(off, rmp_v):
        t = 0.5 * math.sin(p + off)
        swing = math.cos(p + off)
        knee = (hip[0] + math.sin(t) * 4.6, hip[1] + math.cos(t) * 4.6)
        sa = t - (0.9 * swing if swing > 0 else 0)
        foot = (knee[0] + math.sin(sa) * 4.6, min(floor - 1.0, knee[1] + math.cos(sa) * 4.6))
        m = m_capsule(hip[0], hip[1], knee[0], knee[1], 1.4) | m_capsule(knee[0], knee[1], foot[0], foot[1], 1.3)
        paint(cv, m, SUIT, lambda x, y: rmp_v, SUIT_OL)
        boot = m_rrect(int(foot[0]) - 1, int(foot[1]) - 1, int(foot[0]) + 4, int(foot[1]) + 2, 1)
        paint(cv, boot, BLACKP, lambda x, y: 2.4 if y < foot[1] else 1.2, INK)

    leg(math.pi, 1.2)
    top = floor - 27 + bob
    torso = m_rrect(int(hx) - 4, top + 9, int(hx) + 5, top + 19, 2)
    paint(cv, torso, SUIT, f_cyl(hx - 4, hx + 5, len(SUIT), 0.35, 0.6), SUIT_OL)
    cv.rect(int(hx) + 1, top + 12, int(hx) + 4, top + 15, WHITE)
    cv.set(int(hx) + 2, top + 13, "#2f6db0")
    leg(0, 3.0)
    neck = m_rect(int(hx) - 1, top + 7, int(hx) + 2, top + 10)
    paint(cv, neck, SKIN, f_cyl(hx - 1, hx + 2, len(SKIN), 0.3, 0.5), SKIN_OL)
    cv.hline(int(hx) - 1, int(hx) + 1, top + 9, SKIN[1])
    head = m_ellipse(hx + 1, top + 4.5, 4, 4.6)
    paint(cv, head, SKIN, f_sphere(hx - 0.5, top + 2.5, 6, len(SKIN), 0.12), SKIN_OL, soft=SKIN[1])
    cv.set(int(hx) + 3, top + 4, GLOW[3])
    cv.set(int(hx) + 4, top + 4, GLOW[2])
    cv.set(int(hx) + 2, top + 4, SKIN[0])
    cv.hline(int(hx) + 3, int(hx) + 4, top + 7, SKIN_OL)
    cv.set(int(hx) - 1, top + 4, SKIN[1])
    cv.set(int(hx) - 1, top + 5, SKIN[1])
    # Arm out in front holding the tray level.
    sh = (hx + 1, top + 11)
    el = (hx + 2.5, top + 16)
    hand = (hx + 7.5, top + 15)
    paint(cv, m_capsule(sh[0], sh[1], el[0], el[1], 1.3) | m_capsule(el[0], el[1], hand[0], hand[1], 1.2), SUIT,
          lambda x, y: 3.6, SUIT_OL)
    paint(cv, m_ellipse(hand[0] + 0.5, hand[1], 1.6, 1.4), SKIN, lambda x, y: 4, SKIN_OL)
    ty = top + 13
    cv.hline(int(hx) + 5, int(hx) + 17, ty, METAL[5])
    cv.hline(int(hx) + 5, int(hx) + 17, ty + 1, METAL[2])
    mug = m_rect(int(hx) + 7, ty - 4, int(hx) + 11, ty)
    paint(cv, mug, CAN_RED, f_cyl(hx + 7, hx + 11, len(CAN_RED), 0.3, 1.0), CAN_RED[0])
    cv.set(int(hx) + 11, ty - 3, CAN_RED[1])
    dome = {(x, y) for x, y in m_ellipse(hx + 14.5, ty, 3.5, 3.5) if y < ty}
    paint(cv, dome, METAL, f_sphere(hx + 13.5, ty - 2.5, 4, len(METAL), 0.3), METAL[0])
    cv.set(int(hx) + 14, ty - 4, METAL[5])
    sp = int(round(math.sin(p * 2) * 0.8))
    cv.set(int(hx) + 8 + sp, ty - 6, (220, 220, 230, 170))
    cv.set(int(hx) + 9 - sp, ty - 8, (220, 220, 230, 110))


def bb_joe_tray(i, n):
    cv = Canvas(BBW, BBH)
    s = i * 16 // n
    vgrad(cv, 0, 0, BBW, 26, ["#3a4a5a", "#2e3c4a", "#24303c"])
    for y in range(0, 26):
        for x in range(BBW):
            k = (x + s) % 16
            if k == 0:
                cv.set(x, y, "#141c24")
            elif k == 1:
                cv.set(x, y, "#4a5c6e")
    for x in range(BBW):
        cv.set(x, 9, "#1a2430")
        cv.set(x, 10, "#5a6c7e")
        if (x + s) % 32 == 8:
            cv.rect(x, 3, x + 3, 5, "#ffc860")
            cv.hline(x - 1, x + 3, 5, "#6a4a20")
    vgrad(cv, 0, 26, BBW, BBH, ["#3a3e44", "#24272c"], 0)
    cv.hline(0, BBW, 26, "#6a7078")
    for x in range(BBW):
        if (x + s) % 8 == 0:
            cv.vline(x, 27, BBH, "#16181c")
    cv.fill(m_ellipse(28, 29.5, 9, 1.2), "#141618")
    walking_joe(cv, 24, 30, i * 2 * math.pi / n)
    panel = m_rect(52, 0, BBW, BBH)
    paint(cv, panel, SUIT, f_lin(52, 0, 76, BBH, 2.4, 0.8), None, 4)
    cv.vline(52, 0, BBH, "#4a8cbd")
    cv.vline(53, 0, BBH, "#08101a")
    seeg_logo(cv, 60, 8, 5, "#4a8cbd", "#0f1c2c", WHITE)
    draw_text(cv, F3, 66, 3, "SEEGSON", "#9cc8f0")
    draw_text(cv, F3, 66, 9, "SERVICE", "#5a8ab8")
    draw_text(cv, F5, 55, 16, "WORKING", "#ffb030", shadow="#3a1c00")
    draw_text(cv, F5, 55, 24, "JOE", WHITE, shadow="#08101a")
    draw_text(cv, F3, 76, 26, "24/7", "#9cc8f0")
    frame(cv, "#03080f", None, None)
    return cv


def bb_cryo_wake(i, n):
    cv = Canvas(BBW, BBH)
    vgrad(cv, 0, 0, BBW, BBH, ["#02040c", "#040a1a", "#081428", "#0c1c36"])
    starfield(cv, random.Random(31), 1, 1, 60, 14, 18)
    for x in range(1, 60):
        cv.set(x, 15, "#1c2c44")
    lid = [0, 0, 10, 22, 32, 36, 36, 26, 14, 4][i]
    open_k = lid / 36
    glow(cv, 30, 26, 32, 8, "#2ab0d0", 0.2 + 0.25 * open_k, 0, 0, 62, BBH)
    cryo_pod(cv, 3, 7, lid=lid, sleeper=True, L=56)
    if lid >= 10:
        t = i - 2 + 1
        for j in range(60):
            r = random.Random(j * 7 + 3)
            sx = 9 + r.random() * 40
            dx = (sx - 30) / 20 * 1.1 + (r.random() - 0.5) * 0.5
            dy = -0.55 - r.random() * 0.35
            tt = t * (0.6 + r.random() * 0.6)
            x = sx + dx * tt * 2.2
            y = 15 + dy * tt * 2.2 + 0.32 * tt * tt
            a = max(0.0, 1 - tt / 10) * (0.5 + open_k * 0.5)
            if a <= 0:
                continue
            cv.set(int(x), int(y), (214, 244, 255, int(170 * a)))
            for ddx, ddy in N4:
                cv.set(int(x) + ddx, int(y) + ddy, (190, 232, 250, int(60 * a)))
            for ddx, ddy in ((2, 0), (-2, 0), (1, 1), (-1, 1), (1, -1), (-1, -1)):
                cv.set(int(x) + ddx, int(y) + ddy, (170, 220, 245, int(28 * a)))
        for x, y in m_ellipse(30, 28, 30, 2.5):
            cv.set(x, y, (180, 230, 250, int(70 * open_k)))
    panel = m_rect(62, 0, BBW, BBH)
    for x, y in panel:
        cv.set(x, y, mix(cv.get(x, y), "#02040c", 0.6))
    cv.vline(62, 0, BBH, "#1c3a5a")
    wy_logo(cv, 74, 3, "#c8d4f0", small=True)
    draw_text(cv, F5, 65, 11, "HYPER", bands((2, WHITE), (5, "#8ee0f4")))
    draw_text(cv, F5, 65, 19, "SLEEP", bands((2, WHITE), (5, "#8ee0f4")))
    if lid >= 30:
        draw_text(cv, F3, 65, 27, "WAKE UP", "#ffd27a")
    else:
        draw_text(cv, F3, 65, 27, "WAKE UP", "#3a4a6a")
    frame(cv, "#000000", None, None)
    return cv


def bb_wy_spin(i, n):
    cv = Canvas(BBW, BBH)
    radial(cv, 48, 9, 44, ["#3a2a0a", "#261a08", "#160e05", "#0a0703"])
    a = i * 2 * math.pi / n
    sx = math.cos(a)
    cx, top, S = 48, 2, 2
    ew, eh = len(WY_MARK[0]), len(WY_MARK)
    side = 1 if math.sin(a) >= 0 else -1
    mark = set()
    if abs(sx) < 0.12:
        mark = m_rect(cx - 1, top + 1, cx + 1, top + eh * S - 1)
    else:
        for py in range(eh * S):
            for x in range(cx - 15, cx + 15):
                u = ((x + 0.5 - cx) / sx + ew * S / 2) / S
                ui = int(math.floor(u))
                if 0 <= ui < ew and WY_MARK[py // S][ui] == "#":
                    mark.add((x, top + py))
    depth = shift(mark, -side, 1) - mark
    cv.fill(halo(mark | depth), "#0a0703")
    cv.fill(depth, GOLD[1])
    light = 0.3 + 0.7 * abs(sx)
    for x, y in mark:
        v = (len(GOLD) - 1) * light * (1.05 - (y - top) / (eh * S) * 0.45)
        cv.set(x, y, pick(GOLD, v, x, y, 4))
    if i == 0:
        blink(cv, cx - 12, top + 1, GOLD[5], big=True)
    if i == n // 2 + 1:
        blink(cv, cx + 4, top + 3, GOLD[4])
    txt = shift(text_mask(F5, "WEYLAND-YUTANI"), 48 - text_width(F5, "WEYLAND-YUTANI") // 2, 18)
    cv.fill(shift(txt, 1, 1), "#3a2a0a")
    gx = -24 + i * 13
    for x, y in txt:
        d = abs(x - gx + (y - 18))
        cv.set(x, y, WHITE if d < 3 else GOLD[5] if d < 5 else GOLD[4] if y < 20 else GOLD[3])
    draw_text_c(cv, F3, 48, 26, "BUILDING BETTER WORLDS", GOLD[2])
    frame(cv, "#000000", "#3a2a0a", None)
    return cv


def pint_glass(cv, x0, top, bottom, fill, foam=2):
    """Pint glass, wider at the rim; fill 0..1 of beer with a foam head."""
    w_top, w_bot = 14, 10
    h = bottom - top
    level = bottom - 1 - fill * (h - 3)
    for y in range(top, bottom):
        w = w_top - (w_top - w_bot) * (y - top) / h
        xl = int(round(x0 + (w_top - w) / 2))
        xr = int(round(x0 + (w_top + w) / 2)) - 1
        for x in range(xl + 1, xr):
            if fill > 0 and y >= level:
                cv.set(x, y, pick(BEER, 3.6 - (x - xl) / max(1, xr - xl) * 2.2 - (y - level) * 0.04, x, y, 2))
            elif fill > 0 and y >= level - foam:
                cv.set(x, y, "#fff6e0" if y > level - foam else WHITE)
            else:
                cv.set(x, y, mix(cv.get(x, y), "#ffffff", 0.12))
        cv.set(xl, y, (232, 246, 255, 220))
        cv.set(xr, y, (160, 200, 220, 200))
    cv.hline(int(x0 + 2), int(x0 + 11), bottom, (232, 246, 255, 220))
    cv.hline(int(x0 + 3), int(x0 + 10), bottom - 1, (200, 230, 245, 180))
    for y in range(top + 2, bottom - 2):
        cv.set(int(x0) + 2, y, (255, 255, 255, 120))


def bb_aspen_pour(i, n):
    cv = Canvas(BBW, BBH)
    sunrays(cv, 0, 0, BBW, BBH, 46, 44, 20, rgba("#8a0c12"), rgba("#a8161c"), i * (4 * math.pi / 20) / n)
    for y in range(BBH):
        for x in range(BBW):
            if math.hypot((x - 46) / 70, (y - 30) / 34) > 0.62 + threshold(x, y, 4) * 0.25:
                cv.tint({(x, y)}, 0.72)
    tilt = [0, 50, 95, 95, 95, 95, 50, 0, 0, 0][i]
    fill = [0, 0, 0.12, 0.38, 0.62, 0.84, 0.95, 1, 1, 1][i]
    centre = {0: (20, 17.5), 50: (26, 12), 95: (29.5, 8)}[tilt]
    if tilt == 95:
        sx = 40 + (1 if i % 2 else 0)
        level = 30 - fill * 17
        for y in range(9, int(level) + 1):
            wob = 1 if (y + i) % 5 == 0 else 0
            cv.set(sx + wob, y, BEER[3])
            cv.set(sx + 1 + wob, y, BEER[2])
        cv.set(sx, int(level), WHITE)
        cv.set(sx + 2, int(level) - 1, "#fff6e0")
    pint_glass(cv, 34, 12, 31, fill, 2 if fill < 0.9 else 3)
    if fill >= 1:
        for bx, by in ((38, 26), (42, 22), (40, 18), (44, 27)):
            cv.set(bx, by - (i % 3), BEER[4])
    sub = Canvas(40, 40)
    beer_can(sub, 13, 6)
    if tilt == 0:
        for k in range(3):
            dx = 16 + k * 4
            dy = 12 + ((i * 3 + k * 5) % 13)
            sub.set(dx, dy, CAN_RED[5])
            sub.set(dx, dy + 1, CAN_RED[4])
            sub.set(dx, dy - 1, CAN_RED[3])
    rot = sub.img.rotate(-tilt, resample=Image.NEAREST, center=(20, 19.5))
    rc = Canvas(40, 40)
    rc.img, rc.px = rot, rot.load()
    cv.paste(rc, int(round(centre[0] - 20)), int(round(centre[1] - 19.5)))
    if i in (8, 9):
        blink(cv, 33 if i == 8 else 49, 9 if i == 8 else 16, "#ffc8a0", big=True)
    draw_text(cv, BOLD, 57, 3, "ASPEN", bands((3, "#fff6c0"), (3, "#ffe068"), (3, "#f0b628")), outline="#2a0204",
              shadow="#2a0204")
    draw_text(cv, F5, 59, 15, "LAGER", WHITE, shadow="#3a0206")
    draw_text(cv, F3, 59, 25, "ICE COLD", "#ffc8a0")
    frame(cv, "#200204", "#d84030", "#4a0408")
    return cv


def led_row(cv, font, y, text, period, offset, lit, dim, x0=3, x1=BBW - 3):
    """One line of a scrolling LED sign: text repeats every period pixels, shifted left by offset."""
    w = text_width(font, text)
    assert w < period, (text, w, period)
    mask = text_mask(font, text)
    on = set()
    for k in range(-1, (x1 - x0) // period + 2):
        for mx, my in mask:
            x = x0 + mx + k * period - offset
            if x0 <= x < x1:
                on.add((x, y + my))
    for x, yy in halo(on):
        if x0 <= x < x1 and (x, yy) not in on:
            cv.set(x, yy, mix(cv.get(x, yy), dim, 0.5))
    for x, yy in on:
        cv.set(x, yy, lit)


def bb_price_ticker(i, n):
    cv = Canvas(BBW, BBH, "#202228")
    frame(cv, "#0a0b0e", "#4a4e56", "#121418")
    for y in range(2, BBH - 2):
        for x in range(2, BBW - 2):
            cv.set(x, y, "#160c06" if (x + y) % 2 else "#0c0604")
    led_row(cv, F5, 4, "ASPEN 6-PACK $9.99  *", 120, i * 120 // n, "#ffb030", "#a84a00")
    for x in range(3, BBW - 3, 2):
        cv.set(x, 13, "#3a1a08")
    led_row(cv, F3, 16, "SOUTA PINKS $2.20 *", 96, (i * 96 // n + 40) % 96, "#62e27a", "#137028")
    for x in range(3, BBW - 3, 2):
        cv.set(x, 23, "#3a1a08")
    if i % 2 == 0:
        led_row(cv, F3, 25, "*", 1000, 0, "#ff4a3a", "#8a1a10", 8, 12)
        led_row(cv, F3, 25, "*", 1000, 0, "#ff4a3a", "#8a1a10", 84, 88)
    label = "PRICES TODAY"
    lx = 48 - text_width(F3, label) // 2
    led_row(cv, F3, 25, label, 1000, 0, "#ff4a3a", "#8a1a10", lx, lx + text_width(F3, label) + 1)
    for x, y in ((2, 2), (BBW - 3, 2), (2, BBH - 3), (BBW - 3, BBH - 3)):
        cv.set(x, y, METAL[4])
    return cv


def processor_small(cv, ox, ground, lights_on=True):
    """A shorter M-class processor silhouette for the billboards, about 50x26, rim lit from the left."""
    D = ramp("#0c0810", "#1a1220", "#2a1e2e", "#3e2c3e")
    RIM = rgba("#f0904a")

    def part(mask):
        for x, y in mask:
            cv.set(x, y, D[1] if (x + y) % 9 else D[2])
        for x, y in edge(mask):
            if (x - 1, y) not in mask:
                cv.set(x, y, RIM)
            elif (x, y - 1) not in mask:
                cv.set(x, y, D[3])
            else:
                cv.set(x, y, D[0])

    g = ground
    part(m_rect(ox + 4, g - 12, ox + 10, g))
    part(m_rect(ox + 34, g - 16, ox + 41, g))
    part(m_rect(ox + 41, g - 8, ox + 48, g))
    part(m_rect(ox, g - 5, ox + 50, g))
    part(m_rect(ox + 16, g - 21, ox + 30, g - 18))
    part(m_rect(ox + 18, g - 19, ox + 28, g))
    part(m_rect(ox + 21, g - 26, ox + 25, g - 20))
    part(m_rect(ox + 10, g - 9, ox + 18, g - 7))
    part(m_rect(ox + 28, g - 12, ox + 34, g - 10))
    for by in range(g - 16, g - 6, 4):
        cv.hline(ox + 19, ox + 26, by, D[3])
    rng = random.Random(77)
    for _ in range(10):
        cv.set(rng.randrange(ox + 2, ox + 48), rng.randrange(g - 4, g - 1), "#ffc860" if rng.random() > 0.3 else
               "#ff9a30")
    for x in range(ox + 20, ox + 27, 2):
        cv.set(x, g - 14, "#ffc860")
    if lights_on:
        for x, y in ((ox + 23, g - 27), (ox + 7, g - 13), (ox + 37, g - 17)):
            cv.set(x, y, "#ff3a2a")
            cv.set(x, y - 1, "#ff9a8a")


def bb_atmo_puff(i, n):
    cv = Canvas(BBW, BBH)
    vgrad(cv, 0, 0, BBW, 28, ["#140a26", "#2a1238", "#4e1a3e", "#82283a", "#b8442e", "#e07a34", "#f4b45a"])
    sun = m_ellipse(84, 25, 5, 5)
    paint(cv, sun, ramp("#f8c070", "#ffe0a0", "#fff4d0"), lambda x, y: 2 - math.hypot(x - 84, y - 25) / 3, None, 4)
    for x in range(BBW):
        h = int(24 + 2 * math.sin(x * 0.08) + math.sin(x * 0.23 + 1))
        cv.vline(x, h, BBH, "#4a1e3a")
        cv.set(x, h, "#8a3a4a")
    C = ramp("#4a3448", "#7a5a6a", "#a88a8a", "#d8bca8", "#f8e4cc")
    sx, sy = 63, 4
    K = 5
    for k in range(K - 1, -1, -1):
        t = (k + i / n) / K
        px = sx + 1 + t * 36
        py = sy + 1 + math.sin(t * 5) * 1.2 - t * 1.5
        r = 2.2 + t * 4.6
        alpha = int(255 * max(0.0, 1 - t * t * 0.95))
        if alpha <= 20:
            continue
        m = m_ellipse(px, py, r * 1.25, r)
        fn = f_sphere(px - r * 0.5, py + r * 0.6, r * 1.7, len(C), 0.25)
        for x, y in m:
            c = pick(C, fn(x, y), x, y, 4)
            cv.set(x, y, c[:3] + (alpha,))
    processor_small(cv, 40, 31, i % 4 < 2)
    for x in range(BBW):
        cv.set(x, 31, "#120a14")
    for y in range(BBH):
        for x in range(0, 40):
            k = (40 - x) / 6
            if k > 1 or threshold(x, y, 4) < k:
                cv.set(x, y, mix(cv.get(x, y), "#0c0612", 0.75))
    wy_logo(cv, 4, 4, "#f4b45a", small=True)
    draw_text(cv, F3, 4, 12, "M-CLASS", WHITE)
    draw_text(cv, F3, 4, 19, "BREATHE", "#f0e0d0")
    draw_text(cv, F3, 4, 25, "EASY", "#f0904a")
    frame(cv, "#080410", None, None)
    return cv


def dropship(cv, ox, oy, flame=True):
    """UD-4 style dropship in profile flying right, 32x13."""
    if flame:
        for k, c in enumerate(("#ff6a00", "#ffb030", "#fff2c0")):
            cv.fill(m_ellipse(ox + 1 + k, oy + 6.5, 3.5 - k, 1.6 - k * 0.4), c)
    fin = m_poly([(ox + 3, oy + 6), (ox + 1, oy), (ox + 6, oy), (ox + 11, oy + 5)])
    paint(cv, fin, DROP, lambda x, y: 2.6, "#0a0c08")
    body = m_poly([(ox + 3, oy + 5), (ox + 10, oy + 3), (ox + 22, oy + 3), (ox + 28, oy + 5), (ox + 32, oy + 7.5),
                   (ox + 26, oy + 9.5), (ox + 6, oy + 9.5), (ox + 3, oy + 8)])
    paint(cv, body, DROP, f_hcyl(oy + 3, oy + 10, len(DROP), 0.25, 0.6), "#0a0c08", soft=DROP[3])
    pod = m_capsule(ox + 9, oy + 10.5, ox + 19, oy + 10.5, 1.8)
    paint(cv, pod, METAL, f_hcyl(oy + 8.5, oy + 12.5, len(METAL), 0.3, 0.8), METAL[0])
    cv.set(ox + 8, oy + 10, "#ffb030")
    cv.set(ox + 8, oy + 11, "#ff6a00")
    cv.fill(m_poly([(ox + 21, oy + 5), (ox + 26, oy + 5), (ox + 29, oy + 7), (ox + 21, oy + 7)]), "#10283a")
    cv.hline(ox + 22, ox + 25, oy + 5, "#8adcf0")
    cv.line(ox + 26, oy + 9, ox + 31, oy + 10, METAL[3])
    cv.hline(ox + 6, ox + 20, oy + 6, DROP[1])
    for x in range(ox + 11, ox + 18, 3):
        cv.set(x, oy + 7, "#ffe6a0")


def bb_dropship_gateway(i, n):
    cv = Canvas(BBW, BBH)
    vgrad(cv, 0, 0, BBW, BBH, ["#02030a", "#050818", "#0a1430"])
    starfield(cv, random.Random(33), 1, 1, BBW - 1, BBH - 1, 34)
    earth = m_ellipse(48, 74, 90, 46)
    earth_r = ramp("#0a2a5a", "#164a8a", "#2a6ab0", "#4a94d0", "#8ac8f0")
    for x, y in earth:
        if cv.inside(x, y):
            cv.set(x, y, pick(earth_r, 4 - (y - 28) * 0.9 + math.sin(x * 0.3) * 0.4, x, y, 4))
    for x, y in halo(earth, False):
        if cv.inside(x, y):
            cv.set(x, y, "#9ae6ff")
    gateway_station(cv, 72, 14)
    if i % 2 == 0:
        blink(cv, 90, 14, "#ff3a2a")
    xs = [-34, -18, -2, 14, 30, 46, 62, 78, None, None]
    x = xs[i]
    if x is not None:
        y = 18
        for k in range(1, 4):
            cv.hline(x - 10 * k, x - 10 * k + 7, y + 4 + (k % 2) * 3, (200, 220, 255, 140 - k * 35))
        dropship(cv, x, y)
    draw_text(cv, F5, 4, 3, "GATEWAY", WHITE, outline="#02030a")
    draw_text(cv, F3, 4, 12, "FLEET DAYS", "#ffb030", shadow="#02030a")
    frame(cv, "#000000", "#1c2a4a", None)
    return cv


def tv_set(cv, ox, oy, screen):
    """Wood cabinet television, 36x30; screen(cv, mask) paints the picture."""
    cv.line(ox + 14, oy + 2, ox + 8, oy - 3, METAL[4])
    cv.line(ox + 20, oy + 2, ox + 27, oy - 3, METAL[4])
    cv.fill(m_ellipse(ox + 17, oy + 2.5, 3, 1.5), METAL[2])
    for lx in (ox + 4, ox + 30):
        cv.rect(lx, oy + 29, lx + 2, oy + 31, BROWN[1])
    cab = m_rrect(ox, oy + 3, ox + 37, oy + 30, 3)
    paint(cv, cab, BROWN, f_lin(ox, oy + 3, ox + 26, oy + 30, 4.6, 2.2), BROWN[0], soft=BROWN[2])
    for y in range(oy + 6, oy + 28, 3):
        cv.hline(ox + 2, ox + 34, y, mix(BROWN[3], BROWN[4], 0.3))
    cv.fill(m_rrect(ox + 2, oy + 5, ox + 28, oy + 28, 4), "#14100c")
    scr = m_rrect(ox + 4, oy + 7, ox + 26, oy + 26, 4)
    screen(cv, scr)
    scanlines(cv, ox + 4, oy + 7, ox + 26, oy + 26, 0.75)
    for x, y in scr:
        if math.hypot((x - ox - 15) / 11, (y - oy - 16.5) / 9.5) > 0.88:
            cv.tint({(x, y)}, 0.7)
    cv.set(ox + 6, oy + 9, WHITE)
    cv.set(ox + 7, oy + 8, WHITE)
    cv.rect(ox + 29, oy + 6, ox + 35, oy + 28, BROWN[1])
    for ky in (oy + 10, oy + 17):
        paint(cv, m_ellipse(ox + 32, ky, 2.5, 2.5), METAL, f_sphere(ox + 31, ky - 1, 3, len(METAL)), METAL[0])
    for y in range(oy + 22, oy + 27, 2):
        cv.hline(ox + 30, ox + 34, y, BROWN[0])


def bb_tv_static(i, n):
    cv = Canvas(BBW, BBH)
    vgrad(cv, 0, 0, BBW, BBH, ["#120a24", "#0c0a1c", "#080612"])
    for x in range(42, BBW, 3):
        cv.vline(x, 0, BBH, "#140e28")
    mode = "static" if i < 3 else "roll" if i == 3 else "logo"

    def screen(c, mask):
        rng = random.Random(i * 13 + 5)
        xs = [x for x, _ in mask]
        ys = [y for _, y in mask]
        x0, x1, y0, y1 = min(xs), max(xs) + 1, min(ys), max(ys) + 1
        if mode == "static":
            for x, y in mask:
                v = rng.random()
                c.set(x, y, (int(v * 230),) * 3 + (255,))
            band = y0 + (i * 6) % (y1 - y0)
            for x, y in mask:
                if band <= y < band + 3:
                    c.set(x, y, mix(c.get(x, y), WHITE, 0.4))
            return
        bars = ["#e8e8e8", "#e8d020", "#20d0d8", "#20c040", "#d030c0", "#d03020", "#2040d0"]
        roll = 8 if mode == "roll" else 0
        for x, y in mask:
            yy = y0 + (y - y0 + roll) % (y1 - y0)
            c.set(x, y, bars[min(6, (x - x0) * 7 // (x1 - x0))] if yy < y1 - 5 else
                  ("#2040d0" if x < (x0 + x1) // 2 else "#101010"))
        if mode == "roll":
            for x, y in mask:
                if y0 + 2 <= y < y0 + 5:
                    c.set(x, y, "#0a0a0a")
        cxm, cym = (x0 + x1) / 2, (y0 + y1) / 2 - 2
        disc = m_ellipse(cxm, cym + (8 if mode == "roll" else 0), 6, 6)
        disc &= mask
        c.fill(disc, "#101018")
        c.fill(edge(disc), WHITE)
        if mode == "logo":
            draw_text_c(c, F5, int(cxm), int(cym) - 3, "7", WHITE if i % 3 else "#ffd6e4")

    tv_set(cv, 1, 1, screen)
    if mode == "logo":
        draw_text(cv, BOLD, 41, 3, "FRONTIER", bands((3, WHITE), (3, "#ffd6e4"), (3, "#ff9ab8")), outline="#080612",
                  shadow="#86163e")
        for k, c in enumerate(("#ffb030", "#ec5e8a", "#26c8d6")):
            cv.hline(41, 93, 15 + k, c)
        draw_text(cv, F3, 41, 20, "COLONY TV", "#8ae0ff")
        draw_text(cv, F3, 41, 26, "CHANNEL 7", "#ffb030")
        seal = m_ellipse(86.5, 24.5, 5.5, 5.5)
        paint(cv, seal, CAN_RED, f_sphere(85, 22, 7, len(CAN_RED), 0.3), CAN_RED[0])
        draw_text_c(cv, F5, 87, 21, "7", WHITE, shadow=CAN_RED[0])
    else:
        draw_text(cv, F3, 46, 13, "TUNING IN", "#5a5a7a" if i % 2 else "#9a9ac0")
        for k in range(3):
            if k <= i:
                cv.set(84 + k * 3, 17, "#9a9ac0")
    frame(cv, "#000000", "#2a1a4a", None)
    return cv


def bb_coffee_steam(i, n):
    cv = Canvas(BBW, BBH)
    vgrad(cv, 0, 0, BBW, BBH, ["#3a1d0e", "#2a140a", "#1e0f08"])
    radial(cv, 20, 18, 20, ["#8a4a22", "#6a3618", "#4a2410", "#2a140a"], 0, 0, 42, BBH)
    for y in range(BBH):
        for x in range(0, 42):
            if math.hypot(x - 20, y - 18) > 19:
                cv.set(x, y, pick(ramp("#3a1d0e", "#2a140a", "#1e0f08"), y / (BBH - 1) * 2, x, y, 4))
    halftone(cv, 40, 0, BBW, BBH, rgba("#4a2410"), lambda x, y: 0.5 if (x // 4 + y // 4) % 3 else 0.2)
    period = 2 * math.pi / 0.75
    coffee_mug(cv, 8, 1, i * period / n)
    draw_text(cv, BOLD, 40, 3, "RED DUST", bands((3, "#fff0d8"), (3, "#ffc888"), (3, "#e8823a")),
              outline="#140804", shadow="#8e1a14", italic=5)
    draw_text(cv, F3, 41, 15, "MORNING CUP", "#d8b898")
    tag = m_rrect(41, 21, 72, 31, 2)
    paint(cv, tag, CREAM, lambda x, y: 2.6, "#3a2a14")
    draw_text_c(cv, F5, 57, 23, "$3.50", "#8e1a14")
    if i % 4 < 2:
        draw_text(cv, F3, 76, 24, "HOT", "#ffb030")
    else:
        draw_text(cv, F3, 76, 24, "HOT", "#7a3a10")
    frame(cv, "#0e0604", "#5e3016", None)
    return cv


BILLBOARDS = [
    ("joe_tray", bb_joe_tray, 8, 0.1),
    ("cryo_wake", bb_cryo_wake, 10, [0.9, 0.3, 0.12, 0.12, 0.12, 0.2, 0.9, 0.15, 0.15, 0.2]),
    ("wy_logo_spin", bb_wy_spin, 12, 0.09),
    ("aspen_pour", bb_aspen_pour, 10, [0.5, 0.12, 0.15, 0.15, 0.15, 0.15, 0.12, 0.3, 0.4, 0.6]),
    ("price_ticker", bb_price_ticker, 12, 0.12),
    ("atmo_puff", bb_atmo_puff, 8, 0.14),
    ("dropship_gateway", bb_dropship_gateway, 10, [0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.8, 0.8]),
    ("tv_static", bb_tv_static, 10, [0.09, 0.09, 0.09, 0.12, 0.5, 0.25, 0.25, 0.25, 0.25, 0.6]),
    ("coffee_steam", bb_coffee_steam, 8, 0.13),
]


# -- Badges --------------------------------------------------------------------------------------------


def badge_starburst(i, n):
    cv = Canvas(32, 32)
    turn = i * (2 * math.pi / 14) / n
    pulse = 0.7 * math.sin(i * 2 * math.pi / n)
    m = m_burst(16, 16, 15 + pulse, 11.5 + pulse, 14, turn)
    cv.fill(shift(m, 1, 1), (0, 0, 0, 120))
    paint(cv, m, ramp("#7a0808", "#c01010", "#e83020", "#ff6a3a", "#ffaa7a"), f_sphere(13, 12, 18, 5, 0.3),
          "#3a0404")
    inner = m_ellipse(16, 16, 10, 10)
    paint(cv, inner, YELLOW, f_sphere(13, 12, 14, len(YELLOW), 0.45), YELLOW[2])
    draw_text_c(cv, F5, 16, 11, "NEW", "#a00808", shadow=YELLOW[2])
    cv.hline(10, 21, 20, "#a00808")
    if i in (0, 4):
        blink(cv, 6 if i == 0 else 25, 6 if i == 0 else 25, YELLOW[4], big=True)
    return cv


def badge_arrow(i, n):
    cv = Canvas(32, 32)
    off = [0, 1, 3, 5, 6, 5, 3, 1][i]
    pts = [(2, 12), (14, 12), (14, 6), (26, 16), (14, 26), (14, 20), (2, 20)]
    m = m_poly([(x + off - 1, y) for x, y in pts])
    side = shift(m, 1, 2)
    cv.fill(side - m, "#5a0606")
    paint(cv, m, ramp("#7a0808", "#b81010", "#e02a1a", "#ff5a3a", "#ff9a7a"),
          f_lin(0, 6, 0, 26, 4.2, 1.2), "#2a0202")
    for x in range(3 + off, 14 + off):
        cv.set(x, 13, "#ffb89a")
    if off >= 3:
        for k, y in enumerate((9, 23)):
            cv.hline(1, off - 1, y + k, "#ffd27a")
    return cv


def badge_beacon(i, n):
    cv = Canvas(32, 32)
    a = i * 2 * math.pi / n
    sx = math.sin(a)
    front = math.cos(a)
    # Beams.
    if front > 0.3:
        for k in range(8):
            ang = k * math.pi / 8 + math.pi
            for r in range(13, 16):
                x, y = 16 + math.cos(ang) * r, 15 + math.sin(ang) * r * 0.9
                cv.set(int(x), int(y), "#ffd060" if r < 15 else "#ff8a20")
    elif abs(sx) > 0.6:
        side = 1 if sx > 0 else -1
        for x in range(0, 15):
            spread = x // 3
            for y in range(13 - spread, 16 + spread):
                if (x + y) % 2 == 0:
                    cv.set(16 + side * (x + 8), y, (255, 176, 48, 150 - x * 9))
    base = m_rrect(6, 23, 26, 30, 2)
    paint(cv, base, METAL, f_hcyl(23, 30, len(METAL), 0.3, 0.6), METAL[0])
    cv.hline(8, 23, 27, METAL[1])
    dome = m_ellipse(16, 23, 9, 13) - m_rect(0, 23, 32, 32)
    amber = ramp("#5a1a02", "#a83a04", "#e86a0a", "#ff9a2a", "#ffd27a", "#fff6d0")

    def dome_f(x, y):
        base_v = f_sphere(13, 14, 11, len(amber), 0.2)(x, y) * 0.6
        hot = math.exp(-((x + 0.5 - (16 + sx * 6)) / 3.2) ** 2) * max(0.0, front * 0.5 + 0.6)
        return base_v + hot * 4.0
    paint(cv, dome, amber, dome_f, "#3a1002")
    cv.set(12, 13, amber[5])
    cv.set(12, 14, amber[4])
    cv.hline(7, 24, 22, "#3a1002")
    return cv


def badge_vitals(i, n):
    cv = Canvas(32, 32)
    bez = m_rrect(1, 4, 31, 28, 3)
    paint(cv, bez, SHELL, f_lin(1, 4, 20, 28, 3.6, 1.6), SHELL_OL)
    scr = m_rect(4, 7, 28, 23)
    cv.fill(scr, "#021208")
    for x in range(4, 28, 4):
        cv.vline(x, 7, 22, "#062a12")
    trace = [0, 0, 0, 0, 1, 0, -1, -7, 5, 0, 0, 0, -2, -1, 0, 0]
    head = 4 + int(i * 24 / n)
    for x in range(4, 28):
        if head <= x < head + 3:
            continue
        y = 16 + trace[(x - 4) % len(trace)]
        age = (head - x) % 24
        c = GREEN_P[5] if age < 2 else GREEN_P[4] if age < 8 else GREEN_P[3] if age < 16 else GREEN_P[2]
        cv.set(x, y, c)
        prev = 16 + trace[(x - 5) % len(trace)]
        if abs(prev - y) > 1 and x > 4:
            cv.vline(x, min(prev, y), max(prev, y), c)
    beat = (i % (n // 2)) == 1
    heart = [".#.#.", "#####", "#####", ".###.", "..#.."] if beat else [".....", ".#.#.", ".###.", "..#..", "....."]
    bitmap(cv, 22, 8, heart, {"#": "#ff3a4a"})
    draw_text(cv, F3, 5, 8, "72", GREEN_P[4])
    scanlines(cv, 4, 7, 28, 23, 0.75)
    cv.set(26, 25, "#5aff8a" if i % 2 == 0 else "#1a5a2a")
    cv.hline(6, 12, 25, SHELL[2])
    return cv


def badge_wycoin(i, n):
    cv = Canvas(32, 32)
    a = i * 2 * math.pi / n
    c = math.cos(a)
    rx = max(1.0, 12 * abs(c))
    thick = 2.5 * abs(math.sin(a)) + 0.6
    edge_dir = 1 if math.sin(a) >= 0 else -1
    cx = 16 - edge_dir * thick * 0.5
    rim = m_ellipse(16 + edge_dir * thick * 0.5, 16, rx + thick * 0.5, 12)
    paint(cv, rim, GOLD, lambda x, y: 1.3 + ((x + y) % 2) * 0.3, GOLD[0])
    if rx > 1.5:
        face = m_ellipse(cx, 16, rx, 12)
        paint(cv, face, GOLD, f_sphere(12, 10, 22, len(GOLD), 0.45, 1.5), GOLD[0], soft=GOLD[2])
        cv.fill(edge(m_ellipse(cx, 16, max(0.6, rx - 2.2), 9.8)), GOLD[2])
        if rx > 4:
            # The emblem squashed by the turn; mirrored on the back.
            emb = WY_MARK
            ew, eh = len(emb[0]), len(emb)
            sx = rx / 12
            for py in range(eh):
                for qx in range(int(-ew * sx / 2) - 1, int(ew * sx / 2) + 2):
                    u = int(math.floor((qx + ew * sx / 2) / sx))
                    if c < 0:
                        u = ew - 1 - u
                    if 0 <= u < ew and emb[py][u] == "#":
                        X = int(round(cx + qx))
                        cv.set(X, 13 + py, GOLD[0])
                        cv.set(X, 12 + py, GOLD[5] if py == 0 else GOLD[1])
    else:
        paint(cv, m_rect(15, 4, 18, 28), GOLD, lambda x, y: 2.5 + (x - 15) * -0.6, GOLD[0])
    if i in (1, 5):
        blink(cv, 9 if i == 1 else 21, 8, GOLD[4], big=True)
    return cv


def badge_seegeye(i, n):
    """Seegson camera eye: looks around, then blinks its shutter lids."""
    cv = Canvas(32, 32)
    look = [0, -2, -2, 0, 2, 2, 0, 0][i]
    lid = [0, 0, 0, 0, 0, 0, 5, 11][i]
    housing = m_ellipse(16, 16, 15, 11)
    paint(cv, housing, METAL, f_sphere(13, 11, 17, len(METAL), 0.25), METAL[0], soft=METAL[2])
    socket = m_ellipse(16, 16, 12, 8)
    cv.fill(socket, "#05080c")
    iris_c = (16 + look, 16)
    iris = m_ellipse(iris_c[0], iris_c[1], 6, 6) & socket
    blue = ramp("#06183a", "#0c3a7a", "#1a64b8", "#3a9af0", "#9ad8ff", "#ffffff")
    paint(cv, iris, blue, lambda x, y: 4.6 - math.hypot(x + 0.5 - iris_c[0], y + 0.5 - iris_c[1]) * 0.75, None, 2)
    cv.fill(edge(m_ellipse(iris_c[0], iris_c[1], 6, 6)) & socket, blue[1])
    cv.fill(m_ellipse(iris_c[0], iris_c[1], 2, 2), "#020408")
    cv.set(iris_c[0] - 2, 13, WHITE)
    cv.set(iris_c[0] - 1, 13, blue[4])
    # Lids close from above and below.
    if lid:
        top = {(x, y) for x, y in socket if y < 8 + lid}
        bot = {(x, y) for x, y in socket if y >= 24 - lid}
        for m, v in ((top, 3.6), (bot, 2.2)):
            paint(cv, m, METAL, lambda x, y, v=v: v, METAL[0])
        if lid >= 8:
            cv.hline(5, 26, 16, METAL[0])
    cv.fill(edge(socket), METAL[0])
    cv.set(26, 7, "#ff3a2a" if i % 2 == 0 else "#7a1a10")
    return cv


def badge_joewave(i, n):
    cv = Canvas(32, 32)
    a = math.sin(i * 2 * math.pi / n)
    # Small Joe: draw at full size then sample down is too lossy, so a compact bust here.
    cx = 13
    body = m_poly([(1, 32), (2, 26), (6, 23), (20, 23), (24, 26), (25, 32)])
    paint(cv, body, SUIT, f_cyl(1, 25, len(SUIT), 0.3, 0.4), SUIT_OL)
    cv.fill(m_poly([(cx - 3, 23), (cx, 27), (cx + 3, 23)]), "#20242a")
    cv.rect(16, 27, 19, 30, WHITE)
    cv.set(17, 28, "#2f6db0")
    neck = m_rect(cx - 2, 17, cx + 2, 24)
    paint(cv, neck, SKIN, f_cyl(cx - 2, cx + 2, len(SKIN), 0.3, 0.5), SKIN_OL)
    head = m_ellipse(cx, 11, 6, 7.5)
    paint(cv, head, SKIN, f_sphere(cx - 1.5, 9, 8, len(SKIN), 0.12), SKIN_OL, soft=SKIN[1])
    for sx in (cx - 3, cx + 2):
        cv.hline(sx, sx + 1, 11, SKIN[0])
        cv.set(sx, 11, GLOW[2])
        cv.set(sx + 1, 11, GLOW[3])
    cv.hline(cx - 1, cx + 1, 15, SKIN_OL)
    # Raised arm waving from the elbow.
    sh = (21.5, 25)
    el = (25.5, 20)
    ang = math.radians(-90 + a * 30)
    hand = (el[0] + math.cos(ang) * 7, el[1] + math.sin(ang) * 7)
    paint(cv, m_capsule(sh[0], sh[1], el[0], el[1], 2.2), SUIT, lambda x, y: 2.6, SUIT_OL)
    paint(cv, m_capsule(el[0], el[1], hand[0], hand[1], 1.8), SUIT, lambda x, y: 3.4, SUIT_OL)
    # Open palm with three fingers and a thumb, turned with the forearm.
    ux, uy = math.cos(ang), math.sin(ang)
    px, py = -uy, ux
    palm = m_ellipse(hand[0] + ux * 1.5, hand[1] + uy * 1.5, 2.3, 2.3)
    fingers = set()
    for k in (-1.2, 0, 1.2):
        fingers |= m_capsule(hand[0] + ux * 2.5 + px * k, hand[1] + uy * 2.5 + py * k,
                             hand[0] + ux * 5 + px * k * 1.2, hand[1] + uy * 5 + py * k * 1.2, 0.6)
    fingers |= m_capsule(hand[0] + ux * 1.5 - px * 2, hand[1] + uy * 1.5 - py * 2,
                         hand[0] + ux * 2.5 - px * 3.5, hand[1] + uy * 2.5 - py * 3.5, 0.6)
    paint(cv, palm | fingers, SKIN, f_sphere(hand[0] - 1, hand[1] - 2, 5, len(SKIN), 0.3), SKIN_OL)
    if abs(a) > 0.7:
        side = 1 if a > 0 else -1
        for k in range(3):
            cv.set(int(hand[0] + ux * 3 + side * (5 + k)), int(hand[1] + uy * 3 - 2 + k * 2), "#9cc8f0")
    return cv


def badge_cryopod(i, n):
    """A hypersleep chamber whose canopy lifts, lets out a puff of cold mist and closes again."""
    cv = Canvas(32, 32)
    lid = [0, 0, 10, 22, 30, 30, 20, 8][i]
    cryo_pod(cv, 1, 8, lid=lid, sleeper=True, L=30)
    if lid >= 20:
        rng = random.Random(i)
        for _ in range(16):
            x = rng.randrange(6, 26)
            y = rng.randrange(1, 9)
            cv.set(x, y, (210, 244, 255, 120 + rng.randrange(100)))
    if lid == 0 and i == 0:
        blink(cv, 22, 4, GLASS[3])
    return cv


def badge_crt(i, n):
    cv = Canvas(32, 32)
    # A status feed scrolling up the screen; it holds three characters and the cursor.
    feed = ["SYS", "AIR", "PWR", "O2", "DCK", "OK"]
    lines = (feed[i % len(feed)], feed[(i + 1) % len(feed)])
    t = Canvas(32, 30)
    crt_terminal(t, 0, 0, lines=lines, cursor=(i % 2 == 0), phase=i % 2, w=29)
    cv.paste(t, 0, 1)
    return cv


def badge_pricetag(i, n):
    cv = Canvas(32, 32)
    ang = [0, 5, 9, 5, 0, -5, -9, -5][i]
    tag = Canvas(32, 32)
    price_tag(tag, 7, 11, 22, 14, ramp("#8a6a10", "#c49a1a", "#f0c83a", "#ffe68a", "#fff8d0"), "#3a2a04",
              [(F5, "$5", "#a01010", 10, 4)])
    # Rotate about the hole, then draw the string to the pin.
    rot = tag.img.rotate(ang, resample=Image.NEAREST, center=(11.5, 18), translate=(0, 0))
    out = Canvas(32, 32)
    out.img = rot
    out.px = rot.load()
    cv.line(6, 3, 11, 17, rgba("#e8e0c8"))
    cv.paste(out, 0, 0)
    cv.fill(m_ellipse(6, 3, 1.5, 1.5), METAL[4])
    return cv


def ration_tin_tex(tw):
    """The printed wrap of a W-Y ration tin, laid out flat: tw wide, rows match the badge."""
    tex = Canvas(tw, 32, KHAKI[3])
    for y in (10, 24):
        tex.hline(0, tw - 1, y, KHAKI[1])
        tex.hline(0, tw - 1, y + 1, KHAKI[4])
    tex.rect(0, 20, tw, 23, "#a01810")
    for x in range(1, tw, 3):
        tex.set(x, 21, "#ffd040")
    tex.rect(3, 12, 22, 19, "#e8dfb0")
    tex.hline(3, 21, 18, "#a89a60")
    draw_text(tex, F3, 6, 13, "MRE", "#1a1a12")
    bitmap(tex, 27, 13, WY_MARK_S, {"#": KHAKI[1]})
    tex.hline(26, 36, 19, KHAKI[2])
    return tex


def badge_ration_tin(i, n):
    cv = Canvas(32, 32)
    tw = 56
    tex = ration_tin_tex(tw)
    cx, r = 16, 10
    cv.fill(m_ellipse(17, 28.5, 12, 1.6), (0, 0, 0, 110))
    body = m_rect(6, 9, 26, 27) | m_ellipse(cx, 27, r, 2.4)
    off = i * tw / n
    for x, y in body:
        u = max(-1.0, min(1.0, (x + 0.5 - cx) / r))
        th = math.asin(u)
        tx = int((th / (2 * math.pi) * tw + off) % tw)
        c = tex.get(tx, min(y, 26))
        k = 0.55 + 0.6 * max(0.0, math.cos(th + 0.55))
        if -0.75 < th < -0.45:
            k += 0.25
        cv.set(x, y, (min(255, int(c[0] * k)), min(255, int(c[1] * k)), min(255, int(c[2] * k)), 255))
    for x, y in edge(body):
        cv.set(x, y, "#16140a")
    rim = m_ellipse(cx, 27, r, 2.4) - m_ellipse(cx, 26.4, r - 1, 1.8)
    for x, y in rim:
        if y >= 27:
            cv.set(x, y, METAL[3] if x < cx else METAL[2])
    lid = m_ellipse(cx, 9, r, 2.8)
    paint(cv, lid, METAL, f_cyl(6, 26, len(METAL), 0.3, 1.5), METAL[1])
    cv.fill(edge(m_ellipse(cx, 9, r - 2, 1.4)), METAL[2])
    a = i * 2 * math.pi / n
    tx, ty = cx + math.cos(a) * 5.5, 9 + math.sin(a) * 1.3
    cv.set(int(tx), int(ty), METAL[5])
    cv.set(int(tx) + 1, int(ty), METAL[4])
    if i == 0:
        blink(cv, 9, 4, WHITE)
    return cv


def badge_neon_open(i, n):
    cv = Canvas(32, 32)
    state = [1, 1, 1, 1, 0, 1, 2, 1][i]
    cv.line(6, 0, 6, 7, METAL[2])
    cv.line(25, 0, 25, 7, METAL[2])
    board = m_rrect(0, 7, 32, 26, 2)
    paint(cv, board, BLACKP, f_lin(0, 7, 0, 26, 3.2, 1.2), "#000000", dither=4)
    tube = edge(m_rrect(2, 9, 30, 24, 2))
    for x, y in halo(tube):
        if (x, y) in board:
            cv.set(x, y, mix(cv.get(x, y), "#0aa0c0", 0.35))
    cv.fill(tube, "#5af0ff")
    letters = shift(text_mask(F5, "OPEN"), 5, 13)
    lit = letters if state == 1 else ({p for p in letters if p[0] < 16} if state == 2 else set())
    dark = letters - lit
    for x, y in halo(lit) | halo(halo(lit)):
        if (x, y) in board and (x, y) not in letters and (x, y) not in tube:
            near = any((x + dx, y + dy) in lit for dx, dy in N8)
            cv.set(x, y, mix(cv.get(x, y), "#ff2a7a", 0.42 if near else 0.16))
    cv.fill(dark, "#5a1a32")
    for x, y in lit:
        cv.set(x, y, "#ffd6ea" if (x + y) % 3 else "#ff5aa0")
    return cv


def badge_motion_tracker(i, n):
    cv = Canvas(32, 32)
    body = m_rrect(2, 2, 30, 30, 3)
    cv.fill(shift(body, 1, 1), (0, 0, 0, 110))
    paint(cv, body, METAL, f_lin(2, 2, 20, 30, 3.8, 1.2), METAL[0], soft=METAL[3])
    scr = m_rrect(5, 5, 27, 22, 2)
    cv.fill(scr, "#021208")
    ox, oy = 16, 22
    for x, y in scr:
        d = math.hypot(x + 0.5 - ox, y + 0.5 - oy)
        ang = math.degrees(math.atan2(y + 0.5 - oy, x + 0.5 - ox))
        if not (-150 <= ang <= -30):
            continue
        if any(abs(d - rr) < 0.5 for rr in (6, 11, 16)):
            cv.set(x, y, "#0a3a18")
        R = 2 + i * 2.6
        if abs(d - R) < 0.7:
            cv.set(x, y, GREEN_P[5])
        elif 0.7 <= R - d < 2.2:
            cv.set(x, y, GREEN_P[2])
    for ang in (-150, -30):
        for k in range(17):
            x = ox + math.cos(math.radians(ang)) * k
            y = oy + math.sin(math.radians(ang)) * k
            if (int(x), int(y)) in scr:
                cv.set(int(x), int(y), "#0a3a18")
    R = 2 + i * 2.6
    for bx, by in ((11, 10), (21, 13)):
        d = math.hypot(bx + 0.5 - ox, by + 0.5 - oy)
        age = R - d
        if age >= 0:
            c = GREEN_P[5] if age < 3 else GREEN_P[4] if age < 7 else GREEN_P[3]
            cv.fill(m_rect(bx, by, bx + 2, by + 2), c)
            if age < 3:
                for dx, dy in N4:
                    cv.set(bx + (dx > 0) + dx, by + (dy > 0) + dy, GREEN_P[3])
    cv.set(ox, oy - 1, GREEN_P[4])
    scanlines(cv, 5, 5, 27, 22, 0.78)
    dist = ["--", "21", "18", "16", "14", "12", "11", "09"][i]
    draw_text(cv, F3, 6, 24, dist + "M", GREEN_P[4])
    cv.set(25, 25, "#ff3a2a" if i % 2 else "#6a1a10")
    cv.hline(19, 23, 26, METAL[1])
    return cv


def badge_coffee_steam(i, n):
    cv = Canvas(32, 32)
    cv.fill(m_ellipse(14.5, 30, 12, 1.5), (0, 0, 0, 110))
    coffee_mug(cv, 4, 1, i * (2 * math.pi / 0.75) / n)
    return cv


def badge_price_drop(i, n):
    cv = Canvas(32, 32)
    bounce = [0, 2, 4, 6, 7, 6, 4, 2][i]
    squash = 1 if bounce == 7 else 0
    sh = m_ellipse(16, 30, 6 + bounce * 0.7, 1.2)
    cv.fill(sh, (0, 0, 0, 70 + bounce * 12))
    oy = bounce - 1
    pts = [(9 - squash, oy + 1), (23 + squash, oy + 1), (23 + squash, oy + 12), (29 + squash, oy + 12),
           (16, oy + 23 - squash), (3 - squash, oy + 12), (9 - squash, oy + 12)]
    m = m_poly(pts)
    side = shift(m, 1, 1)
    cv.fill(side - m, "#063a14")
    paint(cv, m, GREEN_P, f_lin(0, oy, 0, oy + 22, 4.6, 1.8), "#031a0a")
    cv.vline(10 - squash, oy + 2, oy + 12, GREEN_P[5])
    draw_text_c(cv, F3, 16, oy + 4, "LOW", WHITE, shadow="#0a3a18")
    if bounce == 0:
        for x, y in ((5, 4), (27, 6)):
            blink(cv, x, y, "#c8ffd2")
    return cv


def uscm_flag_tex():
    tex = Canvas(25, 17, "#16305a")
    for y in range(17):
        for x in range(25):
            if (x + y) % 5 == 0:
                tex.set(x, y, "#1c3a6a")
    tex.rect(0, 13, 25, 15, "#b81c1c")
    tex.rect(0, 15, 25, 16, "#f0e8d8")
    tex.rect(0, 16, 25, 17, "#b81c1c")
    for sgn in (-1, 1):
        for k in range(3):
            x0 = 12 + sgn * 4
            x1 = 12 + sgn * (10 - k * 2)
            tex.hline(x0, x1, 4 + k * 2, "#ffd27a" if k == 0 else "#c49a1a")
    tex.fill(m_ellipse(12.5, 6.5, 4, 4), "#c49a1a")
    tex.fill(m_ellipse(12.5, 6.5, 3, 3), "#0c1a34")
    tex.fill(m_burst(12.5, 7, 2.8, 1.2, 5, -math.pi / 2), WHITE)
    return tex


def badge_uscm_flag(i, n):
    cv = Canvas(32, 32)
    tex = uscm_flag_tex()
    ph = i * 2 * math.pi / n
    cv.fill(m_ellipse(5, 30.5, 4, 1), (0, 0, 0, 110))
    for u in range(25):
        amp = 1.6 * (u / 24) ** 0.8
        dy = math.sin(u * 0.38 - ph) * amp
        k = 1 + math.cos(u * 0.38 - ph) * 0.28 * (0.3 + u / 24)
        for v in range(17):
            c = tex.get(u, v)
            y = int(round(5 + v + dy))
            cv.set(6 + u, y, (min(255, int(c[0] * k)), min(255, int(c[1] * k)), min(255, int(c[2] * k)), 255))
        top = int(round(5 + dy))
        cv.set(6 + u, top - 1, "#06101e")
        cv.set(6 + u, top + 17, "#06101e")
    tail = [int(round(5 + v + math.sin(24 * 0.38 - ph) * 1.6)) for v in (0, 16)]
    cv.vline(31, tail[0], tail[1], "#06101e")
    pole = m_rect(3, 3, 6, 31)
    paint(cv, pole, METAL, f_cyl(3, 6, len(METAL), 0.3, 1.0), METAL[0])
    paint(cv, m_ellipse(4.5, 2.5, 2, 2), GOLD, f_sphere(4, 2, 2.5, len(GOLD), 0.3), GOLD[0])
    return cv


def badge_sale_sign(i, n):
    cv = Canvas(32, 32)
    board = m_rrect(1, 6, 31, 26, 2)
    cv.fill(shift(board, 1, 1), (0, 0, 0, 120))
    paint(cv, board, CAN_RED, f_lin(1, 6, 1, 26, 3.6, 1.6), "#2a0404")
    inner = m_rrect(4, 9, 28, 23, 1)
    cv.fill(inner, "#5a0808")
    cv.fill(edge(inner), "#2a0404")
    bulbs = [(x, 7) for x in range(3, 30, 3)] + [(29, y) for y in range(10, 24, 3)] + \
            [(x, 24) for x in range(29, 2, -3)] + [(2, y) for y in range(22, 8, -3)]
    for k, (x, y) in enumerate(bulbs):
        if (k + i) % 3 == 0:
            cv.set(x, y, "#fff6c0")
            for dx, dy in N4:
                if (x + dx, y + dy) in board - inner:
                    cv.set(x + dx, y + dy, "#ffb030")
        else:
            cv.set(x, y, "#8a5a10")
    col = [WHITE, "#ffe468"][i % 2]
    draw_text_c(cv, F5, 16, 13, "SALE", col, shadow="#2a0404")
    return cv


BADGES = [
    ("starburst", badge_starburst, 8, 0.12),
    ("arrow", badge_arrow, 8, 0.09),
    ("beacon", badge_beacon, 8, 0.1),
    ("vitals", badge_vitals, 8, 0.12),
    ("wycoin", badge_wycoin, 8, 0.1),
    ("seegeye", badge_seegeye, 8, [0.6, 0.4, 0.4, 0.3, 0.4, 0.4, 0.06, 0.12]),
    ("joewave", badge_joewave, 6, 0.12),
    ("cryopod", badge_cryopod, 8, [0.8, 0.2, 0.12, 0.12, 0.12, 0.8, 0.15, 0.15]),
    ("terminal", badge_crt, 6, 0.4),
    ("pricetag", badge_pricetag, 8, 0.12),
    ("ration_tin", badge_ration_tin, 8, 0.12),
    ("neon_open", badge_neon_open, 8, [0.5, 0.4, 0.3, 0.6, 0.06, 0.1, 0.08, 0.7]),
    ("motion_tracker", badge_motion_tracker, 8, 0.12),
    ("coffee_steam", badge_coffee_steam, 8, 0.14),
    ("price_drop", badge_price_drop, 8, [0.14, 0.08, 0.07, 0.06, 0.12, 0.06, 0.07, 0.08]),
    ("uscm_flag", badge_uscm_flag, 8, 0.11),
    ("sale_sign", badge_sale_sign, 6, 0.16),
]


# -- Popup icons -------------------------------------------------------------------------------------------


def icon_warning():
    cv = Canvas(24, 24)
    tri = m_poly([(12, 1), (23, 22), (1, 22)])
    cv.fill(shift(tri, 1, 1), (0, 0, 0, 110))
    paint(cv, tri, YELLOW, f_lin(6, 4, 18, 22, 4.6, 2.4), "#2a1e02", soft=YELLOW[2])
    inner = m_poly([(12, 5), (20, 20), (4, 20)])
    for x, y in edge(inner):
        cv.set(x, y, YELLOW[2])
    cv.fill(m_rect(11, 9, 13, 16), "#16120a")
    cv.fill(m_rect(11, 17, 13, 19), "#16120a")
    cv.set(11, 9, "#3a3020")
    return cv


def icon_envelope():
    cv = Canvas(24, 24)
    env = m_rect(1, 5, 23, 20)
    cv.fill(shift(env, 1, 1), (0, 0, 0, 110))
    paper = ramp("#8a8068", "#bdb294", "#e0d8bc", "#f6f0dc")
    paint(cv, env, paper, f_lin(1, 5, 23, 20, 3, 1.6), "#4a4230")
    flap = m_poly([(1.5, 5.5), (22.5, 5.5), (12, 14)])
    paint(cv, flap, paper, f_lin(1, 5, 1, 14, 3.4, 2.2), None)
    for x, y in edge(flap):
        if y > 5:
            cv.set(x, y, "#8a8068")
    cv.line(2, 19, 9, 13, "#a89c7c")
    cv.line(21, 19, 15, 13, "#a89c7c")
    seal = m_ellipse(12, 13.5, 3, 3)
    paint(cv, seal, CAN_RED, f_sphere(11, 12.5, 3.5, len(CAN_RED)), CAN_RED[0])
    bitmap(cv, 10, 13, ["#.#", ".#."], {"#": CAN_RED[5]})
    return cv


def icon_seegson():
    cv = Canvas(24, 24)
    disc = m_ellipse(12, 12, 11, 11)
    cv.fill(shift(disc, 1, 1), (0, 0, 0, 110))
    paint(cv, disc, ramp("#06183a", "#0c2c5a", "#1c4a80", "#2f6db0", "#5a9ad8", "#a8d4ff"),
          f_sphere(9, 8, 14, 6, 0.3), "#040c1c", soft="#1c4a80")
    cv.fill(edge(m_ellipse(12, 12, 9, 9)), "#5a9ad8")
    bitmap(cv, 8, 8, SEEG_MARK, {"#": WHITE})
    cv.set(7, 5, "#d0ecff")
    cv.set(8, 4, "#d0ecff")
    return cv


def icon_wy():
    cv = Canvas(24, 24)
    plate = m_rrect(0, 3, 24, 21, 3)
    cv.fill(shift(plate, 1, 1), (0, 0, 0, 110))
    paint(cv, plate, ramp("#0a0a0e", "#16161c", "#24242c", "#34343e"), f_lin(0, 3, 0, 21, 3, 0.5), "#000000",
          soft="#4a4a56")
    for py, row in enumerate(WY_MARK):
        for px, ch in enumerate(row):
            if ch == "#":
                x, y = 6 + px, 7 + py
                cv.set(x + 1, y + 1, GOLD[0])
    for py, row in enumerate(WY_MARK):
        for px, ch in enumerate(row):
            if ch == "#":
                cv.set(5 + px, 6 + py, GOLD[4] if py < 2 else GOLD[3] if py < 5 else GOLD[2])
    draw_text_c(cv, F3, 12, 15, "W-Y", GOLD[3])
    return cv


def icon_synthetic():
    cv = Canvas(24, 24)
    head = m_ellipse(12, 11, 8, 10)
    neck = m_rect(9, 19, 15, 24)
    cv.fill(shift(head | neck, 1, 1), (0, 0, 0, 110))
    paint(cv, neck, SKIN, f_cyl(9, 15, len(SKIN), 0.3, 0.5), SKIN_OL)
    cv.hline(10, 13, 21, SKIN[1])
    paint(cv, head, SKIN, f_sphere(10, 8, 11, len(SKIN), 0.12), SKIN_OL, soft=SKIN[1])
    cv.hline(6, 9, 8, SKIN[4])
    cv.hline(14, 17, 8, SKIN[3])
    for sx in (8.5, 15.5):
        cv.fill(m_ellipse(sx, 10.5, 2.5, 1.5), SKIN[0])
        cv.set(int(sx) - 1, 10, GLOW[2])
        cv.set(int(sx), 10, GLOW[3])
        cv.set(int(sx) + 1, 10, GLOW[1])
    cv.set(11, 13, SKIN[2])
    cv.set(12, 13, SKIN[1])
    cv.hline(10, 13, 16, SKIN_OL)
    cv.hline(11, 12, 17, SKIN[4])
    cv.vline(5, 10, 14, SKIN[2])
    cv.vline(18, 10, 14, SKIN[1])
    return cv


def icon_cryopod():
    cv = Canvas(24, 24)
    # Head-on view of a hypersleep chamber with the canopy closed.
    shell = m_rrect(2, 4, 22, 22, 7)
    cv.fill(shift(shell, 1, 1), (0, 0, 0, 110))
    paint(cv, shell, SHELL, f_sphere(9, 8, 16, len(SHELL), 0.3), SHELL_OL)
    glass = m_rrect(5, 6, 19, 17, 5)
    paint(cv, glass, GLASS, f_lin(5, 6, 14, 17, 4.2, 1.2), GLASS[0])
    cv.fill(m_ellipse(12, 10, 2.5, 2.5), GLASS[1])
    cv.fill(m_rrect(8, 12, 16, 17, 2) & glass - edge(glass), GLASS[1])
    for k in range(3):
        cv.set(7 + k, 8 - (1 if k == 2 else 0) + (1 if k == 0 else 0), GLASS[4])
    cv.hline(6, 17, 19, SHELL[2])
    cv.set(9, 20, "#5aff8a")
    cv.set(14, 20, "#ffb030")
    return cv


def icon_beer_can():
    cv = Canvas(24, 24)
    body = m_rect(7, 4, 17, 21) | m_ellipse(12, 21, 5, 1.4)
    cv.fill(shift(body, 1, 1), (0, 0, 0, 110))
    paint(cv, body, CAN_RED, f_cyl(7, 17, len(CAN_RED), 0.3, 0.4), CAN_RED[0])
    gfn = f_cyl(7, 17, len(GOLD), 0.3, 0.6)
    for gy in (7, 17):
        for x in range(8, 16):
            cv.set(x, gy, pick(GOLD, gfn(x, gy), x, gy))
    mtn = m_poly([(8.5, 15.5), (12, 9.5), (15.5, 15.5)])
    for x, y in mtn:
        cv.set(x, y, "#e8f0ff" if x < 12 else "#9ab0d0")
    cv.set(12, 10, WHITE)
    rim = m_ellipse(12, 4, 5, 1.6)
    paint(cv, rim, METAL, f_cyl(7, 17, len(METAL), 0.3, 1.5), METAL[1])
    cv.set(13, 4, METAL[5])
    cv.set(9, 11, CAN_RED[5])
    cv.set(9, 12, CAN_RED[1])
    cv.set(15, 19, CAN_RED[4])
    return cv


def icon_mug():
    cv = Canvas(24, 24)
    mug_r = ramp("#2a0606", "#5a0e0c", "#8e1a14", "#c02e22", "#e85a40", "#ffa080")
    handle = m_ellipse(16.5, 14.5, 4, 4.2) - m_ellipse(16.5, 14.5, 2, 2.3)
    body = m_rect(3, 9, 16, 21) | m_ellipse(9.5, 21, 6.5, 1.6)
    cv.fill(shift(body | handle, 1, 1), (0, 0, 0, 110))
    paint(cv, handle, mug_r, f_sphere(15, 12, 6, len(mug_r)), mug_r[0])
    paint(cv, body, mug_r, f_cyl(3, 16, len(mug_r), 0.3, 0.4), mug_r[0])
    rim = m_ellipse(9.5, 9, 6.5, 1.8)
    paint(cv, rim, mug_r, lambda x, y: 4.6, mug_r[0])
    cv.fill(m_ellipse(9.5, 9.2, 5, 1), "#2a1408")
    cv.fill(m_ellipse(9, 15.5, 3.2, 3.2), WHITE)
    paint(cv, m_ellipse(9, 15.5, 2.2, 2.2), ORANGE, f_sphere(8, 14.5, 2.6, len(ORANGE)))
    cv.vline(4, 11, 19, mug_r[5])
    for sx in (7, 11):
        for j, dx in enumerate((0, 1, 1, 0, 0, 1)):
            cv.set(sx + dx, 7 - j, (216, 204, 192, 230) if j < 3 else (168, 152, 138, 180))
    return cv


def icon_uscm():
    cv = Canvas(24, 24)
    for sgn in (-1, 1):
        for k in range(4):
            y = 8 + k * 2
            x0, x1 = 12 + sgn * 6, 12 + sgn * (11 - k * 2)
            cv.hline(x0 + sgn, x1 + sgn, y + 1, (0, 0, 0, 110))
            cv.hline(x0, x1, y, "#ffd27a" if k == 0 else "#c49a1a")
            cv.hline(x0, x1 - sgn, y + 1, "#7a5a08")
    disc = m_ellipse(12, 12, 7, 7)
    cv.fill(shift(disc, 1, 1), (0, 0, 0, 110))
    paint(cv, disc, GOLD, f_sphere(10, 9, 10, len(GOLD), 0.4), GOLD[0])
    cv.fill(m_ellipse(12, 12, 5, 5), "#0c1a34")
    cv.fill(m_burst(12, 12.5, 4.6, 1.9, 5, -math.pi / 2), WHITE)
    return cv


def icon_water_drop():
    cv = Canvas(24, 24)
    drop = m_ellipse(11, 15, 7, 7) | m_poly([(4.2, 14), (11, 1.5), (17.8, 14)])
    cv.fill(shift(drop, 1, 1), (0, 0, 0, 110))
    paint(cv, drop, GLASS, f_sphere(8.5, 11, 11, len(GLASS), 0.25), GLASS[0], soft=GLASS[1])
    cv.vline(7, 12, 16, GLASS[4])
    cv.set(8, 10, GLASS[4])
    cv.set(8, 18, WHITE)
    cross = m_rect(17, 15, 20, 22) | m_rect(15, 17, 22, 20)
    cv.fill(halo(cross) | cross, "#064a1c")
    paint(cv, cross, ramp("#0a5a24", "#128a3a", "#18a048", "#5ad080"), f_lin(15, 15, 22, 22, 3, 1), None)
    return cv


def icon_price_tag():
    cv = Canvas(24, 24)
    cv.line(5, 12, 9, 2, rgba("#e8e0c8"))
    cv.fill(m_ellipse(9.5, 2, 1.5, 1.5), METAL[4])
    tag = m_poly([(1, 13.5), (6, 7), (23, 7), (23, 20), (6, 20)])
    cv.fill(shift(tag, 1, 1), (0, 0, 0, 110))
    price_tag(cv, 1, 7, 22, 13, ramp("#8a6a10", "#c49a1a", "#f0c83a", "#ffe68a", "#fff8d0"), "#3a2a04",
              [(F5, "$5", "#a01010", 9, 3)])
    return cv


def icon_tv():
    cv = Canvas(24, 24)
    cv.line(10, 4, 6, 0, METAL[4])
    cv.line(14, 4, 18, 0, METAL[4])
    cab = m_rrect(1, 4, 23, 21, 3)
    cv.fill(shift(cab, 1, 1), (0, 0, 0, 110))
    paint(cv, cab, BROWN, f_lin(1, 4, 16, 21, 4.6, 2.2), BROWN[0], soft=BROWN[2])
    cv.fill(m_rrect(3, 6, 18, 19, 3), "#14100c")
    scr = m_rrect(4, 7, 17, 18, 3)
    bars = ["#e8e8e8", "#e8d020", "#20d0d8", "#20c040", "#d030c0", "#d03020", "#2040d0"]
    for x, y in scr:
        cv.set(x, y, bars[min(6, (x - 4) * 7 // 13)])
    scanlines(cv, 4, 7, 17, 18, 0.72)
    cv.set(5, 8, WHITE)
    for ky in (9, 14):
        cv.fill(m_ellipse(20.5, ky + 0.5, 1.5, 1.5), METAL[3])
        cv.set(20, ky, METAL[5])
    for lx in (4, 18):
        cv.rect(lx, 21, lx + 2, 23, BROWN[1])
    return cv


def icon_sprout():
    cv = Canvas(24, 24)
    pot = m_poly([(5, 14), (19, 14), (17, 23), (7, 23)])
    cv.fill(shift(pot, 1, 1), (0, 0, 0, 110))
    paint(cv, pot, RUST, f_cyl(5, 19, len(RUST), 0.3, 1.0), RUST[0])
    lip = m_rect(4, 13, 20, 16)
    paint(cv, lip, RUST, f_cyl(4, 20, len(RUST), 0.3, 1.5), RUST[0])
    cv.hline(6, 17, 14, "#3a1d0e")
    cv.vline(12, 6, 13, LEAF[2])
    cv.vline(11, 8, 13, LEAF[3])
    for cx, cy, side in ((8, 6, -1), (16, 4, 1)):
        leaf = m_ellipse(cx, cy, 4, 2.4)
        paint(cv, leaf, LEAF, f_sphere(cx - 1.5, cy - 1.5, 4.5, len(LEAF), 0.3), LEAF[0])
        cv.line(cx - 2 * side, cy + 0.5 * side, 12, 7, LEAF[1])
    return cv


def icon_ration_tin():
    cv = Canvas(24, 24)
    body = m_rect(4, 5, 20, 21) | m_ellipse(12, 21, 8, 1.8)
    cv.fill(shift(body, 1, 1), (0, 0, 0, 110))
    paint(cv, body, KHAKI, f_cyl(4, 20, len(KHAKI), 0.3, 0.6), "#16140a")
    lab = m_rect(5, 9, 19, 15)
    paint(cv, lab, CREAM, f_cyl(4, 20, len(CREAM), 0.3, 1.0))
    draw_text_c(cv, F3, 12, 10, "MRE", "#1a1a12")
    cv.rect(5, 16, 19, 18, "#a01810")
    for x in range(6, 19, 3):
        cv.set(x, 16, "#ffd040")
    lid = m_ellipse(12, 5, 8, 2)
    paint(cv, lid, METAL, f_cyl(4, 20, len(METAL), 0.3, 1.5), METAL[1])
    cv.fill(edge(m_ellipse(12, 5, 6, 1.1)), METAL[2])
    return cv


ICONS = [icon_warning, icon_envelope, icon_seegson, icon_wy, icon_synthetic, icon_cryopod, icon_beer_can, icon_mug,
         icon_uscm, icon_water_drop, icon_price_tag, icon_tv, icon_sprout, icon_ration_tin]


# -- Output ----------------------------------------------------------------------------------------------


def write_rsi(folder, entries, w, h, what):
    """Writes an RSI: one state per entry, its frames left to right in a single row, plus meta.json."""
    rsi = os.path.join(OUT, folder)
    os.makedirs(rsi, exist_ok=True)
    states = []
    for name, fn, n, delay in entries:
        sheet = Image.new("RGBA", (w * n, h))
        for i in range(n):
            cv = fn(i, n)
            assert (cv.w, cv.h) == (w, h), name
            sheet.paste(cv.img, (w * i, 0))
        sheet.save(os.path.join(rsi, f"{name}.png"))
        delays = delay if isinstance(delay, list) else [delay] * n
        assert len(delays) == n, name
        states.append({"name": name, "delays": [delays]})
    meta = {"version": 1, "license": "CC-BY-SA-3.0",
            "copyright": f"{what} drawn for CMU, generated by {GENERATOR}",
            "size": {"x": w, "y": h}, "states": states}
    with open(os.path.join(rsi, "meta.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(meta, f, indent=2)
        f.write("\n")


def write_badges():
    write_rsi("badges.rsi", BADGES, 32, 32, "Ad badges")


def write_billboards():
    write_rsi("billboards.rsi", BILLBOARDS, BBW, BBH, "Animated ad billboards")


def main():
    os.makedirs(OUT, exist_ok=True)
    files = []
    for i, fn in enumerate(BANNERS):
        cv = fn()
        assert (cv.w, cv.h) == (BW, BH), fn.__name__
        name = f"banner_{i + 1:02d}.png"
        cv.save(os.path.join(OUT, name))
        files.append(name)
    icons = []
    for i, fn in enumerate(ICONS):
        name = f"icon_{i + 1:02d}.png"
        fn().save(os.path.join(OUT, name), scale=1)
        icons.append(name)
    write_badges()
    write_billboards()
    with open(os.path.join(OUT, "attributions.yml"), "w", encoding="utf-8", newline="\n") as f:
        for group, what in ((files, "Pixel-art product ad banners"), (icons, "Pixel-art ad popup icons")):
            names = ", ".join(f'"{n}"' for n in group)
            f.write(f"- files: [{names}]\n")
            f.write('  license: "CC-BY-SA-3.0"\n')
            f.write(f'  copyright: "{what} drawn for CMU, generated by {GENERATOR}"\n')
            f.write(f'  source: "{SOURCE}"\n')
            f.write("\n" if group is files else "")


if __name__ == "__main__":
    main()
