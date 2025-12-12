using System;
using System.IO;
using System.Threading;

namespace KAPE8bitEmulator
{
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        public const string FIXED_NAME = "Battlesnake";

        public static bool HasEmbeddedBinary = false;
        public static bool IsHeadless = false;
        public static int HeadlessTimeoutMs = 5000;
        public static string HeadlessDumpPath = null;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            HasEmbeddedBinary = args.Length == 0;

            Console.WriteLine("KAPE-8bit 6502 Emulator (C) 2022 zment\n");

            Args = args;
            IsHeadless = args.Contains("--headless");

            // Parse --timeout (default 5000ms)
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--timeout" && int.TryParse(args[i + 1], out int timeout))
                {
                    HeadlessTimeoutMs = timeout;
                    break;
                }
            }

            // Parse --dump <path>
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--dump")
                {
                    HeadlessDumpPath = args[i + 1];
                    break;
                }
            }

            if (IsHeadless)
            {
                RunHeadless();
            }
            else
            {
                using (var game = new KAPE8bitEmulator())
                    game.Run();
            }
        }

        private static void RunHeadless()
        {
            Console.WriteLine("KAPE8bitEmulator v1.0 - Running in headless mode");
            Console.WriteLine($"Timeout: {HeadlessTimeoutMs}ms");
            if (!string.IsNullOrEmpty(HeadlessDumpPath))
            {
                Console.WriteLine($"Memory dump output: {HeadlessDumpPath}");
            }
            Console.WriteLine();
            
            string fileName = FIXED_NAME;
            if (!HasEmbeddedBinary && Args.Length > 0)
            {
                // Find the binary file argument (usually after --run or first non-flag arg)
                for (int i = 0; i < Args.Length; i++)
                {
                    if (Args[i] == "--run" && i + 1 < Args.Length)
                    {
                        fileName = Args[i + 1];
                        break;
                    }
                    else if (!Args[i].StartsWith("--") && i > 0 && !Args[i - 1].StartsWith("--"))
                    {
                        fileName = Args[i];
                        break;
                    }
                }
            }

            // Initialize emulator components
            var sram = new SRAM64k();
            if (HasEmbeddedBinary)
            {
                Console.WriteLine("[Headless] Loading embedded binary");
                sram.FillFromEmbeddedBinary();
            }
            else
            {
                Console.WriteLine($"[Headless] Loading binary: {fileName}");
                sram.FillRam(fileName);
            }

            var cpu = new CPU_6502();
            sram.RegisterMap(cpu);

            // Create a minimal GPU proxy that just handles writes without graphics
            var gpuProxy = new HeadlessGPUProxy();
            gpuProxy.RegisterWrite(cpu);

            Console.WriteLine("[Headless] Starting CPU execution...");
            cpu.Reset();
            cpu.Start();

            // Run for timeout duration or until CPU halts
            long startTick = DateTime.UtcNow.Ticks;
            long timeoutTicks = (long)HeadlessTimeoutMs * 10000; // Convert ms to 100ns ticks

            try
            {
                while (true)
                {
                    long elapsed = DateTime.UtcNow.Ticks - startTick;
                    if (elapsed >= timeoutTicks)
                    {
                        Console.WriteLine($"[Headless] Timeout after {HeadlessTimeoutMs}ms");
                        break;
                    }

                    if (!cpu.IsRunning)
                    {
                        Console.WriteLine("[Headless] CPU halted");
                        break;
                    }

                    Thread.Sleep(10); // Small sleep to prevent busy-loop
                }
            }
            finally
            {
                cpu.Stop();

                // Dump memory if requested
                if (!string.IsNullOrEmpty(HeadlessDumpPath))
                {
                    try
                    {
                        var ram = sram.GetRamCopy();
                        File.WriteAllBytes(HeadlessDumpPath, ram);
                        Console.WriteLine($"[Headless] Memory dump written to {HeadlessDumpPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Headless] Failed to write memory dump: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Minimal GPU proxy for headless mode - handles GPU writes without graphics rendering.
        /// </summary>
        private class HeadlessGPUProxy
        {
            // Minimal implementation - just enough to satisfy CPU write requests
            public void RegisterWrite(CPU_6502 cpu)
            {
                // Register GPU address range (0x8000-0xbfff) to absorb writes
                cpu.RegisterWrite(0x8000, 0xbfff, (address, value) => 
                {
                    // Silently absorb GPU writes in headless mode
                    if (KAPE8bitEmulator.DebugMode)
                        Console.WriteLine($"[GPU] Write to ${address:X4}: ${value:X2}");
                });
            }
        }

        public static string[] Args;
    }
}
