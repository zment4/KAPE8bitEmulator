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
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_TERMINAL,
                        Action = CMD_TerminalMode,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_FLUSH_FRAME,
                        Action = x => { },
                    },
                });
            }

            void CMD_TerminalMode(byte[] cmdBytes)
            {
                if (KAPE8bitEmulator.GpuTraceMode)
                    Console.WriteLine("[GPU] Switching to TERMINAL mode");

                fgColorBuffer[cursorX, cursorY] = currentFGColor;
                bgColorBuffer[cursorX, cursorY] = currentBGColor;

                _isTerminal = true;
            }

            void CMD_SendCharacter(byte[] cmdBytes)
            {
                if (KAPE8bitEmulator.GpuTraceMode)
                    Console.WriteLine("[GPU] Sending character 0x{0:X2} to terminal", cmdBytes[1]);

                byte b = cmdBytes[1];

                HandleInputCharacter(b);
            }

            void CMD_ClearScreen(byte[] cmdBytes)
            {
                if (KAPE8bitEmulator.GpuTraceMode)
                    Console.WriteLine("[GPU] Clearing screen with character 0x{0:X2}", cmdBytes[1]);

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
                if (KAPE8bitEmulator.GpuTraceMode)
                    Console.WriteLine("[GPU] Setting character at ({0},{1}) to 0x{2:X2}", cmdBytes[1], cmdBytes[2], cmdBytes[3]);

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


            long previousFrame = -1;

            byte currentCursorChar = CHAR_SPACE;
            long currentCursorToggleFramesLeft = 17;

            long drawStartedAtFrame = -1;

            public override void Draw(long currentTicks)
            {
                long calculatedFrame = currentTicks / KAPE8bitEmulator.TICKS_PER_NMI;
                if (previousFrame == calculatedFrame)
                {
                    return;
                }
                previousFrame = calculatedFrame;
                if (drawStartedAtFrame == -1)
                    drawStartedAtFrame = calculatedFrame;

                long currentFrame = calculatedFrame - drawStartedAtFrame;

                currentCursorToggleFramesLeft--;
                if (currentCursorToggleFramesLeft <= 0)
                {
                    currentCursorChar = currentCursorChar == CHAR_SPACE ? (byte) CHAR_UNDERSCORE : (byte) CHAR_SPACE;
                    currentCursorToggleFramesLeft = 17;
                }

                if (IsTerminal) {
                    textBuffer[cursorX, cursorY] = currentCursorChar;
                }

                DrawTextBuffer();
                base.Draw(currentTicks);
            }

            enum TerminalStateEnum
            {
                Normal,
                Escape,
                ColorBinary,
                Color
            }

            int terminalFifoStateCount = 0;

            TerminalStateEnum terminalState = TerminalStateEnum.Normal;

            public override void HandleTerminalCommandByte(byte cmdByte)
            {
                if(KAPE8bitEmulator.GpuTraceMode)
                    Console.WriteLine($"[GPU] TERM CMD BYTE: 0x{cmdByte:X2}");

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
                    case TerminalStateEnum.Color:
                        TerminalState_Color(cmdByte);
                        break;
                }
            }

            private void TerminalState_Normal(byte cmdByte)
            {
                if (KAPE8bitEmulator.GpuTraceMode)
                    Console.WriteLine("[GPU] TerminalState_Normal: Sending character 0x{0:X2} to terminal", cmdByte);

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
                if (KAPE8bitEmulator.GpuTraceMode)
                    Console.WriteLine("[GPU] TerminalState_Escape: Sending character 0x{0:X2} to terminal", cmdByte);

                switch (cmdByte)
                {
                    case KAPE_GPU_CMD_FIFO.CF_TERM_COLOR_BINARY:
                        terminalState = TerminalStateEnum.ColorBinary;
                        break;
                    case KAPE_GPU_CMD_FIFO.CF_TERM_COLOR:
                        terminalState = TerminalStateEnum.Color;
                        break;
                    default:
                        Console.WriteLine($"Unknown TERM command 0x{cmdByte:X2}");
                        Console.WriteLine($"FREEZING!");
                        Task.Delay(Timeout.Infinite).Wait();
                        //Thread.Sleep(Timeout.Infinite);
                        break;
                }
            }

            private void TerminalState_ColorBinary(byte cmdByte)
            {
                if (KAPE8bitEmulator.GpuTraceMode)
                    Console.WriteLine("[GPU] TerminalState_ColorBinary: Sending character 0x{0:X2} to terminal", cmdByte);

                currentBGColor = (byte)(cmdByte & 0xf);
                currentFGColor = (byte)((cmdByte & 0xf0) >> 4);

                terminalState = TerminalStateEnum.Normal;
            }

            
            private void TerminalState_Color(byte cmdByte)
            {
                if (KAPE8bitEmulator.GpuTraceMode)
                    Console.WriteLine("[GPU] TerminalState_Color: Sending character 0x{0:X2} to terminal", cmdByte);

                if (terminalFifoStateCount == 0 || terminalFifoStateCount == 1)
                {
                    terminalFifoStateCount++;

                    if ((cmdByte >= '0' && cmdByte <= '9') ||
                        (cmdByte >= 'A' && cmdByte <= 'F') ||
                        (cmdByte >= 'a' && cmdByte <= 'f'))
                    {
                        if (cmdByte >= 'a') cmdByte -= ('a' - 10);
                        if (cmdByte >= 'A') cmdByte -= ('A' - 10);
                        if (cmdByte >= '0') cmdByte -= (byte) '0';

                        if (terminalFifoStateCount == 0)
                        {
                            currentFGColor = cmdByte;
                        }
                        if (terminalFifoStateCount == 1)
                        {
                            currentBGColor = cmdByte;
                        }
                    }
                }

                if (terminalFifoStateCount == 2)
                {
                    terminalFifoStateCount = 0;

                    terminalState = TerminalStateEnum.Normal;
                }
            }
            const int CHAR_SPACE        = 0x20;
            const int CHAR_UNDERSCORE   = 0x5F;

            void HandleInputCharacter(byte b)
            {
                if (KAPE8bitEmulator.GpuTraceMode)
                    Console.WriteLine("[GPU] Handling input character 0x{0:X2}", b);

                currentCursorToggleFramesLeft = 17;
                currentCursorChar = CHAR_UNDERSCORE;

                if (b == '\n')
                {
                    textBuffer[cursorX, cursorY] = CHAR_SPACE;

                    cursorX = 0;
                    cursorY++;
                    // TODO: Implement Terminal scroll
                    if (cursorY >= TEXT_HEIGHT)
                        cursorY = 0;
                } else 
                {
                    textBuffer[cursorX, cursorY] = b;

                    cursorX++;
                    if (cursorX >= TEXT_WIDTH)
                    {
                        cursorX = 0;
                        cursorY++;
                        // TODO: Implement Terminal scroll
                        if (cursorY >= TEXT_HEIGHT)
                            cursorY = 0;
                    }
                }
        
                fgColorBuffer[cursorX, cursorY] = currentFGColor;
                bgColorBuffer[cursorX, cursorY] = currentBGColor;

            }

            public override void Reset()
            {
                if (KAPE8bitEmulator.GpuTraceMode)
                    Console.WriteLine("[GPU] RESET");

                for (int y = 0, i = 0; y < TEXT_HEIGHT; y++)
                {
                    for (int x = 0; x < TEXT_WIDTH; x++, i++)
                    {
                        textBuffer[x, y] = (byte)i;
                        fgColorBuffer[x, y] = (byte)((i % 15) + 1);
                    }
                }

                cursorX = 0;
                cursorY = 0;
                
                _isTerminal = false;
                terminalState = TerminalStateEnum.Normal;

                base.Reset();
            }
        }
    }
}
