"""Draws the masthead and emblem for The Colony Herald (herald.colony.news).

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/Herald.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Herald/{banner,emblem}.png.
Everything is drawn at 1x on the pixel grid with hand-built bitmap letters (a textura style display face
for the title, a 3x5 face for the small print), then upscaled 2x with nearest neighbour.
Pass --preview DIR to also write 4x previews there.
"""
import math
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "Herald")
SCALE = 2

PAPER = "#F1EBDD"
INK = "#1E1E1E"
RED = "#8A1C1C"
GREY = "#8C857A"   # worn ink: the ridge line and the stack ribs

# Title face: textura capitals and lowercase. Rows 0-4 ascender, 5-15 x-height, 16-18 descender.
TITLE = {
    "T": [
        "..#########",
        ".##...##..#",
        "##....##...",
        "##....##...",
        ".#....##...",
        "......##...",
        "......##...",
        "......##...",
        "......##...",
        "......##...",
        "......##...",
        "......##...",
        "......##...",
        "......##...",
        "......###..",
        ".......###.",
    ],
    "C": [
        "....#####.",
        "..##....##",
        ".##.....##",
        "###......#",
        "###.......",
        "###.......",
        "###.......",
        "###.......",
        "###.......",
        "###.......",
        "###.......",
        "###.......",
        "###......#",
        ".###....##",
        "..###.###.",
        "...####...",
    ],
    "H": [
        "..##.....##.",
        ".##.....##..",
        "##......##..",
        "##......##..",
        "##......##..",
        "##......##..",
        "##......##..",
        "##########..",
        "##......##..",
        "##......##..",
        "##......##..",
        "##......##..",
        "##......##..",
        "##......##..",
        "###.....###.",
        ".###.....###",
    ],
    "h": [
        ".#......",
        "##......",
        "##......",
        "##......",
        "##......",
        "##.###..",
        "###..##.",
        "##...##.",
        "##...##.",
        "##...##.",
        "##...##.",
        "##...##.",
        "##...##.",
        "##...##.",
        "##...##.",
        "##...###",
    ],
    "e": [
        "",
        "",
        "",
        "",
        "",
        "..####.",
        ".##..##",
        "##...##",
        "##..##.",
        "##.##..",
        "####...",
        "##.....",
        "##.....",
        "##.....",
        ".##..##",
        "..####.",
    ],
    "o": [
        "",
        "",
        "",
        "",
        "",
        "..####.",
        ".##..##",
        "##...##",
        "##...##",
        "##...##",
        "##...##",
        "##...##",
        "##...##",
        "##...##",
        "##..##.",
        ".####..",
    ],
    "l": [
        ".#..",
        "##..",
        "##..",
        "##..",
        "##..",
        "##..",
        "##..",
        "##..",
        "##..",
        "##..",
        "##..",
        "##..",
        "##..",
        "##..",
        "###.",
        ".###",
    ],
    "n": [
        "",
        "",
        "",
        "",
        "",
        ".#.###..",
        "###..##.",
        "##...##.",
        "##...##.",
        "##...##.",
        "##...##.",
        "##...##.",
        "##...##.",
        "##...##.",
        "##...##.",
        "##...###",
    ],
    "y": [
        "",
        "",
        "",
        "",
        "",
        ".#...#.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##.###.",
        ".##.##.",
        "....##.",
        "#..##..",
        ".###...",
    ],
    "r": [
        "",
        "",
        "",
        "",
        "",
        ".#.###",
        "###...",
        "##....",
        "##....",
        "##....",
        "##....",
        "##....",
        "##....",
        "##....",
        "##....",
        "###...",
    ],
    "a": [
        "",
        "",
        "",
        "",
        "",
        ".####..",
        "##..##.",
        "....##.",
        "..####.",
        ".##.##.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##.###.",
        ".##.###",
    ],
    "d": [
        ".....#.",
        "....##.",
        "....##.",
        "....##.",
        "....##.",
        "..####.",
        ".##.##.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##..##.",
        "##.###.",
        ".##.###",
    ],
    " ": ["....."],
}

# Small print: 3x5, a few letters wider where three columns cannot carry them.
SMALL = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"], "B": ["##.", "#.#", "##.", "#.#", "##."],
    "C": [".##", "#..", "#..", "#..", ".##"], "D": ["##.", "#.#", "#.#", "#.#", "##."],
    "E": ["###", "#..", "##.", "#..", "###"], "F": ["###", "#..", "##.", "#..", "#.."],
    "G": [".##", "#..", "#.#", "#.#", ".##"], "H": ["#.#", "#.#", "###", "#.#", "#.#"],
    "I": ["###", ".#.", ".#.", ".#.", "###"], "J": ["..#", "..#", "..#", "#.#", ".#."],
    "K": ["#.#", "#.#", "##.", "#.#", "#.#"], "L": ["#..", "#..", "#..", "#..", "###"],
    "M": ["#...#", "##.##", "#.#.#", "#...#", "#...#"], "N": ["#..#", "##.#", "#.##", "#..#", "#..#"],
    "O": [".#.", "#.#", "#.#", "#.#", ".#."], "P": ["##.", "#.#", "##.", "#..", "#.."],
    "Q": [".#.", "#.#", "#.#", "#.#", ".##"], "R": ["##.", "#.#", "##.", "#.#", "#.#"],
    "S": [".##", "#..", ".#.", "..#", "##."], "T": ["###", ".#.", ".#.", ".#.", ".#."],
    "U": ["#.#", "#.#", "#.#", "#.#", "###"], "V": ["#.#", "#.#", "#.#", "#.#", ".#."],
    "W": ["#...#", "#...#", "#.#.#", "##.##", "#...#"], "X": ["#.#", "#.#", ".#.", "#.#", "#.#"],
    "Y": ["#.#", "#.#", ".#.", ".#.", ".#."], "Z": ["###", "..#", ".#.", "#..", "###"],
    "0": ["###", "#.#", "#.#", "#.#", "###"], "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["##.", "..#", ".#.", "#..", "###"], "3": ["##.", "..#", ".#.", "..#", "##."],
    "4": ["#.#", "#.#", "###", "..#", "..#"], "5": ["###", "#..", "##.", "..#", "##."],
    "6": [".##", "#..", "###", "#.#", "###"], "7": ["###", "..#", ".#.", ".#.", ".#."],
    "8": ["###", "#.#", "###", "#.#", "###"], "9": ["###", "#.#", "###", "..#", "##."],
    "-": ["...", "...", "###", "...", "..."], ".": [".", ".", ".", ".", "#"],
    ",": ["..", "..", "..", ".#", "#."], "'": ["#", "#", ".", ".", "."],
    ":": [".", "#", ".", "#", "."], "*": ["...", "#.#", ".#.", "#.#", "..."],
    " ": ["..", "..", "..", "..", ".."],
}


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

    def hline(self, x0, x1, y, c):
        self.rect(x0, y, x1, y, c)

    def vline(self, x, y0, y1, c):
        self.rect(x, y0, x, y1, c)

    def outline(self, x0, y0, x1, y1, c):
        self.hline(x0, x1, y0, c)
        self.hline(x0, x1, y1, c)
        self.vline(x0, y0, y1, c)
        self.vline(x1, y0, y1, c)

    def glyphs(self, font, x, y, text, c, gap=1, bold=False):
        """Sets text; bold doubles every pixel one to the right, which turns the title's two pixel stems
        into the three pixel pen strokes of a textura and closes its counters to two."""
        for ch in text:
            g = font.get(ch, font[" "])
            for gy, row in enumerate(g):
                for gx, bit in enumerate(row):
                    if bit == "#":
                        self.set(x + gx, y + gy, c)
                        if bold:
                            self.set(x + gx + 1, y + gy, c)
            x += glyph_width(g) + gap + (1 if bold else 0)
        return x

    def save(self, path, scale):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.img.resize((self.w * scale, self.h * scale), Image.NEAREST).save(path)


def rgba(c):
    c = c.lstrip("#")
    return (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), 255)


def glyph_width(g):
    return max(len(row) for row in g) if any(g) else 0


def text_width(font, text, gap=1, bold=False):
    return sum(glyph_width(font.get(ch, font[" "])) + gap + (1 if bold else 0) for ch in text) - gap


# Kerning for the title: the textura letters sit tight, as set type would.
TITLE_GAP = 1
TITLE_BOLD = True


def draw_title(cv, x, y, text, c):
    return cv.glyphs(TITLE, x, y, text, c, gap=TITLE_GAP, bold=TITLE_BOLD)


GLASS, SHINE = "#B9B0A0", "#DCD4C4"


def dome(cv, cx, base, r, frame=INK, glass=GLASS, shine=SHINE, ribs=True):
    """A greenhouse dome of Albacetire: a hemisphere of radius r centred on cx whose last glass row is base.
    Nothing is antialiased; every edge is a threshold on the pixel grid."""
    for y in range(base - r, base + 1):
        for x in range(cx - r, cx + r + 1):
            d = math.hypot(x - cx, y - base + 0.5)
            if d <= r + 0.2:
                c = frame if d > r - 1 else glass
                if c == glass and x < cx - r // 3 and y < base - r // 3 and d > r - 3.2:
                    c = shine
                cv.set(x, y, c)
    if not ribs:
        return
    # Meridians: arcs of the hemisphere seen from the front, then two parallels.
    for a in (-0.72, -0.3, 0.3, 0.72):
        for y in range(base - r + 1, base + 1):
            h = (base - y + 0.5) / r
            if h >= 1:
                continue
            x = round(cx + a * r * math.sqrt(1 - h * h))
            if math.hypot(x - cx, y - base + 0.5) <= r - 1:
                cv.set(x, y, frame)
    for f in (0.45, 0.8):
        y = base - round(r * f)
        for x in range(cx - r, cx + r + 1):
            if math.hypot(x - cx, y - base + 0.5) <= r - 1:
                cv.set(x, y, frame)


def footing(cv, x0, x1, y, frame=INK):
    """A wall course under a dome: solid row, a row of piers, solid row."""
    cv.hline(x0, x1, y, frame)
    for x in range(x0 + 1, x1, 3):
        cv.set(x, y + 1, frame)
    cv.hline(x0, x1, y + 2, frame)


def skyline(cv, x0, y0, x1, y1):
    """The colony seen from the press office: an atmosphere stack, the great greenhouse dome and a small
    one, with Stable's mountain ridge behind and Joint Base Schaefer's mast on its peak.
    Fits the box x0..x1, y0..y1; y1 is the ground line."""
    ground = y1
    r = 9
    cx = x0 + 18
    scx, sr = x1 - 6, 5
    # Ridge line, drawn first so the domes stand in front of it.
    peak_x, peak_y = scx - 1, y0 + 4
    for x in range(x0, x1 + 1):
        if x <= peak_x:
            y = peak_y + (peak_x - x) * 2 // 5
        else:
            y = peak_y + (x - peak_x)
        if y < ground - 1 and x > x0 + 6:
            cv.set(x, y, GREY)
    cv.vline(peak_x, peak_y - 3, peak_y - 1, INK)
    cv.set(peak_x, peak_y - 4, RED)
    # Atmosphere stack: a cap, a ribbed tower and a red beacon.
    sx = x0 + 2
    cv.set(sx + 1, y0 + 1, RED)
    cv.vline(sx + 1, y0 + 2, y0 + 3, INK)
    cv.rect(sx - 1, y0 + 4, sx + 3, y0 + 5, INK)
    cv.rect(sx, y0 + 6, sx + 2, ground - 1, INK)
    for y in range(y0 + 8, ground - 2, 3):
        cv.set(sx + 1, y, GREY)
    # Domes on their footings.
    dome(cv, cx, ground - 3, r)
    footing(cv, cx - r - 1, cx + r + 1, ground - 2)
    dome(cv, scx, ground - 2, sr, ribs=False)
    cv.vline(scx, ground - 2 - sr + 2, ground - 2, INK)
    cv.hline(scx - sr + 1, scx + sr - 1, ground - 4, INK)
    cv.hline(scx - sr - 1, scx + sr + 1, ground - 1, INK)
    # Ground.
    cv.hline(x0 - 2, x1 + 2, ground, INK)


# The quill's vane, row by row from the tip: pixels of red left and right of the shaft.
VANE = [(0, 0), (1, 0), (1, 1), (2, 1), (2, 1), (3, 1), (3, 2), (4, 2), (4, 2), (4, 2), (5, 2), (5, 2), (5, 2),
        (4, 2), (4, 1), (3, 1), (2, 1), (1, 0)]


def quill(cv, tx, ty, rows=26, splits=(8, 12), right_splits=(10,)):
    """A quill leaning right with its tip at (tx, ty): the shaft steps one pixel left every two rows, the
    vane follows VANE, and paper splits run from the shaft up and out the way the barbs grow. Below the
    vane the bare shaft carries on, to be hidden behind whatever is drawn over it."""
    def shaft_x(y):
        return tx - (y - ty) // 2

    for i in range(rows):
        y = ty + i
        s = shaft_x(y)
        if i < len(VANE):
            wl, wr = VANE[i]
            for j in range(1, wl + 1):
                cv.set(s - j, y, RED)
            for j in range(1, wr + 1):
                cv.set(s + j, y, RED)
        cv.set(s, y, SHAFT if i else RED)
    for side, rows_, reach in ((-1, splits, 6), (1, right_splits, 3)):
        for n in rows_:
            for j in range(1, reach + 1):
                x, y = shaft_x(ty + n) + side * j, ty + n - (j + (side > 0)) // 2
                if cv.get(x, y) == rgba(RED):
                    cv.set(x, y, PAPER)


SHAFT = "#5A1010"


def banner():
    W, H = 272, 58
    cv = Canvas(W, H, PAPER)

    # Top rules: thin over thick.
    cv.hline(2, W - 3, 1, INK)
    cv.rect(2, 3, W - 3, 4, INK)

    # Ears: the boxed corners either side of the title, as on a broadsheet.
    ear_w = 46
    ey0, ey1 = 8, 38
    left, right = (3, 3 + ear_w - 1), (W - 3 - ear_w, W - 4)
    for x0, x1 in (left, right):
        cv.outline(x0, ey0, x1, ey1, INK)

    # Left ear: the colony skyline with the founding year.
    skyline(cv, left[0] + 4, ey0 + 2, left[1] - 4, ey1 - 9)
    label = "EST. 2169"
    cv.glyphs(SMALL, centre(SMALL, label, left[0], left[1]), ey1 - 6, label, RED)

    # Right ear: the weather, which on Albacetire is never only the weather.
    x0, x1 = right
    cv.glyphs(SMALL, centre(SMALL, "WEATHER", x0, x1), ey0 + 3, "WEATHER", RED)
    cv.hline(x0 + 8, x1 - 8, ey0 + 9, RED)
    y = ey0 + 12
    for text in ("DUST HAZE", "WIND EAST", "AIR: CLEAR"):
        cv.glyphs(SMALL, centre(SMALL, text, x0, x1), y, text, INK)
        y += 6

    # Title, centred between the ears.
    title = "The Colony Herald"
    tw = text_width(TITLE, title, TITLE_GAP, TITLE_BOLD)
    draw_title(cv, (W - tw) // 2, 9, title, INK)

    # Tagline under the title.
    tag = "INDEPENDENT PRESS OF ALBACETIRE"
    cv.glyphs(SMALL, (W - text_width(SMALL, tag)) // 2, 31, tag, RED)

    # Edition bar: thin rule, small print spread across with red diamonds, thick over thin.
    cv.hline(2, W - 3, 41, INK)
    ed = ["VOL. XV", "STABLE GARRISON EDITION", "NEROID SECTOR", "ONE CREDIT"]
    widths = [text_width(SMALL, t) for t in ed]
    x_left, x_right = 6, W - 7
    gap = (x_right - x_left + 1 - sum(widths)) / (len(ed) - 1)
    x = x_left
    for i, t in enumerate(ed):
        xi = round(x)
        cv.glyphs(SMALL, xi, 44, t, INK)
        if i < len(ed) - 1:
            diamond(cv, round(x + widths[i] + gap / 2), 46, RED)
        x += widths[i] + gap
    cv.rect(2, 51, W - 3, 52, INK)
    cv.hline(2, W - 3, 54, INK)
    return cv


def centre(font, text, x0, x1):
    """x for text centred in the box x0..x1 inclusive (box edges are the ear's own rules)."""
    return x0 + (x1 - x0 + 1 - text_width(font, text)) // 2


def diamond(cv, cx, cy, c):
    cv.set(cx, cy - 1, c)
    cv.hline(cx - 1, cx + 1, cy, c)
    cv.set(cx, cy + 1, c)


def emblem():
    """The dome with a red quill rising behind it, inside a ruled ink and red roundel, for the lore pages."""
    S = 44
    cv = Canvas(S, S)
    c = (S - 1) / 2
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - c, y - c)
            if d <= c + 0.3:
                if d > c - 1.7:
                    col = INK
                elif c - 3.7 < d <= c - 2.7:
                    col = RED
                else:
                    col = PAPER
                cv.set(x, y, col)
    # The quill rises from behind the dome, so it reads as the colony's own pen and not as a slash
    # through the roundel.
    quill(cv, 29, 5)
    cx, base, r = S // 2, S - 11, 10
    dome(cv, cx, base, r)
    footing(cv, cx - r - 1, cx + r + 1, base + 1)
    return cv


def main():
    preview = None
    if "--preview" in sys.argv:
        preview = sys.argv[sys.argv.index("--preview") + 1]
    b = banner()
    e = emblem()
    b.save(os.path.join(OUT, "banner.png"), SCALE)
    e.save(os.path.join(OUT, "emblem.png"), SCALE)
    if preview:
        b.save(os.path.join(preview, "herald_banner_4x.png"), 4)
        bg = Canvas(e.w + 8, e.h + 8, PAPER)
        bg.img.alpha_composite(e.img, (4, 4))
        bg.save(os.path.join(preview, "herald_emblem_4x.png"), 6)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
