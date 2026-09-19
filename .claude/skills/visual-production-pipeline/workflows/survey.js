export const meta = {
  name: "visual-production-survey",
  description:
    "Refresh tools and prerequisites for character appearance and UI/UX production (card-battle, Unity 6)",
  whenToUse:
    'visual-production-pipeline skill, survey mode. Pass args {today:"YYYY-MM-DD", focus?:"extra instruction"}',
  phases: [
    {
      title: "Research",
      detail:
        "internal docs, local env, and 4 external tool sweeps in parallel",
    },
    {
      title: "Verify",
      detail: "adversarially fact-check pricing / license / version claims",
    },
    { title: "Synthesize", detail: "merge into one structured Japanese brief" },
    { title: "Critique", detail: "completeness critic and gap fill" },
  ],
};

// args: { today: 'YYYY-MM-DD', focus?: string, previousReport?: 'docs/reports/....html' }
const TODAY = (args && args.today) || "unknown-date";
const FOCUS =
  args && args.focus ? `\nExtra focus from the user: ${args.focus}\n` : "";
const PREVIOUS =
  args && args.previousReport
    ? `\nA previous survey exists at ${args.previousReport} (read it with the Read tool). Report what changed since then.\n`
    : "";

const REPO = "C:/Users/user/orca/original-card-battle";
const CONTEXT = `
Project context (today is ${TODAY}):
- Repo ${REPO}: a turn-based card battle game by a solo developer ("こうだいさん"), Windows 11 desktop, low budget.
- Runtime is Unity 6. Battle core is C# in unity-port/BattleCore/ (source of truth). UGUI battle view lives in unity-port/unity-project-kit/Assets/View/ (BattleTheme.cs, ProceduralArt.cs, UiKit.cs, UiTween.cs etc.). Real Unity project path: C:/Users/user/Unity/RPG-by-card.
- Design SSOT: .claude/docs/vision/concept-v3.md, .claude/docs/battle_document/battle_ui_ux_v2.md, battle_ui_ux_v1.md, .claude/docs/enemy_document/enemy_roster_v4.md, .claude/docs/ui_ux_design_guide.md, .claude/docs/vision/plans/2026-06-28-unity-migration-character-art.md, and the skill reference .claude/skills/visual-production-pipeline/references/tools-and-prerequisites.md (the current tool choices).
- Goal: "tools and prerequisites for creating enemy and player character appearance, and UI/UX" for this game, feeding the visual-production-pipeline skill. Claude Code cannot paint raster images itself; separate what Claude can drive (APIs, local servers, MCP, code-generated art, HTML/SVG mockups, Unity automation) from what needs human hands.
${FOCUS}${PREVIOUS}`;

const RESEARCH_SCHEMA = {
  type: "object",
  properties: {
    markdown: {
      type: "string",
      description:
        "Findings in Japanese markdown. Dense, concrete, with file:line or URL for each fact.",
    },
    claims: {
      type: "array",
      description:
        "Externally checkable facts (price, license threshold, version, availability, policy). Empty for internal-only research.",
      items: {
        type: "object",
        properties: {
          id: { type: "string" },
          claim: { type: "string" },
          source_url: { type: "string" },
          as_of: { type: "string" },
        },
        required: ["id", "claim", "source_url"],
      },
    },
    open_questions: { type: "array", items: { type: "string" } },
  },
  required: ["markdown", "claims", "open_questions"],
};

const VERIFY_SCHEMA = {
  type: "object",
  properties: {
    results: {
      type: "array",
      items: {
        type: "object",
        properties: {
          id: { type: "string" },
          verdict: {
            type: "string",
            enum: ["confirmed", "corrected", "refuted", "unverifiable"],
          },
          corrected_claim: { type: "string" },
          primary_source_url: { type: "string" },
          note: { type: "string" },
        },
        required: ["id", "verdict", "note"],
      },
    },
  },
  required: ["results"],
};

const DIMENSIONS = [
  {
    key: "internal",
    external: false,
    prompt: `${CONTEXT}
Task: extract the INTERNAL prerequisites for character appearance and UI/UX production from the repo (read-only). Read the design SSOT files above, unity-port/README.md, unity-port/PHASE3-KICKOFF.md, unity-port/unity-project-kit/README.md, and the View code (BattleTheme.cs, ProceduralArt.cs, UiKit.cs, UiTween.cs, ArenaView.cs, BattleScreenView.cs). Skim .claude/skills/ui-ux-creator, enemy-creator, character-class-creator, the docs/mockups and docs/reports lists, and list files under public/assets/images and any art folder in the Unity project.
Report in Japanese markdown: 1) decided world / tone / art direction (file:line), 2) concrete asset requirements (player classes, each enemy with visual cues, states/poses, UI parts, timing budgets, resolution), 3) Unity integration points, 4) undecided items, 5) frozen / out-of-scope items. Set claims to an empty array.`,
  },
  {
    key: "local-env",
    external: false,
    prompt: `${CONTEXT}
Task: inventory the LOCAL Windows machine (Bash = Git Bash; powershell.exe -NoProfile -Command for Windows queries). Read-only: install nothing, modify nothing, never print secrets or env values.
Check: GPU + VRAM, RAM, free disk; python/uv/git/node/dotnet versions; Unity Hub + Editor versions and the RPG-by-card project's ProjectVersion.txt and relevant packages in Packages/manifest.json; art tools (Krita, Clip Studio Paint, Photoshop, GIMP, Aseprite, Blender, Live2D Cubism Editor, Spine, Inkscape, Figma, ComfyUI / Forge / SD WebUI, Ollama); Claude Code MCP server names related to design/Unity/browser/image (names only); skills under ~/.claude/skills related to design/visual/image/playwright/artifact; Japanese UI fonts installed (names only).
Report in Japanese markdown: an inventory table (item / present? / version / path) and "what this means for the pipeline". Set claims to an empty array.`,
  },
  {
    key: "illustration-ai",
    external: true,
    prompt: `${CONTEXT}
Task: web research (primary sources: official pricing pages, license texts, model cards, platform policies) on tools for CREATING still character art (player characters and enemies).
Cover: A) human illustration / finishing tools on Windows (Clip Studio Paint, Krita, Photoshop, Aseprite) and their role (line/paint, parts separation, PSD export); B) AI image generation for anime / dark-fantasy characters: local open models (Illustrious XL, NoobAI XL, Animagine XL, Pony, SDXL, FLUX variants, newer anime models) with their actual LICENSE for commercial game use, UIs (ComfyUI, Forge, SwarmUI) and VRAM needs, hosted services (NovelAI, Midjourney/niji, OpenAI gpt-image, Google Gemini image / Imagen, Adobe Firefly, Leonardo) with commercial terms and API availability; C) consistency methods (LoRA training, IP-Adapter / reference, ControlNet, inpainting) and getting separated layers for rigging; D) legal / platform prerequisites (Steam AI disclosure wording, Japan Agency for Cultural Affairs guidance, itch.io AI tagging, generation log / license ledger).
Output Japanese markdown with a comparison table (tool / role / cost / commercial ok? / Windows local? / API?) and a recommended default for a solo low-budget dev. In claims, list every price, license condition, VRAM figure and policy statement separately with source URL.`,
  },
  {
    key: "animation-2_5d",
    external: true,
    prompt: `${CONTEXT}
Task: web research (primary sources) on tools that make character art MOVE and battle VFX, integrated into Unity 6.
Cover: A) Live2D Cubism (Editor version, FREE vs PRO limits, PRO price in JPY, Cubism SDK for Unity incl. Unity 6 support, SDK release license revenue threshold, suitability for non-humanoid enemies); B) Spine (version, Essential vs Professional price, license revenue threshold, spine-unity Unity 6 support, what Essential lacks); C) Unity 2D Animation / PSD Importer / 2D IK / Sprite Swap; D) alternatives briefly (Rive Unity runtime, DragonBones, Spriter, Creature, Moho); E) VFX and feel (Particle System vs VFX Graph with URP 2D, Shader Graph + 2D lights, Effekseer license and Unity 6 support, DOTween / PrimeTween / LitMotion / Feel license and price, whether a hand-written tween helper is enough); F) hours per character for Live2D and Spine at hobbyist level, and commission price ranges.
Output Japanese markdown with a comparison table (tool / what it animates / cost / license threshold / Unity 6 support / fits enemies? / fits portraits?) and a recommended default path. In claims, list each price, version, threshold and support statement with source URL.`,
  },
  {
    key: "ui-ux-tools",
    external: true,
    prompt: `${CONTEXT}
Task: web research (primary sources) on tools for designing and implementing game UI/UX (card battle HUD, cards, menus) with Unity 6. The project designs UI as HTML mockups published as Claude Artifacts, then implements UGUI in C# code.
Cover: A) design tools (Figma plans and free tier, Penpot, Claude Design canvas, Excalidraw) and Figma → Unity bridges; B) Unity UI tech in Unity 6.x (UGUI vs UI Toolkit maturity, world-space, effects, animation; TextMeshPro inside uGUI; Japanese font setup: SDF atlas, dynamic fonts, fallback) and free commercial Japanese fonts with license (Noto Sans/Serif JP, M PLUS, BIZ UDPGothic, Zen fonts) plus dark-fantasy display fonts; C) icon / UI asset sources with licenses (game-icons.net CC BY 3.0, Kenney CC0, Unity Asset Store EULA); D) UX validation (5-second test, think-aloud, colorblind simulation, contrast, readability at 1080p / 720p); E) Claude-drivable Unity automation (Unity MCP servers such as CoplayDev/unity-mcp, IvanMurzak Unity-MCP, Unity official AI/MCP; batchmode screenshots).
Output Japanese markdown with a comparison table and a recommended default stack. In claims, list every price, license, version / support statement with source URL.`,
  },
  {
    key: "claude-driven-pipeline",
    external: true,
    prompt: `${CONTEXT}
Task: research how a Claude Code agent can DRIVE as much of the character-art + UI production pipeline as possible, and where a human must step in. WebSearch/WebFetch for external facts; read local Claude Code skill names only (no secrets).
Cover: A) image generation APIs callable from Node/Python with commercial terms and per-image price (OpenAI gpt-image latest, Google Gemini image / Imagen, Replicate / fal.ai SDXL / Illustrious / FLUX, Stability API, NovelAI API terms, Midjourney API availability) and content-policy limits for dark-fantasy monsters; B) local automation (ComfyUI HTTP/WebSocket API, ComfyUI MCP servers, background removal rembg / BiRefNet / BRIA RMBG license, upscalers Real-ESRGAN license, sprite atlas packing); C) code-generated art Claude can author directly (SVG, Unity procedural sprites, shaders) and when it is good enough; D) verification loops (screenshot crop/zoom review, Playwright screenshots of HTML mockups, Unity batchmode capture, perceptual diff); E) asset / license ledger formats and Steam disclosure text examples.
Output Japanese markdown: a step table "pipeline step / Claude can do / needs human / tool". In claims, list prices, license terms and API availability with source URLs.`,
  },
];

const results = await pipeline(
  DIMENSIONS,
  (d) =>
    agent(d.prompt, {
      label: `research:${d.key}`,
      phase: "Research",
      schema: RESEARCH_SCHEMA,
    }),
  async (res, d) => {
    if (!res) return null;
    if (!d.external || !res.claims || res.claims.length === 0)
      return { key: d.key, res, verify: null };
    const verify = await agent(
      `${CONTEXT}
You are an adversarial fact-checker. For EACH claim below, try to REFUTE it using primary sources (official pricing pages, license texts, model cards, policy pages, release notes) via WebSearch/WebFetch. Secondary blogs do not count as confirmation when a primary source exists. If numbers or thresholds differ, give the corrected claim. If you cannot reach a primary source, mark 'unverifiable' and say why. Default to 'unverifiable' rather than 'confirmed' when uncertain.

Claims (JSON):
${JSON.stringify(res.claims, null, 1)}`,
      { label: `verify:${d.key}`, phase: "Verify", schema: VERIFY_SCHEMA },
    );
    return { key: d.key, res, verify };
  },
);

const ok = results.filter(Boolean);
const missing = DIMENSIONS.map((d) => d.key).filter(
  (k) => !ok.some((r) => r.key === k),
);
if (missing.length) log(`research dimensions missing: ${missing.join(", ")}`);

const bundle = ok
  .map((r) => {
    const v = r.verify
      ? JSON.stringify(r.verify.results, null, 1)
      : "(internal / no external claims)";
    return `### DIMENSION: ${r.key}\n\n#### Findings\n${r.res.markdown}\n\n#### Claims\n${JSON.stringify(r.res.claims, null, 1)}\n\n#### Verification results\n${v}\n\n#### Open questions\n${(r.res.open_questions || []).map((q) => "- " + q).join("\n")}`;
  })
  .join("\n\n---\n\n");

phase("Synthesize");
const SYNTH_SCHEMA = {
  type: "object",
  properties: {
    brief_markdown: { type: "string" },
    decisions_pending: {
      type: "array",
      items: {
        type: "object",
        properties: {
          question: { type: "string" },
          recommendation: { type: "string" },
          why: { type: "string" },
        },
        required: ["question", "recommendation", "why"],
      },
    },
    pipeline_steps: {
      type: "array",
      items: {
        type: "object",
        properties: {
          step: { type: "string" },
          goal: { type: "string" },
          claude_does: { type: "string" },
          human_does: { type: "string" },
          tools: { type: "string" },
          output: { type: "string" },
          gate: { type: "string" },
        },
        required: [
          "step",
          "goal",
          "claude_does",
          "human_does",
          "tools",
          "output",
          "gate",
        ],
      },
    },
    unverified_or_corrected: { type: "array", items: { type: "string" } },
  },
  required: [
    "brief_markdown",
    "decisions_pending",
    "pipeline_steps",
    "unverified_or_corrected",
  ],
};
const synthPrompt = (extra) => `${CONTEXT}
Write the brief "敵・プレイヤーキャラクターの外見と UI/UX を作るためのツールと前提" for こうだいさん in Japanese (です・ます, conclusion first, one idea per sentence, half-width spaces between Japanese and ASCII, no emoji, no filler).
Facts: use pricing / license / version claims ONLY in their verified or corrected form. Refuted claims must not appear. Unverifiable claims are labelled 「未確認」 inline. Keep source URLs next to external facts and file:line next to internal facts.
brief_markdown sections: 1 結論（3 行以内） / 2 前提（決定済み・未決） / 3 必要なアセットの一覧 / 4 工程別のツール表 (コンセプト / 立ち絵制作(人手) / AI 生成 / 一貫性の維持 / パーツ分け / 動かす(2.5D) / VFX / UI デザイン / UI 実装 / 書体・アイコン / 検証 / 記録・ライセンス台帳) / 5 法務とプラットフォームの前提 / 6 リスクと回避.
Also fill decisions_pending, pipeline_steps (the repeatable per-character / per-UI-screen sequence with gates: human approval, visual-inspect check, license ledger entry, Artifact report, life-editor note), and unverified_or_corrected.
${extra || ""}

RESEARCH BUNDLE:
${bundle}`;
let synth = await agent(synthPrompt(""), {
  label: "synthesize",
  phase: "Synthesize",
  schema: SYNTH_SCHEMA,
});

phase("Critique");
const CRITIC_SCHEMA = {
  type: "object",
  properties: {
    gaps: {
      type: "array",
      items: {
        type: "object",
        properties: {
          gap: { type: "string" },
          how_to_fill: { type: "string" },
          severity: { type: "string", enum: ["high", "medium", "low"] },
        },
        required: ["gap", "how_to_fill", "severity"],
      },
    },
    errors: { type: "array", items: { type: "string" } },
  },
  required: ["gaps", "errors"],
};
const critic = await agent(
  `${CONTEXT}
You are a completeness critic. Review the brief against the research bundle and the goal. Find (1) missing roles or tools a solo dev would hit (arena/background art, card illustration vs portrait split, resolution and atlas budget, color-blind safety, Japanese font licensing, Git LFS for binary art, backup, naming), (2) statements that contradict verification results or repo docs, (3) pipeline steps without a clear gate or output. Only high-value items.

BRIEF:
${synth ? synth.brief_markdown : "(synthesis failed)"}

PIPELINE STEPS:
${synth ? JSON.stringify(synth.pipeline_steps, null, 1) : ""}

RESEARCH BUNDLE:
${bundle}`,
  { label: "critic", phase: "Critique", schema: CRITIC_SCHEMA },
);

const important = critic ? critic.gaps.filter((g) => g.severity !== "low") : [];
if (critic && (important.length || critic.errors.length)) {
  log(
    `critic: ${important.length} gaps, ${critic.errors.length} errors → gap fill + revise`,
  );
  const fill = await agent(
    `${CONTEXT}
Fill these gaps with concrete, sourced facts (primary sources for external facts; repo files for internal facts; read-only). Japanese markdown, one section per gap, mark unconfirmed items 「未確認」.

GAPS:
${JSON.stringify(important, null, 1)}

ERRORS TO CHECK:
${JSON.stringify(critic.errors, null, 1)}`,
    { label: "gap-fill", phase: "Critique" },
  );
  const revised = await agent(
    synthPrompt(`REVISION ROUND. Fix every critic error and integrate the gap-fill findings into your previous draft.

PREVIOUS brief_markdown:
${synth ? synth.brief_markdown : ""}

PREVIOUS pipeline_steps:
${synth ? JSON.stringify(synth.pipeline_steps, null, 1) : ""}

CRITIC ERRORS:
${JSON.stringify(critic.errors, null, 1)}

GAP-FILL FINDINGS:
${fill || "(none)"}`),
    { label: "revise", phase: "Critique", schema: SYNTH_SCHEMA },
  );
  if (revised) synth = revised;
}

return {
  synth,
  critic,
  verification: ok.map((r) => ({
    key: r.key,
    verify: r.verify ? r.verify.results : null,
  })),
  internal: ok.find((r) => r.key === "internal")?.res.markdown || null,
  localEnv: ok.find((r) => r.key === "local-env")?.res.markdown || null,
  missing,
};
