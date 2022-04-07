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
            public const byte STA_ABS = 0x8D;
            public const byte STA_ZPG = 0x85;
            public const byte STA_IIX = 0x91;

            void I_STA_X(UInt16 addr)
            {
                CPU.Write(addr, CPU.A);
            }

            void I_STA_ABS()
            {
                I_STA_X(CPU.FetchAbsoluteAddress());
            }

            void I_STA_ZPG()
            {
                I_STA_X(CPU.FetchOperand());
            }

            void I_STA_IIX()
            {
                I_STA_X(CPU.FetchIndirectIndexedAddress());
            }
        }
    }
}
