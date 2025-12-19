using System;
using System.Data;
using Microsoft.Xna.Framework.Input;

namespace KAPE8bitEmulator
{
    public class KeyboardDevice
    {
        const int FIFO_SIZE = 16;
        const ushort ADDR_STATUS = 0xF770;
        const ushort ADDR_DATA = 0xF771;

        byte[] fifo = new byte[FIFO_SIZE];
        int rp = 0, wp = 0, count = 0;

        CPU_6502 _cpu;

        bool irqEnable = false;
        bool rxOverrun = false;
        bool irqPending = false;
        bool modeRaw = false; // false = ASCII mode, true = raw keycodes

        public KeyboardDevice(CPU_6502 cpu)
        {
            _cpu = cpu;
            cpu.RegisterRead(ADDR_STATUS, ADDR_STATUS, ReadStatus);
            cpu.RegisterRead(ADDR_DATA, ADDR_DATA, ReadData);
            cpu.RegisterWrite(ADDR_STATUS, ADDR_STATUS, WriteControl);

            cpu.RegisterIRQ(() => irqPending, "KeyboardDevice");
        }

        protected byte ReadStatus(UInt16 address)
        {
            byte s = 0;
            if (count > 0) s |= 0x01;       // RX_READY
            if (rxOverrun) s |= 0x02;       // RX_OVERRUN
            if (irqPending) s |= 0x04;      // IRQ_PENDING
            if (modeRaw) s |= 0x08;         // MODE_RAW (1 = raw keycodes, 0 = ASCII)
            return s;
        }

        protected byte ReadData(UInt16 address)
        {
            if (count == 0) return 0;
            byte b = fifo[rp];
            rp = (rp + 1) % FIFO_SIZE;
            count--;
            if (count == 0) irqPending = false;
            return b;
        }

        public void WriteControl(UInt16 address, byte value)
        {
            if ((value & 0x01) != 0) // CLEAR FIFO
            {
                rp = 0; wp = 0; count = 0;
                rxOverrun = false;
                irqPending = false;
            }
            irqEnable = (value & 0x02) != 0;
            // bit2: MODE_RAW (0 = ASCII mode, 1 = raw keycodes)
            modeRaw = (value & 0x04) != 0;
        }

        public void PushKey(Keys k, bool shift, bool isDown)
        {
            byte b = 0;
            if (ModeRaw)
                b = MapKeyToRaw7Bit(k);
            else
                b = MapKeyToAscii7Bit(k, shift);
            if (b != 0)
            {
                b = (byte)(b | (isDown ? 0x80 : 0x00)); // bit7 = down
            }

            if (count >= FIFO_SIZE)
            {
                rxOverrun = true;
                return;
            }
            fifo[wp] = b;
            wp = (wp + 1) % FIFO_SIZE;
            count++;
            if (KAPE8bitEmulator.CpuTraceMode)
            {
                Console.WriteLine($"[CPU] PushKey val=${b:X2} count={count} rp={rp} wp={wp} irqEnable={irqEnable} irqPending={irqPending}");
            }
            if (count == 1 && irqEnable)
            {
                irqPending = true;
            }
        }

        public bool ModeRaw => modeRaw;

        // Return a 7-bit key identifier (no ASCII translation). High bit remains used to indicate key-down (1)/key-up (0).
        // The ROM should translate these key IDs to characters if needed.
        private byte MapKeyToRaw7Bit(Keys k)
        {
            if (k >= Keys.A && k <= Keys.Z)
                return (byte)(1 + (k - Keys.A));
            if (k >= Keys.D0 && k <= Keys.D9)
                return (byte)(30 + (k - Keys.D0));
            if (k == Keys.Space) return 0x20;
            if (k == Keys.Enter) return 0x21;
            if (k == Keys.Back) return 0x22;
            if (k == Keys.Tab) return 0x23;
            if (k == Keys.OemMinus) return 0x24;
            if (k == Keys.OemPlus) return 0x25;
            if (k == Keys.OemComma) return 0x26;
            if (k == Keys.OemPeriod) return 0x27;

            return 0;
        }

        // Map physical key to 7-bit ASCII; respect Shift flag for letters/digits/punctuation
        private byte MapKeyToAscii7Bit(Keys k, bool shift)
        {
            if (k >= Keys.A && k <= Keys.Z)
            {
                char c = (char)('a' + (k - Keys.A));
                if (shift) c = char.ToUpper(c);
                return (byte)c;
            }
            if (k >= Keys.D0 && k <= Keys.D9)
            {
                string noShift = "0123456789";
                string withShift = ")!@#$%^&*("; // shift+digits on US keyboard
                int idx = k - Keys.D0;
                return (byte)(shift ? withShift[idx] : noShift[idx]);
            }
            if (k == Keys.Space) return (byte)0x20; // Space
            if (k == Keys.Enter) return (byte)0x0a; // LF
            if (k == Keys.Back) return 0x7f;      // DEL (Backspace)
            if (k == Keys.Tab) return 0x09;
            // Basic punctuation
            if (k == Keys.OemMinus) return (byte)(shift ? '_' : '-');
            if (k == Keys.OemPlus) return (byte)(shift ? '+' : '=');
            if (k == Keys.OemComma) return (byte)(shift ? '<' : ',');
            if (k == Keys.OemPeriod) return (byte)(shift ? '>' : '.');
            return 0;
        }        
    }
}
