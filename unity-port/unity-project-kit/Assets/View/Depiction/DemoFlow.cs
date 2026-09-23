// The demo's screens around the battle (#187): the deck screen (#190), the mode screen and the end
// screen (#191), and the battles in between. Built in code on a canvas of its own above the battle,
// so the scene and the prefabs stay as they are; BattleBootstrap adds it when its demo flow is on.
// It holds no rule: the deck, the run of battles, what carries and every word come from
// Depiction.Bridge (DeckBuilder, DemoSession), and the battles are the sources DemoSession starts.
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using BattleCore;
using Depiction.Bridge;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public class DemoFlow : MonoBehaviour
    {
        /// <summary>Where the deck is kept between runs (DeckBuilder.Save's string).</summary>
        public const string SavedDeckKey = "Depiction.Demo.Deck";

        /// <summary>How long the result card stands on its own before the end screen comes up.</summary>
        private const float EndScreenDelaySeconds = 1.2f;

        private DepictionPlayer _player;
        private int _seed;
        private int _randomPicks;
        private RectTransform _canvas;
        private DeckSelectScreen _deckScreen;
        private ModeSelectScreen _modeScreen;
        private EndScreen _endScreen;
        private List<CardInstance> _deck;
        private DemoSession _session;
        private CoreBattleSource _source;

        /// <summary>Sets the flow up and shows the deck screen. <paramref name="seed"/> fixes the run (each battle takes its own from it).</summary>
        public void Begin(DepictionPlayer player, int seed)
        {
            if (_canvas) Destroy(_canvas.gameObject);
            if (_player) _player.BattleFinished -= OnBattleFinished;
            _player = player;
            _seed = seed;
            _player.BattleFinished += OnBattleFinished;
            _canvas = BuildCanvas();
            _deckScreen = new DeckSelectScreen(_canvas, LoadDeck(), SaveDeck, OnDeckChosen);
            _modeScreen = new ModeSelectScreen(_canvas, StartSingle, StartRandom, StartChain, ShowDeckScreen);
            _endScreen = new EndScreen(_canvas, GoOn, Again, ShowDeckScreen);
            ShowDeckScreen();
        }

        private void OnDestroy()
        {
            if (_player) _player.BattleFinished -= OnBattleFinished;
            if (_canvas) Destroy(_canvas.gameObject);
        }

        // ---- the screens ----

        private void ShowDeckScreen()
        {
            StopAllCoroutines();
            _modeScreen.Visible = false;
            _endScreen.Visible = false;
            _deckScreen.Visible = true;
        }

        private void OnDeckChosen(List<CardInstance> deck)
        {
            _deck = deck;
            _deckScreen.Visible = false;
            _modeScreen.Visible = true;
        }

        private void StartSingle(string enemyId)
        {
            Run(() => DemoSession.Single(_deck, enemyId, _seed));
        }

        private void StartRandom()
        {
            Run(() => DemoSession.Single(_deck, DemoSession.RandomEnemyId(_seed + (_randomPicks++ * 104729)), _seed));
        }

        private void StartChain()
        {
            Run(() => DemoSession.Chain(_deck, _seed));
        }

        private void Run(System.Func<DemoSession> make)
        {
            try
            {
                _session = make();
            }
            catch (System.ArgumentException e)
            {
                Debug.LogError("[DemoFlow] " + e.Message);
                return;
            }
            NextBattle();
        }

        private void NextBattle()
        {
            _modeScreen.Visible = false;
            _endScreen.Visible = false;
            _source = _session.StartBattle();
            _player.Restart(_source);
        }

        private void OnBattleFinished()
        {
            if (_session == null || _source == null || _session.Stage != DemoStage.Fighting) return;
            _session.Finish(_source);
            StartCoroutine(ShowEndScreenSoon());
        }

        private IEnumerator ShowEndScreenSoon()
        {
            yield return new WaitForSecondsRealtime(EndScreenDelaySeconds);
            _endScreen.Show(_session.EndScreen());
        }

        private void GoOn(bool rest)
        {
            _session.GoOn(rest);
            NextBattle();
        }

        private void Again()
        {
            _session.Again();
            NextBattle();
        }

        // ---- the saved deck ----

        private static DeckBuilder LoadDeck()
        {
            DeckBuilder saved = DeckBuilder.Load(PlayerPrefs.GetString(SavedDeckKey, ""));
            return saved.Total > 0 ? saved : DeckBuilder.Prototype();
        }

        private static void SaveDeck(DeckBuilder deck)
        {
            PlayerPrefs.SetString(SavedDeckKey, deck.Save());
            PlayerPrefs.Save();
        }

        // ---- building ----

        private RectTransform BuildCanvas()
        {
            var go = new GameObject("DemoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }
    }
}
#endif
