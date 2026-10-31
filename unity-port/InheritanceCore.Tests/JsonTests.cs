using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InheritanceCore;
using NUnit.Framework;

namespace InheritanceCore.Tests
{
    /// <summary>Json against JSON.stringify / JSON.parse: numbers, strings, indentation, key order, strictness.</summary>
    public class JsonTests
    {
        private static JsonObject Oracle => JsParityTests.Oracle;

        [Test]
        public void Numbers_PrintAsJsonStringifyPrintsThem()
        {
            foreach (var row in ((List<object?>)Oracle["numbers"]!).Cast<List<object?>>())
            {
                string repr = (string)row[0]!;
                string expected = (string)row[1]!;
                double value = repr == "-0" ? -0.0 : double.Parse(repr, NumberStyles.Float, CultureInfo.InvariantCulture);
                Assert.That(Json.Stringify(value), Is.EqualTo(expected), repr);
                Assert.That(Json.Stringify(Json.Parse(expected)), Is.EqualTo(expected), "round trip of " + expected);
            }
        }

        /// <summary>
        /// Literals at the edges of rounding, read and printed as JSON.parse / JSON.stringify do (bits from node). Json
        /// does this in integer arithmetic: Unity's Mono reads and prints some 17-digit numbers otherwise (the last
        /// three rows are such numbers).
        /// </summary>
        [TestCase("2.4703282292062327e-324", "0000000000000000", "0", TestName = "below half the least subnormal reads as 0")]
        [TestCase("2.4703282292062328e-324", "0000000000000001", "5e-324")]
        [TestCase("4.9406564584124654e-324", "0000000000000001", "5e-324")]
        [TestCase("5e-325", "0000000000000000", "0")]
        [TestCase("1.7976931348623158e308", "7fefffffffffffff", "1.7976931348623157e+308")]
        [TestCase("1.7976931348623159e308", "7ff0000000000000", "null", TestName = "past the largest double reads as Infinity")]
        [TestCase("2.2250738585072011e-308", "000fffffffffffff", "2.225073858507201e-308")]
        [TestCase("2.2250738585072012e-308", "0010000000000000", "2.2250738585072014e-308")]
        [TestCase("9007199254740993", "4340000000000000", "9007199254740992", TestName = "a tie rounds to the even double")]
        [TestCase("9007199254740995", "4340000000000002", "9007199254740996")]
        [TestCase("0.1000000000000000055511151231257827021181583404541015625", "3fb999999999999a", "0.1")]
        [TestCase("1.00000000000000011102230246251565404236316680908203125", "3ff0000000000000", "1")]
        [TestCase("123456789012345678901234567890e-10", "43e56a95319d63e1", "12345678901234567000")]
        [TestCase("1e23", "44b52d02c7e14af6", "1e+23")]
        [TestCase("8.41e21", "447c7e83209e90b2", "8.41e+21")]
        [TestCase("1e400", "7ff0000000000000", "null")]
        [TestCase("-1e-400", "8000000000000000", "0")]
        [TestCase("7.334228993859142e-20", "3bf5a5963526c4ac", "7.334228993859142e-20")]
        [TestCase("629826.4332581311", "41233884ddd40280", "629826.4332581311")]
        [TestCase("3.5621774975366353e-300", "01c315a30d9fc718", "3.5621774975366353e-300")]
        public void Numbers_ReadAndPrintExactly(string literal, string bits, string printed)
        {
            double read = Json.ToDouble(Json.Parse(literal));
            if (read != 0) Assert.That(BitConverter.DoubleToInt64Bits(read).ToString("x16"), Is.EqualTo(bits));
            else Assert.That(bits.TrimStart('8', '0'), Is.Empty, "zero");
            Assert.That(Json.Stringify(BitConverter.Int64BitsToDouble(Convert.ToInt64(bits, 16))), Is.EqualTo(printed));
        }

        [Test]
        public void IntegerTypes_PrintWithoutAFraction_AndBeyond2Pow53AsTheDoubleJsWouldHold()
        {
            Assert.That(Json.Stringify(3.0), Is.EqualTo("3"));
            Assert.That(Json.Stringify(-7), Is.EqualTo("-7"));
            Assert.That(Json.Stringify(9007199254740993L), Is.EqualTo("9007199254740992"));
            Assert.That(Json.Stringify(ulong.MaxValue), Is.EqualTo("18446744073709552000"));
            Assert.That(Json.Stringify(0.1f), Is.EqualTo("0.10000000149011612"));
            Assert.That(Json.Stringify(double.NaN), Is.EqualTo("null"));
            Assert.That(Json.Stringify(double.NegativeInfinity), Is.EqualTo("null"));
            Assert.That(Json.NumberToString(double.NegativeInfinity), Is.EqualTo("-Infinity"));
        }

        [Test]
        public void Strings_AreEscapedAsJsonStringifyEscapesThem()
        {
            foreach (var row in ((List<object?>)Oracle["strings"]!).Cast<List<object?>>())
            {
                var codes = ((List<object?>)row[0]!).Select(c => (char)(int)c!).ToArray();
                string s = new string(codes);
                string expected = (string)row[1]!;
                Assert.That(Json.Stringify(s), Is.EqualTo(expected), expected);
                Assert.That(Json.Parse(expected), Is.EqualTo(s), "parse of " + expected);
            }
        }

        [Test]
        public void Indent_IsStringifyWithTwoSpaces_AndKeysFollowJsOrder()
        {
            var o = new JsonObject
            {
                ["a"] = 1,
                ["b"] = new List<object?> { 1, new JsonObject { ["c"] = new List<object?>() }, new JsonObject() },
                ["2"] = "two",
                ["10"] = null,
                ["01"] = true,
                ["d"] = "x",
            };
            Assert.That(Json.Stringify(o, indent: true), Is.EqualTo((string)Oracle["indent"]!));
            string given = "{\"b\":1,\"10\":2,\"a\":3,\"2\":4,\"4294967295\":5,\"4294967294\":6,\"-1\":7,\"01\":8}";
            Assert.That(Json.Stringify(Json.Parse(given)), Is.EqualTo((string)Oracle["order"]!));
        }

        [Test]
        public void JsonObject_KeepsJsPropertyOrder()
        {
            var o = new JsonObject();
            o["z"] = 1;
            o["5"] = 2;
            o["a"] = 3;
            o["1"] = 4;
            Assert.That(o.Keys, Is.EqualTo(new[] { "1", "5", "z", "a" }));
            o["z"] = 9; // setting again keeps the place
            Assert.That(o.Keys, Is.EqualTo(new[] { "1", "5", "z", "a" }));
            Assert.That(o.Remove("z"), Is.True);
            o["z"] = 10; // removed and set again: at the end
            Assert.That(o.Keys, Is.EqualTo(new[] { "1", "5", "a", "z" }));
            Assert.That(o.Remove("1"), Is.True);
            o["0"] = 0;
            Assert.That(o.Keys, Is.EqualTo(new[] { "0", "5", "a", "z" }));
            Assert.That(o.Count, Is.EqualTo(4));
            Assert.That(o["missing"], Is.Null);
            Assert.That(o.TryGet("missing", out _), Is.False);
            o["n"] = null;
            Assert.That(o.TryGet("n", out var n), Is.True);
            Assert.That(n, Is.Null);
            Assert.That(o.Remove("nope"), Is.False);
            Assert.That(Json.Stringify(o), Is.EqualTo("{\"0\":0,\"5\":2,\"a\":3,\"z\":10,\"n\":null}"));
        }

        [Test]
        public void Parse_ReturnsPlainValues_IntLongOrDouble()
        {
            var list = (List<object?>)Json.Parse(" [1, 2.5e3, -0, 1.5, true, false, null, \"x\", 12345678901234567890, 9007199254740993, 3000000000, 1e400, {}] ")!;
            Assert.That(list[0], Is.TypeOf<int>().And.EqualTo(1));
            Assert.That(list[1], Is.TypeOf<int>().And.EqualTo(2500));
            Assert.That(list[2], Is.TypeOf<int>().And.EqualTo(0));
            Assert.That(list[3], Is.TypeOf<double>().And.EqualTo(1.5));
            Assert.That(list[4], Is.EqualTo(true));
            Assert.That(list[5], Is.EqualTo(false));
            Assert.That(list[6], Is.Null);
            Assert.That(list[7], Is.EqualTo("x"));
            Assert.That(list[8], Is.TypeOf<double>());
            Assert.That(list[9], Is.TypeOf<long>().And.EqualTo(9007199254740992L));
            Assert.That(list[10], Is.TypeOf<long>().And.EqualTo(3000000000L));
            Assert.That(list[11], Is.EqualTo(double.PositiveInfinity));
            Assert.That(list[12], Is.TypeOf<JsonObject>());
            Assert.That(Json.Stringify(list), Is.EqualTo("[1,2500,0,1.5,true,false,null,\"x\",12345678901234567000,9007199254740992,3000000000,null,{}]"));
            var dup = Json.Parse("{\"a\":1,\"b\":2,\"a\":3}");
            Assert.That(Json.Stringify(dup), Is.EqualTo("{\"a\":3,\"b\":2}"), "the last duplicate wins, in the first one's place");
            Assert.That(Json.Parse("\"\\u00e9\\/\\uD83D\\uDE00\""), Is.EqualTo("é/😀"));
        }

        [Test]
        public void Parse_IsStrict()
        {
            foreach (var bad in new[]
            {
                "", " ", "{", "[1,]", "{\"a\":1,}", "01", "1.", ".5", "+1", "-", "tru", "nul", "\"\\x\"", "\"a\nb\"", "NaN", "Infinity",
                "1 2", "'a'", "\"\\u12\"", "{a:1}", "[1 2]", "\"abc", "\uFEFF1", "{\"a\" 1}", "1e", "1e+",
            })
            {
                Assert.Throws<JsonException>(() => Json.Parse(bad), bad);
            }
            Assert.That(Json.Parse(" \t\r\n1 \t\r\n"), Is.EqualTo(1));
        }

        [Test]
        public void Stringify_TakesListsArraysDictionariesAndEnumNames()
        {
            Assert.That(Json.Stringify(null), Is.EqualTo("null"));
            Assert.That(Json.Stringify(new[] { 1, 2 }), Is.EqualTo("[1,2]"));
            Assert.That(Json.Stringify(new List<object?>()), Is.EqualTo("[]"));
            Assert.That(Json.Stringify(new List<object?>(), indent: true), Is.EqualTo("[]"));
            Assert.That(Json.Stringify(new JsonObject(), indent: true), Is.EqualTo("{}"));
            Assert.That(Json.Stringify(new Dictionary<string, object?> { ["b"] = 1, ["3"] = "x" }), Is.EqualTo("{\"3\":\"x\",\"b\":1}"));
            Assert.That(Json.Stringify(LifeEnding.Fallen), Is.EqualTo("\"Fallen\""));
            Assert.That(Json.Stringify('a'), Is.EqualTo("\"a\""));
            Assert.Throws<ArgumentException>(() => Json.Stringify(new object()));
        }

        [Test]
        public void AStampLeadsTheSave_WhenWrittenFirst()
        {
            // stamp.test.mjs: the save writer puts { v, rev, tab } first so the stamp is read without parsing.
            var save = new JsonObject
            {
                ["v"] = 1,
                ["rev"] = 3,
                ["tab"] = "abc",
                ["lineage"] = Inheritance.ToJson(Inheritance.NewLineage(5)),
                ["life"] = null,
            };
            Assert.That(Json.Stringify(save), Does.StartWith("{\"v\":1,\"rev\":3,\"tab\":\"abc\","));
        }
    }
}
