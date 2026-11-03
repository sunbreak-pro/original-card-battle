// #780: CardView.Create is the one place a card's ground is made, for the battle's Card prefab and for
// the exploration's card pick alike. These tests keep a part the prefab gains from going missing on the
// pick screen without a word.
//
// CardView lives in the predefined Assembly-CSharp, which an asmdef cannot reference, so it is reached
// by name. Guarded by UNITY_EDITOR: `dotnet test` compiles this folder too and has no UnityEngine.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Depiction.Tests
{
    public class CardGroundTests
    {
        private const string CardViewTypeName = "Depiction.View.CardView";
        private const string CardPrefabPath = "Assets/Prefabs/Depiction/Card.prefab";

        // The type line and the single big value left the face in #242: EnsureParts hides them when an
        // old prefab still has them and never makes them, so a card made in code goes without.
        private static readonly HashSet<string> Retired = new HashSet<string> { "typeText", "valueText" };

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void DestroyMade()
        {
            foreach (GameObject go in _made) if (go) Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void CreateMakesTheGroundEnsurePartsCannotMake()
        {
            Type type = CardViewType();
            Component card = Create(type);

            var group = (Component)Field(type, "group").GetValue(card);
            Assert.IsTrue(group != null, "CardView.group is missing.");
            Assert.That(group.gameObject, Is.SameAs(card.gameObject), "The CanvasGroup belongs on the card's root.");

            var background = (Component)Field(type, "background").GetValue(card);
            Assert.IsTrue(background != null, "CardView.background is missing.");
            Assert.That(background.transform.parent, Is.SameAs(card.transform));
            Assert.That(Raycast(background), Is.True, "The press and the drag land on the background.");

            var shade = (Behaviour)Field(type, "shade").GetValue(card);
            Assert.IsTrue(shade != null, "CardView.shade is missing.");
            Assert.That(shade.transform.parent, Is.SameAs(card.transform));
            Assert.That(shade.enabled, Is.False, "A card starts undimmed.");
            Assert.That(Raycast(shade), Is.False, "The shade must not take the press from the background.");

            var size = (Vector2)type.GetField("Size", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            Assert.That(((RectTransform)card.transform).sizeDelta, Is.EqualTo(size));
        }

        [Test]
        public void ACreatedCardHasEveryPartCardViewPointsAt()
        {
            Type type = CardViewType();
            Component card = Create(type);
            EnsureParts(type, card);

            var missing = new List<string>();
            foreach (FieldInfo field in Parts(type))
            {
                if (Retired.Contains(field.Name)) continue;
                if ((Object)field.GetValue(card) == null) missing.Add(field.Name);
            }
            Assert.That(missing, Is.Empty,
                "Neither CardView.Create nor EnsureParts makes these parts; make them in CardView.Create.");
        }

        [Test]
        public void ACreatedCardHasEveryPartTheBattlePrefabHas()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (prefab == null) Assert.Ignore(CardPrefabPath + " is not in this project (Tools > Depiction > Build Prefabs And Scene).");
            Type type = CardViewType();
            Component fromPrefab = prefab.GetComponent(type);
            Assert.IsTrue(fromPrefab != null, CardPrefabPath + " has no CardView on its root.");

            Component card = Create(type);
            EnsureParts(type, card);

            var missing = new List<string>();
            foreach (FieldInfo field in Parts(type))
            {
                if (Retired.Contains(field.Name)) continue;
                if ((Object)field.GetValue(fromPrefab) == null) continue;
                if ((Object)field.GetValue(card) == null) missing.Add(field.Name);
            }
            Assert.That(missing, Is.Empty,
                "The battle's Card prefab has these parts and a card made by CardView.Create does not; make them in CardView.Create.");
        }

        private Component Create(Type type)
        {
            MethodInfo create = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(create, "CardView.Create(RectTransform, string) is gone.");
            var card = (Component)create.Invoke(null, new object[] { null, "Card" });
            Assert.IsTrue(card != null, "CardView.Create returned nothing.");
            _made.Add(card.gameObject);
            return card;
        }

        // Awake does not run in Edit Mode, so the face is laid out by hand as Awake would.
        private static void EnsureParts(Type type, Component card)
        {
            type.GetMethod("EnsureParts", BindingFlags.Public | BindingFlags.Instance).Invoke(card, null);
        }

        private static IEnumerable<FieldInfo> Parts(Type type)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (typeof(Object).IsAssignableFrom(field.FieldType)) yield return field;
            }
        }

        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "CardView." + name + " is gone.");
            return field;
        }

        // Graphic.raycastTarget, read by name: this assembly does not reference UnityEngine.UI.
        private static bool Raycast(Component graphic)
        {
            PropertyInfo property = graphic.GetType().GetProperty("raycastTarget", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(property, graphic.GetType().Name + " has no raycastTarget.");
            return (bool)property.GetValue(graphic);
        }

        private static Type CardViewType()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(CardViewTypeName);
                if (type != null) return type;
            }
            Assert.Fail(CardViewTypeName + " is not loaded.");
            return null;
        }
    }
}
#endif
