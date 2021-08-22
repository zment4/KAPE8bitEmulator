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
        public class KAPE_GPU_GraphicsMode : IKAPE_GPU_Mode
        {
            public class CommandDescriptor
            {
                public byte Command;
                public Action<byte[]> Action;
            }

            const int TILE_WIDTH = 8;
            const int TILE_HEIGHT = 8;

            const int TILEMAP_WIDTH = 32;
            const int TILEMAP_HEIGHT = 24;

            KAPE_GPU gpu;
            public KAPE_GPU GPU { get => gpu; set => gpu = value; }

            public bool IsTerminal => false;

            byte[,] tileMap = new byte[TILEMAP_WIDTH, TILEMAP_HEIGHT];
            byte[][] patternTable = new byte[0x100][];

            List<CommandDescriptor> commands = new List<CommandDescriptor>();

            public KAPE_GPU_GraphicsMode(KAPE_GPU gpu)
            {
                this.gpu = gpu;

                for (int i = 0; i < patternTable.Length; i++)
                {
                    patternTable[i] = new byte[32];
                }

                commands = new List<CommandDescriptor>() {
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CSM_FIFO.CF_CMD_CLEAR_SCREEN,
                        Action = CMD_ClearScreen,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CSM_FIFO.CF_CMD_SEND_PATTERN_DATA,
                        Action = CMD_SendPatternData,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CSM_FIFO.CF_CMD_SET_INDEX,
                        Action = CMD_SetIndex,
                    },
                };
            }

            public void Draw()
            {
                for (int y = 0; y < TILEMAP_HEIGHT; y++)
                    for (int x = 0; x < TILEMAP_WIDTH; x++)
                    {
                        var patternIndex = tileMap[x, y];
                        for (int ty = 0, toffs = 0; ty < TILE_HEIGHT; ty++)
                            for (int tx = 0; tx < TILE_WIDTH; tx+=2, toffs++)
                            {
                                var sx = x * TILE_WIDTH + tx;
                                var sy = y * TILE_HEIGHT + ty;

                                byte c = patternTable[patternIndex][toffs];
                                byte p1 = (byte) ((c & 0xf0) >> 4);
                                byte p2 = (byte) ((c & 0xf));

                                GPU.PutPixel(sx, sy, p2);
                                GPU.PutPixel(sx + 1, sy, p1);
                            }
                    }
            }

            public void HandleCommandBytes(byte[] cmdBytes)
            {
                var cmd = commands.Find(x => x.Command == cmdBytes[0]);
                if (cmd == null)
                {
                    Console.WriteLine($"Unknown MODE command 0x{cmdBytes[0]:X2}");
                    Console.WriteLine($"FREEZING!");
                    Thread.Sleep(Timeout.Infinite);
                }

                cmd.Action(cmdBytes);
            }

            public void HandleTerminalCommandByte(byte cmdByte)
            {
                throw new NotImplementedException();
            }

            void CMD_ClearScreen(byte[] cmdBytes)
            {
                for (int y = 0; y < FB_HEIGHT; y++)
                    for (int x = 0; x < FB_WIDTH; x++)
                        tileMap[x, y] = cmdBytes[1];
            }

            void CMD_SendPatternData(byte[] cmdBytes)
            {
                var patternIndex = cmdBytes[1];
                for (int i = 0; i < 32; i++)
                {
                    patternTable[patternIndex][i] = cmdBytes[i + 2];
                }
            }

            void CMD_SetIndex(byte[] cmdBytes)
            {
                var x = cmdBytes[1] % TILEMAP_WIDTH;
                var y = cmdBytes[2] % TILEMAP_HEIGHT;
                var c = (byte) (cmdBytes[3]);

                tileMap[x, y] = c;
            }

            public void Reset() 
            {
                for (int y = 0, i = 0; y < TILEMAP_HEIGHT; y++)
                {
                    for (int x = 0; x < TILEMAP_WIDTH; x++, i++)
                    {
                        tileMap[x, y] = (byte)i;
                    }
                }

                for (int i = 0; i < 0x100; i++)
                {
                    for (int k = 0; k < 32; k++)
                    {
                        patternTable[i][k] = 0;
                    }
                }
            }
        }
    }
}
