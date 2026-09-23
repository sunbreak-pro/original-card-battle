// The demo's screens around the battle (#187): the deck screen (#190) first, then the battle, then
// back. Built in code on a canvas of its own above the battle, so the scene and the prefabs stay as
// they are; BattleBootstrap adds it when its demo flow is on. It holds no rule: the deck, its check
// and every word come from Depiction.Bridge, and the battle is the source the bootstrap builds.
#if UNITY_2021_2_OR_NEWER
using System;
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

        private DepictionPlayer _player;
        private Func<List<CardInstance>, IDepictionSource> _startBattle;
        private RectTransform _canvas;
        private DeckSelectScreen _deckScreen;
        private GameObject _backToDeck;

        /// <summary>
        /// Sets the flow up and shows the deck screen. <paramref name="startBattle"/> turns a built
        /// deck into the battle source (BattleLaunch with the Inspector's enemy and seed).
        /// </summary>
        public void Begin(DepictionPlayer player, Func<List<CardInstance>, IDepictionSource> startBattle)
        {
            if (_canvas) Destroy(_canvas.gameObject);
            if (_player) _player.BattleFinished -= OnBattleFinished;
            _player = player;
            _startBattle = startBattle;
            _player.BattleFinished += OnBattleFinished;
            _canvas = BuildCanvas();
            _deckScreen = new DeckSelectScreen(_canvas, LoadDeck(), SaveDeck, StartBattle);
            _backToDeck = BuildBackButton();
            ShowDeckScreen();
        }

        private void OnDestroy()
        {
            if (_player) _player.BattleFinished -= OnBattleFinished;
            if (_canvas) Destroy(_canvas.gameObject);
        }

        private void ShowDeckScreen()
        {
            _backToDeck.SetActive(false);
            _deckScreen.Visible = true;
        }

        private void StartBattle(List<CardInstance> deck)
        {
            IDepictionSource source;
            try
            {
                source = _startBattle(deck);
            }
            catch (ArgumentException e)
            {
                Debug.LogError("[DemoFlow] " + e.Message);
                return;
            }
            _deckScreen.Visible = false;
            _backToDeck.SetActive(false);
            _player.Restart(source);
        }

        private void OnBattleFinished()
        {
            _backToDeck.SetActive(true);
        }

        // ---- the saved deck ----

        private static DeckBuilder LoadDeck()
        {
            return DeckBuilder.LoadOrPrototype(PlayerPrefs.HasKey(SavedDeckKey), PlayerPrefs.GetString(SavedDeckKey, ""));
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
            // Top level, never nested under the battle's canvas: a nested canvas would ignore its own
            // render mode, sorting and scaler. OnDestroy takes it away with the flow.
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        private GameObject BuildBackButton()
        {
            RectTransform box = UiKit.Point(_canvas, "BackToDeck", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(420f, 72f), new Vector2(0f, 40f));
            UiKit.Button(box, "Button", "デッキ選択へ戻る", ShowDeckScreen, BattleTheme.Accent, BattleTheme.InkBlack, 28, Vector2.zero, Vector2.one);
            return box.gameObject;
        }
    }
}
#endif
