using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static KAPE8bitEmulator.CPU_6502.Instructions;
using System.IO;

namespace KAPE8bitEmulator
{
    public partial class CPU_6502
    {
        const long TARGET_HZ = 4000000;
        //const long TARGET_HZ = 14000;

        public AutoResetEvent ResetFinished;

        class MemoryMappedWrite
        {
            public UInt16 StartAddress;
            public UInt16 EndAddress;
            public Action<UInt16, byte> Write;
        }
        List<MemoryMappedWrite> WriteMap = new List<MemoryMappedWrite>();
        Dictionary<UInt16, Func<UInt16, byte>> ReadMap = new Dictionary<UInt16, Func<UInt16, byte>>();
        List<Func<bool>> IRQMap = new List<Func<bool>>();

        Instructions instructions;

        public CPU_6502()
        {
            instructions = new Instructions(this);
        }

        public void RegisterIRQ(Func<bool> action)
        {
            IRQMap.Add(action);
        }

        public void RegisterRead(UInt16 startAddress, UInt16 endAddress, Func<UInt16, byte> action)
        {
            for (int i = startAddress; i <= endAddress; i++)
            {
                ReadMap[(UInt16) i] = action;
            }
        }
        public void RegisterWrite(UInt16 startAddress, UInt16 endAddress, Action<UInt16, byte> action)
        {
            WriteMap.Add(new MemoryMappedWrite() { StartAddress = startAddress, EndAddress = endAddress, Write = action });
        }

        byte Read(UInt16 address)
        {
            if (ReadMap.ContainsKey(address))
                return ReadMap[address](address);

            return 0x00;
        }

        void Write(UInt16 address, byte value)
        {
            foreach (var memWrite in WriteMap.Where(x =>
            address >= x.StartAddress && address <= x.EndAddress)) {
                memWrite.Write(address, value);
            }
        }

        // Registers
        UInt16 PC;
        byte A, X, Y, S, P;

        bool nmiTriggered;
        bool insideNMI;
        bool insideIRQ;

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

            if (KAPE8bitEmulator.DebugMode)
                Program.consoleOut.WriteLine($"CPU reading reset vector, got: ${PC:X4}");

            resetRequested = false;
            insideNMI = false;
            nmiTriggered = false;

            A = X = Y = 0;
            P = 0b00100000;
            S = 0xff;
        }

        public void RequestReset() => resetRequested = true;

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

        void RunCycle()
        {
            if (KAPE8bitEmulator.DebugMode && !insideNMI)
                    PrintRegisters();

            byte instruction = FetchInstruction();
            var instr = instructions[instruction];

            if (KAPE8bitEmulator.DebugMode && !insideNMI)
                PrintInstructionAndOpCodes(instruction, instr);

            if (instr == null)
            {
                Program.consoleOut.WriteLine("Unhandled opcode! Freezing.");
                PC--;
                PrintRegisters();
                PrintInstructionAndOpCodes(instruction, instr);
                PrintStack();
                Program.consoleOut.Flush();
                Thread.Sleep(Timeout.Infinite);
            }

            instr.Action();
            currentCycles += instr.Cycles;
        }

        private void PrintInstructionAndOpCodes(byte instruction, InstructionDescriptor instr)
        {
            Program.consoleOut.Write($"${instruction:X2}\t");
            if (instr == null)
            {
                Program.consoleOut.WriteLine("NOT SUPPORTED");
                return;
            }
            Program.consoleOut.Write($"{instr.Mnemonic}\t");
            UInt16 addr = (UInt16)((Peek((UInt16)(PC + 1)) << 8) | Peek(PC)); ;

            switch (instr.AddressingMode)
            {
                case AddressingModeEnum.Absolute:
                    Program.consoleOut.WriteLine($"${addr:X4}\t=${Peek(addr):X2}");
                    break;
                case AddressingModeEnum.Immediate:
                    Program.consoleOut.WriteLine($"#${Peek(PC):X2}");
                    break;
                case AddressingModeEnum.Implied:
                    Program.consoleOut.WriteLine();
                    break;
                case AddressingModeEnum.Relative:
                    var rel = (sbyte)Peek(PC);
                    Program.consoleOut.WriteLine($"#${rel} (${PC+rel+1:X4})");
                    break;
                case AddressingModeEnum.ZeroPage:
                    Program.consoleOut.WriteLine($"${Peek(PC):X2}\t=${Peek(Peek(PC)):X2}");
                    break;
                case AddressingModeEnum.IndirectIndexed:
                    Program.consoleOut.WriteLine($"(${Peek(PC):X2}),Y\t=${((Peek((UInt16) (Peek(PC)+1)) << 8) | Peek(Peek(PC))) + Y:X4}");
                    break;
                case AddressingModeEnum.AbsoluteIndexedX:
                    addr += X;
                    Program.consoleOut.WriteLine($"${addr:X4}");
                    break;
                case AddressingModeEnum.Accumulator:
                    Program.consoleOut.WriteLine("A");
                    break;
                case AddressingModeEnum.AbsoluteIndirect:
                    Program.consoleOut.WriteLine($"(${addr:X4})\t=${Peek(addr):X2}{Peek((UInt16) (addr+1)):X2}");
                    break;
            }

            Program.consoleOut.WriteLine();
        }

        void PrintRegisters()
        {
            Program.consoleOut.Write($"PC: ${PC:X4} ");
            Program.consoleOut.Write($"A: ${A:X2} ");
            Program.consoleOut.Write($"X: ${X:X2} ");
            Program.consoleOut.Write($"Y: ${Y:X2} ");
            Program.consoleOut.Write($"S: ${S:X2} ");
            Program.consoleOut.Write($"P: {Convert.ToString(P, 2).PadLeft(8,'0')}\n\t\t");
        }

        long currentCycles = 0;
        public int CurrentCyclesPerSecond = 0;
        long currentNMI = 0;
        public int CurrentNMIPerSecond = 0;
        long cyclesLastSecondStart = 0;
        long nmiLastSecondStart = 0;

        bool resetRequested = false;

        public void EnterCycleLoop()
        {
            new Thread(() =>
            {
                Stopwatch measureSW = new Stopwatch();
                Stopwatch limiterSW = new Stopwatch();
                measureSW.Start();
                limiterSW.Start();

                if (Program.Args.Length > 1 && Program.Args[1] == "-wait")
                {
                    Console.WriteLine("Press any key to start emulator...");
                    Console.ReadKey();
                }

                while (true)
                {
                    // Halt, stopped with Stop() and restarted with Start()
                    while (haltRequested)
                    {
                        haltAcknowledged = true;
                        Thread.Sleep(0);
                    }

                    RunCycle();

                    while (currentCycles >= (limiterSW.Elapsed.TotalSeconds * TARGET_HZ)) Thread.Sleep(0);

                    if (measureSW.ElapsedMilliseconds >= 1000)
                    {
                        CurrentCyclesPerSecond = (int) Math.Round((currentCycles - cyclesLastSecondStart) * measureSW.Elapsed.TotalSeconds);
                        cyclesLastSecondStart = currentCycles;
                        CurrentNMIPerSecond = (int)Math.Round((currentNMI - nmiLastSecondStart) * measureSW.Elapsed.TotalSeconds);
                        nmiLastSecondStart = currentNMI;
                        measureSW.Restart();
                    }
                    // Check NMI first
                    if (nmiTriggered && !insideNMI) EnterNMI();
                    // Then check IRQ
                    if (!insideIRQ && !insideNMI && !IsIntDisable() && IRQMap.Any(x => x())) EnterIRQ();

                    Thread.Sleep(0);
                }
            }) { IsBackground = true }.Start();
        }

        public Mutex Halt = new Mutex();

        private void EnterNMI()
        {
            currentNMI++;

            if (KAPE8bitEmulator.DebugMode)
                Program.consoleOut.WriteLine($"Entering NMI at ${PC:X4}");

            PushAddress(PC);
            Push(P);

            var hi = Read(0xfffb);
            var lo = Read(0xfffa);

            PC = (UInt16)(hi << 8 | lo);

            if (KAPE8bitEmulator.DebugMode)
                Program.consoleOut.WriteLine($"CPU reading NMI vector, got: ${PC:X4}");

            insideNMI = true;
            currentCycles += 7;
        }

        private void EnterIRQ()
        {
            if (KAPE8bitEmulator.DebugMode)
                Program.consoleOut.WriteLine($"Entering IRQ at ${PC:X4}");

            PushAddress(PC);
            Push(P);

            var hi = Read(0xffff);
            var lo = Read(0xfffe);

            PC = (UInt16)(hi << 8 | lo);

            if (KAPE8bitEmulator.DebugMode)
                Program.consoleOut.WriteLine($"CPU reading IRQ vector, got: ${PC:X4}");

            insideIRQ = true;
            currentCycles += 7;
        }

        void Push(byte b)
        {
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
            Program.consoleOut.WriteLine("Stack");
            for (int i = 0, offs = 0x0100; i < 16; i++)
            {
                for (int k = 0; k < 16; k++, offs++)
                    Program.consoleOut.Write($"{Read((UInt16) offs):X2}{(S == i ? '*' : ' ')}");
                Program.consoleOut.WriteLine("");
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

        internal void Stop()
        {
            haltRequested = true;
            while (!haltAcknowledged) Thread.Sleep(0);
            haltAcknowledged = false;

            Thread.Sleep(10);
        }

        internal void Start()
        {
            haltRequested = false;
        }
    }
}
