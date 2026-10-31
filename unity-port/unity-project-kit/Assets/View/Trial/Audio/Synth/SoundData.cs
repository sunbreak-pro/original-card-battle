// The data of the trial's sound, byte for byte from the JS: the design system's 16 timbres and 29 battle presets
// (vendor/ds/bundle.js:666-701), the meta synth's 24 labelled timbres, 4 groups and 27 presets, the 8 footstep surfaces,
// the 10 ambience places and the settings defaults (scenes/audio/Audio.js:21-121), and the app's own settings defaults
// (src/app/save.js:10-17). Labels are UI text (Japanese, as the JS writes them).
using System;
using System.Collections.Generic;

namespace Trial.Audio.Synth
{
    /// <summary>One named preset: <c>{ label, group, spec, role, timbre, vol, pitch (Hz), dur (ms), tone (0..1), gap (ms) }</c>.</summary>
    public sealed class SoundPreset
    {
        public string Label, Group, Spec, Role, Timbre;
        public double Vol, Pitch, Dur, Tone;
        /// <summary>The shortest time in ms between two plays of the same event (meta presets only; null = none).</summary>
        public double? Gap;

        public SoundPreset Clone() => (SoundPreset)MemberwiseClone();
    }

    /// <summary>
    /// The ground under the explorer's feet (the <c>step</c> timbre). An absent extra is 0. Read-only: the instances live in a
    /// static table that outlives a play session (domain reload is off in the Unity project).
    /// </summary>
    public sealed class Surface
    {
        public readonly string Label;
        public readonly double Thud, Heel;
        public readonly BiquadType Type;
        public readonly double F, Q, Scuff;
        public readonly double Crackle, Wet, Ring;

        public Surface(string label, double thud, double heel, BiquadType type, double f, double q, double scuff, double crackle = 0, double wet = 0, double ring = 0)
        {
            Label = label; Thud = thud; Heel = heel; Type = type; F = f; Q = q; Scuff = scuff; Crackle = crackle; Wet = wet; Ring = ring;
        }
    }

    /// <summary>A place with an ambience: its peak gain, the send into the cave and the footstep surface (read-only, as <see cref="Surface"/>).</summary>
    public sealed class AmbienceDef
    {
        public readonly string Label, Place, Surface;
        public readonly double Level, Room;
        public readonly string Desc;

        public AmbienceDef(string label, string place, string surface, double level, double room, string desc)
        {
            Label = label; Place = place; Surface = surface; Level = level; Room = room; Desc = desc;
        }
    }

    /// <summary>The persisted sound settings, all 0..1 except <see cref="Muted"/>.</summary>
    public sealed class SoundSettings
    {
        public double Master, Effects, Ambience, Reverb;
        public bool Muted;

        public SoundSettings Clone() => (SoundSettings)MemberwiseClone();

        public override bool Equals(object obj) =>
            obj is SoundSettings s && s.Master == Master && s.Effects == Effects && s.Ambience == Ambience && s.Reverb == Reverb && s.Muted == Muted;

        public override int GetHashCode() => (Master, Effects, Ambience, Reverb, Muted).GetHashCode();

        public override string ToString() => $"{{ master {Master}, effects {Effects}, ambience {Ambience}, reverb {Reverb}, muted {Muted} }}";
    }

    public static class SoundData
    {
        // ---- the design system (vendor/ds/bundle.js) --------------------------------------------------------------

        /// <summary>js: vendor/ds/bundle.js:666 TIMBRES (key order kept).</summary>
        public static readonly string[] DsTimbreNames =
        {
            "paper", "slide", "wood", "thud", "swish", "slash", "thrust", "blunt", "metal", "seal", "string", "chime", "breath", "body", "heart", "crack",
        };

        public static readonly IReadOnlyDictionary<string, string> DsTimbres = new Dictionary<string, string>
        {
            ["paper"] = "紙（擦れ）", ["slide"] = "紙の束（滑り）", ["wood"] = "木の小さな音", ["thud"] = "鈍い音", ["swish"] = "風切り",
            ["slash"] = "斬撃", ["thrust"] = "突き", ["blunt"] = "打撃", ["metal"] = "金属", ["seal"] = "印（判）", ["string"] = "低い弦",
            ["chime"] = "小さな灯", ["breath"] = "息を吸う", ["body"] = "体に当たる", ["heart"] = "鼓動", ["crack"] = "盾が割れる",
        };

        private static SoundPreset P(string label, string spec, string role, string timbre, double vol, double pitch, double dur, double tone, string group = null, double? gap = null) =>
            new SoundPreset { Label = label, Group = group, Spec = spec, Role = role, Timbre = timbre, Vol = vol, Pitch = pitch, Dur = dur, Tone = tone, Gap = gap };

        /// <summary>js: vendor/ds/bundle.js:671 SOUND_DEFAULTS: the 29 battle presets, in their order.</summary>
        public static readonly string[] DsSoundNames =
        {
            "hover", "grab", "frameIn", "frameOut", "noReach", "releaseAttack", "hitSlash", "hitThrust", "hitBlunt", "releaseDefense",
            "guardUp", "releaseSkill", "seal", "releaseStance", "stance", "cancel", "refusal", "draw", "discard", "rebuild",
            "pip", "breath", "reserve", "foeTurn", "omenDrop", "hitGuard", "guardBreak", "hitBody", "heartbeat",
        };

        private static readonly Dictionary<string, SoundPreset> DsDefaults = new Dictionary<string, SoundPreset>
        {
            ["hover"] = P("札にホバーして持ち上がる", "指定なし", "—", "paper", 0.05, 3400, 26, 0.5),
            ["grab"] = P("札を掴む", "指定なし", "—", "paper", 0.16, 1500, 80, 0.4),
            ["frameIn"] = P("対象の枠に入る", "指定なし", "—", "wood", 0.11, 980, 42, 0.5),
            ["frameOut"] = P("対象の枠から出る", "指定なし", "—", "wood", 0.06, 560, 40, 0.3),
            ["noReach"] = P("届かない相手の上", "指定なし", "—", "thud", 0.07, 150, 70, 0.2),
            ["releaseAttack"] = P("攻撃の札を離す", "指定なし", "動作", "swish", 0.18, 900, 150, 0.5),
            ["hitSlash"] = P("命中・斬撃", "系統の音", "結果", "slash", 0.30, 2600, 190, 0.6),
            ["hitThrust"] = P("命中・突き", "系統の音", "結果", "thrust", 0.30, 1900, 140, 0.6),
            ["hitBlunt"] = P("命中・打撃", "系統の音", "結果", "blunt", 0.32, 120, 170, 0.4),
            ["releaseDefense"] = P("防御の札を離す", "指定なし", "動作", "paper", 0.14, 1100, 90, 0.3),
            ["guardUp"] = P("盾の輪が立つ", "金属（小）", "結果", "metal", 0.16, 1240, 340, 0.45),
            ["releaseSkill"] = P("スキルの札を離す", "指定なし", "動作", "paper", 0.14, 1300, 90, 0.4),
            ["seal"] = P("状態が付く（印）", "印", "結果", "seal", 0.22, 150, 140, 0.5),
            ["releaseStance"] = P("スタンスの札を離す", "指定なし", "動作", "paper", 0.12, 900, 110, 0.3),
            ["stance"] = P("型が立つ", "低い弦（短）", "結果", "string", 0.20, 98, 700, 0.4),
            ["cancel"] = P("取り消して手札に戻る", "指定なし", "—", "slide", 0.10, 700, 120, 0.2),
            ["refusal"] = P("出せない（理由の行）", "指定なし", "—", "wood", 0.09, 300, 34, 0.2),
            ["draw"] = P("ドロー（1 枚ごと）", "紙 × 枚数", "—", "paper", 0.07, 2200, 60, 0.5),
            ["discard"] = P("全捨て（1 枚ごと）", "紙", "—", "paper", 0.06, 1600, 55, 0.4),
            ["rebuild"] = P("山札の作り直し", "指定なし", "—", "slide", 0.12, 900, 220, 0.4),
            ["pip"] = P("回復の粒が点く", "小さな灯", "—", "chime", 0.06, 1180, 260, 0.3),
            ["breath"] = P("構え・息を吸う", "息を吸う", "動作", "breath", 0.12, 900, 360, 0.4),
            ["reserve"] = P("構え・盾", "金属（小）", "結果", "metal", 0.14, 1240, 300, 0.4),
            ["foeTurn"] = P("敵の番の始まり", "低い弦", "—", "string", 0.18, 73, 900, 0.3),
            ["omenDrop"] = P("次の予兆が降りる", "低い弦", "—", "string", 0.14, 73, 700, 0.3),
            ["hitGuard"] = P("被弾・Guard が受ける", "金属", "結果", "metal", 0.24, 620, 420, 0.6),
            ["guardBreak"] = P("被弾・盾が割れる", "金属", "結果", "crack", 0.24, 700, 300, 0.6),
            ["hitBody"] = P("被弾・HP が減る", "指定なし", "結果", "body", 0.30, 90, 220, 0.4),
            ["heartbeat"] = P("HP 15 未満の鼓動", "低い鼓動", "—", "heart", 0.16, 55, 160, 0.3),
        };

        /// <summary>A fresh copy of the battle presets (the JS <c>JSON.parse(JSON.stringify(SOUND_DEFAULTS))</c>).</summary>
        public static Dictionary<string, SoundPreset> DsSoundDefaults() => ClonePresets(DsDefaults);

        // ---- the meta synth (scenes/audio/Audio.js) ---------------------------------------------------------------

        /// <summary>js: scenes/audio/Audio.js:21 META_TIMBRES (key order kept).</summary>
        public static readonly string[] MetaTimbreNames =
        {
            "paper", "slide", "wood", "seal", "step", "page", "close", "quill", "exhale", "drill", "settle", "ember",
            "pouch", "swell", "petrify", "toll", "hum", "fall", "tuck", "expire", "relief", "kindle", "nameSeal", "bow",
        };

        public static readonly IReadOnlyDictionary<string, string> MetaTimbres = new Dictionary<string, string>
        {
            ["paper"] = "紙（擦れ）", ["slide"] = "紙の束（滑り）", ["wood"] = "木の小さな音", ["seal"] = "印（判）",
            ["step"] = "足音（層ごとの地面）", ["page"] = "頁をめくる", ["close"] = "本を閉じる", ["quill"] = "筆の掠れ",
            ["exhale"] = "息を吐く", ["drill"] = "素振りと打ち込み", ["settle"] = "置いて収まる", ["ember"] = "灯の温み",
            ["pouch"] = "袋を開けて使う", ["swell"] = "低いうねり", ["petrify"] = "体が固まる", ["toll"] = "湿った鐘",
            ["hum"] = "低い和音", ["fall"] = "落ちる低い弦", ["tuck"] = "札を差し込む", ["expire"] = "最後の鼓動",
            ["relief"] = "安堵の息と灯", ["kindle"] = "灯をともす", ["nameSeal"] = "名を押す（判と弦）", ["bow"] = "擦った低い弦",
        };

        /// <summary>js: scenes/audio/Audio.js:49 META_GROUPS, in display order.</summary>
        public static readonly string[] MetaGroupNames = { "explore", "reward", "life", "ui" };

        public static readonly IReadOnlyDictionary<string, string> MetaGroups = new Dictionary<string, string>
        {
            ["explore"] = "探索", ["reward"] = "報酬", ["life"] = "生と継承", ["ui"] = "操作",
        };

        /// <summary>js: scenes/audio/Audio.js:55 META_SOUND_DEFAULTS: the 27 meta presets, in their order.</summary>
        public static readonly string[] MetaSoundNames =
        {
            "nodeStep", "nodeReturn", "nodeHover", "journalOpen", "journalClose", "rest", "train", "infoGained", "eventResolve",
            "traceTake", "consumableUse", "miasmaLine", "miasmaDeath", "layerCleared", "interlude", "descend",
            "rewardReveal", "rewardPick", "cardAdded",
            "lifeEndDeath", "survived", "hallArrive", "heirNamed", "titleEnter",
            "uiClick", "uiConfirm", "uiCancel",
        };

        private static readonly Dictionary<string, SoundPreset> MetaDefaults = new Dictionary<string, SoundPreset>
        {
            ["nodeStep"] = P("新しいノードへ進む", "指定なし", "動作", "step", 0.22, 110, 260, 0.5, "explore"),
            ["nodeReturn"] = P("踏んだノードへ戻る", "指定なし", "動作", "step", 0.11, 120, 180, 0.3, "explore"),
            ["nodeHover"] = P("ノードにホバー", "指定なし", "—", "paper", 0.035, 2600, 22, 0.4, "explore", 60),
            ["journalOpen"] = P("手記を開く", "指定なし", "—", "page", 0.12, 1500, 280, 0.5, "explore"),
            ["journalClose"] = P("手記を閉じる", "指定なし", "—", "close", 0.10, 1000, 200, 0.3, "explore"),
            ["rest"] = P("休む", "指定なし", "結果", "exhale", 0.15, 760, 1200, 0.3, "explore"),
            ["train"] = P("訓練する", "指定なし", "動作＋結果", "drill", 0.16, 900, 170, 0.5, "explore"),
            ["infoGained"] = P("情報を得る", "指定なし", "結果", "quill", 0.13, 2300, 460, 0.4, "explore"),
            ["eventResolve"] = P("イベントが決まる", "指定なし", "結果", "settle", 0.12, 210, 520, 0.4, "explore"),
            ["traceTake"] = P("痕跡を拾う（遺産）", "灯芯の 1 点", "結果", "ember", 0.085, 523.25, 1700, 0.4, "explore"),
            ["consumableUse"] = P("消耗品を使う", "指定なし", "動作＋結果", "pouch", 0.12, 1200, 260, 0.4, "explore"),
            ["miasmaLine"] = P("瘴気が 20% の線を越える", "低いうねり", "結果", "swell", 0.11, 55, 2400, 0.3, "explore"),
            ["miasmaDeath"] = P("瘴気が 100% に達する", "指定なし", "結果", "petrify", 0.14, 49, 3400, 0.3, "explore"),
            ["layerCleared"] = P("層の主を倒した", "ファンファーレなし", "結果", "toll", 0.12, 98, 3600, 0.3, "explore"),
            ["interlude"] = P("階層間の休憩に入る", "指定なし", "—", "hum", 0.05, 110, 2600, 0.3, "explore"),
            ["descend"] = P("次の層へ降りる", "低い弦（落ちる）", "動作", "fall", 0.12, 98, 2400, 0.35, "explore"),
            ["rewardReveal"] = P("報酬が並ぶ", "指定なし", "—", "slide", 0.11, 1000, 260, 0.4, "reward"),
            ["rewardPick"] = P("報酬を選ぶ", "指定なし", "動作", "paper", 0.15, 1250, 90, 0.4, "reward"),
            ["cardAdded"] = P("デッキに札が入る", "指定なし", "結果", "tuck", 0.12, 1100, 220, 0.4, "reward"),
            ["lifeEndDeath"] = P("倒れて生が終わる", "指定なし", "—", "expire", 0.15, 55, 4400, 0.25, "life"),
            ["survived"] = P("生きたまま生を閉じる", "指定なし", "—", "relief", 0.12, 440, 2200, 0.35, "life"),
            ["hallArrive"] = P("継承の間に着く", "琥珀の灯り", "—", "kindle", 0.12, 2400, 1500, 0.4, "life"),
            ["heirNamed"] = P("襲名", "指定なし", "動作＋結果", "nameSeal", 0.18, 98, 1500, 0.4, "life"),
            ["titleEnter"] = P("タイトルから入る", "指定なし", "—", "bow", 0.075, 73.42, 2800, 0.3, "life"),
            ["uiClick"] = P("ボタンを押す", "指定なし", "—", "wood", 0.07, 760, 30, 0.4, "ui", 40),
            ["uiConfirm"] = P("決める", "指定なし", "—", "seal", 0.10, 170, 110, 0.4, "ui"),
            ["uiCancel"] = P("やめる・戻る", "指定なし", "—", "slide", 0.08, 620, 110, 0.2, "ui"),
        };

        /// <summary>A fresh copy of the meta presets.</summary>
        public static Dictionary<string, SoundPreset> MetaSoundDefaults() => ClonePresets(MetaDefaults);

        /// <summary>The default preset of a battle or meta name, or null.</summary>
        public static SoundPreset DefaultPreset(string name)
        {
            if (name == null) return null;
            if (DsDefaults.TryGetValue(name, out SoundPreset p) || MetaDefaults.TryGetValue(name, out p)) return p.Clone();
            return null;
        }

        public static bool IsBattleName(string name) => name != null && DsDefaults.ContainsKey(name);
        public static bool IsMetaName(string name) => name != null && MetaDefaults.ContainsKey(name);

        /// <summary>js: scenes/audio/Audio.js:190 clonePresets.</summary>
        public static Dictionary<string, SoundPreset> ClonePresets(IReadOnlyDictionary<string, SoundPreset> src)
        {
            var o = new Dictionary<string, SoundPreset>(src.Count);
            foreach (KeyValuePair<string, SoundPreset> kv in src) o[kv.Key] = kv.Value.Clone();
            return o;
        }

        /// <summary>js: scenes/audio/Audio.js:86 SURFACES (key order kept).</summary>
        public static readonly string[] SurfaceNames = { "stone", "litter", "sand", "damp", "glass", "salt", "gravel", "void" };

        public static readonly IReadOnlyDictionary<string, Surface> Surfaces = new Dictionary<string, Surface>
        {
            ["stone"] = new Surface("石", thud: 1.0, heel: 0.8, BiquadType.Bandpass, f: 900, q: 0.8, scuff: 0.45),
            ["litter"] = new Surface("落ち葉", thud: 0.85, heel: 0.6, BiquadType.Bandpass, f: 2600, q: 0.6, scuff: 0.55, crackle: 0.45),
            ["sand"] = new Surface("石畳の砂", thud: 1.05, heel: 0.7, BiquadType.Highpass, f: 2800, q: 0.7, scuff: 0.5),
            ["damp"] = new Surface("湿った岩", thud: 0.9, heel: 0.9, BiquadType.Lowpass, f: 650, q: 1.2, scuff: 0.5, wet: 0.4),
            ["glass"] = new Surface("結晶の床", thud: 1.25, heel: 0.7, BiquadType.Bandpass, f: 1500, q: 1.0, scuff: 0.3, ring: 0.16),
            ["salt"] = new Surface("塩の殻", thud: 1.0, heel: 0.7, BiquadType.Bandpass, f: 1900, q: 1.3, scuff: 0.5, crackle: 0.65),
            ["gravel"] = new Surface("砂利", thud: 0.95, heel: 0.8, BiquadType.Bandpass, f: 1300, q: 0.9, scuff: 0.6, crackle: 0.35),
            ["void"] = new Surface("床が無い", thud: 0.65, heel: 0.6, BiquadType.Lowpass, f: 320, q: 0.7, scuff: 0.3),
        };

        /// <summary>js: scenes/audio/Audio.js:113 AMBIENCE_IDS.</summary>
        public static readonly string[] AmbienceIds = { "title", "hall", "interlude", "layer1", "layer2", "layer3", "layer4", "layer5", "layer6", "layer7" };

        /// <summary>js: scenes/audio/Audio.js:101 AMBIENCE_DEFS.</summary>
        public static readonly IReadOnlyDictionary<string, AmbienceDef> AmbienceDefs = new Dictionary<string, AmbienceDef>
        {
            ["title"] = new AmbienceDef("タイトル", "住処の口", "stone", level: 0.5, room: 0.5, "住処の口へ吸い込まれる風、低い D の響き、まれに遠い弦"),
            ["hall"] = new AmbienceDef("継承の間", "継承の間", "stone", level: 0.7, room: 0.3, "動かない部屋の空気、灯の炎の息、温かい五度、まれに梁の軋み"),
            ["interlude"] = new AmbienceDef("階層間の休憩", "階層間", "stone", level: 0.64, room: 0.45, "ゆっくり呼吸する空気、低い A と E、まれに柔らかい音"),
            ["layer1"] = new AmbienceDef("第 1 層", "燦光の樹海", "litter", level: 1.0, room: 0.25, "天井の大穴から入る明るい空気、風に揺れる葉、小枝、光の粒"),
            ["layer2"] = new AmbienceDef("第 2 層", "■■の秘跡", "sand", level: 0.87, room: 0.55, "柱列を抜ける風の共鳴、流れる砂、石の低い唸り、芽を出す結晶"),
            ["layer3"] = new AmbienceDef("第 3 層", "大黒蛇の住処", "damp", level: 0.5, room: 0.7, "大蛇の呼吸に合わせて膨らむ低い空気、湿った雫、とぐろが壁を擦る"),
            ["layer4"] = new AmbienceDef("第 4 層", "輝晶の間", "glass", level: 1.2, room: 0.6, "結晶のかすかな唸りの重なり、光の薄い擦れ、ゆっくり膨らむ結晶の音"),
            ["layer5"] = new AmbienceDef("第 5 層", "血染めの死海", "salt", level: 0.81, room: 0.3, "干上がった海床の乾いた風、肋骨を抜ける笛のような風、塩の爆ぜ、遠い唸り"),
            ["layer6"] = new AmbienceDef("第 6 層", "獄雷峡", "gravel", level: 0.86, room: 0.5, "谷を下る風、崖を走る獄雷の爆ぜと遠い雷鳴、黒く低い響き"),
            ["layer7"] = new AmbienceDef("第 7 層", "果てなき世界", "void", level: 0.57, room: 0.8, "空洞の圧、うなりを持つ低い二音、湧き口の上下、沈んでいく瘴気の息"),
        };

        /// <summary>js: scenes/audio/Audio.js:115 SOUND_SETTINGS_DEFAULTS (the module's; the game starts from <see cref="GameSoundSettingsDefaults"/>).</summary>
        public static SoundSettings SoundSettingsDefaults() =>
            new SoundSettings { Master = 0.8, Effects = 1.0, Ambience = 0.6, Reverb = 0.18, Muted = false };

        /// <summary>
        /// js: src/app/save.js:10 DEFAULT_SETTINGS (the sound part): master 0.8, effects 0.9, ambience 0.6, not muted. Reverb is never
        /// persisted by the app, so it stays at the module default 0.18.
        /// </summary>
        public static SoundSettings GameSoundSettingsDefaults() =>
            new SoundSettings { Master = 0.8, Effects = 0.9, Ambience = 0.6, Reverb = 0.18, Muted = false };

        /// <summary>js: scenes/audio/Audio.js:117 MIASMA_LINE_PITCH: 20 / 40 / 60 / 80 %, a whole tone lower per line.</summary>
        public static readonly double[] MiasmaLinePitch = { 1, 0.891, 0.794, 0.707 };

        /// <summary>js: scenes/audio/Audio.js:118 RESULT_AFTER_STEP_MS.</summary>
        public const double ResultAfterStepMs = 280;
        /// <summary>js: scenes/audio/Audio.js:119 LOOKAHEAD_S.</summary>
        public const double LookaheadS = 1.5;
        /// <summary>js: scenes/audio/Audio.js:120 TICK_MS.</summary>
        public const double TickMs = 250;
        /// <summary>js: src/app/main.js:423 BATTLE_DUCK.</summary>
        public const double BattleDuck = 0.4;
        /// <summary>The fade <c>unlock()</c> starts a pending ambience with (Audio.js:780).</summary>
        public const double UnlockFadeMs = 2500;
        /// <summary>The default fade of <c>setAmbience</c> (Audio.js:742).</summary>
        public const double DefaultAmbienceFadeMs = 1800;
        /// <summary>The default fade of <c>duckAmbience</c> (Audio.js:813).</summary>
        public const double DefaultDuckFadeMs = 600;
        /// <summary>The design system's hover throttle (bundle.js:830), in seconds of context time.</summary>
        public const double DsHoverThrottleS = 0.05;
        /// <summary>The length of the damp room (both the battle and the meta one), seconds.</summary>
        public const double RoomSeconds = 1.4;
    }
}
