"""Draws The Night Market (bloodmarket.deep) banner and lantern emblem for the deep web.

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/Deep/BloodMarket.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Deep/BloodMarket/{banner,emblem}.png.

Everything is drawn at 1x on the pixel grid with no antialiasing, then upscaled 2x with nearest
neighbour. A string of paper lanterns sags across the top, the big lantern on the left carries a
blood drop, the name is set in 2 pixel block capitals that drip, and a small LED price board sits
on the right. One row of the name is knocked sideways with a green fringe, like a bad signal. Background is the site's wine black, so the banner sits flush on the page.
"""
import os

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "Deep",
                   "BloodMarket")
SCALE = 2

# Palette (site style: background #140709, text #D9C8AA, heading #FFB23E, link #8CFF5A)
BG = "#140709"
SHADOW = "#070203"
PANEL = "#0B0405"
WINE = "#2C0D11"
WINE_HI = "#4A161C"
BLOOD_DK = "#5E0A10"
BLOOD = "#B5121C"
BLOOD_HI = "#FF4A3D"
BONE = "#D9C8AA"
BONE_DK = "#8A7A62"
AMBER_DK = "#7A4A12"
AMBER = "#E08A1E"
AMBER_HI = "#FFB23E"
AMBER_GLOW = "#3A1A0A"
LED = "#8CFF5A"
LED_DK = "#2F5A1E"
WIRE = "#3A2A26"

# Block capitals: 11 rows, 2 pixel strokes.
BLOCK = {
    "N": ["##....##", "###...##", "####..##", "####..##", "##.##.##", "##.##.##",
          "##.##.##", "##..####", "##..####", "##...###", "##....##"],
    "I": ["######", "######", "..##..", "..##..", "..##..", "..##..",
          "..##..", "..##..", "..##..", "######", "######"],
    "G": [".######.", "########", "##....##", "##......", "##......", "##..####",
          "##..####", "##....##", "##....##", "########", ".######."],
    "H": ["##....##", "##....##", "##....##", "##....##", "##....##", "########",
          "########", "##....##", "##....##", "##....##", "##....##"],
    "T": ["########", "########", "...##...", "...##...", "...##...", "...##...",
          "...##...", "...##...", "...##...", "...##...", "...##..."],
    "M": ["##......##", "###....###", "####..####", "##.####.##", "##..##..##", "##......##",
          "##......##", "##......##", "##......##", "##......##", "##......##"],
    "A": [".######.", "########", "##....##", "##....##", "##....##", "########",
          "########", "##....##", "##....##", "##....##", "##....##"],
    "R": ["#######.", "########", "##....##", "##....##", "########", "#######.",
          "##..##..", "##..###.", "##...##.", "##...###", "##....##"],
    "K": ["##....##", "##...###", "##..###.", "##.###..", "#####...", "####....",
          "#####...", "##.###..", "##..###.", "##...###", "##....##"],
    "E": ["########", "########", "##......", "##......", "######..", "######..",
          "##......", "##......", "##......", "########", "########"],
    " ": ["....."] * 11,
}

# 3x5 terminal font.
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
    "?": ["##.", "..#", ".#.", "...", ".#."], "!": ["#", "#", "#", ".", "#"],
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
        self.rect(x0, y, x1, y, c)

    def vline(self, x, y0, y1, c):
        self.rect(x, y0, x, y1, c)

    def bitmap(self, x, y, rows, c):
        for gy, row in enumerate(rows):
            for gx, bit in enumerate(row):
                if bit == "#":
                    self.set(x + gx, y + gy, c)

    def text(self, font, x, y, text, c, gap=1):
        for ch in text:
            g = font.get(ch, font[" "])
            self.bitmap(x, y, g, c)
            x += len(g[0]) + gap
        return x

    def save(self, path):
        big = self.img.resize((self.w * SCALE, self.h * SCALE), Image.NEAREST)
        big.save(path)


def text_width(font, text, gap=1):
    return sum(len(font.get(ch, font[" "])[0]) + gap for ch in text) - gap


# Lanterns
def small_lantern(cv, cx, top, body, hi, dark):
    """A 5x7 paper lantern hanging from (cx, top) with a cap, ribs and a short tassel."""
    cv.vline(cx, top, top + 1, WIRE)
    cv.hline(cx - 1, cx + 1, top + 2, dark)
    # faint glow ring
    for dx, dy in ((-4, 5), (4, 5), (-3, 2), (3, 2), (-3, 8), (3, 8), (0, 11), (-4, 7), (4, 7)):
        if cv.get(cx + dx, top + dy) == rgba(BG):
            cv.set(cx + dx, top + dy, AMBER_GLOW)
    rows = [(1, 3), (2, 4), (2, 5), (2, 6), (2, 7), (1, 8)]
    for hw, y in rows:
        cv.hline(cx - hw, cx + hw, top + y, body)
    cv.vline(cx, top + 4, top + 7, hi)
    cv.set(cx - 1, top + 5, hi)
    cv.hline(cx - 2, cx + 2, top + 6, dark)
    cv.hline(cx - 1, cx + 1, top + 9, dark)
    cv.set(cx, top + 10, dark)


def drop(cv, cx, top, c, hi, h=7):
    """A blood drop, point up, h rows tall."""
    widths = [0, 0, 1, 1, 2, 2, 2, 1][:h] if h <= 8 else [0, 0, 1, 1, 2, 2, 3, 3, 3, 2, 1][:h]
    for i, hw in enumerate(widths):
        cv.hline(cx - hw, cx + hw, top + i, c)
    cv.set(cx - 1, top + h - 4, hi)
    cv.set(cx - 1, top + h - 3, hi)


def big_lantern(cv, cx, top):
    """The emblem: a round paper lantern, ribbed, with a blood drop painted on the paper."""
    W2 = 11  # half width at the belly
    H = 26
    # glow halo, dithered
    for y in range(-4, H + 6):
        for x in range(-W2 - 5, W2 + 6):
            ny = (y - H / 2) / (H / 2 + 5)
            nx = x / (W2 + 5)
            d = nx * nx + ny * ny
            if 0.55 < d < 1.0 and (x + y) % 2 == 0:
                if cv.get(cx + x, top + 4 + y) == rgba(BG):
                    cv.set(cx + x, top + 4 + y, AMBER_GLOW)
    # hanger and cap
    cv.vline(cx, top - 3, top + 1, WIRE)
    cv.hline(cx - 4, cx + 4, top + 2, SHADOW)
    cv.hline(cx - 5, cx + 5, top + 3, AMBER_DK)
    cv.hline(cx - 4, cx + 4, top + 2, AMBER_DK)
    cv.hline(cx - 3, cx + 3, top + 2, WIRE)
    by = top + 4
    # body, a squashed circle drawn row by row
    half = []
    for i in range(H):
        t = (i + 0.5) / H * 2 - 1
        hw = int(round((1 - t * t) ** 0.5 * W2))
        hw = max(hw, 5)
        half.append(hw)
    for i, hw in enumerate(half):
        y = by + i
        cv.hline(cx - hw + 1, cx + hw + 1, y + 1, SHADOW)
    for i, hw in enumerate(half):
        y = by + i
        for x in range(-hw, hw + 1):
            # shade by distance from a light just left of centre
            k = abs(x + 2) / (hw + 1)
            c = AMBER_HI if k < 0.35 else (AMBER if k < 0.75 else AMBER_DK)
            cv.set(cx + x, y, c)
    # ribs every 4 rows
    for i in range(3, H - 1, 4):
        hw = half[i]
        for x in range(-hw, hw + 1):
            cv.set(cx + x, by + i, AMBER_DK if abs(x) < hw - 1 else BLOOD_DK)
    # vertical seam lines
    for i, hw in enumerate(half):
        for sx in (-hw * 2 // 3, hw * 2 // 3):
            if (i % 4) != 3:
                cv.set(cx + sx, by + i, AMBER)
    # outline the rim
    for i, hw in enumerate(half):
        cv.set(cx - hw, by + i, BLOOD_DK)
        cv.set(cx + hw, by + i, BLOOD_DK)
    # painted blood drop
    dtop = by + 7
    rows = [0, 0, 1, 1, 2, 2, 3, 3, 3, 3, 2, 1]
    for i, hw in enumerate(rows):
        cv.hline(cx - hw - 1, cx + hw + 1, dtop + i, BLOOD_DK)
    for i, hw in enumerate(rows):
        if hw >= 1 or i > 0:
            cv.hline(cx - hw, cx + hw, dtop + i, BLOOD)
    cv.set(cx, dtop - 1, BLOOD_DK)
    cv.set(cx - 2, dtop + 6, BLOOD_HI)
    cv.set(cx - 2, dtop + 7, BLOOD_HI)
    cv.set(cx - 1, dtop + 5, BLOOD_HI)
    # bottom cap and tassel
    bot = by + H
    cv.hline(cx - 5, cx + 5, bot, AMBER_DK)
    cv.hline(cx - 4, cx + 4, bot + 1, WIRE)
    cv.vline(cx, bot + 2, bot + 3, BLOOD_DK)
    for dx in (-1, 0, 1):
        cv.vline(cx + dx, bot + 4, bot + 8 - abs(dx), BLOOD)
    cv.set(cx, bot + 9, BLOOD_HI)
    return bot + 9


def wire_y(x, w, top=2, sag=7):
    t = x / max(1, w - 1) * 2 - 1
    return top + int(round(sag * (1 - t * t)))


# Title
def draw_title(cv, x, y, text, drips):
    """Block capitals in blood red with a bone highlight row, a hard shadow and drips."""
    positions = []
    cx = x
    for ch in text:
        positions.append((cx, BLOCK[ch]))
        cx += len(BLOCK[ch][0]) + 2
    end = cx - 2
    for gx, g in positions:
        cv.bitmap(gx + 1, y + 1, g, SHADOW)
    for gx, g in positions:
        cv.bitmap(gx, y, g, BLOOD)
        # top lit edge
        for row in range(len(g)):
            for col, bit in enumerate(g[row]):
                if bit != "#":
                    continue
                above = g[row - 1][col] if row > 0 else "."
                if above != "#":
                    cv.set(gx + col, y + row, BLOOD_HI)
    # drips: (absolute x offset from title start, length)
    base = y + len(BLOCK["N"])
    for dx, ln in drips:
        px = x + dx
        cv.vline(px, base - 1, base + ln - 1, BLOOD)
        cv.set(px + 1, base + ln - 1, BLOOD_DK)
        cv.set(px, base + ln, BLOOD)
        cv.set(px + 1, base + ln, BLOOD)
        cv.set(px, base + ln + 1, BLOOD_DK)
        cv.set(px + 1, base + ln + 1, BLOOD_DK)
        cv.set(px, base + ln - 1, BLOOD_HI)
    return end


def glitch(cv, x0, x1, y, rows, shift, fringe):
    """Slides a band of rows sideways and leaves a coloured ghost where it was."""
    band = [[cv.get(x, y + r) for x in range(x0, x1 + 1)] for r in range(rows)]
    bg = rgba(BG)
    for r in range(rows):
        for i, p in enumerate(band[r]):
            if p != bg and p != rgba(SHADOW):
                cv.set(x0 + i, y + r, fringe)
            elif p == rgba(SHADOW):
                cv.set(x0 + i, y + r, BG)
        for i, p in enumerate(band[r]):
            if p != bg:
                if 0 <= x0 + i + shift < cv.w:
                    cv.img.putpixel((x0 + i + shift, y + r), p)


# Price board
def price_board(cv, x0, y0, x1, y1):
    cv.rect(x0 + 1, y0 + 1, x1 + 1, y1 + 1, SHADOW)
    cv.rect(x0, y0, x1, y1, PANEL)
    cv.hline(x0, x1, y0, WINE_HI)
    cv.hline(x0, x1, y1, WINE)
    cv.vline(x0, y0, y1, WINE_HI)
    cv.vline(x1, y0, y1, WINE)
    # header plate
    head = "TONIGHT"
    cv.rect(x0 + 2, y0 + 2, x1 - 2, y0 + 8, WINE)
    cv.text(F3, (x0 + x1) // 2 - text_width(F3, head) // 2, y0 + 3, head, AMBER_HI)
    lines = [("OXY", "160", LED), ("IDS", "900", LED), ("MAGS", "300", LED), ("BLOOD", "400", BLOOD_HI),
             ("JOE", "???", AMBER)]
    y = y0 + 11
    for item, price, c in lines:
        cv.text(F3, x0 + 4, y, item, BONE)
        # dotted leader
        lx0 = x0 + 4 + text_width(F3, item) + 2
        lx1 = x1 - 4 - text_width(F3, price) - 2
        for lx in range(lx0, lx1 + 1, 2):
            cv.set(lx, y + 4, LED_DK)
        cv.text(F3, x1 - 3 - text_width(F3, price), y, price, c)
        y += 7
    # scanlines over the board
    for sy in range(y0 + 10, y1, 3):
        for sx in range(x0 + 1, x1):
            if cv.get(sx, sy) == rgba(PANEL):
                cv.set(sx, sy, "#0F0607")


def banner():
    W, H = 264, 72
    cv = Canvas(W, H, BG)

    # Lantern string across the top.
    for x in range(W):
        cv.set(x, wire_y(x, W), WIRE)
    palette = [(AMBER, AMBER_HI, AMBER_DK), (BLOOD, BLOOD_HI, BLOOD_DK)]
    k = 0
    for lx in range(52, W - 4, 19):
        body, hi, dark = palette[k % 2]
        small_lantern(cv, lx, wire_y(lx, W) + 1, body, hi, dark)
        k += 1

    # Emblem lantern on the left, hanging from the string.
    ex = 22
    big_lantern(cv, ex, wire_y(ex, W) + 4)

    # Name.
    nx = 46
    cv.text(F3, nx, 19, "-- THE --", AMBER_HI)
    drips = [(3, 5), (12, 3), (25, 7), (38, 4), (55, 6), (61, 2), (78, 5), (96, 8), (107, 3)]
    end = draw_title(cv, nx, 27, "NIGHT MARKET", drips)
    glitch(cv, nx + 62, end + 2, 33, 1, 3, LED_DK)

    # Small print.
    sy = 52
    cv.text(F3, nx, sy, "PRICES AFTER DARK - HANDLES ONLY", BONE)
    ax = cv.text(F3, nx, sy + 8, "BLOODMARKET.DEEP", LED)
    cv.rect(ax + 1, sy + 8, ax + 3, sy + 12, LED)

    # Price board on the right.
    price_board(cv, end + 14, 18, W - 8, 63)

    # Ragged bottom edge: a thin wine rule with a few drops falling off it.
    cv.hline(0, W - 1, H - 3, WINE)
    for dx in (9, 40, 131, 177, 233):
        cv.vline(dx, H - 2, H - 1, BLOOD_DK)
    return cv


def emblem():
    cv = Canvas(40, 48, None)
    big_lantern(cv, 19, 4)
    return cv


def main():
    os.makedirs(OUT, exist_ok=True)
    banner().save(os.path.join(OUT, "banner.png"))
    emblem().save(os.path.join(OUT, "emblem.png"))
    print("wrote", OUT)


if __name__ == "__main__":
    main()
