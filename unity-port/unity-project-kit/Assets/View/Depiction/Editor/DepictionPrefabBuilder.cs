// Tools > Depiction > Build Prefabs And Scene
// Writes the first version of the depiction prefabs and the BattleDepiction scene. From
// then on they are edited by hand in the Editor: anything that already exists on disk is
// left alone, so re-running the menu never overwrites hand adjustments. Delete an asset
// to have it regenerated.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Depiction.View
{
    public static class DepictionPrefabBuilder
    {
        public const string PrefabFolder = "Assets/Prefabs/Depiction";
        public const string ScenePath = "Assets/Scenes/BattleDepiction.unity";

        private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);
        private static readonly Color CardBack = BattleTheme.Hex("#15252f");
        private static readonly Color PanelBack = BattleTheme.Hex("#0b151c", 0.88f);

        [MenuItem("Tools/Depiction/Build Prefabs And Scene")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabFolder);
            EnsureFolder("Assets/Scenes");

            int created = 0;
            created += BuildPrefab("Card", BuildCard);
            created += BuildPrefab("Figure", BuildFigure);
            created += BuildPrefab("StatusBar", BuildStatusBar);
            created += BuildPrefab("OmenBadge", BuildOmenBadge);
            created += BuildPrefab("Receiver", BuildReceiver);
            created += BuildPrefab("ThrowLine", BuildThrowLine);
            created += BuildPrefab("CornerInfo", BuildCornerInfo);
            AssetDatabase.SaveAssets();

            bool sceneCreated = false;
            // Building the scene replaces whatever is open, so unsaved edits are offered for saving first;
            // cancelling that dialog skips the scene and keeps the open one.
            if (!File.Exists(ScenePath) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                BuildScene();
                sceneCreated = true;
            }
            AssetDatabase.Refresh();
            Debug.Log("[Depiction] prefabs created: " + created + " (existing ones kept). scene " + (sceneCreated ? "created" : "kept") + ".");
        }

        // ---- prefabs --------------------------------------------------------------------------

        private static int BuildPrefab(string name, System.Func<GameObject> build)
        {
            string path = PrefabFolder + "/" + name + ".prefab";
            if (File.Exists(path)) return 0;
            GameObject go = build();
            go.name = name;
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return 1;
        }

        private static GameObject BuildCard()
        {
            RectTransform root = Root("Card", new Vector2(190f, 260f), Half);
            var view = root.gameObject.AddComponent<CardView>();
            view.group = root.gameObject.AddComponent<CanvasGroup>();

            view.background = Img(root, "Background", CardBack, Vector2.zero, Vector2.one);
            view.background.raycastTarget = true; // the drag handle
            Frame(root, BattleTheme.Line, 2f);

            view.kindStripe = Img(root, "KindStripe", BattleTheme.Omen, new Vector2(0f, 1f), new Vector2(1f, 1f));
            view.kindStripe.rectTransform.offsetMin = new Vector2(0f, -8f);

            RectTransform cost = UiKit.Point(root, "Cost", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, 46f), new Vector2(8f, -16f));
            Image costBack = cost.gameObject.AddComponent<Image>();
            costBack.color = BattleTheme.InkBlack;
            costBack.raycastTarget = false;
            view.costText = UiKit.Label(cost, "CostText", 28, TextAnchor.MiddleCenter, BattleTheme.Warm, Half, Half, new Vector2(46f, 46f), Vector2.zero, "1");
            view.costText.fontStyle = FontStyle.Bold;

            view.nameText = UiKit.Label(root, "Name", 24, TextAnchor.MiddleCenter, BattleTheme.Ink, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(130f, 40f), new Vector2(24f, -20f), "技名");
            view.valueText = UiKit.Label(root, "Value", 72, TextAnchor.MiddleCenter, BattleTheme.Omen, Half, Half,
                new Vector2(180f, 100f), new Vector2(0f, 4f), "6");
            view.valueText.fontStyle = FontStyle.Bold;

            RectTransform trait = UiKit.Point(root, "Trait", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(174f, 40f), new Vector2(0f, 12f));
            view.traitBox = trait.gameObject;
            Image traitBack = trait.gameObject.AddComponent<Image>();
            traitBack.color = BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.55f);
            traitBack.raycastTarget = false;
            RectTransform lamp = UiKit.Point(trait, "Lamp", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 14f), new Vector2(10f, 0f));
            view.traitLamp = lamp.gameObject.AddComponent<Image>();
            view.traitLamp.color = BattleTheme.Warm;
            view.traitLamp.raycastTarget = false;
            view.traitText = UiKit.Label(trait, "TraitText", 22, TextAnchor.MiddleLeft, BattleTheme.Warm, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(140f, 36f), new Vector2(32f, 0f), "初手 +3");
            return root.gameObject;
        }

        private static GameObject BuildFigure()
        {
            RectTransform root = Root("Figure", new Vector2(210f, 450f), new Vector2(0.5f, 0f));
            var view = root.gameObject.AddComponent<FigureView>();

            view.body = Img(root, "Body", BattleTheme.InkBlack, Vector2.zero, Vector2.one);
            view.body.preserveAspect = true;
            view.chest = UiKit.Point(root, "Chest", new Vector2(0.5f, 0f), Half, Vector2.zero, new Vector2(0f, 270f));
            view.head = UiKit.Point(root, "Head", new Vector2(0.5f, 0f), Half, Vector2.zero, new Vector2(0f, 470f));

            view.rangeTag = UiKit.Point(root, "RangeTag", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(52f, 52f), new Vector2(0f, -14f));
            Image tagBack = view.rangeTag.gameObject.AddComponent<Image>();
            tagBack.color = BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.85f);
            tagBack.raycastTarget = false;
            view.rangeTagFrame = Img(view.rangeTag, "Underline", BattleTheme.Accent, new Vector2(0f, 0f), new Vector2(1f, 0f));
            view.rangeTagFrame.rectTransform.offsetMax = new Vector2(0f, 3f);
            view.rangeGlyph = UiKit.Label(view.rangeTag, "Glyph", 32, TextAnchor.MiddleCenter, BattleTheme.Accent, Half, Half, new Vector2(52f, 52f), Vector2.zero, "近");
            view.rangeGlyph.fontStyle = FontStyle.Bold;
            return root.gameObject;
        }

        private static GameObject BuildStatusBar()
        {
            RectTransform root = Root("StatusBar", new Vector2(480f, 96f), Half);
            var view = root.gameObject.AddComponent<StatusBarView>();

            view.guardBadge = UiKit.Point(root, "GuardBadge", new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(60f, 68f), new Vector2(32f, -36f));
            view.guardIcon = view.guardBadge.gameObject.AddComponent<Image>();
            view.guardIcon.color = BattleTheme.Guard;
            view.guardIcon.raycastTarget = false;
            view.guardText = UiKit.Label(view.guardBadge, "GuardText", 30, TextAnchor.MiddleCenter, BattleTheme.Ink, Half, Half, new Vector2(60f, 60f), new Vector2(0f, 4f), "0");
            view.guardText.fontStyle = FontStyle.Bold;
            var guardShadow = view.guardText.gameObject.AddComponent<Shadow>();
            guardShadow.effectColor = BattleTheme.InkBlack;

            RectTransform bar = UiKit.Point(root, "HpBar", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(400f, 34f), new Vector2(76f, -18f));
            Image barBack = bar.gameObject.AddComponent<Image>();
            barBack.color = BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.8f);
            barBack.raycastTarget = false;
            view.hpFillImage = Img(bar, "Fill", BattleTheme.Accent, Vector2.zero, Vector2.one);
            view.hpFill = view.hpFillImage.rectTransform;
            view.hpText = UiKit.Label(bar, "HpText", 28, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120f, 34f), new Vector2(10f, 0f), "50");
            view.hpText.fontStyle = FontStyle.Bold;
            var hpShadow = view.hpText.gameObject.AddComponent<Shadow>();
            hpShadow.effectColor = BattleTheme.InkBlack;

            view.pipRow = UiKit.Point(root, "Stamina", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, 24f), new Vector2(76f, -62f));
            view.pips = new Image[10];
            for (int i = 0; i < view.pips.Length; i++)
            {
                RectTransform pip = UiKit.Point(view.pipRow, "Pip" + i, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 22f), new Vector2(i * 30f, 0f));
                view.pips[i] = pip.gameObject.AddComponent<Image>();
                view.pips[i].color = BattleTheme.Warm;
                view.pips[i].raycastTarget = false;
            }

            RectTransform chipRow = UiKit.Point(root, "Statuses", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(400f, 0f), new Vector2(76f, 0f));
            view.chips = new Text[6];
            for (int i = 0; i < view.chips.Length; i++)
            {
                view.chips[i] = UiKit.Label(chipRow, "Chip" + i, 20, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(64f, 24f), new Vector2(i * 66f, -4f), "");
                view.chips[i].gameObject.SetActive(false);
            }
            return root.gameObject;
        }

        private static GameObject BuildOmenBadge()
        {
            RectTransform root = Root("OmenBadge", new Vector2(270f, 68f), Half);
            var view = root.gameObject.AddComponent<OmenBadgeView>();
            view.group = root.gameObject.AddComponent<CanvasGroup>();
            view.group.blocksRaycasts = false;
            Img(root, "Back", PanelBack, Vector2.zero, Vector2.one);
            Image edge = Img(root, "Edge", BattleTheme.Omen, new Vector2(0f, 0f), new Vector2(0f, 1f));
            edge.rectTransform.offsetMax = new Vector2(4f, 0f);

            view.kindText = UiKit.Label(root, "Kind", 30, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90f, 60f), new Vector2(18f, 0f), "攻撃");

            view.sideChip = UiKit.Point(root, "SideChip", new Vector2(0f, 0.5f), Half, new Vector2(44f, 44f), new Vector2(136f, 0f));
            Image chipBack = view.sideChip.gameObject.AddComponent<Image>();
            chipBack.color = BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.85f);
            chipBack.raycastTarget = false;
            view.sideChipFrame = Img(view.sideChip, "Underline", BattleTheme.Omen, new Vector2(0f, 0f), new Vector2(1f, 0f));
            view.sideChipFrame.rectTransform.offsetMax = new Vector2(0f, 3f);
            view.sideText = UiKit.Label(view.sideChip, "Side", 28, TextAnchor.MiddleCenter, BattleTheme.Omen, Half, Half, new Vector2(44f, 44f), Vector2.zero, "近");
            view.sideText.fontStyle = FontStyle.Bold;
            RectTransform strike = UiKit.Point(view.sideChip, "Strike", Half, Half, new Vector2(56f, 4f), Vector2.zero);
            strike.localRotation = Quaternion.Euler(0f, 0f, -35f);
            view.sideStrike = strike.gameObject.AddComponent<Image>();
            view.sideStrike.color = BattleTheme.Ink;
            view.sideStrike.raycastTarget = false;
            view.sideStrike.enabled = false;

            view.valueText = UiKit.Label(root, "Value", 44, TextAnchor.MiddleRight, BattleTheme.Omen, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(90f, 60f), new Vector2(-16f, 0f), "13");
            view.valueText.fontStyle = FontStyle.Bold;
            return root.gameObject;
        }

        private static GameObject BuildReceiver()
        {
            RectTransform root = Root("Receiver", new Vector2(340f, 340f), Half);
            var view = root.gameObject.AddComponent<ReceiverView>();
            view.group = root.gameObject.AddComponent<CanvasGroup>();
            view.group.blocksRaycasts = false;
            view.group.interactable = false;
            view.ring = Img(root, "Ring", BattleTheme.WithAlpha(BattleTheme.Omen, 0.12f), Vector2.zero, Vector2.one);
            view.dish = Img(root, "Dish", BattleTheme.WithAlpha(BattleTheme.Omen, 0.28f), new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
            view.previewText = UiKit.Label(root, "Preview", 84, TextAnchor.MiddleCenter, BattleTheme.Ink, Half, Half, new Vector2(200f, 120f), new Vector2(-250f, 0f), "9"); // beside the dish: a held card covers its centre
            view.previewText.fontStyle = FontStyle.Bold;
            var shadow = view.previewText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = BattleTheme.InkBlack;
            shadow.effectDistance = new Vector2(3f, -3f);
            return root.gameObject;
        }

        private static GameObject BuildThrowLine()
        {
            RectTransform root = Root("ThrowLine", new Vector2(1000f, 8f), Half);
            var view = root.gameObject.AddComponent<ThrowLineView>();
            view.group = root.gameObject.AddComponent<CanvasGroup>();
            view.group.blocksRaycasts = false;
            view.group.interactable = false;
            const int count = 25;
            view.dashes = new Image[count];
            for (int i = 0; i < count; i++)
            {
                RectTransform dash = UiKit.Point(root, "Dash" + i, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 4f), new Vector2(i * 40f + 8f, 0f));
                view.dashes[i] = dash.gameObject.AddComponent<Image>();
                view.dashes[i].color = BattleTheme.WithAlpha(BattleTheme.Ink, 0.5f);
                view.dashes[i].raycastTarget = false;
            }
            view.previewText = UiKit.Label(root, "Preview", 56, TextAnchor.MiddleCenter, BattleTheme.Ink2, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(200f, 70f), new Vector2(0f, 10f), "7");
            view.previewText.fontStyle = FontStyle.Bold;
            return root.gameObject;
        }

        private static GameObject BuildCornerInfo()
        {
            RectTransform root = Root("CornerInfo", new Vector2(300f, 24f), new Vector2(0f, 1f));
            var view = root.gameObject.AddComponent<CornerInfoView>();
            CornerLabel(root, "TurnIcon", 0f, 18f, BattleTheme.Ink2, "砂");
            view.turnText = CornerLabel(root, "Turn", 20f, 30f, BattleTheme.Ink, "2");
            CornerLabel(root, "FloorIcon", 58f, 18f, BattleTheme.Ink2, "階");
            view.floorText = CornerLabel(root, "Floor", 78f, 30f, BattleTheme.Ink, "1");
            view.chainText = CornerLabel(root, "Chain", 116f, 60f, BattleTheme.Ink, "●○○");
            CornerLabel(root, "MiasmaIcon", 186f, 18f, BattleTheme.Ink2, "霧");
            view.miasmaText = CornerLabel(root, "Miasma", 206f, 60f, BattleTheme.Ink, "0%");
            return root.gameObject;
        }

        private static Text CornerLabel(RectTransform parent, string name, float x, float width, Color color, string text)
        {
            // 14 px, one line (battle_ui_ux_v2 §10.2).
            return UiKit.Label(parent, name, 14, TextAnchor.MiddleLeft, color, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(width, 24f), new Vector2(x, 0f), text);
        }

        // ---- scene ----------------------------------------------------------------------------

        private static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BattleTheme.InkBlack;
            cam.orthographic = true;
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

#if ENABLE_INPUT_SYSTEM
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif

            var canvasGo = new GameObject("DepictionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(BattleTheme.RefWidth, BattleTheme.RefHeight);
            scaler.matchWidthOrHeight = 0.5f;
            var canvas = (RectTransform)canvasGo.transform;

            var player = new GameObject("DepictionPlayer").AddComponent<DepictionPlayer>();
            player.cardPrefab = Load<CardView>("Card");

            player.backdrop = Img(canvas, "Backdrop", BattleTheme.Ground, Vector2.zero, Vector2.one);
            Image floor = Img(canvas, "Floor", BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.55f), new Vector2(0f, 0f), new Vector2(1f, 0.46f));
            floor.name = "Floor";
            Image floorLine = Img(canvas, "FloorLine", BattleTheme.Line, new Vector2(0f, 0.46f), new Vector2(1f, 0.46f));
            floorLine.rectTransform.offsetMax = new Vector2(0f, 2f);

            player.cornerInfo = Place<CornerInfoView>("CornerInfo", canvas, new Vector2(0f, 1f), new Vector2(24f, -16f));

            RectTransform arena = UiKit.Rect(canvas, "Arena", Vector2.zero, Vector2.one);
            const float feetY = -60f;
            player.playerNearSlot = UiKit.Point(arena, "PlayerNearSlot", Half, Half, Vector2.zero, new Vector2(-260f, feetY));
            player.playerFarSlot = UiKit.Point(arena, "PlayerFarSlot", Half, Half, Vector2.zero, new Vector2(-520f, feetY));

            player.playerFigure = Place<FigureView>("Figure", arena, Half, new Vector2(-260f, feetY));
            player.playerFigure.name = "PlayerFigure";
            player.playerFigure.side = UnitSide.Player;
            Overrides(player.playerFigure);

            player.enemyFigure = Place<FigureView>("Figure", arena, Half, new Vector2(400f, feetY));
            player.enemyFigure.name = "EnemyFigure";
            player.enemyFigure.side = UnitSide.Enemy;
            player.enemyFigure.useSpearSilhouette = true;
            player.enemyFigure.body.rectTransform.localScale = new Vector3(-1f, 1f, 1f); // face the player
            Overrides(player.enemyFigure);
            Overrides(player.enemyFigure.body.rectTransform);

            player.omenBadge = Place<OmenBadgeView>("OmenBadge", arena, Half, new Vector2(400f, 468f));
            player.receiver = Place<ReceiverView>("Receiver", arena, Half, new Vector2(400f, 200f));

            player.playerStatus = Place<StatusBarView>("StatusBar", canvas, Half, new Vector2(-690f, -190f));
            player.playerStatus.name = "PlayerStatus";
            player.playerStatus.side = UnitSide.Player;
            Overrides(player.playerStatus);
            player.enemyStatus = Place<StatusBarView>("StatusBar", canvas, Half, new Vector2(690f, -190f));
            player.enemyStatus.name = "EnemyStatus";
            player.enemyStatus.side = UnitSide.Enemy;
            Overrides(player.enemyStatus);

            player.throwLine = Place<ThrowLineView>("ThrowLine", canvas, Half, new Vector2(0f, -250f));

            player.deckPoint = UiKit.Point(canvas, "DeckPoint", Half, Half, Vector2.zero, new Vector2(-840f, -420f));
            player.discardPoint = UiKit.Point(canvas, "DiscardPoint", Half, Half, Vector2.zero, new Vector2(900f, -520f));

            player.endTurn = UiKit.Point(canvas, "EndTurn", Half, Half, new Vector2(200f, 60f), new Vector2(790f, -420f));
            Image endBack = player.endTurn.gameObject.AddComponent<Image>();
            endBack.color = PanelBack;
            endBack.raycastTarget = false;
            Frame(player.endTurn, BattleTheme.Line, 2f);
            UiKit.Label(player.endTurn, "Label", 26, TextAnchor.MiddleCenter, BattleTheme.Ink, Half, Half, new Vector2(200f, 60f), Vector2.zero, "ターン終了");
            RectTransform hint = UiKit.Point(canvas, "StanceHint", Half, Half, new Vector2(120f, 44f), new Vector2(790f, -360f));
            RectTransform hintIcon = UiKit.Point(hint, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 38f), new Vector2(12f, 0f));
            player.stanceHintIcon = hintIcon.gameObject.AddComponent<Image>();
            player.stanceHintIcon.color = BattleTheme.Guard;
            player.stanceHintIcon.raycastTarget = false;
            player.stanceHintText = UiKit.Label(hint, "Text", 30, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70f, 44f), new Vector2(54f, 0f), "+3");
            player.stanceHintText.fontStyle = FontStyle.Bold;

            player.handArea = UiKit.Point(canvas, "HandArea", Half, Half, new Vector2(1100f, 280f), new Vector2(0f, -400f));

            player.fxLayer = UiKit.Rect(canvas, "FxLayer", Vector2.zero, Vector2.one);
            CanvasGroup fxGroup = player.fxLayer.gameObject.AddComponent<CanvasGroup>();
            fxGroup.blocksRaycasts = false;
            fxGroup.interactable = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ---- helpers ----------------------------------------------------------------------------

        private static T Load<T>(string prefabName) where T : Component
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + prefabName + ".prefab");
            if (go == null) throw new FileNotFoundException("Missing prefab " + prefabName);
            return go.GetComponent<T>();
        }

        private static T Place<T>(string prefabName, RectTransform parent, Vector2 anchor, Vector2 offset) where T : Component
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + prefabName + ".prefab");
            if (asset == null) throw new FileNotFoundException("Missing prefab " + prefabName);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = offset;
            Overrides(rt);
            return go.GetComponent<T>();
        }

        private static void Overrides(Object target)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static RectTransform Root(string name, Vector2 size, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Half;
            rt.anchorMax = Half;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            return rt;
        }

        // Plain colour Image with no sprite: ProceduralArt sprites are runtime textures and cannot be
        // saved into a prefab. Views assign them in Awake where a shape is needed.
        private static Image Img(RectTransform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rt = UiKit.Rect(parent, name, anchorMin, anchorMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void Frame(RectTransform parent, Color color, float thickness)
        {
            Image top = Img(parent, "FrameTop", color, new Vector2(0f, 1f), new Vector2(1f, 1f));
            top.rectTransform.offsetMin = new Vector2(0f, -thickness);
            Image bottom = Img(parent, "FrameBottom", color, new Vector2(0f, 0f), new Vector2(1f, 0f));
            bottom.rectTransform.offsetMax = new Vector2(0f, thickness);
            Image left = Img(parent, "FrameLeft", color, new Vector2(0f, 0f), new Vector2(0f, 1f));
            left.rectTransform.offsetMax = new Vector2(thickness, 0f);
            Image right = Img(parent, "FrameRight", color, new Vector2(1f, 0f), new Vector2(1f, 1f));
            right.rectTransform.offsetMin = new Vector2(-thickness, 0f);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
