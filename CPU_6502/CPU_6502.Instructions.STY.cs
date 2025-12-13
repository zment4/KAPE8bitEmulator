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
            public const byte STY_ZPG = 0x84;

            void I_STY_ZPG()
            {
                CPU.Write(CPU.FetchOperand(), CPU.Y);
            }
        }
    }
}
