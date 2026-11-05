import fs from 'fs';
import path from 'path';
import { execFileSync } from 'child_process';

const paperDir = 'C:\\Users\\ROG\\.gemini\\antigravity\\scratch\\agentic-sci-paper\\paper';

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
  table { width: 100%; border-collapse: collapse; margin: 7pt 0 11pt 0; font-size: 8.8pt; }
  th { border-top: 1.5pt solid #0f172a; border-bottom: 1pt solid #0f172a; padding: 4pt 5pt; background: #f8fafc; text-align: left; font-weight: bold; }
  td { border-bottom: 0.5pt solid #cbd5e1; padding: 4pt 5pt; vertical-align: top; }
  tr:last-child td { border-bottom: 1.5pt solid #0f172a; }
  .qbox { border-left: 3pt solid #dc2626; background: #fef2f2; padding: 6pt 9pt; margin: 8pt 0 4pt 0; }
  .abox { border-left: 3pt solid #16a34a; background: #f0fdf4; padding: 6pt 9pt; margin: 4pt 0 10pt 0; }
  .fbox { border: 1pt solid #94a3b8; background: #f8fafc; padding: 7pt 10pt; margin: 7pt 0; font-family: "Times New Roman", serif; }
  code { font-family: "Consolas", monospace; font-size: 8.8pt; background: #f1f5f9; padding: 1pt 3pt; border-radius: 2pt; }
  .page-break { page-break-before: always; }
`;

// ============================================================================
// PART A: 15-QUESTION SCI PEER-REVIEW REBUTTAL & DEFENSE PLAYBOOK
// ============================================================================
const defenseQuestions = [
  {
    id: "Q1",
    category: "Reviewer #1 (Project Management & DSR Theory)",
    q_en: "How does your four-layer architecture advance Stage-Gate project management theory rather than merely implementing standard DevOps CI/CD scripts?",
    q_cn: "你们的四层架构是如何在理论上推进项目管理“阶段门控（Stage-Gate）”理论的，而不是仅仅写了几个普通的 DevOps 自动化脚本？",
    a_en: "Classical Stage-Gate theory (Cooper, 1990; Bianchi et al., 2022) assumes human-to-human review meetings where actors share semantic context, whereas classic CI/CD pipelines (Humble & Farley, 2010) assume deterministic compilers and idempotent deployment targets. Multi-agent AI workflows violate both assumptions: foundation models suffer from stochastic context drift across asynchronous sessions, and external digital platforms exhibit non-idempotent, opaque state transitions (e.g., 92.32% post-submission confirmation waits). Our framework advances Stage-Gate theory by formalizing 'Computational Stage-Gates' through three enforceable invariants: (1) pure temporal-checklist scheduling predicates (Algorithm 1, Theorem A.1), (2) cryptographic plan-hash binding between dry-run rehearsal and execution (--confirm CURRENT_PLAN_HASH, Theorem A.3), and (3) epistemic audit gates (Algorithm 3) that prevent agents from conflating raw output volume with verified project outcomes.",
    a_cn: "核心防御逻辑：传统 Stage-Gate 假设“人与人开会评审”，传统 CI/CD 假设“编译器和部署目标是确定且幂等的”。但多智能体项目同时打破了这两个假设（AI 会上下文漂移，外部社交平台 92.32% 时间卡在不确定等待态）。我们将阶段门控理论升华为“可计算门控不变式（Computational Stage-Gates）”，并在 Appendix A 给出了形式化定理证明（Theorems A.1–A.3）。"
  },
  {
    id: "Q2",
    category: "Reviewer #1 (Project Management & DSR Theory)",
    q_en: "Why is a 'Sole-Operator' human-in-the-loop (HITL) topology necessary when frameworks like ChatDev and MetaGPT advocate fully autonomous multi-agent teams?",
    q_cn: "既然 ChatDev 和 MetaGPT 等框架都在鼓吹“全自动无人值守多智能体团队”，为什么你们还要坚持“单人最高授权（Sole-Operator HITL）”？",
    a_en: "As demonstrated in our comparative governance analysis (Table 1) and ablation study (Table 11, Ablation A4/A5), fully autonomous conversational handoffs lack legal/operational accountability and fail catastrophically when encountering non-API authentication barriers (CAPTCHAs, QR re-logins) or ambiguous timeout states. Drawing on high-reliability control theory (Parasuraman et al., 2000; Shneiderman, 2020; Raisch & Krakowski, 2021), high automation must be paired with high human control over irreversible external commitments. By partitioning roles so that GPT/Codex, Claude, and DeepSeek handle high-frequency synthesis, UI engineering, and heartbeat monitoring while the human Project Manager retains sole release-token authority, our system achieved 0 unauthorized writes across N=69 checkpoints while bounding human intervention solely to explicit NEEDS_HUMAN boundary states.",
    a_cn: "核心防御逻辑：全自动多智能体在遇到滑块验证码、Cookie 过期或超时重发时会直接崩溃或闯祸（见表 1 对比与表 11 消融实验 A4/A5）。依据 Shneiderman (2020) 的“高自动化+高人类控制”理论，把高频生成/巡检交给 AI，把不可逆的外部发布锁（Sole Operator）留给人类项目经理，才能实现 69 次运行 0 越权事故。"
  },
  {
    id: "Q3",
    category: "Reviewer #1 (Project Management & DSR Theory)",
    q_en: "How does this study fulfill the six mandatory steps of Peffers et al.'s (2007) Design Science Research Methodology (DSRM) and Gregor & Hevner's (2013) knowledge contribution framework?",
    q_cn: "本研究是如何严格满足 Peffers (2007) 的设计科学研究六步法以及 Gregor & Hevner (2013) 的知识贡献框架的？",
    a_en: "Section 3.1 and Figure 1 explicitly map all six DSRM activities: (1) Problem Identification (three failure modes in Section 1), (2) Objectives of a Solution (four-layer invariants), (3) Design & Development (Algorithms 1–3, 123 Python modules, 27,095 LOC), (4) Longitudinal Demonstration (two multi-episode production series and standalone releases across 5 platforms), (5) Multi-Method Evaluation (N=69 operational events, N=55 channel records, N=30 SHA-256 media versions, and 2,737 unit tests), and (6) Communication (complete open-source archival and 150-page Supplementary Material). Under Gregor & Hevner's (2013) matrix, our work provides an 'Exaptation / Improvement' contribution that formalizes nascent design theory for agentic project governance.",
    a_cn: "核心防御逻辑：第 3.1 节完整对应了 DSRM 六步法，兼具软件制品（27,095 行核心代码）与纵向实证评估（69 条运维日志、55 条发布快照、2,737 个测试），符合 Gregor & Hevner (2013) 的设计科学知识贡献标准。"
  },
  {
    id: "Q4",
    category: "Reviewer #2 (Distributed Systems & State-Machine Formalism)",
    q_en: "In Theorem A.2 and Algorithm 2, why do you latch post-submission verification timeouts into an 'UNKNOWN' state rather than implementing automatic exponential-backoff retries?",
    q_cn: "在定理 A.2 和算法 2 中，为什么提交后回读超时要锁定为 UNKNOWN 状态，而不是像常规网络程序那样做“指数退避自动重试”？",
    a_en: "Automatic exponential-backoff retry is only safe when the target operation is natively idempotent (e.g., HTTP PUT with a server-enforced idempotency key). Across heterogeneous social media platforms (especially browser-automated endpoints like Douyin and Instagram or chunked uploads without universal idempotency tokens), a timeout during the post-submission confirmation window (which consumes 92.32% / 380.551 s of Instagram release duration, Table 5) means the payload has already been transmitted and the server is likely processing it asynchronously. As proven in Theorem A.2 (Supplementary Appendix A) and empirically confirmed on Douyin (Table 4), automatic retry after SUBMITTING violates At-Most-Once publication safety and produces duplicate public posts. Latching into UNKNOWN and restricting recovery strictly to read-only reconciliation (verify_remote_state) guarantees zero duplicate side effects across K >= 1 recovery rounds.",
    a_cn: "核心防御逻辑：指数退避重试只对幂等接口安全！而在抖音、Instagram 等跨平台发布中，92.32% 的耗时发生在“点击提交后的等待确认期”（见表 5）。此时视频数据早已上传完毕，如果因为超时就自动重传，100% 会造成重复发帖（抖音实测案例见表 4）。锁定为 UNKNOWN 并只允许只读回查，在数学上（定理 A.2）保证了“最多发布一次（At-Most-Once）”。"
  },
  {
    id: "Q5",
    category: "Reviewer #2 (Distributed Systems & State-Machine Formalism)",
    q_en: "What threat model does the cryptographic plan-hash lock (--confirm CURRENT_PLAN_HASH) defend against?",
    q_cn: "你们设计的加密计划哈希锁（--confirm CURRENT_PLAN_HASH）具体防御的是什么威胁模型（Threat Model）？",
    a_en: "The plan-hash lock defends against Time-of-Check to Time-of-Use (TOCTOU) drift in multi-agent pipelines. Specifically, between the time a human Project Manager reviews a dry-run rehearsal plan and the moment the execution agent invokes the live release command, another asynchronous agent or background script might modify the title, tags, target platform list, or re-render the video file. Because H_plan = SHA256(Canon(M, T, H(f))) binds the canonical metadata M, target platform vector T, and full stream-based SHA-256 media digest H(f) (Theorem A.3), any single-bit mutation invalidates the confirmation token and aborts execution before network transmission.",
    a_cn: "核心防御逻辑：防御的是多智能体协作中的 TOCTOU（检查时与使用时不一致）漂移風險——防止人类刚审完预演计划，另一个后台 AI 就悄悄改了标题、标签或替换了视频文件。通过绑定元数据与 64KB 分块 SHA-256 视频哈希（定理 A.3），改一个字节就会立刻拦截。"
  },
  {
    id: "Q6",
    category: "Reviewer #2 (Distributed Systems & State-Machine Formalism)",
    q_en: "Browser DOM automation (Playwright) is notoriously fragile when platforms update their UI. How does your architecture handle DOM selector breakage?",
    q_cn: "网页端自动化（Playwright）在平台改版时很容易因为 DOM 选择器失效而崩溃，你们的架构是如何应对这种脆弱性的？",
    a_en: "Rather than assuming DOM selectors will never break, our architecture treats UI fragility as a first-class design constraint across three mechanisms: (1) Preflight & Dry-Run isolation checks selector availability and authentication validity before entering SUBMITTING; (2) explicit phase separation distinguishes pre-click failures (safe to abort into FAILED without side effects) from post-click verification failures (latched into UNKNOWN); and (3) a dedicated test suite of 2,737 unit and contract tests across 126 modules (Table 8, Supplementary Appendix F) continuously validates adapter contracts, fallback selectors, and receipt serialization.",
    a_cn: "核心防御逻辑：我们不假设网页永不改版，而是把“UI 脆弱性”作为一等公民来设计：提交前坏了直接安全退回 FAILED；点击提交后如果选择器变了导致读不到回执，状态机立刻锁入 UNKNOWN 防止重发；同时配合 126 个测试模块、2,737 个单元测试（表 8 与附录 F）持续验证适配器契约。"
  },
  {
    id: "Q7",
    category: "Reviewer #3 (Empirical Evaluation & Statistical Validity)",
    q_en: "Your empirical evaluation comes from a single organizational laboratory (Agentic Work Lab). How do you justify external validity and generalizability?",
    q_cn: "你们的实证数据来自单一机构实验室（Agentic Work Lab），如何证明研究结论的外部有效性（External Validity）与普适性？",
    a_en: "In Design Science Research and empirical software engineering (Hevner et al., 2004; Runeson & Höst, 2009; Wieringa, 2014), generalization from longitudinal single-site studies relies on 'analytic generalization' (architectural mechanisms and mathematical invariants) rather than purely statistical population sampling. While our operational dataset originates from one laboratory, the technical environment spans 3 distinct foundation model families (GPT/Codex, Claude, DeepSeek), 5 structurally diverse external platforms (Bilibili, YouTube, X, Douyin, Instagram) covering both REST APIs and browser-automated engines, 69 operational checkpoints, 55 published videos, 30 SHA-256 locked media versions (2.21 GB), and 2,737 automated tests. Furthermore, all schemas and source code are publicly archived under the MIT License so independent teams can replicate the governance gates in their own pipelines.",
    a_cn: "核心防御逻辑：依据软件工程实证案例研究规范（Runeson & Höst, 2009；Wieringa, 2014），设计科学采用的是“分析性推广（Analytic Generalization）”。虽然来自同一实验室，但横跨了 3 大主流模型家族（GPT、Claude、DeepSeek）、5 个异构国内外平台（API + 浏览器混合）、69 次运维事件、55 条视频与 2,737 个测试，且全部开源可复现。"
  },
  {
    id: "Q8",
    category: "Reviewer #3 (Empirical Evaluation & Statistical Validity)",
    q_en: "In Table 7, why do the AI-assisted cohorts ('Whale Girl Shift Log' mean = 95.89 views; 'GPT Duty Log' mean = 32.56 views) have lower views than the legacy baseline (mean = 3,477.00 views)? Doesn't this show AI performed worse?",
    q_cn: "在表 7 中，为什么 AI 辅助序列（鲸鱼娘均值 95.89，GPT 值班日志均值 32.56）的播放量反而低于历史老视频基线（均值 3,477）？这难道不是说明 AI 表现更差吗？",
    a_en: "Quite the opposite—highlighting this discrepancy is the core point of RQ3 and Algorithm 3 (Epistemic Audit Gate). As explained in Section 5.5, comparing raw cumulative views between legacy videos (which have accumulated organic traffic and algorithmic recommendations over many months/years) and newly launched AI-assisted series (published only days or weeks prior to snapshot collection) commits severe exposure-duration confounding. A naive or dishonest study would cherry-pick metrics or claim 'AI increased output volume by 300%'. Instead, our Mann–Whitney U test (U = 65.5, z = -3.24, p = 0.0012, r_rb = 0.721) formally quantifies this distributional shift and enforces our epistemic rule: operational governance success (0 unauthorized writes, 0 duplicate posts, 100% SHA-256 integrity) must be evaluated independently from time-confounded audience traffic.",
    a_cn: "核心防御逻辑：这恰恰是本文 RQ3 最亮眼的学术贡献（诚实审计门，算法 3）！老视频挂在网上累积了很久的自然流量，而新 AI 序列才刚发布不久。如果直接比播放量就犯了统计学上的“曝光周期混杂偏差（Exposure-Duration Confounding）”。我们专门用 Mann–Whitney U 检验（p=0.0012）揭示这一现象，证明工程治理的成功（0 越权、0 重复发布）绝不能与受时间干扰的播放量混为一谈——审稿人最喜欢这种极其诚实、反浮躁的严谨学风！"
  },
  {
    id: "Q9",
    category: "Reviewer #3 (Empirical Evaluation & Statistical Validity)",
    q_en: "In Table 3, 68 out of 69 operational events were blocked non-write checkpoints and only 1 was an executed write. Is N=1 write event too small to evaluate write execution?",
    q_cn: "在表 3 中，69 次事件里有 68 次是非写入拦截，只有 1 次是写入执行。1 次写入样本是不是太少了？",
    a_en: "Table 3 specifically measures the autonomous scheduled heartbeat daemon (DeepSeek / OpenClaw routine runner) governed by Algorithm 1 (planRun), whose primary safety requirement is to prevent unscheduled or duplicate background mutations across continuous polling intervals (68/68 = 100% suppression accuracy, 0 false-positive writes). The actual multi-platform media write and release executions are evaluated separately across the 5-platform release matrix (Table 4), the phase-level telemetry decomposition (Table 5), the 30 cryptographically verified video delivery versions totaling 2.21 GB (Table 6), the 55 published channel videos (Table 7), and 2,737 automated test cases simulating every state transition (Table 8).",
    a_cn: "核心防御逻辑：表 3 测的是后台定时巡检守护进程（Algorithm 1），它的核心使命就是在 68 个非写入时间窗口里 100% 拦住 AI 乱动文件；而真正的跨平台写入与发布实测在表 4（5 大平台发布）、表 5（Instagram 耗时拆解）、表 6（30 个视频版本 2.21GB 交付）、表 7（55 条已发布视频）和表 8（2,737 个自动化测试）中得到了全面验证。"
  },
  {
    id: "Q10",
    category: "Reviewer #3 (Empirical Evaluation & Statistical Validity)",
    q_en: "Why did you note that the 'blinded identical-task multi-model merchant comparison' is an unexecuted plan rather than running it before submission?",
    q_cn: "为什么你们在论文里诚实说明“多模型同题盲测对比”目前只是待执行计划（Unexecuted Plan），而不是编造或凑一组数据放进去？",
    a_en: "Rule 4 of our Epistemic Hygiene Protocol (Section 3.2) strictly mandates separating verified execution receipts from planned experimental designs. In our production workflow, GPT/Codex, Claude, and DeepSeek were assigned complementary functional roles (synthesis/QA, UI tooling, and scheduled monitoring, Table 10) rather than redundant identical tasks. Documenting the schema and priority protocol for future blinded comparisons while explicitly labeling its unexecuted status prevents selective reporting bias and upholds the highest standards of empirical software engineering transparency.",
    a_cn: "核心防御逻辑：在实际生产中，三个模型是按特长分工协作的（GPT 写脚本与验证、Claude 做界面、DeepSeek 做定时巡检，见表 10），而不是让三个模型重复干同一件事。在论文中明确把“同题盲测”标注为待执行计划，体现了第 3.2 节提出的“诚实学术边界准则（Epistemic Hygiene）”。"
  },
  {
    id: "Q11",
    category: "Reviewer #2 (Systems Performance & Overhead)",
    q_en: "Does computing 64-KB chunked SHA-256 hashes across gigabytes of video files introduce unacceptable latency overhead into the release pipeline?",
    q_cn: "对几个 GB 的视频文件做 64-KB 分块 SHA-256 加密哈希计算，会不会给发布流水线带来很大的性能延迟开销？",
    a_en: "No. Stream-based 64-KB SHA-256 hashing in Python/OpenSSL processes over 500–800 MB/s on modern NVMe SSDs, requiring less than 0.15 seconds per 75 MB video file while keeping peak RAM usage under 64 KB. Compared to external platform upload and confirmation latencies (e.g., 412.217 s on Instagram, Table 5), cryptographic hashing overhead represents less than 0.04% of end-to-end release time while guaranteeing 100% byte-level payload immutability.",
    a_cn: "核心防御逻辑：64-KB 流式 SHA-256 哈希在现代固态硬盘上每秒可处理 500–800 MB，算一个 75 MB 的视频不到 0.15 秒，内存占用仅 64 KB；相比于跨平台上传动辄 412 秒的耗时（表 5），哈希校验的开销占比不到 0.04%，却换来了 100% 的字节级防篡改安全。"
  },
  {
    id: "Q12",
    category: "Reviewer #1 (Human Factors & Cognitive Load)",
    q_en: "Does routing UNKNOWN and NEEDS_HUMAN states to the human Project Manager create alert fatigue or bottleneck scalability?",
    q_cn: "把 UNKNOWN 和 NEEDS_HUMAN 状态交给人类项目经理处理，会不会导致人类“告警疲劳（Alert Fatigue）”或成为扩展性瓶颈？",
    a_en: "Our architecture minimizes human cognitive load by filtering out all routine checks and pre-submission errors automatically: Layer 1 suppressed 68/69 non-write polling intervals without human involvement, and Layer 3 only escalates to NEEDS_HUMAN or UNKNOWN when an irreversible external boundary (such as an expired login session or post-submit read-back timeout) occurs. Moreover, when escalation occurs, the receipt provides exact diagnostic telemetry (platform, phase latency, error code, and pre-filled CLI recovery commands), reducing human resolution to a single verification action rather than manual log debugging.",
    a_cn: "核心防御逻辑：恰恰相反，第一层门控自动过滤了 68/69 的日常巡检，只有遇到真正不可逆的外部边界（如登录过期或提交后超时）才会触发人工接管，而且回执单里直接给出了精确的故障阶段与恢复命令，极大降低了人类的排查负担。"
  },
  {
    id: "Q13",
    category: "Reviewer #2 (State Machine Completeness)",
    q_en: "In Figure 3 and Algorithm 2, are the eight states of the release receipt state machine dead-lock free and formally terminating?",
    q_cn: "在图 3 和算法 2 中，发布回执状态机的 8 个状态是否保证无死锁（Deadlock-Free）且必然终止？",
    a_en: "Yes. As formalized in Section 4.3 and proven in Supplementary Appendix A (Theorem A.2), the state transition graph is a Directed Acyclic Graph (DAG) within any single execution attempt: every active state (INIT, DRY_RUN, PREFLIGHT, SUBMITTING, VERIFYING) has bounded timeout guards (tau_submit, tau_verify) that deterministically transition into one of the four absorbing terminal states (PUBLISHED, UNKNOWN, NEEDS_HUMAN, FAILED). Consequently, no execution path can loop infinitely or deadlock.",
    a_cn: "核心防御逻辑：在单次执行中，状态转移图是一个带超时上界（tau_submit, tau_verify）的有向无环图（DAG），任何活跃状态在有限时间内必然进入四个吸收态（PUBLISHED、UNKNOWN、NEEDS_HUMAN、FAILED）之一，因此在数学上绝不存在死锁或无限循环。"
  },
  {
    id: "Q14",
    category: "Reviewer #3 (Ablation Study Design)",
    q_en: "How were the ablation configurations (A1–A5 in Table 11) evaluated without intentionally corrupting live public social media accounts?",
    q_cn: "表 11 中的消融实验（A1–A5）是如何在不污染真实公开社交媒体账号的前提下完成验证的？",
    a_en: "As detailed in Section 5.9 and Table 11, each ablation variant (A1–A5) was verified by combining historical incident traces observed prior to gate deployment (e.g., the Douyin read-back timeout trace and Instagram session-expiry trace) with deterministic fault-injection test harnesses in the 2,737-test suite (e.g., tests/test_state_machine_*.py, tests/test_preflight_*.py, and tests/plan-run.test.mjs). This allows exact measurement of failure behavior under gate removal without spamming live public platforms.",
    a_cn: "核心防御逻辑：结合了门控上线前的真实故障历史记录（如抖音回读超时、Instagram 登录过期记录）以及 2,737 个自动化测试中的确定性故障注入测试（Fault-Injection Test Harness），既精确验证了去掉每一层门控后的必现故障，又避免了往真实公网平台恶意连发垃圾视频。"
  },
  {
    id: "Q15",
    category: "Editor-in-Chief (Journal Fit & Readership Value)",
    q_en: "What concrete takeaway can software engineering and project management practitioners immediately apply from your paper?",
    q_cn: "软件工程与项目管理的一线从业者能从你的论文中立刻拿走什么可落地的实践经验？",
    a_en: "Practitioners gain three immediately actionable engineering patterns: (1) Never rely on conversational prompts to constrain multi-agent write authority—enforce a pure scheduling gate and cryptographic plan-hash token (--confirm CURRENT_PLAN_HASH); (2) Never allow an AI agent to auto-retry a failed post-submission check—latch post-submit timeouts into an UNKNOWN state and reconcile via read-only queries; and (3) Always apply non-parametric cohort stratification before claiming AI productivity or traffic gains. All patterns are available as ready-to-run open-source tools in Agentic Work Lab and PublishOps CrossCast.",
    a_cn: "核心防御逻辑：给业界提供了三条即插即用的工程铁律：① 绝不用提示词约束 AI 写权限，必须用纯函数门控+加密计划哈希锁；② 绝不允许 AI 在提交后超时自动重试，必须锁入 UNKNOWN 态做只读回查；③ 评估 AI 效果必须做非参数批次分层检验。且全套工具已开源。"
  }
];

let defenseMd = `# SCI Peer-Review Defense & Rebuttal Playbook (15 Core Questions & Standard Answers)
**Manuscript:** *Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations*  
**Author:** HuaiCheng Sheng (Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd.)

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
<h1>SCI Peer-Review Defense &amp; Rebuttal Playbook (15 Questions)</h1>
<p><strong>Manuscript:</strong> Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations<br/>
<strong>Author:</strong> HuaiCheng Sheng (Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd. | ORCID: <code>0009-0003-2900-6377</code>)</p>
<p>This playbook anticipates the 15 toughest questions from SCI Q1 reviewers across Project Management Theory (Reviewer #1), Distributed Systems &amp; State Machines (Reviewer #2), and Empirical Statistics (Reviewer #3), providing ready-to-copy English responses and Chinese strategic summaries.</p>
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
// PART B: FULL CHINESE ACADEMIC COMPANION EDITION (全中文完整学术对照精读本)
// ============================================================================
const cnCompanionHtml = `<!DOCTYPE html><html><head><meta charset="utf-8"><style>${css}</style></head><body>
<h1>SCI 论文全中文完整学术对照版与核心公式/算法/图表精读手册</h1>
<p><strong>英文原题：</strong> Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations<br/>
<strong>中文标题：</strong> 多平台数字交付中人机协同项目工作流的治理研究：基于设计科学的可审计交接与幂等发布运维<br/>
<strong>作者信息：</strong> HuaiCheng Sheng（盛怀诚），Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd.（杭州诚欣控股集团有限公司），Hangzhou, Zhejiang, China | 邮箱：<code>ZackSheng@gmail.com</code> | ORCID：<code>0009-0003-2900-6377</code><br/>
<strong>配套文件说明：</strong> 本手册与 21 页英文正刊主手稿（<code>manuscript.pdf</code>）及 150 页在线补充材料（<code>supplementary_material.pdf</code>）严格一一对应，供作者审阅、汇报、答辩及与合作模型讨论使用。</p>

<h2>摘要（Abstract 完整中文对照）</h2>
<p><strong>背景（Context）：</strong> 随着大语言模型（LLM）智能体在数字工程与媒体生产项目中从“对话式助手”演进为“自主执行主体”，项目管理面临着严峻的治理空白。多智能体工作流频繁遭遇角色交接失调、非幂等的外部副作用（例如网络超时后触发的跨平台重复发布）、静默质量退化以及混淆的绩效归因。</p>
<p><strong>目标（Objective）：</strong> 基于设计科学研究（Design Science Research, DSR）范式与阶段门控（Stage-Gate）项目控制理论，本研究设计、实现并实证评估了一套面向多智能体、多平台数字交付的可审计“人在回路（HITL）”项目治理框架与发布运维系统（<em>Agentic Work Lab</em> 与 <em>PublishOps CrossCast</em>）。</p>
<p><strong>方法（Method）：</strong> 我们形式化提出了四层治理架构：（1）确定性的单人主控与时间窗口调度门（<code>planRun</code>）；（2）耦合流式 SHA-256 媒体完整性校验的加密计划哈希锁（<code>--confirm CURRENT_PLAN_HASH</code>）；（3）在异构 API 与浏览器自动化端点上将不确定提交结果锁定为 <code>UNKNOWN</code> 态以抑制自动重试故障的幂等回执状态机；以及（4）独立回读校验与人工异常接管协议。我们利用纵向真实项目制品对该框架进行了实证评估，涵盖 <strong>N = 69</strong> 条脱敏运维事件日志、<strong>N = 55</strong> 条频道发布快照记录、<strong>N = 30</strong> 个经加密校验的视频交付版本（总计 <strong>2.21 GB</strong>），以及跨 <strong>126</strong> 个测试模块的 <strong>2,737</strong> 个自动化验证测试用例。</p>
<p><strong>结果（Results）：</strong> 在涉及多智能体协作（人类项目管理下的 GPT/Codex、Claude 与 DeepSeek）的 69 个运维检查点中，治理门在 68 个非写入窗口中成功抑制了未授权变更，仅在授权窗口执行 1 次真实写入（0 次重复上传重放，0 起新增风险事件）。经核实的自主巡检运行表现出 <strong>24.992 秒</strong> 的触发延迟与 <strong>31.175 秒</strong> 的执行耗时，任务错误率为零。跨 5 大平台（Bilibili、YouTube、X、抖音、Instagram）的发布评估证实，在抖音出现程序化回读超时期间，将状态锁定为 <code>UNKNOWN</code> 成功阻止了重复发布并完整保留了审计轨迹。Instagram 端到端发布（总耗时 <strong>423.236 秒</strong>，发布命令耗时 <strong>412.217 秒</strong>）的阶段级延迟拆解表明，<strong>92.32%（380.551 秒）</strong> 的命令耗时集中在“提交后的平台确认等待期”，从而精准定位了非幂等智能体重试的核心脆弱窗口。</p>
<p><strong>结论（Conclusions）：</strong> 代理式（Agentic）工作流的有效项目管理，必须从“提示词层面的软性规劝”转向“确定性的阶段门控不变式、加密制品锁定与幂等状态机恢复”。全部源代码、数据模式与脱敏实证数据集均已公开归档，为多智能体项目治理建立了可复现的基准。</p>

<h2>第 1 节：引言与三大研究问题（Section 1: Introduction &amp; RQs）</h2>
<p>论文第一部分指出，将多个异构大模型（GPT/Codex、Claude、DeepSeek）投入真实生产环境时，传统项目管理无法应对三大故障模式：</p>
<ol>
  <li><strong>多智能体交接漂移与角色模糊（Multi-Agent Handoff Drift）：</strong> 异步智能体缺少形式化所有权锁，容易覆盖人工审核结果或在非授权时段写文件。</li>
  <li><strong>不确定状态下的非幂等外部副作用（Non-Idempotent External Side Effects）：</strong> 社交平台发布兼具官方 API 与浏览器自动化（Playwright）。网络抖动、审核队列或回读超时会让提交处于不确定态（<code>UNKNOWN</code>），传统智能体盲目自动重试会导致严重重复发帖。</li>
  <li><strong>绩效评估中的认知混杂（Epistemic Confounding）：</strong> 把累积了长周期流量的老视频与刚上线的新 AI 视频混在一起比均值，会产生严重的曝光时间混杂偏差。</li>
</ol>
<p>为此，论文提出 <strong>3 个核心研究问题（RQs）</strong>：</p>
<ul>
  <li><strong>RQ1（治理架构）：</strong> 如何将阶段门控项目管理原则形式化为确定性的软件不变式，以治理多模型交接并杜绝未授权或重复的自主操作？</li>
  <li><strong>RQ2（容错发布工程）：</strong> 当提交后验证遇到超时或人工认证障碍时，幂等回执状态机如何协调混合 API 与浏览器通道的跨平台数字发布？</li>
  <li><strong>RQ3（实证与认知验证）：</strong> 将该治理架构部署于纵向多平台生产工作流时，表现出怎样的实证运行特征、延迟瓶颈与方法学边界？</li>
</ul>

<h2>第 2–3 节：理论背景与设计科学研究方法（Sections 2 &amp; 3）</h2>
<p>第 2 节综述了阶段门控理论（Cooper, 1990；Bianchi et al., 2022）、人机协同控制（Raisch &amp; Krakowski, 2021；Shneiderman, 2020）、多智能体框架对比（AutoGen、MetaGPT、ChatDev，对应 <strong>Table 1</strong>）以及幂等状态机理论（Schneider, 1990；Kleppmann, 2017）。</p>
<p>第 3 节定义了设计科学研究（DSR）六步法与 <strong>4 条强制学术诚实准则（Epistemic Hygiene Rules）</strong>：① 观测与推断严格分离；② 状态颗粒度显式区分（绝不把超时未确认当成失败或成功，必须锁入 <code>UNKNOWN</code>）；③ 历史快照不可变追加；④ 未执行实验显式标注。</p>

<h2>第 4 节：四层人机协同治理架构、三大数学公式与三大核心算法（Section 4）</h2>
<div class="fbox">
  <strong>核心数学公式 1（Layer 1 纯函数调度门控谓词，对应定理 A.1）：</strong><br/>
  <code>G_run(C, E, S_chk, t_now) = I_enabled(C) &and; I_window(t_now, W) &and; I_cooldown(t_now, E, &Delta;t_min) &and; I_checklist(S_chk)</code><br/>
  <em>含义：</em> 只有当配置开关开启、当前时间处于授权窗口 <code>W</code> 内、距离上次运行已满最小冷却时间 <code>&Delta;t_min</code>、且任务清单存在未认领的就绪项时，门控谓词才返回 <code>1</code>（放行），否则一律返回 <code>0</code>（硬拦截）。
</div>

<div class="fbox">
  <strong>核心数学公式 2（Layer 2 流式 SHA-256 载荷与计划哈希锁，对应定理 A.3）：</strong><br/>
  <code>H_plan = SHA256(Canon(M, T, H(f)))</code>，执行条件：<code>I_exec(&tau;_user, H_plan) = 1 &hArr; (&tau;_user == H_plan)</code><br/>
  <em>含义：</em> 将规范化后的元数据 <code>M</code>、目标平台列表 <code>T</code> 以及 64-KB 分块流式计算的媒体文件 SHA-256 哈希 <code>H(f)</code> 组合生成唯一计划指纹 <code>H_plan</code>。执行发布时，用户或上层门控传入的确认令牌 <code>&tau;_user</code> 必须与当前重算的 <code>H_plan</code> 完全一致，哪怕改动 1 个字节也会立即中止执行。
</div>

<div class="fbox">
  <strong>核心数学公式 3（Layer 3 幂等状态机“最多发布一次”安全不变式，对应定理 A.2）：</strong><br/>
  <code>&forall; p &in; T, &Sigma;_{k=1..K} I(transmit(f, M, p, k)) &le; 1</code><br/>
  <em>含义：</em> 对于任何目标平台 <code>p</code>，在任意 <code>K &ge; 1</code> 轮恢复与核对过程中，一旦首次尝试进入 <code>SUBMITTING</code> 状态，后续任何因回读超时进入 <code>UNKNOWN</code> 态的恢复操作都只允许调用只读查询接口（<code>verify_remote_state</code>），绝不再次调用字节流上传接口（<code>transmit</code>），从而在数学上严格保证外部平台最多只会被写入 1 次。
</div>

<h3>三大形式化算法（Algorithms 1–3）一览：</h3>
<ul>
  <li><strong>Algorithm 1 (<code>planRun</code>)：</strong> 确定性时间窗口与清单门控算法（纯函数，无外部 I/O 副作用，复杂度 O(|E| + |S_chk|)）。</li>
  <li><strong>Algorithm 2 (<code>executePublishStep</code>)：</strong> 跨平台幂等回执状态机与 <code>UNKNOWN</code> 锁定算法（涵盖 8 个状态：<code>INIT, DRY_RUN, PREFLIGHT, SUBMITTING, VERIFYING, PUBLISHED, UNKNOWN, NEEDS_HUMAN, FAILED</code>）。</li>
  <li><strong>Algorithm 3 (<code>auditCohortComparison</code>)：</strong> 频道作品序列非参数分层与认知混杂审计门算法（自动计算 Mann–Whitney U 统计量、p 值与秩双列相关系数 r_rb，拦截混杂归因）。</li>
</ul>

<div class="page-break"></div>
<h2>第 5 节：11 张核心实证与消融三线表全解析（Section 5: Tables 1–11）</h2>
<table>
<thead><tr><th>表号</th><th>表格名称与核心实证内容</th><th>关键硬核数据指标（100% 源自仓库真实记录）</th></tr></thead>
<tbody>
<tr><td><strong>Table 1</strong></td><td>多智能体治理框架对比表</td><td>对比 AutoGen、MetaGPT、ChatDev、标准 CI/CD 与本文框架在 6 大治理维度的差异</td></tr>
<tr><td><strong>Table 2</strong></td><td>四层治理门控架构与可验证制品映射表</td><td>汇总 Layers 1–4 的控制目标、软件机制、核心文件与审计回执路径</td></tr>
<tr><td><strong>Table 3</strong></td><td>69 次纵向运维事件门控审计表</td><td><code>N = 69</code> 次事件：68 次非写入窗口拦截（100% 准确），1 次授权写入，0 次重复上传；心跳触发延迟 <code>24.992 s</code>，执行耗时 <code>31.175 s</code></td></tr>
<tr><td><strong>Table 4</strong></td><td>5 大平台跨平台发布验证矩阵</td><td>覆盖 Bilibili、YouTube、X、抖音、Instagram；其中抖音回读超时被成功锁定为 <code>UNKNOWN</code>，阻止了重复发帖</td></tr>
<tr><td><strong>Table 5</strong></td><td>Instagram 端到端发布毫秒级耗时拆解表</td><td>端到端总耗时 <code>423.236 s</code>，发布命令耗时 <code>412.217 s</code>；其中提交后平台确认等待耗时 <code>380.551 s</code>（占比高达 <strong>92.32%</strong>）</td></tr>
<tr><td><strong>Table 6</strong></td><td>30 个视频交付版本 SHA-256 加密清单</td><td>3 大内容序列共 <code>N = 30</code> 个 MP4 交付版本，总计 <code>2,377,509,759 bytes (2.21 GB)</code>，100% 通过流式 SHA-256 校验</td></tr>
<tr><td><strong>Table 7</strong></td><td>55 条频道发布记录分层与非参数检验表</td><td>老视频序列（均值 3,477.00 播放）vs 鲸鱼娘序列（均值 95.89）vs GPT 值班日志（均值 32.56）；Mann–Whitney <code>U = 65.5, p = 0.0012, r_rb = 0.721</code></td></tr>
<tr><td><strong>Table 8</strong></td><td>CrossCast 代码库与自动化测试规模表</td><td><code>123</code> 个 Python 源码模块（<code>27,095 LOC</code>）+ <code>126</code> 个测试模块（<code>37,155 LOC</code>，共 <strong>2,737 个单元测试函数</strong>）</td></tr>
<tr><td><strong>Table 9</strong></td><td>多智能体交接动作分布统计表</td><td>统计 69 次运行中 <code>skip_outside_schedule</code>、<code>skip_cooldown</code>、<code>execute_authorized_task</code> 等动作精确分布</td></tr>
<tr><td><strong>Table 10</strong></td><td>多模型角色分工与治理边界矩阵</td><td>明确 Human PM、GPT/Codex、Claude、DeepSeek 的读写权限、门控约束与验收标准</td></tr>
<tr><td><strong>Table 11</strong></td><td>四层治理架构消融实验表（Ablation A1–A5）</td><td>验证移除 Layer 1、Layer 2、Layer 3（<code>UNKNOWN</code> 锁）、Preflight 或 HITL 接管后必现的故障模式</td></tr>
</tbody>
</table>

<h2>第 6–7 节：讨论、有效性威胁与结论（Sections 6 &amp; 7）</h2>
<ul>
  <li><strong>理论贡献：</strong> 将项目管理中的“阶段门控（Stage-Gate）”从人工会议评审推进为“确定性软件不变式 + 加密哈希锁 + 幂等回执状态机”。</li>
  <li><strong>实践启示：</strong> 揭示了混合自动化发布中 <strong>92.32% 的耗时集中在提交后等待确认期</strong> 这一关键工程定律，确立了“超时锁入 <code>UNKNOWN</code> 态、严禁盲目自动重传”的行业准则。</li>
  <li><strong>四维有效性威胁防御（Threats to Validity）：</strong> 全面覆盖构造有效性（Construct Validity）、内部有效性（Internal Validity）、外部有效性（External Validity）与结论有效性（Conclusion Validity）。</li>
</ul>
</body></html>`;

const cnHtmlPath = path.join(paperDir, 'manuscript_CN_companion_print.html');
fs.writeFileSync(cnHtmlPath, cnCompanionHtml, 'utf8');
htmlToPdf(cnHtmlPath, path.join(paperDir, 'manuscript_CN_companion.pdf'));

console.log('Generated reviewer_defense_playbook.pdf and manuscript_CN_companion.pdf.');
