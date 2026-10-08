"""Inventory authored small/shrinking labels. Findings are review candidates, not bugs."""
from collections import Counter
import csv
import json
import re
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / '_Documentation' / 'Usability_Audit'

def main():
    rows, errors = [], []
    for path in sorted((ROOT / 'Config').rglob('*.xml')):
        try:
            tree = ET.parse(path)
        except ET.ParseError as exc:
            errors.append({'file': str(path.relative_to(ROOT)), 'error': str(exc)})
            continue
        def visit(node, context, scrolling=False):
            context = context + '/' + node.tag + ('[' + node.get('name') + ']' if node.get('name') else '')
            scrolling = scrolling or node.tag == 'scrollview'
            if node.tag == 'label':
                font = node.get('font_size', '')
                size = float(font) if font.replace('.', '', 1).isdigit() else None
                overflow = node.get('overflow', 'inherited/default')
                reasons = []
                if size is not None and size < 18: reasons.append('authored font below 18')
                if overflow == 'shrinkcontent': reasons.append('text may shrink')
                if overflow == 'clampcontent' and not scrolling: reasons.append('fixed clipping without scroll ancestor')
                if reasons:
                    rows.append(dict(file=path.relative_to(ROOT).as_posix(), context=context,
                                     font=font, width=node.get('width', ''), height=node.get('height', ''),
                                     overflow=overflow, scroll_ancestor=scrolling,
                                     text_key=node.get('text_key', ''), text=node.get('text', ''),
                                     reasons='; '.join(reasons)))
            for child in node: visit(child, context, scrolling)
        visit(tree.getroot(), '')
    OUT.mkdir(parents=True, exist_ok=True)
    with (OUT / 'label_review_candidates.csv').open('w', encoding='utf-8', newline='') as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]) if rows else ['file'])
        writer.writeheader(); writer.writerows(rows)
    runtime = []
    font_assignment = re.compile(r'\b(?:FontSize|fontSize)\s*=\s*(\d+)')
    for path in sorted((ROOT / 'Scripts').rglob('*.cs')):
        for line_number, line in enumerate(path.read_text(encoding='utf-8-sig').splitlines(), 1):
            match = font_assignment.search(line)
            dynamic = re.search(r'\b(?:FontSize|fontSize)\s*=\s*[^\d\s]', line)
            if (match and int(match.group(1)) < 18) or dynamic or 'ShrinkContent' in line:
                runtime.append(dict(file=path.relative_to(ROOT).as_posix(), line=line_number, source=line.strip()))
    (OUT / 'runtime_label_candidates.json').write_text(json.dumps(runtime, indent=2), encoding='utf-8')
    summary = dict(candidate_labels=len(rows), runtime_candidates=len(runtime), by_file=dict(Counter(r['file'] for r in rows)),
                   parse_errors=errors,
                   limitations=['Authored XML only; conditional branches may duplicate controls.',
                                'Runtime overrides, inherited templates and physical screen scaling need separate review.',
                                'Small decorative labels and hidden controls may be intentional. No automatic global font replacement.'])
    (OUT / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    print(json.dumps({'candidate_labels': len(rows), 'files': len(summary['by_file']), 'parse_errors': len(errors)}))

if __name__ == '__main__': main()
