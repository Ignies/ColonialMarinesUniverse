"""Draws the banner and emblem for Is Your Coworker Synthetic? (thebox.deep), a hidden deep web site.

  python TheBox.py [--preview DIR]

Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Deep/TheBox/{banner,emblem}.png.
The look is a paranoid zine taped to a freezer door: milk white on near black, hazard yellow for the
headings, a teal accent, a glitch tear through the title and a white drip of synthetic blood.
Everything is drawn at 1x on the pixel grid with hand-built bitmap letters, no antialiasing, then
upscaled 2x with nearest neighbour. Pass --preview DIR to also write 4x previews there.
"""
import argparse
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "Deep", "TheBox")
SCALE = 2

W, H = 260, 64          # banner at 1x, 520x128 once upscaled
EW, EH = 52, 52         # emblem at 1x

BG = "#101114"
SCAN = "#15171B"
MILK = "#F4F1E6"
MILK_SHADE = "#C9C4B2"
TEXT = "#DCD8C8"
YELLOW = "#F0E14A"
YELLOW_DARK = "#8F8526"
TEAL = "#86E3C8"
TEAL_DARK = "#2F6B5C"
RED = "#FF4F4F"
GREY = "#5C5F66"
BLACK = "#08090B"

# 5x7 face for the title (drawn 4x) and the tagline.
F5 = {
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "B": ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
    "C": [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "I": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####"],
    "K": ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"],
    "X": ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "?": [".###.", "#...#", "....#", "..##.", "..#..", ".....", "..#.."],
    " ": [".....", ".....", ".....", ".....", ".....", ".....", "....."],
}

# 3x5 face for the small print.
F3 = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"], "B": ["##.", "#.#", "##.", "#.#", "##."],
    "C": [".##", "#..", "#..", "#..", ".##"], "D": ["##.", "#.#", "#.#", "#.#", "##."],
    "E": ["###", "#..", "##.", "#..", "###"], "F": ["###", "#..", "##.", "#..", "#.."],
    "G": [".##", "#..", "#.#", "#.#", ".##"], "H": ["#.#", "#.#", "###", "#.#", "#.#"],
    "I": ["###", ".#.", ".#.", ".#.", "###"], "K": ["#.#", "#.#", "##.", "#.#", "#.#"],
    "L": ["#..", "#..", "#..", "#..", "###"], "M": ["#...#", "##.##", "#.#.#", "#...#", "#...#"],
    "N": ["#..#", "##.#", "#.##", "#..#", "#..#"], "O": [".#.", "#.#", "#.#", "#.#", ".#."],
    "P": ["##.", "#.#", "##.", "#..", "#.."], "R": ["##.", "#.#", "##.", "#.#", "#.#"],
    "S": [".##", "#..", ".#.", "..#", "##."], "T": ["###", ".#.", ".#.", ".#.", ".#."],
    "U": ["#.#", "#.#", "#.#", "#.#", "###"], "V": ["#.#", "#.#", "#.#", "#.#", ".#."],
    "W": ["#.#", "#.#", "###", "###", "#.#"], "X": ["#.#", "#.#", ".#.", "#.#", "#.#"],
    "Y": ["#.#", "#.#", ".#.", ".#.", ".#."],
    "0": ["###", "#.#", "#.#", "#.#", "###"], "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["##.", "..#", ".#.", "#..", "###"], "3": ["##.", "..#", ".#.", "..#", "##."],
    "4": ["#.#", "#.#", "###", "..#", "..#"], "8": ["###", "#.#", "###", "#.#", "###"],
    ".": ["...", "...", "...", "...", ".#."], "/": ["..#", "..#", ".#.", "#..", "#.."],
    "-": ["...", "...", "###", "...", "..."], ":": ["...", ".#.", "...", ".#.", "..."],
    " ": ["...", "...", "...", "...", "..."],
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

    def get(self, x, y):
        return self.img.getpixel((x, y))

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def outline(self, x0, y0, x1, y1, c):
        for x in range(x0, x1 + 1):
            self.set(x, y0, c)
            self.set(x, y1, c)
        for y in range(y0, y1 + 1):
            self.set(x0, y, c)
            self.set(x1, y, c)

    def text(self, font, x, y, s, c, gap=1, scale=1):
        for ch in s:
            g = font.get(ch, font[" "])
            for gy, row in enumerate(g):
                for gx, bit in enumerate(row):
                    if bit == "#":
                        self.rect(x + gx * scale, y + gy * scale,
                                  x + gx * scale + scale - 1, y + gy * scale + scale - 1, c)
            x += (len(g[0]) + gap) * scale if scale == 1 else len(g[0]) * scale + gap
        return x


def text_width(font, s, gap=1, scale=1):
    if scale == 1:
        return sum(len(font.get(ch, font[" "])[0]) + gap for ch in s) - gap
    return sum(len(font.get(ch, font[" "])[0]) * scale + gap for ch in s) - gap


def drip(cv, x, y0, length, bulb=True):
    """A run of white synthetic blood: a 1-2px streak ending in a droplet."""
    for y in range(y0, y0 + length):
        cv.set(x, y, MILK)
        if y < y0 + length // 2:
            cv.set(x + 1, y, MILK_SHADE)
    if bulb:
        yb = y0 + length
        cv.rect(x - 1, yb, x + 1, yb + 1, MILK)
        cv.set(x, yb + 2, MILK)
        cv.set(x + 1, yb, MILK_SHADE)
        cv.set(x - 1, yb, "#FFFFFF")


def draw_emblem(cv, ox, oy):
    """A crate stencilled with a watching eye, white blood leaking out of the bottom."""
    s = 52
    # crate: yellow frame with hazard corners
    cv.rect(ox, oy, ox + s - 1, oy + s - 1, BLACK)
    cv.outline(ox, oy, ox + s - 1, oy + s - 1, YELLOW)
    cv.outline(ox + 1, oy + 1, ox + s - 2, oy + s - 2, YELLOW)
    cv.outline(ox + 3, oy + 3, ox + s - 4, oy + s - 4, YELLOW_DARK)
    for i in range(6):  # corner hazard ticks
        for (cx, cy, dx, dy) in ((ox + 4, oy + 4, 1, 1), (ox + s - 5, oy + 4, -1, 1),
                                 (ox + 4, oy + s - 5, 1, -1), (ox + s - 5, oy + s - 5, -1, -1)):
            if i % 2 == 0:
                cv.set(cx + dx * i, cy, YELLOW)
                cv.set(cx, cy + dy * i, YELLOW)
    # eye: almond made of row spans, centred
    cx, cy = ox + s // 2, oy + 22
    spans = [3, 8, 12, 15, 17, 18, 18, 17, 15, 12, 8, 3]  # half-widths, top to bottom
    top = cy - len(spans) // 2
    for i, hw in enumerate(spans):
        y = top + i
        for x in range(cx - hw, cx + hw):
            cv.set(x, y, MILK)
        cv.set(cx - hw - 1, y, TEXT)
        cv.set(cx + hw, y, TEXT)
    # lids: dark lash line above, shade under
    for i, hw in enumerate(spans[:6]):
        cv.set(cx - hw, top + i - 1, YELLOW)
        cv.set(cx + hw - 1, top + i - 1, YELLOW)
    for x in range(cx - 3, cx + 3):
        cv.set(x, top - 1, YELLOW)
    for i, hw in enumerate(spans[6:]):
        y = top + 6 + i
        cv.set(cx - hw, y, MILK_SHADE)
        cv.set(cx + hw - 1, y, MILK_SHADE)
    # iris and pupil
    iris = [2, 4, 5, 5, 5, 5, 4, 2]
    for i, hw in enumerate(iris):
        y = cy - 4 + i
        for x in range(cx - hw, cx + hw):
            cv.set(x, y, TEAL if (i in (0, 7) or x in (cx - hw, cx + hw - 1)) else TEAL_DARK)
    for y in range(cy - 2, cy + 2):
        for x in range(cx - 2, cx + 2):
            cv.set(x, y, BLACK)
    cv.set(cx - 3, cy - 3, "#FFFFFF")
    cv.set(cx - 2, cy - 3, "#FFFFFF")
    cv.set(cx - 3, cy - 2, "#FFFFFF")
    # a white tear running from the lower lid
    drip(cv, cx + 4, top + len(spans), 9)
    # stencil text on the crate
    label = "SYNTH"
    cv.text(F3, cx - text_width(F3, label) // 2, oy + 41, label, YELLOW)


def draw_banner():
    cv = Canvas(W, H, BG)
    for y in range(0, H, 2):
        for x in range(W):
            cv.set(x, y, SCAN)
    cv.outline(0, 0, W - 1, H - 1, GREY)

    draw_emblem(cv, 5, 5)

    # title: THE BOX, 5x7 drawn at 4x, red and teal ghosts for a cheap chromatic tear
    title = "THE BOX"
    tx, ty, sc, gap = 70, 5, 4, 4
    tw = text_width(F5, title, gap=gap, scale=sc)
    tx = 66 + (W - 6 - 66 - tw) // 2
    title_layer = Canvas(W, H)
    title_layer.text(F5, tx, ty, title, YELLOW, gap=gap, scale=sc)
    shadow = Canvas(W, H)
    shadow.text(F5, tx + 2, ty + 2, title, YELLOW_DARK, gap=gap, scale=sc)
    tear0, tear1 = ty + 12, ty + 15  # rows of the glitch tear: shifted right, split red and teal
    for y in range(H):
        for x in range(W):
            if shadow.img.getpixel((x, y))[3]:
                dx = 3 if tear0 <= y <= tear1 else 0
                cv.set(min(W - 1, x + dx), y, YELLOW_DARK)
    for y in range(H):
        for x in range(W):
            if not title_layer.img.getpixel((x, y))[3]:
                continue
            if tear0 <= y <= tear1:
                cv.set(x + 1, y, RED)
                cv.set(x + 5, y, TEAL)
                cv.set(x + 3, y, YELLOW)
            else:
                cv.set(x, y, YELLOW)
    for y in range(tear0, tear1 + 1):  # second pass so the yellow core sits over both fringes
        for x in range(W):
            if title_layer.img.getpixel((x, y))[3]:
                cv.set(x + 3, y, YELLOW)
    # a drip of white blood out of the bottom of the O
    xo = tx + (5 * sc + gap) * 5 + 2 * sc + 1
    drip(cv, xo, ty + 7 * sc, 2)

    # tagline
    tag = "IS YOUR COWORKER SYNTHETIC?"
    gw = text_width(F5, tag)
    gx = 66 + (W - 6 - 66 - gw) // 2
    cv.text(F5, gx + 1, 41, tag, BLACK)
    cv.text(F5, gx, 40, tag, MILK)

    # hazard band with the address cut into it
    y0, y1 = 51, 58
    x0, x1 = 66, W - 6
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            cv.set(x, y, YELLOW if ((x + y) // 4) % 2 == 0 else BLACK)
    addr = "THEBOX.DEEP // NO LOGS // NO NAMES"
    aw = text_width(F3, addr)
    ax = x1 - aw - 4
    cv.rect(ax - 3, y0, x1, y1, BLACK)
    cv.outline(ax - 3, y0, x1, y1, YELLOW)
    cv.text(F3, ax, y0 + 2, addr, TEAL)
    return cv


def emblem_image():
    cv = Canvas(EW, EH)
    draw_emblem(cv, 0, 0)
    return cv


def up(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    banner = draw_banner().img
    emblem = emblem_image().img
    up(banner, SCALE).save(os.path.join(OUT, "banner.png"))
    up(emblem, SCALE).save(os.path.join(OUT, "emblem.png"))
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)
        up(banner, 4).save(os.path.join(args.preview, "thebox_banner_4x.png"))
        up(emblem, 4).save(os.path.join(args.preview, "thebox_emblem_4x.png"))


if __name__ == "__main__":
    main()
