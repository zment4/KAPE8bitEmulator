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
        public class KAPE_GPU_GraphicsMode : KAPE_GPU_Mode
        {

            const int TILE_WIDTH = 8;
            const int TILE_HEIGHT = 8;

            const int TILEMAP_WIDTH = 32;
            const int TILEMAP_HEIGHT = 24;

            byte[,] tileMap = new byte[TILEMAP_WIDTH, TILEMAP_HEIGHT];
            byte[][] patternTable = new byte[0x100][];

            public KAPE_GPU_GraphicsMode(KAPE_GPU gpu) : base(gpu)
            {
                for (int i = 0; i < patternTable.Length; i++)
                {
                    patternTable[i] = new byte[32];
                }

                commands.AddRange(new List<CommandDescriptor>() {
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_CLEAR_SCREEN,
                        Action = CMD_ClearScreen,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_SEND_PATTERN_DATA,
                        Action = CMD_SendPatternData,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_SET_INDEX,
                        Action = CMD_SetIndex,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_FLUSH_FRAME,
                        Action = CMD_IgnoreCommand,
                    },
                });
            }

            public override void Draw()
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

                for (int i = 0; i < 32; i++)
                {
                    if (sprites[i].Active)
                        DrawSprite(i);
                }

                base.Draw();
            }

            void DrawSprite(int index)
            {
                var spriteY = sprites[index].Y;
                var spriteX = sprites[index].X / 2 * 2;
                var patternIndex = sprites[index].TileIndex;
                for (int y = spriteY, toffs = 0; y < Math.Min(spriteY + 8, FB_HEIGHT);  y++)
                {
                    for (int x = spriteX; x < spriteX + 8; x += 2, toffs++)
                    {
                        if (x >= FB_WIDTH - 1) continue;

                        byte c = patternTable[patternIndex][toffs];
                        byte p1 = (byte)((c & 0xf0) >> 4);
                        byte p2 = (byte)((c & 0xf));
                        if (p2 != sprites[index].Alpha) GPU.PutPixel(x, y, p2);
                        if (p1 != sprites[index].Alpha) GPU.PutPixel(x+1, y, p1);
                    }
                }
            }

            public override void HandleCommandBytes(byte[] cmdBytes)
            {
                var cmd = commands.Find(x => x.Command == cmdBytes[0]);

                if (cmd == null)
                {
                    Console.WriteLine($"Unknown GraphicsMode command 0x{cmdBytes[0]:X2}");
                    Console.WriteLine($"Falling back to base...");

                    base.HandleCommandBytes(cmdBytes);
                } else cmd.Action(cmdBytes);
            }

            public override void HandleTerminalCommandByte(byte cmdByte)
            {
                throw new NotImplementedException();
            }

            void CMD_IgnoreCommand(byte[] cmdBytes)
            {
                // Just ignore the command. This is for commands that have no effect for this specific mode
                // TODO: maybe log something. 
            }

            void CMD_ClearScreen(byte[] cmdBytes)
            {
                for (int y = 0; y < TILEMAP_HEIGHT; y++)
                    for (int x = 0; x < TILEMAP_WIDTH; x++)
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

            public override void Reset() 
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
