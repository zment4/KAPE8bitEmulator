using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KAPE8bitEmulator
{
    class SRAM64k
    {
        byte[] sram = new byte[0x10000];

        public void RegisterMap(CPU_6502 cpu)
        {
            cpu.RegisterRead(0, 0xffff, Read);
            cpu.RegisterWrite(0, 0xffff, Write);
        }

        public byte Read(UInt16 address)
        {
            return sram[address];
        }

        public void Write(UInt16 address, byte val)
        {
            sram[address] = val;
            if (KAPE8bitEmulator.DebugMode && address >= 0x0100 && address <= 0x01FF)
            {
                Console.WriteLine($"SRAM.Write stack: addr ${address:X4} <- ${val:X2}");
            }
        }

        internal void FillRam(string binFile)
        {
            using (var fileStream = new FileStream(binFile, FileMode.Open))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                binaryReader.Read(sram, 0, 0x10000);

                Console.WriteLine($"Filled ram with {fileStream.Name}, {fileStream.Position} bytes.");
            }
        }

        internal void FillFromEmbeddedBinary()
        {
            using (var fileStream = new FileStream(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fileStream.Seek(-0x10000, SeekOrigin.End);
                using (var binaryReader = new BinaryReader(fileStream))
                {
                    binaryReader.Read(sram, 0, 0x10000);

                    Console.WriteLine($"Filled ram with {0x10000} bytes from embedded bin.");
                    Console.WriteLine($"First byte: {sram[0]:X2}");
                }
            }
        }

        public byte[] GetRamCopy()
        {
            return (byte[])sram.Clone();
        }
    }
}
