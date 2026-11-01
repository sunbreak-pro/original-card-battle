// Shared helpers of the save tests (dotnet only; not copied into Unity).
using System;
using System.Collections.Generic;
using InheritanceCore;
using NUnit.Framework;

namespace Trial.Bridge.Tests
{
    internal static class SaveTestKit
    {
        /// <summary>Inh.newLineage(seed).</summary>
        public static Lineage NewLineage(int seed) => Inheritance.NewLineage((ulong)seed);

        /// <summary>Inh.serialize(lineage): the lineage's JSON text.</summary>
        public static string Serialize(Lineage lineage) => Inheritance.Serialize(lineage);

        /// <summary>Runs <paramref name="body"/> with Save.Warn silenced (the JS tests silence console.warn).</summary>
        public static void Quietly(Action body)
        {
            Action<string, Exception> warn = Save.Warn;
            Save.Warn = (m, e) => { };
            try { body(); }
            finally { Save.Warn = warn; }
        }

        public static T Quietly<T>(Func<T> body)
        {
            T result = default;
            Quietly(() => { result = body(); });
            return result;
        }

        public static void SameStamp(Stamp actual, long rev, string tab, string message = null)
        {
            Assert.That(actual, Is.Not.Null, message);
            Assert.That(actual.Rev, Is.EqualTo(rev), message);
            Assert.That(actual.Tab, Is.EqualTo(tab), message);
        }
    }

    /// <summary>A store that answers ReadHead only: proves a read of the stamp never reads the whole save.</summary>
    internal sealed class HeadOnlyStore : IKeyValueStore
    {
        private readonly MemoryKeyValueStore _inner = new MemoryKeyValueStore();
        public int HeadReads { get; private set; }
        public int LastHeadChars { get; private set; }

        public string Read(string key) => throw new AssertionException("the whole text was read");

        public string ReadHead(string key, int maxChars)
        {
            HeadReads++;
            LastHeadChars = maxChars;
            return _inner.ReadHead(key, maxChars);
        }

        public bool Write(string key, string text) => _inner.Write(key, text);
        public void Remove(string key) => _inner.Remove(key);
    }

    /// <summary>A live life for SaveKeeper (main.js `live`).</summary>
    internal sealed class FakeLive : ILiveLife
    {
        public LifeStartData Start { get; set; }
        public LifeLog Log { get; set; } = new LifeLog();
        public string Memo { get; set; } = "";
        public object ScribblesJson { get; set; } = new List<object>();
    }
}
