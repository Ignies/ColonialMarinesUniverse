"""Draws the Colony Administration (admin.colony.gov) masthead and seal for the ExtraNet.

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/Administration.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Administration/banner.png and emblem.png.

Everything is drawn at 1x on the pixel grid with no antialiasing, then upscaled 2x with nearest neighbour.
The seal is a gold ring with sixteen pips around a greenhouse dome on a frontier horizon, with a
mountain fort behind it; the masthead sets it on a gold livery band beside the office's name.
Pillow only.
"""
import math
import os

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites",
                   "Administration")
SCALE = 2

# Site palette (boards.yml style) plus a few shades of it.
NAVY = "#0E1E33"      # page background
NAVY_DK = "#07111E"
NAVY_MID = "#1A3152"
HILL = "#0C1A2C"
HILL_LIT = "#3D6794"
SKY_HI = "#24456E"
SKY_LO = "#1A3557"
GLASS = "#2F5F8F"
BLUE = "#7FB2E5"      # link colour
TEXT = "#DCE6F2"
GOLD = "#F2C14E"
GOLD_DK = "#A87B22"
GOLD_LT = "#FFE39A"

# Title capitals: 9 rows, two pixel stems.
BIG = {
    "A": [".####.", "##..##", "##..##", "##..##", "######", "##..##", "##..##", "##..##", "##..##"],
    "C": [".####.", "##..##", "##....", "##....", "##....", "##....", "##....", "##..##", ".####."],
    "D": ["#####.", "##..##", "##..##", "##..##", "##..##", "##..##", "##..##", "##..##", "#####."],
    "I": ["##", "##", "##", "##", "##", "##", "##", "##", "##"],
    "L": ["##....", "##....", "##....", "##....", "##....", "##....", "##....", "##....", "######"],
    "M": ["##....##", "###..###", "########", "##.##.##", "##....##", "##....##", "##....##",
          "##....##", "##....##"],
    "N": ["##...##", "###..##", "###..##", "####.##", "##.####", "##..###", "##..###", "##...##",
          "##...##"],
    "O": [".####.", "##..##", "##..##", "##..##", "##..##", "##..##", "##..##", "##..##", ".####."],
    "R": ["#####.", "##..##", "##..##", "##..##", "#####.", "##.##.", "##..##", "##..##", "##..##"],
    "S": [".####.", "##..##", "##....", "##....", ".####.", "....##", "....##", "##..##", ".####."],
    "T": ["######", "..##..", "..##..", "..##..", "..##..", "..##..", "..##..", "..##..", "..##.."],
    "Y": ["##..##", "##..##", "##..##", "##..##", ".####.", "..##..", "..##..", "..##..", "..##.."],
    " ": ["....", "....", "....", "....", "....", "....", "....", "....", "...."],
}


def _tall(glyph, rows):
    """Stretches a 9 row capital to 11 rows by doubling the two given rows."""
    out = []
    for i, row in enumerate(glyph):
        out.append(row)
        if i in rows:
            out.append(row)
    return out


# Masthead capitals, 11 rows: M doubles its legs so its joints stay one row deep.
TALL = {ch: _tall(g, (5, 6) if ch == "M" else (2, 6)) for ch, g in BIG.items()}

# 5x7 capitals for the secondary lines and the livery band.
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
    "J": ["..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.."],
    "K": ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "M": ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    "N": ["#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "V": ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#."],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "0": [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."],
    "1": ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
    "2": [".###.", "#...#", "....#", "..##.", ".#...", "#....", "#####"],
    "6": ["..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###."],
    "8": [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
    ".": [".", ".", ".", ".", ".", ".", "#"],
    " ": ["...", "...", "...", "...", "...", "...", "..."],
}


def rgba(c):
    c = c.lstrip("#")
    return int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), 255


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

    def text(self, font, x, y, text, c, gap=1):
        for ch in text:
            g = font[ch]
            for gy, row in enumerate(g):
                for gx, bit in enumerate(row):
                    if bit == "#":
                        self.set(x + gx, y + gy, c)
            x += len(g[0]) + gap
        return x

    def save(self, path):
        big = self.img.resize((self.w * SCALE, self.h * SCALE), Image.NEAREST)
        big.save(path)


def text_width(font, text, gap=1):
    return sum(len(font[ch][0]) + gap for ch in text) - gap


def star(cv, cx, cy, c, core=None):
    """A five pixel cross with a lit centre: the seal's pips."""
    for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
        cv.set(cx + dx, cy + dy, c)
    cv.set(cx, cy, core or c)


def seal(cv, cx, cy):
    """The office seal, 63 px across, centred on pixel (cx, cy)."""
    horizon = 9            # ground line, below the centre
    dome_r = 11.5
    for dy in range(-32, 33):
        for dx in range(-32, 33):
            d = math.hypot(dx, dy)
            x, y = cx + dx, cy + dy
            if d > 31.5:
                continue
            if d > 30.5:
                cv.set(x, y, GOLD_DK)          # rim shadow
            elif d > 28.5:
                cv.set(x, y, GOLD)             # outer ring
            elif d > 27.5:
                cv.set(x, y, NAVY_DK)
            elif d > 22.5:
                cv.set(x, y, NAVY)             # pip band
            elif d > 21.5:
                cv.set(x, y, GOLD)             # inner ring
            elif d > 20.5:
                cv.set(x, y, NAVY_DK)
            else:
                # Field: sky, hills, then furrowed greenhouse ground below the horizon.
                if dy < horizon:
                    sky = SKY_LO if dy < 4 or (dy == 4 and dx % 2 == 0) else SKY_HI
                    cv.set(x, y, sky)
                elif dy == horizon:
                    cv.set(x, y, GOLD)
                else:
                    cv.set(x, y, NAVY_MID if (dy - horizon) % 3 == 0 else NAVY_DK)

    # Mountain fort behind the dome, left of centre, and a low ridge to the right.
    # Heights are pixels above the horizon; moonlight from the upper left lights the left faces.
    def ground(dx):
        if dx <= -17:
            return 15 - (-17 - dx)                 # left slope
        if dx <= -7:
            return 15                              # plateau under the fort
        if dx <= 0:
            return max(0, 15 - (dx + 7) * 2)       # steep fall behind the dome
        return max(0, 5 - abs(dx - 13) // 2)       # far ridge

    for dx in range(-20, 21):
        top = ground(dx)
        for h in range(1, top + 1):
            y = cy + horizon - h
            if math.hypot(dx, y - cy) <= 20.5:
                lit = h == top and dx <= -7
                cv.set(cx + dx, y, HILL_LIT if lit else HILL)

    # The fort on the plateau: a crenellated curtain wall, a flat topped keep flying the colony's
    # gold flag, lit faces on the moon side and warm windows.
    base = cy + horizon - 16                 # last row of the fort, sitting on the plateau
    cv.rect(cx - 16, base - 2, cx - 8, base, HILL)                       # curtain wall
    for mx in (-16, -14, -10, -8):
        cv.set(cx + mx, base - 3, HILL)                                  # merlons
    cv.rect(cx - 13, base - 5, cx - 11, base - 3, HILL)                  # keep
    for y in range(base - 3, base + 1):
        cv.set(cx - 16, y, HILL_LIT)                                     # wall's lit face
    for y in range(base - 5, base - 2):
        cv.set(cx - 13, y, HILL_LIT)                                     # keep's lit face
    cv.hline(cx - 13, cx - 11, base - 5, HILL_LIT)                       # keep roof
    for y in range(base - 8, base - 5):
        cv.set(cx - 12, y, TEXT)                                         # flagpole
    cv.rect(cx - 11, base - 8, cx - 10, base - 7, GOLD)                  # flag
    cv.set(cx - 12, base - 4, GOLD_LT)                                   # keep window
    cv.set(cx - 15, base - 1, GOLD)                                      # wall windows
    cv.set(cx - 9, base - 1, GOLD)

    # Greenhouse dome on the horizon: glass, ribs, a lit rim.
    for dy in range(-12, 1):
        for dx in range(-12, 13):
            d = math.hypot(dx, dy)
            if d > dome_r:
                continue
            x, y = cx + dx, cy + horizon - 1 + dy
            if d > dome_r - 1:
                cv.set(x, y, TEXT)
            elif dx in (0, -6, 6) or dy in (-6, -10):
                cv.set(x, y, BLUE)
            else:
                cv.set(x, y, GLASS)
    cv.rect(cx - 13, cy + horizon - 1, cx + 13, cy + horizon - 1, GOLD_LT)   # dome footing
    cv.rect(cx - 1, cy + horizon - 2, cx + 1, cy + horizon - 2, GOLD_LT)     # airlock sill
    cv.rect(cx - 1, cy + horizon - 4, cx + 1, cy + horizon - 3, NAVY_DK)     # airlock door

    # Guiding star above the dome, and two small ones.
    star(cv, cx, cy - 12, GOLD, GOLD_LT)
    cv.set(cx + 10, cy - 14, TEXT)
    cv.set(cx + 15, cy - 7, BLUE)
    cv.set(cx - 6, cy - 16, BLUE)

    # Sixteen pips around the band, a bigger one at the crown.
    for k in range(16):
        a = math.radians(k * 22.5 - 90)
        px = cx + round(25 * math.cos(a))
        py = cy + round(25 * math.sin(a))
        if k == 0:
            star(cv, px, py, GOLD, GOLD_LT)
            cv.set(px - 2, py, GOLD_DK)
            cv.set(px + 2, py, GOLD_DK)
        elif k % 2 == 0:
            star(cv, px, py, GOLD)
        else:
            cv.set(px, py, GOLD_DK)


def banner():
    w, h = 272, 72
    cv = Canvas(w, h, NAVY)
    cx, cy = 35, 35
    x0 = 74
    right = w - 5

    # Livery band, running out from under the seal: thin gold, navy, broad gold, navy, thin gold.
    band = 52
    cv.hline(cx, w - 1, band, GOLD)
    cv.hline(cx, w - 1, band + 1, NAVY_DK)
    cv.rect(cx, band + 2, w - 1, band + 10, GOLD)
    cv.hline(cx, w - 1, band + 11, NAVY_DK)
    cv.hline(cx, w - 1, band + 12, GOLD)
    cv.hline(cx + 31, w - 1, band + 15, BLUE)

    # Lettering on the band.
    cv.text(SMALL, x0, band + 3, "PUBLIC NOTICES", NAVY)
    addr = "ADMIN.COLONY.GOV"
    cv.text(SMALL, right - text_width(SMALL, addr), band + 3, addr, NAVY)
    # Diamond rule between them.
    left_end = x0 + text_width(SMALL, "PUBLIC NOTICES") + 6
    right_start = right - text_width(SMALL, addr) - 6
    cv.hline(left_end, right_start, band + 6, GOLD_DK)
    mid = (left_end + right_start) // 2
    for dx, dy in ((0, -2), (-1, -1), (0, -1), (1, -1), (-2, 0), (-1, 0), (1, 0), (2, 0), (-1, 1), (0, 1),
                   (1, 1), (0, 2)):
        cv.set(mid + dx, band + 6 + dy, NAVY)
    cv.set(mid, band + 6, GOLD_LT)

    # Top rule and the office's name.
    cv.hline(x0, right, 4, GOLD)
    cv.text(SMALL, x0, 9, "COLONIAL CIVIL GOVERNMENT", BLUE)
    title_end = cv.text(TALL, x0, 20, "COLONY ADMINISTRATION", GOLD, gap=2)
    cv.hline(x0, right, 34, GOLD)
    cv.hline(x0, right, 36, GOLD_DK)
    cv.text(SMALL, x0, 41, "OFFICE OF THE ADMINISTRATOR", TEXT)
    for k in range(3):
        star(cv, right - 2 - k * 8, 44, GOLD if k == 1 else GOLD_DK, GOLD_LT if k == 1 else None)
    assert title_end - 2 <= right, "title runs past the rule"

    seal(cv, cx, cy)
    return cv


def emblem():
    cv = Canvas(64, 64)
    seal(cv, 31, 31)
    return cv


def main():
    os.makedirs(OUT, exist_ok=True)
    banner().save(os.path.join(OUT, "banner.png"))
    emblem().save(os.path.join(OUT, "emblem.png"))
    print("wrote", OUT)


if __name__ == "__main__":
    main()
