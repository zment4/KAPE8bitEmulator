using System.Diagnostics;
using HighPrecisionTimer;

namespace KAPE8bitEmulator
{
    public class NMITimer : MultimediaTimer
    {
        Stopwatch stopwatch = new Stopwatch();
        public long lastNMIticks = 0;

        public NMITimer(int fps, long ticksPerNMI, CPU_6502 cpu)
        {
            Resolution = 0;
            Interval = 1000 / fps; // 62 Hz
            Elapsed += (o, e) =>
            {
                var elapsedTicks = stopwatch.ElapsedTicks;
                var deltaTicks = elapsedTicks - lastNMIticks;
                if (deltaTicks >= ticksPerNMI)
                {
                    cpu.TriggerNMI();
                    lastNMIticks += deltaTicks - (deltaTicks - ticksPerNMI);
                }
            };

            stopwatch.Start();
            Start();
        }
    }
}