namespace KAPE8bitEmulator
{
    public interface IKAPE_GPU_Mode
    {
        KAPE_GPU GPU { get; set; }
        bool IsTerminal { get; }

        void Draw();
        void HandleCommandBytes(byte[] cmdBytes);
        void HandleTerminalCommandByte(byte cmdByte);
        void Reset();
    }
}