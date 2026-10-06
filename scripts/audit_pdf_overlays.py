"""Read-only PDF diagnostic: later opaque white rectangles covering earlier text.

This is NOT a general visible-text extractor. Arbitrary clipping paths, images,
rotations, transparency groups and later non-white paint require visual review.
"""
import argparse
import hashlib
import json
from pathlib import Path

import pymupdf


def coverage(text_box, rectangle):
    box = pymupdf.Rect(text_box)
    if box.is_empty or box.is_infinite:
        return 0.0
    return (box & rectangle).get_area() / box.get_area()


def inspect(page):
    traces = page.get_texttrace()
    findings = []
    for paint in page.get_drawings():
        if (paint.get("fill") != (1.0, 1.0, 1.0)
                or paint.get("fill_opacity") != 1.0
                or len(paint["items"]) != 1
                or paint["items"][0][0] != "re"):
            continue
        rect = paint["items"][0][1]
        hidden = [t for t in traces if t["seqno"] < paint["seqno"]
                  and coverage(t["bbox"], rect) >= .99]
        if not hidden:
            continue
        replacements = [t for t in traces if t["seqno"] > paint["seqno"]
                        and coverage(t["bbox"], rect) >= .5]
        def item(t):
            return dict(text="".join(chr(c[0]) for c in t["chars"]),
                        font=t["font"], seqno=t["seqno"], bbox=list(t["bbox"]))
        findings.append(dict(white_rectangle=list(rect), paint_seqno=paint["seqno"],
                             covered_fragments=[item(t) for t in hidden],
                             later_overlapping_text=[item(t) for t in replacements]))
    return findings


def self_test():
    # In-memory fixture: a rectangle before text must not hide that text;
    # later opaque rectangle must flag OLD but not NEW.
    doc = pymupdf.open()
    page = doc.new_page()
    page.draw_rect((10, 10, 150, 70), fill=(1, 1, 1), color=None)
    page.insert_text((20, 40), "OLD", fontsize=12)
    assert inspect(page) == []
    page.draw_rect((10, 10, 150, 70), fill=(1, 1, 1), color=None)
    page.insert_text((20, 40), "NEW", fontsize=12)
    result = inspect(page)
    assert len(result) == 1
    assert [t["text"] for t in result[0]["covered_fragments"]] == ["OLD"]
    assert [t["text"] for t in result[0]["later_overlapping_text"]] == ["NEW"]
    assert coverage((0, 0, 0, 0), pymupdf.Rect(0, 0, 1, 1)) == 0
    doc.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("pdf", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    if args.pdf.resolve() == args.output.resolve():
        parser.error("Output must not overwrite the source PDF.")
    self_test()
    digest = hashlib.sha256(args.pdf.read_bytes()).hexdigest().upper()
    with pymupdf.open(args.pdf) as doc:
        pages = [dict(page=i + 1, candidates=inspect(p)) for i, p in enumerate(doc)]
    if hashlib.sha256(args.pdf.read_bytes()).hexdigest().upper() != digest:
        raise RuntimeError("Source PDF changed during inspection.")
    report = dict(source=str(args.pdf.resolve()), sha256=digest, source_unchanged=True,
                  scope="Potential hidden text from later opaque rectangular white paint. Visual verification required; hidden text is not a current drawing requirement.",
                  self_tests_passed=True, pages=pages)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Self-tests passed; {sum(len(p['candidates']) for p in pages)} candidates; source unchanged.")


if __name__ == "__main__":
    main()
