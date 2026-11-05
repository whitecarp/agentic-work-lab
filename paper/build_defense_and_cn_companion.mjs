// Generates the updated (4 November 2026 revision) companion documents next to this script:
//   1. manuscript_full_EN.md          (converted directly from manuscript_print.html)
//   2. reviewer_defense_playbook.md   (15-question peer-review rebuttal playbook aligned with commit 801b3cc)
//   3. reviewer_defense_playbook.pdf
//   4. manuscript_CN_companion.pdf    (Full Chinese companion aligned with all 11 tables & 41 references)
import fs from 'fs';
import path from 'path';
import { execFileSync } from 'child_process';
import { fileURLToPath } from 'url';

const paperDir = path.dirname(fileURLToPath(import.meta.url));

const edgePaths = [
  'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe'
];
const browserExe = edgePaths.find(p => fs.existsSync(p));

function htmlToPdf(htmlPath, pdfPath) {
  const fileUrl = 'file:///' + htmlPath.replace(/\\/g, '/');
  execFileSync(browserExe, [
    '--headless',
    '--disable-gpu',
    '--no-pdf-header-footer',
    `--print-to-pdf=${pdfPath}`,
    fileUrl
  ], { stdio: 'ignore' });
}

// ============================================================================
// PART 0: GENERATE manuscript_full_EN.md DIRECTLY FROM manuscript_print.html
// ============================================================================
const htmlPath = path.join(paperDir, 'manuscript_print.html');
if (fs.existsSync(htmlPath)) {
  const rawHtml = fs.readFileSync(htmlPath, 'utf8');
  const bodyMatch = rawHtml.match(/<body>([\s\S]*?)<\/body>/i);
  if (bodyMatch) {
    let md = bodyMatch[1]
      .replace(/<svg[\s\S]*?<\/svg>/gi, '\n*[Vector Figure — see `manuscript.pdf` / `figures/figN.pdf`]*\n')
      .replace(/<h1[^>]*>([\s\S]*?)<\/h1>/gi, '# $1\n\n')
      .replace(/<h2[^>]*>([\s\S]*?)<\/h2>/gi, '\n## $1\n\n')
      .replace(/<h3[^>]*>([\s\S]*?)<\/h3>/gi, '\n### $1\n\n')
      .replace(/<p[^>]*>([\s\S]*?)<\/p>/gi, '$1\n\n')
      .replace(/<li>([\s\S]*?)<\/li>/gi, '- $1\n')
      .replace(/<\/?(?:ul|ol|div|span|thead|tbody|table|tr|th|td|caption)[^>]*>/gi, ' ')
      .replace(/<strong>([\s\S]*?)<\/strong>/gi, '**$1**')
      .replace(/<em>([\s\S]*?)<\/em>/gi, '*$1*')
      .replace(/<code>([\s\S]*?)<\/code>/gi, '`$1`')
      .replace(/<br\s*\/?>/gi, '\n')
      .replace(/&mdash;/g, '—')
      .replace(/&ndash;/g, '–')
      .replace(/&minus;/g, '−')
      .replace(/&le;/g, '≤')
      .replace(/&ge;/g, '≥')
      .replace(/&ne;/g, '≠')
      .replace(/&asymp;/g, '≈')
      .replace(/&times;/g, '×')
      .replace(/&rarr;/g, '→')
      .replace(/&larr;/g, '←')
      .replace(/&isin;/g, '∈')
      .replace(/&and;/g, '∧')
      .replace(/&or;/g, '∨')
      .replace(/&empty;/g, '∅')
      .replace(/&ldquo;|&rdquo;/g, '"')
      .replace(/&lsquo;|&rsquo;/g, "'")
      .replace(/&amp;/g, '&')
      .replace(/&lt;/g, '<')
      .replace(/&gt;/g, '>')
      .replace(/&nbsp;/g, ' ')
      .replace(/<[^>]+>/g, '')
      .replace(/[ \t]+\n/g, '\n')
      .replace(/\n{3,}/g, '\n\n')
      .trim() + '\n';
    fs.writeFileSync(path.join(paperDir, 'manuscript_full_EN.md'), md, 'utf8');
  }
}

const css = `
  @page { size: A4 portrait; margin: 16mm 16mm 16mm 16mm; }
  body {
    font-family: "Times New Roman", "Noto Serif SC", "Microsoft YaHei", "SimSun", serif;
    font-size: 10pt;
    line-height: 1.55;
    color: #111827;
    margin: 0;
    padding: 0;
  }
  h1 { font-size: 15.5pt; font-weight: bold; color: #0f172a; border-bottom: 1.5pt solid #0f172a; padding-bottom: 5pt; margin: 0 0 10pt 0; }
  h2 { font-size: 12pt; font-weight: bold; color: #1e3a8a; border-bottom: 0.75pt solid #94a3b8; padding-bottom: 3pt; margin: 14pt 0 6pt 0; }
  h3 { font-size: 10.5pt; font-weight: bold; color: #0f172a; margin: 9pt 0 4pt 0; }
  p { margin: 0 0 6pt 0; text-align: justify; }
  ul, ol { margin: 0 0 8pt 0; padding-left: 18pt; }
  li { margin-bottom: 3.5pt; }
  table { width: 100%; border-collapse: collapse; margin: 7pt 0 11pt 0; font-size: 8.6pt; }
  th { border-top: 1.5pt solid #0f172a; border-bottom: 1pt solid #0f172a; padding: 4pt 5pt; background: #f8fafc; text-align: left; font-weight: bold; }
  td { border-bottom: 0.5pt solid #cbd5e1; padding: 4pt 5pt; vertical-align: top; }
  tr:last-child td { border-bottom: 1.5pt solid #0f172a; }
  .qbox { border-left: 3pt solid #dc2626; background: #fef2f2; padding: 6pt 9pt; margin: 8pt 0 4pt 0; }
  .abox { border-left: 3pt solid #16a34a; background: #f0fdf4; padding: 6pt 9pt; margin: 4pt 0 10pt 0; }
  .fbox { border: 1pt solid #94a3b8; background: #f8fafc; padding: 7pt 10pt; margin: 7pt 0; font-family: "Times New Roman", serif; }
  code { font-family: "Consolas", monospace; font-size: 8.6pt; background: #f1f5f9; padding: 1pt 3pt; border-radius: 2pt; }
  .page-break { page-break-before: always; }
`;

// ============================================================================
// PART A: 15-QUESTION SCI PEER-REVIEW REBUTTAL & DEFENSE PLAYBOOK (4 NOV REVISION)
// ============================================================================
const defenseQuestions = [
  {
    id: "Q1",
    category: "Reviewer #1 (Project Governance & Stage-Gate Theory)",
    q_en: "How does your four-layer architecture advance Stage-Gate project governance theory rather than merely packaging standard DevOps CI/CD scripts?",
    q_cn: "你们的四层架构是如何在理论上推进项目管理“阶段门控（Stage-Gate）”理论的，而不是仅仅封装了几个常规的 DevOps 脚本？",
    a_en: "As formalized in Section 2.4 ('Reading Stage-Gate Control as a Commit Protocol') and Section 7.1, classical Stage-Gate models (Cooper, 1990; Too & Weaver, 2014; Conforto & Amaral, 2016; Bianchi et al., 2022) assume human committees with shared organizational memory, while standard CI/CD pipelines (Humble & Farley, 2010) assume idempotent internal deployment targets. Multi-agent digital delivery violates both assumptions: autonomous LLM workflows lack durable cross-session state, and external publishing platforms expose non-idempotent endpoints without client-supplied idempotency keys (Helland, 2012). We bridge project governance and distributed commit protocols (Gray & Reuter, 1992; Saltzer et al., 1984; Garcia-Molina & Salem, 1987; Bainbridge, 1983) through five correspondences: (1) gatekeeping as deterministic admission control (planRun, Algorithm 1), (2) configuration baselines (Whyte et al., 2016) as cryptographic content addresses (--confirm CURRENT_PLAN_HASH), (3) the go decision as a write-ahead at-most-once commit (persisting UNKNOWN and final_submit_attempted = true before pressing Submit, Proposition 1), (4) post-gate review as an end-to-end read-back check, and (5) human escalation (WAITING_FOR_USER) as resolution of in-doubt transactions.",
    a_cn: "核心防御逻辑：第 2.4 节与第 7.1 节提出了“将阶段门控重构为分布式提交协议（Reading Stage-Gate Control as a Commit Protocol）”的理论综合。外部社交平台缺乏幂等键（Helland, 2012），一旦超时重发就会造成不可逆的重复发帖。我们通过 5 组理论映射将 Cooper (1990) 与 Too & Weaver (2014) 的项目治理理论同 Gray & Reuter (1992) 的预写日志（Write-Ahead）提交协议、Saltzer et al. (1984) 端到端原则及 Garcia-Molina & Salem (1987) Saga 补偿理论融为一体。"
  },
  {
    id: "Q2",
    category: "Reviewer #1 (Human-in-the-Loop Governance & Ironies of Automation)",
    q_en: "Why is a human-gated topology necessary when frameworks like AutoGen, MetaGPT, and ChatDev advocate fully autonomous multi-agent execution?",
    q_cn: "既然 AutoGen、MetaGPT 和 ChatDev 等框架都在主打全自动多智能体协作，为什么你们还要保留人类门控与接管？",
    a_en: "Conversational frameworks (AutoGen, MetaGPT, ChatDev; Table 1) operate inside local code sandboxes where errors can be rolled back via git revert. Live multi-platform publishing mutates public external state that cannot be rolled back and where delete-and-repost destroys work identity and URLs (Garcia-Molina & Salem, 1987). Drawing on Bainbridge's (1983) 'Ironies of Automation', Parasuraman et al. (2000), Shneiderman (2020), and Raisch & Krakowski (2021), automating routine preparation concentrates the remaining human role on precisely the in-doubt or interactive boundaries where automation cannot safely guess (expired platform sessions, CAPTCHAs, or post-submit read-back timeouts). Designing explicit WAITING_FOR_USER and UNKNOWN states turns human intervention into a bounded, auditable transaction resolution step.",
    a_cn: "核心防御逻辑：AutoGen/ChatDev 是在本地沙盒写代码，错了能 git revert；公网发视频一旦连发或删错就是不可逆的公开事故。引用自动化经典文献 Bainbridge (1983)《自动化的讽刺》与 Shneiderman (2020)，常规步骤越自动化，留给去处理的边缘异常（扫码登录、验证码、提交后超时）就越关键。显式设计 WAITING_FOR_USER 与 UNKNOWN 状态，把人工介入变成了受界、可审计的“存疑事务裁决”。"
  },
  {
    id: "Q3",
    category: "Reviewer #1 (DSRM, Action Design Research & Researcher Role)",
    q_en: "Since the author served as both system designer and sole operator during the nine-day field deployment, how do you address researcher-role bias and methodological rigor?",
    q_cn: "既然作者在 9 天现场部署中同时担任系统设计者与唯一操作员，如何应对研究者角色偏差并保证设计科学的方法学严谨性？",
    a_en: "Sections 3.1, 3.3, and 7.4 explicitly situate this study within Design Science Research (Hevner et al., 2004; Peffers et al., 2007; Gregor & Hevner, 2013), Action Design Research (Sein et al., 2011), and Technical Action Research (Wieringa, 2014; Venable et al., 2016). To mitigate single-researcher bias, every empirical claim is anchored in contemporaneous machine-generated receipts committed to two public repositories (agentic-work-lab at data commit e3205d6 and publishops-crosscast at code commit 75b7abc) and recomputed deterministically by paper/reproduce_paper_tables.mjs. Furthermore, Section 7.4 explicitly discloses the dual designer-operator role and separates field observations, automated test results, and analytical baseline comparisons (Table 11).",
    a_cn: "核心防御逻辑：第 3.1、3.3 与 7.4 节依据 Sein et al. (2011) 的行动设计研究（ADR）与 Wieringa (2014) 的技术行动研究规范，坦诚披露了“设计者兼操作员”的双重角色，并通过完全公开的双仓库原始机器回执（数据快照 e3205d6、代码快照 75b7abc）与零依赖复算脚本 paper/reproduce_paper_tables.mjs 消除主观叙述偏差。"
  },
  {
    id: "Q4",
    category: "Reviewer #2 (Distributed Systems & Proposition 1 Safety Guarantee)",
    q_en: "In Proposition 1 and Algorithm 2, why is the receipt saved with state UNKNOWN and final_submit_attempted = true BEFORE pressing the final submit button, and what is the exact exception (A4)?",
    q_cn: "在命题 1（Proposition 1）与算法 2 中，为什么要在按下最终提交按钮之前先把状态 UNKNOWN 和 final_submit_attempted = true 落盘？例外条款 (A4) 又是什么？",
    a_en: "Without platform-side idempotency keys, a client cannot achieve exactly-once delivery on its own; it must choose between at-least-once and at-most-once (Helland, 2012; Kleppmann, 2017). In publishops-crosscast (browser_backends.py at commit 75b7abc), the adapter writes the receipt with state UNKNOWN and final_submit_attempted = true immediately BEFORE invoking the single final submit press ('Persist BEFORE clicking. A timeout must never cause another click'). Under Proposition 1 (Assumptions A1–A4), if a crash or timeout occurs during or after the press, any subsequent run sees final_submit_attempted = true and refuses to press again, with one explicit exception implemented in code (A4): a resumed run may press again only when the platform itself shows that the earlier work is still an unsubmitted draft. Thus Proposition 1 provides a formal safety guarantee (at-most-once automated submission) rather than a liveness guarantee.",
    a_cn: "核心防御逻辑：对应 browser_backends.py 的真实注释与代码（“Persist BEFORE clicking”）：在点击提交按钮前先行把 UNKNOWN 和 final_submit_attempted = true 原子落盘。命题 1 严谨列出了假设 A1–A4，并如实说明了代码中的唯一例外 (A4)：仅当远端平台明确显示此前作品仍处于“未提交草稿（unsubmitted draft）”时才允许恢复点击；同时明确指出这是安全性保证（Safety，最多提交一次）而非活性保证（Liveness）。"
  },
  {
    id: "Q5",
    category: "Reviewer #2 (Field Evidence of Crash & Timeout Recovery)",
    q_en: "What concrete field evidence supports your release state machine beyond unit tests?",
    q_cn: "除了单元测试之外，有哪些具体的现场实测证据支撑你们的发布回执状态机？",
    a_en: "Section 6.3 and Tables 5–6 document three concrete operational traces from the repositories: (1) during the five-platform release of the flagship film, the Douyin adapter completed upload and submission but timed out during post-submit verification, latching the receipt into UNKNOWN (with states PREPARING -> UNKNOWN -> SUBMISSION_VERIFIED / SUBMITTED_UNVERIFIED / WAITING_FOR_USER in Figure 2) without re-uploading; (2) a separate Douyin scheduled-card test on 1 November exhibited the same read-back timeout and likewise held without duplicate submission; and (3) during a private YouTube upload check on 30 October (v0.15.1), a process killed after 8,388,608 acknowledged bytes resumed in a second process and completed under the same video ID rather than creating a duplicate upload.",
    a_cn: "核心防御逻辑：第 6.3 节与表 5–6 给出了 3 个硬核实测证据：① 五平台发布中抖音出现回读超时，状态机安全锁入 UNKNOWN 未重传；② 11 月 1 日抖音定时卡片测试复现同类回读超时，同样零重复提交；③ 10 月 30 日 YouTube 私享上传测试中，进程在已确认传输 8,388,608 字节后被强制杀死，第二进程成功续传并保持同一视频 ID，未产生重复视频。"
  },
  {
    id: "Q6",
    category: "Reviewer #2 (Post-Publication Revision & Deletion — Algorithm 3)",
    q_en: "How does Algorithm 3 prevent destructive compensation (such as delete-and-repost) during post-publication edits and deletions?",
    q_cn: "算法 3 是如何在发布后修订（vpub edit）与删除（vpub delete）中防止“先删帖再重发”这类破坏性操作的？",
    a_en: "In Saga terminology (Garcia-Molina & Salem, 1987), deleting and reposting a live social media item is not a clean compensating transaction because it destroys the post's URL, engagement history, and timestamp. Algorithm 3 (implemented in PRs #20, #21, #25, #29, and #30 at commit 75b7abc) checks expected_identity before any action and uses four exact status codes: EDIT_VERIFIED (live metadata already matches target; no save click issued), EDIT_MISMATCH (dry-run reports differences without saving), EDIT_UNAVAILABLE (on X, where the post menu exposes no native Edit action, the adapter stops without deleting, reposting, or replying), and EDIT_SUBMITTED_UNVERIFIED. For single-work deletion (vpub delete), the adapter requires a prior dry-run and explicit human approval of the target work, then refreshes the platform catalogue to confirm the work's absence.",
    a_cn: "核心防御逻辑：算法 3 严格对齐了真实代码中的四个状态码（EDIT_VERIFIED、EDIT_MISMATCH、EDIT_UNAVAILABLE、EDIT_SUBMITTED_UNVERIFIED）。特别是在 X 平台没有原生编辑按钮时，直接返回 EDIT_UNAVAILABLE 并终止，绝不刪帖重发；删除作品则必须先经过 dry-run 与人工确认，并在删除后刷新列表确认该作品已不存在。"
  },
  {
    id: "Q7",
    category: "Reviewer #3 (Operational Journal Evidence & Passive vs. Active Evidence)",
    q_en: "In Table 3, why do you state that the 69-record operational journal shows 'absence of writes' rather than claiming it directly proves '68 suppressed writes'?",
    q_cn: "在表 3 中，为什么你们严格区分“日志记录中未发生未授权写入”与“主动拦截了 68 次越权尝试”？",
    a_en: "Because scientific honesty requires distinguishing passive outcome logs from active refusal logs (Sections 6.1 and 7.4). In data/operations-events.json (N=69 records; 46 timestamped across 25.3 hours from 26 Oct 01:27 to 27 Oct 02:45 UTC+8), the journal records what each checkpoint did, not what it refused: 20 records carry explicit zero-write counters (13 reads, 0 writes), 39 record platform_calls = 0, 1 records a read-only platform call without a write field (#22), 1 records the authorized platform write (#52: 4 reads, 1 write, replay = 0, new_risk = false), and 8 early records carry no call/write counter fields. While this outcome log is fully consistent with the gate, direct proof that planRun blocks unauthorized callers, stop flags, and unscheduled jobs comes from the 10 unit tests in tests/workflows.test.mjs (10/10 passed) and the scheduled heartbeat evidence.",
    a_cn: "核心防御逻辑：这是主动化解审稿人质疑的绝佳设计！运维日志记录的是“每次检查的结果（仅 #52 写了 1 次，其余 0 写入）”，而不是“拦截了多少次恶意攻击”。我们在表 3 按 JSON 真实字段分成 20 / 39 / 1 / 1 / 8 五类（总计 17 次平台读取、1 次写入），同时指出门控的主动拦截逻辑由 workflows.test.mjs（10/10 通过）直接证明，并在未来工作中提出增加“拒绝尝试日志”。这种严谨度让审稿人挑不出任何毛病！"
  },
  {
    id: "Q8",
    category: "Reviewer #3 (Software Scale, Bundled Code Disclosure & Snapshot Pointers)",
    q_en: "How are the code and test counts in Table 4 computed, and how do you handle bundled third-party code and repository evolution?",
    q_cn: "表 4 中的代码与测试规模是如何计算的？你们是如何处理内置第三方代码与仓库版本演进的？",
    a_en: "All counts in Table 4 are computed at the evaluated head commit 75b7abc (3 November 2026) using physical line counts (via paper/reproduce_paper_tables.mjs) and compared transparently against the v0.16.0 release tag (commit a46bd19: 107 source files, 84 test files, 2,009 test functions). At 75b7abc, publishops-crosscast contains 123 Python source files (26,972 physical lines), of which 42 files (8,967 lines) are an unmodified bundled copy of MIT-licensed bili-cli v1.2.1 (disclosed in NOTICE.md) and 81 files (18,005 lines) constitute CrossCast's own package. The test suite comprises 126 files (122 test_* modules + 4 helper files, 37,029 physical lines, 2,737 def test_* functions), yielding a test-to-source line ratio of 1.37 overall and 2.06 against CrossCast's own package.",
    a_cn: "核心防御逻辑：表 4 不仅用物理行数精确复算了 commit 75b7abc（2026年11月3日快照）的全部代码，还主动披露其中 42 个文件（8,967 行）是原样内置的 MIT 开源组件 bili-cli v1.2.1，自研 CrossCast 包为 81 个文件（18,005 行）；对应 126 个测试文件（37,029 行，2,737 个测试函数），自研测试源码比高达 2.06，且同时给出了 v0.16.0（a46bd19）的对比数字，做到了 100% 透明。"
  },
  {
    id: "Q9",
    category: "Reviewer #3 (Instagram Timing Interpretation & N=1 Scope)",
    q_en: "In Table 7 and Figure 5, what does the 380.551 s (92.32%) Instagram step actually measure, and can you generalize that exact percentage to all platforms?",
    q_cn: "在表 7 和图 5 中，Instagram 的 380.551 秒（92.32%）具体测的是什么？这个百分比能直接推广到所有平台吗？",
    a_en: "As stated in Section 6.3 and Table 7 (N = 1 timed Instagram release: 423.236 s end-to-end, 412.217 s inside vpub publish), the 380.551 s post-Share phase spans from the single Share click until the platform returns confirmation. From outside the platform's servers, client telemetry cannot separate network media transfer completion, server-side transcoding/processing, and final confirmation within that 380.551 s window, and the phase breakdown selects major phases rather than an exhaustive additive split (leaving a 15.384 s derived residual inside vpub publish). This study does not claim the 92.32% figure transfers numerically to other platforms; rather, it empirically illustrates the structural hazard that motivates Layer 3: a long post-press in-doubt window where an unguarded timeout retry would press Submit a second time.",
    a_cn: "核心防御逻辑：我们在摘要、Highlights 与第 6.3 节明确说明：380.551 秒（92.32%）来自单次完整计时的 Instagram 发布（N=1），且从客户端视角无法把该窗口内的上传传输、服务端转码与确认拆开（命令内部其余未拆分步骤派生余项为 15.384 秒）。我们不宣称“所有平台都是 92.32%”，而是用这一实测证据直观揭示“点击提交后存在漫长的存疑窗口”，从而论证在点击前先行落盘 UNKNOWN 状态的架构必然性。"
  },
  {
    id: "Q10",
    category: "Reviewer #3 (Cohort Complete Separation & Exposure-Age Confounding)",
    q_en: "In Section 6.5 and Table 10, why do you report descriptive quantiles for the 55 Bilibili records without running a significance test?",
    q_cn: "在第 6.5 节和表 10 中，为什么你们对 55 条 Bilibili 记录只报告分位数等描述统计，而不做显著性假设检验？",
    a_en: "In data/creator-video-snapshot.json (N = 55 records: n = 45 in human_led_history published Oct 2019–Oct 2026, and n = 10 in current_ai_series comprising 5 published videos aged 1.6 hours to 3.1 days and 5 scheduled releases), every single historical video has more views than every single newly published AI-series video: min(human_led_history) = 274 views (mean 40,884.31, SD 64,604.81, median 15,407.0, Q1–Q3 1,722–67,502, max 333,725) versus max(current_ai_series) = 231 views (mean 133.20, SD 99.53, median 183.0, Q1–Q3 42–199, min 11). Because the two distributions exhibit complete separation (274 > 231) across a multi-year versus multi-hour exposure gap, a rank test on views mechanically yields U = 0 and would merely restate exposure age rather than content quality. Furthermore, weekly play growth (76,162 vs. 21,398) was led by older travel vlogs and 88.09% TV-client traffic, reinforcing why raw channel totals must not be attributed to the new AI workflow.",
    a_cn: "核心防御逻辑：在 55 条快照中，45 条人类历史视频最低播放量是 274（Q1–Q3 为 1,722–67,502），而 5 条刚发布 1.6 小时–3.1 天的 AI 视频最高播放量是 231（Q1–Q3 为 42–199；274 > 231，两组样本完全不重叠，即 Complete Separation，若硬做秩检验 U 恒等于 0）。而且周播放增长（76,162 vs 21,398）主要来自电视端（88.09%）推荐的老旅行视频。因此不做误导性的显著性检验、如实呈现曝光时长混杂，恰恰是第 4 层“认知治理（Epistemic Governance）”的核心示范！"
  },
  {
    id: "Q11",
    category: "Reviewer #3 (Analytical Comparison vs. Executed Ablation — Table 11)",
    q_en: "How are the baseline alternatives in Table 11 evaluated if you did not intentionally run unguarded agents on live public accounts?",
    q_cn: "既然你们没有在真实公开账号上故意运行不设防的基线智能体，表 11 中的基线对比是如何评估的？",
    a_en: "Table 11 is explicitly framed as an analytical comparison rather than an executed ablation trial. For the unguarded baselines, the table states what would occur under each removed layer based on architectural mechanism ('reasoned, not executed'), while for our four-layer design every cell explicitly tags its actual evidence source: field traces (e.g., the two Douyin read-back timeouts and the Instagram login escalation), automated test suites (e.g., workflows.test.mjs and the 2,737 CrossCast test functions), review logs (audio/loudness defect detection in Table 8), or code inspection.",
    a_cn: "核心防御逻辑：表 11 明确界定为“分析性对比（Analytical Comparison）”，将未执行的基线标注为基于机制推导的后果（would occur），而本文方案的每一行都严格标注了实证来源（现场记录 Field、自动化测试 Test、审校记录 Review 或代码实现 Code），不把机制推导伪装成跑过的消融实验。"
  },
  {
    id: "Q12",
    category: "Reviewer #2 (Advisory Scheduling Gate vs. Hard Platform Enforcement)",
    q_en: "If planRun in Layer 1 is a pure JavaScript function, couldn't a rogue script or operator bypass it and call the release CLI directly?",
    q_cn: "既然第 1 层的 planRun 是一个纯函数，如果某个脚本绕过它直接调用发布 CLI 怎么办？",
    a_en: "Section 4.2 explicitly notes this defense-in-depth boundary: planRun is an advisory scheduling gate for the autonomous runner, whereas hard enforcement at the platform boundary resides in Layers 2 and 3 inside publishops-crosscast itself. Even if a caller bypasses planRun, vpub publish refuses to execute a live write without a matching --confirm CURRENT_PLAN_HASH digest (Layer 2) and an unlatched receipt lock (Layer 3).",
    a_cn: "核心防御逻辑：第 4.2 节坦诚说明了这一纵深防御边界：Layer 1（planRun）负责约束后台定时调度器，而真正卡死外部平台写入边界的是内置在 vpub publish 命令内部的 Layer 2（--confirm 计划哈希锁）与 Layer 3（进程锁与回执状态机）。即使绕过 Layer 1，没有匹配的计划哈希与未锁定回执也无法触发发布。"
  },
  {
    id: "Q13",
    category: "Reviewer #2 (Attestation Boundary of --person-approved)",
    q_en: "Does the --confirm digest cryptographically prove that a human being, rather than an agent, typed the command?",
    q_cn: "--confirm 摘要能否在密码学意义上证明敲下命令的一定是人类而不是智能体？",
    a_en: "No, and Section 4.3 states this boundary explicitly: the --confirm CURRENT_PLAN_HASH digest cryptographically binds the approval to the exact metadata and media SHA-256 content reviewed during dry-run (preventing TOCTOU content drift), whereas the --person-approved flag is the caller's attestation that human review took place, as documented in the CLI's own help text.",
    a_cn: "核心防御逻辑：第 4.3 节如实界定了密码学边界：--confirm 哈希在数学上保证的是“实发内容与预演审阅内容字节级一致（防 TOCTOU 篡改）”，而“是否由真人批准（--person-approved）”则是调用方的声明与操作规程边界。"
  },
  {
    id: "Q14",
    category: "Reviewer #1 (Single-Case Generalizability & External Validity)",
    q_en: "What generalizes from a single-organization, nine-day study to other software engineering and project management teams?",
    q_cn: "从单一机构、9 天现场部署的研究中，有哪些结论可以推广到其他软件工程与项目管理团队？",
    a_en: "Following Flyvbjerg (2006), Runeson & Höst (2009), Wieringa (2014), and Wieringa & Daneva (2015), generalization proceeds by architectural mechanism ('analytical generalization', Section 7.4). While platform-specific latency numbers (such as the 380.551 s Instagram wait) are not claimed to transfer numerically, the underlying structural dilemma transfers to any project where AI agents act on external APIs or browser endpoints that lack idempotency keys and transactional rollback: write-ahead UNKNOWN receipt persistence before the final press, content-addressed approval tokens, and identity-bound in-place revision provide a reusable reference architecture.",
    a_cn: "核心防御逻辑：依据 Flyvbjerg (2006) 与 Wieringa & Daneva (2015) 的“按机制分析性推广（Analytical Generalization）”，具体平台的秒数无需照搬，但只要外部接口缺乏幂等键与回滚机制，“点击前预写 UNKNOWN 回执 + 内容寻址计划哈希 + 原位身份核验修订”这一参考架构就具有广泛适用性。"
  },
  {
    id: "Q15",
    category: "Editor-in-Chief (Future Research & Measurable Gate Suppression)",
    q_en: "What is the most important next step to strengthen this governance framework in future empirical deployments?",
    q_cn: "在未来的实证部署中，进一步强化该治理框架最关键的下一步工作是什么？",
    a_en: "As outlined in Section 8, the top priority is to log gate decisions and refused attempts (not only executed outcomes) so that suppression rates become directly measurable, alongside timing bounded concurrent multi-platform runs against sequential execution, running the planned blinded identical-task multi-model comparison with per-call cost logging, and replicating the architecture across independent teams.",
    a_cn: "核心防御逻辑：第 8 节明确提出了 4 项紧扣研究局限的未来工作：① 不仅记录执行结果，还要持久化记录每一次门控决策与被拒绝的尝试，使“拦截率”可直接测量；② 实测受控并发发布与串行发布的性能对比；③ 执行带成本记录的多模型同题盲测；④ 在外部独立团队中开展跨机构复现。"
  }
];

let defenseMd = `# SCI Peer-Review Defense & Rebuttal Playbook (15 Core Questions & Standard Answers)
**Manuscript:** *Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations*  
**Author:** HuaiCheng Sheng (Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd.)  
**Revision Alignment:** Synchronized 100% with the 4 November 2026 peer-review revision (\`main.tex\`, \`manuscript.pdf\`, and \`paper/reproduce_paper_tables.mjs\`).

---

`;

for (const item of defenseQuestions) {
  defenseMd += `## ${item.id} [${item.category}]\n`;
  defenseMd += `- **Reviewer Question (EN):** *${item.q_en}*\n`;
  defenseMd += `- **审稿人问题（中文解析）：** ${item.q_cn}\n\n`;
  defenseMd += `### Standard Academic Rebuttal (Direct Copy-Paste for Response Letter):\n> ${item.a_en}\n\n`;
  defenseMd += `### 核心防御逻辑总结（供作者快速掌握）：\n${item.a_cn}\n\n---\n\n`;
}

fs.writeFileSync(path.join(paperDir, 'reviewer_defense_playbook.md'), defenseMd, 'utf8');

const defenseHtml = `<!DOCTYPE html><html><head><meta charset="utf-8"><style>${css}</style></head><body>
<h1>SCI Peer-Review Defense &amp; Rebuttal Playbook (15 Questions — 4 Nov Revision)</h1>
<p><strong>Manuscript:</strong> Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations<br/>
<strong>Author:</strong> HuaiCheng Sheng (Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd. | ORCID: <code>0009-0003-2900-6377</code>)</p>
<p>This playbook is 100% synchronized with the revised 22-page manuscript (41 references, 11 tables, 5 figures) and <code>paper/reproduce_paper_tables.mjs</code>, providing ready-to-copy English responses and Chinese strategic summaries.</p>
${defenseQuestions.map(item => `
  <div class="qbox">
    <strong>${item.id} [${item.category}]</strong><br/>
    <em>EN:</em> ${item.q_en}<br/>
    <em>CN:</em> ${item.q_cn}
  </div>
  <div class="abox">
    <strong>Standard SCI Rebuttal (English):</strong> ${item.a_en}<br/><br/>
    <strong>${item.a_cn}</strong>
  </div>
`).join('\n')}
</body></html>`;
const defHtmlPath = path.join(paperDir, 'reviewer_defense_print.html');
fs.writeFileSync(defHtmlPath, defenseHtml, 'utf8');
htmlToPdf(defHtmlPath, path.join(paperDir, 'reviewer_defense_playbook.pdf'));

// ============================================================================
// PART B: FULL CHINESE ACADEMIC COMPANION EDITION (全中文完整学术对照精读本 — 4 NOV REVISION)
// ============================================================================
const cnCompanionHtml = `<!DOCTYPE html><html><head><meta charset="utf-8"><style>${css}</style></head><body>
<h1>SCI 论文全中文完整学术对照版与核心命题/算法/11表精读手册（11月4日精修对齐版）</h1>
<p><strong>英文原题：</strong> Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations<br/>
<strong>中文标题：</strong> 多平台数字交付中人机协同项目工作流的治理研究：基于设计科学的可审计交接与幂等发布运维<br/>
<strong>作者信息：</strong> HuaiCheng Sheng（盛怀诚），Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd.（杭州诚欣控股集团有限公司），Hangzhou, Zhejiang, China | 邮箱：<code>ZackSheng@gmail.com</code> | ORCID：<code>0009-0003-2900-6377</code><br/>
<strong>版本对齐说明：</strong> 本手册与精修后的 22 页英文正刊主手稿（<code>manuscript.pdf</code> / <code>main.tex</code>，41 篇经 Crossref 核验的参考文献、11 张实证三线表、5 张矢量图）及 142 页在线补充材料（<code>supplementary_material.pdf</code>）100% 严格对齐，所有统计数字均由 <code>paper/reproduce_paper_tables.mjs</code> 直接从仓库原始数据复算生成。</p>

<h2>摘要（Abstract 完整中文对照，234 词精炼版，严格满足 &le; 250 词期刊上限）</h2>
<p><strong>背景（Context）：</strong> 大语言模型（LLM）智能体如今在缺乏服务端幂等键（Idempotency Keys）的外部平台上执行实质性任务；一旦对结果存疑的提交进行盲目重试，就会造成重复公开发布，且描述性统计指标极易与 AI 带来的因果收益发生混淆。</p>
<p><strong>目标（Objective）：</strong> 基于设计科学研究（DSR）与阶段门控（Stage-Gate）控制理论，本研究设计并评估了一套面向多智能体、多平台数字交付的人在回路（HITL）治理框架，并实现于两项开源制品 <em>Agentic Work Lab</em> 与 <em>PublishOps CrossCast</em>。</p>
<p><strong>方法（Method）：</strong> 该框架强制执行四层门控：（1）确定性的所有者、停止标志与到期时间准入门控（<code>planRun</code>，Algorithm 1）；（2）将每条发布命令绑定至经审阅计划与媒体的 SHA-256 摘要（<code>--confirm CURRENT_PLAN_HASH</code>）；（3）在单次最终提交（<code>PressFinalSubmitOnce</code>）<strong>之前</strong>先行将 <code>UNKNOWN</code> 状态持久化落盘、并严禁自动重提的回执状态机（Algorithm 2，Proposition 1）；以及（4）独立远端回读校验与人工升级机制（Algorithm 3）。研究在为期 9 天的现场部署（2026 年 10 月 26 日至 11 月 3 日）中对 <strong>N = 69</strong> 条运维日志、1 次定时心跳轨迹、1 部短片的 5 平台发布、<strong>2,737</strong> 个离线测试函数、<strong>30</strong> 个带 SHA-256 记录的视频版本（<strong>2.21 GB</strong>）以及 <strong>55</strong> 条频道快照记录进行了评估。</p>
<p><strong>结果（Results）：</strong> 69 条运维记录中仅记录 1 次平台写入（#52）且无重放上传；经审计的定时心跳延迟 <strong>24.992 秒</strong> 启动、运行 <strong>31.175 秒</strong> 且零错误。当抖音回读超时时，回执保持 <code>UNKNOWN</code> 状态且未触发重提，直至人工在手机端确认。在单次完整计时的 Instagram 发布中，单次点击 Share 后的等待阶段占发布命令总耗时 <strong>412.217 秒</strong> 中的 <strong>380.551 秒（92.32%）</strong>。</p>
<p><strong>结论（Conclusions）：</strong> 确定性门控、绑定内容的审批以及配合人工裁决的“最多一次”回执状态机，在提示词指令无法奏效的边界上实现了对智能体交付的可靠治理。本单案例研究的代码、数据与复算脚本均已公开。</p>

<h2>第 2.4 节 &amp; 第 7.1 节：核心理论贡献——将阶段门控重构为分布式提交协议</h2>
<p>论文将项目治理（Cooper, 1990; Too &amp; Weaver, 2014; Whyte et al., 2016）与分布式事务可靠性（Gray &amp; Reuter, 1992; Helland, 2012; Saltzer et al., 1984; Garcia-Molina &amp; Salem, 1987; Bainbridge, 1983）建立起 <strong>5 组严格映射</strong>：</p>
<ol>
  <li><strong>门控准入 = 确定性准入控制（Gatekeeping as Admission Control）：</strong> 用纯函数 <code>planRun</code> 检查 <code>owner</code>、<code>stop_flag</code>、<code>already_running</code> 与到期时间，但明确说明它是调度器层的咨询门控（Advisory Gate），底层强约束由第 2–3 层执行。</li>
  <li><strong>配置基线 = 内容寻址摘要（Configuration Baselines as Content Addresses）：</strong> 将排序键紧凑 JSON 序列化后的计划 SHA-256 摘要作为回执文件名与 <code>--confirm</code> 令牌，绑定预演与实发内容；同时坦诚说明 <code>--person-approved</code> 是调用方对人工审阅的声明。</li>
  <li><strong>放行决策 = 预写式最多一次提交（Go Decision as Write-Ahead At-Most-Once Commit）：</strong> 外部平台无幂等键时，客户端只能在“至少一次”与“最多一次”间抉择。在调用 <code>PressFinalSubmitOnce()</code> 前先把 <code>UNKNOWN</code> 与 <code>final_submit_attempted = true</code> 写入磁盘，即选择了安全性优先的“最多一次自动提交”（Proposition 1）。</li>
  <li><strong>门后评审 = 端到端回读核验（Post-Gate Review as End-to-End Check）：</strong> 依据端到端论证（Saltzer et al., 1984），只有从远端平台独立回读到唯一匹配的作品标识（<code>SUBMISSION_VERIFIED</code>），才能确认发布生效。</li>
  <li><strong>异常升级 = 存疑事务裁决与反破坏性补偿（In-Doubt Resolution &amp; Saga Safety）：</strong> <code>UNKNOWN</code> 对应两阶段提交中的存疑态（In-Doubt State），交由人工通过只读核验裁决；修订与删除（Algorithm 3）严禁采用“删帖重发”这种破坏作品身份的伪补偿操作。</li>
</ol>

<h2>第 4 节：形式化命题（Proposition 1）与三大算法真实状态码（Section 4）</h2>
<div class="fbox">
  <strong>Proposition 1（最多一次自动最终提交安全性保证，含例外条款 A4）：</strong><br/>
  假设 <strong>(A1)</strong> 计划 $P$ 在平台 $p$ 上的每次最终提交均经过 Algorithm 2 的回执事务；<strong>(A2)</strong> 回执写入是原子且持久的；<strong>(A3)</strong> 回执不被手工篡改。则自动运行对每个 $(P, p)$ 最多发出一次最终提交点击，仅有一个代码级例外：<strong>(A4)</strong> 当平台明确显示此前的作品仍处于“未提交草稿（unsubmitted draft）”状态时，恢复运行才允许再次点击提交。该命题提供安全性保证（Safety），不提供活性保证（Liveness）。
</div>
<ul>
  <li><strong>图 2 与 Algorithm 2 的真实代码状态名（<code>browser_backends.py</code>）：</strong> <code>PREPARING</code> &rarr; <code>UNKNOWN</code>（点击前落盘 <code>final_submit_attempted = true</code>）&rarr; <code>SUBMISSION_VERIFIED</code> / <code>SUBMITTED_UNVERIFIED</code> / <code>WAITING_FOR_USER</code>。</li>
  <li><strong>Algorithm 3 的真实修订/删除状态码（PR #20/#21/#25/#29/#30）：</strong> <code>EDIT_VERIFIED</code>（线上元数据已匹配，无需保存）、<code>EDIT_MISMATCH</code>（干跑发现差异）、<code>EDIT_UNAVAILABLE</code>（如 X 平台无原生编辑菜单，立即停止，绝不删帖重发或评论续貂）、<code>EDIT_SUBMITTED_UNVERIFIED</code>；删除前强制要求先做 dry-run 与人工批准，并在删除后刷新目录确认作品已消失。</li>
</ul>

<div class="page-break"></div>
<h2>第 5–6 节：全篇 11 张核心三线表精确复算指南（Tables 1–11）</h2>
<table>
<thead><tr><th>表号</th><th>表格内容</th><th>精确复算指标与方法学边界（100% 对应 <code>reproduce_paper_tables.mjs</code>）</th></tr></thead>
<tbody>
<tr><td><strong>Table 1</strong></td><td>多智能体治理对比</td><td>对比 AutoGen、MetaGPT、ChatDev、标准 CI/CD 与本文架构（明确浏览器自动化 + 内置 <code>bili-cli</code>，API 扩展包独立分发）</td></tr>
<tr><td><strong>Table 2</strong></td><td>四层架构与角色分工</td><td>明确 Human PM、GPT/Codex、Claude、DeepSeek 的工作流角色边界（注明 Git 提交分支为 <code>codex/*</code>，不按 PR 强行归因具体模型版本）</td></tr>
<tr><td><strong>Table 3</strong></td><td>69 条运维日志证据分类</td><td>46 条带时间戳（10月26日 01:27 至 27日 02:45，25.3 小时）：显式零写 <code>20 条 (13 读 0 写)</code>、零平台调用 <code>39 条</code>、单次调用无写字段 <code>1 条 (#22)</code>、授权写入 <code>1 条 (#52, 4 读 1 写, 0 重放)</code>、无调用/写字段 <code>8 条</code>（总计 17 读 1 写）</td></tr>
<tr><td><strong>Table 4</strong></td><td>代码与测试规模（<code>75b7abc</code>）</td><td><code>123</code> 个源文件（<code>26,972</code> 物理行）= 自研 <code>CrossCast</code>（<code>81</code> 文件 / <code>18,005</code> 行）+ 内置 <code>bili-cli v1.2.1</code>（<code>42</code> 文件 / <code>8,967</code> 行）；<code>126</code> 个测试文件（122 测试模块 + 4 辅助文件，<code>37,029</code> 物理行，<strong>2,737 个测试函数</strong>，自研测试代码比 <strong>2.06</strong>）</td></tr>
<tr><td><strong>Table 5</strong></td><td>仓库演进与验证里程碑</td><td>涵盖 Lab commit <code>596e951</code>、YouTube <code>8,388,608</code> 字节中断续传验证、<code>v0.16.0</code>（<code>a46bd19</code>：107 源文件 / 84 测试文件 / 2,009 测试函数）至 <code>75b7abc</code>（Python 3.10–3.14 CI 全绿）</td></tr>
<tr><td><strong>Table 6</strong></td><td>5 大平台发布状态实录</td><td>如实报告原始状态：Bilibili（<code>public_reported_by_production_session</code>）、YouTube（从原草稿续传并核验）、X（<code>SUBMISSION_VERIFIED</code>）、抖音（回读超时保持 <code>UNKNOWN</code>，另有 11月1日定时卡片同类超时验证）、Instagram（<code>SUBMISSION_VERIFIED</code> + 元数据回读，人工在手机端更换封面）</td></tr>
<tr><td><strong>Table 7</strong></td><td>Instagram 单次运行耗时拆解</td><td><code>N = 1</code> 次计时运行：端到端 <code>423.236 s</code>，发布命令 <code>412.217 s</code>；其中点击提交后阶段占 <code>380.551 s (92.32%)</code>（含传输、处理与确认，客户端不可拆分；命令内部其余步骤派生余项 <code>15.384 s</code>）</td></tr>
<tr><td><strong>Table 8</strong></td><td>多模态质量缺陷检出记录</td><td>精确对齐原始记录：GPT 值班日志 05 峰值 <code>-2.21 dBTP</code>、变身试炼 v4 峰值 <code>&approx; -1.636 dBFS</code>、第 07 集切点峰值 <code>&approx; -1.5 dBFS</code></td></tr>
<tr><td><strong>Table 9</strong></td><td>30 个视频版本清单核验</td><td><code>N = 30</code> 个 MP4，精确总字节 <code>2,211,716,129 bytes (2,109.26 MiB / 2.21 GB)</code>，SHA-256 与 <code>SHA256SUMS.txt</code> 完全一致；后 5 个版本（#26–#30）含零错误全量解码记录；旗舰短片完整哈希 <code>7d6fd76090f7a93b2711e15db4bb924f741d3a904a01fc4b7628fc2130dbafdc</code></td></tr>
<tr><td><strong>Table 10</strong></td><td>55 条频道快照分层描述统计</td><td><code>human_led_history</code>（<code>n = 45</code>，播放均值 <code>40,884.31</code>，SD <code>64,604.81</code>，中位数 <code>15,407.0</code>，Q1–Q3 <code>1,722–67,502</code>，范围 <code>274–333,725</code>）vs <code>current_ai_series</code>（<code>n = 10</code>：5 条已发仅 1.6h–3.1d，均值 <code>133.20</code>，SD <code>99.53</code>，中位数 <code>183.0</code>，Q1–Q3 <code>42–199</code>，范围 <code>11–231</code>；5 条待发）。呈现<strong>完全分离（274 &gt; 231）</strong>，故不报告冗余显著性检验</td></tr>
<tr><td><strong>Table 11</strong></td><td>四层治理机制分析性对比表</td><td>将未执行的无门控基线标为机制推导（would occur），并为本文每一层明确标注实证类型（Field 现场记录 / Test 自动化单测 / Review 审校记录 / Code 代码审查）</td></tr>
</tbody>
</table>

<h2>第 7–8 节：研究问题显式解答（Section 7.2）、合规声明与未来工作</h2>
<ul>
  <li><strong>RQ1–RQ3 逐条闭环解答（Section 7.2）：</strong> 第 7.2 节逐一回应了引言提出的三个研究问题，分别给出核心机制、实证支撑与证据边界。</li>
  <li><strong>已完成收敛的投稿配置项（对应 <code>CLAUDE_PEER_REVIEW_REPORT.md</code>）：</strong> ① 按作者确认，全套手稿与声明文件均<strong>不包含</strong>任何 AI 辅助写作声明；② 利益冲突声明（Declaration of Competing Interest）已如实写明作者隶属杭州诚欣控股集团有限公司 Agentic Work Lab、系两座开源仓库开发者及独立分发的 API 扩展包维护者；③ <code>agentic-work-lab</code> 已补充根目录 MIT <code>LICENSE</code> 文件；④ 英文摘要已压缩至 <strong>234 词</strong>（满足所有目标期刊 &le; 250 词的严格上限），且全篇单作者人称已统一规范为“this study / this paper”。</li>
</ul>
</body></html>`;

const cnHtmlPath = path.join(paperDir, 'manuscript_CN_companion_print.html');
fs.writeFileSync(cnHtmlPath, cnCompanionHtml, 'utf8');
htmlToPdf(cnHtmlPath, path.join(paperDir, 'manuscript_CN_companion.pdf'));

console.log('Successfully generated updated manuscript_full_EN.md, reviewer_defense_playbook.pdf, and manuscript_CN_companion.pdf in', paperDir);
