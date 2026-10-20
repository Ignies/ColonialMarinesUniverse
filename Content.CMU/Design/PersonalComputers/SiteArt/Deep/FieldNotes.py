"""Draws the Field Notes (fieldnotes.deep) banner and emblem for the deep web.

Run from anywhere: python Content.CMU/Design/PersonalComputers/SiteArt/Deep/FieldNotes.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Deep/FieldNotes/{banner,emblem}.png.

The banner is a page torn out of a colony biologist's notebook: faint ruled lines and a rust margin
on near black, the specimen (the game's own xeno egg sprite, xeno_egg.rsi state egg, pasted at 1x and
never redrawn) pencilled in with a scale bar, the title lettered by hand
(5x7 capitals drawn with 2x2 pixel nibs, each letter on a slightly wandering baseline), and acid
burns eaten through the paper with a drip running off one of them. Everything is drawn at 1x on the
pixel grid with no antialiasing, then upscaled 2x with nearest neighbour. The small print uses the
colony ATM generator's 3x5 font, extended with the few characters it lacks.
"""
import importlib.util
import math
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import rsi_sprites as RS  # noqa: E402

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites", "Deep",
                   "FieldNotes")
ATM_ART = os.path.join(REPO, "Content.CMU", "Design", "ColonyEconomy", "AtmPixelArt", "atm_pixel_art.py")
SCALE = 2


def load_atm_art():
    spec = importlib.util.spec_from_file_location("atm_pixel_art", ATM_ART)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ATM = load_atm_art()

# Palette (site style: background #0E0D09, text #D3CCB2, heading #C8EE3C, link #E2A443)
BG = "#0E0D09"
RULE = "#1B1A12"
MARGIN = "#3A1C13"
INK = "#D3CCB2"
INK_DIM = "#7D7763"
INK_FAINT = "#4A4636"
ACID = "#C8EE3C"
ACID_MID = "#7FA21E"
ACID_DARK = "#3D4F10"
HOLE = "#040403"
AMBER = "#E2A443"

# Small print: the ATM 3x5 font plus the characters it does not carry.
F3 = dict(ATM.F3)
F3.update({
    "Q": [".#.", "#.#", "#.#", "##.", ".##"], "X": ["#.#", "#.#", ".#.", "#.#", "#.#"],
    "Z": ["###", "..#", ".#.", "#..", "###"], "K": ["#.#", "#.#", "##.", "#.#", "#.#"],
    ".": [".", ".", ".", ".", "#"], ":": [".", "#", ".", "#", "."],
    "/": ["..#", "..#", ".#.", "#..", "#.."], "7": ["###", "..#", ".#.", ".#.", ".#."],
    "4": ["#.#", "#.#", "###", "..#", "..#"], "3": ["##.", "..#", ".#.", "..#", "##."],
    "8": ["###", "#.#", "###", "#.#", "###"], "9": ["###", "#.#", "###", "..#", "##."],
    "5": ["###", "#..", "##.", "..#", "##."], "6": [".##", "#..", "###", "#.#", "###"],
})

# Hand lettering: 5x7 capitals, each pixel drawn as a 2x2 nib.
HAND = {
    "F": ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    "N": ["#...#", "##..#", "#.#.#", "#.#.#", "#..##", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    " ": ["...", "...", "...", "...", "...", "...", "..."],
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
        return self.img.getpixel((x, y))

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def hline(self, x0, x1, y, c):
        self.rect(x0, y, x1, y, c)

    def vline(self, x, y0, y1, c):
        self.rect(x, y0, x, y1, c)

    def text(self, font, x, y, text, c, gap=1):
        for ch in text:
            g = font.get(ch, font[" "])
            for gy, row in enumerate(g):
                for gx, bit in enumerate(row):
                    if bit == "#":
                        self.set(x + gx, y + gy, c)
            x += len(g[0]) + gap
        return x

    def save(self, path):
        self.img.resize((self.w * SCALE, self.h * SCALE), Image.NEAREST).save(path)


def f3_width(text):
    return sum(len(F3.get(ch, F3[" "])[0]) + 1 for ch in text) - 1


class Rng:
    """Tiny deterministic generator so every run draws the same page."""

    def __init__(self, seed):
        self.s = seed

    def next(self):
        self.s = (self.s * 1103515245 + 12345) & 0x7FFFFFFF
        return self.s

    def chance(self, p):
        return (self.next() % 10000) < p * 10000


# Hand lettered title
WOBBLE = (0, -1, 0, 1, 0, 0, 1, 0, -1, 0, 0)


def hand_width(text):
    return sum(len(HAND[ch][0]) * 2 + 2 for ch in text) - 2


def hand(cv, x, y, text, ink, shadow):
    for i, ch in enumerate(text):
        g = HAND[ch]
        dy = WOBBLE[i % len(WOBBLE)]
        for gy, row in enumerate(g):
            for gx, bit in enumerate(row):
                if bit != "#":
                    continue
                px, py = x + gx * 2, y + dy + gy * 2
                cv.rect(px + 1, py + 1, px + 2, py + 2, shadow)
        for gy, row in enumerate(g):
            for gx, bit in enumerate(row):
                if bit != "#":
                    continue
                px, py = x + gx * 2, y + dy + gy * 2
                cv.rect(px, py, px + 1, py + 1, ink)
        x += len(g[0]) * 2 + 2
    return x


# Specimen: the egg. It exists in the game, so it is the game's own sprite (xeno_egg.rsi, state egg), pasted at 1x
# with nearest neighbour, never redrawn.
EGG_RSI = "_RMC14/Structures/Xenos/xeno_egg.rsi"


def egg(cv, cx, bottom, k=1):
    """Pastes the real egg sprite centred on cx with its foot on bottom; returns its box."""
    img = RS.scaled(RS.trim(RS.frame(EGG_RSI, "egg")), k)
    x0, y0 = cx - img.width // 2, bottom - img.height
    RS.paste(cv.img, img, x0, y0)
    return x0, y0, x0 + img.width, y0 + img.height


def callout(cv, x0, y0, x1, y1, c):
    """A dotted pencil leader from a label to the sketch."""
    steps = max(abs(x1 - x0), abs(y1 - y0))
    for i in range(0, steps + 1, 2):
        cv.set(x0 + (x1 - x0) * i // steps, y0 + (y1 - y0) * i // steps, c)


def burn(cv, cx, cy, r, rng, drip=0):
    """Acid eaten through the page: a ragged black hole with a glowing, darkening rim."""
    edge = []
    for y in range(cy - r - 2, cy + r + 3):
        for x in range(cx - r - 2, cx + r + 3):
            d = ((x - cx) ** 2) * 1.0 + ((y - cy) ** 2) * 1.4
            rough = (rng.next() % 5) - 2
            if d <= (r + rough * 0.3) ** 2:
                cv.set(x, y, HOLE)
            elif d <= (r + 1.2) ** 2:
                edge.append((x, y, ACID))
            elif d <= (r + 2.4) ** 2 and rng.chance(0.7):
                edge.append((x, y, ACID_DARK if rng.chance(0.5) else ACID_MID))
    for x, y, c in edge:
        if cv.get(x, y)[:3] != rgba(HOLE)[:3]:
            cv.set(x, y, c)
    if drip:
        x = cx + 1
        for i in range(drip):
            y = cy + r + 1 + i
            cv.set(x, y, ACID if i < drip - 2 else ACID_MID)
            if i % 3 == 0:
                cv.set(x + 1, y, ACID_DARK)
        cv.set(x, cy + r + 1 + drip, ACID)
        cv.set(x - 1, cy + r + 1 + drip, ACID_DARK)
        cv.set(x + 1, cy + r + 1 + drip, ACID_DARK)


def glitch(cv, x0, x1, y, dx, rows=1):
    """Slips a strip of rows sideways, as a bad dump off a hacked terminal would."""
    for yy in range(y, y + rows):
        line = [cv.get(x, yy) for x in range(x0, x1 + 1)]
        for i, c in enumerate(line):
            x = x0 + i + dx
            if x0 <= x <= x1:
                cv.img.putpixel((x, yy), c)


def page(cv):
    """Ruled notebook paper, a margin line and punched holes down the left edge."""
    for y in range(9, cv.h, 7):
        cv.hline(0, cv.w - 1, y, RULE)
    cv.vline(46, 0, cv.h - 1, MARGIN)
    cv.vline(47, 0, cv.h - 1, MARGIN)
    for y in (12, 32, 52):
        cv.rect(3, y, 5, y + 2, HOLE)
        cv.set(3, y, RULE)
        cv.set(5, y + 2, RULE)


def banner():
    w, h = 260, 66
    cv = Canvas(w, h, BG)
    page(cv)
    rng = Rng(1979)

    # The specimen inside the margin, with a pencilled scale bar down its left side.
    x0, y0, x1, y1 = egg(cv, 27, 55)
    cv.text(F3, 9, 4, "NO.1", INK_DIM)
    callout(cv, 26, 8, x0 + 8, y0 + 1, INK_FAINT)
    cv.vline(10, y0, y1 - 1, INK_FAINT)
    for y in range(y0, y1, 4):
        cv.hline(9, 11 if (y - y0) % 8 else 12, y, INK_FAINT)
    cv.text(F3, 9, 59, "WARM", INK_FAINT)

    # Title.
    title = "FIELD NOTES"
    tx = 58
    ty = 13
    end = hand(cv, tx, ty, title, INK, MARGIN)
    # Underline drawn twice, not quite straight.
    for x in range(tx - 2, end + 2):
        cv.set(x, ty + 18 + (1 if (x // 23) % 2 else 0), ACID_MID)
    for x in range(tx + 6, end - 10):
        cv.set(x, ty + 20, ACID_DARK)

    # Small print.
    cv.text(F3, tx, ty + 25, "SPECIMEN LOG - NOT FOR THE CLINIC FILE", INK_DIM)
    cv.text(F3, tx, ty + 33, "SHIFT 24", AMBER)
    cv.text(F3, tx + f3_width("SHIFT 24") + 6, ty + 33, "THEY ARE NOT ANIMALS", ACID)
    tag = "FIELDNOTES.DEEP"
    cv.text(F3, w - f3_width(tag) - 6, 59, tag, INK_FAINT)

    # Acid burns through the page, one dripping down off the title.
    burn(cv, 226, 17, 5, rng, drip=9)
    burn(cv, 246, 34, 2, rng)
    burn(cv, 238, 50, 3, rng)
    for x, y in ((215, 26), (221, 40), (250, 14), (252, 46)):
        cv.set(x, y, ACID_DARK)

    # Two slipped scan rows across the title.
    glitch(cv, 52, 200, ty + 4, 3)
    glitch(cv, 52, 200, ty + 11, -2)
    return cv


def emblem():
    cv = Canvas(40, 44, BG)
    for y in range(6, cv.h, 7):
        cv.hline(0, cv.w - 1, y, RULE)
    egg(cv, 20, 39)
    return cv


def main():
    os.makedirs(OUT, exist_ok=True)
    banner().save(os.path.join(OUT, "banner.png"))
    emblem().save(os.path.join(OUT, "emblem.png"))
    m = RS.meta(EGG_RSI)
    with open(os.path.join(OUT, "attributions.yml"), "w", encoding="utf-8", newline="\n") as f:
        f.write('- files: ["banner.png", "emblem.png"]\n')
        f.write(f'  license: "{m["license"]}"\n')
        f.write('  copyright: "Pixel-art Field Notes banner drawn for CMU, generated by '
                'Content.CMU/Design/PersonalComputers/SiteArt/Deep/FieldNotes.py. Includes the game sprite '
                f'xeno_egg.rsi ({m["copyright"]})"\n')
        f.write('  source: "https://github.com/Ignies/ColonialMarinesUniverse"\n')
    print("wrote", OUT)


if __name__ == "__main__":
    main()
