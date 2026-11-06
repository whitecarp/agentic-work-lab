"""Rebuild both submission ZIPs from paper/: forward-slash paths, LF line endings for text files.

Usage (from the repository root): python paper/reference_update/build_zips.py
"""
import zipfile
from pathlib import Path

P = Path('paper')
TEXT = ('.tex', '.bib', '.md', '.svg')
overleaf = ['main.tex', 'references.bib'] + [f'figures/fig{i}.pdf' for i in range(1, 6)]
complete = overleaf + [
    'manuscript.pdf', 'supplementary_material.pdf',
    'graphical_abstract.pdf', 'graphical_abstract.svg',
    'highlights.pdf', 'highlights.md',
    'declarations_and_credit.pdf', 'declarations_and_credit.md',
    'cover_letters_3_journals.pdf', 'cover_letters_3_journals.md',
]
for name, files in [('SCI_Submission_Overleaf_Package.zip', overleaf),
                    ('SCI_Submission_Complete_Package.zip', complete)]:
    with zipfile.ZipFile(P / name, 'w', zipfile.ZIP_DEFLATED) as z:
        for f in files:
            data = (P / f).read_bytes()
            if f.endswith(TEXT):
                data = data.replace(b'\r\n', b'\n')
            z.writestr(f, data)
    print(f'{name}: {len(files)} entries')
