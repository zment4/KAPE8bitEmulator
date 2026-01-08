using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KAPE8bitEmulator
{
    public partial class CPU_6502
    {
        public partial class Instructions
        {
            public const byte LDA_IMM = 0xA9;
            public const byte LDA_ZPG = 0xA5;
            public const byte LDA_IXI = 0xA1;
            public const byte LDA_ABS = 0xAD;
            public const byte LDA_IIX = 0xB1;
            public const byte LDA_ABX = 0xBD;

            void I_LDA_X(byte b)
            {
                CPU.A = b;
                CPU.SetZero(CPU.A);
                CPU.SetNegative(CPU.A);
            }

            void I_LDA_IMM()
            {
                I_LDA_X(CPU.FetchOperand());
            }

            void I_LDA_ABS()
            {
                I_LDA_X(CPU.Read(CPU.FetchAbsoluteAddress()));
            }

            void I_LDA_IIX()
            {
                bool pageBoundaryCrossed = false;
                I_LDA_X(CPU.Read(CPU.FetchIndirectIndexedAddress(out pageBoundaryCrossed)));

                instructionDescriptors[LDA_IIX].Cycles = pageBoundaryCrossed ? 6 : 5;
            }

            void I_LDA_IXI()
            {
                // (zp,X) addressing
                I_LDA_X(CPU.Read(CPU.FetchIndexedIndirectAddress()));
            }

            void I_LDA_ZPG()
            {
                I_LDA_X(CPU.Read(CPU.FetchOperand()));
            }

            void I_LDA_ABX()
            {
                bool pageBoundaryCrossed = false;
                I_LDA_X(CPU.Read(CPU.FetchAbsoluteIndexedAddress(CPU.X, out pageBoundaryCrossed)));

                instructionDescriptors[LDA_ABX].Cycles = pageBoundaryCrossed ? 5 : 4;
            }
        }
    }
}
