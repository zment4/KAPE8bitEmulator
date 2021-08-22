using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KAPE8bitEmulator
{
    partial class KAPE_GPU {
        public class KAPE_GPU_TextMode : IKAPE_GPU_Mode
        {
            const int TEXT_WIDTH = 32;
            const int TEXT_HEIGHT = 24;

            byte currentFGColor = 0x7;
            byte currentBGColor;

            byte cursorX;
            byte cursorY;

            KAPE_GPU gpu;
            public KAPE_GPU GPU { get => gpu; set => gpu = value; }

            private bool _isTerminal;
            public bool IsTerminal => _isTerminal;

            public KAPE_GPU_TextMode(KAPE_GPU kapeGPU)
            {
                gpu = kapeGPU;
            }

            byte[,] textBuffer = new byte[TEXT_WIDTH, TEXT_HEIGHT];
            byte[,] fgColorBuffer = new byte[TEXT_WIDTH, TEXT_HEIGHT];
            byte[,] bgColorBuffer = new byte[TEXT_WIDTH, TEXT_HEIGHT];

            public void DrawTextBuffer()
            {
                for (int y = 0; y < TEXT_HEIGHT; y++)
                {
                    for (int x = 0; x < TEXT_WIDTH; x++)
                    {
                        DrawCharacter(x, y, char_gen[textBuffer[x, y]]);
                    }
                }
            }

            public void DrawCharacter(int x, int y, byte[] c)
            {
                int base_px = x << 3;
                int base_py = y << 3;

                for (int py = 0; py < 8; py++)
                {
                    for (int px = 0; px < 8; px++)
                    {
                        bool pxlOn = ((1 << (7 - px)) & c[py]) >> (7 - px) == 1 ? true : false;
                        gpu.PutPixel(base_px + px, base_py + py, pxlOn ? fgColorBuffer[x, y] : bgColorBuffer[x, y]);
                    }
                }
            }

            public void Draw()
            {
                DrawTextBuffer();
            }

            public void HandleCommandBytes(byte[] cmdBytes)
            {
                switch (cmdBytes[0])
                {
                    case KAPE_GPU_CSM_FIFO.CF_CMD_SEND_CHARACTER:
                        HandleCmdSendCharacter(cmdBytes[1]);
                        break;
                    case KAPE_GPU_CSM_FIFO.CF_CMD_SET_INDEX:
                        HandleCmdSetIndex(cmdBytes[1], cmdBytes[2], cmdBytes[3]);
                        break;
                    case KAPE_GPU_CSM_FIFO.CF_CMD_CLEAR_SCREEN:
                        HandleCmdClearScreen(cmdBytes[1]);
                        break;
                    case KAPE_GPU_CSM_FIFO.CF_CMD_TERMINAL:
                        _isTerminal = true;
                        break;
                    default:
                        Console.WriteLine($"Unknown MODE command 0x{cmdBytes[0]:X2}");
                        Console.WriteLine($"FREEZING!");
                        Thread.Sleep(Timeout.Infinite);
                        break;
                }
            }

            enum TerminalStateEnum
            {
                Normal,
                Escape,
                ColorBinary
            }

            TerminalStateEnum terminalState = TerminalStateEnum.Normal;

            void IKAPE_GPU_Mode.HandleTerminalCommandByte(byte cmdByte)
            {
                switch (terminalState)
                {
                    case TerminalStateEnum.Normal:
                        TerminalState_Normal(cmdByte);
                        break;
                    case TerminalStateEnum.Escape:
                        TerminalState_Escape(cmdByte);
                        break;
                    case TerminalStateEnum.ColorBinary:
                        TerminalState_ColorBinary(cmdByte);
                        break;
                }
            }

            private void TerminalState_Normal(byte cmdByte)
            {
                switch (cmdByte)
                {
                    case KAPE_GPU_CSM_FIFO.TERM_ESCAPE:
                        terminalState = TerminalStateEnum.Escape;
                        break;
                    default:
                        HandleInputCharacter(cmdByte);
                        break;
                }
            }

            private void TerminalState_Escape(byte cmdByte)
            { 
                switch (cmdByte)
                {
                    case KAPE_GPU_CSM_FIFO.CF_TERM_COLOR_BINARY:
                        terminalState = TerminalStateEnum.ColorBinary;
                        break;
                    default:
                        Console.WriteLine($"Unknown TERM command 0x{cmdByte:X2}");
                        Console.WriteLine($"FREEZING!");
                        Thread.Sleep(Timeout.Infinite);
                        break;
                }
            }

            private void TerminalState_ColorBinary(byte cmdByte)
            {
                currentBGColor = (byte)(cmdByte & 0xf);
                currentFGColor = (byte)((cmdByte & 0xf0) >> 4);

                terminalState = TerminalStateEnum.Normal;
            }

            const int CHAR_SPACE        = 0x20;
            const int CHAR_UNDERSCORE   = 0x5F;

            void HandleCmdSendCharacter(byte b)
            {
                HandleInputCharacter(b);
            }

            void HandleInputCharacter(byte b)
            { 
                textBuffer[cursorX, cursorY] = b;
                fgColorBuffer[cursorX, cursorY] = currentFGColor;
                bgColorBuffer[cursorX, cursorY] = currentBGColor;

                cursorX++;
                if (cursorX >= TEXT_WIDTH)
                {
                    cursorX = 0;
                    cursorY++;
                    if (cursorY >= TEXT_HEIGHT)
                        cursorY = 0;
                }

                textBuffer[cursorX, cursorY] = CHAR_UNDERSCORE;
                fgColorBuffer[cursorX, cursorY] = currentFGColor;
                bgColorBuffer[cursorX, cursorY] = currentBGColor;
            }

            void HandleCmdSetIndex(byte x, byte y, byte c)
            {
                x %= TEXT_WIDTH;
                y %= TEXT_HEIGHT;
                    
                textBuffer[x, y] = c;
                fgColorBuffer[x, y] = currentFGColor;
                bgColorBuffer[x, y] = currentBGColor;
            }

            void HandleCmdClearScreen(byte c)
            {
                for (int y = 0; y < TEXT_HEIGHT; y++)
                {
                    for (int x = 0; x < TEXT_WIDTH; x++)
                    {
                        textBuffer[x, y] = c;
                    }
                }
            }

            public void Reset()
            {
                for (int y = 0, i = 0; y < TEXT_HEIGHT; y++)
                {
                    for (int x = 0; x < TEXT_WIDTH; x++, i++)
                    {
                        textBuffer[x, y] = (byte)i;
                        fgColorBuffer[x, y] = (byte)((i % 15) + 1);
                    }
                }

                _isTerminal = false;
                terminalState = TerminalStateEnum.Normal;
            }
        }
    }
}
