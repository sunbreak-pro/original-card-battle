// Records a card drag frame by frame as JSON lines (#643), so Claude can read the follow curve, the
// moment the receiver lights and the return after the release as numbers. A uloop call takes 0.3〜1.5 s
// while the Editor runs at about 500 fps, so reading the scene from outside catches 2 or 3 points of a
// drag; this component reads it every frame instead.
//
// It only reads the scene: DepictionPlayer and CardView are not changed (#585, #589, #606, #612, #619
// touch them). The held card comes from DepictionPlayer's private _dragging field when it is there, else
// from the drag events; the pointer from the drag events; the lit receiver from the two target marks
// (alpha 1 = hot, TargetMarkView.SetHot); the lit cells from the floor's Cell<n>/ReachFill images; the
// pay from the stamina tag's "▸ n" label. DragTrace (Script/) formats the line.
//
// Start and stop it from `uloop execute-dynamic-code`:
//   Depiction.View.DragTraceRecorder.Begin();          // returns the file it will write
//   Depiction.View.DragTraceRecorder.End();            // writes the file, returns its path and line count
// The file goes to the Unity project's Logs/ (drag-trace-<time>.jsonl), which git does not track.
#if UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Depiction.View
{
    [DefaultExecutionOrder(10000)] // after DepictionPlayer and the tweens have moved the card this frame
    public sealed class DragTraceRecorder : MonoBehaviour
    {
        private static readonly FieldInfo DraggingField =
            typeof(DepictionPlayer).GetField("_dragging", BindingFlags.Instance | BindingFlags.NonPublic);

        private static DragTraceRecorder _instance;

        private readonly List<string> _lines = new List<string>();
        private readonly HashSet<CardView> _watched = new HashSet<CardView>();
        private readonly List<int> _reach = new List<int>();
        private string _path;
        private bool _recording;
        private DepictionPlayer _player;
        private CardView _traced;
        private CardView _eventCard;
        private bool _eventHeld;
        private Vector2? _pointer;
        private Text _pay;
        private RectTransform _floor;

        /// <summary>True between Begin and End.</summary>
        public static bool IsRecording => _instance && _instance._recording;

        // Called by Unity after the first scene load: the recorder waits, idle, until Begin.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Ensure();
        }

        private static DragTraceRecorder Ensure()
        {
            if (_instance) return _instance;
            var go = new GameObject("DragTraceRecorder") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DragTraceRecorder>();
            return _instance;
        }

        /// <summary>Starts a new trace (a running one is dropped) and returns the file End will write.</summary>
        public static string Begin()
        {
            if (!Application.isPlaying) return "not playing: enter Play mode first";
            DragTraceRecorder recorder = Ensure();
            recorder.Reset();
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            recorder._path = Path.Combine(folder, "drag-trace-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl");
            recorder._recording = true;
            return recorder._path;
        }

        /// <summary>Stops the trace, writes it one JSON object per line and returns "path (n lines)".</summary>
        public static string End()
        {
            if (!_instance || !_instance._recording) return "not recording";
            DragTraceRecorder recorder = _instance;
            recorder._recording = false;
            Directory.CreateDirectory(Path.GetDirectoryName(recorder._path));
            var sb = new StringBuilder();
            foreach (string line in recorder._lines) sb.Append(line).Append('\n');
            File.WriteAllText(recorder._path, sb.ToString(), new UTF8Encoding(false));
            string result = recorder._path + " (" + recorder._lines.Count + " lines)";
            recorder.Reset();
            return result;
        }

        private void Reset()
        {
            foreach (CardView card in _watched)
            {
                if (!card) continue;
                card.DragBegan -= OnDragBegan;
                card.DragMoved -= OnDragMoved;
                card.DragEnded -= OnDragEnded;
            }
            _watched.Clear();
            _lines.Clear();
            _recording = false;
            _player = null;
            _traced = null;
            _eventCard = null;
            _eventHeld = false;
            _pointer = null;
            _pay = null;
            _floor = null;
        }

        private void OnDestroy()
        {
            Reset();
            if (_instance == this) _instance = null;
        }

        private void LateUpdate()
        {
            if (!_recording) return;
            if (!_player) _player = FindAnyObjectByType<DepictionPlayer>();
            WatchCards();

            CardView dragging = Dragging();
            if (dragging) _traced = dragging;
            // With DepictionPlayer's field, a drag it refused (busy, another card held) is not a hold.
            bool held = _player && DraggingField != null ? (bool)dragging : _eventHeld;

            var frame = new DragTraceFrame
            {
                Frame = Time.frameCount,
                Time = Time.unscaledTimeAsDouble,
                PointerX = _pointer?.x,
                PointerY = _pointer?.y,
                Card = Describe(_traced, held),
                Receiver = LitReceiver(),
                ReachCells = LitCells(),
                StaminaPay = Pay(),
            };
            _lines.Add(frame.ToJson());
        }

        // ---- the held card ------------------------------------------------------------------

        // Cards are dealt every turn, so new ones are looked for each frame. Only the drag events are
        // listened to; DepictionPlayer's own handlers ran first (they subscribed when the card was made).
        private void WatchCards()
        {
            RectTransform root = _player ? RefScreen.Root(_player.handArea ? _player.handArea : (Transform)_player.transform) : null;
            if (!root) return;
            foreach (CardView card in root.GetComponentsInChildren<CardView>(true))
            {
                if (!_watched.Add(card)) continue;
                card.DragBegan += OnDragBegan;
                card.DragMoved += OnDragMoved;
                card.DragEnded += OnDragEnded;
            }
        }

        private void OnDragBegan(CardView card, PointerEventData e)
        {
            _pointer = e.position;
            _eventCard = card;
            _eventHeld = true;
            CardView dragging = Dragging();
            _traced = dragging ? dragging : card;
        }

        private void OnDragMoved(CardView card, PointerEventData e)
        {
            _pointer = e.position;
        }

        private void OnDragEnded(CardView card, PointerEventData e)
        {
            _pointer = e.position;
            _eventHeld = false;
        }

        // DepictionPlayer may hand the drag to the hovered neighbour (OnDragBegan), so its own field is
        // the truth when it has one; the event's card is the fallback.
        private CardView Dragging()
        {
            if (_player && DraggingField != null) return DraggingField.GetValue(_player) as CardView;
            return _eventHeld ? _eventCard : null;
        }

        private static DragTraceCard Describe(CardView card, bool held)
        {
            if (!card) return null;
            RectTransform rt = card.Rect;
            Vector3 rot = rt.localEulerAngles;
            Vector3 scale = rt.localScale;
            return new DragTraceCard
            {
                Id = card.CardId,
                Held = held,
                X = rt.anchoredPosition.x,
                Y = rt.anchoredPosition.y,
                RotX = rot.x,
                RotY = rot.y,
                RotZ = rot.z,
                ScaleX = scale.x,
                ScaleY = scale.y,
                ScaleZ = scale.z,
                Alpha = card.group ? card.group.alpha : 1f,
            };
        }

        // ---- what lights ---------------------------------------------------------------------

        private DragReceiver LitReceiver()
        {
            if (!_player) return DragReceiver.None;
            if (Hot(_player.enemyFigure)) return DragReceiver.Enemy;
            if (Hot(_player.playerFigure)) return DragReceiver.Self;
            return DragReceiver.None;
        }

        // TargetMarkView.SetHot: 1 while releasing would play the card, 0.75 shown but cold, 0 hidden.
        private static bool Hot(FigureView figure)
        {
            return figure && figure.targetMark && figure.targetMark.group && figure.targetMark.group.alpha >= 0.999f;
        }

        private List<int> LitCells()
        {
            _reach.Clear();
            if (!_floor && _player && _player.enemyFigure && _player.enemyFigure.Rect.parent)
                _floor = _player.enemyFigure.Rect.parent.Find("Floor") as RectTransform;
            if (!_floor || !_floor.gameObject.activeInHierarchy) return new List<int>(_reach);
            foreach (Transform cell in _floor)
            {
                int number = DragTrace.CellNumber(cell.name);
                if (number == 0 || !cell.gameObject.activeInHierarchy) continue;
                Transform fill = cell.Find("ReachFill");
                if (fill && fill.TryGetComponent(out Image image) && image.enabled) _reach.Add(number);
            }
            _reach.Sort();
            return new List<int>(_reach);
        }

        private int? Pay()
        {
            if (!_pay && _player)
            {
                RectTransform root = RefScreen.Root(_player.handArea ? _player.handArea : (Transform)_player.transform);
                Transform tag = root ? root.Find("StaminaTag") : null;
                Transform label = tag ? tag.Find("Pay") : null;
                if (label) _pay = label.GetComponent<Text>();
            }
            if (!_pay) return null;
            return DragTrace.ParsePay(_pay.text, _pay.enabled && _pay.gameObject.activeInHierarchy);
        }
    }
}
#endif
