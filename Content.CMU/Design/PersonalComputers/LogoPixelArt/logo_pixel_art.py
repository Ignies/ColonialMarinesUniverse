"""Draws the boot splash logos for the personal computer operating systems.

Run from the repo root: python Content.CMU/Design/PersonalComputers/LogoPixelArt/logo_pixel_art.py
Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Logos/{weyland,seegson,sevastolink}.png and
Icons/<os id>_<program>.png at 1x.
The splash upscales them by whole numbers, so every shape is drawn on the pixel grid without antialiasing.
All small text uses the colony ATM generator's 3x5 font, and the Weyland-Yutani logo is the ATM's own boot
mark (Design/ColonyEconomy/AtmPixelArt), so the company looks the same on every screen it boots.
"""
import importlib.util
import os
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Logos")
ATM_ART = os.path.join(REPO, "Content.CMU", "Design", "ColonyEconomy", "AtmPixelArt", "atm_pixel_art.py")

# Seegson's rewire mark: slab letters on a 5x5 grid, rows alternating thick bars and thin joins.
SLAB = {
    "S": ["#####", "#....", "#####", "....#", "#####"],
    "E": ["#####", "#....", "####.", "#....", "#####"],
    "G": ["#####", "#....", "#..##", "#...#", "#####"],
    # Solid with a punched slot, as on the Seegson boot screen.
    "O": ["#####", "#####", "##..#", "#####", "#####"],
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#"],
}
SLAB_ROWS = (3, 2, 3, 2, 3)
SLAB_COL = 3

# Sevastolink's wordmark: 5x7 capitals drawn with two pixel strokes.
WIDE = {
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "V": ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    "K": ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
}


def load_atm_art():
    spec = importlib.util.spec_from_file_location("atm_pixel_art", ATM_ART)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ATM = load_atm_art()


def italic(layer, height, slope):
    """Leans a layer right: row y moves (height - 1 - y) // slope pixels, so the base stays put."""
    leaned = ATM.Layer()
    for (x, y), c in layer.px.items():
        leaned.set(x + (height - 1 - y) // slope, y, c)
    return leaned


def ramp_colour(ramp, y, height):
    return ramp[min(len(ramp) - 1, y * len(ramp) // height)]


def weyland():
    """The Weyland-Yutani mark from the colony ATM's boot screen, in the corporate terminal's amber,
    laid out as the ATM's boot_logo holds it."""
    hot, main, dim = "#ffe2a0", "#ffb547", "#a8681c"
    mark = ATM.Layer()
    width = ATM.wy_logo(mark, 0, 0, h=13, w=8, m=3, gold=main, grey=dim)
    name, motto = "WEYLAND-YUTANI", "BUILDING BETTER WORLDS"
    canvas = max(width, ATM.text_width(ATM.F3, motto)) + 2
    ox = (canvas - width) // 2

    art = ATM.Layer()
    for (px, py), c in mark.px.items():
        art.set(ox + px, py, hot if c == ATM.rgba(ATM.C["gold_hi"]) else c)
    art.glyphs(ATM.F3, ATM.centred(ATM.F3, name, canvas // 2), 19, name, main)
    art.glyphs(ATM.F3, ATM.centred(ATM.F3, motto, canvas // 2), 27, motto, dim)
    return art.image((0, 0, canvas, 32))


def seegson():
    """SEEGSON in leaning yellow slab letters over REWIRE SYSTEM BOOT: the screen a Seegson
    terminal shows while its system comes up."""
    ramp = ("#fff7a8", "#f4e76e", "#f4e76e", "#e2d35a", "#c9bc4a")
    word, gap = "SEEGSON", 3
    height = sum(SLAB_ROWS)
    letters = ATM.Layer()
    x = 0
    for ch in word:
        glyph = SLAB[ch]
        y = 0
        for row, bits in enumerate(glyph):
            for col, bit in enumerate(bits):
                if bit == "#":
                    for dy in range(SLAB_ROWS[row]):
                        colour = ramp_colour(ramp, y + dy, height)
                        letters.rect(x + col * SLAB_COL, y + dy, x + col * SLAB_COL + SLAB_COL - 1, y + dy, colour)
            y += SLAB_ROWS[row]
        x += len(glyph[0]) * SLAB_COL + gap
    word_width = x - gap
    lean = 4
    art = italic(letters, height, lean)
    canvas = word_width + height // lean + 2

    tag = "REWIRE SYSTEM BOOT"
    art.glyphs(ATM.F3, ATM.centred(ATM.F3, tag, canvas // 2), height + 6, tag, "#e2d35a")
    return art.image((0, 0, canvas, height + 11))


def sevastolink():
    """SEVASTOLINK, Seegson's operating system: a leaning green wordmark with scan gaps through its
    strokes and AN LM-LINK PRODUCT under its tail. The splash draws the progress bar beneath it."""
    main, scan = "#3fe05a", "#1f8a33"
    word, gap = "SEVASTOLINK", 2
    height = 7 * 2
    letters = ATM.Layer()
    x = 0
    for ch in word:
        glyph = WIDE[ch]
        for row, bits in enumerate(glyph):
            for col, bit in enumerate(bits):
                if bit != "#":
                    continue
                for dy in range(2):
                    y = row * 2 + dy
                    # Every fourth row runs dim, like the raster lines through the original lettering.
                    letters.rect(x + col * 2, y, x + col * 2 + 1, y, scan if y % 4 == 2 else main)
        x += len(glyph[0]) * 2 + gap
    word_width = x - gap
    lean = 3
    art = italic(letters, height, lean)
    canvas = word_width + height // lean + 2

    tag = "AN LM-LINK PRODUCT"
    art.glyphs(ATM.F3, canvas - ATM.text_width(ATM.F3, tag) - 2, height + 3, tag, main)
    return art.image((0, 0, canvas, height + 8))


# Program icons, 16x16, one set per OS palette: (light, main, dim, dark). The desktop draws them at 2x,
# the taskbar, start menu and title bars at 1x.
ICON_PALETTES = {
    "cmuosseegson": ("#b8ffc6", "#3fe05a", "#1f8a33", "#0f3d18"),
    "cmuosweyland": ("#ffe2a0", "#ffb547", "#a8681c", "#4a2c08"),
}


def icon_browser(light, main, dim, dark):
    """A wireframe globe."""
    from PIL import Image, ImageDraw

    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([1, 1, 14, 14], fill=dark, outline=main)
    d.ellipse([5, 1, 10, 14], outline=dim)
    d.line([(1, 7), (14, 7)], fill=main)
    d.line([(3, 4), (12, 4)], fill=dim)
    d.line([(3, 11), (12, 11)], fill=dim)
    d.line([(7, 1), (7, 14)], fill=main)
    d.point([(4, 3), (3, 4)], fill=light)
    return img


def icon_files(light, main, dim, dark):
    """A folder with a sheet sticking out of it."""
    from PIL import Image, ImageDraw

    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([1, 3, 6, 4], fill=main)
    d.rectangle([7, 0, 13, 7], fill=light, outline=dim)
    d.line([(9, 2), (11, 2)], fill=dim)
    d.line([(9, 4), (11, 4)], fill=dim)
    d.rectangle([1, 5, 14, 14], fill=dark, outline=main)
    d.line([(2, 6), (13, 6)], fill=light)
    d.line([(2, 13), (13, 13)], fill=dim)
    return img


def icon_system(light, main, dim, dark):
    """A monitor with a prompt on its screen."""
    from PIL import Image, ImageDraw

    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([1, 1, 14, 11], fill=dim, outline=main)
    d.rectangle([3, 3, 12, 9], fill=dark)
    d.line([(4, 5), (7, 5)], fill=light)
    d.line([(4, 7), (9, 7)], fill=main)
    d.point([(11, 7)], fill=light)
    d.rectangle([6, 12, 9, 12], fill=main)
    d.rectangle([3, 13, 12, 14], fill=main)
    d.line([(3, 13), (12, 13)], fill=light)
    return img


ICONS = {"browser": icon_browser, "files": icon_files, "system": icon_system}


PREVIEW_BACKGROUNDS = {
    "weyland": (18, 10, 2),
    "seegson": (44, 38, 34),
    "sevastolink": (10, 14, 18),
}


def main():
    from PIL import Image

    os.makedirs(OUT, exist_ok=True)
    for name, build in (("weyland", weyland), ("seegson", seegson), ("sevastolink", sevastolink)):
        img = build()
        img.save(os.path.join(OUT, f"{name}.png"))
        if "--preview" in sys.argv:
            big = Image.new("RGBA", (img.width + 8, img.height + 8), PREVIEW_BACKGROUNDS[name] + (255,))
            big.alpha_composite(img, (4, 4))
            big.resize((big.width * 5, big.height * 5), Image.NEAREST).save(
                os.path.join(sys.argv[-1], f"{name}_preview.png"))


def icons():
    from PIL import Image

    out = os.path.join(os.path.dirname(OUT), "Icons")
    os.makedirs(out, exist_ok=True)
    sheet = []
    for os_id, palette in ICON_PALETTES.items():
        for app, draw in ICONS.items():
            img = draw(*palette)
            img.save(os.path.join(out, f"{os_id}_{app}.png"))
            sheet.append(img)
    if "--preview" in sys.argv:
        strip = Image.new("RGBA", (len(sheet) * 20, 20), (12, 12, 14, 255))
        for i, img in enumerate(sheet):
            strip.alpha_composite(img, (i * 20 + 2, 2))
        strip.resize((strip.width * 6, strip.height * 6), Image.NEAREST).save(os.path.join(sys.argv[-1], "icons_preview.png"))


if __name__ == "__main__":
    icons()
    main()
