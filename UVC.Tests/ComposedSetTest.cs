using System.IO;
using NUnit.Framework;
using ComposedString = UVC.ComposedSet<string, UVC.FilesAndFoldersComposedStringDatabase>;
//using Unity.PerformanceTesting;

namespace UVC.UnitTests
{
    public class ComposedSetTest
    {
        static ComposedString CCSet(string str)
        {
            return new ComposedString(str);
        }

        [Test]
        public void GetHashCodeTest()
        {
            var abcd = CCSet("A.B.C.D");
            Assert.AreEqual    (CCSet("A.B.C.D").GetHashCode()  , CCSet("A.B.C.D")  .GetHashCode());
            Assert.AreEqual    (abcd.GetHashCode()              , abcd.GetHashCode());
            Assert.AreEqual    (CCSet("").GetHashCode()         , CCSet("").GetHashCode());
            Assert.AreNotEqual (CCSet("A.B.C.D").GetHashCode()  , CCSet("A.B.C.E")  .GetHashCode());
            Assert.AreNotEqual (CCSet("A.B.C.D").GetHashCode()  , CCSet("a.B.C.D")  .GetHashCode());
            Assert.AreNotEqual (CCSet(" ").GetHashCode()        , CCSet("").GetHashCode());
        }

        [Test]
        public void TrimEndTest()
        {
            var abcd  = CCSet("A.B.C.D");
            var ab    = CCSet("A.B.");
            var cd    = CCSet("C.D");
            var empty = CCSet("");
            Assert.AreEqual    (abcd .TrimEnd(cd)    ,ab);
            Assert.AreEqual    (ab   .TrimEnd(ab)    ,empty);
            Assert.AreEqual    (empty.TrimEnd(empty) ,empty);
            Assert.AreEqual    (ab   .TrimEnd(empty) ,ab);
            Assert.AreNotEqual (abcd .TrimEnd(ab)    ,ab);
            Assert.AreNotEqual (abcd .TrimEnd(ab)    ,cd);
        }

        [Test]
        public void OperatorAddTest()
        {
            var abcd  = CCSet("A.B.C.D");
            var a     = CCSet("A");
            var b     = CCSet("B");
            var c     = CCSet("C");
            var d     = CCSet("D");
            var ab    = CCSet("A.B");
            var cd    = CCSet("C.D");
            var dot   = CCSet(".");
            Assert.AreEqual    (abcd , ab + dot + cd);
            Assert.AreEqual    (ab + dot + cd, ab + dot + cd);
            Assert.AreEqual    (abcd , a + dot + b + dot + c + dot + d);
            Assert.AreEqual    (abcd.GetHashCode(), (a + dot + b + dot + c + dot + d).GetHashCode());
            Assert.AreNotEqual (abcd, ab + dot + dot + cd);
        }

        [Test]
        public void EndsWithTest()
        {
            Assert.IsTrue(CCSet("A.B.C.D") .EndsWith(CCSet("A.B.C.D")));
            Assert.IsTrue(CCSet("A.B.C.D") .EndsWith(CCSet("A.B.C.D")));
            Assert.IsTrue(CCSet("A.B.C.D") .EndsWith(CCSet("A.B.C.D")));
            Assert.IsTrue(CCSet("A.B.C.D") .EndsWith(CCSet("A.B.C.D")));
            Assert.IsTrue(CCSet("A.B.C.D") .EndsWith(CCSet("A.B.C.D")));
            Assert.IsFalse(CCSet("A/B/C/D").EndsWith(CCSet("A.B.C.D")));
            Assert.IsFalse(CCSet("A/B/C/D").EndsWith(CCSet("A.B.C.D")));
            Assert.IsFalse(CCSet("A/B/C/D").EndsWith(CCSet("A.B.C.D")));
            Assert.IsFalse(CCSet("A/B/C/D").EndsWith(CCSet("A.B.C.D")));
            Assert.IsFalse(CCSet("A/B/C/D").EndsWith(CCSet("A.B.C.D")));
            Assert.IsFalse(CCSet("A/B/C/D").EndsWith(CCSet("A.B.C.D")));
            Assert.IsFalse(CCSet("A/B/C/D").EndsWith(CCSet("A.B.C.D")));
            Assert.IsTrue(CCSet("A.B.C.D") .EndsWith(CCSet("C.D")));
            Assert.IsFalse(CCSet(" ")      .EndsWith(CCSet("A.B.C.D")));
            Assert.IsFalse(CCSet(" ")      .EndsWith(CCSet("A.B.C.D")));
        }

        [Test]
        public void EqualsTest()
        {
            Assert.That(CCSet("A.B.C.D" ).Equals(CCSet("A.B.C.D" )), Is.True);
            Assert.That(CCSet("A.B.C.D" ) ==    (CCSet("A.B.C.D" )), Is.True);
            Assert.That(CCSet(" ")       .Equals(CCSet(" ")),        Is.True);
            Assert.That(CCSet("" )       .Equals(CCSet("")),         Is.True);
            Assert.That(CCSet("A/B/C/D" ).Equals(CCSet("A.B.C.D" )), Is.False);
            Assert.That(CCSet("A.B.CD"  ).Equals(CCSet("A.B.C.D" )), Is.False);
            Assert.That(CCSet("A.B.C.C" ).Equals(CCSet("A.B.C.D" )), Is.False);
            Assert.That(CCSet("A.B.C.C" ) ==    (CCSet("A.B.C.D" )), Is.False);
            Assert.That(CCSet("A.B.C.D.").Equals(CCSet("A.B.C.D" )), Is.False);
            Assert.That(CCSet("0.B.C.D" ).Equals(CCSet("A.B.C.D" )), Is.False);
            Assert.That(CCSet("a.B.C.D" ).Equals(CCSet("A.B.C.D" )), Is.False);
            Assert.That(CCSet("B.B.C.D" ).Equals(CCSet("A.B.C.D" )), Is.False);
            Assert.That(CCSet(" ")       .Equals(CCSet("A.B.C.D" )), Is.False);
            Assert.That(CCSet("" )       .Equals(CCSet("A.B.C.D" )), Is.False);
        }

        [Test]
        public void ComposeTest()
        {
            Assert.That(CCSet("A.B.C.D") .Compose(), Is.EqualTo("A.B.C.D"));
            Assert.That(CCSet("A.B.C.D") .Compose(), Is.Not.EqualTo("A/B/C/D"));
            Assert.That(CCSet("A/B/C/D") .Compose(), Is.Not.EqualTo("A.B.C.D"));
            Assert.That(CCSet("A.B.CD")  .Compose(), Is.Not.EqualTo("A.B.C.D"));
            Assert.That(CCSet("A.B.C.C") .Compose(), Is.Not.EqualTo("A.B.C.D"));
            Assert.That(CCSet("A.B.C.D.").Compose(), Is.Not.EqualTo("A.B.C.D"));
            Assert.That(CCSet("0.B.C.D") .Compose(), Is.Not.EqualTo("A.B.C.D"));
            Assert.That(CCSet("a.B.C.D") .Compose(), Is.Not.EqualTo("A.B.C.D"));
            Assert.That(CCSet("B.B.C.D") .Compose(), Is.Not.EqualTo("A.B.C.D"));
        }

        [Test]
        public void GetSubsetTest()
        {
            var abcd  = CCSet("A.B.C.D");
            Assert.That(abcd             .GetSubset(0,7), Is.EqualTo(CCSet("A.B.C.D")));
            Assert.That(abcd             .GetSubset(0,7), Is.SameAs(abcd));
            Assert.That(CCSet("A.B.C.D") .GetSubset(0,5), Is.EqualTo(CCSet("A.B.C")));
            Assert.That(CCSet("A/B/C/D") .GetSubset(1,2), Is.EqualTo(CCSet("/B")));
            Assert.That(CCSet("A.B.C.D") .GetSubset(2,0), Is.EqualTo(CCSet("")));
        }

        [Test]
        public void FindIndexTest()
        {
            // FindFirstIndex
            Assert.AreEqual(CCSet("A.B.C.D") .FindFirstIndex(CCSet("A")),  0);
            Assert.AreEqual(CCSet("A.B.C.D") .FindFirstIndex(CCSet("B")),  2);
            Assert.AreEqual(CCSet("A.B.C/D") .FindFirstIndex(CCSet("D")),  6);
            Assert.AreEqual(CCSet("A.B.CD")  .FindFirstIndex(CCSet("CD")), 4);
            Assert.AreEqual(CCSet("AB.C.C")  .FindFirstIndex(CCSet("AB")), 0);
            Assert.AreEqual(CCSet("A.BC.D.") .FindFirstIndex(CCSet("BC")), 2);
            Assert.AreEqual(CCSet("....")    .FindFirstIndex(CCSet("..")), 0);
            Assert.AreEqual(CCSet("A...B")   .FindFirstIndex(CCSet("..")), 1);
            Assert.AreEqual(CCSet("B.B.C.D") .FindFirstIndex(CCSet("A")), -1);
            Assert.AreEqual(CCSet("AB.C.C")  .FindFirstIndex(CCSet("C")),  2);
            Assert.AreEqual(CCSet("")        .FindFirstIndex(CCSet("C")), -1);
            Assert.AreEqual(CCSet("")        .FindFirstIndex(CCSet("")),  -1);
            Assert.AreEqual(CCSet("A")       .FindFirstIndex(CCSet("")),  -1);

            // FindLastIndex
            Assert.AreEqual(CCSet("A.B.C.D") .FindLastIndex(CCSet("A")),  0);
            Assert.AreEqual(CCSet("A.B.C.D") .FindLastIndex(CCSet("B.C")),2);
            Assert.AreEqual(CCSet("C.C.D.D") .FindLastIndex(CCSet("C.C")),0);
            Assert.AreEqual(CCSet("A.B.C/D") .FindLastIndex(CCSet("D")),  6);
            Assert.AreEqual(CCSet("A.B.CD")  .FindLastIndex(CCSet("CD")), 4);
            Assert.AreEqual(CCSet("AB.C.C")  .FindLastIndex(CCSet("C")),  4);
            Assert.AreEqual(CCSet("....")    .FindLastIndex(CCSet("..")), 2);
            Assert.AreEqual(CCSet("A...B")   .FindLastIndex(CCSet("..")), 2);
        }

        [Test]
        public void ContainsTest()
        {
            // True
            Assert.IsTrue (CCSet("A.B.C.D") .Contains(CCSet("A.B.C.D")));
            Assert.IsTrue (CCSet("A.B.C.D") .Contains(CCSet("B.C")));
            Assert.IsTrue (CCSet("A.B.C/D") .Contains(CCSet("D")));
            Assert.IsTrue (CCSet("A.B.CD")  .Contains(CCSet("CD")));
            Assert.IsTrue (CCSet("AB.C.C")  .Contains(CCSet("AB.C")));
            Assert.IsTrue (CCSet("A.BC.D.") .Contains(CCSet("BC")));
            Assert.IsTrue (CCSet("....")    .Contains(CCSet("..")));
            Assert.IsTrue (CCSet("A...B")   .Contains(CCSet("..")));
            Assert.IsTrue (CCSet("AB.C.C")  .Contains(CCSet("C")));

            // False
            Assert.IsFalse (CCSet("B.B.C.D").Contains(CCSet("A")));
            Assert.IsFalse (CCSet("AB.C.C") .Contains(CCSet("")));
            Assert.IsFalse (CCSet("AB.C.C") .Contains(CCSet("A.B.C")));
            Assert.IsFalse (CCSet("AB.C.C") .Contains(CCSet("C.c")));
            Assert.IsFalse (CCSet("AB.C.C") .Contains(CCSet("CC")));
            Assert.IsFalse (CCSet("")       .Contains(CCSet("A")));
            Assert.IsFalse (CCSet("")       .Contains(CCSet("X")));
        }
    }

    /*public class ComposedSetPerformanceTest
    {
        private string testText;

        [SetUp]
        public void LoadTestData()
        {
            if(testText == null)
                testText = File.ReadAllText("./Packages/org.kjems.uvc/UVC.Tests/test.dat");
        }

        [PerformanceTest, Version("1")]
        public void DecomposeTest()
        {
            Measure.Method(() => new ComposedString(testText)).Run();
        }

        [PerformanceTest, Version("1")]
        public void ComposeTest()
        {
            var cs = new ComposedString(testText);
            Measure.Method(() => cs.Compose()).Run();
        }
    }*/
}
