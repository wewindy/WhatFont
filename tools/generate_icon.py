from pathlib import Path

from PIL import Image, ImageDraw


CANVAS_SIZE = 256
SUPERSAMPLE = 4
ICON_SIZES = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40),
              (48, 48), (64, 64), (128, 128), (256, 256)]


def scaled_box(box: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    return tuple(value * SUPERSAMPLE for value in box)


def main() -> None:
    size = CANVAS_SIZE * SUPERSAMPLE
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    black = (0, 0, 0, 255)
    radius = 5 * SUPERSAMPLE
    draw.rounded_rectangle(scaled_box((76, 56, 103, 201)), radius=radius, fill=black)
    draw.rounded_rectangle(scaled_box((76, 56, 184, 84)), radius=radius, fill=black)
    draw.rounded_rectangle(scaled_box((76, 113, 161, 141)), radius=radius, fill=black)

    image = image.resize((CANVAS_SIZE, CANVAS_SIZE), Image.Resampling.LANCZOS)
    output = Path(__file__).parents[1] / "WhatFont" / "Assets" / "whatfont.ico"
    image.save(output, format="ICO", sizes=ICON_SIZES, bitmap_format="png")


if __name__ == "__main__":
    main()
