// (c) HMMüller 2006...2015

using System;

namespace NDepCheck {
    public interface ISectionAttribute {
    }
}

namespace NDepCheck.TestAssembly.For.Attributes {
    public class MySectionAttribute : Attribute, ISectionAttribute {
        public const string ITEM_TYPE = "MY_ITEM_TYPE";
        public string SectionA { get; set; }
        public string SectionB { get; set; }
        public string SectionC { get; set; }
    }

    [MySection(SectionB = "BB")]
    public class WithSectionB { }

    [MySection(SectionC = "CC")]
    public class WithSectionC { }

    [MySection(SectionB = "B2", SectionC = "C1")]
    public class WithSectionBC { }

    [MySection(SectionC = "C3", SectionB = "B4")]
    public class WithMethods {
        public void M1() {
            var x = "123";
            Console.Out.WriteLine(x);            
        }

        [MySection(SectionB = "B")]
        public void M2() {
            var y = "123";
            Console.Out.WriteLine(y);
        }
    }

    [MySection(SectionB = "B5")]
    public class WithInnerTypes {
        [MySection(SectionC = "C4", SectionB = "B6")]
        public class InnerType {
            [MySection(SectionB = "B7")]
            public class InnerInnerType {
                public void M3() {
                    var z = "123";
                    Console.Out.WriteLine(z);
                }
            }            
        }
    }
}

