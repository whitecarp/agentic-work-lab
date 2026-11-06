"""Integrates the 9 reviewed 2025-2026 references (exact find/replace edits + 9 BibTeX entries).

Usage (from the repository root): python paper/reference_update/apply_ref_edits.py .
Every 'find' string must occur exactly once; if any check fails, nothing is written.
"""
import sys
from pathlib import Path

root = Path(sys.argv[1])

LI = {
    'Bakhshi2026': "  <li>Bakhshi, J., Matous, P., &amp; Crawford, L. (2026). From control to adaptation: Mapping theoretical developments of project governance research. <em>Project Management Journal</em>, 57(3), 402&ndash;422.</li>\n",
    'Dacre2026': "  <li>Dacre, N. (2026). AI readiness is not enough: Towards purpose-led AI governance in projects. <em>International Journal of Project Management</em>, 44(4), 102850.</li>\n",
    'GetirYaman2025': "  <li>Getir Yaman, S., Ribeiro, P., Cavalcanti, A., Calinescu, R., Paterson, C., &amp; Townsend, B. (2025). Specification, validation and verification of social, legal, ethical, empathetic and cultural requirements for autonomous agents. <em>Journal of Systems and Software</em>, 220, 112229.</li>\n",
    'Khoee2026': "  <li>Khoee, A. G., Wang, S., Feldt, R., Parthasarathy, D., &amp; Yu, Y. (2026). GateLens: A reasoning-enhanced LLM agent for automotive software release analytics. <em>Journal of Systems and Software</em>, 240, 112961.</li>\n",
    'Liu2026': "  <li>Liu, J., Wang, K., Chen, Y., et al. (2026). Large language model-based agents for software engineering: A survey. <em>ACM Transactions on Software Engineering and Methodology</em>. Advance online publication. https://doi.org/10.1145/3796507</li>\n",
    'Liu2025': "  <li>Liu, Y., Lo, S. K., Lu, Q., et al. (2025). Agent design pattern catalogue: A collection of architectural patterns for foundation model based agents. <em>Journal of Systems and Software</em>, 220, 112278.</li>\n",
    'McGrath2025': "  <li>McGrath, Q. P., Hevner, A. R., &amp; de Vreede, G.-J. (2025). Designing an enhanced enterprise risk management system to mitigate ethical risks of artificial intelligence applications. <em>IEEE Transactions on Engineering Management</em>, 72, 1813&ndash;1830.</li>\n",
    'Papadonikolaki2026': "  <li>Papadonikolaki, E. (2026). Rethinking artificial intelligence in projects: A paradox lens. <em>International Journal of Project Management</em>, 44(4), 102857.</li>\n",
    'Shin2026': "  <li>Shin, Y. (2026). From data to experience: Framework for managing artificial intelligence system development projects. <em>International Journal of Project Management</em>, 44(4), 102856.</li>\n",
}

NEW_PARA_2_2 = (
    "<p>Recent software architecture research catalogues design patterns for foundation-model-based agents, "
    "such as goal creators, plan generators and multi-agent cooperation patterns, and analyses their trade-offs "
    "(Liu et al., 2025). Formal methods for the social, legal and ethical rules of autonomous agents detect rule "
    "conflicts and redundancies at design time (Getir Yaman et al., 2025); they complement, rather than replace, "
    "runtime controls such as process locks, content-hash bindings and receipt latches when agents act on external "
    "platforms.</p>\n\n"
)

H23 = "<h3>2.3 DevOps, Release Engineering, and Idempotent State Machines</h3>"

EDITS = {
    'paper/build_publication_pdf.mjs': [
        # E1 Sec. 2.1
        ("from the management of execution itself (Too &amp; Weaver, 2014);",
         "from the management of execution itself (Too &amp; Weaver, 2014; Bakhshi et al., 2026);"),
        # E2 Sec. 2.1
        ("decision authority and human&ndash;machine agency (Holzmann et al., 2022; M&uuml;ller et al., 2024).",
         "decision authority and human&ndash;machine agency (Holzmann et al., 2022; M&uuml;ller et al., 2024; Dacre, 2026; Papadonikolaki, 2026)."),
        # E3 Sec. 2.1
        ("graceful exception handoffs (Parasuraman et al., 2000; Amershi et al., 2019; Shneiderman, 2020).",
         "graceful exception handoffs (Parasuraman et al., 2000; Amershi et al., 2019; Shneiderman, 2020), and recent engineering-management research extends such oversight to enterprise risk management of the ethical risks of AI applications (McGrath et al., 2025)."),
        # E4 Sec. 2.1
        ("relies on conceptual typologies or expert Delphi surveys (Holzmann et al., 2022);",
         "relies on conceptual typologies, expert Delphi surveys or frameworks for managing AI system development (Holzmann et al., 2022; Shin, 2026);"),
        # E5 Sec. 2.2
        ("self-contained coding benchmarks (Wang et al., 2024), but",
         "self-contained coding benchmarks (Wang et al., 2024; Liu et al., 2026), but"),
        # E6 Sec. 2.2 new paragraph, inserted before the Sec. 2.3 heading
        (H23, NEW_PARA_2_2 + H23),
        # E7 Sec. 2.3 new sentence
        ("so the client alone must decide whether a second attempt is safe. Table 1 contrasts",
         "so the client alone must decide whether a second attempt is safe. LLM agents are also entering release engineering, where a formal intermediate representation between natural-language queries and generated code makes release analytics more transparent and reliable (Khoee et al., 2026); release execution against endpoints without idempotency keys needs an equally explicit artifact, which in this study is the persistent client-side receipt. Table 1 contrasts"),
        # E8 Sec. 3.1 style fix (drop the repeated "therefore")
        ("are therefore the author's own.", "are the author's own."),
        # R1-R7 reference-list insertions (alphabetical)
        ("  <li>Bass, L., Weber, I., &amp; Zhu, L. (2015).", LI['Bakhshi2026'] + "  <li>Bass, L., Weber, I., &amp; Zhu, L. (2015)."),
        ("  <li>Flyvbjerg, B. (2006).", LI['Dacre2026'] + "  <li>Flyvbjerg, B. (2006)."),
        ("  <li>Gray, J., &amp; Reuter, A. (1992).", LI['GetirYaman2025'] + "  <li>Gray, J., &amp; Reuter, A. (1992)."),
        ("  <li>Kleppmann, M. (2017).", LI['Khoee2026'] + "  <li>Kleppmann, M. (2017)."),
        ("  <li>M&uuml;ller, R., Locatelli, G.", LI['Liu2026'] + LI['Liu2025'] + LI['McGrath2025'] + "  <li>M&uuml;ller, R., Locatelli, G."),
        ("  <li>Parasuraman, R., Sheridan, T. B.", LI['Papadonikolaki2026'] + "  <li>Parasuraman, R., Sheridan, T. B."),
        ("  <li>Shinn, N., Cassano, F.", LI['Shin2026'] + "  <li>Shinn, N., Cassano, F."),
    ],
    'paper/sync_main_tex.mjs': [
        ("wu2024: 'Wu2023AutoGen', yao2023: 'Yao2023ReAct'",
         "wu2024: 'Wu2023AutoGen', yao2023: 'Yao2023ReAct',\n"
         "  bakhshi2026: 'Bakhshi2026', dacre2026: 'Dacre2026', getiryaman2025: 'GetirYaman2025', khoee2026: 'Khoee2026', liu2025: 'Liu2025',\n"
         "  liu2026: 'Liu2026', mcgrath2025: 'McGrath2025', papadonikolaki2026: 'Papadonikolaki2026', shin2026: 'Shin2026'"),
    ],
    'paper/build_extra_sci_deliverables.mjs': [
        ("3 algorithms, 41 references;", "3 algorithms, 50 references;"),
    ],
    'paper/verify_all_submission_artifacts.mjs': [
        ("BibTeX references (41 entries, all cited;", "BibTeX references (50 entries, all cited;"),
    ],
    'paper/build_defense_and_cn_companion.mjs': [
        ("all 11 tables & 41 references", "all 11 tables & 50 references"),
        ("manuscript (41 references, 11 tables, 5 figures)", "manuscript (50 references, 11 tables, 5 figures)"),
    ],
}

BIB_APPEND = r"""
@article{Bakhshi2026,
  author  = {Bakhshi, Javad and Matous, Petr and Crawford, Lynn},
  title   = {From Control to Adaptation: Mapping Theoretical Developments of Project Governance Research},
  journal = {Project Management Journal},
  volume  = {57},
  number  = {3},
  pages   = {402--422},
  year    = {2026},
  doi     = {10.1177/87569728251383503}
}

@article{Dacre2026,
  author  = {Dacre, Nicholas},
  title   = {{AI} Readiness Is Not Enough: Towards Purpose-Led {AI} Governance in Projects},
  journal = {International Journal of Project Management},
  volume  = {44},
  number  = {4},
  pages   = {102850},
  year    = {2026},
  doi     = {10.1016/j.ijproman.2026.102850}
}

@article{GetirYaman2025,
  author  = {Getir Yaman, Sinem and Ribeiro, Pedro and Cavalcanti, Ana and Calinescu, Radu and Paterson, Colin and Townsend, Beverley},
  title   = {Specification, Validation and Verification of Social, Legal, Ethical, Empathetic and Cultural Requirements for Autonomous Agents},
  journal = {Journal of Systems and Software},
  volume  = {220},
  pages   = {112229},
  year    = {2025},
  doi     = {10.1016/j.jss.2024.112229}
}

@article{Khoee2026,
  author  = {Khoee, Arsham Gholamzadeh and Wang, Shuai and Feldt, Robert and Parthasarathy, Dhasarathy and Yu, Yinan},
  title   = {{GateLens}: A Reasoning-Enhanced {LLM} Agent for Automotive Software Release Analytics},
  journal = {Journal of Systems and Software},
  volume  = {240},
  pages   = {112961},
  year    = {2026},
  doi     = {10.1016/j.jss.2026.112961}
}

@article{Liu2025,
  author  = {Liu, Yue and Lo, Sin Kit and Lu, Qinghua and Zhu, Liming and Zhao, Dehai and Xu, Xiwei and Harrer, Stefan and Whittle, Jon},
  title   = {Agent Design Pattern Catalogue: A Collection of Architectural Patterns for Foundation Model Based Agents},
  journal = {Journal of Systems and Software},
  volume  = {220},
  pages   = {112278},
  year    = {2025},
  doi     = {10.1016/j.jss.2024.112278}
}

@article{Liu2026,
  author  = {Liu, Junwei and Wang, Kaixin and Chen, Yixuan and Peng, Xin and Chen, Zhenpeng and Zhang, Lingming and Lou, Yiling},
  title   = {Large Language Model-Based Agents for Software Engineering: A Survey},
  journal = {ACM Transactions on Software Engineering and Methodology},
  year    = {2026},
  note    = {Advance online publication. https://doi.org/10.1145/3796507},
  doi     = {10.1145/3796507}
}

@article{McGrath2025,
  author  = {McGrath, Quintin P. and Hevner, Alan R. and de Vreede, Gert-Jan},
  title   = {Designing an Enhanced Enterprise Risk Management System to Mitigate Ethical Risks of Artificial Intelligence Applications},
  journal = {IEEE Transactions on Engineering Management},
  volume  = {72},
  pages   = {1813--1830},
  year    = {2025},
  doi     = {10.1109/TEM.2025.3565221}
}

@article{Papadonikolaki2026,
  author  = {Papadonikolaki, Eleni},
  title   = {Rethinking Artificial Intelligence in Projects: A Paradox Lens},
  journal = {International Journal of Project Management},
  volume  = {44},
  number  = {4},
  pages   = {102857},
  year    = {2026},
  doi     = {10.1016/j.ijproman.2026.102857}
}

@article{Shin2026,
  author  = {Shin, Youngsoo},
  title   = {From Data to Experience: Framework for Managing Artificial Intelligence System Development Projects},
  journal = {International Journal of Project Management},
  volume  = {44},
  number  = {4},
  pages   = {102856},
  year    = {2026},
  doi     = {10.1016/j.ijproman.2026.102856}
}
"""

def load(p):
    raw = p.read_bytes().decode('utf-8')
    eol = '\r\n' if '\r\n' in raw else '\n'   # keep each file's existing line endings
    return raw.replace('\r\n', '\n'), eol

def save(p, text, eol):
    p.write_bytes(text.replace('\n', eol).encode('utf-8'))

# Check every file first, so nothing is written unless all edits apply.
staged = {}
for rel, edits in EDITS.items():
    p = root / rel
    text, eol = load(p)
    for find, repl in edits:
        n = text.count(find)
        if n != 1:
            sys.exit(f'STOP (nothing written): {rel}: expected 1 match, found {n}: {find[:80]!r}')
        text = text.replace(find, repl)
    staged[rel] = (p, text, eol, len(edits))

bib = root / 'paper/references.bib'
b, beol = load(bib)
if 'Bakhshi2026' in b or 'Zhan2026' in b:
    sys.exit('STOP (nothing written): references.bib already contains new entries')

for rel, (p, text, eol, n) in staged.items():
    save(p, text, eol)
    print(f'ok  {rel}: {n} edit(s)')
save(bib, b.rstrip('\n') + '\n\n' + BIB_APPEND.strip('\n') + '\n', beol)
print('ok  paper/references.bib: 9 entries appended')
