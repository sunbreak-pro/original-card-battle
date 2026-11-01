// The storage under the trial's save: the browser's localStorage, as the C# sees it (port of the guarded
// read / write / remove of src/app/save.js:19-27). Every access is guarded: storage can be missing, locked
// or full, and the game must still run without it — a read that fails is null, a write that fails is false.
//
// FileKeyValueStore keeps one file per key under a folder (the Unity side passes
// Application.persistentDataPath + "/trial"). A write is atomic: the text goes to "<file>.tmp", is flushed
// to the disk, and replaces the file in one move (File.Replace); a lock (an antivirus scanning the file) is
// retried once, then the tmp is copied over. A read shares the file with every other handle and is retried
// once when the file is there but does not open. MemoryKeyValueStore is the same store in memory (tests, and
// the boot switch that simulates blocked storage).
//
// Engine-free (no UnityEngine), main thread only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Trial.Bridge
{
    /// <summary>A string store by key (the browser's localStorage). No member throws.</summary>
    public interface IKeyValueStore
    {
        /// <summary>The stored text, or null when nothing is stored or the store cannot be read.</summary>
        string Read(string key);

        /// <summary>
        /// The first characters of the stored text, at most <paramref name="maxChars"/> of them (the save's stamp is
        /// read this way, without reading or parsing the whole save). Null when nothing is stored or it cannot be read
        /// (whatever <paramref name="maxChars"/> is: "" means a text is stored).
        /// </summary>
        string ReadHead(string key, int maxChars);

        /// <summary>Stores the text; whether the store took it.</summary>
        bool Write(string key, string text);

        /// <summary>Removes the key (nothing happens when it is not there or the store refuses).</summary>
        void Remove(string key);
    }

    /// <summary>
    /// One file per key under <see cref="Folder"/>. The JS keys map to the file names of the port
    /// (app.md §2.1); any other key becomes "&lt;key&gt;.json" with the characters a file name cannot hold replaced.
    /// </summary>
    public sealed class FileKeyValueStore : IKeyValueStore
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>How long a locked file is given before the one retry (ms).</summary>
        public const int RetryDelayMs = 50;

        public FileKeyValueStore(string folder)
        {
            if (string.IsNullOrEmpty(folder)) throw new ArgumentException("FileKeyValueStore: a folder is needed.", nameof(folder));
            Folder = folder;
        }

        /// <summary>The folder the files live in. It is created on the first write.</summary>
        public string Folder { get; }

        /// <summary>The file a key is kept in (the JS key → file name of app.md §2.1).</summary>
        public static string FileNameOf(string key)
        {
            switch (key)
            {
                case "ocb.save.v1": return "save.v1.json";
                case "ocb.save.v1.broken": return "save.v1.broken.json";
                case "ocb.settings.v1": return "settings.v1.json";
                case "ocb.departure.draft.v1": return "departure-draft.v1.json";
                case "ocb.probe": return "probe.tmp";
            }
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("FileKeyValueStore: an empty key.", nameof(key));
            var sb = new StringBuilder(key.Length + 5);
            foreach (char c in key)
            {
                bool plain = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_';
                sb.Append(plain ? c : '_');
            }
            sb.Append(".json");
            return sb.ToString();
        }

        /// <summary>The full path of the file a key is kept in.</summary>
        public string PathOf(string key) => Path.Combine(Folder, FileNameOf(key));

        public string Read(string key) => ReadText(key, int.MaxValue);

        public string ReadHead(string key, int maxChars) => ReadText(key, Math.Max(0, maxChars));

        // A read shares the file with every other handle (a write's File.Replace, a scanner), so it does not stand in
        // their way. A file that is there but does not open (a replace under way, a lock) is given one retry: a read
        // that failed would read as "nothing stored", and the stamp check would take that for another instance's
        // write (localStorage reads never fail that way in the browser).
        private string ReadText(string key, int maxChars)
        {
            string path;
            try
            {
                path = PathOf(key);
                if (!File.Exists(path)) return null;
            }
            catch (Exception) { return null; }
            if (TryRead(path, maxChars, out string text)) return text;
            Thread.Sleep(RetryDelayMs);
            return TryRead(path, maxChars, out text) ? text : null;
        }

        private static bool TryRead(string path, int maxChars, out string text)
        {
            text = null;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: true))
                {
                    if (maxChars == int.MaxValue)
                    {
                        text = reader.ReadToEnd();
                        return true;
                    }
                    // UTF-8 never decodes to more chars than it has bytes: the file's length bounds the buffer.
                    int want = (int)Math.Min(maxChars, stream.Length);
                    var buffer = new char[want];
                    int read = 0;
                    while (read < want)
                    {
                        int n = reader.Read(buffer, read, want - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    text = new string(buffer, 0, read);
                    return true;
                }
            }
            catch (Exception) { return false; }
        }

        public bool Write(string key, string text)
        {
            if (text == null) return false;
            string path;
            string tmp;
            try
            {
                Directory.CreateDirectory(Folder);
                path = PathOf(key);
                tmp = path + ".tmp";
                byte[] bytes = Utf8.GetBytes(text);
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
            }
            catch (Exception) { return false; }

            // One move, so a crash leaves either the old file or the new one, never half of one.
            if (TryMoveInto(tmp, path)) return true;
            Thread.Sleep(RetryDelayMs);
            if (TryMoveInto(tmp, path)) return true;
            // Last resort: copy the text over (not atomic, but the text is complete on the disk already).
            try
            {
                File.Copy(tmp, path, overwrite: true);
                TryDelete(tmp);
                return true;
            }
            catch (Exception)
            {
                TryDelete(tmp);
                return false;
            }
        }

        public void Remove(string key)
        {
            try { File.Delete(PathOf(key)); }
            catch (Exception) { /* storage unavailable */ }
        }

        private static bool TryMoveInto(string tmp, string path)
        {
            try
            {
                if (File.Exists(path)) File.Replace(tmp, path, null, ignoreMetadataErrors: true);
                else File.Move(tmp, path);
                return true;
            }
            catch (Exception) { return false; }
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (Exception) { /* left behind; the next write overwrites it */ }
        }
    }

    /// <summary>
    /// The store in memory. <see cref="Blocked"/> makes every access fail (a private window's storage, the
    /// boot switch noStorage); <see cref="RefuseWrites"/> keeps reads and removes working and refuses writes
    /// (storage full).
    /// </summary>
    public sealed class MemoryKeyValueStore : IKeyValueStore
    {
        private readonly Dictionary<string, string> _items = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();

        /// <summary>Every access fails: reads are null, writes false (the JS fake storage that throws SecurityError).</summary>
        public bool Blocked { get; set; }

        /// <summary>Reads and removes work, writes fail (QuotaExceededError).</summary>
        public bool RefuseWrites { get; set; }

        /// <summary>How many writes the store took.</summary>
        public int Writes { get; private set; }

        /// <summary>The keys held, in the order they were first written.</summary>
        public IReadOnlyList<string> Keys => _order;

        public string Read(string key)
        {
            if (Blocked || key == null) return null;
            return _items.TryGetValue(key, out string text) ? text : null;
        }

        public string ReadHead(string key, int maxChars)
        {
            string text = Read(key);
            if (text == null) return null;
            if (maxChars <= 0) return "";
            return text.Length <= maxChars ? text : text.Substring(0, maxChars);
        }

        public bool Write(string key, string text)
        {
            if (Blocked || RefuseWrites || key == null || text == null) return false;
            if (!_items.ContainsKey(key)) _order.Add(key);
            _items[key] = text;
            Writes++;
            return true;
        }

        public void Remove(string key)
        {
            if (Blocked || key == null) return;
            if (_items.Remove(key)) _order.Remove(key);
        }
    }
}
