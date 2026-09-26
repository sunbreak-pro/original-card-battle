// Unity's NUnit (com.unity.ext.nunit, a custom NUnit 3.5) has no Assert.Multiple, which these tests
// use under dotnet test (NUnit 4). Inside Unity this Assert is found before NUnit's, keeps every
// other Assert member through inheritance, and runs a Multiple block as plain asserts: the first
// failure stops it instead of collecting them all. Under dotnet test the file is empty.
#if UNITY_2021_2_OR_NEWER
namespace Depiction.Bridge.Tests
{
    internal class Assert : NUnit.Framework.Assert
    {
        public static void Multiple(NUnit.Framework.TestDelegate testDelegate) => testDelegate();
    }
}
#endif
