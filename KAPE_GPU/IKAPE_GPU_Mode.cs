namespace KAPE8bitEmulator
{
    public partial class KAPE_GPU
    {
        public interface IKAPE_GPU_Mode
        {
            KAPE_GPU GPU { get; set; }
            bool IsTerminal { get; }

            void Draw(long currentTicks);
            void HandleCommandBytes(byte[] cmdBytes);
            void HandleTerminalCommandByte(byte cmdByte);
            void Reset();
        }
    }
}