// Port of unity-project-kit/Assets/View/Depiction/Script/TurnSliceScript.cs
// The hand-written one-turn script (plan 2026-09-19-unity-battle-depiction §4).
// Numbers are the §18 draft scale (attack 6 / 13 / 21, guard 4 / 9 / 15, recovery 3).
// Every value below is a settled result written by hand; nothing here is a formula the
// View could re-run.
// The filmed hand stays at five cards, the rule before #351 (six since 2026-10-06): this is a
// record of the recorded slice, not the current rule. The core deals six.
//
// Port notes: the objects are built as the C# object initializers build them, and shared the same
// way (the one `attackNear` omen frame stands in the frames of events 1 to 5). Kesagiri / Daijodan /
// Ushirotobi are DemoDeck's strings (C#: consts copied from DemoDeck).
import {
  CardAim,
  CornerFrame,
  Cue,
  CueKind,
  DepictionEvent,
  DepictionEventKind,
  DepictionFrame,
  DepictionScript,
  OmenFrame,
  OmenIcon,
  RangeSide,
  UnitFrame,
  UnitSide,
} from './DepictionTypes.js';
import { EffectStrength } from './Effects.js';
import { DemoDeck } from './DemoDeck.js';

function Snapshot(playerHp, playerGuard, stamina, range, enemyHp, hand, omen, stanceHint = '+3') {
  return new DepictionFrame({
    Corner: new CornerFrame({ Turn: 2, Floor: 1, ChainIndex: 1, ChainTotal: 3, MiasmaPercent: 0 }),
    Player: new UnitFrame({
      Hp: playerHp, HpMax: 50, Guard: playerGuard,
      ShowStamina: true, Stamina: stamina, StaminaMax: 10,
      HasRange: true, Range: range, RangeGlyph: GlyphOf(range),
    }),
    Enemy: new UnitFrame({ Hp: enemyHp, HpMax: 60, Guard: 0, ShowStamina: false, HasRange: false }),
    Omen: omen,
    Hand: hand,
    StanceHint: stanceHint,
    StanceDivider: TurnSliceScript.StanceThreshold,
  });
}

/** The script decides how a range side is printed; the View prints the string as given. */
function GlyphOf(range) {
  return DemoDeck.Glyph(range);
}

/**
 * The five faces the slice opens with, printed from DemoDeck's table. The slice is written at the top
 * of a turn with the attack omen showing, so both printed traits are lit.
 */
function FullHand() {
  const hand = [];
  for (const def of DemoDeck.All) {
    hand.push(DemoDeck.Face(def, def.DefId, true));
  }
  return hand;
}

function HandWithout(...played) {
  const hand = FullHand();
  // List.RemoveAll(c => Array.IndexOf(played, c.Id) >= 0), in place.
  for (let i = hand.length - 1; i >= 0; i--) {
    if (played.indexOf(hand[i].Id) >= 0) hand.splice(i, 1);
  }
  return hand;
}

export const TurnSliceScript = {
  // The slice deals one of each kind, so a card's kind id doubles as its instance id.
  Kesagiri: DemoDeck.Kesagiri,
  Daijodan: DemoDeck.Daijodan,
  Ushirotobi: DemoDeck.Ushirotobi,

  /** Stamina left at the turn's end that still earned the stance shield in the recorded slice. */
  StanceThreshold: 3,

  Build() {
    const Kesagiri = TurnSliceScript.Kesagiri;
    const Daijodan = TurnSliceScript.Daijodan;
    const Ushirotobi = TurnSliceScript.Ushirotobi;
    const script = new DepictionScript();

    // Turn 2 opens: HP 50/50, stamina 6/10, near. Enemy 錆槍の竜兵 HP 60/60, no range side.
    const opening = Snapshot(50, 0, 6, RangeSide.Near, 60, [], new OmenFrame({ Visible: false }));
    script.Opening = opening;

    // 1. Turn start: Guard 0, stamina 6 -> 9, draw five, show the omen.
    const attackNear = new OmenFrame({ Visible: true, Icon: OmenIcon.Attack, KindLabel: '攻撃', SideGlyph: '近', ValueText: '13' });
    const turnStart = new DepictionEvent({
      Order: 1, Kind: DepictionEventKind.TurnStart, Title: 'ターン開始',
      After: Snapshot(50, 0, 9, RangeSide.Near, 60, FullHand(), attackNear),
    });
    turnStart.Cues.push(new Cue({ Kind: CueKind.GuardReset, Target: UnitSide.Player, GuardAfter: 0 }));
    turnStart.Cues.push(new Cue({ Kind: CueKind.StaminaChange, Target: UnitSide.Player, Amount: 3, StaminaAfter: 9, StaminaMax: 10 }));
    turnStart.Cues.push(new Cue({ Kind: CueKind.DrawHand, Target: UnitSide.Player, Amount: 5 }));
    turnStart.Cues.push(new Cue({ Kind: CueKind.OmenShow, Target: UnitSide.Enemy }));
    script.Events.push(turnStart);

    // 2. 袈裟斬り (cost 1): trait 初手 +3 fires, 9 damage, enemy 60 -> 51, stamina 9 -> 8.
    const kesagiri = new DepictionEvent({
      Order: 2, Kind: DepictionEventKind.PlayCard, Title: '袈裟斬り',
      CardId: Kesagiri, Aim: CardAim.Single, PreviewText: '9',
      After: Snapshot(50, 0, 8, RangeSide.Near, 51, HandWithout(Kesagiri), attackNear),
    });
    kesagiri.Cues.push(new Cue({ Kind: CueKind.StaminaChange, Target: UnitSide.Player, Amount: -1, StaminaAfter: 8, StaminaMax: 10 }));
    kesagiri.Cues.push(new Cue({ Kind: CueKind.TraitFire, Target: UnitSide.Player, Text: '初手 +3' }));
    kesagiri.Cues.push(new Cue({ Kind: CueKind.Slash, Source: UnitSide.Player, Target: UnitSide.Enemy, Amount: 9, Intensity: EffectStrength.Uniform, HpAfter: 51 }));
    script.Events.push(kesagiri);

    // 3. 大上段 (cost 2): 13 damage, enemy 51 -> 38, stamina 8 -> 6.
    const daijodan = new DepictionEvent({
      Order: 3, Kind: DepictionEventKind.PlayCard, Title: '大上段',
      CardId: Daijodan, Aim: CardAim.Single, PreviewText: '13',
      After: Snapshot(50, 0, 6, RangeSide.Near, 38, HandWithout(Kesagiri, Daijodan), attackNear),
    });
    daijodan.Cues.push(new Cue({ Kind: CueKind.StaminaChange, Target: UnitSide.Player, Amount: -2, StaminaAfter: 6, StaminaMax: 10 }));
    daijodan.Cues.push(new Cue({ Kind: CueKind.Slash, Source: UnitSide.Player, Target: UnitSide.Enemy, Amount: 13, Intensity: EffectStrength.Uniform, HpAfter: 38 }));
    script.Events.push(daijodan);

    // 4. 後ろ跳び (cost 1): switch to far, Guard 4, trait 予兆(攻撃) Guard +3 fires -> Guard 7, stamina 6 -> 5.
    const ushirotobi = new DepictionEvent({
      Order: 4, Kind: DepictionEventKind.PlayCard, Title: '後ろ跳び',
      CardId: Ushirotobi, Aim: CardAim.Self, PreviewText: '7',
      After: Snapshot(50, 7, 5, RangeSide.Far, 38, HandWithout(Kesagiri, Daijodan, Ushirotobi), attackNear),
    });
    ushirotobi.Cues.push(new Cue({ Kind: CueKind.StaminaChange, Target: UnitSide.Player, Amount: -1, StaminaAfter: 5, StaminaMax: 10 }));
    ushirotobi.Cues.push(new Cue({ Kind: CueKind.RangeSwitch, Target: UnitSide.Player, RangeAfter: RangeSide.Far, RangeGlyphAfter: GlyphOf(RangeSide.Far) }));
    ushirotobi.Cues.push(new Cue({ Kind: CueKind.GuardGain, Target: UnitSide.Player, Amount: 4, Intensity: EffectStrength.Uniform, GuardAfter: 4 }));
    ushirotobi.Cues.push(new Cue({ Kind: CueKind.TraitFire, Target: UnitSide.Player, Text: '予兆 +3' }));
    ushirotobi.Cues.push(new Cue({ Kind: CueKind.GuardGain, Target: UnitSide.Player, Amount: 3, Intensity: EffectStrength.Uniform, GuardAfter: 7 }));
    script.Events.push(ushirotobi);

    // 5. Turn end: stance (5 left >= 3) Guard +3 -> 10, discard the remaining two.
    const turnEnd = new DepictionEvent({
      Order: 5, Kind: DepictionEventKind.TurnEnd, Title: 'ターン終了',
      After: Snapshot(50, 10, 5, RangeSide.Far, 38, [], attackNear, ''),
    });
    turnEnd.Cues.push(new Cue({ Kind: CueKind.StanceCue, Target: UnitSide.Player, Amount: 3, Text: '+3', GuardAfter: 10 }));
    turnEnd.Cues.push(new Cue({ Kind: CueKind.DiscardHand, Target: UnitSide.Player, Amount: 2 }));
    script.Events.push(turnEnd);

    // 6. Enemy attacks as foretold: 13. The near +5 misses. 13 - 10 = 3, HP 50 -> 47.
    const enemyAction = new DepictionEvent({
      Order: 6, Kind: DepictionEventKind.EnemyAction, Title: '敵の攻撃',
      After: Snapshot(47, 0, 5, RangeSide.Far, 38, [], new OmenFrame({ Visible: false }), ''),
    });
    enemyAction.Cues.push(new Cue({ Kind: CueKind.EnemyWindup, Target: UnitSide.Enemy }));
    enemyAction.Cues.push(new Cue({ Kind: CueKind.SideBonusMiss, Target: UnitSide.Enemy, Amount: 5, Text: '+5' }));
    enemyAction.Cues.push(new Cue({ Kind: CueKind.Slash, Source: UnitSide.Enemy, Target: UnitSide.Player, Amount: 13, Intensity: EffectStrength.Uniform }));
    enemyAction.Cues.push(new Cue({ Kind: CueKind.GuardBlock, Target: UnitSide.Player, Amount: 10, GuardAfter: 0 }));
    enemyAction.Cues.push(new Cue({ Kind: CueKind.Hit, Target: UnitSide.Player, Amount: 3, Intensity: EffectStrength.Uniform, HpAfter: 47 }));
    script.Events.push(enemyAction);

    // 7. Next omen: 防御 9, the enemy Guard of the recorded slice (#349).
    const nextOmen = new DepictionEvent({
      Order: 7, Kind: DepictionEventKind.NextOmen, Title: '次の予兆',
      After: Snapshot(47, 0, 5, RangeSide.Far, 38, [],
        new OmenFrame({ Visible: true, Icon: OmenIcon.Guard, KindLabel: '防御', ValueText: '9' }), ''),
    });
    nextOmen.Cues.push(new Cue({ Kind: CueKind.OmenShow, Target: UnitSide.Enemy }));
    script.Events.push(nextOmen);

    return script;
  },
};
