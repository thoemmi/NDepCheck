using NDepCheck.TestAssembly.dir1.dir3;

namespace NDepCheck.TestAssembly.dir1.dir2 {
    public interface ISomeBaseInterface {
        int IntMethod();
    }

    public interface ISomeInterface : ISomeBaseInterface {
        Class13A Class13AMethod(Class13B b, Class13C c);
        Class13E.Class13EInner.Class13EInnerInner CreateInner3();
        char CharProperty { get; set; }
    }
}