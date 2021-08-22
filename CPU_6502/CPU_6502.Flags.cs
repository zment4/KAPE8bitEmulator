using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static KAPE8bitEmulator.CPU_6502.Instructions;

namespace KAPE8bitEmulator
{
    public partial class CPU_6502
    {
        public const byte P_CARRY_MASK = 0b00000001;
        public const byte P_ZERO_MASK = 0b00000010;
        public const byte P_INTDISABLE_MASK = 0b00000100;
        public const byte P_DECIMAL_MASK = 0b00001000;
        public const byte P_OVERFLOW_MASK = 0b01000000;
        public const byte P_NEGATIVE_MASK = 0b10000000;

        private void ClearDecimal()
        {
            P = (byte)(P & ~P_DECIMAL_MASK);
        }
        private void ClearCarry()
        {
            P = (byte)(P & ~P_CARRY_MASK);
        }

        private void ClearZero()
        {
            P = (byte)(P & ~P_ZERO_MASK);
        }
        private void ClearNegative()
        {
            P = (byte)(P & ~P_NEGATIVE_MASK);
        }

        private void ClearIntDisable()
        {
            P = (byte)(P & ~P_INTDISABLE_MASK);
        }

        private void SetNegative()
        {
            P = (byte)(P | P_NEGATIVE_MASK);
        }

        private void SetCarry()
        {
            P = (byte)(P | P_CARRY_MASK);
        }

        private void SetCarry(bool b)
        {
            if (b) SetCarry();
            else ClearCarry();
        }

        private bool GetCarry()
        {
            return ((P & P_CARRY_MASK) != 0);
        }

        private void SetZero()
        {
            P = (byte)(P | P_ZERO_MASK);
        }

        private bool GetZero()
        {
            return ((P & P_ZERO_MASK) != 0);
        }

        private void SetIntDisable()
        {
            P = (byte)(P | P_INTDISABLE_MASK);
        }

        private bool IsIntDisable()
        {
            return (P | P_INTDISABLE_MASK) != 0;
        }

        private void SetOverflow(bool v)
        {
            if (v) SetOverflow();
            else ClearOverflow();
        }

        private void ClearOverflow()
        {
            P = (byte)(P & ~P_OVERFLOW_MASK);
        }

        private void SetOverflow()
        {
            P = (byte)(P | P_OVERFLOW_MASK);
        }

        private void SetNegative(bool b)
        {
            if (b) SetNegative();
            else ClearNegative();

        }
        private void SetZero(bool b)
        {
            if (b) SetZero();
            else ClearZero();
        }

        private void SetNegative(byte b)
        {
            SetNegative(b < 0);
        }

        private void SetZero(byte b)
        {
            SetZero(b == 0);
        }
    }
}
