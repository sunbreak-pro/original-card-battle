// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/GrammarSamples.cs
// #57: the battles the Editor's sample capture plays, one per system of the effect grammar
// (battle_ui_ux_v2 §5.3) and one for the trait cue over a blow (§5.9 J2). Pure C#: BattleCore +
// Depiction.Script, no UnityEngine, so which card stands for a system, from which gap it is played
// and which effect the capture waits for are all checked under `dotnet test`. The capture itself
// (Editor/GrammarSampleCapture.cs) only plays these sources and takes the picture.
import { ArgumentNullException, InvalidOperationException, idiv } from '../../shared/cs.js';
import { CardCatalog } from '../../core/battle/CardCatalog.js';
import { Cards } from '../../core/battle/Cards.js';
import { Constants } from '../../core/battle/Constants.js';
import { Enemies } from '../../core/battle/Enemies.js';
import { BattleSetup } from '../../core/battle/TurnLoop.js';
import {
  CueKind, DepictionEventKind, DepictionText, PlayVerdict, UnitSide,
} from '../depictionScript/DepictionTypes.js';
import { EffectGrammar, FxMark, FxSystem } from '../depictionScript/EffectGrammar.js';
import { EffectId } from '../depictionScript/Effects.js';
import { BattleLaunch } from './BattleLaunch.js';
import { CardSystems } from './CardSystems.js';
import { CoreBattleSource } from './CoreBattleSource.js';

/** new ArgumentNullException(nameof(x)) with .NET's message. */
function argNull(name) {
  const e = new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/**
 * One sample: a card, the gap it is played from, and the effect whose middle is the picture.
 *
 * JS extra: `init` plays the part of a C# object initializer (`new GrammarSample { System = ..., ... }`):
 * its own properties are assigned after the field defaults. `new GrammarSample()` is the C# default.
 */
export class GrammarSample {
  constructor(init) {
    /** 1-based, in the order of §5.3's table; the trait cue comes last. */
    this.Index = 0;

    /** The system the picture shows (the blow's own system for the trait cue). (default(FxSystem) = Slash) */
    this.System = FxSystem.Slash;

    /** The sample shows the trait cue (§5.9 J2) laid over the blow, not the system alone. */
    this.TraitAccent = false;

    /** @type {object|null} CardDef */
    this.Card = null;

    /** The gap on turn 1: the first one from which the card plays the beat this sample shows. */
    this.StartGap = 0;

    /** The effect the capture waits for: the picture is taken in its middle. (default(EffectId) = its first member) */
    this.Watch = EffectId.CardDraw;

    if (init) Object.assign(this, init);
  }

  /** "01-slash.png" … "08-breath.png", "09-slash-trait-accent.png". */
  get FileName() {
    // Index.ToString("00"): at least two digits; a negative number keeps its sign before the digits.
    const digits = String(Math.abs(this.Index)).padStart(2, '0');
    const index = this.Index < 0 ? '-' + digits : digits;
    // FxSystem values are their member names, so ToString() is the value itself.
    return index + '-' + String(this.System).toLowerCase() + (this.TraitAccent ? '-trait-accent' : '') + '.png';
  }

  /** One line for the log and the index file: "斬 袈裟斬り (kesa_cut) 間合い 1 StrikeShape". */
  get Label() {
    return EffectGrammar.Glyph(this.System) + (this.TraitAccent ? ' + 特性の合図' : '') + ' ' + this.Card.Name + ' (' + this.Card.Id + ') 間合い ' + this.StartGap + ' ' + this.Watch;
  }

  /** The battle the capture plays: the sample card only, the leftmost one suggested for the auto drag. */
  CreateSource() {
    return GrammarSamples.SourceFor(this.Card, this.StartGap);
  }
}

/** C# `private static IReadOnlyList<GrammarSample> _all;` */
let _all = null;

export const GrammarSamples = {
  /**
   * Copies of the sample card in the deck: the opening hand holds nothing else, and the pile keeps
   * cards past the hand (HandDraw 6) so a draw face has something to draw. (C# const = Constants.HandDraw * 2)
   */
  get DeckCopies() { return Constants.HandDraw * 2; },

  /** The gaps tried, nearest first (the slice's line holds 0〜3 from the player's start cell). */
  MaxGap: 4,

  /** The player opens at half HP, so a heal (息) has room to show its number and the chest's rise. (C# const = Constants.PlayerMaxHp / 2) */
  get StartHp() { return idiv(Constants.PlayerMaxHp, 2); },

  /** The nine samples: the eight systems in §5.3's order, then the trait cue over a blow. */
  get All() { return _all ?? (_all = GrammarSamples.Build()); },

  SetupFor(card, startGap) {
    if (card == null) throw argNull('card');
    return new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck([card], GrammarSamples.DeckCopies),
      BattleSetup.SliceFieldCells, { StartGap: startGap, PlayerStartHp: GrammarSamples.StartHp });
  },

  /** The same source the capture hands the screen; suggestCards drives the screen's auto drag. */
  SourceFor(card, startGap) {
    return new CoreBattleSource(GrammarSamples.SetupFor(card, startGap), BattleLaunch.DefaultSeed, true);
  },

  /**
   * Plays the source the way the screen's auto drag does: the turn starts, the suggested card is
   * released on the zone its aim needs, and the events up to the next wait are read. Returns the
   * card's play, or null when no card could be played.
   */
  PlayFirstCard(source) {
    if (source == null) throw argNull('source');
    for (let guard = 0; !source.WaitingForPlayer; guard++) {
      if (source.Finished || guard > 50) return null;
      source.AdvanceAuto();
    }
    const id = source.SuggestedCardId;
    if (id == null || id.length === 0) return null;
    const face = DepictionText.Find(source.Frame.Hand, id);
    if (face == null) return null;
    // C# `TryPlay(id, zone, out DepictionEvent played)`: the port returns { ok: verdict, value: played }.
    const attempt = source.TryPlay(id, DepictionText.RequiredZone(face.Aim));
    if (attempt.ok !== PlayVerdict.Accepted) return null;
    const played = attempt.value;
    if (played.Kind === DepictionEventKind.PlayCard) return played;
    for (let guard = 0; !source.WaitingForPlayer && !source.Finished && guard < 50; guard++) {
      const next = source.AdvanceAuto();
      if (next.Kind === DepictionEventKind.PlayCard) return next;
    }
    return null;
  },

  /**
   * The effect the screen starts for `system` in this play, or null when the play does not show it.
   * A blow is its StrikeShape (§5.3) and the trait cue its TraitAccent (§5.9); 盾 / 型 / 気 draw
   * their mark (GrammarMark); 息 is the chest's rise on a heal, else the pips lighting. (EffectId?)
   */
  WatchOf(play, system, traitAccent) {
    if (play == null) return null;
    if (EffectGrammar.IsBlow(system)) {
      for (const cue of play.Cues) {
        if (cue.Kind !== CueKind.Slash || cue.Source !== UnitSide.Player || cue.Fx !== system) continue;
        if (cue.TraitAccent !== traitAccent) continue;
        return traitAccent ? EffectId.TraitAccent : EffectId.StrikeShape;
      }
      return null;
    }
    if (traitAccent) return null; // the cue rides a blow only
    if (system === FxSystem.Breath) {
      for (const cue of play.Cues) {
        if (cue.Fx === system && EffectGrammar.MarkOf(cue) === FxMark.ChestHeave) return EffectId.BreathHeave;
      }
      for (const cue of play.Cues) {
        if (cue.Fx === system && cue.Kind === CueKind.StaminaChange && cue.Amount > 0) return EffectId.StaminaRecover;
      }
      return null;
    }
    for (const cue of play.Cues) {
      if (cue.Fx === system && EffectGrammar.MarkOf(cue) === FxMark.Shape) return EffectId.GrammarMark;
    }
    return null;
  },

  /**
   * The sample for one system: the first card in canon order (§2's initial forty first) whose row is
   * that system alone, played from the first gap that shows it with no trait firing, so the picture
   * holds the system's shape and nothing of a trait. Failing that, a gap where a trait fires; failing
   * that, a card that shares its row with another system.
   */
  For(system) {
    const alone = (row) => row.length === 1 && row[0] === system;
    const among = (row) => Contains(row, system);
    const sample = FirstPlaying(system, false, alone, true)
      ?? FirstPlaying(system, false, alone, false)
      ?? FirstPlaying(system, false, among, false);
    if (sample == null) throw new InvalidOperationException('No card plays ' + EffectGrammar.Glyph(system) + '.');
    return sample;
  },

  /**
   * A trait of the card fired in this play: its beat (§5.9 順 1) is on the script. The stance a card
   * sets is written as a TraitFire beat too (「構え・…」), but that one is the 型 face and carries its
   * system; a trait's own beat carries none.
   */
  TraitFired(play) {
    if (play == null) return false;
    for (const cue of play.Cues) {
      if (cue.Kind === CueKind.TraitFire && cue.Fx == null) return true;
    }
    return false;
  },

  /** C# private static IReadOnlyList<GrammarSample> Build() */
  Build() {
    const samples = [];
    for (const system of EffectGrammar.Systems) {
      const sample = GrammarSamples.For(system);
      sample.Index = samples.length + 1;
      samples.push(sample);
    }
    // §5.9 J2: the trait cue on the 斬 sample's own card when its trait can be lit, so the two
    // pictures differ only by the cue; else on the first blow that lights one.
    const slash = samples[0];
    let accent = TryCard(slash.Card, slash.System, true, false);
    if (accent == null) {
      for (const system of EffectGrammar.Systems) {
        if (!EffectGrammar.IsBlow(system)) continue;
        accent = FirstPlaying(system, true, (row) => Contains(row, system), false);
        if (accent != null) break;
      }
    }
    if (accent == null) throw new InvalidOperationException('No blow lights its trait from any gap.');
    accent.Index = samples.length + 1;
    samples.push(accent);
    // .AsReadOnly(): a read-only view over the list.
    return Object.freeze(samples);
  },
};

/** C# private static GrammarSample FirstPlaying(FxSystem system, bool traitAccent, Func<IReadOnlyList<FxSystem>, bool> rowFits, bool plainOnly) */
function FirstPlaying(system, traitAccent, rowFits, plainOnly) {
  for (const def of CardCatalog.All) {
    if (!rowFits(CardSystems.Of(def))) continue;
    const sample = TryCard(def, system, traitAccent, plainOnly);
    if (sample != null) return sample;
  }
  return null;
}

/**
 * C# private static GrammarSample TryCard(CardDef card, FxSystem system, bool traitAccent, bool plainOnly)
 * @param plainOnly Only from a gap where no trait of the card fires.
 */
function TryCard(card, system, traitAccent, plainOnly) {
  for (let gap = 0; gap <= GrammarSamples.MaxGap; gap++) {
    const play = GrammarSamples.PlayFirstCard(GrammarSamples.SourceFor(card, gap));
    if (plainOnly && GrammarSamples.TraitFired(play)) continue;
    const watch = GrammarSamples.WatchOf(play, system, traitAccent);
    if (watch != null) {
      return new GrammarSample({ System: system, TraitAccent: traitAccent, Card: card, StartGap: gap, Watch: watch });
    }
  }
  return null;
}

/** C# private static bool Contains(IReadOnlyList<FxSystem> row, FxSystem system) */
function Contains(row, system) {
  for (const s of row) {
    if (s === system) return true;
  }
  return false;
}
