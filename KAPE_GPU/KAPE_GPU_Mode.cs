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
        public class KAPE_GPU_Mode : IKAPE_GPU_Mode
        {
            protected KAPE_GPU gpu;
            public KAPE_GPU GPU { get => gpu; set => gpu = value; }

            public virtual bool IsTerminal => false;
            public struct Sprite
            {
                public byte TileIndex;
                public bool Active;
                public byte Alpha;
                public int X;
                public int Y;
            }
            public Sprite[] sprites = new Sprite[32];

            public class CommandDescriptor
            {
                public byte Command;
                public Action<byte[]> Action;
            }
            public List<CommandDescriptor> commands = new List<CommandDescriptor>();

            public KAPE_GPU_Mode(KAPE_GPU gpu)
            {
                this.gpu = gpu;

                commands.AddRange(new List<CommandDescriptor>() { 
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_SET_SPRITE_INDEX,
                        Action = CMD_SetSpriteIndex,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_SET_SPRITE_XY,
                        Action = CMD_SetSpriteXY,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_SET_SPRITE_ACTIVE,
                        Action = CMD_SetSpriteActive,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_SET_SPRITE_NOT_ACTIVE,
                        Action = CMD_SetSpriteNotActive,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_SET_SPRITE_ALPHA_COLOR,
                        Action = CMD_SetSpriteAlphaColor,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_SET_SPRITE_X,
                        Action = CMD_SetSpriteX,
                    },
                    new CommandDescriptor()
                    {
                        Command = KAPE_GPU_CMD_FIFO.CF_CMD_SET_SPRITE_Y,
                        Action = CMD_SetSpriteY,
                    },
                });
            }

            public virtual void Draw()
            {
            }

            public virtual void HandleCommandBytes(byte[] cmdBytes)
            {
                var cmd = commands.Find(x => x.Command == cmdBytes[0]);
                if (cmd == null)
                {
                    Console.WriteLine($"Unknown Mode command 0x{cmdBytes[0]:X2}");
                    Console.WriteLine($"FREEZING GPU!");
                    Thread.Sleep(Timeout.Infinite);
                }

                cmd.Action(cmdBytes);
            }

            public virtual void HandleTerminalCommandByte(byte cmdByte)
            {
            }

            public virtual void Reset()
            {
            }

            public void CMD_SetSpriteIndex(byte[] cmdBytes)
            {
                sprites[cmdBytes[1]].TileIndex = cmdBytes[2];
            }

            public void CMD_SetSpriteXY(byte[] cmdBytes)
            {
                sprites[cmdBytes[1]].X = cmdBytes[2];
                sprites[cmdBytes[1]].Y = cmdBytes[3] % 192;
            }

            public void CMD_SetSpriteActive(byte[] cmdBytes)
            {
                sprites[cmdBytes[1]].Active = true;
            }

            public void CMD_SetSpriteNotActive(byte[] cmdBytes)
            {
                sprites[cmdBytes[1]].Active = false;
            }

            public void CMD_SetSpriteAlphaColor(byte[] cmdBytes)
            {
                sprites[cmdBytes[1]].Alpha = (byte) (cmdBytes[2] & 0xf);

            }

            public void CMD_SetSpriteX(byte[] cmdBytes)
            {
                sprites[cmdBytes[1]].X = cmdBytes[2];

            }

            public void CMD_SetSpriteY(byte[] cmdBytes)
            {
                sprites[cmdBytes[1]].Y = cmdBytes[2] % 192;

            }
        }
    }
}
