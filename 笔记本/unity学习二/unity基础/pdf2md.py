#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Convert PDF study notes to Markdown.
Extracts images using correct coordinate scaling and places them inline.
Also fixes font encoding issues (Latin letters encoded as Hangul Syllables in PDFs).
"""

import pdfplumber
from PIL import Image
import os
import sys
import re
from collections import defaultdict

if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8')

EXTRACTED_IMAGES_DIR = "_extracted_images"
RENDER_DPI = 150  # Render resolution (higher = better image quality)


def fix_pdf_encoding(text):
    """Fix font encoding corruption where Latin letters were encoded as Hangul Syllables."""
    result = []
    for c in text:
        cp = ord(c)
        # Uppercase A-Z: U+D434-U+D44D → chr(cp - 0xD3F3)
        if 0xD434 <= cp <= 0xD44D:
            result.append(chr(cp - 0xD3F3))
        # Lowercase a-z: U+D44E-U+D467 → chr(cp - 0xD3ED)
        elif 0xD44E <= cp <= 0xD467:
            result.append(chr(cp - 0xD3ED))
        elif cp == 0xD6FD:  # 훽 → β
            result.append("β")
        elif cp == 0xF06C:  # PUA bullet → •
            result.append("•")
        else:
            result.append(c)
    return "".join(result)


def extract_page_images(page, output_dir, pdf_base, page_num):
    """Extract images from a page. Uses page.images bbox with correct coordinate scaling."""
    # Render full page at chosen DPI
    pil_page = page.to_image(resolution=RENDER_DPI).original
    
    # Scale factors: PDF points (72 DPI) → rendered pixels (RENDER_DPI)
    scale_x = pil_page.width / page.width if page.width > 0 else 1
    scale_y = pil_page.height / page.height if page.height > 0 else 1
    
    images = page.images
    result = []
    
    for idx, img in enumerate(images):
        x0_pt = img["x0"]
        y0_pt = img["top"]
        x1_pt = img["x1"]
        y1_pt = img["bottom"]
        
        w_pt = x1_pt - x0_pt
        h_pt = y1_pt - y0_pt
        
        # Skip tiny decorative elements
        if w_pt < 20 or h_pt < 20:
            continue
        
        # Convert PDF points → rendered pixels
        x0_px = int(x0_pt * scale_x)
        y0_px = int(y0_pt * scale_y)
        x1_px = int(x1_pt * scale_x)
        y1_px = int(y1_pt * scale_y)
        
        # Small margin
        margin = 2
        crop_left = max(0, x0_px - margin)
        crop_upper = max(0, y0_px - margin)
        crop_right = min(pil_page.width, x1_px + margin)
        crop_lower = min(pil_page.height, y1_px + margin)
        
        try:
            cropped = pil_page.crop((crop_left, crop_upper, crop_right, crop_lower))
            
            img_filename = f"{pdf_base}_p{page_num}_img{idx}.png"
            img_path = os.path.join(output_dir, img_filename)
            cropped.save(img_path)
            
            # Use point-coordinate Y center for ordering with text
            y_center = (y0_pt + y1_pt) / 2
            
            result.append({
                "y_center": y_center,
                "y_top": y0_pt,
                "y_bottom": y1_pt,
                "path": img_path,
                "filename": img_filename,
            })
        except Exception as e:
            print(f"  [!] Crop failed image {idx}: {e}", file=sys.stderr)
    
    return result


def extract_text_blocks(page):
    """Extract text paragraphs with their Y positions."""
    words = page.extract_words(keep_blank_chars=True, extra_attrs=["fontname", "size"])
    
    if not words:
        # Fallback: char-level extraction
        chars = page.chars
        if not chars:
            return []
        lines = defaultdict(list)
        for c in chars:
            y_key = round(c["top"], 0)
            lines[y_key].append(c)
        blocks = []
        for y_key in sorted(lines.keys()):
            chs = lines[y_key]
            text = "".join(c["text"] for c in sorted(chs, key=lambda x: x["x0"]))
            text = text.strip()
            if text:
                y_avg = sum(c["top"] for c in chs) / len(chs)
                y_bottom = max(c["bottom"] for c in chs)
                blocks.append({
                    "text": text,
                    "y_center": y_avg, "y_top": y_avg, "y_bottom": y_bottom,
                })
        return blocks
    
    # Group words into lines by Y proximity
    line_threshold = 8
    raw_lines = []
    current_line = []
    current_y = None
    
    for w in sorted(words, key=lambda w: (w["top"], w["x0"])):
        if current_y is None:
            current_y = w["top"]
            current_line = [w]
        elif abs(w["top"] - current_y) < line_threshold:
            current_line.append(w)
        else:
            if current_line:
                raw_lines.append((current_y, current_line))
            current_y = w["top"]
            current_line = [w]
    if current_line:
        raw_lines.append((current_y, current_line))
    
    paragraph_gap = max(w.get("size", 14) for w in words) * 1.5 if words else 20
    
    blocks = []
    for y_top, line_words in raw_lines:
        line_text = "".join(w["text"] for w in sorted(line_words, key=lambda x: x["x0"]))
        line_text = line_text.strip()
        if not line_text:
            continue
        
        y_bottom = max(w["bottom"] for w in line_words)
        y_center = (y_top + y_bottom) / 2
        
        if blocks and (y_top - blocks[-1]["y_bottom"]) < paragraph_gap:
            blocks[-1]["text"] += " " + line_text
            blocks[-1]["y_bottom"] = max(blocks[-1]["y_bottom"], y_bottom)
            blocks[-1]["y_center"] = (blocks[-1]["y_top"] + blocks[-1]["y_bottom"]) / 2
        else:
            blocks.append({
                "text": line_text,
                "y_center": y_center, "y_top": y_top, "y_bottom": y_bottom,
            })
    
    return blocks


def page_to_markdown(page, page_num, output_dir, pdf_base):
    """Convert a single page to markdown, interleaving text and images by Y position."""
    text_blocks = extract_text_blocks(page)
    images = extract_page_images(page, output_dir, pdf_base, page_num)
    
    elements = []
    for tb in text_blocks:
        elements.append(("text", tb))
    for img in images:
        elements.append(("image", img))
    
    elements.sort(key=lambda e: e[1]["y_center"])
    
    current_text = ""
    md_lines = []
    
    for etype, data in elements:
        if etype == "text":
            text = data["text"]
            text = fix_pdf_encoding(text)  # Fix font encoding corruption
            text = re.sub(r'\s+', ' ', text).strip()
            if text:
                current_text += text + " "
        elif etype == "image":
            if current_text.strip():
                md_lines.append(current_text.strip() + "\n")
                current_text = ""
            rel_path = os.path.relpath(data["path"],
                os.path.dirname(os.path.dirname(data["path"])))
            rel_path = rel_path.replace("\\", "/")
            md_lines.append(f"![{data['filename']}]({rel_path})\n")
    
    if current_text.strip():
        md_lines.append(current_text.strip() + "\n")
    
    return "".join(md_lines)


def convert_pdf(pdf_path):
    """Convert a single PDF to markdown."""
    print(f"\n{'='*60}")
    print(f"Converting: {pdf_path}")
    print(f"{'='*60}")
    
    pdf_dir = os.path.dirname(pdf_path)
    pdf_name = os.path.splitext(os.path.basename(pdf_path))[0]
    
    images_dir = os.path.join(pdf_dir, EXTRACTED_IMAGES_DIR)
    os.makedirs(images_dir, exist_ok=True)
    
    try:
        with pdfplumber.open(pdf_path) as pdf:
            total_pages = len(pdf.pages)
            print(f"  Pages: {total_pages}")
            
            md_parts = [
                f"# {pdf_name}\n\n",
                f"> 来源：{os.path.basename(pdf_path)}\n\n",
                f"---\n",
            ]
            
            for i, page in enumerate(pdf.pages):
                print(f"  Processing page {i+1}/{total_pages}...")
                page_md = page_to_markdown(page, i, images_dir, pdf_name)
                md_parts.append(f"\n## Page {i+1}\n")
                md_parts.append(page_md)
            
            md_filename = os.path.join(pdf_dir, f"{pdf_name}.md")
            with open(md_filename, "w", encoding="utf-8") as f:
                f.write("".join(md_parts))
            
            print(f"  ✓ Saved: {md_filename}")
            return True
    
    except Exception as e:
        print(f"  ✗ Error: {e}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        return False


def main():
    root = r"C:\Users\Administrator\Desktop\unity学习二\unity基础"
    
    pdf_files = []
    for dirpath, dirnames, filenames in os.walk(root):
        for f in filenames:
            if f.lower().endswith('.pdf'):
                pdf_files.append(os.path.join(dirpath, f))
    
    pdf_files.sort()
    print(f"Found {len(pdf_files)} PDF files")
    
    success = fail = 0
    for pdf_path in pdf_files:
        if convert_pdf(pdf_path):
            success += 1
        else:
            fail += 1
    
    print(f"\n{'='*60}")
    print(f"Done! {success} succeeded, {fail} failed out of {len(pdf_files)} PDFs")
    print(f"{'='*60}")


if __name__ == "__main__":
    main()
