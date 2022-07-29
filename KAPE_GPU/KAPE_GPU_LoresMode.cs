using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KAPE8bitEmulator
{
    public partial class KAPE_GPU
    {
        public class KAPE_GPU_LoresMode : KAPE_GPU_Mode
        {
            const int FB_WIDTH = 128;
            const int FB_HEIGHT = 96;

            public byte[,] backingFrameBuffer = new byte[FB_WIDTH, FB_HEIGHT];
            public byte[,] frameBuffer = new byte[FB_WIDTH, FB_HEIGHT];

            public KAPE_GPU_LoresMode(KAPE_GPU gpu) : base(gpu)
            {
                this.gpu = gpu;
                commands = new List<CommandDescriptor>() {
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_CLEAR_SCREEN,
                        Action = CMD_ClearScreen,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_FLUSH_FRAME,
                        Action = CMD_FlushFrame,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_DRAW_LINE,
                        Action = CMD_DrawLine,
                    }
                };
            }

            public override void Draw()
            {
                for (int y = 0; y < FB_HEIGHT; y++)
                {
                    for (int x = 0; x < FB_WIDTH; x++)
                    {
                        int sx = x * 2;
                        int sy = y * 2;

                        GPU.PutPixel(sx, sy, frameBuffer[x, y]);
                        GPU.PutPixel(sx+1, sy, frameBuffer[x, y]);
                        GPU.PutPixel(sx+1, sy+1, frameBuffer[x, y]);
                        GPU.PutPixel(sx, sy+1, frameBuffer[x, y]);
                    }
                }
            }

            public override void HandleCommandBytes(byte[] cmdBytes)
            {
                var cmd = commands.Find(x => x.Command == cmdBytes[0]);
                if (cmd == null)
                {
                    Console.WriteLine($"Unknown MODE command 0x{cmdBytes[0]:X2}");
                    Console.WriteLine($"FREEZING!");
                    Thread.Sleep(Timeout.Infinite);
                }

                cmd.Action(cmdBytes);

                base.HandleCommandBytes(cmdBytes);
            }

            public override void HandleTerminalCommandByte(byte cmdByte)
            {
                base.HandleTerminalCommandByte(cmdByte);
            }

            void CMD_ClearScreen(byte[] cmdBytes)
            {
                for (int y = 0; y < FB_HEIGHT; y++)
                    for (int x = 0; x < FB_WIDTH; x++)
                        backingFrameBuffer[x, y] = cmdBytes[1];
            }

            void CMD_FlushFrame(byte[] cmdBytes)
            {
                for (int y = 0; y < FB_HEIGHT; y++)
                    for (int x = 0; x < FB_WIDTH; x++)
                        frameBuffer[x, y] = backingFrameBuffer[x, y];
            }

            void CMD_DrawLine(byte[] cmdBytes)
            {
                DrawLine(cmdBytes[1] % FB_WIDTH, cmdBytes[2] % FB_HEIGHT, cmdBytes[3] % FB_WIDTH, cmdBytes[4] % FB_HEIGHT, cmdBytes[5]);
            }

            void DrawLine(int x1, int y1, int x2, int y2, int c)
            {
                int dx = Math.Abs(x2 - x1);
                int dy = -Math.Abs(y2 - y1);
                int sx = x1 < x2 ? 1 : -1;
                int sy = y1 < y2 ? 1 : -1;
                int err = dx + dy;
                while (true)
                {
                    PutPixel(x1, y1, c);
                    if (x1 == x2 && y1 == y2) break;
                    int e2 = err * 2;
                    if (e2 >= dy)
                    {
                        err += dy;
                        x1 += sx;
                    }
                    if (e2 <= dx)
                    {
                        err += dx;
                        y1 += sy;
                    }
                }
            }

            void PutPixel(int x, int y, int c)
            {
                backingFrameBuffer[x % FB_WIDTH, y % FB_HEIGHT] = (byte) (c & 0xf);
            }

            public override void Reset() 
            {
                base.Reset();
            }
        }
    }
}
