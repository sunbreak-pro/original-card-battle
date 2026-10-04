// Unity's NUnit (com.unity.ext.nunit, a custom NUnit 3.5) has no Assert.Multiple and no Assert.Warn,
// which these tests use under dotnet test (NUnit 4). Inside Unity this Assert is found before NUnit's,
// keeps every other Assert member through inheritance, and runs a Multiple block as plain asserts: the
// first failure stops it instead of collecting them all. A Warn goes to the Console as a warning and the
// test still passes, since NUnit 3.5 has no warning result. Under dotnet test the file is empty.
#if UNITY_2021_2_OR_NEWER
namespace Depiction.Bridge.Tests
{
    internal class Assert : NUnit.Framework.Assert
    {
        public static void Multiple(NUnit.Framework.TestDelegate testDelegate) => testDelegate();

        public static void Warn(string message) => UnityEngine.Debug.LogWarning(message);
    }
}
#endif
