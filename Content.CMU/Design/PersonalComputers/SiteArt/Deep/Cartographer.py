"""Draws The Cartographer (cartographer.deep) banner and emblem for the deep web.

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/Deep/Cartographer.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Deep/Cartographer/{banner,emblem}.png.

Everything is drawn at 1x on the pixel grid with no antialiasing, then upscaled 2x with nearest
neighbour. The look is an old spacer's hand-kept star chart: a parsec grid in deep ink, a compass
rose emblem, a dotted route that wanders off and ends at a red cross, and a title whose middle rows
have slipped sideways like a worn tape. Colours match the site style (background #07141B, text
#C4D6CC, heading #7FE3D2, link #F2955E).
"""
import os

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites",
                   "Deep", "Cartographer")
SCALE = 2
W, H = 264, 64

# -- Palette ---------------------------------------------------------------------------------------
BG = "#07141B"
GRID = "#0D222C"
GRID_HI = "#15394A"
DIM = "#4F6E69"
TEXT = "#C4D6CC"
TEAL = "#7FE3D2"
TEAL_DK = "#2F7C74"
CORAL = "#F2955E"
CORAL_DK = "#8E4C2F"
RED = "#E0483A"
STAR = "#E9F2EC"

# -- 5x7 capitals for the title (drawn with 2x2 blocks, so each letter is 10x14 on the 1x grid) ------
F5 = {
    "C": [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "G": [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####"],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    " ": [".....", ".....", ".....", ".....", ".....", ".....", "....."],
}

# -- 3x5 small print -------------------------------------------------------------------------------
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
        self.img = Image.new("RGBA", (w, h), rgba(bg))

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.img.putpixel((x, y), rgba(c))

    def get(self, x, y):
        return self.img.getpixel((x, y))

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def text(self, font, x, y, text, c, gap=1, block=1):
        for ch in text:
            g = font.get(ch, font[" "])
            for gy, row in enumerate(g):
                for gx, bit in enumerate(row):
                    if bit == "#":
                        self.rect(x + gx * block, y + gy * block,
                                  x + gx * block + block - 1, y + gy * block + block - 1, c)
            x += (len(g[0]) + gap) * block
        return x

    def save(self, path):
        self.img.resize((self.w * SCALE, self.h * SCALE), Image.NEAREST).save(path)


def text_width(font, text, gap=1, block=1):
    return (sum(len(font.get(ch, font[" "])[0]) + gap for ch in text) - gap) * block


def circle(cv, cx, cy, r, c, dotted=0):
    """Midpoint circle; dotted=n leaves gaps so only every n-th step is drawn."""
    x, y, err, step = r, 0, 1 - r, 0
    while x >= y:
        if not dotted or step % dotted == 0:
            for px, py in ((x, y), (y, x), (-y, x), (-x, y), (-x, -y), (-y, -x), (y, -x), (x, -y)):
                cv.set(cx + px, cy + py, c)
        y += 1
        step += 1
        if err < 0:
            err += 2 * y + 1
        else:
            x -= 1
            err += 2 * (y - x) + 1


def line(cv, x0, y0, x1, y1, c, dash=0):
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err, i = dx + dy, 0
    while True:
        if not dash or (i // dash) % 2 == 0:
            cv.set(x0, y0, c)
        if x0 == x1 and y0 == y1:
            return
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy
        i += 1


def rose(cv, cx, cy):
    """Compass rose: two rings, a long north point in coral, short diagonals, a red fix mark."""
    circle(cv, cx, cy, 22, TEAL_DK)
    circle(cv, cx, cy, 19, GRID_HI, dotted=2)
    circle(cv, cx, cy, 9, TEAL_DK)
    # Tick marks on the outer ring every 30 degrees (precomputed integer offsets).
    for dx, dy in ((0, -22), (11, -19), (19, -11), (22, 0), (19, 11), (11, 19), (0, 22),
                   (-11, 19), (-19, 11), (-22, 0), (-19, -11), (-11, -19)):
        cv.set(cx + dx * 24 // 22, cy + dy * 24 // 22, DIM)
    # Diagonal short points.
    for sx, sy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        for t in range(1, 11):
            cv.set(cx + sx * t, cy + sy * t, DIM if t > 6 else TEAL_DK)
    # Cardinal points as tapering diamonds; each has a lit half and a shaded half.
    for (ux, uy), length, lit, shade in (((0, -1), 20, CORAL, CORAL_DK), ((0, 1), 16, TEAL, TEAL_DK),
                                         ((1, 0), 16, TEAL, TEAL_DK), ((-1, 0), 16, TEAL, TEAL_DK)):
        for t in range(length + 1):
            half = 3 - (t * 4) // (length + 1)
            for k in range(-half, half + 1):
                px = cx + ux * t + (k if ux == 0 else 0)
                py = cy + uy * t + (k if uy == 0 else 0)
                cv.set(px, py, lit if k <= 0 else shade)
    cv.set(cx, cy, STAR)
    # N above the rose.
    cv.text(F3, cx - 1 - 1, cy - 30, "N", CORAL)


def grid(cv):
    for x in range(0, cv.w, 8):
        for y in range(cv.h):
            cv.set(x, y, GRID if y % 2 else GRID_HI if x % 32 == 0 else GRID)
    for y in range(4, cv.h, 8):
        for x in range(cv.w):
            if x % 8:
                cv.set(x, y, GRID)


def stars(cv, pts):
    for x, y, big in pts:
        if big:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                cv.set(x + dx, y + dy, DIM)
        cv.set(x, y, STAR)


def cross(cv, x, y, c):
    for t in range(-2, 3):
        cv.set(x + t, y + t, c)
        cv.set(x + t, y - t, c)


def slip(cv, y0, y1, x0, x1, shift):
    """Glitch: copy a horizontal band and paste it shifted, leaving a coral ghost on the old edge."""
    band = cv.img.crop((x0, y0, x1, y1 + 1))
    for y in range(y0, y1 + 1):
        for x in range(x0, x0 + shift):
            if cv.get(x, y)[:3] == rgba(TEXT)[:3]:
                cv.set(x, y, CORAL_DK)
    cv.img.paste(band, (x0 + shift, y0))


def banner():
    cv = Canvas(W, H, BG)
    grid(cv)
    stars(cv, ((70, 6, False), (96, 58, False), (118, 9, False), (151, 59, True), (178, 7, False),
               (212, 58, False), (238, 4, True), (257, 47, False), (6, 58, False), (58, 52, False),
               (244, 33, False), (200, 31, False)))

    # Route: dotted from the rose, over the title past three waypoints to the red cross.
    route = ((50, 20), (68, 5), (140, 3), (200, 7), (247, 13))
    for (ax, ay), (bx, by) in zip(route, route[1:]):
        line(cv, ax, ay, bx, by, CORAL_DK, dash=2)
    for wx, wy in route[1:-1]:
        cv.rect(wx - 1, wy - 1, wx + 1, wy + 1, BG)
        cv.set(wx, wy, CORAL)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            cv.set(wx + dx, wy + dy, CORAL_DK)
    cross(cv, 249, 13, RED)
    cv.text(F3, 233, 18, "LOST", RED)

    # Emblem.
    cv.rect(4, 6, 58, 59, BG)
    rose(cv, 31, 35)

    # Title.
    tx = 70
    cv.text(F3, tx, 10, "THE", TEAL)
    cv.rect(tx + 13, 12, tx + 30, 12, TEAL_DK)
    title = "CARTOGRAPHER"
    tw = text_width(F5, title, block=2)
    # Drop shadow first, then the face.
    cv.text(F5, tx + 1, 18 + 1, title, GRID_HI, block=2)
    cv.text(F5, tx, 18, title, TEXT, block=2)
    # Worn tape: two bands of the title slip right.
    slip(cv, 24, 25, tx, tx + tw + 2, 2)
    slip(cv, 28, 28, tx + 40, tx + tw + 2, 1)

    # Underline as a scale bar, then the small print.
    by = 36
    for x in range(tx, tx + tw + 1):
        cv.set(x, by, TEAL_DK)
        if (x - tx) % 12 == 0:
            cv.set(x, by - 1, TEAL)
            cv.set(x, by + 1, TEAL)
    cv.text(F3, tx, 41, "CHARTS OF THE DEAD LANES", TEAL)
    cv.text(F3, tx, 49, "HAND PLOTTED - NOT FILED - 2182", DIM)
    addr = "CARTOGRAPHER.DEEP"
    cv.text(F3, W - 6 - text_width(F3, addr), 57, addr, CORAL)
    return cv


def emblem():
    cv = Canvas(64, 64, BG)
    grid(cv)
    cv.rect(6, 4, 57, 60, BG)
    rose(cv, 32, 35)
    cross(cv, 51, 16, RED)
    return cv


def main():
    os.makedirs(OUT, exist_ok=True)
    banner().save(os.path.join(OUT, "banner.png"))
    emblem().save(os.path.join(OUT, "emblem.png"))
    print("wrote", OUT)


if __name__ == "__main__":
    main()
