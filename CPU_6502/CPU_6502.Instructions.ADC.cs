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
            public const byte ADC_IMM = 0x69;
            public const byte ADC_ABS = 0x6D;
            public const byte ADC_IIX = 0x71;

            void I_ADC_X(byte b)
            {
                var carry = CPU.GetCarry() ? 1 : 0;
                int val = CPU.A + b + carry;
                CPU.SetCarry(val > 0xff);
                CPU.SetOverflow(val < -128 || val > 127);
                byte res = (byte)(sbyte)val;
                CPU.SetNegative((res & (1 << 7)) != 0);
                CPU.SetZero(res == 0);
                CPU.A = res;
            }

            void I_ADC_IMM()
            {
                I_ADC_X(CPU.FetchOperand());
            }
            void I_ADC_ABS()
            {
                I_ADC_X(CPU.Read(CPU.FetchAbsoluteAddress()));
            }

            void I_ADC_IIX()
            {
                I_ADC_X(CPU.Read(CPU.FetchIndirectIndexedAddress()));
            }
        }
    }
}
