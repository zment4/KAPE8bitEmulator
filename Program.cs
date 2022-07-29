using System;
using System.IO;

namespace KAPE8bitEmulator
{
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("No *.bin provided as first argument. Nothing to emulate. Exiting...");

                return;
            }

            Console.WriteLine("KAPE-8bit 6502 Emulator (C) 2022 zment\n");

            Args = args;

            using (var game = new KAPE8bitEmulator())
                game.Run();
        }

        public static string[] Args;
    }
}
