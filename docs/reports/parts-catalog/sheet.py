"""Contact sheet of screenshots: sheet.py <out.png> <cols> <file>..."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw

out, cols, files = sys.argv[1], int(sys.argv[2]), sys.argv[3:]
tw, th = 640, 360
rows = (len(files) + cols - 1) // cols
sheet = Image.new("RGB", (cols * tw, rows * (th + 22)), (20, 20, 24))
draw = ImageDraw.Draw(sheet)
for i, f in enumerate(files):
    im = Image.open(f).convert("RGB").resize((tw, th), Image.LANCZOS)
    x, y = (i % cols) * tw, (i // cols) * (th + 22)
    sheet.paste(im, (x, y + 22))
    draw.text((x + 6, y + 5), Path(f).stem, fill=(255, 220, 120))
sheet.save(out)
print(out, sheet.size)
