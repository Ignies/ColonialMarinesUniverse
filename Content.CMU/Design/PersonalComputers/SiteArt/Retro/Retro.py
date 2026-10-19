"""Retro 1980s computer art for the personal computers: desktop wallpapers, animated web gifs and desktop icons.

The colonies run on hardware that looks like it fell out of 1984: CRT screens, beige cases, chunky bitmap fonts.
This draws the art those machines would ship with, in the same hand-placed pixel style as the deep web ads
(SiteArt/Ads/Ads.py, whose colour, mask, shading and font helpers are copied in below so this file runs alone).

Writes to Content.CMU/Resources/Textures/CMU14/PersonalComputers/:
  Wallpapers/wallpaper_NN.png  desktop wallpapers, drawn at 320x200 and saved 3x with nearest neighbour (960x600).
                               The left 100 columns and the bottom 20 rows stay quiet for desktop icons and the
                               taskbar.
  Retro/retro.rsi              64x64 animated web gifs, one state per strip, frames left to right
  Retro/icon_NN.png            32x32 desktop icons, saved at 1x
  Wallpapers/attributions.yml and Retro/attributions.yml

Everything is drawn at 1x on the pixel grid with no antialiasing, with ordered dithering for gradients.

    python Retro.py              everything
    python Retro.py wall rsi     only some parts (wall, rsi, icons)
"""

import json
import math
import os
import random
import sys

from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
PC = os.path.join(ROOT, "Resources", "Textures", "CMU14", "PersonalComputers")
OUT_WALL = os.path.join(PC, "Wallpapers")
OUT_RETRO = os.path.join(PC, "Retro")
SCALE = 3
GENERATOR = "Content.CMU/Design/PersonalComputers/SiteArt/Retro/Retro.py"
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



# == Retro art ==========================================================================================
W, H = 320, 200
CALM_X, CALM_Y = 100, 180
SHADOW = (0, 0, 0, 110)


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def ridge_fn(seed, amp, scale=1.0):
    """Mountain ridge height in 0..amp as a function of x, from a few seeded sines."""
    r = random.Random(seed)
    waves = [(f * scale, r.random() * 6.283, w) for f, w in ((0.019, 0.42), (0.047, 0.27), (0.11, 0.17),
                                                              (0.27, 0.09), (0.61, 0.05))]

    def f(x):
        v = sum(math.sin(x * fq + ph) * w for fq, ph, w in waves)
        return amp * (v / 1.0 * 0.5 + 0.5)
    return f


def seg(cv, x0, y0, x1, y1, fn):
    """Bresenham line whose colour is fn(x, y)."""
    x0, y0, x1, y1 = int(round(x0)), int(round(y0)), int(round(x1)), int(round(y1))
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    for _ in range(4000):
        cv.set(x0, y0, fn(x0, y0))
        if x0 == x1 and y0 == y1:
            return
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def dmix(cv, x, y, colour, amount, levels=4):
    """Mixes colour into a pixel by a dithered amount, so only a few mix steps appear."""
    v = amount * levels
    i = int(v)
    if v - i > threshold(x, y, 4):
        i += 1
    if i:
        cv.set(x, y, mix(cv.get(x, y), colour, min(1.0, i / levels)))


def calm(cv, tone, k=0.45, fade=28, soften=0.75):
    """Quietens the strip behind the desktop icons (left CALM_X columns) and the taskbar strip (bottom rows).
    Detail is pulled toward a blurred copy of the picture, so the mean tone stays put and no seam shows, then a
    little of tone is mixed in; both fade out over fade pixels."""
    blur = cv.img.filter(ImageFilter.BoxBlur(5)).load()
    for y in range(cv.h):
        ay = smooth((y - (CALM_Y - 10)) / 10)
        for x in range(cv.w):
            ax = 1 - smooth((x - (CALM_X - 6)) / fade)
            a = max(ax, ay)
            if a > 0:
                dmix(cv, x, y, blur[x, y], a * soften)
                dmix(cv, x, y, tone, a * k * 0.5)


def vnoise(seed, cell, octaves=3, gain=0.5):
    """Smooth fractal value noise f(x, y) in about 0..1, with no per pixel grain."""
    layers = []
    for o in range(octaves):
        r = random.Random(seed * 31 + o)
        layers.append(({}, r.random() * 1000, cell / 2 ** o, gain ** o))
    total = sum(l[3] for l in layers)

    def lattice(tab, ix, iy, salt):
        key = (ix, iy)
        if key not in tab:
            tab[key] = random.Random(hash((ix, iy, salt, seed))).random()
        return tab[key]

    def f(x, y):
        v = 0.0
        for tab, salt, c, w in layers:
            gx, gy = x / c, y / c
            ix, iy = math.floor(gx), math.floor(gy)
            fx, fy = gx - ix, gy - iy
            fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
            a = lattice(tab, ix, iy, salt) + (lattice(tab, ix + 1, iy, salt) - lattice(tab, ix, iy, salt)) * fx
            b = lattice(tab, ix, iy + 1, salt) + (lattice(tab, ix + 1, iy + 1, salt) - lattice(tab, ix, iy + 1, salt)) * fx
            v += (a + (b - a) * fy) * w
        return v / total
    return f


def vignette(cv, colour, k=0.5, power=2.2):
    cx, cy = cv.w / 2, cv.h / 2
    for y in range(cv.h):
        for x in range(cv.w):
            d = math.hypot((x + 0.5 - cx) / cx, (y + 0.5 - cy) / cy) / math.sqrt(2)
            dmix(cv, x, y, colour, k * d ** power)


def stars(cv, rng, x0, y0, x1, y1, n, colours, density=None, cross=0.96):
    for _ in range(n):
        x, y = rng.randrange(x0, x1), rng.randrange(y0, y1)
        if density is not None and rng.random() > density(x, y):
            continue
        k = rng.random()
        c = colours[min(len(colours) - 1, int(k * k * len(colours)))]
        cv.set(x, y, c)
        if k > cross:
            for dx, dy in N4:
                cv.set(x + dx, y + dy, colours[len(colours) // 2])
            cv.set(x, y, WHITE)


def sparkle(cv, x, y, arm, core, mid, dim):
    cv.set(x, y, core)
    for d in range(1, arm + 1):
        c = mid if d < arm else dim
        for dx, dy in N4:
            cv.set(x + dx * d, y + dy * d, c)


def puffs(cv, x0, y0, rng, C, n=12, r0=3.0, grow=0.6, dx=5.0, rise=0.6, light=(-0.5, 0.6)):
    """Billowing plume: overlapping shaded puffs drifting right."""
    x, y, r = x0, y0, r0
    pts = []
    for _ in range(n):
        pts.append((x, y, r))
        x += dx * (0.8 + rng.random() * 0.5)
        y += rise - rng.random() * 1.6
        r += grow
    for px, py, pr in pts:
        m = m_ellipse(px, py, pr * 1.25, pr)
        paint(cv, m, C, f_sphere(px + light[0] * pr, py + light[1] * pr, pr * 1.8, len(C), 0.25), None, 4)


# -- Big sprites ---------------------------------------------------------------------------------------------
PROC_PARTS = [(6, -14, 13, 0), (39, -19, 47, 0), (47, -9, 54, 0), (0, -3, 56, 0), (18, -27, 34, -23),
              (20, -25, 32, 0), (23, -33, 29, -26), (13, -10, 20, -7), (32, -14, 39, -11)]


def processor_big(cv, ox, ground, s, D, rim, top_c, window_cs, seed=4, lights=True):
    """Atmosphere processor silhouette scaled by s, rim lit from the left; D is a dark ramp of 4."""
    allm = set()
    for x0, y0, x1, y1 in PROC_PARTS:
        m = m_rect(ox + round(x0 * s), ground + round(y0 * s), ox + round(x1 * s), ground + round(y1 * s) + 1)
        allm |= m
        for x, y in m:
            cv.set(x, y, D[1] if (x * 3 + y * 5) % 11 else D[2])
        for x, y in edge(m):
            if (x - 1, y) not in m:
                cv.set(x, y, rim)
            elif (x, y - 1) not in m:
                cv.set(x, y, top_c)
            else:
                cv.set(x, y, D[0])
    # Bands on the main stack, vents and stair lights.
    sx0, sx1 = ox + round(21 * s), ox + round(31 * s)
    for by in range(ground - round(24 * s), ground - 4, max(3, round(4 * s))):
        cv.hline(sx0, sx1, by, D[3])
        cv.set(sx0 - 1, by, rim)
    rng = random.Random(seed)
    for _ in range(int(16 * s)):
        x, y = rng.randrange(ox + 2, ox + round(54 * s)), rng.randrange(ground - round(3 * s) + 1, ground)
        if (x, y) in allm:
            cv.set(x, y, window_cs[rng.randrange(len(window_cs))])
    for x in range(sx0 + 1, sx1, 2):
        cv.set(x, ground - round(17 * s), window_cs[0])
    if lights:
        for px, py in ((26, -34), (9, -15), (43, -20), (50, -10)):
            x, y = ox + round(px * s), ground + round(py * s)
            cv.set(x, y, "#ff3a2a")
            cv.set(x, y - 1, "#ffb0a0")
    return allm


def gateway_big(cv, cx, cy, s):
    """Gateway Station: a spindle through a turning habitat ring, about 38s x 30s."""
    ring_o = m_ellipse(cx, cy, 18 * s, 6.5 * s)
    ring_i = m_ellipse(cx, cy, 14 * s, 4.0 * s)
    ring = ring_o - ring_i
    back = {(x, y) for x, y in ring if y + 0.5 < cy}
    front = ring - back
    paint(cv, back, SHELL, lambda x, y: 1.3, SHELL_OL)
    for x, y in back:
        if (x + y) % 4 == 0 and (x, y) not in edge(back):
            cv.set(x, y, SHELL[0])
    for a in (0.6, 2.5, 4.2):
        cv.line(cx, cy, cx + math.cos(a + math.pi) * 15 * s, cy + math.sin(a + math.pi) * 5 * s, SHELL[1])
    # Solar wings off the top of the spindle.
    for side in (-1, 1):
        for k in range(3):
            x0 = cx + side * (4 + k * 6) * s
            panel = m_rect(min(x0, x0 + side * 5 * s), cy - 15 * s, max(x0, x0 + side * 5 * s), cy - 12 * s)
            for x, y in panel:
                cv.set(x, y, "#1a3a7a" if (x + y) % 3 else "#3a6ac0")
            for x, y in edge(panel):
                cv.set(x, y, "#0a1430")
        cv.hline(int(cx), int(cx + side * 21 * s), int(cy - 13.5 * s), SHELL[2])
    spindle = m_rect(cx - 2 * s, cy - 18 * s, cx + 2 * s, cy + 16 * s)
    paint(cv, spindle, SHELL, f_cyl(cx - 2 * s, cx + 2 * s, len(SHELL), 0.3, 0.6), SHELL_OL)
    for my, h, w in ((-11, 4, 4), (-2, 4, 5), (9, 4, 4)):
        mod = m_rect(cx - w * s, cy + my * s, cx + w * s, cy + (my + h) * s)
        paint(cv, mod, SHELL, f_cyl(cx - w * s, cx + w * s, len(SHELL), 0.3, 0.8), SHELL_OL)
        for wx in range(int(cx - w * s + 2), int(cx + w * s - 2), 2):
            cv.set(wx, int(cy + (my + 2) * s), "#ffe6a0")
    cv.vline(int(cx), int(cy - 22 * s), int(cy - 18 * s), SHELL[3])
    cv.set(int(cx), int(cy - 22 * s), "#ff3a2a")
    for a in (0.6, 2.5, 4.2):
        cv.line(cx, cy, cx + math.cos(a) * 15 * s, cy + math.sin(a) * 5 * s, SHELL[2])
    paint(cv, front, SHELL, f_lin(cx - 18 * s, cy, cx + 18 * s, cy, 4.4, 1.4), SHELL_OL, soft=SHELL[2])
    for k in range(int(-16 * s), int(16 * s), 3):
        x = int(cx + k)
        ys = [y for (px, y) in front if px == x]
        if ys:
            cv.set(x, max(ys) - 2, "#ffe6a0")


def joe_big(cv, cx, top, s, bottom, wave=True):
    """Working Joe bust at scale s; the head top sits near top, the suit runs down to bottom."""
    S = lambda v: v * s
    ox = cx - S(15)
    if wave:
        # Raised right arm (viewer's right), drawn behind the shoulder.
        arm = m_capsule(cx + S(11), top + S(25), cx + S(19), top + S(15), S(3.1))
        paint(cv, arm, SUIT, f_lin(cx + S(9), top, cx + S(22), top, 4.0, 1.4), SUIT_OL)
        fore = m_capsule(cx + S(19), top + S(15), cx + S(20.5), top + S(5), S(2.5))
        paint(cv, fore, SUIT, f_cyl(cx + S(17.5), cx + S(22.5), len(SUIT), 0.3, 0.6), SUIT_OL)
        cuff = m_rect(cx + S(18), top + S(5), cx + S(23), top + S(6.5))
        paint(cv, cuff, SKIN, lambda x, y: 2.2, SKIN_OL)
        palm = m_ellipse(cx + S(20.6), top + S(2.2), S(2.4), S(2.8))
        paint(cv, palm, SKIN, f_sphere(cx + S(19.5), top + S(1), S(3.6), len(SKIN), 0.2), SKIN_OL)
        for i, fx in enumerate((18.6, 20.0, 21.4, 22.8)):
            f = m_capsule(cx + S(fx), top + S(0.4), cx + S(fx + 0.2 * (i - 1.5)), top - S(2.2 - abs(i - 1.5) * 0.4),
                          S(0.6))
            paint(cv, f, SKIN, lambda x, y: 3.6, SKIN_OL)
        thumb = m_capsule(cx + S(18.4), top + S(3), cx + S(16.8), top + S(1.2), S(0.6))
        paint(cv, thumb, SKIN, lambda x, y: 3.6, SKIN_OL)
        # Motion arcs beside the hand.
        for r in (S(5.5), S(7)):
            for a in range(-60, 30, 6):
                t = math.radians(a)
                cv.set(int(cx + S(20.5) + math.cos(t) * r), int(top + S(2) + math.sin(t) * r), "#1a1a1a")
    body = m_poly([(ox, bottom), (ox + S(1), top + S(27)), (ox + S(4), top + S(23)), (ox + S(10), top + S(21)),
                   (ox + S(20), top + S(21)), (ox + S(26), top + S(23)), (ox + S(29), top + S(27)),
                   (ox + S(30), bottom)])
    paint(cv, body, SUIT, f_cyl(ox, ox + S(30), len(SUIT), 0.3, 0.4), SUIT_OL)
    vee = m_poly([(cx - S(5), top + S(21)), (cx, top + S(28)), (cx + S(5), top + S(21))])
    cv.fill(vee, "#20242a")
    cv.line(cx - S(5), top + S(21), cx, top + S(28), SUIT[4])
    cv.line(cx + S(5), top + S(21), cx, top + S(28), SUIT[2])
    cv.vline(int(cx), int(top + S(29)), bottom, SUIT[0])
    cv.line(ox + S(5), top + S(24), ox + S(8), bottom, SUIT[1])
    cv.line(ox + S(25), top + S(24), ox + S(22), bottom, SUIT[1])
    # Seegson patch.
    px0, py0 = int(ox + S(18)), int(top + S(29))
    cv.rect(px0, py0, px0 + int(S(5)), py0 + int(S(4)), WHITE)
    cv.rect(px0 + 1, py0 + 1, px0 + int(S(5)) - 1, py0 + int(S(4)) - 1, "#2f6db0")
    cv.fill(edge(m_ellipse(px0 + S(2.5), py0 + S(2), S(1.3), S(1.3))), WHITE)
    # Neck.
    neck = m_rect(cx - S(3), top + S(15), cx + S(3), top + S(23))
    paint(cv, neck, SKIN, f_cyl(cx - S(3), cx + S(3), len(SKIN), 0.3, 0.5), SKIN_OL)
    for ry in (S(18), S(20)):
        cv.hline(int(cx - S(2.5)), int(cx + S(2)), int(top + ry), SKIN[1])
        cv.hline(int(cx - S(2.5)), int(cx + S(2)), int(top + ry) + 1, SKIN[3])
    for ex in (cx - S(7.2), cx + S(7.2)):
        paint(cv, m_ellipse(ex, top + S(11), S(1.6), S(2.6)), SKIN, f_sphere(ex - S(1), top + S(10), S(3), len(SKIN)),
              SKIN_OL)
    head = m_ellipse(cx, top + S(9.5), S(7), S(9))
    paint(cv, head, SKIN, f_sphere(cx - S(1.5), top + S(7.5), S(9.5), len(SKIN), 0.12), SKIN_OL, soft=SKIN[1], dither=4)
    cv.vline(int(cx - S(6)), int(top + S(9)), int(top + S(13)), SKIN[2])
    cv.vline(int(cx + S(5.4)), int(top + S(9)), int(top + S(13)), SKIN[1])
    cv.hline(int(cx - S(5)), int(cx - S(2)), int(top + S(7.6)), SKIN[5])
    cv.hline(int(cx + S(1)), int(cx + S(4)), int(top + S(7.6)), SKIN[4])
    for sxp in (cx - S(3.5), cx + S(3.5)):
        cv.fill(m_ellipse(sxp, top + S(10.5), S(2.2), S(1.4)), SKIN[0])
        eye = m_ellipse(sxp - S(0.4), top + S(10.4), S(1.5), S(0.75))
        for x, y in eye:
            d = abs(x + 0.5 - (sxp - S(0.4))) / S(1.5)
            cv.set(x, y, GLOW[3] if d < 0.35 else GLOW[2] if d < 0.7 else GLOW[1])
    cv.set(int(cx - S(1)), int(top + S(13)), SKIN[2])
    cv.hline(int(cx - S(1)), int(cx), int(top + S(13.4)), SKIN[1])
    cv.hline(int(cx - S(2)), int(cx + S(1.5)), int(top + S(15.2)), SKIN_OL)
    cv.hline(int(cx - S(1.2)), int(cx + S(0.8)), int(top + S(16.2)), SKIN[4])


# -- Wallpapers ----------------------------------------------------------------------------------------------


def w_synthwave():
    cv = Canvas(W, H)
    hy = 134
    vgrad(cv, 0, 0, W, hy, ["#08051a", "#0f0828", "#1a0b3c", "#2c0e50", "#46125e", "#681a68", "#902468",
                            "#b8345e", "#dc5450", "#f2864c", "#f8b45a"])
    rng = random.Random(1)
    stars(cv, rng, 0, 0, W, 70, 150, ("#3a2a6a", "#7a6aaa", "#c8bce8", "#ffffff"),
          density=lambda x, y: (1 - y / 70) * (0.35 if x < CALM_X else 1))
    scx, scy, sr = 214, 104, 46
    glow(cv, scx, scy, 120, 64, "#ff7a6a", 0.4)
    SUN = ramp("#ff2a7a", "#ff4a6a", "#ff6a52", "#ff9440", "#ffbe3a", "#ffe46a", "#fff6b0")
    for x, y in m_ellipse(scx, scy, sr, sr):
        if y >= hy:
            continue
        t = (y - (scy - sr)) / (2 * sr)
        k = y - (scy - 6)
        if k >= 0 and k % 8 < 1 + k // 9:
            continue
        cv.set(x, y, pick(SUN, (1 - t) * 6.4, x, y, 4))
    # Mountains: a far rose ridge and a near violet one, both rim lit by the sun.
    far = ridge_fn(7, 34, 1.0)
    near = ridge_fn(19, 22, 1.4)
    for x in range(W):
        lift = 0.35 + 0.65 * smooth((x - 60) / 120)
        h = int(hy - far(x) * lift)
        for y in range(h, hy):
            cv.set(x, y, pick(ramp("#1c0a34", "#2e0e46", "#44125a"), 2 - (y - h) / 10, x, y, 4))
        d = abs(x - scx)
        cv.set(x, h, "#ff8ab0" if d < 90 else "#a03a7a")
        if d < 60:
            cv.set(x, h + 1, "#c04a8a")
    for x in range(W):
        lift = 0.2 + 0.8 * smooth((x - 120) / 120)
        h = int(hy - near(x) * lift)
        for y in range(h, hy):
            cv.set(x, y, "#12061f" if (x + y) % 7 else "#1a0a2c")
        cv.set(x, h, "#ff5a9a" if abs(x - scx) < 70 else "#6a2a6a")
    # Ground and the neon grid.
    vgrad(cv, 0, hy, W, H, ["#1c0630", "#120420", "#0a0214", "#06010c"])
    vx = scx
    gl = rgba("#ff3ab4")
    gy = hy + 1.0
    step = 1.4
    while gy < H:
        yi = int(gy)
        f = (yi - hy) / (H - hy)
        cv.hline(0, W - 1, yi, mix("#1c0630", gl, 0.35 + 0.65 * min(1.0, f * 2.4)))
        step *= 1.34
        gy += step
    for k in range(-34, 35):
        def col(x, y, k=k):
            f = (y - hy) / (H - hy)
            return mix("#1c0630", gl, 0.3 + 0.7 * min(1.0, f * 2.2))
        seg(cv, vx + k * 22 * 1 / (H - hy), hy + 1, vx + k * 22, H - 1, col)
    # The sun's reflection, broken by the grid.
    for y in range(hy + 1, hy + 40):
        t = (y - hy) / 40
        half = int((1 - t) * 30 + 2)
        for x in range(scx - half, scx + half):
            if (y * 3 + x // 6) % 4 == 0 and rng.random() < 0.8 - t * 0.6:
                cv.set(x, y, pick(SUN, 5 - t * 4, x, y, 4))
    cv.hline(0, W - 1, hy, "#ffb0d8")
    # Atmosphere processor on the horizon with its steam plume.
    rngp = random.Random(5)
    puffs(cv, 288, hy - 63, rngp, ramp("#3a1648", "#6a2a6a", "#a8488a", "#e07a9a", "#ffb4b4"), n=11, r0=2.5,
          grow=0.75, dx=-5.5, rise=-0.9, light=(-0.6, -0.4))
    processor_big(cv, 240, hy + 2, 1.9, ramp("#07020e", "#100618", "#1a0a26", "#3a1a4a"), rgba("#ff7aa0"),
                  rgba("#7a3a8a"), ("#ffd27a", "#ff9a5a", "#7af0ff"))
    # A second, smaller processor further away.
    processor_big(cv, 128, hy + 1, 0.8, ramp("#14061e", "#1e0a2c", "#2a1038", "#4a2050"), rgba("#d05a8a"),
                  rgba("#6a3070"), ("#ffd27a",), seed=8)
    calm(cv, "#0a0414", 0.5)
    return cv


def w_storm():
    cv = Canvas(W, H)
    hy = 142
    bx = 246
    STORM = ramp("#04070a", "#081014", "#0e191e", "#152429", "#1e3034", "#2a3e40", "#3c5250", "#587068",
                 "#86a096")
    rng = random.Random(2)
    big = vnoise(31, 46, 4)
    fine = vnoise(77, 12, 2)
    # Cloud deck: heavy at the top, a pale break along the horizon, lit around the bolt.
    for y in range(hy):
        for x in range(W):
            n = big(x * 0.8, y * 1.7) * 0.75 + fine(x, y * 1.4) * 0.25
            v = n * 5.2 - 1.2
            v += 3.4 * smooth((y - (hy - 46)) / 46)
            v += 3.0 * max(0.0, 1 - math.hypot((x - bx) / 110, (y - 34) / 60)) ** 1.5
            v -= 1.2 * (1 - y / hy) ** 2
            cv.set(x, y, pick(STORM, v, x, y, 4))
    # Underside ridges of the clouds catch the horizon light.
    for y in range(20, hy - 20):
        for x in range(W):
            if big(x * 0.8, y * 1.7) > 0.58 and big(x * 0.8, (y + 2) * 1.7) <= 0.58:
                dmix(cv, x, y, "#86a096", 0.5 * (y / hy))
    # Rain sheets.
    for _ in range(380):
        x, y = rng.randrange(-20, W), rng.randrange(16, hy + 10)
        if x < CALM_X and rng.random() < 0.6:
            continue
        ln = rng.randrange(3, 7)
        cv.line(x, y, x + ln // 2, y + ln, "#4e6466" if rng.random() < 0.7 else "#7a9494")
    # Lightning.
    pts = [(bx, 0)]
    x, y = bx, 0
    rb = random.Random(9)
    while y < hy - 2:
        y += rb.randrange(5, 11)
        x += rb.randrange(-7, 6)
        pts.append((x, min(y, hy - 2)))
    branch = [pts[5]]
    x, y = pts[5]
    for _ in range(5):
        x += rb.randrange(3, 9)
        y += rb.randrange(4, 9)
        branch.append((x, y))
    glow(cv, pts[-1][0], pts[-1][1] + 8, 40, 14, "#c8f0ff", 0.4)
    for path, core in ((pts, True), (branch, False)):
        for (x0, y0), (x1, y1) in zip(path, path[1:]):
            for d in (-1, 1):
                cv.line(x0 + d, y0, x1 + d, y1, "#6ab0e0" if core else "#3a6a90")
            cv.line(x0, y0, x1, y1, "#ffffff" if core else "#b8e4ff")
    # Far ridge.
    far = ridge_fn(3, 12, 1.6)
    for x in range(W):
        h = int(hy - 3 - far(x) * (0.3 + 0.7 * smooth((x - 80) / 140)))
        for y in range(h, hy + 1):
            cv.set(x, y, "#121614" if (x + y) % 5 else "#161a18")
        cv.set(x, h, "#5a706c" if abs(x - bx) < 90 else "#34403e")
    # The derelict: a horseshoe hull half buried in the plain, horns curling inward.
    dcx, base = 170, hy + 1
    hull = (m_ellipse(dcx, base - 18, 30, 25) - m_ellipse(dcx, base - 25, 17, 23))
    hull = {(x, y) for x, y in hull if y <= base}
    for side in (-1, 1):
        hull |= m_ellipse(dcx + side * 20, base - 39, 5.5, 4)
        hull |= m_ellipse(dcx + side * 15, base - 42, 4, 2.6)
    for x, y in hull:
        cv.set(x, y, "#0a0c0c" if (x + 2 * y) % 7 else "#101414")
    for x, y in edge(hull):
        if (x + 1, y) not in hull:
            cv.set(x, y, "#6a8480")
        elif (x, y - 1) not in hull:
            cv.set(x, y, "#3a4a48")
    for k in range(5):
        a = math.pi * (0.15 + k * 0.17)
        for r in range(19, 29):
            px, py = int(dcx + math.cos(a) * r), int(base - 18 + math.sin(a) * r * 0.85)
            if (px, py) in hull and r % 2:
                cv.set(px, py, "#1c2424")
    # Ground.
    GROUND = ramp("#070605", "#0e0c09", "#17140f", "#221d16", "#2e271d", "#3e3426", "#524634")
    gn = vnoise(5, 20, 3)
    for y in range(hy + 1, H):
        t = (y - hy) / (H - hy)
        for x in range(W):
            v = 4.6 - t * 3.4 + (gn(x, y * 3) - 0.5) * 2.6
            v += 1.2 * max(0.0, 1 - math.hypot((x - bx) / 90, (y - hy) / 20))
            cv.set(x, y, pick(GROUND, v, x, y, 4))
    # Basalt spires lit from the bolt side.
    ROCK = ramp("#080706", "#13110d", "#211d17", "#332c22", "#4a4132", "#6a5e48")
    for rx, rw, rh, seed in ((204, 20, 40, 1), (226, 12, 24, 2), (284, 30, 54, 3), (126, 14, 16, 4),
                             (308, 14, 30, 5), (262, 10, 16, 6)):
        rr = random.Random(seed)
        base_y = hy + 6 + rr.randrange(0, 8)
        left = [(rx, base_y)]
        right = [(rx + rw, base_y)]
        for k in range(1, 5):
            f = k / 5
            left.append((rx + rw * 0.18 * f + rr.uniform(-1.5, 1.5), base_y - rh * f))
            right.append((rx + rw - rw * 0.2 * f + rr.uniform(-1.5, 1.5), base_y - rh * f * 0.92))
        peak = (rx + rw * rr.uniform(0.35, 0.6), base_y - rh)
        rock = m_poly(left + [peak] + right[::-1])
        lit_left = rx > bx
        mid = rx + rw / 2
        for x, y in rock:
            facing = (x < mid) == lit_left
            v = (3.3 if facing else 1.5) + (0.6 if (y + int(x * 0.3)) % 6 == 0 else 0)
            cv.set(x, y, pick(ROCK, v, x, y, 4))
        for x, y in rock:
            if (x // 3 + rr.randrange(2)) % 4 == 0 and (x, y) not in edge(rock):
                cv.set(x, y, ROCK[1] if not ((x < mid) == lit_left) else ROCK[3])
        for x, y in edge(rock):
            lit = ((x - 1, y) not in rock) if lit_left else ((x + 1, y) not in rock)
            if lit or (x, y - 1) not in rock:
                cv.set(x, y, "#9ab8b4" if abs(x - bx) < 70 else "#5a6a64")
            else:
                cv.set(x, y, ROCK[0])
    # Dust blown low over the plain.
    for _ in range(160):
        x, y = rng.randrange(0, W), rng.randrange(hy - 6, H)
        cv.line(x, y, x + rng.randrange(4, 10), y + 1, "#4a4232")
    calm(cv, "#06080a", 0.4)
    return cv


def big_mark(cv, rows, x, y, s, fill_fn, hi, lo, outline):
    """Bitmap mark scaled by s with a bevel: lit top-left edges, dark bottom-right edges and an outline."""
    m = set()
    for gy, row in enumerate(rows):
        for gx, ch in enumerate(row):
            if ch == "#":
                m |= m_rect(x + gx * s, y + gy * s, x + (gx + 1) * s, y + (gy + 1) * s)
    cv.fill(halo(m), outline)
    for px, py in m:
        cv.set(px, py, fill_fn(px, py))
    for px, py in m:
        if (px - 1, py) not in m or (px, py - 1) not in m:
            cv.set(px, py, hi)
        elif (px + 1, py) not in m or (px, py + 1) not in m:
            cv.set(px, py, lo)
    return m


def w_weyland():
    cv = Canvas(W, H)
    vgrad(cv, 0, 0, W, H, ["#04060c", "#070b16", "#0a1120", "#0d1628", "#0a1020", "#05080e"])
    # Faint diagonal tile of small marks.
    for ty in range(-4, H, 18):
        for tx in range(-8 + (ty // 18 % 2) * 12, W, 24):
            bitmap(cv, tx, ty, WY_MARK_S, {"#": "#0e1830"})
    # Light fan behind the mark.
    cx, cy = 212, 70
    for y in range(H):
        for x in range(W):
            a = math.atan2(y + 0.5 - cy, x + 0.5 - cx)
            d = math.hypot(x - cx, (y - cy) * 1.6)
            band = (math.sin(a * 14) > 0.55)
            if band and 20 < d < 170:
                dmix(cv, x, y, "#1e3050", 0.6 * (1 - d / 170))
    glow(cv, cx, cy, 110, 60, "#2a4a7a", 0.4)
    glow(cv, cx, cy, 60, 32, "#c89a4a", 0.25)
    G = ramp("#5a3a0a", "#8a5e14", "#c08a22", "#e8b440", "#ffe08a", "#fff6d0")
    s = 7
    mw = len(WY_MARK[0]) * s
    x0, y0 = cx - mw // 2, cy - len(WY_MARK) * s // 2 - 2
    big_mark(cv, WY_MARK, x0 + 2, y0 + 3, s, lambda x, y: rgba("#02040a"), rgba("#02040a"), rgba("#02040a"),
             rgba("#02040a"))
    big_mark(cv, WY_MARK, x0, y0, s,
             lambda x, y: pick(G, 4.6 - (y - y0) / (len(WY_MARK) * s) * 3.4 + (0.6 if (x - y) % 23 < 3 else 0), x, y, 4),
             G[5], G[0], rgba("#140c02"))
    # Glints.
    sparkle(cv, x0 + 10, y0 + 2, 3, WHITE, G[5], G[3])
    sparkle(cv, x0 + mw - 16, y0 + 30, 2, WHITE, G[4], G[2])
    ty = y0 + len(WY_MARK) * s + 12
    chrome = ramp("#3a4a6a", "#7a8aa8", "#c8d4e8", "#ffffff")
    text_fill(cv, BOLD, cx - text_width(BOLD, "WEYLAND-YUTANI") // 2, ty, "WEYLAND-YUTANI",
              lambda x, y: pick(chrome, 3.2 - (y - ty) / 9 * 2.6 + (1 if y - ty == 4 else 0), x, y, 4),
              outline=rgba("#02040a"), depth=1, depth_c=rgba("#02040a"))
    ry = ty + 14
    tw = text_width(F5, "BUILDING BETTER WORLDS")
    draw_text_c(cv, F5, cx, ry, "BUILDING BETTER WORLDS", G[4], shadow="#02040a")
    for side in (-1, 1):
        xa = cx + side * (tw // 2 + 6)
        xb = cx + side * (tw // 2 + 40)
        cv.hline(xa, xb, ry + 3, G[3])
        cv.hline(xa, xb, ry + 4, G[1])
        cv.set(xa, ry + 3, WHITE)
    # A thin orbital line and corporate fine print.
    for x in range(110, W - 8):
        y = int(150 + 0.0009 * (x - 212) ** 2)
        cv.set(x, y, "#2a4a7a" if x % 3 else "#4a7ab0")
    draw_text_c(cv, F3, cx, 162, "EST 2099  -  EARTH  MARS  LV-426", "#3a5a8a")
    calm(cv, "#04060c", 0.4)
    return cv


def w_blueprint():
    cv = Canvas(W, H)
    noise_tex(cv, 0, 0, W, H, ramp("#0c2856", "#0f2e60", "#12336a"), 12, cell=10)
    minor, major = rgba("#1a3f78"), rgba("#2c5a98")
    for x in range(0, W, 8):
        cv.vline(x, 0, H - 1, major if x % 40 == 0 else minor)
    for y in range(0, H, 8):
        cv.hline(0, W - 1, y, major if y % 40 == 0 else minor)
    L, F = rgba("#e4f2ff"), rgba("#80aee6")
    D = rgba("#5a8cd0")
    cx, top = 190, 16
    s = 4.6
    S = lambda v: v * s
    head = m_ellipse(cx, top + S(9.5), S(7), S(9))
    cv.fill(edge(head), L)
    for ex in (cx - S(7.4), cx + S(7.4)):
        e = m_ellipse(ex, top + S(11), S(1.6), S(2.6))
        for x, y in edge(e):
            if (x, y) not in head:
                cv.set(x, y, L)
    for sxp in (cx - S(3.5), cx + S(3.5)):
        cv.fill(edge(m_ellipse(sxp, top + S(10.5), S(2.2), S(1.4))), L)
        cv.fill(edge(m_ellipse(sxp, top + S(10.5), S(1.0), S(0.7))), F)
        cv.set(int(sxp), int(top + S(10.5)), WHITE)
    cv.hline(int(cx - S(5)), int(cx - S(2)), int(top + S(7.6)), L)
    cv.hline(int(cx + S(2)), int(cx + S(5)), int(top + S(7.6)), L)
    cv.line(cx - S(6), top + S(8.6), cx - S(6), top + S(13), F)
    cv.line(cx + S(6), top + S(8.6), cx + S(6), top + S(13), F)
    cv.hline(int(cx - S(1)), int(cx + S(1)), int(top + S(13.4)), L)
    cv.hline(int(cx - S(2.2)), int(cx + S(2.2)), int(top + S(15.4)), L)
    nk = m_rect(cx - S(3), top + S(18.4), cx + S(3), top + S(23))
    for x, y in edge(nk):
        if y > top + S(18.5):
            cv.set(x, y, L)
    for ry in (S(19.8), S(21.3)):
        cv.hline(int(cx - S(3)), int(cx + S(3)), int(top + ry), F)
    body = [(cx - S(15), H - 22), (cx - S(14), top + S(27)), (cx - S(11), top + S(23)), (cx - S(3), top + S(22.5)),
            (cx + S(3), top + S(22.5)), (cx + S(11), top + S(23)), (cx + S(14), top + S(27)), (cx + S(15), H - 22)]
    for (x0, y0), (x1, y1) in zip(body, body[1:]):
        cv.line(x0, y0, x1, y1, L)
    cv.line(cx - S(4), top + S(22.5), cx, top + S(28), L)
    cv.line(cx + S(4), top + S(22.5), cx, top + S(28), L)
    cv.fill(edge(m_rect(cx + S(6), top + S(30), cx + S(11), top + S(34))), L)
    cv.fill(edge(m_ellipse(cx + S(8.5), top + S(32), S(1.2), S(1.2))), F)
    for y in range(top - 6, H - 20):
        if y % 6 < 3:
            cv.set(cx, y, D)

    def callout(px, py, tx, ty, text, left):
        cv.fill(m_ellipse(px + 0.5, py + 0.5, 1.5, 1.5), WHITE)
        cv.line(px, py, tx, ty + 2, F)
        tw = text_width(F3, text)
        if left:
            cv.hline(tx - tw - 2, tx, ty + 2, F)
            draw_text(cv, F3, tx - tw - 1, ty - 4, text, L)
        else:
            cv.hline(tx, tx + tw + 2, ty + 2, F)
            draw_text(cv, F3, tx + 2, ty - 4, text, L)
    callout(int(cx - S(3.5)), int(top + S(10.5)), 150, 44, "OPTIC ARRAY", True)
    callout(int(cx - S(7.4)), int(top + S(12.5)), 152, 92, "AUDIO PICKUP", True)
    callout(int(cx + S(3)), int(top + S(20.5)), 248, 118, "NECK SERVO", False)
    callout(int(cx - S(9)), int(top + S(32)), 152, 150, "PVC SHELL", True)
    dx = int(cx + S(10))
    cv.vline(dx, top, int(top + S(18.5)), F)
    for yy in (top, int(top + S(18.5))):
        cv.hline(dx - 2, dx + 2, yy, F)
    bitmap(cv, dx - 1, top + 1, [".#.", "###"], {"#": F})
    bitmap(cv, dx - 1, int(top + S(18.5)) - 2, ["###", ".#."], {"#": F})
    draw_text(cv, F3, dx + 4, int(top + S(9)) - 2, "0.31 M", L)
    # Detail A: the optic in section.
    dcx, dcy = 284, 74
    cv.fill(edge(m_ellipse(dcx, dcy, 16, 16)), L)
    cv.fill(edge(m_ellipse(dcx, dcy, 11, 7)), L)
    cv.fill(edge(m_ellipse(dcx, dcy, 5, 5)), F)
    cv.fill(m_ellipse(dcx, dcy, 2, 2), L)
    cv.line(dcx - 16, dcy, dcx + 16, dcy, D)
    draw_text_c(cv, F3, dcx, dcy + 19, "DETAIL A", L)
    seeg = m_ellipse(290, 26, 15, 15)
    cv.fill(edge(seeg), L)
    cv.fill(edge(m_ellipse(290, 26, 12.5, 12.5)), F)
    bitmap(cv, 286, 22, SEEG_MARK, {"#": L})
    tb = (262, 134, 317, 177)
    cv.fill(edge(m_rect(*tb)), L)
    for yy in (143, 152, 165):
        cv.hline(tb[0], tb[2] - 1, yy, F)
    draw_text_c(cv, F5, 290, 136, "SEEGSON", L)
    draw_text_c(cv, F3, 290, 146, "WORKING JOE", L)
    draw_text_c(cv, F3, 290, 155, "WJ-2", F)
    draw_text(cv, F3, 265, 168, "1/4", L)
    draw_text(cv, F3, 291, 168, "1:5", L)
    cv.vline(287, 165, tb[3] - 1, F)
    calm(cv, "#0e2c5c", 0.4)
    return cv


def sulaco(cv, ox, oy, rng):
    """A long gun-shaped Conestoga-class assault ship flying left, about 196x44."""
    HL = HULL
    OL = rgba("#030407")
    # Engine glow trailing to the right.
    for i, c in enumerate(("#123a8a", "#3a7af0", "#9ad0ff", "#ffffff")):
        for ny in (oy + 18, oy + 26, oy + 34):
            cv.fill(m_ellipse(ox + 198 + 4 - i * 2, ny + 0.5, 9 - i * 2.2, 2.8 - i * 0.6), c)
    parts = [
        # Barrel spine and under rail.
        (m_poly([(ox, oy + 20), (ox + 4, oy + 17), (ox + 84, oy + 17), (ox + 84, oy + 25), (ox + 4, oy + 25)]), 0.4),
        (m_rect(ox + 12, oy + 25, ox + 92, oy + 29), 0.2),
        # Nose block.
        (m_poly([(ox - 2, oy + 15), (ox + 10, oy + 13), (ox + 12, oy + 15), (ox + 12, oy + 28), (ox - 2, oy + 27)]), 0.5),
        # Mid body with the bridge tower.
        (m_poly([(ox + 70, oy + 14), (ox + 98, oy + 9), (ox + 112, oy + 9), (ox + 118, oy + 2), (ox + 140, oy + 2),
                 (ox + 146, oy + 9), (ox + 160, oy + 9), (ox + 160, oy + 36), (ox + 120, oy + 40), (ox + 96, oy + 36),
                 (ox + 76, oy + 30)]), 0.6),
        # Aft engine block.
        (m_poly([(ox + 154, oy + 6), (ox + 192, oy + 8), (ox + 196, oy + 12), (ox + 196, oy + 40), (ox + 190, oy + 44),
                 (ox + 156, oy + 42)]), 0.8),
    ]
    allm = set()
    for m, lo in parts:
        ys = [y for _, y in m]
        paint(cv, m, HL, f_hcyl(min(ys), max(ys) + 1, len(HL), 0.25, lo), OL, soft=HL[4])
        allm |= m
    # Nozzles.
    for ny in (oy + 15, oy + 23, oy + 31):
        cv.rect(ox + 196, ny, ox + 200, ny + 6, HL[1])
        cv.vline(ox + 199, ny + 1, ny + 4, "#bfe4ff")
        cv.hline(ox + 196, ox + 199, ny, HL[3])
    # Panel lines, greebles and windows.
    for px in range(ox + 16, ox + 84, 9):
        cv.vline(px, oy + 18, oy + 24, HL[1])
    for px in range(ox + 100, ox + 156, 11):
        cv.vline(px, oy + 12, oy + 34, HL[1])
    for _ in range(70):
        x, y = rng.randrange(ox + 14, ox + 194), rng.randrange(oy + 4, oy + 42)
        if (x, y) in allm and (x, y) not in edge(allm):
            w = rng.randrange(2, 6)
            c = HL[2] if rng.random() < 0.5 else HL[4]
            for k in range(w):
                if (x + k, y) in allm and (x + k, y) not in edge(allm):
                    cv.set(x + k, y, c)
    for px in range(ox + 20, ox + 82, 3):
        if rng.random() < 0.5:
            cv.set(px, oy + 21, "#ffd27a")
    for px in range(ox + 100, ox + 158, 2):
        if rng.random() < 0.45:
            cv.set(px, oy + 20 + rng.choice((0, 6, 12)), "#ffd27a" if rng.random() < 0.8 else "#9adcf0")
    cv.hline(ox + 120, ox + 138, oy + 5, "#9adcf0")
    cv.hline(ox + 121, ox + 137, oy + 6, "#2a5a7a")
    # Hazard band, dropship bay and the hull number.
    for k in range(10):
        cv.line(ox + 160 + k * 3, oy + 37, ox + 162 + k * 3, oy + 40, "#e8b020" if k % 2 == 0 else "#202020")
    cv.rect(ox + 104, oy + 34, ox + 126, oy + 38, "#06080c")
    cv.hline(ox + 104, ox + 125, oy + 34, "#ff9a40")
    draw_text(cv, F3, ox + 164, oy + 16, "USS", HL[5])
    draw_text(cv, F3, ox + 164, oy + 23, "SULACO", "#e8e0c8")
    # Nav lights.
    cv.set(ox - 2, oy + 15, "#ff3a2a")
    cv.set(ox + 140, oy + 1, "#5aff8a")
    cv.set(ox + 118, oy + 1, "#ff3a2a")


def w_starship():
    cv = Canvas(W, H)
    vgrad(cv, 0, 0, W, H, ["#010104", "#030310", "#06061a", "#0a0820", "#060514", "#020208"])
    # Faint nebula band behind everything.
    tmp = Canvas(W, H)
    noise_tex(tmp, 0, 0, W, H, ramp("#000000", "#ffffff"), 44, cell=22)
    for y in range(H):
        for x in range(W):
            band = math.exp(-((y - 60 - x * 0.25) / 34) ** 2)
            v = tmp.get(x, y)[0] / 255
            a = band * v * 0.7
            if a > 0.05:
                dmix(cv, x, y, "#3a1a5a" if x < 200 else "#1a3a6a", a)
    rng = random.Random(5)
    stars(cv, rng, 0, 0, W, H, 520, ("#262a44", "#4a5070", "#9aa4c8", "#e8eeff"),
          density=lambda x, y: 0.4 if x < CALM_X else 1)
    # Streaking stars: the ship is moving fast.
    for _ in range(26):
        x, y = rng.randrange(CALM_X, W), rng.randrange(10, 170)
        ln = rng.randrange(6, 18)
        for k in range(ln):
            cv.set(x + k, y, mix("#c8d4ff", "#06061a", k / ln))
    # A distant moon, top right.
    moon = m_ellipse(290, 30, 13, 13)
    paint(cv, moon, ramp("#141824", "#2a3044", "#4a5268", "#7a8298", "#b4bccc"), f_sphere(284, 24, 16, 5, 0.05, 1.3),
          None, 4)
    for cxp, cyp, r in ((286, 27, 2.5), (293, 34, 1.6), (282, 35, 1.2)):
        cv.fill(edge(m_ellipse(cxp, cyp, r, r)) & moon, "#2a3044")
    sulaco(cv, 116, 96, random.Random(7))
    calm(cv, "#020208", 0.35)
    return cv


def w_gateway():
    cv = Canvas(W, H)
    vgrad(cv, 0, 0, W, H, ["#010106", "#03061a", "#050a24", "#03061a"])
    rng = random.Random(6)
    stars(cv, rng, 0, 0, W, H, 380, ("#20263e", "#4a5272", "#a4b0d4", "#ffffff"),
          density=lambda x, y: 0.45 if x < CALM_X else 1)
    ecx, ecy, er = 236, 344, 214
    OCEAN = ramp("#01040c", "#041026", "#082248", "#0e3a72", "#18569c", "#2a76c0", "#5aa4e0")
    LAND = ramp("#030604", "#0a1608", "#14280e", "#204016", "#365a22", "#5a7434")
    CLOUD = ramp("#0a0e18", "#3a4458", "#8a96aa", "#d8e0ea", "#ffffff")
    land_n = vnoise(8, 34, 4)
    cl_n = vnoise(18, 22, 3)
    Lv = (-0.72, -0.58, 0.38)
    disc = m_ellipse(ecx, ecy, er, er)
    for x, y in disc:
        if not cv.inside(x, y):
            continue
        nx, ny = (x + 0.5 - ecx) / er, (y + 0.5 - ecy) / er
        nz = math.sqrt(max(0.0, 1 - nx * nx - ny * ny))
        lit = max(0.0, nx * Lv[0] + ny * Lv[1] + nz * Lv[2])
        lit = min(1.0, lit * 1.35)
        u, v = math.atan2(nx, nz) * 120, math.asin(max(-1.0, min(1.0, ny))) * 120
        land = land_n(u + 300, v + 300) > 0.61
        cloud = cl_n(u * 0.45 + 40, v * 2.4 + 90 + math.sin(u * 0.04) * 14)
        c = pick(LAND, lit * 5.4 + 0.3, x, y, 4) if land else pick(OCEAN, lit * 6.4 + 0.4, x, y, 4)
        if cloud > 0.6 and lit > 0.04:
            c = mix(c, pick(CLOUD, lit * 4.4, x, y, 4), min(1.0, (cloud - 0.6) * 7))
        elif lit < 0.05 and land and rng.random() < 0.12:
            c = rgba("#ffb84a") if rng.random() < 0.6 else rgba("#ff8a3a")
        cv.set(x, y, c)
    for x, y in halo(disc, False):
        if cv.inside(x, y):
            nx, ny = (x + 0.5 - ecx) / er, (y + 0.5 - ecy) / er
            lit = max(0.0, nx * Lv[0] + ny * Lv[1] + 0.25)
            if lit > 0.12:
                cv.set(x, y, mix("#1a3a80", "#bff0ff", min(1.0, lit * 1.3)))
                for d in (1, 2, 3, 4):
                    dmix(cv, x - int(round(nx * d)), y - int(round(ny * d)), "#4a98f0", 0.55 * lit / d)
    gateway_big(cv, 214, 62, 3.0)
    for k in range(40):
        cv.set(140 + k * 2, 30 + k // 3, mix("#ffb060", "#050a24", k / 40))
    cv.fill(m_poly([(138, 29), (143, 27.5), (145, 30), (140, 31)]), "#d8dce4")
    cv.set(144, 29, "#ffe6a0")
    calm(cv, "#02040e", 0.3)
    return cv


def w_testcard():
    cv = Canvas(W, H, "#4e535c")
    cx, cy, r = 212, 92, 74
    # Grid and castellated border.
    for x in range(4, W, 16):
        cv.vline(x, 0, H - 1, "#c8ccd4")
    for y in range(4, H, 16):
        cv.hline(0, W - 1, y, "#c8ccd4")
    for i, x in enumerate(range(-4, W, 16)):
        cv.rect(x + 1, 0, x + 16, 4, "#101010" if i % 2 else "#f0f0f0")
        cv.rect(x + 1, H - 4, x + 16, H, "#f0f0f0" if i % 2 else "#101010")
    for i, y in enumerate(range(-4, H, 16)):
        cv.rect(0, y + 1, 4, y + 16, "#101010" if i % 2 else "#f0f0f0")
        cv.rect(W - 4, y + 1, W, y + 16, "#f0f0f0" if i % 2 else "#101010")
    circle = m_ellipse(cx, cy, r, r)
    cv.fill(halo(circle) | edge(circle), "#f0f0f0")
    bars = ["#f0f0f0", "#f0e020", "#20e0e8", "#20d040", "#e030d0", "#e03020", "#2040e0", "#101010"]
    xs0, xs1 = cx - r, cx + r
    for x, y in circle:
        ry = y - (cy - r)
        if ry < 34:
            c = bars[min(7, (x - xs0) * 8 // (xs1 - xs0))]
        elif ry < 46:
            # Frequency gratings, coarse to fine.
            seg_i = (x - xs0) * 5 // (xs1 - xs0)
            p = (6, 4, 3, 2, 1)[seg_i]
            c = "#f0f0f0" if (x // p) % 2 else "#101010"
        elif ry < 102:
            c = "#202020"
        elif ry < 114:
            c = pick(ramp("#000000", "#2a2a2a", "#555555", "#808080", "#aaaaaa", "#d4d4d4", "#ffffff"),
                     (x - xs0) / (xs1 - xs0) * 6, x, y, 0)
        else:
            c = "#a01818" if x < cx - 20 else "#f0f0f0" if x < cx + 20 else "#1840a0"
        cv.set(x, y, c)
    # Central cross and label box.
    cv.hline(cx - r + 2, cx + r - 2, cy + 3, "#f0f0f0")
    cv.vline(cx, cy - r + 46, cy + r - 26, "#f0f0f0")
    for rr in (14, 28):
        cv.fill(edge(m_ellipse(cx + 0.5, cy + 3.5, rr, rr)) & {(x, y) for x, y in circle
                                                                 if cy - r + 46 <= y < cy - r + 102}, "#f0f0f0")
    cv.rect(cx - 40, cy - r + 50, cx + 41, cy - r + 64, "#101010")
    cv.fill(edge(m_rect(cx - 40, cy - r + 50, cx + 41, cy - r + 64)), "#f0f0f0")
    draw_text_c(cv, F5, cx, cy - r + 54, "COLONY NET 4", "#f0f0f0")
    cv.rect(cx - 30, cy + 22, cx + 31, cy + 28, "#101010")
    draw_text_c(cv, F3, cx, cy + 23, "WY BROADCAST", "#f0e020")
    # Clock in the lower block.
    cv.rect(cx - 18, cy + r - 22, cx + 19, cy + r - 13, "#101010")
    draw_text_c(cv, F5, cx, cy + r - 21, "18:24", "#f0f0f0")
    # Channel box.
    cv.rect(296, 76, 312, 108, "#101010")
    draw_text_c(cv, F5, 304, 80, "C", "#f0f0f0")
    draw_text_c(cv, F5, 304, 89, "H", "#f0f0f0")
    draw_text_c(cv, F5, 304, 99, "4", "#f0e020")
    # CRT: curvature shadow, phosphor scanlines and a hot spot.
    glow(cv, cx, cy, 150, 110, "#ffffff", 0.12)
    scanlines(cv, 0, 0, W, H, 0.84)
    vignette(cv, "#000000", 0.85, 3.0)
    calm(cv, "#2a2e34", 0.45)
    return cv


def w_vector():
    cv = Canvas(W, H)
    hy = 100
    vgrad(cv, 0, 0, W, hy + 1, ["#000000", "#02010a", "#060216", "#120424", "#24063a"])
    rng = random.Random(8)
    stars(cv, rng, 0, 0, W, hy - 10, 110, ("#2a2a4a", "#6a6a9a", "#c8c8ff"),
          density=lambda x, y: 0.4 if x < CALM_X else 1)
    scx, scy, sr = 236, 66, 40
    for y in range(int(scy - sr), hy):
        k = y - int(scy - sr)
        gap = y > scy and (k % 4 == 0 or (y > scy + 16 and k % 2 == 0))
        if not gap:
            half = math.sqrt(max(0, sr * sr - (y + 0.5 - scy) ** 2))
            t = (y - (scy - sr)) / (2 * sr)
            cv.hline(int(scx - half), int(scx + half), y,
                     mix("#ffe060", "#ff2a8a", t) if k % 2 == 0 or y < scy else mix("#7a2a4a", "#4a0a3a", t))
    f, cam, cx = 150.0, 2.2, 160

    def height(X, Z):
        if Z < 4:
            return 0.0
        side = smooth((X + 2) / 14)
        m = (math.sin(X * 0.55 + 1.2) * 0.5 + math.sin(Z * 0.7 + X * 0.21) * 0.4 + math.sin(X * 1.3 - Z * 0.4) * 0.2
             + 1.1)
        return max(0.0, m * 3.0 * side * smooth((Z - 4) / 6))
    zs = [1.0 * 1.085 ** i for i in range(40)]
    xs = [x * 1.0 for x in range(-40, 41)]

    def proj(X, Z):
        return cx + X * f / Z, hy + (cam - height(X, Z)) * f / Z
    rows = [[proj(X, Z) for X in xs] for Z in zs]
    LINE = ramp("#16062e", "#2e0a58", "#5a1494", "#2a72d0", "#36d0ec", "#b8ffff")
    for ri in range(len(zs) - 1, -1, -1):
        row = rows[ri]
        for (x0, y0), (x1, y1) in zip(row, row[1:]):
            if x1 < -2 or x0 > W + 2:
                continue
            for x in range(max(0, int(x0)), min(W, int(x1) + 1)):
                t = (x - x0) / max(0.001, x1 - x0)
                yy = y0 + (y1 - y0) * t
                for y in range(max(0, int(yy) + 1), H):
                    cv.set(x, y, "#000000")
        depth = max(0.0, 1 - math.log(zs[ri] / 3.3) / math.log(zs[-1] / 3.3))
        c = LINE[max(0, min(5, int(depth * 5.6)))]
        for (x0, y0), (x1, y1) in zip(row, row[1:]):
            if max(x0, x1) < -50 or min(x0, x1) > W + 50 or min(y0, y1) > H + 50:
                continue
            cv.line(x0, y0, x1, min(y1, H + 50), c)
        if ri + 1 < len(zs):
            for (x0, y0), (x1, y1) in zip(row, rows[ri + 1]):
                if max(x0, x1) < -50 or min(x0, x1) > W + 50 or min(y0, y1) > H + 50:
                    continue
                cv.line(x0, min(H + 50, y0), x1, y1, c)
    cv.hline(0, W - 1, hy, "#5a1494")
    gx, gy = 168, 26
    pts = [(gx, gy), (gx + 22, gy + 6), (gx + 30, gy + 4), (gx + 22, gy + 2)]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]):
        cv.line(x0, y0, x1, y1, "#36d0ec")
    cv.line(gx + 22, gy + 2, gx + 22, gy + 6, "#36d0ec")
    for k in range(3):
        cv.line(gx + 31 + k * 4, gy + 4, gx + 32 + k * 4, gy + 4, "#ff6a9a")
    calm(cv, "#000000", 0.5, soften=0.85)
    return cv


def w_hypersleep():
    cv = Canvas(W, H)
    WALL = ramp("#0c141c", "#16222c", "#22323e", "#30444f", "#425864", "#5a7280")
    for y in range(0, 118):
        for x in range(W):
            v = 2.4 + 1.6 * (1 - abs(y - 40) / 90) - abs(x - 220) / 260
            cv.set(x, y, pick(WALL, v, x, y, 4))
    for px in range(14, W, 40):
        cv.vline(px, 18, 117, WALL[1])
        cv.vline(px + 1, 18, 117, WALL[4])
    for py in (18, 62, 100):
        cv.hline(0, W - 1, py, WALL[1])
        cv.hline(0, W - 1, py + 1, WALL[4])
    vgrad(cv, 0, 0, W, 18, ["#05080c", "#0a1016", "#101a22"])
    for lx in range(30, W, 70):
        cv.rect(lx, 12, lx + 40, 15, "#c8f0ff")
        cv.hline(lx, lx + 39, 15, "#6ab8d8")
        glow(cv, lx + 20, 18, 40, 22, "#9ae0ff", 0.3)
    wx0, wy0, wx1, wy1 = 210, 24, 306, 78
    win = m_rrect(wx0, wy0, wx1, wy1, 6)
    vgrad(cv, wx0, wy0, wx1, wy1, ["#01020a", "#030818", "#061028"])
    stars(cv, random.Random(3), wx0, wy0, wx1, wy1, 60, ("#2a3a5a", "#7a8aaa", "#ffffff"))
    planet = m_ellipse(276, 116, 64, 54)
    paint(cv, planet & win, ramp("#0a0c10", "#262018", "#4a3e2c", "#7a6648", "#a89070"),
          f_sphere(256, 76, 76, 5, 0.0, 1.4), None, 4)
    for x, y in edge(planet) & win:
        cv.set(x, y, "#c8b088")
    outside = m_rect(wx0 - 4, wy0 - 4, wx1 + 4, wy1 + 4) - win
    paint(cv, outside, SHELL, f_lin(wx0, wy0, wx1, wy1, 3.5, 1.4), SHELL_OL)
    for x, y in edge(win):
        cv.set(x, y, "#0a0e14")
    for k in range(6):
        cv.set(wx0 + 6 + k, wy0 + 3 + k, "#3a5a7a")
        cv.set(wx0 + 12 + k, wy0 + 3 + k, "#24405a")
    draw_text(cv, F5, 120, 28, "HYPERSLEEP", "#8aa8b8", shadow=WALL[0])
    draw_text(cv, F3, 120, 38, "BAY 2  -  CREW 7", WALL[4])
    cv.rect(120, 45, 180, 47, "#e8b020")
    for x in range(120, 180, 4):
        cv.rect(x, 45, x + 2, 47, "#202020")
    FLOOR = ramp("#03060a", "#070c12", "#0c141c", "#14202a", "#1e2e3a")
    for y in range(118, H):
        t = (y - 118) / (H - 118)
        for x in range(W):
            v = 3.2 - t * 2.6 + (0.6 if ((x - 210) * 6 // max(1, y - 110)) % 2 == 0 else 0)
            cv.set(x, y, pick(FLOOR, v, x, y, 4))
    cv.hline(0, W - 1, 118, "#5a7280")
    for ox, oy, s, lid, sl in ((112, 72, 1.5, 0, True), (196, 76, 1.5, 0, True), (134, 112, 2.4, 34, False)):
        L = 46
        glow(cv, ox + L * s / 2, oy + 20 * s, L * s * 0.7, 9 * s, "#3ad0f0", 0.35)
        pod_big(cv, ox, oy, s, L, lid, sl)
        y0 = int(oy + 21 * s)
        for x in range(int(ox + 4 * s), int(ox + (L - 4) * s)):
            for k in range(1, 7):
                if (x + k) % 2 == 0:
                    dmix(cv, x, y0 + k, "#3a8aa0", 0.4 * (1 - k / 7))
    cv.rect(96, 66, 110, 100, "#1a2028")
    cv.fill(edge(m_rect(96, 66, 110, 100)), "#05080c")
    for i, c in enumerate(("#5aff8a", "#5aff8a", "#ffb030", "#5aff8a", "#3ad0f0")):
        cv.rect(99, 70 + i * 5, 102, 72 + i * 5, c)
        cv.hline(104, 107, 71 + i * 5, "#3a4a58")
    vignette(cv, "#000000", 0.6)
    calm(cv, "#060a10", 0.4)
    return cv


def w_colony():
    cv = Canvas(W, H)
    vgrad(cv, 0, 0, W, 150, ["#020310", "#050822", "#0a1030", "#121a40", "#1e2450", "#30285a", "#4a2e5a"])
    rng = random.Random(10)
    stars(cv, rng, 0, 0, W, 110, 260, ("#2a2e5a", "#5a6090", "#b4bce8", "#ffffff"),
          density=lambda x, y: (1 - y / 120) * (0.4 if x < CALM_X else 1))
    # Ringed gas giant.
    gcx, gcy, gr = 262, 40, 24
    ring = m_ellipse(gcx, gcy, 44, 8) - m_ellipse(gcx, gcy, 32, 5)
    back = {(x, y) for x, y in ring if y < gcy}
    for x, y in back:
        cv.set(x, y, "#5a4a7a" if (x // 3) % 3 else "#3a3060")
    planet = m_ellipse(gcx, gcy, gr, gr)
    GG = ramp("#0a0818", "#1e1838", "#3a2e5a", "#6a4a7a", "#a4708a", "#e0a89a")
    for x, y in planet:
        nx, ny = (x + 0.5 - gcx) / gr, (y + 0.5 - gcy) / gr
        nz = math.sqrt(max(0, 1 - nx * nx - ny * ny))
        lit = max(0.0, -nx * 0.7 - ny * 0.4 + nz * 0.5)
        band = math.sin(ny * 13 + math.sin(nx * 4) * 0.8) * 0.5
        cv.set(x, y, pick(GG, lit * 5 + band, x, y, 4))
    for x, y in ring - back:
        if (x, y) in planet or True:
            cv.set(x, y, "#8a7aa8" if (x // 3) % 3 else "#5a4a7a")
    # Distant atmosphere processor with lights and a plume.
    puffs(cv, 209, 88, random.Random(4), ramp("#1a1a3e", "#2a2a54", "#3e3e6c", "#5a5a86", "#7a7aa0"), n=11, r0=2.5,
          grow=0.7, dx=-5, rise=-1.0, light=(0.4, 0.5))
    processor_big(cv, 178, 136, 1.2, ramp("#04050c", "#080a16", "#0e1020", "#1e2236"), rgba("#3a4270"),
                  rgba("#2a3058"), ("#ffd27a", "#ff9a5a"), seed=12)
    # Hills.
    far = ridge_fn(5, 16, 1.3)
    for x in range(W):
        h = int(140 - far(x))
        cv.vline(x, h, 150, "#0a0c1a")
        cv.set(x, h, "#1e2440")
    # Colony blocks with lit windows.
    WIN = ("#ffd27a", "#ffc060", "#ffe6a0", "#ff9a5a", "#9adcf0")
    blocks = [(84, 30, 14), (112, 26, 22), (140, 40, 30), (182, 22, 18), (206, 46, 40), (254, 28, 24),
              (284, 36, 34), (60, 22, 10)]
    ground = 168
    for bx, bw, bh in sorted(blocks, key=lambda b: -b[2]):
        blk = m_rect(bx, ground - bh, bx + bw, ground)
        for x, y in blk:
            cv.set(x, y, "#070914" if (x + y) % 9 else "#0b0e1c")
        for x, y in edge(blk):
            if (x, y - 1) not in blk:
                cv.set(x, y, "#2a3050")
            elif (x - 1, y) not in blk:
                cv.set(x, y, "#161a30")
        rr = random.Random(bx)
        for wy in range(ground - bh + 4, ground - 4, 5):
            for wx in range(bx + 3, bx + bw - 3, 5):
                if rr.random() < 0.55:
                    c = WIN[rr.randrange(len(WIN))]
                    cv.rect(wx, wy, wx + 3, wy + 2, c)
                    cv.hline(wx, wx + 2, wy + 2, mix(c, "#070914", 0.5))
                    glow(cv, wx + 1.5, wy + 1, 5, 4, c, 0.18)
        # Roof clutter.
        cv.rect(bx + 3, ground - bh - 2, bx + 7, ground - bh, "#0b0e1c")
        cv.set(bx + bw - 4, ground - bh - 3, "#2a3050")
        cv.vline(bx + bw - 4, ground - bh - 3, ground - bh - 1, "#161a30")
    # Hadley's Hope sign on the tall block.
    cv.rect(209, 130, 247, 136, "#101428")
    draw_text_c(cv, F3, 228, 131, "HADLEY'S HOPE", "#7af0ff")
    glow(cv, 228, 133, 28, 8, "#3ab0d0", 0.2)
    # Comm tower with a red beacon.
    tx = 268
    for y in range(80, ground - 24):
        w = (y - 80) // 12
        cv.set(tx - w, y, "#1e2440")
        cv.set(tx + w, y, "#1e2440")
        if y % 6 == 0:
            cv.hline(tx - w, tx + w, y, "#161a30")
    cv.set(tx, 78, "#ff3a2a")
    glow(cv, tx, 78, 4, 4, "#ff3a2a", 0.35)
    # Ground, walkway lamps and mist.
    vgrad(cv, 0, ground, W, H, ["#06070e", "#04050a", "#020306"])
    for lx in range(120, W, 34):
        cv.vline(lx, ground - 9, ground, "#1e2440")
        cv.set(lx, ground - 10, "#fff0c0")
        glow(cv, lx, ground - 2, 14, 6, "#ffb860", 0.35)
    for y in range(150, ground + 6):
        for x in range(W):
            m = 0.5 + 0.5 * math.sin(x * 0.04 + y * 0.3)
            dmix(cv, x, y, "#4a4e7a", 0.18 * m * (1 - abs(y - 160) / 14))
    calm(cv, "#04050e", 0.45)
    return cv


def w_pinup():
    cv = Canvas(W, H, "#f0e2c0")
    cx, cy = 224, 96
    RAYS = (rgba("#f4c46a"), rgba("#f0e2c0"))
    for y in range(H):
        for x in range(W):
            a = math.atan2(y + 0.5 - cy, x + 0.5 - cx) * 16 / (2 * math.pi)
            fade = smooth((x - 70) / 110)
            if int(math.floor(a)) % 2 == 0 and threshold(x, y, 4) < fade:
                cv.set(x, y, RAYS[0])
    halftone(cv, 0, 0, W, H, rgba("#e8a85a"), lambda x, y: max(0.0, 1 - math.hypot(x - cx, y - cy) / 120) *
             smooth((x - 90) / 60))
    # Poster frame.
    cv.fill(edge(m_rect(106, 6, 314, 174)), "#7a2a10")
    cv.fill(edge(m_rect(108, 8, 312, 172)), "#c04a1a")
    # Title.
    red = ramp("#7a1a08", "#b82a10", "#e44a20", "#ff7a3a", "#ffb070")
    ttl = "WORKING JOE"
    tw = text_width(BOLD, ttl, scale=2)
    ty = 14
    text_fill(cv, BOLD, cx - tw // 2, ty, ttl, lambda x, y: pick(red, 3.8 - (y - ty) / 18 * 3.2, x, y, 4), scale=2,
              outline=rgba("#fff6e0"), depth=2, depth_c=rgba("#3a1004"))
    draw_text_c(cv, F5, cx, ty + 22, "BY SEEGSON", "#2f5a9a", shadow="#fff6e0")
    joe_big(cv, cx - 4, 56, 3.1, 172)
    # Speech bubble.
    bub = m_rrect(122, 66, 176, 92, 6) | m_poly([(160, 90), (172, 100), (168, 90)])
    cv.fill(shift(bub, 1, 1), "#3a1004")
    paint(cv, bub, ramp("#d8ccae", "#f8f2e4", "#ffffff"), lambda x, y: 2 - (y - 66) / 40, "#3a1004")
    draw_text_c(cv, F3, 149, 71, "HOW CAN", "#3a1004")
    draw_text_c(cv, F3, 149, 77, "I HELP", "#3a1004")
    draw_text_c(cv, F3, 149, 83, "TODAY?", "#b82a10")
    # Ribbon tagline across the bottom of the poster.
    ribbon(cv, 132, 300, 150, 13, ramp("#4a0a04", "#8a1a08", "#c43010", "#e85a24", "#ff9a5a"), rgba("#2a0802"),
           "ALWAYS ON SHIFT!", F5, "#fff6e0")
    # Burst sticker.
    sticker(cv, 288, 116, 19, 15, 14, ramp("#7a5a08", "#c49412", "#f0c41e", "#ffe468", "#fff6c0"), rgba("#3a2a04"),
            [], turn=0.1, shadow=rgba("#3a1004"))
    draw_text_c(cv, F3, 288, 108, "ONLY", "#5a1004")
    draw_text_c(cv, F5, 289, 115, "$99", "#a01808")
    draw_text(cv, F3, 112, 11, "COLONY", "#7a2a10")
    draw_text(cv, F3, 112, 17, "APPROVED", "#7a2a10")
    calm(cv, "#d8c8a4", 0.5)
    return cv


def w_nebula():
    cv = Canvas(W, H)
    big = vnoise(51, 70, 4)
    mid = vnoise(52, 26, 3)
    wisp = vnoise(53, 40, 2)
    NEB = ramp("#05030f", "#0e0620", "#1e0a36", "#34104c", "#561660", "#82226c", "#b23e72", "#e0727e", "#ffb8a4")
    TEAL = ramp("#05030f", "#041420", "#072838", "#0c4450", "#186a6c", "#3a9a90")
    for y in range(H):
        for x in range(W):
            n = big(x, y) * 0.65 + mid(x, y) * 0.35
            side = 0.35 + 0.65 * smooth((x - 50) / 170)
            # Filaments: ridges where the wisp field crosses one half.
            fil = max(0.0, 1 - abs(wisp(x * 1.2, y * 0.8) - 0.5) * 9)
            v = max(0.0, n - 0.36) * 16 * side + fil * 1.6 * side
            t = max(0.0, 0.5 - n) * 12 * side
            if t > v and t > 0.3:
                cv.set(x, y, pick(TEAL, t, x, y, 4))
            else:
                cv.set(x, y, pick(NEB, v, x, y, 4))
    # The xenomorph's head, a darker shape in the cloud with soft edges and a faint rim of nebula light.
    head = [(142, 108), (146, 100), (152, 93), (160, 87), (172, 81), (186, 76), (204, 71), (224, 66), (246, 62),
            (266, 59), (284, 58), (298, 60), (307, 65), (310, 72), (305, 78), (294, 82), (278, 87), (262, 93),
            (250, 101), (243, 112), (240, 126), (240, 142), (244, 158), (216, 162), (211, 146), (206, 134),
            (196, 126), (182, 121), (168, 117), (156, 114), (146, 112)]
    hm = m_poly(head)
    tubes = set()
    for tx, ty, ex, ey in ((254, 104, 286, 92), (258, 118, 294, 110), (260, 132, 298, 128), (262, 146, 296, 148)):
        tubes |= m_capsule(tx, ty, ex, ey, 3.4)
    xeno = hm | tubes
    mask = Image.new("L", (W, H), 0)
    mp = mask.load()
    for x, y in xeno:
        if 0 <= x < W and 0 <= y < H:
            mp[x, y] = 255
    soft = mask.filter(ImageFilter.GaussianBlur(2.5)).load()
    for y in range(H):
        for x in range(W):
            a = soft[x, y] / 255
            if a > 0.02:
                dmix(cv, x, y, "#030108", 0.68 * a)
    for x, y in edge(xeno):
        if cv.inside(x, y) and ((x, y - 1) not in xeno or (x - 1, y) not in xeno):
            dmix(cv, x, y, "#f08aa8", 0.4)
    # Dome ribs, lip and teeth, barely there.
    for k in range(4):
        for x in range(196 + k * 10, 300 - k * 6, 2):
            yy = int(76 + k * 4 - (x - 196) * 0.12 + ((x - 196) / 104) ** 2 * 6)
            if (x, yy) in hm and (x, yy) not in edge(hm):
                dmix(cv, x, yy, "#5a2a5a", 0.6)
    for x in range(147, 190, 2):
        y = int(110 - (x - 146) * 0.12)
        dmix(cv, x, y, "#c8a0b8", 0.55)
        dmix(cv, x, y + 1, "#7a5a7a", 0.4)
    rng = random.Random(12)
    stars(cv, rng, 0, 0, W, H, 300, ("#3a2a4a", "#7a6a9a", "#e0d4ff", "#ffffff"),
          density=lambda x, y: (0.12 if (x, y) in xeno else 1) * (0.5 if x < CALM_X else 1))
    for sx, sy in ((126, 30), (300, 168), (200, 22)):
        sparkle(cv, sx, sy, 4, WHITE, "#ffc8e0", "#7a3a6a")
    calm(cv, "#04020a", 0.4)
    return cv


def pod_big(cv, ox, oy, s, L=46, lid=0.0, sleeper=True):
    """cryo_pod scaled by s."""
    S = lambda v: v * s
    R = lambda v: int(round(v))
    for i, c in enumerate(("#0f4a5c", "#0b3442")):
        cv.fill(m_ellipse(ox + S(L / 2), oy + S(20.5), S(L / 2 - 1 - i * 4), S(1.6 - i * 0.3)), c)
    plinth = m_rect(R(ox + S(4)), R(oy + S(16)), R(ox + S(L - 4)), R(oy + S(20)))
    paint(cv, plinth, METAL, f_hcyl(oy + S(16), oy + S(20), len(METAL), 0.2, 0.5), METAL[0])
    for x in range(R(ox + S(6)), R(ox + S(L - 6)), R(S(4))):
        cv.rect(x, R(oy + S(18)) - 1, x + 2, R(oy + S(18)) + 1, "#3ad0f0")
    shell = m_rrect(R(ox + S(1)), R(oy + S(8)), R(ox + S(L - 1)), R(oy + S(17)), S(4))
    paint(cv, shell, SHELL, f_hcyl(oy + S(8), oy + S(17), len(SHELL), 0.25, 0.6), SHELL_OL, dither=4)
    for k, c in ((14, SHELL[2]), (15, SHELL[1])):
        for d in range(R(s)):
            cv.hline(R(ox + S(4)), R(ox + S(L - 5)), R(oy + S(k)) + d, c)
    cv.rect(R(ox + S(L - 9)), R(oy + S(11)), R(ox + S(L - 4)), R(oy + S(13)), "#1d2229")
    cv.rect(R(ox + S(L - 8)), R(oy + S(11.5)), R(ox + S(L - 7)), R(oy + S(12.5)), "#5aff8a")
    cv.rect(R(ox + S(L - 6)), R(oy + S(11.5)), R(ox + S(L - 5)), R(oy + S(12.5)), "#ffb030")
    for vx in range(R(ox + S(4)), R(ox + S(9)), R(S(2))):
        cv.vline(vx, R(oy + S(11)), R(oy + S(13)), SHELL[2])
    c0, c1, base = ox + S(4), ox + S(L - 6), oy + S(10)
    hinge = (c0 + 0.5, base)
    a = math.radians(lid)
    Hh = S(6 + L * 0.08)
    pts = []
    for k in range(41):
        t = k / 40
        pts.append((c0 + (c1 - c0) * t, base - Hh * math.sin(math.pi * t) ** 0.55))
    rot = [(hinge[0] + (px - hinge[0]) * math.cos(a) + (py - hinge[1]) * math.sin(a),
            hinge[1] - (px - hinge[0]) * math.sin(a) + (py - hinge[1]) * math.cos(a)) for px, py in pts]
    canopy = m_poly(rot)
    head = m_ellipse(ox + S(L * 0.24), oy + S(6.6), S(2.4), S(2.3))
    body = m_capsule(ox + S(L * 0.24 + 3.5), oy + S(7.4), ox + S(L * 0.76), oy + S(8), S(2.1))
    cv.fill(m_rect(R(c0 + 1), R(oy + S(8)), R(c1), R(oy + S(10))), "#0a1a22")
    if sleeper:
        cv.fill(m_rect(R(ox + S(L * 0.24 - 3)), R(oy + S(8)), R(ox + S(L * 0.24 + 2)), R(oy + S(10))), SHELL[3])
        paint(cv, head, FLESH, f_sphere(ox + S(L * 0.24 - 1), oy + S(5.8), S(3.2), len(FLESH)), FLESH[0])
        for x, y in head:
            if y < oy + S(6) and x < ox + S(L * 0.24):
                cv.set(x, y, "#3a2a1e")
        paint(cv, body, SHELL, f_hcyl(oy + S(6), oy + S(11), len(SHELL), 0.3, 1.5), SHELL_OL)
    if lid > 0:
        lo = min(y for _, y in canopy)
        paint(cv, canopy, GLASS, lambda x, y: 4.2 - (y - lo) / s * 0.4, GLASS[0], soft=GLASS[1], dither=4)
    else:
        for x, y in canopy:
            v = 4.0 - (y - (base - Hh)) / Hh * 3.0
            under = cv.get(x, y)
            g = pick(GLASS, v, x, y, 4)
            inside = sleeper and ((x, y) in head or (x, y) in body or y >= oy + S(8))
            cv.set(x, y, mix(under, g, 0.3) if inside else g)
        for x, y in edge(canopy):
            cv.set(x, y, GLASS[0])
        gx = int(c0 + (c1 - c0) * 0.3)
        for i in range(R(S(5))):
            cv.set(gx + i, int(base - Hh + S(1.2)), GLASS[4])
    for d in range(max(1, R(s / 2))):
        cv.hline(R(ox + S(3)), R(ox + S(L - 4)), R(oy + S(10)) + d, SHELL_OL)


WALLPAPERS = [
    ("synthwave sunset over a colony atmosphere processor", w_synthwave),
    ("LV-426 storm plains", w_storm),
    ("Weyland-Yutani corporate logo", w_weyland),
    ("Seegson Working Joe blueprint", w_blueprint),
    ("starfield with a passing Conestoga-class ship", w_starship),
    ("Gateway Station in Earth orbit", w_gateway),
    ("CRT test card", w_testcard),
    ("vector wireframe landscape", w_vector),
    ("hypersleep bay", w_hypersleep),
    ("colony at night", w_colony),
    ("Working Joe pinup poster", w_pinup),
    ("deep space nebula with a xenomorph in the clouds", w_nebula),
]


# -- Animated states (64x64) -------------------------------------------------------------------------
A = 64


def rot3(p, ax, ay, az=0.0):
    x, y, z = p
    cy_, sy_ = math.cos(ay), math.sin(ay)
    x, z = x * cy_ + z * sy_, -x * sy_ + z * cy_
    cx_, sx_ = math.cos(ax), math.sin(ax)
    y, z = y * cx_ - z * sx_, y * sx_ + z * cx_
    cz_, sz_ = math.cos(az), math.sin(az)
    x, y = x * cz_ - y * sz_, x * sz_ + y * cz_
    return x, y, z


def a_cube(i, n):
    cv = Canvas(A, A)
    bob = math.sin(i / n * 2 * math.pi) * 1.5
    cv.fill(m_ellipse(32, 57, 13 - bob, 2.4), (20, 10, 40, 140))
    cv.fill(m_ellipse(32, 57, 8 - bob, 1.4), (20, 10, 40, 160))
    verts = [(x, y, z) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
    edges_ = [(a, b) for a in range(8) for b in range(a + 1, 8)
              if sum(1 for k in range(3) if verts[a][k] != verts[b][k]) == 1]
    ang = i / n * math.pi / 2
    P = []
    for v in verts:
        x, y, z = rot3(v, 0.5, ang + 0.3)
        z += 4.2
        P.append((32 + x * 64 / z, 28 + bob + y * 64 / z, z))
    C = ramp("#2a0a5a", "#5a1a9a", "#a02ad0", "#2ac8f0", "#9af6ff", "#ffffff")
    zs = [p[2] for p in P]
    zmin, zmax = min(zs), max(zs)
    order = sorted(edges_, key=lambda e: -(P[e[0]][2] + P[e[1]][2]))
    # Faint faces first so the cube reads as solid glass.
    for a_, b_ in order:
        zf = ((P[a_][2] + P[b_][2]) / 2 - zmin) / max(0.01, zmax - zmin)
        c = C[min(5, int((1 - zf) * 4.6))]
        if zf > 0.55:
            cv.line(P[a_][0], P[a_][1], P[b_][0], P[b_][1], c)
        else:
            for d in ((1, 0), (0, 1)):
                cv.line(P[a_][0] + d[0], P[a_][1] + d[1], P[b_][0] + d[0], P[b_][1] + d[1], C[2])
            cv.line(P[a_][0], P[a_][1], P[b_][0], P[b_][1], c)
    for x, y, z in P:
        zf = (z - zmin) / max(0.01, zmax - zmin)
        if zf < 0.5:
            cv.rect(int(x) - 1, int(y) - 1, int(x) + 1, int(y) + 1, WHITE)
        else:
            cv.set(int(x), int(y), C[3])
    return cv


def a_new(i, n):
    cv = Canvas(A, A)
    t = i / n
    h = abs(math.sin(t * math.pi))
    up = h * 14
    squash = 1.0 if h > 0.25 else 1.0 + (0.25 - h) * 1.0
    cy = 34 - up + (squash - 1) * 8
    sx, sy = 1.0 * squash, 1.0 / squash
    cv.fill(m_ellipse(32, 60, 14 - up * 0.4, 2), (40, 10, 10, 120))
    burst = set()
    outer, inner, pts, turn = 23, 18, 14, t * 0.9
    for y in range(int(cy - 28), int(cy + 28)):
        for x in range(4, 60):
            dx, dy = (x + 0.5 - 32) / sx, (y + 0.5 - cy) / sy
            a = (math.atan2(dy, dx) + turn) * pts / (2 * math.pi)
            f = a - math.floor(a)
            tri = 1 - abs(f - 0.5) * 2
            if math.hypot(dx, dy) <= outer - (outer - inner) * tri:
                burst.add((x, y))
    cv.fill(shift(burst, 1, 1), rgba("#4a0a04"))
    paint(cv, burst, CAN_RED, f_sphere(28, cy - 6, 28, len(CAN_RED), 0.3), rgba("#3a0404"))
    cv.fill(edge(m_ellipse(32, cy, 15 * sx, 15 * sy)), CAN_RED[4])
    flash = i % 2 == 0
    fill = bands((3, rgba("#fff6c0")), (3, rgba("#ffe468")), (3, rgba("#f0c41e"))) if flash else \
        bands((3, rgba("#ffffff")), (3, rgba("#fff6c0")), (3, rgba("#ffe468")))
    tx = 32 - text_width(BOLD, "NEW!") // 2
    draw_text(cv, BOLD, tx, int(cy) - 4, "NEW!", fill, outline=rgba("#3a0404"))
    if flash:
        for sx_, sy_ in ((10, int(cy) - 18), (54, int(cy) + 12)):
            sparkle(cv, sx_, sy_, 2, WHITE, "#ffe468", "#c49412")
    else:
        sparkle(cv, 52, int(cy) - 16, 2, WHITE, "#ffe468", "#c49412")
    return cv


def mini_joe(cv, hx, floor, lean, theta, lift):
    """A 20 pixel Working Joe in side view facing right, digging. theta is the shovel angle from vertical."""
    # Legs.
    hip = (hx, floor - 8)
    for lx, c in ((hx - 2, SUIT[1]), (hx + 2, SUIT[2])):
        cv.fill(m_capsule(hip[0], hip[1], lx, floor - 1, 1.3), c)
        cv.rect(lx - 1, floor - 1, lx + 3, floor + 1, "#1a1c22")
    sh = (hx + lean, floor - 15)
    torso = m_capsule(hip[0], hip[1], sh[0], sh[1], 3.2)
    paint(cv, torso, SUIT, f_cyl(hx - 4, hx + 4 + lean, len(SUIT), 0.3, 0.8), SUIT_OL)
    # Shovel along theta, hands on it.
    gx, gy = sh[0] + 6 + math.sin(theta) * 2, sh[1] + 5 - lift
    ex, ey = gx + math.sin(theta) * 12, gy + math.cos(theta) * 12
    sx0, sy0 = gx - math.sin(theta) * 6, gy - math.cos(theta) * 6
    cv.line(sx0, sy0, ex, ey, "#8a5a2a")
    cv.line(sx0 + 1, sy0, ex + 1, ey, "#5a3418")
    cv.hline(int(sx0) - 1, int(sx0) + 1, int(sy0), "#2a1a0a")
    blade = m_poly([(ex - math.cos(theta) * 3, ey + math.sin(theta) * 3),
                    (ex + math.cos(theta) * 3, ey - math.sin(theta) * 3),
                    (ex + math.cos(theta) * 2 + math.sin(theta) * 5, ey - math.sin(theta) * 2 + math.cos(theta) * 5),
                    (ex - math.cos(theta) * 2 + math.sin(theta) * 5, ey + math.sin(theta) * 2 + math.cos(theta) * 5)])
    paint(cv, blade, METAL, lambda x, y: 3.4, METAL[0])
    for hx2, hy2 in ((sx0 + (ex - sx0) * 0.25, sy0 + (ey - sy0) * 0.25), (sx0 + (ex - sx0) * 0.6, sy0 + (ey - sy0) * 0.6)):
        cv.fill(m_capsule(sh[0], sh[1] + 1, hx2, hy2, 1.1), SUIT[3])
        cv.fill(m_ellipse(hx2, hy2, 1.4, 1.4), SKIN[3])
    head = m_ellipse(sh[0] + 1.5, sh[1] - 4.5, 3.6, 4.2)
    paint(cv, head, SKIN, f_sphere(sh[0], sh[1] - 6, 5, len(SKIN), 0.2), SKIN_OL)
    cv.set(int(sh[0] + 3), int(sh[1] - 5), GLOW[3])
    cv.set(int(sh[0] + 4), int(sh[1] - 5), GLOW[2])
    cv.set(int(sh[0] - 1), int(sh[1] - 4), SKIN[1])


def a_construction(i, n):
    cv = Canvas(A, A)
    # Sign board on posts.
    for px in (10, 52):
        cv.rect(px, 18, px + 2, 36, "#5a3418")
        cv.vline(px, 18, 35, "#8a5a2a")
    board = m_rect(2, 1, 62, 21)
    cv.fill(shift(board, 1, 1), SHADOW)
    for x, y in board:
        cv.set(x, y, "#f0c41e" if (x, y) not in edge(board) else "#1a1408")
    for x in range(3, 61):
        for y in (2, 3, 18, 19):
            if (x + y) % 6 < 3:
                cv.set(x, y, "#1a1408")
    draw_text_c(cv, F3, 32, 5, "UNDER", "#1a1408")
    draw_text_c(cv, F3, 32, 11, "CONSTRUCTION", "#1a1408")
    # Warning lamp blinking on the board.
    on = i % 4 < 2
    cv.fill(m_ellipse(58.5, 1.5, 2.5, 2.5), "#ff6a00" if on else "#7a2a00")
    if on:
        cv.set(58, 0, "#fff2c0")
        glow(cv, 58.5, 1.5, 7, 7, "#ffb030", 0.5)
    # Ground, dirt pile and the hole.
    floor = 54
    cv.rect(0, floor, A, 58, "#5e3016")
    cv.hline(0, A - 1, floor, "#8a4a22")
    pile_h = 6 + (1 if i >= 6 else 0)
    pile = m_ellipse(50, floor, 11, pile_h)
    paint(cv, pile, BROWN, f_sphere(47, floor - 4, 12, len(BROWN), 0.2), BROWN[0])
    for x, y in pile:
        if (x * 7 + y * 3) % 11 == 0:
            cv.set(x, y, BROWN[1])
    cv.fill(m_ellipse(30, floor + 1, 6, 2), "#1e0f08")
    # Hazard strip at the foot.
    for x in range(A):
        for y in range(58, 64):
            cv.set(x, y, "#f0c41e" if (x + y) // 3 % 2 else "#1a1408")
    # Pose: dig in, lever, lift, toss.
    poses = [(3, 0.55, 0), (4, 0.35, -2), (4, 0.1, -3), (2, 0.4, 0), (0, 0.9, 3), (-1, 1.3, 5), (0, 1.1, 3),
             (2, 0.8, 1)]
    lean, theta, lift = poses[i % len(poses)]
    mini_joe(cv, 18, floor, lean, theta, lift)
    # Flying dirt clods.
    if i >= 4:
        k = i - 4
        for j, (vx, vy) in enumerate(((3.6, -3.0), (4.6, -2.0), (2.8, -3.6))):
            x = 34 + vx * k * 1.6
            y = floor - 18 + vy * k * 1.6 + 0.9 * (k * 1.6) ** 2
            if y < floor - 1:
                cv.rect(int(x), int(y), int(x) + 2, int(y) + 2, BROWN[2 + j % 2])
                cv.set(int(x), int(y), BROWN[4])
    return cv


def tri_noise(x, y, z, seed):
    r = random.Random(seed)
    v = 0.0
    for k in range(7):
        f = 1.4 * 1.9 ** (k * 0.6)
        a, b, c, p = (r.uniform(-1, 1) for _ in range(4))
        ll = math.sqrt(a * a + b * b + c * c) or 1
        v += math.sin((a * x + b * y + c * z) / ll * f * 3 + p * 6) / (1 + k * 0.7)
    return v


def a_globe(i, n):
    cv = Canvas(A, A)
    cx, cy, r = 32, 32, 25
    rot = i / n * 2 * math.pi
    ROCK = ramp("#0e0c0a", "#221e18", "#3a3428", "#56503e", "#787058", "#a09878", "#c8c0a0")
    STORM = rgba("#a8c4c4")
    # Cache the noise per pixel lookup through a coarse closure.
    for y in range(cy - r - 1, cy + r + 1):
        for x in range(cx - r - 1, cx + r + 1):
            nx, ny = (x + 0.5 - cx) / r, (y + 0.5 - cy) / r
            d = nx * nx + ny * ny
            if d > 1:
                continue
            nz = math.sqrt(1 - d)
            lit = max(0.0, nx * -0.6 + ny * -0.45 + nz * 0.66)
            # Tilted axis, then spin.
            tx, ty, tz = rot3((nx, ny, nz), 0.35, 0)
            lon = math.atan2(tx, tz) + rot
            lat = math.asin(max(-1, min(1, ty)))
            px, py, pz = math.cos(lat) * math.sin(lon), math.sin(lat), math.cos(lat) * math.cos(lon)
            h = tri_noise(px, py, pz, 3)
            storm = math.sin(lat * 9 + tri_noise(px, py, pz, 5) * 1.6)
            v = 2.6 + h * 1.2
            c = pick(ROCK, v * (0.25 + 0.95 * lit), x, y, 4)
            if storm > 0.7:
                c = mix(c, STORM, (storm - 0.7) * 2.2 * (0.2 + 0.8 * lit))
            cv.set(x, y, c)
    disc = m_ellipse(cx, cy, r, r)
    for x, y in halo(disc, False):
        nx, ny = (x + 0.5 - cx) / r, (y + 0.5 - cy) / r
        lit = nx * -0.6 + ny * -0.45
        if lit > 0:
            cv.set(x, y, mix("#2a4a50", "#9ae6e0", min(1.0, lit * 1.6)))
    # Terminator shading is built in; add a few stars that twinkle in turn.
    for k, (sx, sy) in enumerate(((5, 6), (58, 10), (8, 55), (56, 54), (60, 30))):
        if (i + k) % 3:
            cv.set(sx, sy, "#c8d4f0")
        else:
            sparkle(cv, sx, sy, 1, WHITE, "#7a8aaa", "#3a4a6a")
    return cv


def a_counter(i, n):
    cv = Canvas(A, A)
    vals = [4197, None, 4198, None, 4199, None, 4200, None, 4201, None, 4202, None]
    v = vals[i]
    frac = 0.0
    if v is None:
        v = vals[i - 1]
        frac = 0.5
    # Bezel.
    plate = m_rrect(1, 12, 63, 50, 3)
    cv.fill(shift(plate, 1, 1), SHADOW)
    paint(cv, plate, METAL, f_lin(0, 12, 0, 50, 4.2, 1.6), METAL[0])
    draw_text_c(cv, F3, 32, 15, "VISITORS", "#1a1c21")
    draw_text_c(cv, F3, 32, 43, "SINCE 2179", METAL[1])
    digits = 6
    wx = 7
    ww = 8
    y0, y1 = 22, 40
    nxt = v + 1
    for k in range(digits):
        place = 10 ** (digits - 1 - k)
        d = (v // place) % 10
        d2 = (nxt // place) % 10
        rolling = frac > 0 and d != d2
        x0 = wx + k * (ww + 1)
        last = k == digits - 1
        bg = ramp("#000000", "#0e0e10", "#24242a", "#3a3a44") if not last else ramp("#100000", "#2a0606", "#4a0c0c",
                                                                                      "#6a1414")
        for x in range(x0, x0 + ww):
            for y in range(y0, y1):
                t = (y - y0) / (y1 - y0 - 1)
                cv.set(x, y, pick(bg, 3.2 - abs(t - 0.45) * 6, x, y, 2))
        off = int(round(frac * 11)) if rolling else 0
        for dd, dy in ((d, -off), (d2, 11 - off)):
            ym = y0 + 5 + dy
            mask = shift(text_mask(F5, str(dd)), x0 + 2 if dd != 1 else x0 + 3, ym)
            for x, y in mask:
                if y0 + 1 <= y < y1 - 1:
                    t = (y - y0) / (y1 - y0)
                    cv.set(x, y, mix("#ffffff", "#7a7a80", abs(t - 0.45) * 1.8))
        cv.hline(x0, x0 + ww - 1, y0, "#000000")
        cv.hline(x0, x0 + ww - 1, y1 - 1, "#000000")
    cv.fill(edge(m_rect(wx - 1, y0 - 1, wx + digits * (ww + 1), y1 + 1)), "#1a1c21")
    # Glass glint.
    cv.hline(wx, wx + 10, y0 + 1, (255, 255, 255, 90))
    return cv


FIRE = ramp("#3a0402", "#7a0e02", "#c02a04", "#ec5a0a", "#ff9a1e", "#ffd25a", "#fff4c0", "#ffffff")


def a_flame(i, n):
    cv = Canvas(A, A)
    t = i / n * 2 * math.pi
    base = 50
    # Fire: per column height from looping waves, per pixel heat with flicker; tiles horizontally.
    for x in range(A):
        u = x / A * 2 * math.pi
        hgt = (20 + 7 * math.sin(u * 3 + t) + 5 * math.sin(u * 5 - 2 * t + 1) + 4 * math.sin(u * 8 + 3 * t)
               + 3 * math.sin(u * 2 - t + 2))
        for y in range(0, base):
            h = (base - y) / max(1.0, hgt)
            wob = 0.12 * math.sin(y * 0.5 + u * 7 - t * 2) + 0.08 * math.sin(y * 0.9 - u * 11 + t * 3)
            heat = 1 - h + wob
            if heat <= 0.05:
                continue
            v = heat * 9.0 - 0.6
            cv.set(x, y, pick(FIRE, v, x, y, 4))
    # Embers rising in a loop.
    rng = random.Random(3)
    for k in range(9):
        ex = rng.randrange(A)
        sp = rng.randrange(1, 3)
        ph = rng.random()
        y = base - 12 - ((i / n + ph) % 1) * 36 * sp / 2
        x = ex + int(math.sin(t + k) * 2)
        if 0 <= y < base - 10:
            cv.set(x % A, int(y), "#ffd25a" if k % 2 else "#ff9a1e")
    # The divider bar the fire burns along.
    bar = m_rect(0, base, A, base + 6)
    paint(cv, bar, GOLD, f_hcyl(base, base + 6, len(GOLD), 0.3, 0.6), None)
    cv.hline(0, A - 1, base, "#fff8d8")
    cv.hline(0, A - 1, base + 6, "#2a1804")
    for x in range(4, A, 16):
        cv.set(x, base + 3, "#4a2c06")
        cv.set(x + 1, base + 2, "#fff8d8")
    return cv


def a_robot(i, n):
    cv = Canvas(A, A)
    t = i / n * 2 * math.pi
    bob = int(round(abs(math.sin(t)) * -3))
    sway = int(round(math.sin(t) * 2))
    floor = 58
    cv.fill(m_ellipse(32, floor + 2, 12, 2), (0, 0, 0, 120))
    # Disco floor tiles.
    for k, x in enumerate(range(8, 56, 8)):
        on = (k + i) % 3 == 0
        cv.rect(x, floor + 1, x + 7, floor + 4, ("#ff4a9a" if k % 2 else "#4ad0ff") if on else "#20202a")
    # Legs with bending knees.
    kneel = int(round(abs(math.sin(t)) * 2))
    for lx, ph in ((26, 0), (37, math.pi)):
        knee_x = lx + sway + (1 if (i % 2) == (ph > 0) else 0)
        cv.fill(m_capsule(lx + sway, 42 + bob, knee_x - kneel, 50, 1.8), METAL[2])
        cv.fill(m_capsule(knee_x - kneel, 50, lx, floor - 1, 1.8), METAL[3])
        foot = m_rrect(lx - 4, floor - 2, lx + 4, floor + 1, 1)
        paint(cv, foot, CAN_RED, lambda x, y: 3, CAN_RED[0])
    # Body.
    bx, by = 22 + sway, 26 + bob
    body = m_rrect(bx, by, bx + 21, by + 17, 2)
    paint(cv, body, SHELL, f_lin(bx, by, bx + 21, by + 17, 4.6, 1.8), SHELL_OL)
    panel = m_rect(bx + 4, by + 4, bx + 17, by + 12)
    cv.fill(panel, "#1a1c22")
    for k in range(4):
        on = (i + k) % 4 < 2
        cv.rect(bx + 5 + k * 3, by + 5, bx + 7 + k * 3, by + 7, ("#ff3a2a", "#5aff8a", "#ffd25a", "#3ad0f0")[k]
                if on else "#30343a")
    for k in range(3):
        cv.hline(bx + 5, bx + 15, by + 9 + k, "#40444b" if (k + i) % 2 else "#2a2d33")
    # Arms: angles per frame for a disco point and a wave.
    poses = [(-2.4, -1.2, 0.9, 1.6), (-2.0, -0.6, 1.3, 2.2), (-1.0, 0.2, 2.4, 1.2), (-0.4, 0.8, 2.0, 0.4),
             (-0.9, -0.2, 2.6, 3.4), (-1.6, -1.0, 1.8, 2.6), (-2.6, -2.0, 1.0, 1.2), (-2.2, -1.6, 0.6, 0.0)]
    a1, a2, b1, b2 = poses[i % len(poses)]
    for side, (u, f) in ((-1, (a1, a2)), (1, (b1, b2))):
        sx, sy = (bx + (0 if side < 0 else 21)), by + 4
        ex = sx + side * abs(math.cos(u)) * 7
        ey = sy + math.sin(u if side < 0 else -u) * 7
        hx = ex + side * abs(math.cos(f)) * 7
        hy = ey + math.sin(f if side < 0 else -f) * 7
        cv.fill(m_capsule(sx, sy, ex, ey, 1.8), METAL[3])
        cv.fill(m_capsule(ex, ey, hx, hy, 1.6), METAL[4])
        cv.fill(m_ellipse(ex, ey, 2, 2), METAL[1])
        claw = m_ellipse(hx, hy, 2.6, 2.6) - m_ellipse(hx + side * 1.4, hy - 1.2, 1.4, 1.4)
        paint(cv, claw, CAN_RED, lambda x, y: 3.5, CAN_RED[0])
    # Head with antenna.
    hx0, hy0 = bx + 3 + (1 if i % 4 in (1, 2) else -1 if i % 4 == 3 else 0), by - 13
    neck = m_rect(bx + 9, by - 3, bx + 13, by + 1)
    cv.fill(neck, METAL[1])
    head = m_rrect(hx0, hy0, hx0 + 16, hy0 + 11, 2)
    paint(cv, head, SHELL, f_lin(hx0, hy0, hx0 + 16, hy0 + 11, 4.6, 2.0), SHELL_OL)
    for ex in (hx0 + 4, hx0 + 11):
        cv.fill(m_ellipse(ex + 0.5, hy0 + 4.5, 2.2, 2.2), "#101216")
        cv.fill(m_ellipse(ex + 0.5, hy0 + 4.5, 1.2, 1.2), "#3ad0f0" if i % 8 != 5 else "#101216")
        cv.set(ex, hy0 + 4, "#e6ffff" if i % 8 != 5 else "#101216")
    cv.rect(hx0 + 4, hy0 + 8, hx0 + 13, hy0 + 10, "#101216")
    for k in range(hx0 + 5, hx0 + 13, 2):
        cv.set(k, hy0 + 8 + (1 if (k // 2 + i) % 2 else 0), "#ff6a3a")
    cv.vline(hx0 + 8, hy0 - 5, hy0 - 1, METAL[3])
    cv.fill(m_ellipse(hx0 + 8.5, hy0 - 5.5, 1.8, 1.8), "#ff3a2a" if i % 2 == 0 else "#ffd25a")
    if i % 2 == 0:
        glow(cv, hx0 + 8.5, hy0 - 5.5, 5, 5, "#ff6a5a", 0.5)
    # Music notes.
    for k, (nx, ny) in enumerate(((6, 14), (54, 8))):
        yy = ny + ((i + k * 3) % 4)
        bitmap(cv, nx, yy, [".##", ".#.", ".#.", "##.", "##."], {"#": "#ff4a9a" if k else "#4ad0ff"})
    return cv


def a_envelope(i, n):
    cv = Canvas(A, A)
    t = i / n * 2 * math.pi
    bob = math.sin(t) * 3
    flap = math.cos(t)
    ex0, ey0 = 17, int(round(26 + bob))
    ew, eh = 30, 20
    cv.fill(m_ellipse(32, 60, 12 - bob, 1.8), (0, 0, 0, 90))
    # Wings: three feather tiers that sweep with flap.
    for side in (-1, 1):
        root = (32 + side * 13, ey0 + 6)
        for k in range(3):
            ang = (0.35 + flap * 0.75) - k * 0.32
            ln = 18 - k * 4
            tip = (root[0] + side * math.cos(ang) * ln, root[1] - math.sin(ang) * ln)
            wing = m_capsule(root[0], root[1] + k * 2, tip[0], tip[1], 3.4 - k * 0.6)
            cv.fill(halo(wing), "#4a5a6a")
            paint(cv, wing, ramp("#9aa8b8", "#c8d4e0", "#eef4fa", "#ffffff"), lambda x, y, k=k: 3 - k * 0.7, None)
        # Feather separations.
        for k in range(3):
            ang = (0.35 + flap * 0.75) - k * 0.32
            ln = 18 - k * 4
            cv.set(int(root[0] + side * math.cos(ang) * (ln - 2)), int(root[1] - math.sin(ang) * (ln - 2) + 2), "#7a8898")
    body = m_rect(ex0, ey0, ex0 + ew, ey0 + eh)
    paint(cv, body, ramp("#a89c80", "#d8ccae", "#f0e8d0", "#fffaf0"), f_lin(ex0, ey0, ex0 + ew, ey0 + eh, 3.2, 1.6),
          rgba("#3e352a"))
    for k in range(ew // 2):
        cv.set(ex0 + 1 + k, ey0 + eh - 2 - int(k * (eh - 4) / (ew / 2)), "#b6a98c")
        cv.set(ex0 + ew - 2 - k, ey0 + eh - 2 - int(k * (eh - 4) / (ew / 2)), "#b6a98c")
    fl = m_poly([(ex0, ey0), (ex0 + ew, ey0), (ex0 + ew / 2, ey0 + eh * 0.6)])
    paint(cv, fl, ramp("#b6a98c", "#d8ccae", "#f0e8d0"), lambda x, y: 1.6 - (y - ey0) / eh * 1.4, rgba("#3e352a"))
    seal = m_ellipse(ex0 + ew / 2, ey0 + eh * 0.58, 3, 3)
    paint(cv, seal, CAN_RED, f_sphere(ex0 + ew / 2 - 1, ey0 + eh * 0.5, 4, len(CAN_RED)), CAN_RED[0])
    # Speed lines and a "1" badge popping.
    for k in range(3):
        y = ey0 + 6 + k * 5
        cv.hline(2 + (i + k) % 3, 9 + (i + k) % 3, y, "#9aa8b8")
    badge = m_ellipse(48, ey0 - 2, 5, 5)
    paint(cv, badge, CAN_RED, f_sphere(46, ey0 - 4, 6, len(CAN_RED), 0.3), rgba("#3a0404"))
    draw_text_c(cv, F5, 48, ey0 - 5, "1", WHITE)
    return cv


def hourglass_frame(fill):
    """fill 0..1 is how much sand has run into the bottom bulb."""
    cv = Canvas(A, A)
    WOOD = ramp("#2a1408", "#5a2e14", "#8a4a22", "#b86e36", "#e09a5a")
    cx = 32
    def bulb_half(y):
        # Half width of the glass at row y (8..56), pinched at 32.
        d = abs(y - 32) / 22
        return 2 + 12 * math.sin(min(1.0, d) * math.pi / 2) ** 0.8
    glass = {(x, y) for y in range(9, 56) for x in range(A) if abs(x + 0.5 - cx) <= bulb_half(y)}
    top_sand = set()
    bot_sand = set()
    top_level = 13 + fill * 19
    bot_level = 54 - (1 - (1 - fill) ** 0.8) * 20
    for x, y in glass:
        if y < 32 and y >= top_level:
            top_sand.add((x, y))
        if y > 32 and y >= bot_level - max(0, 4 - abs(x - cx)) * (0.8 if fill > 0.05 else 0):
            bot_sand.add((x, y))
    SAND = ramp("#6a4a14", "#a8781e", "#d4a238", "#f0cc6a", "#fff0b0")
    for x, y in glass:
        cv.set(x, y, mix("#1a2a3a", "#9adcf0", 0.25))
    paint(cv, top_sand, SAND, f_cyl(cx - 14, cx + 14, len(SAND), 0.3, 0.8), None)
    paint(cv, bot_sand, SAND, f_cyl(cx - 14, cx + 14, len(SAND), 0.3, 0.8), None)
    if 0 < fill < 1:
        for y in range(32, int(bot_level) + 1):
            cv.set(cx, y, SAND[4] if y % 3 else SAND[2])
    for x, y in edge(glass):
        cv.set(x, y, "#5a8aa8")
    for y in range(12, 28):
        cv.set(int(cx - bulb_half(y)) + 2, y, "#e6ffff")
    for y in range(38, 52):
        cv.set(int(cx - bulb_half(y)) + 2, y, "#c8f0ff")
    for yy in (3, 55):
        cap = m_rrect(12, yy, 52, yy + 6, 2)
        paint(cv, cap, WOOD, f_hcyl(yy, yy + 6, len(WOOD), 0.3, 0.8), WOOD[0])
    for px in (14, 48):
        post = m_rect(px, 9, px + 3, 55)
        paint(cv, post, WOOD, f_cyl(px, px + 3, len(WOOD), 0.3, 1.0), WOOD[0])
    return cv


def a_hourglass(i, n):
    flow = n - 2
    if i < flow:
        return hourglass_frame(i / (flow - 1))
    full = hourglass_frame(1.0)
    ang = 60 if i == flow else 120
    cv = Canvas(A, A)
    img = full.img.rotate(-ang, resample=Image.NEAREST, center=(32, 32))
    cv.img.paste(img, (0, 0))
    cv.px = cv.img.load()
    return cv


def monitor(cv, ox, oy, sw, sh, case=BEIGE):
    """Beige CRT monitor: returns the screen rectangle."""
    mon = m_rrect(ox, oy, ox + sw + 10, oy + sh + 12, 3)
    cv.fill(shift(mon, 1, 1), SHADOW)
    paint(cv, mon, case, f_lin(ox, oy, ox + sw, oy + sh + 12, 4.6, 2.2), case[0], soft=case[1])
    cv.fill(m_rrect(ox + 3, oy + 3, ox + sw + 7, oy + sh + 7, 3), case[1])
    scr = (ox + 5, oy + 5, ox + sw + 5, oy + sh + 5)
    return scr


def a_crt_code(i, n):
    cv = Canvas(A, A)
    # Stand.
    cv.fill(m_poly([(22, 52), (42, 52), (46, 58), (18, 58)]), BEIGE[2])
    cv.hline(18, 45, 58, BEIGE[0])
    cv.rect(26, 48, 38, 53, BEIGE[1])
    x0, y0, x1, y1 = monitor(cv, 4, 4, 46, 32)
    rng = random.Random(4)
    lines = []
    for k in range(8):
        ind = [0, 1, 2, 2, 1, 2, 3, 0][k]
        toks = []
        x = ind * 3
        while x < 40:
            w = rng.randrange(2, 9)
            toks.append((x, w, rng.choice((GREEN_P[5], GREEN_P[4], GREEN_P[3], GREEN_P[3], "#e8ff9a"))))
            x += w + 2
            if rng.random() < 0.3:
                break
        lines.append(toks)
    for x in range(x0, x1):
        for y in range(y0, y1):
            d = math.hypot((x + 0.5 - (x0 + x1) / 2) / ((x1 - x0) / 2), (y + 0.5 - (y0 + y1) / 2) / ((y1 - y0) / 2))
            cv.set(x, y, pick(ramp("#020a04", "#04140a", "#062010"), 2.4 - d * 1.8, x, y, 4))
    lh = 4
    for k in range(-1, 10):
        li = (k + i) % 8
        y = y0 + 2 + k * lh - 0
        if y < y0 + 1 or y + 2 > y1 - 1:
            continue
        for tx, tw, c in lines[li]:
            for dx in range(tw):
                xx = x0 + 3 + tx + dx
                if xx < x1 - 2:
                    cv.set(xx, y, c)
                    cv.set(xx, y + 1, mix(c, "#020a04", 0.5))
    cy_ = y0 + 2 + 7 * lh
    if i % 2 == 0:
        cv.rect(x0 + 3, cy_, x0 + 6, cy_ + 3, GREEN_P[5])
    scanlines(cv, x0, y0, x1, y1, 0.7)
    cv.set(x0 + 1, y0 + 1, "#c8ffd2")
    cv.set(x0 + 2, y0 + 1, "#62e27a")
    # Badge and LED on the chin.
    cv.rect(8, 42, 14, 44, "#2f6db0")
    cv.set(52, 43, "#5aff8a" if i % 4 < 3 else "#1a5a2a")
    for vx in range(18, 44, 3):
        cv.vline(vx, 42, 43, BEIGE[2])
    return cv


def a_terminal(i, n):
    cv = Canvas(A, A)
    win = m_rect(2, 6, 62, 58)
    cv.fill(shift(win, 1, 1), SHADOW)
    paint(cv, win, CASE, lambda x, y: 3.5, rgba("#0a0b0e"))
    cv.hline(3, 60, 7, CASE[5])
    cv.vline(3, 7, 56, CASE[5])
    cv.rect(4, 8, 60, 15, "#1c3a8a")
    draw_text(cv, F3, 6, 9, "TERM", "#e8ecff")
    for k, c in enumerate(("#e8b020", "#5aff8a", "#ff3a2a")):
        cv.rect(46 + k * 5, 9, 49 + k * 5, 13, c)
    cv.rect(5, 17, 60, 56, "#0a0602")
    lines = ["READY.", "> RUN", "LOADING...", ""]
    typed = ["", "R", "RU", "RUN", "RUN", "RUN"]
    sched = [(1, 0), (1, 0), (1, 1), (1, 2), (1, 3), (3, 0), (3, 0), (3, 0)]
    stage, k = sched[i]
    AM = AMBER
    y = 19
    draw_text(cv, F3, 7, y, "WY-DOS 4.1", AM[3])
    draw_text(cv, F3, 7, y + 7, "READY.", AM[4])
    if stage == 1:
        t = "> " + typed[k]
        draw_text(cv, F3, 7, y + 14, t, AM[5])
        cx = 7 + text_width(F3, t) + 2 if typed[k] else 7 + text_width(F3, ">") + 2 + 2
        cyl = y + 14
    else:
        draw_text(cv, F3, 7, y + 14, "> RUN", AM[4])
        dots = (i - 5) % 3 + 1
        draw_text(cv, F3, 7, y + 21, "LOADING" + "." * dots, AM[3])
        cx, cyl = 7, y + 28
        cv.rect(7, y + 28 + 6, 7 + (i - 4) * 12, y + 28 + 8, AM[3])
        cv.fill(edge(m_rect(6, y + 33, 59, y + 36)), AM[2])
    blink_on = (i % 2 == 0) or stage == 1 and k > 0
    if blink_on:
        cv.rect(cx, cyl, cx + 3, cyl + 5, AM[5])
    scanlines(cv, 5, 17, 60, 56, 0.75)
    glow(cv, 32, 36, 30, 22, "#ffa030", 0.12, 5, 17, 60, 56)
    return cv


def ua_flag_tex(w, h):
    tex = Canvas(w, h)
    for y in range(h):
        stripe = y * 9 // h
        for x in range(w):
            tex.set(x, y, "#c42a2a" if stripe % 2 == 0 else "#f4f1e8")
    cw, ch = w * 5 // 11, h * 5 // 9
    tex.rect(0, 0, cw, ch, "#1c3a8a")
    ccx, ccy = cw / 2, ch / 2
    for k in range(8):
        a = k / 8 * 2 * math.pi
        tex.set(int(ccx + math.cos(a) * 5.5), int(ccy + math.sin(a) * 4.5), WHITE)
    bitmap(tex, int(ccx) - 1, int(ccy) - 1, [".#.", "###", ".#."], {"#": WHITE})
    return tex


def a_flag(i, n):
    cv = Canvas(A, A)
    t = i / n * 2 * math.pi
    fw, fh = 46, 28
    tex = ua_flag_tex(fw, fh)
    px, top = 6, 8
    pole = m_rect(px, 4, px + 3, 63)
    paint(cv, pole, METAL, f_cyl(px, px + 3, len(METAL), 0.3, 1.0), METAL[0])
    paint(cv, m_ellipse(px + 1.5, 3, 2.6, 2.6), GOLD, f_sphere(px + 0.5, 2, 3, len(GOLD), 0.2), GOLD[0])
    fmask = {}
    for x in range(fw):
        u = x / fw
        ph = u * 2 * math.pi * 1.1 - t
        amp = 0.6 + u * 3.4
        dy = math.sin(ph) * amp
        shade = 1.0 + math.cos(ph) * 0.18 * (0.3 + u)
        for y in range(fh):
            c = tex.get(x, y)
            fmask[(px + 3 + x, int(round(top + y + dy)))] = tuple(min(255, int(c[k] * shade)) for k in range(3)) + (255,)
    m = set(fmask)
    # Fill any one pixel tears between columns.
    for x in range(px + 3, px + 3 + fw):
        ys = sorted(y for (xx, y) in m if xx == x)
        for y in range(ys[0], ys[-1] + 1):
            if (x, y) not in fmask:
                fmask[(x, y)] = fmask.get((x, y - 1), fmask[(x, ys[0])])
    m = set(fmask)
    cv.fill(shift(m, 1, 2), (0, 0, 0, 70))
    for (x, y), c in fmask.items():
        cv.set(x, y, c)
    for x, y in edge(m):
        if x > px + 3:
            cv.set(x, y, mix(cv.get(x, y), "#1a0a0a", 0.45))
    return cv


STATES = [
    ("wireframe_cube", a_cube, 8, 0.1),
    ("new_bounce", a_new, 8, 0.08),
    ("under_construction", a_construction, 8, [0.18, 0.12, 0.12, 0.1, 0.08, 0.1, 0.12, 0.2]),
    ("lv426_globe", a_globe, 12, 0.12),
    ("hit_counter", a_counter, 12, [0.7, 0.08] * 6),
    ("flame_divider", a_flame, 8, 0.08),
    ("dancing_robot", a_robot, 8, 0.12),
    ("email_envelope", a_envelope, 8, 0.1),
    ("hourglass", a_hourglass, 12, [0.25] + [0.15] * 8 + [0.3, 0.08, 0.08]),
    ("crt_code", a_crt_code, 8, 0.14),
    ("terminal_cursor", a_terminal, 8, [0.5, 0.5, 0.2, 0.2, 0.4, 0.3, 0.3, 0.3]),
    ("ua_flag", a_flag, 8, 0.1),
]


# -- Icons (32x32) -----------------------------------------------------------------------------------------
I = 32


def i_floppy():
    cv = Canvas(I, I)
    body = m_poly([(3, 3), (25, 3), (29, 7), (29, 29), (3, 29)])
    cv.fill(shift(body, 1, 1), SHADOW)
    blue = ramp("#0a1430", "#14285a", "#1c3a8a", "#2a56b4", "#4a7ad8")
    paint(cv, body, blue, f_lin(3, 3, 29, 29, 3.6, 1.6), rgba("#060a18"), soft=blue[3])
    shutter = m_rect(9, 3, 23, 12)
    paint(cv, shutter, METAL, f_cyl(9, 23, len(METAL), 0.3, 1.5), METAL[0])
    cv.rect(18, 5, 21, 11, "#14171c")
    cv.vline(10, 4, 10, METAL[5])
    label = m_rect(6, 15, 27, 29)
    paint(cv, label, CREAM, lambda x, y: 3 - (y - 15) * 0.05, rgba("#8a7a5a"))
    cv.rect(7, 16, 26, 18, "#c42a2a")
    draw_text(cv, F3, 9, 20, "BOOT", "#1c3a8a")
    for y in (26,):
        cv.hline(8, 24, y, "#9ab0d0")
    cv.rect(4, 26, 6, 28, "#060a18")
    return cv


def i_crt():
    cv = Canvas(I, I)
    cv.fill(m_poly([(10, 26), (22, 26), (25, 30), (7, 30)]), BEIGE[2])
    cv.hline(7, 24, 30, BEIGE[0])
    x0, y0, x1, y1 = monitor(cv, 2, 2, 18, 13)
    for x in range(x0, x1):
        for y in range(y0, y1):
            d = math.hypot((x + 0.5 - (x0 + x1) / 2) / 9, (y + 0.5 - (y0 + y1) / 2) / 7)
            cv.set(x, y, pick(ramp("#02101c", "#06283a", "#0b4a5c"), 2.4 - d * 2, x, y, 4))
    draw_text(cv, F3, x0 + 2, y0 + 2, "C:", CYAN[4])
    cv.rect(x0 + 10, y0 + 2, x0 + 13, y0 + 7, CYAN[4])
    cv.hline(x0 + 2, x0 + 13, y0 + 9, CYAN[2])
    scanlines(cv, x0, y0, x1, y1, 0.75)
    cv.set(x0 + 1, y0 + 1, WHITE)
    cv.set(25, 22, "#5aff8a")
    return cv


def i_modem():
    cv = Canvas(I, I)
    # Acoustic coupler modem: a box with a phone handset in two rubber cups.
    box = m_poly([(2, 18), (6, 14), (30, 14), (30, 27), (2, 27)])
    cv.fill(shift(box, 1, 1), SHADOW)
    paint(cv, box, BEIGE, f_lin(2, 14, 2, 27, 4.4, 2.0), BEIGE[0], soft=BEIGE[2])
    cv.hline(3, 29, 22, BEIGE[1])
    for k, c in enumerate(("#ff3a2a", "#5aff8a", "#5aff8a", "#ffb030")):
        cv.rect(5 + k * 4, 24, 7 + k * 4, 26, c)
    for cxp in (9.5, 23.5):
        cup = m_ellipse(cxp, 16, 4.5, 2.6)
        paint(cv, cup, BLACKP, f_sphere(cxp - 1, 15, 5, len(BLACKP)), INK)
    hs = (m_capsule(10, 9, 23, 9, 1.8) | m_capsule(9.5, 9.5, 9.5, 13.5, 3.2) | m_capsule(23.5, 9.5, 23.5, 13.5, 3.2))
    paint(cv, hs, ramp("#3a0a08", "#6a1410", "#a82a1a", "#d84a2a", "#f88a5a"), f_hcyl(7, 16, 5, 0.3, 0.8),
          rgba("#1a0404"))
    cv.hline(11, 22, 8, "#f88a5a")
    for hx in (8, 22):
        cv.hline(hx, hx + 3, 15, "#1a0404")
    cv.line(27, 15, 30, 4, "#2a2a2a")
    cv.line(30, 4, 28, 1, "#2a2a2a")
    # Data chirps.
    for k, (x, y) in enumerate(((3, 4), (6, 2), (9, 4))):
        cv.set(x, y, CYAN[3 + k % 2])
    return cv


def i_joystick():
    cv = Canvas(I, I)
    base = m_poly([(4, 20), (8, 17), (28, 17), (29, 27), (3, 27)])
    cv.fill(shift(base, 1, 1), SHADOW)
    paint(cv, base, BLACKP, f_lin(3, 17, 3, 27, 4.2, 1.2), INK, soft=BLACKP[3])
    cv.hline(5, 27, 22, "#c46a1a")
    stick = m_capsule(16, 20, 18, 9, 1.6)
    paint(cv, stick, METAL, f_cyl(14, 20, len(METAL), 0.3, 1), METAL[0])
    cv.fill(m_ellipse(16.5, 19.5, 4, 1.8), BLACKP[1])
    knob = m_ellipse(18.5, 7.5, 4.5, 4.5)
    paint(cv, knob, CAN_RED, f_sphere(17, 6, 5.5, len(CAN_RED), 0.15), rgba("#3a0404"))
    cv.set(17, 5, WHITE)
    btn = m_ellipse(7.5, 19, 2.6, 1.6)
    paint(cv, btn, CAN_RED, lambda x, y: 3.5, rgba("#3a0404"))
    cv.set(7, 18, CAN_RED[5])
    return cv


def i_cassette():
    cv = Canvas(I, I)
    body = m_rrect(1, 7, 31, 27, 2)
    cv.fill(shift(body, 1, 1), SHADOW)
    paint(cv, body, BLACKP, f_lin(1, 7, 1, 27, 4.0, 1.6), INK, soft=BLACKP[3])
    label = m_rect(4, 9, 28, 22)
    paint(cv, label, CREAM, lambda x, y: 2.6, None)
    draw_text_c(cv, F3, 16, 10, "MIX 82", "#1a1a1a")
    cv.hline(4, 27, 16, "#e86a1a")
    cv.hline(4, 27, 21, "#f0c41e")
    win = m_rrect(9, 15, 23, 21, 2)
    cv.fill(win, "#2a2016")
    for cxp in (12.5, 19.5):
        cv.fill(m_ellipse(cxp, 18, 2.6, 2.6), "#5a3a1e")
        cv.fill(m_ellipse(cxp, 18, 1.2, 1.2), WHITE)
    cv.fill(m_poly([(8, 27), (10, 23), (22, 23), (24, 27)]), BLACKP[2])
    for hx in (12, 16, 20):
        cv.set(hx, 25, INK)
    for hx in (3, 28):
        cv.set(hx, 9, BLACKP[4])
    return cv


def i_dish():
    cv = Canvas(I, I)
    cv.fill(m_poly([(10, 29), (14, 20), (18, 20), (22, 29)]), METAL[2])
    cv.hline(8, 23, 29, METAL[0])
    cv.hline(9, 22, 30, SHADOW)
    dish = m_ellipse(14, 12, 12, 8.5)
    rot = set()
    for x, y in dish:
        # Tilt the bowl up and to the right.
        if (x - 14) * 0.5 + (y - 12) * 0.9 > -3:
            rot.add((x, y))
    paint(cv, rot, SHELL, f_sphere(18, 17, 14, len(SHELL), 0.2), SHELL_OL, soft=SHELL[2])
    cv.fill(edge(m_ellipse(14, 12, 8, 5.5)) & rot, SHELL[2])
    cv.line(14, 15, 23, 4, METAL[1])
    cv.line(19, 18, 23, 4, METAL[1])
    cv.fill(m_ellipse(23.5, 4.5, 1.8, 1.8), "#ff3a2a")
    cv.set(23, 4, "#ffb0a0")
    for r in (3, 5):
        for a in range(-50, 40, 12):
            t = math.radians(a)
            cv.set(int(24 + math.cos(t) * r), int(3 + math.sin(t) * r), CYAN[3])
    return cv


def i_magnifier():
    cv = Canvas(I, I)
    handle = m_capsule(19, 19, 28, 28, 2.6)
    cv.fill(shift(handle, 1, 1), SHADOW)
    paint(cv, handle, BROWN, f_lin(18, 18, 30, 30, 4.5, 2), BROWN[0])
    ring = m_ellipse(12.5, 12.5, 10, 10)
    cv.fill(shift(ring, 1, 1), SHADOW)
    paint(cv, ring, GOLD, f_sphere(9, 9, 13, len(GOLD), 0.25), GOLD[0])
    lens = m_ellipse(12.5, 12.5, 7.5, 7.5)
    for x, y in lens:
        d = math.hypot(x - 10, y - 10)
        cv.set(x, y, pick(GLASS, 3.6 - d * 0.35, x, y, 4))
    for x, y in edge(lens):
        cv.set(x, y, GOLD[1])
    cv.line(8, 11, 11, 8, WHITE)
    cv.set(8, 12, GLASS[4])
    return cv


def i_folder():
    cv = Canvas(I, I)
    MAN = ramp("#5a3a08", "#9a6a14", "#d4a028", "#f0c84a", "#ffe48a", "#fff4c8")
    back = m_poly([(2, 7), (11, 7), (13, 10), (29, 10), (29, 27), (2, 27)])
    cv.fill(shift(back, 1, 1), SHADOW)
    paint(cv, back, MAN, lambda x, y: 2.2, MAN[0])
    paper = m_rect(5, 9, 26, 22)
    paint(cv, paper, MILK, lambda x, y: 3, rgba("#7a8a98"))
    for y in (12, 15, 18):
        cv.hline(7, 23, y, "#9ab0c0")
    front = m_poly([(1, 14), (30, 14), (29, 27), (2, 27)])
    paint(cv, front, MAN, f_lin(1, 14, 1, 27, 4.6, 2.6), MAN[0], soft=MAN[3])
    cv.hline(3, 28, 15, MAN[5])
    return cv


def i_envelope():
    cv = Canvas(I, I)
    body = m_rect(2, 8, 30, 26)
    cv.fill(shift(body, 1, 1), SHADOW)
    ENV = ramp("#a89c80", "#d8ccae", "#f0e8d0", "#fffaf0")
    paint(cv, body, ENV, f_lin(2, 8, 30, 26, 3.2, 1.6), rgba("#3e352a"))
    for k in range(13):
        cv.set(3 + k, 24 - int(k * 0.75), "#b6a98c")
        cv.set(28 - k, 24 - int(k * 0.75), "#b6a98c")
    fl = m_poly([(2, 8), (30, 8), (16, 19)])
    paint(cv, fl, ENV, lambda x, y: 2.2 - (y - 8) * 0.1, rgba("#3e352a"))
    paint(cv, m_ellipse(16, 18, 3, 3), CAN_RED, f_sphere(15, 17, 4, len(CAN_RED)), CAN_RED[0])
    # Stamp.
    cv.rect(23, 10, 28, 15, "#2f6db0")
    cv.fill(edge(m_rect(23, 10, 28, 15)), WHITE)
    return cv


def i_ghost():
    cv = Canvas(I, I)
    body = m_ellipse(16, 13, 11, 10) | m_rect(5, 13, 27, 26)
    for k in range(4):
        body -= m_poly([(5 + k * 5.5 + 1, 27), (5 + k * 5.5 + 2.75, 23.5), (5 + k * 5.5 + 4.5, 27)])
    cv.fill(shift(body, 1, 1), SHADOW)
    PG = ramp("#3a0a4a", "#6a1a8a", "#a02ad0", "#c85af0", "#e8a0ff")
    paint(cv, body, PG, f_sphere(12, 9, 16, len(PG), 0.35), rgba("#1a0420"), soft=PG[2])
    for ex in (11.5, 20.5):
        cv.fill(m_ellipse(ex, 13, 3, 3.6), WHITE)
        cv.fill(m_ellipse(ex + 1, 14, 1.6, 1.8), "#1c3a8a")
    cv.set(9, 7, PG[4])
    cv.set(10, 6, PG[4])
    return cv


def i_chat():
    cv = Canvas(I, I)
    back = m_rrect(13, 3, 31, 17, 4) | m_poly([(25, 16), (29, 21), (27, 15)])
    cv.fill(shift(back, 1, 1), SHADOW)
    paint(cv, back, CYAN, lambda x, y: 2.4, CYAN[0])
    for x in (18, 22, 26):
        cv.rect(x, 9, x + 2, 11, CYAN[5])
    front = m_rrect(1, 9, 22, 25, 4) | m_poly([(4, 24), (3, 30), (10, 24)])
    cv.fill(shift(front, 1, 1), SHADOW)
    paint(cv, front, MILK, f_lin(1, 9, 1, 25, 4, 2), rgba("#3a4a58"))
    for y, w in ((14, 15), (18, 10)):
        cv.hline(5, 5 + w, y, "#5a6a7a")
    return cv


def i_star():
    cv = Canvas(I, I)
    pts = []
    for k in range(10):
        a = -math.pi / 2 + k * math.pi / 5
        r = 14 if k % 2 == 0 else 6
        pts.append((16 + math.cos(a) * r, 17 + math.sin(a) * r))
    star = m_poly(pts)
    cv.fill(shift(star, 1, 1), SHADOW)
    paint(cv, star, GOLD, f_sphere(13, 12, 16, len(GOLD), 0.3), rgba("#3a2204"))
    # Facet split: lit left half, darker right half of each point.
    for x, y in star:
        if (x, y) not in edge(star) and x > 16 and (x - 16) > (y - 17) * -0.3:
            cv.set(x, y, mix(cv.get(x, y), "#8a5a0e", 0.3))
    sparkle(cv, 26, 6, 2, WHITE, "#ffe68a", "#c48a1a")
    cv.set(13, 11, WHITE)
    return cv


ICONS = [("floppy disk", i_floppy), ("CRT monitor", i_crt), ("modem", i_modem), ("joystick", i_joystick),
         ("cassette", i_cassette), ("satellite dish", i_dish), ("magnifying glass", i_magnifier),
         ("folder", i_folder), ("envelope", i_envelope), ("ghost", i_ghost), ("chat bubble", i_chat),
         ("star", i_star)]


# -- Output --------------------------------------------------------------------------------------------------


def write_attributions(folder, files, what):
    names = ", ".join(f'"{n}"' for n in files)
    with open(os.path.join(folder, "attributions.yml"), "w", encoding="utf-8", newline="\n") as f:
        f.write(f"- files: [{names}]\n")
        f.write('  license: "CC-BY-SA-3.0"\n')
        f.write(f'  copyright: "{what} drawn for CMU, generated by {GENERATOR}"\n')
        f.write(f'  source: "{SOURCE}"\n')


def write_rsi(path, entries, w, h, what):
    os.makedirs(path, exist_ok=True)
    states = []
    for name, fn, n, delay in entries:
        sheet = Image.new("RGBA", (w * n, h))
        for i in range(n):
            cv = fn(i, n)
            assert (cv.w, cv.h) == (w, h), name
            sheet.paste(cv.img, (w * i, 0))
        sheet.save(os.path.join(path, f"{name}.png"))
        delays = delay if isinstance(delay, list) else [delay] * n
        assert len(delays) == n, name
        states.append({"name": name, "delays": [delays]})
    meta = {"version": 1, "license": "CC-BY-SA-3.0",
            "copyright": f"{what} drawn for CMU, generated by {GENERATOR}",
            "size": {"x": w, "y": h}, "states": states}
    with open(os.path.join(path, "meta.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(meta, f, indent=2)
        f.write("\n")


def main():
    only = sys.argv[1:]
    os.makedirs(OUT_WALL, exist_ok=True)
    os.makedirs(OUT_RETRO, exist_ok=True)
    if not only or "wall" in only:
        files = []
        for k, (_, fn) in enumerate(WALLPAPERS):
            cv = fn()
            assert (cv.w, cv.h) == (W, H), fn.__name__
            name = f"wallpaper_{k + 1:02d}.png"
            cv.save(os.path.join(OUT_WALL, name), scale=SCALE)
            files.append(name)
        write_attributions(OUT_WALL, files, "Retro pixel-art desktop wallpapers")
    if not only or "rsi" in only:
        write_rsi(os.path.join(OUT_RETRO, "retro.rsi"), STATES, A, A, "Retro animated web gifs")
    if not only or "icons" in only:
        icons = []
        for k, (_, fn) in enumerate(ICONS):
            cv = fn()
            assert (cv.w, cv.h) == (I, I), fn.__name__
            name = f"icon_{k + 1:02d}.png"
            cv.save(os.path.join(OUT_RETRO, name), scale=1)
            icons.append(name)
        write_attributions(OUT_RETRO, icons, "Retro pixel-art desktop icons")


if __name__ == "__main__":
    main()
