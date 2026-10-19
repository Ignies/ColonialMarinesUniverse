"""Draws the masthead and emblem for the ICSC Diplomatic Mission ExtraNet site (embassy.icsc).

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/EmbassyICSC.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/EmbassyICSC/{banner,emblem}.png.

Everything is drawn at 1x on the pixel grid with no antialiasing (hand bitmap glyphs and plain
ImageDraw primitives), then upscaled 2x with nearest neighbour. The emblem is a ring of linked
colony dots around the Core: the free worlds held together by the EntaCOMM relay network.
The livery is the mission's green and silver.
"""
import math
import os

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "EmbassyICSC")
SCALE = 2

BG = "#10261F"
PANEL = "#16332A"
MINT = "#D7EDE3"
JADE = "#6FD3A8"
JADE_DIM = "#3F8A6B"
JADE_DEEP = "#255443"
PALE = "#BFE8D7"
SILVER = "#AEBBB6"
SILVER_DIM = "#6F7E78"

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
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "V": ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    " ": ["...", "...", "...", "...", "...", "...", "..."],
}

# 3x5 capitals and the few marks the small print needs.
F3 = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"], "B": ["##.", "#.#", "##.", "#.#", "##."],
    "C": [".##", "#..", "#..", "#..", ".##"], "D": ["##.", "#.#", "#.#", "#.#", "##."],
    "E": ["###", "#..", "##.", "#..", "###"], "F": ["###", "#..", "##.", "#..", "#.."],
    "G": [".##", "#..", "#.#", "#.#", ".##"], "H": ["#.#", "#.#", "###", "#.#", "#.#"],
    "I": ["###", ".#.", ".#.", ".#.", "###"], "J": ["..#", "..#", "..#", "#.#", ".#."],
    "K": ["#.#", "#.#", "##.", "#.#", "#.#"], "L": ["#..", "#..", "#..", "#..", "###"],
    "M": ["#...#", "##.##", "#.#.#", "#...#", "#...#"], "N": ["#..#", "##.#", "#.##", "#..#", "#..#"],
    "O": ["###", "#.#", "#.#", "#.#", "###"], "P": ["##.", "#.#", "##.", "#..", "#.."],
    "R": ["##.", "#.#", "##.", "#.#", "#.#"], "S": [".##", "#..", ".#.", "..#", "##."],
    "T": ["###", ".#.", ".#.", ".#.", ".#."], "U": ["#.#", "#.#", "#.#", "#.#", "###"],
    "V": ["#.#", "#.#", "#.#", "#.#", ".#."], "W": ["#...#", "#...#", "#.#.#", "##.##", "#...#"],
    "Y": ["#.#", "#.#", ".#.", ".#.", ".#."],
    "0": ["###", "#.#", "#.#", "#.#", "###"], "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["##.", "..#", ".#.", "#..", "###"], "5": ["###", "#..", "##.", "..#", "##."],
    "7": ["###", "..#", ".#.", ".#.", ".#."], "8": ["###", "#.#", "###", "#.#", "###"],
    "-": ["...", "...", "###", "...", "..."], ".": [".", ".", ".", ".", "#"],
    "'": ["#", "#", ".", ".", "."], " ": ["..", "..", "..", "..", ".."],
}


def text_width(font, text, gap=1):
    return sum(len(font[ch][0]) + gap for ch in text) - gap


def glyphs(d, font, x, y, text, colour, gap=1):
    for ch in text:
        g = font[ch]
        for gy, row in enumerate(g):
            for gx, bit in enumerate(row):
                if bit == "#":
                    d.point((x + gx, y + gy), fill=colour)
        x += len(g[0]) + gap
    return x


def rect(d, x0, y0, x1, y1, colour):
    d.rectangle((x0, y0, x1, y1), fill=colour)


def big_letter(d, ch, x, y, colour, h=15, w=14, s=3):
    """Wide geometric capitals in the style of the ICSC paper header: s-pixel strokes, clipped corners."""
    x1, y1 = x + w - 1, y + h - 1
    mid = y + (h - s) // 2
    if ch == "I":
        rect(d, x, y, x + s - 1, y1, colour)
        return x + s
    if ch == "C":
        rect(d, x, y, x1, y + s - 1, colour)
        rect(d, x, y1 - s + 1, x1, y1, colour)
        rect(d, x, y, x + s - 1, y1, colour)
        corners = [(x, y), (x, y1)]
    elif ch == "S":
        rect(d, x, y, x1, y + s - 1, colour)
        rect(d, x, mid, x1, mid + s - 1, colour)
        rect(d, x, y1 - s + 1, x1, y1, colour)
        rect(d, x, y, x + s - 1, mid + s - 1, colour)
        rect(d, x1 - s + 1, mid, x1, y1, colour)
        corners = [(x, y), (x1, y1)]
    else:
        raise KeyError(ch)
    # Clip the outer corners so the letters read as rounded, like the stamped header.
    for cx, cy in corners:
        dx = 1 if cx == x else -1
        dy = 1 if cy == y else -1
        for px, py in ((cx, cy), (cx + dx, cy), (cx, cy + dy)):
            d.point((px, py), fill=BG)
    return x + w


def big_width(text, w=14, s=3, gap=4):
    return sum(s if ch == "I" else w for ch in text) + gap * (len(text) - 1)


def disc(d, cx, cy, r, colour):
    """A filled pixel circle centred on a pixel."""
    for yy in range(-r, r + 1):
        for xx in range(-r, r + 1):
            if xx * xx + yy * yy <= r * r + r * 0.8:
                d.point((cx + xx, cy + yy), fill=colour)


def emblem(size=48, back=None):
    """The ring of linked colony dots: eight worlds on one relay ring, a ninth star above, the Core inside."""
    img = Image.new("RGBA", (size, size), back or (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = size // 2
    ring_r = size // 2 - 6
    points = []
    count = 8
    for i in range(count):
        a = -math.pi / 2 + (i + 0.5) * 2 * math.pi / count
        points.append((c + round(ring_r * math.cos(a)), c + round(ring_r * math.sin(a))))
    # Relay links first, so the colony dots sit on top of them.
    for i in range(count):
        d.line((points[i], points[(i + 1) % count]), fill=SILVER_DIM, width=1)
    # Spokes from the Core to every world.
    for p in points:
        d.line(((c, c), p), fill=JADE_DEEP, width=1)
    # The Core: a jade world with a pale limb and a dark terminator.
    disc(d, c, c, 7, JADE)
    for yy in range(-7, 8):
        for xx in range(-7, 8):
            dist = xx * xx + yy * yy
            if dist <= 49 + 5 and xx + yy >= 5:
                d.point((c + xx, c + yy), fill=JADE_DIM)
    for xx, yy in ((-4, -4), (-3, -5), (-5, -3), (-2, -5), (-5, -2)):
        d.point((c + xx, c + yy), fill=PALE)
    # The colony dots: silver rims, mint cores.
    for px, py in points:
        disc(d, px, py, 3, SILVER)
        disc(d, px, py, 2, MINT)
        d.point((px, py), fill=JADE)
    # The guiding star at the top of the ring, carried over from the ICSC letterhead.
    sx, sy = c, c - ring_r - 1
    star = ["..#..", ".###.", "#####", ".###.", ".#.#."]
    for gy, row in enumerate(star):
        for gx, bit in enumerate(row):
            if bit == "#":
                d.point((sx - 2 + gx, sy - 2 + gy), fill=PALE)
    return img


def banner():
    W, H = 272, 76
    img = Image.new("RGBA", (W, H), BG)
    d = ImageDraw.Draw(img)

    # Top livery: a silver hairline over a thin jade rule.
    rect(d, 0, 2, W - 1, 2, SILVER_DIM)
    rect(d, 0, 4, W - 1, 4, JADE_DEEP)

    # Emblem on a rounded panel at the left.
    px0, py0, px1, py1 = 6, 9, 59, 61
    rect(d, px0 + 1, py0, px1 - 1, py1, PANEL)
    rect(d, px0, py0 + 1, px1, py1 - 1, PANEL)
    img.alpha_composite(emblem(48), (px0 + 3, py0 + 2))

    tx = 68
    # Wordmark, then a silver divider, then the stacked office name.
    y = 12
    x = tx
    for i, ch in enumerate("ICSC"):
        x = big_letter(d, ch, x, y, MINT)
        x += 4
    x -= 4
    div = x + 6
    rect(d, div, y, div, y + 14, SILVER_DIM)
    ox = div + 6
    glyphs(d, F5, ox, y, "DIPLOMATIC", JADE)
    glyphs(d, F5, ox, y + 8, "MISSION", JADE)

    # Right-hand livery block: slanted green and silver stripes, like a hull band.
    bx0, bx1 = 222, 265
    for yy in range(y, y + 15):
        for xx in range(bx0, bx1 + 1):
            k = (xx + yy) % 8
            if k < 3:
                colour = JADE_DIM
            elif k == 4:
                colour = SILVER_DIM
            else:
                continue
            d.point((xx, yy), fill=colour)

    # Rule, full name, small print.
    rect(d, tx, 33, bx1, 33, SILVER_DIM)
    name = "INDEPENDENT CORE SYSTEM COLONIES"
    glyphs(d, F5, tx, 38, name, PALE)
    # The site is on the public net of every map an ICSC ambassador can spawn on, so no colony is named.
    small = "APPOINTED BY THE BOARD OF MINISTRIES"
    glyphs(d, F3, tx, 50, small, SILVER)
    tag = "EMBASSY.ICSC"
    glyphs(d, F3, bx1 + 1 - text_width(F3, tag), 50, tag, JADE)

    # Bottom livery band: silver, jade, silver, with link nodes punched along the jade.
    rect(d, 0, 64, W - 1, 64, SILVER_DIM)
    rect(d, 0, 66, W - 1, 69, JADE_DIM)
    for xx in range(4, W, 12):
        rect(d, xx, 67, xx + 1, 68, BG)
    rect(d, 0, 71, W - 1, 71, SILVER)
    rect(d, 0, 73, W - 1, 73, JADE_DEEP)
    return img


def save(img, name):
    big = img.resize((img.width * SCALE, img.height * SCALE), Image.NEAREST)
    big.save(os.path.join(OUT, name))


def main():
    os.makedirs(OUT, exist_ok=True)
    save(banner(), "banner.png")
    save(emblem(48), "emblem.png")


if __name__ == "__main__":
    main()
