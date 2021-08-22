using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KAPE8bitEmulator
{
    public partial class CPU_6502
    {
        partial class Instructions
        {
            public const byte CMP_IMM = 0xC9;
            public const byte CMP_ABS = 0xCD;
            public const byte CPY_IMM = 0xC0;
            public const byte CMP_ZPG = 0xC5;

            void I_CMP_X(byte a, byte b)
            {
                int res = a - b;

                CPU.SetNegative((byte)(sbyte)res);
                CPU.SetZero((byte)res);
                CPU.SetCarry(res >= 0);
            }

            void I_CMP_IMM()
            {
                I_CMP_X(CPU.A, CPU.FetchOperand());
            }

            void I_CMP_ABS()
            {
                I_CMP_X(CPU.A, CPU.Read(CPU.FetchAbsoluteAddress()));
            }

            void I_CMP_ZPG()
            {
                I_CMP_X(CPU.A, CPU.Read(CPU.FetchOperand()));
            }

            void I_CPY_IMM()
            {
                I_CMP_X(CPU.Y, CPU.FetchOperand());
            }
        }
    }
}
