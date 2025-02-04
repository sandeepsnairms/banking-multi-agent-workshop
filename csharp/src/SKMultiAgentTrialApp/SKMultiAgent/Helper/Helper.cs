using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKMultiAgent.Helper
{
    internal static class Logger
    {
        internal static void LogMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine("#[PuginCall] " + message);
            Console.ResetColor();
        }
    }
}
