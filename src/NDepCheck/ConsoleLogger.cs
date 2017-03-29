using System;

namespace NDepCheck {
    internal class ConsoleLogger : AbstractLogger {
        protected override void DoWriteError(string msg) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Out.WriteLine(msg);
            Console.ResetColor();
        }

        protected override void DoWriteWarning(string msg) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Out.WriteLine(msg);
            Console.ResetColor();
        }

        public override void WriteInfo(string msg) {
            Console.Out.WriteLine(msg);
        }

        public override void WriteDebug(string msg) {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Out.WriteLine(msg);
            Console.ResetColor();
        }
    }
}