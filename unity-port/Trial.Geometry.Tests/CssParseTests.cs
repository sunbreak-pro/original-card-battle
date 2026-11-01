using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Trial.Geometry.Tests
{
    public class CssParseTests
    {
        // vendor/ds/bundle.css:15-52, every declaration block of the .ocb-card rules, verbatim
        private static readonly string[] OcbCardRules =
        {
            "position: relative; width: 216px; height: 304px; border-radius: var(--radius-panel); background: var(--card); box-shadow: var(--shadow-card); --attr: var(--steel); --attr-ink: var(--ink); user-select: none;",
            "--attr: var(--violet); --attr-ink: var(--phosphor);",
            "--attr: var(--guard); --attr-ink: var(--guard);",
            "--attr: var(--amber); --attr-ink: var(--amber);",
            "--attr: var(--crimson); --attr-ink: var(--hp-text);",
            "position: absolute; inset: 0; border-radius: var(--radius-panel); overflow: hidden; pointer-events: none;",
            "content: \"\"; position: absolute; left: 0; top: 0; bottom: 0; width: 8px; background: var(--attr);",
            "content: \"\"; position: absolute; left: 0; top: 0; right: 0; height: 5px; background: var(--attr);",
            "position: absolute; inset: 0; border: var(--stroke-frame) solid var(--attr); border-radius: var(--radius-panel); pointer-events: none;",
            "border-color: var(--wick);",
            "box-shadow: var(--shadow-card), var(--shadow-lamp);",
            "position: absolute; left: 16px; top: 11px; width: 44px; height: 44px; border-radius: 50%; border: var(--stroke-frame) solid var(--amber); background: rgba(0,0,0,.45); display: flex; align-items: center; justify-content: center; font: 700 28px/1 var(--font-mono); color: var(--amber);",
            "background: var(--amber); color: var(--ground);",
            "position: absolute; left: 50%; transform: translateX(-50%); bottom: -9px; height: 18px; padding: 0 4px; border-radius: 3px; background: var(--panel-solid); border: 1px solid var(--amber); font: 400 14px/16px var(--font-body); color: var(--ink); white-space: nowrap;",
            "position: absolute; left: 68px; top: 21px;",
            "position: absolute; right: 12px; top: 11px; height: 32px; padding: 0 8px; display: flex; align-items: center; gap: 4px; background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-tag); font: 700 20px/1 var(--font-mono); color: var(--ink); white-space: nowrap;",
            "font: 400 14px/1 var(--font-body);",
            "position: absolute; left: 16px; top: 60px; right: 12px; font: 700 22px/28px var(--font-display); letter-spacing: .02em; white-space: nowrap;",
            "position: absolute; left: 16px; top: 94px; display: flex; gap: 14px;",
            "display: grid; grid-template-columns: 24px auto; column-gap: 4px; align-items: center;",
            "font: 700 36px/40px var(--font-mono); color: var(--val-ink, var(--attr-ink));",
            "grid-column: 2; font: 400 16px/18px var(--font-body); color: var(--ink-muted); margin-top: -2px;",
            "position: absolute; left: 16px; top: 156px; width: 188px; height: 66px; font: 400 16px/22px var(--font-body); color: var(--ink); line-break: strict; word-break: normal; overflow-wrap: anywhere;",
            "top: 94px; height: 88px;",
            "display: flex; gap: 6px; align-items: center; height: 22px; white-space: nowrap;",
            "white-space: nowrap; overflow: visible;",
            "overflow: hidden;",
            "color: var(--ink-muted);",
            "position: absolute; left: 12px; right: 7px; bottom: 7px; height: 62px; background: rgba(0,0,0,.34); border: 1px solid var(--rule); border-radius: var(--radius-tag); display: flex; align-items: center; padding: 0 4px 0 0;",
            "border-style: dashed; border-color: var(--steel); color: var(--ink-muted); padding-left: 12px;",
            "width: 26px; display: flex; justify-content: center; flex: none;",
            "font: 400 16px/22px var(--font-body); white-space: nowrap;",
            "font-weight: 700;",
            "width: 10px; height: 10px; border-radius: 50%; background: var(--steel);",
            "width: 16px; height: 16px; background: var(--wick); box-shadow: 0 0 10px rgba(255,216,154,.8);",
            "position: absolute; inset: 0; border-radius: var(--radius-panel); background: #000; opacity: 0; pointer-events: none;",
            "opacity: var(--opacity-unpayable);",
            "text-decoration: underline dotted var(--ink-muted) 2px; text-underline-offset: 4px;",
        };

        // the card's own custom properties, as the rules above give them to the children
        private static CssVars CardVars(string attr = "var(--violet)", string attrInk = "var(--phosphor)") =>
            new CssVars().Set("--attr", attr).Set("--attr-ink", attrInk);

        [Test]
        public void EveryOcbCardDeclaration_Parses()
        {
            foreach (string rule in OcbCardRules)
            {
                CssBlock b = Css.Parse(rule, DsTheme.Dusk, CardVars());
                Assert.That(b.Unknown, Is.Empty, rule);
                Assert.That(b.Invalid, Is.Empty, rule);
            }
        }

        // the scenes' stylesheets, a sample of blocks verbatim (common, battle, field, hand, hud, explore, meta)
        private static readonly string[] SceneRules =
        {
            // common.css
            "position: fixed; inset: env(safe-area-inset-top, 0px) env(safe-area-inset-right, 0px) env(safe-area-inset-bottom, 0px) env(safe-area-inset-left, 0px); background: var(--ground); overflow: hidden; touch-action: none;",
            "font: 700 22px/1 var(--font-body); letter-spacing: .06em; color: var(--ink); background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-panel); padding: 16px 28px; cursor: pointer;",
            "outline: 2px solid var(--wick); outline-offset: 2px;",
            "position: absolute; inset: 0; z-index: 900; display: flex; align-items: center; justify-content: center; background: rgba(5,6,10,.62);",
            "position: absolute; z-index: 880; box-sizing: border-box; padding: 12px 20px; font: 400 20px/30px var(--font-body); color: var(--ink); background: var(--panel-solid); border: 1px solid var(--rule); border-left: 3px solid var(--caution); border-radius: var(--radius-panel); box-shadow: var(--shadow-tip); white-space: pre-line; transition: opacity .6s; pointer-events: none;",
            "animation-duration: 1ms !important; transition-duration: 1ms !important;",
            // field.css
            "position: absolute; left: 0; top: 556px; width: 1920px; height: 524px; background: linear-gradient(180deg, color-mix(in srgb, var(--ground) 80%, transparent), color-mix(in srgb, var(--ground) 92%, transparent));",
            "inset: 4px; box-shadow: 0 0 0 2px var(--panel-solid), inset 0 0 0 2px var(--panel-solid);",
            "content: ''; position: absolute; left: 0; right: 0; top: 8px; height: 1px; background: color-mix(in srgb, var(--ink) 55%, transparent);",
            "right: 8px; left: 50%; border-right: 1px solid color-mix(in srgb, var(--ink) 55%, transparent);",
            "border: 2px solid var(--amber); background: color-mix(in srgb, var(--amber) 16%, var(--panel-solid)); box-shadow: 0 0 12px color-mix(in srgb, var(--amber) 40%, transparent);",
            "opacity: 0; mix-blend-mode: screen;",
            "position: absolute; left: 0; top: 0; filter: brightness(0.82);",
            "position: absolute; top: 614px; width: 120px; box-sizing: border-box; padding: var(--space-6) var(--space-8) var(--space-8); display: flex; flex-direction: column; gap: var(--space-6);",
            "position: absolute; left: 0; top: 0; width: 1920px; height: 1080px; pointer-events: none; opacity: 0;\n  background: radial-gradient(ellipse 72% 66% at 50% 50%, transparent 58%, color-mix(in srgb, var(--crimson) 70%, transparent) 86%, var(--crimson) 100%);",
            "background: radial-gradient(ellipse 60% 58% at 50% 50%, transparent 66%, color-mix(in srgb, var(--crimson) 70%, transparent) 86%, color-mix(in srgb, var(--crimson) 75%, var(--flood, #06090c)) 100%);",
            "filter: drop-shadow(0 0 6px currentColor);",
            "position: absolute; inset: 0; transform-origin: 50% 90%;",
            "position: absolute; border-radius: 5px; background: linear-gradient(180deg, transparent, var(--ink) 55%, var(--ink)); box-shadow: 0 0 14px 4px color-mix(in srgb, var(--ink) 70%, transparent); pointer-events: none;",
            "font-family: var(--font-body); color: var(--ink); text-decoration: line-through 3px var(--miss);",
            "font: 400 16px/1 var(--font-body); color: var(--ink); margin-left: 6px; white-space: nowrap;",
            // hand.css
            "position: absolute; left: 0; top: 0; width: 216px; height: 304px; transform-origin: 50% 50%; will-change: transform, opacity;",
            "white-space: pre; overflow: hidden; line-break: auto; overflow-wrap: normal;",
            "left: 42px; top: 42px; bottom: auto; transform: translate(-50%, -50%);",
            "stroke: var(--ink-muted); stroke-width: 3px;",
            "position: absolute; height: 3px; box-shadow: 0 0 0 2px var(--keyline); pointer-events: none;",
            "filter: grayscale(0.85) brightness(0.62);",
            "position: relative; box-sizing: content-box; max-height: 330px; overflow: hidden; flex: none; padding-bottom: 0; border-bottom: 8px solid transparent;",
            "position: absolute; left: 0; width: 1920px; display: flex; align-items: center; justify-content: center; font: 400 22px/28px var(--font-body); color: var(--ink); text-shadow: 0 1px 3px var(--ground), 0 0 8px var(--ground); white-space: nowrap; pointer-events: none;",
            "position: absolute; left: 0; bottom: 100%; margin-bottom: 6px; padding: 2px 10px; background: var(--panel-solid); border: var(--stroke-frame) solid var(--wick); border-radius: var(--radius-tag, 4px); font: 700 20px/26px var(--font-body); color: var(--ink); white-space: nowrap; box-shadow: 0 0 12px color-mix(in srgb, var(--amber) 45%, transparent);",
            "display: flex; flex-direction: column; align-items: flex-start; gap: 2px; align-self: center; margin-left: -4px;",
            "display: inline-flex; align-items: center; gap: 2px; height: 20px; padding: 0 3px; border-radius: 4px; background: color-mix(in srgb, var(--card) 70%, var(--ink) 18%); box-shadow: 0 0 0 1px color-mix(in srgb, var(--ink) 25%, transparent); white-space: nowrap;",
            // hud.css
            "border: var(--stroke-frame) solid var(--amber); box-shadow: 0 0 10px color-mix(in srgb, var(--amber) 45%, transparent);",
            "position: absolute; height: 30px; font: 700 24px/30px var(--font-mono); font-variant-numeric: tabular-nums; color: var(--ink); white-space: nowrap;",
            "position: absolute; box-sizing: border-box; border: var(--stroke-rule) solid var(--rule); border-radius: 3px; overflow: hidden; background: color-mix(in srgb, var(--ground) 80%, transparent);",
            "width: 12px !important; height: 12px !important;",
            "opacity: var(--opacity-unplayed); filter: grayscale(1);",
            "position: absolute; display: flex; flex-wrap: wrap; align-content: flex-start; gap: 5px;",
            "position: absolute; width: 0; height: 0; border-left: 8px solid transparent; border-right: 8px solid transparent; border-top: 9px solid var(--violet); pointer-events: none;",
            "display: inline-flex; align-items: center; flex: none; box-sizing: border-box; padding: 0 2px; letter-spacing: -0.04em; border: 3px double var(--boss); border-radius: var(--radius-chip-foe); font: 700 16px/1 var(--font-body); color: var(--ink); white-space: nowrap;",
            "content: \"\"; position: absolute; left: 4px; right: 4px; top: 50%; height: 2px; background: var(--miss); transform: rotate(-24deg);",
            "content: \"\"; position: absolute; inset: 0; border: var(--stroke-frame) dashed var(--ink-muted); border-radius: var(--radius-panel); opacity: var(--plan-alpha, 0.62); pointer-events: none;",
            "order: 3; flex: 0 0 100%; margin: 0 0 10px; text-align: right; font: 400 16px/22px var(--font-body); color: var(--ink); white-space: nowrap;",
            "margin: 0 0 8px auto; padding: 10px 14px; font: 400 18px/1 var(--font-body); color: var(--ink); background: var(--card); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); cursor: pointer; white-space: nowrap;",
            "font: 400 16px/26px var(--font-body); color: var(--ink); white-space: pre-wrap;",
            "border-color: var(--ink); background-image: linear-gradient(color-mix(in srgb, var(--ink) 10%, transparent), color-mix(in srgb, var(--ink) 10%, transparent));",
            "font: inherit; font-variant-numeric: tabular-nums;",
            "display: grid; grid-template-columns: 96px 1fr 48px; align-items: center; column-gap: 14px; font: 400 20px/28px var(--font-body);",
            "position: absolute; right: 8px; top: 50%; translate: 0 -50%; padding: 4px 10px; background: var(--panel-solid); border: var(--stroke-frame) solid var(--phosphor); border-radius: var(--radius-tag, 4px); font: 400 16px/22px var(--font-body); color: var(--ink); white-space: nowrap; pointer-events: none; z-index: 2; box-shadow: 0 0 0 3px var(--panel-solid);",
            "position: absolute; left: 0; width: 1920px; height: 1240px; background: linear-gradient(180deg, transparent 0, color-mix(in srgb, var(--flood, #06090c) 94%, transparent) 160px);",
            // explore.css
            "animation: ocbg-explore-fade 420ms ease-out both;",
            "display: inline-flex; align-items: center; justify-content: center; min-width: 24px; height: 24px; padding: 0 6px; font: 600 16px/1 var(--font-mono); color: var(--ink); border: 1px solid var(--steel); border-radius: var(--radius-tag); background: var(--panel-solid);",
            "position: absolute; inset: 0; opacity: 0; transition: opacity 900ms ease-in-out;\n  background:\n    radial-gradient(60% 55% at 34% 88%, color-mix(in srgb, var(--amber) 14%, transparent), transparent 70%),\n    linear-gradient(color-mix(in srgb, var(--ground) 62%, transparent), color-mix(in srgb, var(--ground) 40%, transparent));",
            "position: absolute; left: 0; top: 0; width: 1920px; height: 96px; z-index: 7; display: flex; align-items: center; gap: 24px; padding: 0 24px; background: var(--panel); border-bottom: 1px solid var(--rule); box-shadow: 0 6px 18px color-mix(in srgb, var(--ground) 60%, transparent);",
            "width: 14px; height: 14px; border-radius: 3px; border: 2px solid var(--steel); transition: background-color 420ms ease-out, border-color 420ms ease-out, box-shadow 420ms ease-out;",
            "position: relative; width: 380px; height: 18px; margin-top: 10px; border: 1px solid; border-radius: 4px; overflow: hidden;",
            "position: absolute; bottom: 0; width: 2px; transform: translateX(-1px);",
            "width: 460px; flex: none; display: grid; grid-template-columns: 186px 1fr; grid-template-rows: 30px 26px; column-gap: 16px; row-gap: 6px; align-items: center;",
            "justify-self: start; font: 400 16px/22px var(--font-body); color: var(--ink); background: none; border: 1px solid transparent; border-radius: var(--radius-chip-self); padding: 0 10px; margin-left: -10px; white-space: nowrap; cursor: default;",
            "height: 72px; padding: 6px 12px 4px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px; color: var(--ink); background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-panel); cursor: pointer; font: 400 16px/20px var(--font-body); white-space: nowrap; transition: border-color 90ms, background-color 90ms, transform 90ms;",
            "border-color: var(--ink); transform: translateY(-2px);",
            "position: absolute; left: 0; top: calc(100% + 3px); padding: 0 8px; font: 700 16px/18px var(--font-body); color: var(--ink); border: 1px solid var(--amber); border-radius: var(--radius-chip-self); background: color-mix(in srgb, var(--amber) 16%, transparent); white-space: nowrap;",
            "position: absolute; inset: 0; z-index: 2; pointer-events: none; overflow: hidden; transition: opacity 500ms ease-in-out, filter 400ms ease-in-out;",
            "position: absolute; left: 0; top: 0; width: 520px; height: 400px; border-radius: 50%; pointer-events: none; will-change: transform;\n  background: radial-gradient(closest-side, color-mix(in srgb, var(--amber) 16%, transparent), color-mix(in srgb, var(--amber) 5%, transparent) 55%, transparent);",
            "stroke: var(--steel); stroke-width: 2.5; stroke-linecap: round; stroke-dasharray: 3 9; fill: none; transition: stroke 300ms, stroke-width 300ms;",
            "stroke: var(--amber); stroke-width: 4; stroke-dasharray: none; filter: drop-shadow(0 0 6px color-mix(in srgb, var(--amber) 55%, transparent));",
            "position: absolute; inset: 0; transition: transform 90ms cubic-bezier(.4,0,.2,1), opacity 300ms;",
            "transform: translateY(-6px) scale(1.04);",
            "position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: var(--panel-solid); border: 2px solid var(--steel); border-radius: var(--radius-panel); box-shadow: var(--shadow-card); transition: border-color 300ms, box-shadow 300ms, background-color 300ms;",
            "position: absolute; left: 50%; top: 96px; transform: translateX(-50%); font: 700 16px/20px var(--font-body); color: var(--ink); white-space: nowrap; text-shadow: 0 0 6px var(--ground), 0 1px 2px var(--ground);",
            "border: 3px solid var(--amber); box-shadow: var(--shadow-card), var(--shadow-reach);",
            "border-color: var(--amber); animation: ocbg-explore-beckon 2.4s ease-in-out infinite;",
            "border: 2px dashed var(--steel); background: color-mix(in srgb, var(--panel-solid) 72%, transparent); box-shadow: none;",
            "outline: 2px solid var(--boss); outline-offset: 4px;",
            "outline-style: dashed;",
            "content: \"\"; position: absolute; inset: -3px; border-radius: var(--radius-panel); border: 2px solid var(--amber); opacity: 0; pointer-events: none;",
            "position: absolute; inset: -24px; border-radius: 50%; background: radial-gradient(closest-side, color-mix(in srgb, var(--amber) 42%, transparent), transparent); animation: ocbg-explore-breathe 2.6s ease-in-out infinite;",
            "position: absolute; inset: 6px; border-radius: 50%; display: flex; align-items: center; justify-content: center; background: var(--panel-solid); border: 3px solid var(--amber); box-shadow: 0 2px 6px color-mix(in srgb, var(--ground) 80%, transparent), var(--shadow-reach);",
            "position: absolute; left: 24px; width: 1256px; top: 392px; height: 200px; z-index: 4; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px; pointer-events: none; opacity: 0; transform: translateY(8px); transition: opacity 420ms ease-out, transform 420ms ease-out;\n  background: linear-gradient(90deg, transparent, color-mix(in srgb, var(--ground) 92%, transparent) 18%, color-mix(in srgb, var(--ground) 92%, transparent) 82%, transparent);",
            "font: 800 64px/76px var(--font-display); letter-spacing: .14em;",
            "font: 400 24px/32px var(--font-body); font-style: normal; letter-spacing: .04em;",
            "display: grid; grid-template-columns: 72px 1fr auto; column-gap: 16px; align-items: center;",
            "outline: 2px solid var(--boss); outline-offset: 3px;",
            "display: grid; grid-template-columns: 92px 1fr; column-gap: 12px; align-items: baseline;",
            "margin-top: auto; display: grid; gap: 14px;",
            "display: flex; align-items: flex-start; gap: 10px; margin: 2px 0 0; padding: 8px 10px; font: 400 17px/24px var(--font-body); background: color-mix(in srgb, var(--caution) 10%, transparent); border-left: 3px solid var(--caution); border-radius: 0 var(--radius-tag) var(--radius-tag) 0;",
            "display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px;",
            "position: relative; height: 112px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; padding: 8px; color: var(--ink); background: var(--panel-solid); border: 2px solid var(--steel); border-radius: var(--radius-panel); font: 700 18px/22px var(--font-body); cursor: default; transition: transform 90ms, border-color 120ms, background-color 120ms;",
            "border-color: var(--wick); box-shadow: var(--shadow-lamp); animation: ocbg-explore-pulse 1.6s ease-in-out infinite; cursor: pointer;",
            "position: absolute; inset: 0; pointer-events: auto; background: color-mix(in srgb, var(--ground) 64%, transparent); animation: ocbg-explore-fade 260ms ease-out both;",
            "pointer-events: auto; display: flex; flex-direction: column; gap: 14px; padding: 36px 44px 32px; border-radius: var(--radius-panel); box-shadow: var(--shadow-tip); animation: ocbg-explore-plate-in 300ms cubic-bezier(.22,.61,.36,1) both;",
            "background: var(--washi); color: var(--washi-ink); border: 1px solid color-mix(in srgb, var(--washi-ink) 22%, transparent);\n  box-shadow: inset 0 0 90px color-mix(in srgb, var(--washi-ink) 14%, transparent), var(--shadow-tip);",
            "align-items: center; text-align: center; padding-top: 44px; background:\n  radial-gradient(80% 70% at 50% 0%, color-mix(in srgb, var(--violet) 14%, transparent), transparent 70%), var(--panel-solid);",
            "border-top: 3px solid var(--crimson);",
            "position: relative; width: 216px; height: 304px; animation: ocbg-explore-deal 460ms cubic-bezier(.22,.61,.36,1) both; transition: transform 120ms cubic-bezier(.4,0,.2,1);",
            "flex: 1; min-height: 0; overflow: auto; display: flex; flex-direction: column; border-top: 1px solid color-mix(in srgb, var(--washi-ink) 25%, transparent);",
            "display: grid; grid-template-columns: 28px 150px 1fr auto; align-items: center; column-gap: 14px; min-height: 52px; padding: 6px 8px; text-align: left; color: var(--washi-ink); background: none; border: 0; border-bottom: 1px solid color-mix(in srgb, var(--washi-ink) 15%, transparent); cursor: pointer; font: 400 18px/24px var(--font-body);",
            "background: color-mix(in srgb, var(--washi-ink) 8%, transparent); box-shadow: inset 3px 0 0 var(--amber);",
            "outline: 2px solid var(--amber); outline-offset: -2px;",
            "flex-shrink: 0;",
            "grid-column: 3 / -1; margin-top: 2px; font: 400 16px/22px var(--font-body); color: var(--washi-ink);",
            "-webkit-mask-image: linear-gradient(180deg, transparent, var(--ground) 14%, var(--ground) 86%, transparent); mask-image: linear-gradient(180deg, transparent, var(--ground) 14%, var(--ground) 86%, transparent);",
            "border-color: var(--amber); background: radial-gradient(closest-side, var(--amber) 60%, transparent 66%);",
            "position: absolute; left: -1px; top: -1px; animation: ocbg-explore-fade 160ms ease-out both;",
            "position: absolute; left: 96px; top: 236px; width: 1112px; pointer-events: auto; display: flex; flex-direction: column; gap: 18px; padding: 44px 56px 40px; color: var(--washi-ink); background: var(--washi); border: 1px solid color-mix(in srgb, var(--washi-ink) 22%, transparent); border-radius: var(--radius-panel);\n  box-shadow: inset 0 0 110px color-mix(in srgb, var(--washi-ink) 14%, transparent), var(--shadow-tip); animation: ocbg-explore-plate-in 600ms cubic-bezier(.22,.61,.36,1) both;",
            "pointer-events: auto; display: flex; flex-direction: column; gap: 16px; padding: 24px 28px; color: var(--ink); background: var(--panel); border: 1px solid var(--rule); border-radius: var(--radius-panel); animation: ocbg-explore-plate-in 700ms 120ms cubic-bezier(.22,.61,.36,1) both;",
            "display: block; width: 534px; height: 300px; animation: ocbg-explore-window 9s ease-in-out infinite alternate;",
            "content: \"\"; position: absolute; inset: 0; box-shadow: inset 0 0 40px color-mix(in srgb, var(--ground) 70%, transparent); pointer-events: none;",
            "position: absolute; right: 0; top: 0; width: 576px; height: 1080px; pointer-events: auto; display: flex; flex-direction: column; gap: 14px; padding: 28px 32px; color: var(--washi-ink); background: var(--washi); box-shadow: var(--shadow-tip); animation: ocbg-explore-drawer 220ms cubic-bezier(.22,.61,.36,1) both;",
            "animation: none;",
            "position: relative; display: flex; flex-direction: column; align-items: flex-start; gap: 10px; min-height: 150px; padding: 20px 24px 18px 64px; text-align: left; color: var(--ink); background: var(--panel-solid); border: 2px solid var(--steel); border-radius: var(--radius-panel); cursor: pointer; transition: transform 90ms, border-color 120ms, background-color 120ms;",
            "border-color: var(--amber); transform: translateX(4px);",
            "display: flex; grid-template-columns: none;",
            // meta.css
            "font-family: inherit; color: inherit; background: none; border: 0; padding: 0; cursor: pointer; -webkit-tap-highlight-color: transparent;",
            "position: absolute; font: 400 16px/22px var(--font-body); color: var(--ink); letter-spacing: .06em; white-space: nowrap;",
            "width: 8px; height: 8px;",
            "background: var(--steel); border-radius: 4px;",
            "display: flex; align-items: center; justify-content: center; gap: var(--space-16); border: var(--stroke-frame) solid var(--amber); border-radius: var(--radius-panel); background: color-mix(in srgb, var(--amber) 16%, transparent); font: 700 28px/1 var(--font-display); letter-spacing: .2em; color: var(--ink); transition: background .14s, box-shadow .14s, transform .14s, opacity .2s;",
            "display: inline-flex; align-items: center; justify-content: center; gap: var(--space-8); height: 100%; padding: 0 var(--space-24); background: var(--panel); border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-panel); font: 700 20px/1 var(--font-body); letter-spacing: .06em; transition: border-color .14s, background .14s, transform .14s;",
            "position: absolute; pointer-events: none; background: linear-gradient(180deg, color-mix(in srgb, var(--ground) 0%, transparent), color-mix(in srgb, var(--ground) 92%, transparent) 70%);",
            "position: absolute; transform-origin: 50% 100%;",
            "font: 700 30px/1 var(--font-display); letter-spacing: .34em; color: var(--ink); white-space: nowrap; text-shadow: 0 2px 12px color-mix(in srgb, var(--ground) 80%, transparent);",
            "display: flex; flex-direction: row-reverse; justify-content: flex-start; gap: 40px;",
            "writing-mode: vertical-rl; font: 400 46px/1 var(--font-display); letter-spacing: .22em; color: var(--ink); text-shadow: 0 0 18px color-mix(in srgb, var(--violet) 40%, transparent);",
            "position: relative; display: flex; align-items: baseline; gap: var(--space-16); padding: 10px 0 10px 48px; text-align: left; font: 700 36px/1.2 var(--font-display); letter-spacing: .14em; color: var(--ink); transition: letter-spacing .2s, text-shadow .2s;",
            "position: absolute; left: 0; top: 50%; width: 32px; height: 2px; margin-top: -1px; background: var(--amber); transform: scaleX(0); transform-origin: 0 50%; transition: transform .22s;",
            "content: \"\"; position: absolute; right: -4px; top: -3px; width: 8px; height: 8px; border-radius: 50%; background: var(--wick); box-shadow: 0 0 10px var(--wick); opacity: 0; transition: opacity .22s;",
            "font: 800 56px/1.1 var(--font-display); letter-spacing: .24em; color: var(--ink); text-shadow: 0 0 30px color-mix(in srgb, var(--amber) 30%, transparent);",
            "position: relative; background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-left: 3px solid var(--steel); border-radius: var(--radius-panel); padding: 14px 16px 12px 18px; display: flex; flex-direction: column; gap: 6px;",
            "overflow: hidden; border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); transition: box-shadow .2s, border-color .2s;",
            "border-color: var(--wick); box-shadow: 0 0 0 1px var(--wick), 0 0 22px color-mix(in srgb, var(--wick) 30%, transparent);",
            "position: absolute; right: -5px; top: -5px; width: 14px; height: 14px; border-radius: 50%; background: radial-gradient(closest-side, var(--wick), color-mix(in srgb, var(--wick) 0%, transparent));",
            "font: 400 16px/1.45 var(--font-body); padding: 8px 10px; border-left: 2px solid var(--amber); background: color-mix(in srgb, var(--amber) 8%, transparent);",
            "display: grid; grid-template-columns: 136px 1fr auto; align-items: center; gap: var(--space-12); min-height: 42px; border-bottom: var(--stroke-rule) solid var(--rule);",
            "display: flex; justify-content: space-between; gap: var(--space-12); padding: 6px 0 6px 12px; border-left: 2px dashed var(--steel); font: 400 16px/1.4 var(--font-body);",
            "width: 62px; height: 92px; flex: none; display: flex; flex-direction: column; align-items: center; justify-content: center; border: 3px solid var(--amber); border-radius: var(--radius-tag); font: 800 30px/1.08 var(--font-display); color: var(--amber); background: color-mix(in srgb, var(--amber) 10%, transparent);",
            "animation: ocbg-meta-stamp .7s ease-out;",
            "height: 48px; padding: 0 14px; flex: 1 1 0; display: inline-flex; align-items: center; justify-content: center; gap: 8px; background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel); font: 700 18px/1 var(--font-body); white-space: nowrap; transition: border-color .14s, background .14s;",
            "position: relative; display: block; width: 156px; height: 219px; margin: 0 auto; border-radius: var(--radius-panel); transition: transform .14s ease-out, box-shadow .14s;",
            "box-shadow: 0 0 0 3px var(--amber), 0 0 20px color-mix(in srgb, var(--amber) 40%, transparent);",
            "opacity: .4;",
            "position: relative; height: 14px; border-radius: 7px; background: color-mix(in srgb, var(--ground) 70%, transparent); border: var(--stroke-rule) solid var(--rule); overflow: hidden;",
            "position: absolute; left: 0; top: 0; bottom: 0; background: var(--steel); transition: width .26s ease-out, background .2s;",
            "position: absolute; top: -2px; bottom: -2px; width: 2px; margin-left: -1px; background: var(--ink);",
            "content: \"\"; width: 12px; height: 12px; margin-top: 8px; flex: none; border-radius: 50%; border: var(--stroke-frame) solid var(--steel);",
            "position: relative; display: flex; flex-wrap: wrap; gap: 6px 8px; overflow-y: auto; height: 84px; flex: none; align-content: flex-start; scroll-snap-type: y mandatory; scrollbar-width: none;",
            "height: 24px; padding: 2px 8px; border-radius: var(--radius-tag); background: color-mix(in srgb, var(--ink) 7%, transparent); font: 400 16px/20px var(--font-body); white-space: nowrap; scroll-snap-align: start; transition: box-shadow .14s, background .14s;",
            "box-shadow: inset 0 0 0 var(--stroke-rule) var(--amber); background: color-mix(in srgb, var(--amber) 16%, transparent);",
            "content: \"▼\"; display: inline-block; margin-right: 6px; font-size: 12px; color: var(--amber); transform: translateY(-1px);",
            "font: 400 16px/1.35 var(--font-body); margin-top: 4px; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;",
            "background-color: var(--washi); color: var(--washi-ink);\n  background-image:\n    var(--ocbg-washi-tex, none),\n    radial-gradient(ellipse at 30% 18%, color-mix(in srgb, var(--washi) 65%, var(--ink) 35%) 0%, color-mix(in srgb, var(--washi) 0%, transparent) 60%),\n    radial-gradient(ellipse at 78% 92%, color-mix(in srgb, var(--washi-ink) 7%, transparent) 0%, color-mix(in srgb, var(--washi-ink) 0%, transparent) 55%);\n  background-size: 360px 360px, 100% 100%, 100% 100%;",
            "display: flex; flex-direction: column; gap: var(--space-16); min-height: 560px; padding: 64px 92px 52px; border-radius: 3px; box-shadow: 0 34px 80px color-mix(in srgb, var(--ground) 85%, transparent), inset 0 0 0 1px color-mix(in srgb, var(--washi-ink) 10%, transparent), inset 0 0 60px color-mix(in srgb, var(--washi-ink) 7%, transparent); transform-origin: 50% 40%;",
            "font: 800 72px/1 var(--font-display); letter-spacing: .32em;",
            "width: 8px; height: 8px; flex: none; border-radius: 50%; background: var(--washi-ink); transform: translateY(-4px);",
            "content: \"\"; position: absolute; left: 0; right: 0; top: -14px; bottom: -14px;",
            "position: absolute; top: 50%; width: 24px; height: 24px; margin: -12px 0 0 -12px; border-radius: 50%; background: var(--panel-solid); border: var(--stroke-frame) solid var(--amber); box-shadow: 0 0 10px color-mix(in srgb, var(--amber) 40%, transparent);",
            "padding: 0; margin: 0 auto 0 0; max-width: 300px; flex: 1 1 auto; min-width: 0;",
            "display: flex; flex-direction: column; padding: 26px 28px 18px; box-shadow: -18px 0 40px color-mix(in srgb, var(--ground) 70%, transparent); border-left: 1px solid color-mix(in srgb, var(--washi-ink) 25%, transparent);",
            "display: grid; grid-template-columns: repeat(4, 1fr); gap: 6px; margin-top: 20px;",
            "height: 46px; font: 700 20px/1 var(--font-display); letter-spacing: .08em; color: var(--washi-ink); background: color-mix(in srgb, var(--washi-ink) 6%, transparent); border-bottom: 3px solid transparent; border-radius: 4px 4px 0 0; transition: background .14s;",
            "padding: 0 4px; font: 700 18px/1.15 var(--font-display); letter-spacing: .02em; word-break: keep-all; overflow-wrap: anywhere;",
            "width: 10px; height: 10px; border-radius: 50%; border: 1.5px solid var(--washi-ink);",
            "display: grid; grid-template-columns: 132px 1fr; gap: 14px; padding: 8px 0; border-bottom: 1px dotted color-mix(in srgb, var(--washi-ink) 25%, transparent);",
            "display: block; width: 100%; flex: 1; min-height: 360px; margin-top: 12px; padding: 4px 6px; resize: none; border: 0; outline: none; border-radius: 0;\n  background-color: transparent; color: var(--washi-ink); caret-color: var(--washi-ink); font: 400 20px/36px var(--font-display); letter-spacing: .04em;\n  background-image: linear-gradient(180deg, transparent 35px, color-mix(in srgb, var(--washi-ink) 22%, transparent) 35px, color-mix(in srgb, var(--washi-ink) 22%, transparent) 36px);\n  background-size: 100% 36px; background-attachment: local;",
            "font-size: 14px; line-height: 19px;",
            "white-space: normal; height: auto; min-height: 22px; align-items: flex-start; line-height: 20px;",
            "text-wrap: pretty;",
            "word-break: auto-phrase;",
            "text-wrap: balance;",
            "width: 120px; height: 1px; background: linear-gradient(90deg, color-mix(in srgb, var(--ink) 0%, transparent), color-mix(in srgb, var(--ink) 55%, transparent));",
            "transform: scaleX(-1);",
            "content: \"\"; position: absolute; left: 22%; right: 22%; top: 0; height: 1px; background: linear-gradient(90deg, color-mix(in srgb, var(--violet) 0%, transparent), color-mix(in srgb, var(--violet) 55%, transparent), color-mix(in srgb, var(--violet) 0%, transparent));",
        };

        private static string S(string part)
        {
            string[] hits = SceneRules.Where(r => r.Contains(part)).ToArray();
            Assert.That(hits.Length, Is.EqualTo(1), "sample lookup: " + part);
            return hits[0];
        }

        [Test]
        public void SceneDeclarations_Parse()
        {
            var vars = CardVars();
            foreach (string rule in SceneRules)
            {
                CssBlock b = Css.Parse(rule, DsTheme.Dusk, vars);
                Assert.That(b.Unknown, Is.Empty, rule);
                Assert.That(b.Invalid, Is.Empty, rule);
            }
        }

        [Test]
        public void Card_Values()
        {
            CssBlock card = Css.Parse(OcbCardRules[0]);
            Assert.That((card.Layout.Width.Value.Px, card.Layout.Height.Value.Px), Is.EqualTo((216f, 304f)));
            Assert.That(card.Box.Radius.Resolve(216f, 304f), Is.EqualTo(CornerSet.All(6f)));
            Assert.That(card.Box.BackgroundColor, Is.EqualTo(DsTokens.Card));
            Assert.That(card.Box.Shadows, Is.EqualTo(DsTokens.ShadowCard));
            Assert.That(card.CustomProperties["--attr"], Is.EqualTo("var(--steel)"));
            Assert.That(card.Ignored.Select(i => i.Property), Is.EqualTo(new[] { "user-select" }));
            // the edge inherits --attr from the card: an attack card's edge is violet
            BoxStyle edge = Css.Box(OcbCardRules[8], DsTheme.Dusk, CardVars());
            Assert.That(edge.Top, Is.EqualTo(new BorderSide(2f, BorderStyle.Solid, DsTokens.Violet)));
            // without the variable the declaration is invalid (the browser would drop it too)
            Assert.That(Css.Parse(OcbCardRules[8]).Invalid.Count, Is.EqualTo(1));
            CssBlock cost = Css.Parse(OcbCardRules[11]);
            Assert.That(cost.Box.Top.Width, Is.EqualTo(2f));
            Assert.That(cost.Text.Font, Is.EqualTo(new FontSpec(Family.Mono, 700, 28f, 28f)));
            Assert.That((cost.Layout.Display, cost.Layout.AlignItems, cost.Layout.JustifyContent), Is.EqualTo(("flex", (Align?)Align.Center, (Justify?)Justify.Center)));
            CssBlock val = Css.Parse(OcbCardRules[20], DsTheme.Dusk, new CssVars().Set("--attr-ink", "var(--guard)"));
            Assert.That(val.Text.Color, Is.EqualTo(DsTokens.Guard)); // var(--val-ink, var(--attr-ink))
            CssBlock small = Css.Parse(OcbCardRules[21]);
            Assert.That((small.Layout.GridColumnStart, small.Layout.MarginTop.Value.Px), Is.EqualTo(((int?)2, -2f)));
            CssBlock term = Css.Parse(OcbCardRules[37]);
            Assert.That((term.Text.Decoration, term.Text.DecorationStyle, term.Text.DecorationThickness, term.Text.UnderlineOffset),
                Is.EqualTo(((DecorationLine?)DecorationLine.Underline, (BorderStyle?)BorderStyle.Dotted, (float?)2f, (float?)4f)));
            Assert.That(term.Text.DecorationColor, Is.EqualTo(DsTokens.InkMuted));
            CssBlock plain = Css.Parse(OcbCardRules[28] + " " + OcbCardRules[29]);
            Assert.That(plain.Box.Left, Is.EqualTo(new BorderSide(1f, BorderStyle.Dashed, DsTokens.Steel)));
            Assert.That(plain.Box.Padding, Is.EqualTo(new Insets(0f, 4f, 0f, 12f)));
            Assert.That(Css.Parse(OcbCardRules[30]).Layout.Grow, Is.EqualTo(0f)); // flex: none
        }

        [Test]
        public void Borders_ShorthandsAndLonghands()
        {
            BoxStyle notice = Css.Box(S("z-index: 880"));
            Assert.That(notice.Top, Is.EqualTo(new BorderSide(1f, BorderStyle.Solid, DsTokens.Rule)));
            Assert.That(notice.Left, Is.EqualTo(new BorderSide(3f, BorderStyle.Solid, DsTokens.Caution)));
            BoxStyle arm = Css.Box("position: absolute; top: 50%; height: 12px; margin-top: -6px; border: 1px solid rgba(236,235,242,.55); border-right: 0; border-left-width: 1px;");
            Assert.That((arm.Right.Used, arm.Left.Width, arm.Left.Style), Is.EqualTo((0f, 1f, BorderStyle.Solid)));
            BoxStyle tail = Css.Box("border-left: 8px solid transparent; border-right: 8px solid transparent; border-top: 9px solid var(--violet);");
            Assert.That((tail.Left.Visible, tail.Left.Used, tail.Top.Color), Is.EqualTo((false, 8f, DsTokens.Violet)));
            // `border: 1px solid;` takes currentColor
            BoxStyle gauge = Css.Box("color: var(--phosphor); border: 1px solid;");
            Assert.That(gauge.Top.Color, Is.EqualTo(DsTokens.Phosphor));
            BoxStyle boss = Css.Box("border: 3px double var(--boss)");
            Assert.That(boss.Top.Style, Is.EqualTo(BorderStyle.Double));
            Assert.That(Css.Box("border: 0").Top.Used, Is.EqualTo(0f));
            Assert.That(Css.Box("border: none").Top.Style, Is.EqualTo(BorderStyle.None));
            Assert.That(Css.Box("border-width: 1px 2px 3px").Left.Width, Is.EqualTo(2f));
        }

        [Test]
        public void Backgrounds_LayersSizesPositions()
        {
            BoxStyle lull = Css.Box(S("radial-gradient(60% 55% at 34% 88%"));
            Assert.That(lull.Backgrounds.Count, Is.EqualTo(2));
            Assert.That(lull.Backgrounds[0].Image, Is.InstanceOf<RadialGradientSpec>());
            Assert.That(((RadialGradientSpec)lull.Backgrounds[0].Image).Prelude, Is.EqualTo("60% 55% at 34% 88%"));
            Assert.That(lull.Backgrounds[1].Image, Is.InstanceOf<LinearGradientSpec>());
            BoxStyle end = Css.Box(S("radial-gradient(80% 70% at 50% 0%, color-mix(in srgb, var(--violet)"));
            Assert.That(end.Backgrounds.Count, Is.EqualTo(1));
            Assert.That(end.BackgroundColor, Is.EqualTo(DsTokens.PanelSolid));
            BoxStyle washi = Css.Box(SceneRules.First(r => r.Contains("ocbg-washi-tex")));
            Assert.That(washi.BackgroundColor, Is.EqualTo(DsTokens.Washi));
            Assert.That(washi.Backgrounds.Count, Is.EqualTo(3));
            Assert.That(washi.Backgrounds[0].Image, Is.Null); // var(--ocbg-washi-tex, none)
            Assert.That((washi.Backgrounds[0].SizeW.Px, washi.Backgrounds[0].SizeH.Px), Is.EqualTo((360f, 360f)));
            Assert.That(washi.Backgrounds[1].SizeW.Percent, Is.EqualTo(100f));
            BoxStyle streak = Css.Box("background: linear-gradient(270deg, #fff 0%, #fff 70%, transparent 100%) center / 100% 6px no-repeat, linear-gradient(270deg, #fff 0%, transparent 100%) center / 100% 18px no-repeat");
            Assert.That(streak.Backgrounds.Count, Is.EqualTo(2));
            Assert.That(streak.Backgrounds[0].Repeat, Is.EqualTo(BackgroundRepeat.NoRepeat));
            Assert.That(streak.Backgrounds[1].SizeH.Px, Is.EqualTo(18f));
            Assert.That(streak.Backgrounds[0].Tile(new RectF(0f, 0f, 100f, 18f)), Is.EqualTo(new RectF(0f, 6f, 100f, 6f)));
            BoxStyle memo = Css.Box(SceneRules.First(r => r.Contains("background-attachment")));
            var memoImage = (LinearGradientSpec)memo.Backgrounds[0].Image;
            Assert.That(memoImage.Stops.Select(s => s.Pos), Is.EqualTo(new[] { 35f, 35f, 36f }));
            Assert.That(memo.Backgrounds[0].SizeH.Px, Is.EqualTo(36f));
            Assert.That(Css.Box("background: none").Backgrounds, Is.Empty);
        }

        [Test]
        public void Shadows_TextShadowsAndFilters()
        {
            BoxStyle row = Css.Box("box-shadow: inset 3px 0 0 var(--amber)");
            Assert.That(row.Shadows[0], Is.EqualTo(new BoxShadow(3f, 0f, 0f, 0f, DsTokens.Amber, true)));
            BoxStyle sheet = Css.Box(SceneRules.First(r => r.Contains("0 34px 80px")));
            Assert.That(sheet.Shadows.Count, Is.EqualTo(3));
            Assert.That(sheet.Shadows[1].Inset && sheet.Shadows[2].Inset, Is.True);
            Assert.That(sheet.Shadows[2].Blur, Is.EqualTo(60f));
            TextStyleData guide = Css.Text(S("text-shadow: 0 1px 3px var(--ground), 0 0 8px"));
            Assert.That(guide.Shadows, Is.EqualTo(new[] { new TextShadow(0f, 1f, 3f, DsTokens.Ground), new TextShadow(0f, 0f, 8f, DsTokens.Ground) }));
            FilterSpec other = Css.Parse(S("filter: grayscale(0.85)")).Filter;
            Assert.That(other.Color.Brightness, Is.EqualTo(0.62f).Within(1e-6f));
            Assert.That(other.Color.Grayscale, Is.EqualTo(0.85f).Within(1e-6f));
            FilterSpec glow = Css.Parse("color: var(--amber); filter: drop-shadow(0 0 6px currentColor);").Filter;
            Assert.That(glow.DropShadows[0], Is.EqualTo(new DropShadow(0f, 0f, 6f, DsTokens.Amber)));
            Assert.That(glow.DropShadows[0].Sigma, Is.EqualTo(6f)); // measured: σ = b for drop-shadow
            Assert.That(Css.Shadows("none"), Is.Empty);
            Assert.That(Css.TextShadows("0 0 0 transparent")[0].Color.A, Is.EqualTo(0f));
        }

        [Test]
        public void Text_FontLineHeightSpacing()
        {
            TextStyleData banner = Css.Text("font: 800 64px/72px var(--font-display); letter-spacing: .12em;");
            Assert.That(banner.Font, Is.EqualTo(new FontSpec(Family.Display, 800, 64f, 72f)));
            Assert.That(banner.LetterSpacingEm, Is.EqualTo(0.12f));
            TextStyleData item = Css.Text(SceneRules.First(r => r.Contains("36px/1.2")));
            Assert.That(item.LineHeightRatio, Is.EqualTo(1.2f));
            Assert.That(item.Font.LineHeightPx, Is.EqualTo(43.2f).Within(1e-4f));
            TextStyleData tagline = Css.Text(SceneRules.First(r => r.Contains("vertical-rl")));
            Assert.That(tagline.WritingMode, Is.EqualTo("vertical-rl"));
            Assert.That(tagline.Shadows[0].Color.A, Is.EqualTo(0.4f).Within(1e-5f));
            TextStyleData clamp = Css.Text(SceneRules.First(r => r.Contains("-webkit-line-clamp")));
            Assert.That(clamp.LineClamp, Is.EqualTo(2));
            TextStyleData chip = Css.Text("font: 700 16px/1 var(--font-body); letter-spacing: -0.04em");
            Assert.That(chip.LetterSpacingEm, Is.EqualTo(-0.04f));
            TextStyleData inheritFont = Css.Text("font: inherit; font-variant-numeric: tabular-nums;");
            Assert.That((inheritFont.Size, inheritFont.TabularNums), Is.EqualTo(((float?)null, (bool?)true)));
            TextStyleData sizeOnly = Css.Text("font-size: 14px; line-height: 19px;");
            Assert.That((sizeOnly.Size, sizeOnly.LineHeightPx), Is.EqualTo(((float?)14f, (float?)19f)));
        }

        [Test]
        public void Layout_FlexGridPositioning()
        {
            LayoutStyle hint = Css.Layout(S("order: 3; flex: 0 0 100%"));
            Assert.That((hint.Order, hint.Grow, hint.Shrink, hint.Basis.Value.Percent), Is.EqualTo(((int?)3, (float?)0f, (float?)0f, 100f)));
            Assert.That(hint.MarginBottom.Value.Px, Is.EqualTo(10f));
            LayoutStyle close = Css.Layout(S("margin: 0 0 8px auto"));
            Assert.That((close.MarginLeft.Value.IsAuto, close.MarginBottom.Value.Px), Is.EqualTo((true, 8f)));
            LayoutStyle bonus = Css.Layout(SceneRules.First(r => r.Contains("calc(100% + 3px)")));
            Assert.That((bonus.Top.Value.Percent, bonus.Top.Value.Px), Is.EqualTo((100f, 3f)));
            Assert.That(bonus.Top.Value.Resolve(96f), Is.EqualTo(99f));
            LayoutStyle band = Css.Layout(SceneRules.First(r => r.Contains("grid-template-rows: 30px 26px")));
            Assert.That(band.GridColumns, Is.EqualTo(new[] { Track.Px(186f), Track.Fr(1f) }));
            Assert.That(band.GridRows, Is.EqualTo(new[] { Track.Px(30f), Track.Px(26f) }));
            Assert.That((band.ColumnGap, band.RowGap), Is.EqualTo(((float?)16f, (float?)6f)));
            LayoutStyle deck = Css.Layout(SceneRules.First(r => r.Contains("gap: 6px 8px")));
            Assert.That((deck.RowGap, deck.ColumnGap, deck.Wrap, deck.AlignContent), Is.EqualTo(((float?)6f, (float?)8f, (bool?)true, (AlignContent?)AlignContent.Start)));
            LayoutStyle tagline = Css.Layout(SceneRules.First(r => r.Contains("row-reverse")));
            Assert.That((tagline.Column, tagline.Reverse), Is.EqualTo(((bool?)false, (bool?)true)));
            LayoutStyle inset = Css.Layout("inset: -1px");
            Assert.That((inset.Top.Value.Px, inset.Left.Value.Px), Is.EqualTo((-1f, -1f)));
            LayoutStyle env = Css.Layout(S("safe-area-inset-top"));
            Assert.That(env.Top.Value.Px, Is.EqualTo(0f)); // env(safe-area-inset-top, 0px) → 0px
        }

        [Test]
        public void TransformsAndMotion()
        {
            CssBlock down = Css.Parse(OcbCardRules[13]);
            Assert.That(down.Transform.Compose().TxPercent, Is.EqualTo(-50f));
            CssBlock centre = Css.Parse(S("left: 42px; top: 42px"));
            TransformParts c = centre.Transform.Compose();
            Assert.That((c.TxPercent, c.TyPercent), Is.EqualTo((-50f, -50f)));
            Affine m = centre.Transform.ToAffine(100f, 40f, new Vec2(50f, 20f));
            Assert.That(m.Apply(Vec2.Zero), Is.EqualTo(new Vec2(-50f, -20f)));
            CssBlock lift = Css.Parse(S("transform: translateY(-6px) scale(1.04)"));
            TransformParts l = lift.Transform.Compose();
            Assert.That((l.Ty, l.Sx), Is.EqualTo((-6f, 1.04f)));
            CssBlock word = Css.Parse(S("translate: 0 -50%"));
            Assert.That(word.Translate.Compose().TyPercent, Is.EqualTo(-50f));
            Assert.That(word.CombinedTransform().Functions.Count, Is.EqualTo(1));
            CssBlock slash = Css.Parse(S("rotate(-24deg)"));
            Assert.That(slash.Transform.Compose().Rot, Is.EqualTo(-24f).Within(1e-4f));
            CssBlock mark = Css.Parse(SceneRules.First(r => r.Contains("scaleX(0)")));
            Assert.That((mark.OriginX.Value.Px, mark.OriginY.Value.Percent), Is.EqualTo((0f, 50f)));
            Assert.That(mark.Transform.Compose().Sx, Is.EqualTo(0f));
            CssBlock node = Css.Parse(S("inset: 0; transition: transform 90ms"));
            Assert.That(node.Transitions.Select(t => (t.Property, t.DurationMs)), Is.EqualTo(new[] { ("transform", 90f), ("opacity", 300f) }));
            CssBlock next = Css.Parse(SceneRules.First(r => r.Contains("700ms 120ms")));
            Assert.That((next.Animations[0].Name, next.Animations[0].DelayMs), Is.EqualTo(("ocbg-explore-plate-in", 120f)));
            Assert.That(Css.Parse("animation: none;").Animations, Is.Empty);
            CssBlock reduced = Css.Parse(S("animation-duration: 1ms"));
            Assert.That(reduced.Animations[0].DurationMs, Is.EqualTo(1f));
        }

        [Test]
        public void SvgAndMask()
        {
            CssBlock edge = Css.Parse(SceneRules.First(r => r.Contains("stroke-dasharray: 3 9")));
            Assert.That((edge.Svg.Stroke, edge.Svg.StrokeWidth, edge.Svg.LineCap, edge.Svg.FillNone), Is.EqualTo(((Rgba?)DsTokens.Steel, (float?)2.5f, (Cap?)Cap.Round, true)));
            Assert.That(edge.Svg.DashArray, Is.EqualTo(new[] { 3f, 9f }));
            CssBlock list = Css.Parse(SceneRules.First(r => r.StartsWith("-webkit-mask-image")));
            var mask = (LinearGradientSpec)list.MaskImage;
            Assert.That(mask.Stops.Select(s => s.Pos), Is.EqualTo(new[] { float.NaN, 0.14f, 0.86f, float.NaN }));
        }

        [Test]
        public void RoundTrip_BoxStyleThroughItsCss()
        {
            var vars = CardVars();
            string[] samples =
            {
                OcbCardRules[0], OcbCardRules[11], OcbCardRules[15], OcbCardRules[28] + " " + OcbCardRules[29], OcbCardRules[35],
                S("z-index: 880"), S("inset: 4px; box-shadow"), S("border-top: 9px solid"), S("radial-gradient(60% 55% at 34% 88%"), S("520px; height: 400px"), S("stroke-dasharray: 3 9"),
                SceneRules.First(r => r.Contains("0 34px 80px")), SceneRules.First(r => r.Contains("ocbg-washi-tex")),
            };
            foreach (string s in samples)
            {
                BoxStyle a = Css.Box(s, DsTheme.Dusk, vars);
                string css = Css.ToCss(a);
                BoxStyle b = Css.Box(css);
                Assert.That(Css.ToCss(b), Is.EqualTo(css), s);
                Assert.That(b.Shadows, Is.EqualTo(a.Shadows), s);
                Assert.That(b.Top, Is.EqualTo(a.Top).Using<BorderSide>((x, y) => x.Width == y.Width && x.Style == y.Style && x.Color.Approximately(y.Color, 1f / 255f) ? 0 : 1), s);
                Assert.That(b.BackgroundColor.Approximately(a.BackgroundColor, 1f / 255f), Is.True, s);
                Assert.That(b.Backgrounds.Count, Is.EqualTo(a.Backgrounds.Count), s);
                for (int i = 0; i < a.Backgrounds.Count; i++)
                {
                    RectF box = new RectF(0f, 0f, 200f, 120f);
                    Paint pa = a.Backgrounds[i].Image?.Resolve(box), pb = b.Backgrounds[i].Image?.Resolve(box);
                    if (pa == null) { Assert.That(pb, Is.Null); continue; }
                    foreach (Vec2 q in new[] { new Vec2(10f, 10f), new Vec2(100f, 60f), new Vec2(190f, 110f) })
                        Assert.That(pb.At(q).Approximately(pa.At(q), 2f / 255f), Is.True, s + " @" + q);
                }
                Assert.That(b.Radius.Resolve(200f, 120f), Is.EqualTo(a.Radius.Resolve(200f, 120f)), s);
                Assert.That(b.Padding, Is.EqualTo(a.Padding), s);
            }
        }

        [Test]
        public void Cache_ReturnsCopies()
        {
            BoxStyle a = Css.Box("background: var(--panel); border: 1px solid var(--rule); border-radius: 6px");
            a.BackgroundColor = Rgba.White;
            a.Shadows.Add(new BoxShadow(1f, 1f, 1f, 1f, Rgba.Black));
            BoxStyle b = Css.Box("background: var(--panel); border: 1px solid var(--rule); border-radius: 6px");
            Assert.That(b.BackgroundColor, Is.EqualTo(DsTokens.Panel));
            Assert.That(b.Shadows, Is.Empty);
            // vars are part of the key
            Assert.That(Css.Box("background: var(--attr)", DsTheme.Dusk, new CssVars().Set("attr", "#fff")).BackgroundColor, Is.EqualTo(Rgba.White));
            Assert.That(Css.Box("background: var(--attr)", DsTheme.Dusk, new CssVars().Set("attr", "#000")).BackgroundColor, Is.EqualTo(Rgba.Black));
        }

        [Test]
        public void Cache_IsNotPoisonedByChangesToTheCopies()
        {
            // explore deal cards: one animation string, a delay per card (ExploreScene.js:1116 style.animationDelay)
            const string deal = "position: relative; width: 216px; height: 304px; animation: ocbg-explore-deal 460ms cubic-bezier(.22,.61,.36,1) both; transition: transform 120ms cubic-bezier(.4,0,.2,1);";
            CssBlock first = Css.Parse(deal);
            first.Animations[0].DelayMs = 120f + 3 * 110f;
            first.Transitions[0].DurationMs = 999f;
            CssBlock second = Css.Parse(deal);
            Assert.That(second.Animations[0].DelayMs, Is.EqualTo(0f));
            Assert.That(second.Transitions[0].DurationMs, Is.EqualTo(120f));
            Assert.That(second.Animations[0].Keyframes, Is.SameAs(first.Animations[0].Keyframes)); // frames are shared, they never change
            // a gradient's stops
            const string band = "background: linear-gradient(90deg, transparent, var(--ink) 50%, transparent);";
            var spec = (LinearGradientSpec)Css.Box(band).Backgrounds[0].Image;
            spec.Stops.Clear();
            spec.AngleDeg = 0f;
            var again = (LinearGradientSpec)Css.Box(band).Backgrounds[0].Image;
            Assert.That((again.Stops.Count, again.AngleDeg), Is.EqualTo((3, 90f)));
            // a mask
            const string fade = "mask-image: linear-gradient(180deg, transparent, var(--ground) 14%, var(--ground) 86%, transparent);";
            ((LinearGradientSpec)Css.Parse(fade).MaskImage).Stops.RemoveAt(0);
            Assert.That(((LinearGradientSpec)Css.Parse(fade).MaskImage).Stops.Count, Is.EqualTo(4));
        }

        [Test]
        public void Cache_SeesAParentScopeChangedLater()
        {
            // the card sets --attr on itself; its children parse against a child scope of the card's
            var card = new CssVars().Set("--attr", "var(--violet)");
            var child = new CssVars(card);
            const string bar = "content: \"\"; position: absolute; left: 0; top: 0; bottom: 0; width: 8px; background: var(--attr);";
            Assert.That(Css.Box(bar, DsTheme.Dusk, child).BackgroundColor, Is.EqualTo(DsTokens.Violet));
            card.Set("--attr", "var(--amber)"); // the card turns into a skill card
            Assert.That(Css.Box(bar, DsTheme.Dusk, child).BackgroundColor, Is.EqualTo(DsTokens.Amber));
            card.Set("--attr", "var(--violet)");
            Assert.That(Css.Box(bar, DsTheme.Dusk, child).BackgroundColor, Is.EqualTo(DsTokens.Violet));
        }

        [Test]
        public void AnimationLonghands_ApplyToEveryAnimation()
        {
            // common.css reduced motion: one duration for every animation of the element
            CssBlock b = Css.Parse("animation: ocbg-explore-fade 260ms ease-out both, ocbg-explore-pulse 1.6s ease-in-out infinite; animation-duration: 1ms !important;");
            Assert.That(b.Animations.Select(a => a.DurationMs), Is.EqualTo(new[] { 1f, 1f }));
            CssBlock c = Css.Parse("animation: ocbg-explore-fade 260ms ease-out both, ocbg-explore-pulse 1.6s ease-in-out infinite; animation-delay: 10ms, 20ms; animation-fill-mode: forwards; animation-direction: alternate; animation-iteration-count: 2");
            Assert.That(c.Animations.Select(a => (a.DelayMs, a.Fill, a.Direction, a.Iterations)),
                Is.EqualTo(new[] { (10f, AnimFill.Forwards, AnimDirection.Alternate, 2f), (20f, AnimFill.Forwards, AnimDirection.Alternate, 2f) }));
            CssBlock named = Css.Parse("animation-name: ocbg-explore-fade, ocbg-explore-pulse; animation-duration: 200ms");
            Assert.That(named.Animations.Select(a => (a.Name, a.DurationMs, a.Keyframes != null)),
                Is.EqualTo(new[] { ("ocbg-explore-fade", 200f, true), ("ocbg-explore-pulse", 200f, true) }));
            Assert.That(Css.Parse("animation: ocbg-explore-fade 1s; animation-duration: -1s").Invalid.Select(i => i.Property), Is.EqualTo(new[] { "animation-duration" }));
        }

        [Test]
        public void AlignSelfAuto_AndNormal()
        {
            Assert.That(Css.Layout("align-self: center; align-self: auto").AlignSelf, Is.Null); // the container's align-items
            Assert.That(Css.Layout("justify-self: auto").JustifySelf, Is.Null);
            Assert.That(Css.Layout("align-items: normal").AlignItems, Is.EqualTo((Align?)Align.Stretch)); // normal = stretch for flex / grid items
            Assert.That(Css.Layout("justify-self: start").JustifySelf, Is.EqualTo((Align?)Align.Start));
        }

        [Test]
        public void UnknownAndInvalid_AreReported()
        {
            CssBlock b = Css.Parse("frobnicate: 3; width: banana; color: var(--nope); cursor: pointer; -webkit-user-select: none");
            Assert.That(b.Unknown.Select(u => u.Property), Is.EqualTo(new[] { "frobnicate" }));
            Assert.That(b.Invalid.Select(u => u.Property), Is.EqualTo(new[] { "width", "color" }));
            Assert.That(b.Ignored.Count, Is.EqualTo(2));
            Assert.Throws<FormatException>(() => Css.Shadows("1px solid"));
            Assert.Throws<FormatException>(() => Css.Length("12deg"));
        }
    }
}
