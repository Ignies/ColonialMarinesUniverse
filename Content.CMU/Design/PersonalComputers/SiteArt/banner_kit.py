"""Shared toolkit for the personal computer site banners: the deep web ad generator's pixel helpers, lettering from
the game's own fonts, and the game's real sprites.

Site banners are drawn at 1x on a 264x68 pixel grid and saved 2x with nearest neighbour (528x136), the size of the
existing deep web banners, so a sprite pasted at 1x shows at the same pixel size as in the game at 2x zoom.

Rules the banners follow:
  - Anything that exists in the game as a sprite (xenos, eggs, pods, Joes, items, machines) is the real sprite,
    loaded from its .rsi with rsi_sprites and pasted unchanged, or dimmed, tinted or silhouetted for mood.
  - Only backgrounds, frames, lettering and things with no sprite are painted.
  - Lettering is set without antialiasing in the fonts the game ships (Resources/Fonts, OFL licensed), or in the
    hand-built bitmap faces of SiteArt/Ads/Ads.py.

    from banner_kit import *
    b = Banner(BG)
    b.sprite(("_RMC14/Mobs/Xenonids/Runner/runner.rsi", "alive", 0, EAST), 200, 64, anchor="bc", dim=0.4)
    b.text("display_bold", 22, 12, 10, "THE RED DOG WATCH", "#D0582C", shadow="#000000")
    b.write(out_folder, "Pixel-art The Red Dog Watch banner", GENERATOR)

Every sprite a banner uses is recorded and credited, with its own licence and copyright, in the attributions.yml
written next to the banner.
"""

import importlib.util
import json
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import rsi_sprites as RS  # noqa: E402
from rsi_sprites import SOUTH, NORTH, EAST, WEST  # noqa: E402,F401


def _load_ads():
    spec = importlib.util.spec_from_file_location("cmu_site_ads", os.path.join(HERE, "Ads", "Ads.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


A = _load_ads()
# Pixel helpers and bitmap faces from the ad generator.
rgba, mix, ramp, pick, threshold = A.rgba, A.mix, A.ramp, A.pick, A.threshold
Canvas = A.Canvas
m_rect, m_ellipse, m_poly, m_rrect, m_capsule, m_burst = A.m_rect, A.m_ellipse, A.m_poly, A.m_rrect, A.m_capsule, A.m_burst
shift, edge, halo, paint = A.shift, A.edge, A.halo, A.paint
f_cyl, f_hcyl, f_sphere, f_lin = A.f_cyl, A.f_hcyl, A.f_sphere, A.f_lin
BOLD, F5, F3 = A.BOLD, A.F5, A.F3
draw_text, draw_text_c, text_width, bands, bitmap = A.draw_text, A.draw_text_c, A.text_width, A.bands, A.bitmap
vgrad, radial, scanlines, halftone, sunrays, starfield, frame = (A.vgrad, A.radial, A.scanlines, A.halftone,
                                                                 A.sunrays, A.starfield, A.frame)
sticker, price_tag, ribbon, blink, wy_logo, seeg_logo = A.sticker, A.price_tag, A.ribbon, A.blink, A.wy_logo, A.seeg_logo
glow, noise_tex = A.glow, A.noise_tex
WHITE, INK = A.WHITE, A.INK
# A few glyphs the ad faces lack (adding keys leaves the ad generator's own output unchanged).
F3.setdefault("_", ["...", "...", "...", "...", "###"])
F3.setdefault("(", [".#", "#.", "#.", "#.", ".#"])
F3.setdefault(")", ["#.", ".#", ".#", ".#", "#."])
F3.setdefault("#", ["#.#", "###", "#.#", "###", "#.#"])
F3.setdefault("=", ["...", "###", "...", "###", "..."])
F3.setdefault(";", ["..", ".#", "..", ".#", "#."])
F3.setdefault("<", ["..#", ".#.", "#..", ".#.", "..#"])
F5.setdefault("_", [".....", ".....", ".....", ".....", ".....", ".....", "#####"])
F5.setdefault("#", [".#.#.", ".#.#.", "#####", ".#.#.", "#####", ".#.#.", ".#.#."])
F5.setdefault("=", [".....", ".....", "#####", ".....", "#####", ".....", "....."])
F5.setdefault(";", ["..", "..", ".#", "..", "..", ".#", "#."])
F5.setdefault("<", ["...#", "..#.", ".#..", "#...", ".#..", "..#.", "...#"])
N4, N8 = A.N4, A.N8

REPO = RS.REPO
SITES = os.path.join(REPO, "Content.CMU", "Resources", "Textures", "CMU14", "PersonalComputers", "Sites")
SOURCE = "https://github.com/Ignies/ColonialMarinesUniverse"
BW, BH = 264, 68
SCALE = 2

# -- Fonts ------------------------------------------------------------------------------------------------------
FONT_DIR = os.path.join(REPO, "Resources", "Fonts")
FONTS = {
    "display": "NotoSansDisplay/NotoSansDisplay-Regular.ttf",
    "display_bold": "NotoSansDisplay/NotoSansDisplay-Bold.ttf",
    "display_italic": "NotoSansDisplay/NotoSansDisplay-Italic.ttf",
    "display_bold_italic": "NotoSansDisplay/NotoSansDisplay-BoldItalic.ttf",
    "sans": "NotoSans/NotoSans-Regular.ttf",
    "sans_bold": "NotoSans/NotoSans-Bold.ttf",
    "mono": "Cozette/CozetteVector.ttf",
    "mono_bold": "Cozette/CozetteVectorBold.ttf",
    "roboto": "RobotoMono/RobotoMono-Regular.ttf",
    "roboto_bold": "RobotoMono/RobotoMono-Bold.ttf",
    "osd": "UAVOSD/UAV-OSD-Sans-Mono.ttf",
    "box": "Boxfont-round/Boxfont Round.ttf",
    "slab": "Animal Silence.otf",
    "comic": os.path.join("..", "..", "Content.CMU", "Resources", "Fonts", "ComicNeue", "ComicNeue-Bold.ttf"),
}
_font_cache = {}


def font(key, size):
    k = (key, size)
    if k not in _font_cache:
        _font_cache[k] = ImageFont.truetype(os.path.join(FONT_DIR, FONTS[key]), size)
    return _font_cache[k]


def ttf_mask(key, size, text, spacing=0):
    """Pixel set of text set in a game font with no antialiasing, its cap height top at y 0 and its left ink at x 0.
    spacing adds pixels between letters."""
    f = font(key, size)
    cap = f.getbbox("H")[1]
    out = set()
    x = 0
    pieces = [text] if spacing == 0 else list(text)
    for piece in pieces:
        l, t, r, b = f.getbbox(piece)
        w = int(math.ceil(f.getlength(piece)))
        img = Image.new("1", (max(1, w + 8), max(1, b - min(t, cap) + 8)), 0)
        d = ImageDraw.Draw(img)
        d.fontmode = "1"
        oy = 2 - min(t, cap)
        d.text((4, oy), piece, font=f, fill=1)
        px = img.load()
        for yy in range(img.height):
            for xx in range(img.width):
                if px[xx, yy]:
                    out.add((x + xx - 4, yy - oy - cap))
        x += w + spacing
    if out:
        mx = min(p[0] for p in out)
        out = {(px_ - mx, py) for px_, py in out}
    return out


def mask_size(mask):
    if not mask:
        return 0, 0
    return max(p[0] for p in mask) + 1, max(p[1] for p in mask) + 1


def ttf_width(key, size, text, spacing=0):
    return mask_size(ttf_mask(key, size, text, spacing))[0]


def cap_height(key, size):
    return mask_size(ttf_mask(key, size, "H"))[1]


# -- Sprites ----------------------------------------------------------------------------------------------------
ALLOWED = {"CC-BY-SA-3.0", "CC-BY-SA-4.0", "CC0-1.0", "CC-BY-3.0", "CC-BY-4.0"}


def spr(rsi, state, index=0, direction=0):
    return RS.frame(rsi, state, index, direction)


class Banner(Canvas):
    def __init__(self, bg=None, w=BW, h=BH):
        super().__init__(w, h, bg)
        self.credits = {}

    # Sprites.
    def credit(self, rsi):
        m = RS.meta(rsi)
        lic = m.get("license", "")
        if lic not in ALLOWED:
            raise ValueError(f"{rsi} is licensed {lic}; pick another sprite")
        self.credits[rsi] = (lic, m.get("copyright", "").strip())

    def sprite(self, what, x, y, k=1, anchor="tl", trim=True, dim=None, tint=None, tint_amount=1.0, sil=None,
               sil_alpha=255, alpha=None, outline=None, flip=False, credit=None):
        """Pastes a real sprite. what is (rsi, state[, frame[, direction]]) or a PIL image built from sprites
        (then credit lists its RSIs). Returns the pasted box (x0, y0, x1, y1)."""
        if isinstance(what, Image.Image):
            img = what.copy()
            for rsi in credit or ():
                self.credit(rsi)
        else:
            rsi, state = what[0], what[1]
            img = RS.frame(rsi, state, what[2] if len(what) > 2 else 0, what[3] if len(what) > 3 else 0)
            self.credit(rsi)
        if trim:
            img = RS.trim(img)
        if flip:
            img = img.transpose(Image.FLIP_LEFT_RIGHT)
        img = RS.scaled(img, k)
        if dim is not None:
            img = RS.darkened(img, dim)
        if tint is not None:
            img = RS.tinted(img, tint, tint_amount)
        if sil is not None:
            img = RS.silhouette(img, sil, sil_alpha)
        if alpha is not None:
            a = img.getchannel("A").point(lambda v: int(v * alpha))
            img.putalpha(a)
        if outline is not None:
            img = RS.outlined(img, outline)
        w, h = img.size
        ax = {"l": 0, "c": w // 2, "r": w}[anchor[1]] if len(anchor) == 2 else w // 2
        ay = {"t": 0, "c": h // 2, "b": h}[anchor[0]]
        x0, y0 = int(x - ax), int(y - ay)
        RS.paste(self.img, img, x0, y0)
        self.px = self.img.load()
        return x0, y0, x0 + w, y0 + h

    # Lettering in game fonts.
    def text(self, key, size, x, y, text, fill, outline=None, shadow=None, shadow_off=(1, 1), spacing=0,
             anchor="l", diag=True, fit=None):
        """Sets text in a game font; y is the top of the capitals, anchor l, c or r on x. fill is a colour or
        f(row) -> colour with row counted from the cap top. fit, when given, shrinks the size until the text is
        at most that wide. Returns (x0, x1) of the ink."""
        if fit is not None:
            while size > 6 and ttf_width(key, size, text, spacing) > fit:
                size -= 1
        mask = ttf_mask(key, size, text, spacing)
        w, _ = mask_size(mask)
        x0 = x - {"l": 0, "c": w // 2, "r": w}[anchor]
        mask = shift(mask, x0, y)
        ring = halo(mask, diag) if outline is not None else set()
        if shadow is not None:
            self.fill(shift(mask | ring, *shadow_off), shadow)
        if outline is not None:
            self.fill(ring, outline)
        for px_, py in mask:
            self.set(px_, py, fill(py - y) if callable(fill) else fill)
        return x0, x0 + w

    def bm(self, font_, x, y, text, fill, anchor="l", **kw):
        w = text_width(font_, text, kw.get("gap", 1), kw.get("scale", 1), kw.get("italic", 0))
        x0 = x - {"l": 0, "c": w // 2, "r": w}[anchor]
        draw_text(self, font_, x0, y, text, fill, **kw)
        return x0, x0 + w

    # Finishing.
    def write(self, folder, what, generator, name="banner.png"):
        os.makedirs(folder, exist_ok=True)
        self.save(os.path.join(folder, name), scale=SCALE)
        write_attributions(folder, [(name, what, self.credits)], generator)


def write_attributions(folder, entries, generator):
    """entries: (file name, description, {rsi: (licence, copyright)})."""
    lines = []
    for name, what, credits in entries:
        lic = "CC-BY-SA-3.0"
        if any(v[0] == "CC-BY-SA-4.0" for v in credits.values()):
            lic = "CC-BY-SA-4.0"
        text = f"{what} drawn for CMU, generated by {generator}"
        if credits:
            parts = []
            for rsi, (l, c) in sorted(credits.items()):
                parts.append(f"{rsi.split('/')[-1]} ({l}, {c})" if c else f"{rsi.split('/')[-1]} ({l})")
            text += ". Includes game sprites: " + "; ".join(parts)
        text = text.replace('"', "'").replace("\n", " ")
        lines.append(f'- files: ["{name}"]\n  license: "{lic}"\n  copyright: "{text}"\n  source: "{SOURCE}"\n')
    with open(os.path.join(folder, "attributions.yml"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


# -- Shared decoration ------------------------------------------------------------------------------------------


def dither_fill(cv, x0, y0, x1, y1, fn, rmp, dither=4):
    """Fills a box with ramp colours from fn(x, y) -> ramp index."""
    rmp = [rgba(c) for c in rmp]
    for y in range(y0, y1):
        for x in range(x0, x1):
            cv.set(x, y, pick(rmp, fn(x, y), x, y, dither))


def hgrad(cv, x0, y0, x1, y1, colours, dither=4):
    rmp = [rgba(c) for c in colours]
    for x in range(x0, x1):
        v = (x - x0) / max(1, x1 - x0 - 1) * (len(rmp) - 1)
        for y in range(y0, y1):
            cv.set(x, y, pick(rmp, v, x, y, dither))


def darken_box(cv, x0, y0, x1, y1, colour, amount):
    for y in range(max(0, y0), min(cv.h, y1)):
        for x in range(max(0, x0), min(cv.w, x1)):
            cv.set(x, y, mix(cv.get(x, y), colour, amount))


def hazard(cv, x0, y0, x1, y1, on, off, width=3, turn=1):
    for y in range(y0, y1):
        for x in range(x0, x1):
            cv.set(x, y, on if ((x + turn * y) // width) % 2 == 0 else off)


def dashes(cv, x0, x1, y, c, on=2, off=2):
    for x in range(x0, x1):
        if (x - x0) % (on + off) < on:
            cv.set(x, y, c)


def noise(cv, x0, y0, x1, y1, colour, density, seed):
    rng = random.Random(seed)
    for y in range(y0, y1):
        for x in range(x0, x1):
            if rng.random() < density:
                cv.set(x, y, colour)


def vignette(cv, colour, k=0.5, power=2.2):
    cx, cy = cv.w / 2, cv.h / 2
    for y in range(cv.h):
        for x in range(cv.w):
            d = math.hypot((x + 0.5 - cx) / cx, (y + 0.5 - cy) / cy) / math.sqrt(2)
            a = k * d ** power
            if a > 0.01:
                cv.set(x, y, mix(cv.get(x, y), colour, min(1.0, a)))


def soft_glow(cv, cx, cy, rx, ry, colour, strength=0.5):
    glow(cv, cx, cy, rx, ry, colour, strength)


def scan(cv, k=0.86):
    scanlines(cv, 0, 0, cv.w, cv.h, k)


def border(cv, outer, inner=None):
    frame(cv, outer, inner, inner)


def tape(cv, x, y, w, h, colour, alpha=170):
    c = rgba(colour)
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            if (xx == x or xx == x + w - 1) and (yy + xx) % 2:
                continue
            cv.set(xx, yy, c[:3] + (alpha,))


def paper(cv, x0, y0, x1, y1, base, shade, seed=1, ruled=None, rule_step=6):
    """A sheet of paper with a little grain and optional ruled lines."""
    cv.rect(x0, y0, x1, y1, base)
    noise(cv, x0, y0, x1, y1, shade, 0.06, seed)
    if ruled is not None:
        for y in range(y0 + rule_step, y1, rule_step):
            cv.hline(x0 + 1, x1 - 2, y, ruled)


def blur_glow(cv, mask_pts, colour, radius=3.0, strength=0.6):
    """A soft glow around a set of points, blurred with PIL then dithered back onto the grid."""
    m = Image.new("L", (cv.w, cv.h), 0)
    p = m.load()
    for x, y in mask_pts:
        if 0 <= x < cv.w and 0 <= y < cv.h:
            p[x, y] = 255
    s = m.filter(ImageFilter.GaussianBlur(radius)).load()
    for y in range(cv.h):
        for x in range(cv.w):
            a = s[x, y] / 255 * strength
            if a > 0.03 and a * 4 > threshold(x, y, 4) * 2:
                cv.set(x, y, mix(cv.get(x, y), colour, min(0.85, a)))
