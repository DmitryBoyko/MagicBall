#!/usr/bin/env python3
"""Иконки приложения из original-icon.png (корень проекта)."""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "original-icon.png"
ICON_512 = ROOT / "icon.png"
ICONS_DIR = ROOT / "assets" / "icons"

SIZES = {
    "app_icon_512.png": 512,
    "app_icon_432.png": 432,
    "app_icon_192.png": 192,
    "app_icon_144.png": 144,
    "app_icon_96.png": 96,
    "app_icon_72.png": 72,
    "app_icon_48.png": 48,
    "adaptive_fg_432.png": 432,
    "adaptive_bg_432.png": 432,
    "adaptive_monochrome_432.png": 432,
}


def _load_source() -> Image.Image:
    if not SOURCE.is_file():
        raise FileNotFoundError(f"Missing source icon: {SOURCE}")
    img = Image.open(SOURCE)
    img.load()
    if img.mode not in ("RGB", "RGBA"):
        img = img.convert("RGBA")
    return img


def _fit_square(img: Image.Image, size: int) -> Image.Image:
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    return ImageOps.fit(img, (size, size), method=Image.Resampling.LANCZOS, centering=(0.5, 0.5))


def _resize_rgb(img: Image.Image, size: int) -> Image.Image:
    return _fit_square(img, size).convert("RGB")


def _adaptive_foreground(img: Image.Image, size: int) -> Image.Image:
    """Логотип в safe-zone (~80% холста) на прозрачном фоне."""
    src = img.convert("RGBA")
    inner = int(round(size * 0.80))
    logo = _fit_square(src, inner)
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ox = (size - inner) // 2
    oy = (size - inner) // 2
    canvas.alpha_composite(logo, (ox, oy))
    return canvas


def _adaptive_background(img: Image.Image, size: int) -> Image.Image:
    """Фон adaptive icon — средний цвет по краям исходника."""
    rgb = img.convert("RGB").resize((64, 64), Image.Resampling.BILINEAR)
    px = list(rgb.getdata())
    edge: list[tuple[int, int, int]] = []
    w = 64
    for y in range(w):
        for x in range(w):
            if x < 4 or y < 4 or x >= w - 4 or y >= w - 4:
                edge.append(px[y * w + x])
    if not edge:
        edge = px
    r = sum(p[0] for p in edge) // len(edge)
    g = sum(p[1] for p in edge) // len(edge)
    b = sum(p[2] for p in edge) // len(edge)
    return Image.new("RGB", (size, size), (r, g, b))


def _adaptive_monochrome(img: Image.Image, size: int) -> Image.Image:
    """Монохром для Android 13+ themed icon."""
    fg = _adaptive_foreground(img, size).convert("L")
    mono = Image.new("RGBA", (size, size), (0, 0, 0, 255))
    white = Image.new("RGBA", (size, size), (255, 255, 255, 255))
    alpha = fg.point(lambda v: 255 if v > 32 else 0)
    white.putalpha(alpha)
    mono.alpha_composite(white)
    return mono.convert("RGB")


def _save(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, optimize=True)


def main() -> None:
    source = _load_source()
    master = _fit_square(source, 512)
    _save(master.convert("RGB"), ICON_512)

    fg_src = _adaptive_foreground(source, 432)
    bg_src = _adaptive_background(source, 432)
    mono_src = _adaptive_monochrome(source, 432)

    for name, px in SIZES.items():
        if name == "adaptive_fg_432.png":
            out = fg_src if px == 432 else fg_src.resize((px, px), Image.Resampling.LANCZOS)
        elif name == "adaptive_bg_432.png":
            out = bg_src if px == 432 else bg_src.resize((px, px), Image.Resampling.LANCZOS)
        elif name == "adaptive_monochrome_432.png":
            out = mono_src if px == 432 else mono_src.resize((px, px), Image.Resampling.LANCZOS)
        else:
            out = _resize_rgb(source, px)
        _save(out, ICONS_DIR / name)

    print(f"Source: {SOURCE.name} {source.size}")
    print(f"Wrote {ICON_512} and {len(SIZES)} files under {ICONS_DIR}")


if __name__ == "__main__":
    main()
