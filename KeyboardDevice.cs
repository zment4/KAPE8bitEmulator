using System;

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

        protected void WriteControl(UInt16 address, byte value)
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

        public void PushKey(byte b)
        {
            if (count >= FIFO_SIZE)
            {
                rxOverrun = true;
                return;
            }
            fifo[wp] = b;
            wp = (wp + 1) % FIFO_SIZE;
            count++;
            if (KAPE8bitEmulator.TraversalMode)
            {
                Console.WriteLine($"[TRAV] PushKey val=${b:X2} count={count} rp={rp} wp={wp} irqEnable={irqEnable} irqPending={irqPending}");
            }
            if (count == 1 && irqEnable)
            {
                irqPending = true;
                _cpu.TriggerIRQ();
            }
        }

        public bool ModeRaw => modeRaw;
    }
}
