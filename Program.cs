using System;
using System.Data;
using System.IO;
using System.Threading;
using Microsoft.VisualBasic;

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
        public static string FileName = "";

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("KAPE-8bit 6502 Emulator (C) 2022 zment\n");

            Args = args;

            var parsed = ArgParser.Parse(args ?? Array.Empty<string>());

            // Flags
            IsHeadless = parsed.Switches.ContainsKey("headless");
            if (parsed.Switches.TryGetValue("timeout", out var t) && int.TryParse(t, out int to))
                HeadlessTimeoutMs = to;
            if (parsed.Switches.TryGetValue("dump", out var dp))
                HeadlessDumpPath = dp;
            if (parsed.Switches.ContainsKey("debug"))
                KAPE8bitEmulator.DebugMode = true;
            // Lightweight traversal tracing (Push/IRQ/PushKey)
            if (parsed.Switches.ContainsKey("trace"))
                KAPE8bitEmulator.TraversalMode = true;

            // Determine binary filename (explicit --run takes precedence)
            FileName = parsed.FileName;
            HasEmbeddedBinary = string.IsNullOrEmpty(FileName);

            // Validate explicit --run usage: if user supplied --run but no filename, error out
            if (parsed.Switches.ContainsKey("run") && string.Equals(parsed.Switches["run"], "true", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Error: '--run' specified but no filename provided. Use '--run <file>' or provide the filename positionally.");
                Environment.Exit(1);
            }
            // Print args for visibility (kept from previous behavior)
            for (int i = 0; i < Args.Length; i++) Console.WriteLine($"Arg {i}: {Args[i]}");

            if (IsHeadless)
            {
                RunHeadless();
                // Ensure process terminates cleanly after headless run
                Environment.Exit(0);
            }
            else
            {
                using (var game = new KAPE8bitEmulator())
                    game.Run();
                // Ensure process terminates cleanly after normal run
                Environment.Exit(0);
            }
        }

        private static void RunHeadless()
        {
            Console.WriteLine("Running in headless mode");
            Console.WriteLine($"Timeout: {HeadlessTimeoutMs}ms");
            if (!string.IsNullOrEmpty(HeadlessDumpPath))
            {
                Console.WriteLine($"Memory dump output: {HeadlessDumpPath}");
            }
            Console.WriteLine();

            // Initialize emulator components
            var sram = new SRAM64k();
            if (HasEmbeddedBinary)
            {
                Console.WriteLine("[Headless] Loading embedded binary");
                sram.FillFromEmbeddedBinary();
            }
            else
            {
                Console.WriteLine($"[Headless] Loading binary: {FileName}");
                sram.FillRam(FileName);
            }

            var cpu = new CPU_6502();
            // Create a headless keyboard device so we can simulate key events
            // for testing. Create the keyboard before mapping SRAM so the
            // keyboard's read/write handlers take precedence over the SRAM
            // fall-through handlers.
            var keyboard = new KeyboardDevice(cpu);

            sram.RegisterMap(cpu);

            // Create a minimal GPU proxy that just handles writes without graphics
            var gpuProxy = new HeadlessGPUProxy();
            gpuProxy.RegisterWrite(cpu);

            Console.WriteLine("[Headless] Starting CPU execution...");
            cpu.Reset();
            cpu.Start();
            cpu.EnterCycleLoop();

            // If requested, simulate a keypress to trigger IRQ handling.
            if (Args != null && Array.Exists(Args, a => string.Equals(a, "--simulate-key", StringComparison.OrdinalIgnoreCase)))
            {
                // Enable IRQ via the keyboard control register (bit1 = IRQ enable)
                try
                {
                    cpu.WriteMemory(0xF770, 0x02);
                    // Push ASCII 'A' with key-down bit (we use simple ASCII value)
                    keyboard.PushKey(0x41);
                    Console.WriteLine("[Headless] Simulated keypress 'A' (0x41)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Headless] Failed to simulate key: {ex.Message}");
                }
            }
            
            // Run for timeout duration or until CPU halts
            long startTick = DateTime.UtcNow.Ticks;
            long timeoutTicks = (long)HeadlessTimeoutMs * 10000; // Convert ms to 100ns ticks

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

                Thread.Sleep(100); // Small sleep to prevent busy-loop
            }
            Console.WriteLine("[Headless] Stopping CPU...");
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
            
            Console.WriteLine("[Headless] Exiting...");            
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
