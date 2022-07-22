using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KAPE8bitEmulator
{
    public partial class KAPE_GPU {
        public class KAPE_GPU_TextMode : KAPE_GPU_Mode
        {
            const int TEXT_WIDTH = 32;
            const int TEXT_HEIGHT = 24;

            byte currentFGColor = 0x7;
            byte currentBGColor;

            byte cursorX;
            byte cursorY;

            private bool _isTerminal;
            public override bool IsTerminal => _isTerminal;

            byte[,] textBuffer = new byte[TEXT_WIDTH, TEXT_HEIGHT];
            byte[,] fgColorBuffer = new byte[TEXT_WIDTH, TEXT_HEIGHT];
            byte[,] bgColorBuffer = new byte[TEXT_WIDTH, TEXT_HEIGHT];

            public KAPE_GPU_TextMode(KAPE_GPU gpu) : base(gpu)
            {
                commands.AddRange(new List<CommandDescriptor>() {
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_SEND_CHARACTER,
                        Action = CMD_SendCharacter,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_CLEAR_SCREEN,
                        Action = CMD_ClearScreen,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_SET_INDEX,
                        Action = CMD_SetIndex,
                    }
                });
            }

            void CMD_SendCharacter(byte[] cmdBytes)
            {
                byte b = cmdBytes[1];

                HandleInputCharacter(b);
            }

            void CMD_ClearScreen(byte[] cmdBytes)
            {
                byte b = cmdBytes[1];

                for (int y = 0; y < TEXT_HEIGHT; y++)
                {
                    for (int x = 0; x < TEXT_WIDTH; x++)
                    {
                        textBuffer[x, y] = b;
                    }
                }
            }

            void CMD_SetIndex(byte[] cmdBytes)
            {
                byte x = (byte) (cmdBytes[1] % TEXT_WIDTH);
                byte y = (byte) (cmdBytes[2] % TEXT_HEIGHT);
                byte b = (byte) (cmdBytes[3]);

                textBuffer[x, y] = b;
                fgColorBuffer[x, y] = currentFGColor;
                bgColorBuffer[x, y] = currentBGColor;
            }

            public void DrawTextBuffer()
            {
                for (int y = 0; y < TEXT_HEIGHT; y++)
                {
                    for (int x = 0; x < TEXT_WIDTH; x++)
                    {
                        DrawCharacter(x, y, textBuffer[x, y]);
                    }
                }
            }

            public void DrawCharacter(int x, int y, byte index)
            {
                int base_px = x << 3;
                int base_py = y << 3;

                for (int py = 0; py < 8; py++)
                {
                    var cg = CharGenData.CharGen[index * 8 + py];
                    for (int px = 0; px < 8; px++)
                    {
                        bool pxlOn = ((1 << (7 - px)) & cg) >> (7 - px) == 1 ? true : false;
                        gpu.PutPixel(base_px + px, base_py + py, pxlOn ? fgColorBuffer[x, y] : bgColorBuffer[x, y]);
                    }
                }
            }

            public override void Draw()
            {
                DrawTextBuffer();
                base.Draw();
            }

            enum TerminalStateEnum
            {
                Normal,
                Escape,
                ColorBinary
            }

            TerminalStateEnum terminalState = TerminalStateEnum.Normal;

            public override void HandleTerminalCommandByte(byte cmdByte)
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
                    case KAPE_GPU_CMD_FIFO.TERM_ESCAPE:
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
                    case KAPE_GPU_CMD_FIFO.CF_TERM_COLOR_BINARY:
                        terminalState = TerminalStateEnum.ColorBinary;
                        break;
                    default:
                        Program.consoleOut.WriteLine($"Unknown TERM command 0x{cmdByte:X2}");
                        Program.consoleOut.WriteLine($"FREEZING!");
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

            public override void Reset()
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

                base.Reset();
            }
        }
    }
}
