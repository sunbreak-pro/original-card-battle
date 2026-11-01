using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Trial.Script.Tests
{
    /// <summary>
    /// MetaText against the JS text.js, byte for byte: every string and list member, every function (by name), and a
    /// sample call of each function (JsMetaText holds what node printed for them).
    /// </summary>
    public class MetaTextTests
    {
        private static IEnumerable<string> CSharpStrings() =>
            typeof(MetaText).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .Select(f => f.Name);

        [Test]
        public void EveryStringMember_IsTheJs()
        {
            foreach (var pair in JsMetaText.Strings)
            {
                FieldInfo field = typeof(MetaText).GetField(pair.Key, BindingFlags.Public | BindingFlags.Static);
                Assert.That(field, Is.Not.Null, pair.Key);
                Assert.That(field.GetValue(null), Is.EqualTo(pair.Value), pair.Key);
            }
            Assert.That(CSharpStrings(), Is.EquivalentTo(JsMetaText.Strings.Keys), "no string the JS lacks");
        }

        [Test]
        public void EveryListMember_IsTheJs()
        {
            foreach (var pair in JsMetaText.Lists)
            {
                PropertyInfo property = typeof(MetaText).GetProperty(pair.Key, BindingFlags.Public | BindingFlags.Static);
                Assert.That(property, Is.Not.Null, pair.Key);
                Assert.That((IReadOnlyList<string>)property.GetValue(null), Is.EqualTo(pair.Value), pair.Key);
            }
        }

        [Test]
        public void EveryFunctionMember_IsAMethod()
        {
            var methods = typeof(MetaText).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => !m.IsSpecialName).Select(m => m.Name).Distinct().ToList();
            Assert.That(methods, Is.EquivalentTo(JsMetaText.Functions));
        }

        [Test]
        public void SampleCalls_PrintWhatTheJsPrints()
        {
            foreach (var (name, args, result) in JsMetaText.Calls)
            {
                MethodInfo method = typeof(MetaText).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
                Assert.That(method, Is.Not.Null, name);
                Assert.That(method.Invoke(null, args), Is.EqualTo(result), name + "(" + string.Join(", ", args) + ")");
            }
        }

        [Test]
        public void HallKeysFor_DropsTheTraceAndListKeys_WhenNotNeeded()
        {
            Assert.That(MetaText.HallKeysFor(false, false), Is.EqualTo(MetaText.HallKeysNoTrace));
            Assert.That(MetaText.HallKeysFor(true, false), Is.EqualTo(MetaText.HallKeys));
        }

        [Test]
        public void SaveTexts_ByKind_FallBackToALife()
        {
            Assert.That(MetaText.SaveQuitOf(null), Is.EqualTo(MetaText.SaveQuit));
            Assert.That(MetaText.SaveNoteOf(null), Is.EqualTo(MetaText.SaveNote));
            Assert.That(MetaText.SaveQuitOf("lineage"), Is.EqualTo(MetaText.SaveToTitle));
        }

        [Test]
        public void TheTagline_IsTwentyFourLetters()
        {
            // title.js times the letters of both columns through one count (the last starts at 2855 ms)
            Assert.That(MetaText.TaglineLines.Select(l => MetaTimelines.Chars(l).Count), Is.EqualTo(new[] { 11, 13 }));
        }

        [Test]
        public void Numbers_PrintWithoutCulture()
        {
            var saved = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
                Assert.That(MetaText.DeckCount(1234), Is.EqualTo("デッキ: 1234 枚"));
                Assert.That(MetaText.TraceWaitsMany(-1, 2), Does.StartWith("第 -1 層"));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = saved;
            }
        }
    }
}
