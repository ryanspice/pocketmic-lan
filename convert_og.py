from PIL import Image, ImageDraw, ImageFont
import os

W, H = 1200, 630
img = Image.new("RGB", (W, H), "#f4f1ea")
draw = ImageDraw.Draw(img)

# Gold accent stripe
draw.rectangle([0, 0, 8, H], fill="#b88a3b")

# Decorative circles (subtle)
draw.ellipse([850, -100, 1250, 300], fill="#efeae0")
draw.ellipse([950, 400, 1250, 700], fill="#efeae0")

# Microphone icon (simplified)
mx, my = 120, 220
draw.rounded_rectangle([mx, my, mx+48, my+68], radius=16, fill="rgba(184,138,59,30)", outline="#b88a3b", width=4)
draw.arc([mx-6, my+48, mx+54, my+96], 0, 180, fill="#b88a3b", width=4)
draw.line([mx+24, my+92, mx+24, my+112], fill="#b88a3b", width=4)
draw.line([mx+10, my+112, mx+38, my+112], fill="#b88a3b", width=4)

# Lock icon (simplified)
lx, ly = 120, 390
draw.rounded_rectangle([lx+8, ly+16, lx+40, ly+44], radius=4, fill="rgba(61,122,107,25)", outline="#3d7a6b", width=3)
draw.arc([lx+12, ly, lx+36, ly+24], 180, 0, fill="#3d7a6b", width=3)

# Load fonts (fallback to default if not available)
try:
    font_title = ImageFont.truetype("C:/Windows/Fonts/SEGOEUI.TTF", 68)
    font_title_bold = ImageFont.truetype("C:/Windows/Fonts/SEGOEUIB.TTF", 68)
    font_sub = ImageFont.truetype("C:/Windows/Fonts/SEGOEUIS.TTF", 30)
    font_body = ImageFont.truetype("C:/Windows/Fonts/SEGOEUI.TTF", 21)
    font_mono = ImageFont.truetype("C:/Windows/Fonts/CONSOLA.TTF", 17)
    font_mono_sm = ImageFont.truetype("C:/Windows/Fonts/CONSOLA.TTF", 12)
    font_badge = ImageFont.truetype("C:/Windows/Fonts/CONSOLAB.TTF", 14)
    font_footer = ImageFont.truetype("C:/Windows/Fonts/SEGOEUI.TTF", 15)
except:
    font_title_bold = ImageFont.load_default()
    font_sub = font_title_bold
    font_body = font_title_bold
    font_mono = font_title_bold
    font_mono_sm = font_title_bold
    font_badge = font_title_bold
    font_footer = font_title_bold

# Title
draw.text((200, 200), "PocketMic LAN", fill="#1a1712", font=font_title_bold)

# Subtitle
draw.text((200, 280), "Encrypted Phone Microphone for Windows", fill="#8a6523", font=font_sub)

# Tagline
draw.text((200, 330), "48 kHz mono PCM16  ·  AES-256-GCM  ·  LAN only", fill="#6d6659", font=font_body)

# Stats badges
badges = [
    ("48 kHz", "mono PCM16"),
    ("10 ms", "UDP packets"),
    ("AES-256-GCM", "authenticated"),
    ("LAN only", "private network"),
]
bx = 200
for label, sublabel in badges:
    bw = max(len(label) * 12, len(sublabel) * 8, 120) + 20
    draw.rounded_rectangle([bx, 400, bx+bw, 455], radius=10, fill="white", outline="#e3ddcf", width=1)
    draw.text((bx + bw//2, 410), label, fill="#1a1712", font=font_mono, anchor="mt")
    draw.text((bx + bw//2, 432), sublabel, fill="#6d6659", font=font_mono_sm, anchor="mt")
    bx += bw + 15

# Version badge
draw.rounded_rectangle([1020, 30, 1140, 66], radius=18, outline="#b88a3b", width=2)
draw.text((1080, 48), "v0.1.4", fill="#8a6523", font=font_badge, anchor="mm")

# Footer
draw.text((200, 570), "MIT Licensed  ·  github.com/canopydigital/pocketmic-lan", fill="#9a9283", font=font_footer)

out = r"B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.4\web\og-image.png"
img.save(out, "PNG")
print(f"Saved: {out}")
print(f"Size: {os.path.getsize(out)} bytes")
