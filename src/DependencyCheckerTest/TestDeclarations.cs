// (c) HMMüller 2006

using System;

namespace ILDASMDependencyCheckerTest.dir1.dir3 {
    /// <summary>
    /// This and all other classes in and below
    /// the <c>dir1</c> namespace are used for testing
    /// the ILDASMDependencyChecker.
    /// </summary>
    public struct Struct13 {
        public int I;
    }
    public class Class13a {
        public string S;
        public string[][] T;
        public Class13a(string t) {
            T[0][0] = t;
            S = "ILDASMDependencyCheckerTests.dir1.dir3.Class13d";
        }
    }
    public class Class13b {
        public double D;
    }
    public class Class13c {
        public decimal M;
        public Class13c() {
            M = 1.5m * new Random().Next();
        }
    }
    public class Class13d {
        public enum Enum13gInner {
            Inner1,
            Inner2
        };
        Enum13gInner e = Enum13gInner.Inner1;
        public Delegate13 X;
        public Enum13gInner SomeValue() {
            return e;
        }
    }
    public class Class13e {
        public char C;
        public class Class13eInner {
            public bool B;
            public class Class13eInnerInner {
                public float F;
                public Class13d DeepDependency;
            }
        }
    }
    public delegate Class13e Delegate13(Class13d e);
}

public class NamespacelessTestClassForILDASMDependencyChecker {
    public static int I;
    static NamespacelessTestClassForILDASMDependencyChecker() {
        I = new Random().Next();
    }
}

namespace ILDASMDependencyCheckerTest.dir1 {
    using ILDASMDependencyCheckerTest.dir1.dir3;
    public interface SomeInterface {
        Class13a Class13aMethod(Class13b b, Class13c c);
        Class13e.Class13eInner.Class13eInnerInner CreateInner3();
    }

}

namespace ILDASMDependencyCheckerTest.dir1.dir2 {
    using ILDASMDependencyCheckerTest.dir1.dir3;

    public delegate Struct13 SomeDelegate(Class13a param);

    public interface SomeInterface {
        int IntMethod();
        Class13a Class13aMethod(Class13b b, Class13c c);
        Class13e.Class13eInner.Class13eInnerInner CreateInner3();
    }

    public enum SomeEnum {
        Enum1,
        Enum2,
        Enum3
    }; // gibt es hier auch Abhängigkeiten, z.B. EnumConst = ...?

    public struct SomeStruct {
        Class13b _instVar;
        bool b;
        SomeStruct(Delegate13 d) {
            Class13c auxVar = new Class13c();
            _instVar = new Class13b();
            b = auxVar.M == 123.456m;
        }
        internal static Class13e Method(Delegate13 x) {
            Class13e auxVar = x(new Class13d());
            Class13d.Enum13gInner e = Class13d.Enum13gInner.Inner1;
            Console.Out.WriteLine(e.ToString());
            return auxVar;
        }
    }

    public class SomeClass {
        public SomeClass(int i) {
            Class13d auxVar = new Class13d();
            I = auxVar.SomeValue() == Class13d.Enum13gInner.Inner2 ? i : -i;
        }
        internal char SomeMethod() {
            Class13e auxVar = new Class13e();
            bool b = new Class13e.Class13eInner().B;
            return b ? auxVar.C : 'ä';
        }
        int I;
        protected void OtherMethod(Class13d[] d) {
            Class13a auxVar = new Class13a("ILDASMDependencyCheckerTest.dir1.dir3.SomeClass");
            if (auxVar.S == auxVar.T[0][0]) {
                SomeStruct.Method(d[0].X);
            }
        }
        protected internal int AnotherMethod(Class13d d) {
            Class13a auxVar = new Class13a("ILDASMDependencyCheckerTest.dir1.dir3.SomeClass");
            return NamespacelessTestClassForILDASMDependencyChecker.I;
        }
        public SomeInterface YetAnotherMethod(SomeInterface s) {
            return s;
        }
    }
}
