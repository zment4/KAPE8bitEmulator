using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static KAPE8bitEmulator.CPU_6502.Instructions;
using System.IO;
using System.Globalization;
using System.Numerics;
using System.Runtime.Intrinsics;

namespace KAPE8bitEmulator
{
    public partial class CPU_6502
    {
        const long TARGET_HZ = 2000000;
        //const long TARGET_HZ = 14000;

        public AutoResetEvent ResetFinished;

        class MemoryMappedWrite
        {
            public UInt16 StartAddress;
            public UInt16 EndAddress;
            public Action<UInt16, byte> Write;
        }
        List<MemoryMappedWrite> WriteMap = new List<MemoryMappedWrite>();
        Func<UInt16, byte>[] ReadMap = new Func<UInt16, byte>[65536];

        bool irqTriggered = false;

        Instructions instructions;

        public CPU_6502()
        {
            instructions = new Instructions(this);
            InitDebugCommands();
        }

        public void TriggerIRQ()
        {
            // IRQ requested by peripheral
            if (KAPE8bitEmulator.DebugMode || KAPE8bitEmulator.TraversalMode)
                Console.WriteLine("IRQ requested by peripheral");

            irqTriggered = true;
        }

        public void RegisterRead(UInt16 startAddress, UInt16 endAddress, Func<UInt16, byte> action)
        {
            for (int i = startAddress; i <= endAddress; i++)
            {
                // Do not clobber an existing registration — devices registered earlier should keep precedence.
                if (ReadMap[(UInt16)i] == null)
                    ReadMap[(UInt16) i] = action;
            }
        }
        public void RegisterWrite(UInt16 startAddress, UInt16 endAddress, Action<UInt16, byte> action)
        {
            WriteMap.Add(new MemoryMappedWrite() { StartAddress = startAddress, EndAddress = endAddress, Write = action });
            if (KAPE8bitEmulator.DebugMode)
                Console.WriteLine($"RegisterWrite: ${startAddress:X4}-${endAddress:X4}");
        }

        byte Read(UInt16 address)
        {
            var handler = ReadMap[address];
            if (handler == null)
            {
                if (KAPE8bitEmulator.DebugMode || KAPE8bitEmulator.TraversalMode)
                    Console.WriteLine($"Read: no handler for ${address:X4}");
                return 0;
            }
            var val = handler(address);
            if (KAPE8bitEmulator.DebugMode)
            {
                if (address >= 0x0100 && address <= 0x01FF)
                    Console.WriteLine($"ReadStack: addr ${address:X4} -> ${val:X2} S:${S:X2}");
                if (address == 0xfffe || address == 0xffff)
                    Console.WriteLine($"ReadVector: addr ${address:X4} -> ${val:X2}");
            }
            if (KAPE8bitEmulator.TraversalMode)
            {
                if (address == 0x0000)
                    Console.WriteLine($"[TRAV] Read @ $0000 -> ${val:X2}");
                if (address == 0xfffe || address == 0xffff)
                    Console.WriteLine($"[TRAV] ReadVector byte @ ${address:X4} -> ${val:X2}");
            }
            return val;
        }

        void Write(UInt16 address, byte value)
        {
            foreach (var memWrite in WriteMap.Where(x =>
            address >= x.StartAddress && address <= x.EndAddress)) {
                if (KAPE8bitEmulator.DebugMode && address >= 0x0100 && address <= 0x01FF)
                    Console.WriteLine($"WriteStack: addr ${address:X4} <- ${value:X2} S:${S:X2}");
                memWrite.Write(address, value);
            }
        }

        // Expose a way for emulator components to write into memory via CPU API
        public void WriteMemory(UInt16 address, byte value)
        {
            Write(address, value);
        }

        public void WriteMemory16(UInt16 address, UInt16 value)
        {
            Write(address, (byte)(value & 0xFF));
            Write((UInt16)(address + 1), (byte)((value >> 8) & 0xFF));
        }

        // Registers
        UInt16 PC;
        byte A, X, Y, S, P;

        // Debug accessors
        public UInt16 DebugPC => PC;
        public string DebugStateString() => $"PC:${PC:X4} A:${A:X2} X:${X:X2} Y:${Y:X2} S:${S:X2} P:{Convert.ToString(P,2).PadLeft(8,'0')} nmiTriggered:{nmiTriggered} insideNMI:{insideNMI} insideIRQ:{insideIRQ}";

        public void SetPC(UInt16 newPC)
        {
            if (KAPE8bitEmulator.DebugMode || KAPE8bitEmulator.TraversalMode)
            {
                Console.WriteLine($"SetPC: ${PC:X4} -> ${newPC:X4} -- state: {DebugStateString()}");
            }
            PC = newPC;
            if ((KAPE8bitEmulator.DebugMode || KAPE8bitEmulator.TraversalMode) && PC == 0)
            {
                Console.WriteLine("*** ALERT: PC set to $0000 ***");
                PrintRegisters();
                DumpStackTop(32);
                try
                {
                    Console.WriteLine("Memory $0000..$0010:");
                    for (int i = 0; i < 16; i++) Console.Write($"{Read((UInt16)i):X2} ");
                    Console.WriteLine();
                }
                catch { }
            }
        }

        bool nmiTriggered;
        bool insideNMI;
        bool insideIRQ;

        public bool HideNMIMessages = true;

        public void TriggerNMI()
        {
            if (haltRequested)
                return;

            nmiTriggered = true;
        }

        // Get reset address and set it to PC
        public void Reset()
        {
            var hi = Read(0xfffd);
            var lo = Read(0xfffc);

            PC = (UInt16) (hi << 8 | lo);

            if (KAPE8bitEmulator.DebugMode || KAPE8bitEmulator.TraversalMode)
                Console.WriteLine($"CPU reading reset vector, got: ${PC:X4}");

            insideNMI = false;
            nmiTriggered = false;
            insideIRQ = false;
            irqTriggered = false;

            A = X = Y = 0;
            P = 0b00100000;
            S = 0xff;
        }

        byte FetchInstruction()
        {
            return Read(PC++);
        }

        byte FetchOperand()
        {
            return Read(PC++);
        }

        // NOTE:XXX: This can create side-effects! Some added peripherals reset their IRQ when read from a specific address
        byte Peek(UInt16 addr)
        {
            return Read(addr);
        }

        enum DebugState
        {
            Continue,
            Step
        }

        DebugState debugState = DebugState.Step;

        void RunCycle()
        {
            byte instruction = Read(PC);
            var instr = instructions[instruction];
            if (KAPE8bitEmulator.TraversalMode)
            {
                Console.WriteLine($"[TRAV] Fetch @ ${PC:X4} -> ${instruction:X2}");
            }

            if (KAPE8bitEmulator.DebugMode && !insideNMI)
            {
                HandleDebugState(instruction, instr);
            }

            if (instr == null)
            {
                // Non-fatal handling for unimplemented/unknown opcodes:
                // Log once and treat unknown opcode as a 1-byte NOP so
                // execution can continue for testing. This is a pragmatic
                // short-term fix — later we should implement the real
                // instruction or reject unsupported ROMs.
                Console.WriteLine($"Unhandled opcode ${instruction:X2} at ${PC:X4} - treating as NOP");
                PC++;
                currentCycles += 2; // approximate small cost
                return;
            }

            PC++;
            instr.Action();
            currentCycles += instr.Cycles;
        }

        private void HandleDebugState(byte instruction, InstructionDescriptor instr)
        {
            if (debugState == DebugState.Continue && IsBreakpoint())
            {
                Console.WriteLine($"Breakpoint hit at ${PC:X4}");
                debugState = DebugState.Step;
            }

            if (debugState == DebugState.Step)
            {
                PrintRegisters();
                PrintInstructionAndOpCodes(instruction, instr);

                DebugPrompt();
            }
        }

        public bool DebugCommandNext(string[] words)
        {
            debugState = DebugState.Step;
            return false;
        }

        public bool DebugCommandBreakpoint(string[] words)
        {
            if (words.Length == 1)
            {
                Console.WriteLine("No breakpoint address provided.");
                return true;
            }

            Action listBreakpoints = () =>
            {
                for (int i = 0; i < BreakpointAddressList.Count; i++)
                {
                    Console.WriteLine($"{(i+1).ToString().PadLeft(2, ' ')}: ${BreakpointAddressList[i]:X4}");
                }
            };

            Action addBreakpoint = () =>
            {
                if (words[2].Length == 5 && words[2][0] == '$')
                {
                    UInt16 bpAddress = 0;
                    if (UInt16.TryParse(words[2].Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bpAddress))
                    {
                        BreakpointAddressList.Add(bpAddress);
                        Console.WriteLine($"Added breakpoint {BreakpointAddressList.Count} at ${bpAddress:X4}");
                    }
                }
                else Console.WriteLine($"Can't add breakpoint {words[2]}");
            };

            Action removeBreakpoint = () =>
            {
                int bpIndex = 0;
                if (int.TryParse(words[2], out bpIndex))
                {
                    bpIndex--;
                    if (bpIndex < BreakpointAddressList.Count && bpIndex >= 0)
                    {
                        var bpAddress = BreakpointAddressList[bpIndex];
                        BreakpointAddressList.RemoveAt(bpIndex);
                        Console.WriteLine($"Removed breakpoint {bpIndex + 1}: ${bpAddress:X4}");
                    }
                    else Console.WriteLine($"No breakpoint {bpIndex} set.");
                }
                else Console.WriteLine($"Could not parse '{words[2]}' as a breakpoint ID.");
            };

            switch (words[1])
            {
                case "a":
                    addBreakpoint();
                    break;
                case "add":
                    addBreakpoint();
                    break;
                case "remove":
                    removeBreakpoint();
                    break;
                case "r":
                    removeBreakpoint();
                    break;
                case "list":
                    listBreakpoints();
                    break;
                case "l":
                    listBreakpoints();
                    break;
            }

            return true;
        }

        public bool DebugCommandMemory(string[] words)
        {
            if (words.Length < 2)
            {
                Console.WriteLine("No parameters provided.");
                Console.WriteLine("USAGE: m|memory <address> [<count>]");
                return true;
            }


            UInt16 mAddress = 0;
            if (words[1].Length != 5 ||
                words[1][0] != '$' || 
                !UInt16.TryParse(words[1].Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out mAddress))
            {
                Console.WriteLine($"Invalid memory address '{words[1]}'");
                return true;
            }

            int count = 1;
            if (words.Length == 3 && !int.TryParse(words[2], out count))
            {
                Console.WriteLine($"Invalid count");
            }

            while (count > 0)
            {
                Console.Write($"{mAddress:X4}: ");

                int curCount = 0;
                while (curCount < 32)
                {
                    Console.Write($"{Read(mAddress):X2} ");
                    mAddress++;
                    curCount++;
                    count--;
                    if (count == 0)
                        break;
                }

                Console.WriteLine();
            }

            return true;
        }

        public bool DebugCommandContinue(string[] words)
        {
            debugState = DebugState.Continue;

            return false;
        }

        public bool DebugCommandHelp(string[] words)
        {
            Console.WriteLine("Available commands:");
            foreach(var debugCommand in debugCommands)
            {
                Console.WriteLine($"\t{debugCommand.Key}");
            }

            return true;
        }

        Dictionary<string, Func<string[], bool>> debugCommands = new Dictionary<string, Func<string[], bool>>();

        void InitDebugCommands()
        {
            debugCommands = new Dictionary<string, Func<string[], bool>>()
            {
                { "next", DebugCommandNext },
                { "n", DebugCommandNext },
                { "breakpoint", DebugCommandBreakpoint },
                { "bp", DebugCommandBreakpoint },
                { "memory", DebugCommandMemory },
                { "m", DebugCommandMemory },
                { "continue", DebugCommandContinue },
                { "c", DebugCommandContinue },
                { "help", DebugCommandHelp },
                { "h", DebugCommandHelp }
            };
        }

        string lastDebugCommand = "";
        void DebugPrompt()
        {
            var showPrompt = true;
            while (showPrompt)
            {
                Console.Write("> ");

                var commandline = Console.ReadLine();
                if (String.IsNullOrEmpty(commandline))
                {
                    commandline = lastDebugCommand;
                }

                var words = commandline.ToLower().Split(' ');
                if (!String.IsNullOrEmpty(words[0]))
                {
                    if (debugCommands.ContainsKey(words[0]))
                        showPrompt = debugCommands[words[0]](words);
                    else
                        Console.WriteLine($"Unknown command {words[0]}.");
                }

                lastDebugCommand = commandline;
            }
        }

        List<UInt16> BreakpointAddressList = new List<UInt16>();
        private bool IsBreakpoint() => BreakpointAddressList.Contains(PC);

        private void PrintInstructionAndOpCodes(byte instruction, InstructionDescriptor instr)
        {
            PC++;
            Console.Write($"${instruction:X2}\t");
            if (instr == null)
            {
                Console.WriteLine("NOT SUPPORTED");
                return;
            }
            Console.Write($"{instr.Mnemonic}\t");
            UInt16 addr = (UInt16)((Peek((UInt16)(PC + 1)) << 8) | Peek(PC)); ;

            switch (instr.AddressingMode)
            {
                case AddressingModeEnum.Absolute:
                    Console.WriteLine($"${addr:X4}\t=${Peek(addr):X2}");
                    break;
                case AddressingModeEnum.Immediate:
                    Console.WriteLine($"#${Peek(PC):X2}");
                    break;
                case AddressingModeEnum.Implied:
                    Console.WriteLine();
                    break;
                case AddressingModeEnum.Relative:
                    var rel = (sbyte)Peek(PC);
                    Console.WriteLine($"#{rel} (${PC+rel+1:X4})");
                    break;
                case AddressingModeEnum.ZeroPage:
                    Console.WriteLine($"${Peek(PC):X2}\t=${Peek(Peek(PC)):X2}");
                    break;
                case AddressingModeEnum.IndirectIndexed:
                    Console.WriteLine($"(${Peek(PC):X2}),Y\t=${((Peek((UInt16) (Peek(PC)+1)) << 8) | Peek(Peek(PC))) + Y:X4}");
                    break;
                case AddressingModeEnum.AbsoluteIndexedX:
                    addr += X;
                    Console.WriteLine($"${addr:X4}");
                    break;
                case AddressingModeEnum.Accumulator:
                    Console.WriteLine("A");
                    break;
                case AddressingModeEnum.AbsoluteIndirect:
                    Console.WriteLine($"(${addr:X4})\t=${Peek(addr):X2}{Peek((UInt16) (addr+1)):X2}");
                    break;
            }

            Console.WriteLine();

            PC--;
        }

        void PrintRegisters()
        {
            Console.Write($"PC: ${PC:X4} ");
            Console.Write($"A: ${A:X2} ");
            Console.Write($"X: ${X:X2} ");
            Console.Write($"Y: ${Y:X2} ");
            Console.Write($"S: ${S:X2} ");
            Console.Write($"P: {Convert.ToString(P, 2).PadLeft(8,'0')}\n\t\t");
        }

        long currentCycles = 0;
        public int CurrentCyclesPerSecond = 0;
        long currentNMI = 0;
        public int CurrentNMIPerSecond = 0;
        long cyclesLastSecondStart = 0;
        long nmiLastSecondStart = 0;

        public void EnterCycleLoop()
        {
            new Task(() =>
            //new Thread(() =>
            {
                Stopwatch measureSW = new Stopwatch();
                Stopwatch limiterSW = new Stopwatch();
                limiterSW.Start();

                if (Program.Args.Contains("-wait"))
                {
                    Console.WriteLine("Press any key to start emulator...");
                    Console.ReadKey();
                }

                if (KAPE8bitEmulator.DebugMode)
                    Console.WriteLine("Ready.");

                var measureTimer = new Timer((o) =>
                {
                    CurrentCyclesPerSecond = (int)Math.Round((currentCycles - cyclesLastSecondStart) * measureSW.Elapsed.TotalSeconds);
                    cyclesLastSecondStart = currentCycles;
                    CurrentNMIPerSecond = (int)Math.Round((currentNMI - nmiLastSecondStart) * measureSW.Elapsed.TotalSeconds);
                    nmiLastSecondStart = currentNMI;
                    measureSW.Restart();
                });

                measureSW.Start();
                measureTimer.Change(0, 1000);

                while (true)
                {
                    // Halt, stopped with Stop() and restarted with Start()
                    while (haltRequested)
                    {
                        haltAcknowledged = true;
                        //Thread.Sleep(100);
                        Task.Delay(100).Wait();
                    }

                    RunCycle();

                    // sleep until we haven't been too fast
                    while (currentCycles >= (limiterSW.Elapsed.TotalSeconds * TARGET_HZ))
                    {
                        Task.Delay(1).Wait();
                        //Thread.Sleep(1);
                    }

                    // Check NMI first
                    if (nmiTriggered && !insideNMI) EnterNMI();

                    // Then check IRQ
                    if (!insideIRQ && !insideNMI && !IsIntDisable() && irqTriggered)
                    {
                        irqTriggered = false;
                        EnterIRQ();
                    }
                }
            })
            //}) { IsBackground = true }
            .Start();
        }

        public Mutex Halt = new Mutex();

        private void EnterNMI()
        {
            currentNMI++;

            if (KAPE8bitEmulator.DebugMode && !HideNMIMessages)
                Console.WriteLine($"Entering NMI at ${PC:X4}");

            var retHi = (byte)((PC >> 8) & 0xff);
            var retLo = (byte)(PC & 0xff);
            var pushedP = P;

            PushAddress(PC);
            Push(P);

            if (KAPE8bitEmulator.DebugMode && !HideNMIMessages)
            {
                Console.WriteLine($"Pushed return hi:${retHi:X2} lo:${retLo:X2} P:${pushedP:X2}");
                DumpStackTop(8);
            }

            var hi = Read(0xfffb);
            var lo = Read(0xfffa);

            if (KAPE8bitEmulator.DebugMode && !HideNMIMessages)
                Console.WriteLine($"NMI vector raw hi:${hi:X2} lo:${lo:X2}");

            PC = (UInt16)(hi << 8 | lo);

            if (KAPE8bitEmulator.DebugMode && !HideNMIMessages)
                Console.WriteLine($"CPU reading NMI vector, got: ${PC:X4}");

            insideNMI = true;
            currentCycles += 7;
        }

        private void EnterIRQ()
        {
            if (KAPE8bitEmulator.DebugMode || KAPE8bitEmulator.TraversalMode)
                Console.WriteLine($"[TRAV] Entering IRQ at ${PC:X4} -- S=${S:X2} P=${P:X2}");

            // Read IRQ vector first
            var vecHi = Read(0xffff);
            var vecLo = Read(0xfffe);
            var vec = (UInt16)(vecHi << 8 | vecLo);

            // Normal IRQ entry: push and transfer to vector
            var retHi2 = (byte)((PC >> 8) & 0xff);
            var retLo2 = (byte)(PC & 0xff);
            var pushedP2 = P;

            PushAddress(PC);
            Push(P);

            if (KAPE8bitEmulator.TraversalMode)
                Console.WriteLine($"[TRAV] normal push -> return=${PC:X4} P=${P:X2} S=${S:X2}");

            PC = vec;

            if (KAPE8bitEmulator.TraversalMode)
            {
                Console.WriteLine($"[TRAV] IRQ vector -> PC set to ${PC:X4}");
                try
                {
                    Console.Write("[TRAV] Prefetch bytes at PC: ");
                    for (int i = 0; i < 8; i++)
                    {
                        Console.Write($"{Read((UInt16)(PC + i)):X2} ");
                    }
                    Console.WriteLine();
                }
                catch { }
            }

            insideIRQ = true;
            currentCycles += 7;
        }

        void Push(byte b)
        {
            if (KAPE8bitEmulator.TraversalMode)
                Console.WriteLine($"[TRAV] Push byte ${b:X2} to addr ${ (0x0100 | S):X4} S(before)=${S:X2}");
            Write((UInt16)(0x0100 | S--), b);
        }

        byte Pull()
        {
            return Read((UInt16)(0x0100 | ++S));
        }

        UInt16 PullAddress()
        {
            return (UInt16)(Pull() | (Pull() << 8));
        }

        private void PushAddress(UInt16 addr)
        {
            Push((byte)((addr >> 8) & 0xff));
            Push((byte)(addr & 0xff));
        }

        private UInt16 FetchAbsoluteAddress()
        {
            var lo = FetchOperand();
            var hi = FetchOperand();
            return (UInt16)(hi << 8 | lo);
        }

        private void PrintStack()
        {
            Console.WriteLine("Stack");
            for (int i = 0, offs = 0x0100; i < 16; i++)
            {
                for (int k = 0; k < 16; k++, offs++)
                    Console.Write($"{Read((UInt16) offs):X2}{(S == i ? '*' : ' ')}");
                Console.WriteLine("");
            }
        }

        public void DumpStackTop(int count = 8)
        {
            try
            {
                // Top-most pushed byte is at 0x0100 | (S + 1)
                int startIndex = S + 1;
                Console.Write("Stack top: ");
                for (int i = 0; i < count; i++)
                {
                    int idx = startIndex + i;
                    if (idx > 0xff) break;
                    var addr = (UInt16)(0x0100 | idx);
                    Console.Write($"${addr:X4}={Read(addr):X2} ");
                }
                Console.WriteLine("");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DumpStackTop failed: {ex.Message}");
            }
        }

        private UInt16 ReadAddress(UInt16 addr)
        {
            return (UInt16) (Read(addr) | (Read((UInt16) (addr + 1)) << 8));
        }

        private UInt16 FetchIndirectIndexedAddress()
        {
            bool pageBoundaryCrossed;
            return FetchIndirectIndexedAddress(out pageBoundaryCrossed);
        }

        private UInt16 FetchIndirectIndexedAddress(out bool pageBoundaryCrossed)
        {
            UInt16 addr = ReadAddress((UInt16)FetchOperand());
            int oldH = addr & 0xff00;
            addr = (UInt16)(addr + Y);
            pageBoundaryCrossed = (addr & 0xff00) != oldH;

            return addr;
        }
        private UInt16 FetchAbsoluteIndexedAddress(byte index, out bool pageBoundaryCrossed)
        {
            UInt16 addr = FetchAbsoluteAddress();
            int oldH = addr & 0xff00;
            addr = (UInt16)(addr + index);
            pageBoundaryCrossed = (addr & 0xff00) != oldH;

            return addr;
        }

        bool haltRequested = false;
        bool haltAcknowledged = false;

        public bool IsRunning => !haltRequested;

        internal void Stop()
        {
            haltRequested = true;
            while (!haltAcknowledged);
            haltAcknowledged = false;

            Task.Delay(10).Wait();
            //Thread.Sleep(10);
        }

        internal void Start()
        {
            haltRequested = false;
        }
    }
}
