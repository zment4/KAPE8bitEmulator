using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KAPE8bitEmulator
{
    public partial class KAPE_GPU : DrawableGameComponent
    {
        const int FB_BORDER = 8;
        const int FB_WIDTH = 256;
        const int FB_HEIGHT = 192;

        IKAPE_GPU_Mode CurrentMode;
        List<IKAPE_GPU_Mode> gpuModes = new List<IKAPE_GPU_Mode>();

        Color[] _frameBuffer = new Color[FB_WIDTH * FB_HEIGHT];
        Texture2D _outputTexture;
        SpriteBatch _spriteBatch;

        public AutoResetEvent ResetFinished = new AutoResetEvent(false);

        Color[] _RGBI_palette = new Color[]
        {
            new Color(0x00, 0x00, 0x00), // black
			new Color(0xAA, 0x00, 0x00), // red
			new Color(0x00, 0xAA, 0x00), // green
			new Color(0xAA, 0xAA, 0x00), // yellow
			new Color(0x00, 0x00, 0xAA), // blue
			new Color(0xAA, 0x00, 0xAA), // magenta
			new Color(0x00, 0xAA, 0xAA), // cyan
			new Color(0xAA, 0xAA, 0xAA), // light gray
			new Color(0x55, 0x55, 0x55), // dark gray
			new Color(0xFF, 0x55, 0x55), // light red
			new Color(0x55, 0xFF, 0x55), // light green
			new Color(0xFF, 0xFF, 0x55), // light yellow
			new Color(0x55, 0x55, 0xFF), // light blue
			new Color(0xFF, 0x55, 0xFF), // light magenta
			new Color(0x55, 0xFF, 0xFF), // light cyan
			new Color(0xFF, 0xFF, 0xFF), // white
        };

        public KAPE_GPU(Game game) : base(game)
        {
            gpuModes.Add(new KAPE_GPU_TextMode(this));
            gpuModes.Add(new KAPE_GPU_LoresMode(this));
            gpuModes.Add(new KAPE_GPU_GraphicsMode(this));
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _outputTexture = new Texture2D(GraphicsDevice, FB_WIDTH+FB_BORDER*2, FB_HEIGHT+FB_BORDER*2);
            _frameBuffer = Enumerable.Repeat(Color.Black, _outputTexture.Width * _outputTexture.Height).ToArray();

            base.LoadContent();
        }

        public override void Draw(GameTime gameTime)
        {
            CurrentMode.Draw();

            var xScale = (float)Game.Window.ClientBounds.Width / _outputTexture.Width;
            var yScale = (float)Game.Window.ClientBounds.Height / _outputTexture.Height;

            var xPos = 0.5f * _outputTexture.Width;
            var yPos = 0.5f * _outputTexture.Height;

            if (xScale > yScale)
            {
                xPos *= (xScale / yScale);
                xScale = yScale;
            }
            if (yScale > xScale)
            {
                yPos *= (yScale / xScale);
                yScale = xScale;
            }

            _spriteBatch.Begin(
                SpriteSortMode.Deferred,
                null,
                SamplerState.AnisotropicWrap,
                null,
                null,
                null,
                Matrix.CreateScale(xScale, yScale, 1f)
            );

            _outputTexture.SetData(_frameBuffer);
            _spriteBatch.Draw(_outputTexture, new Vector2(xPos, yPos), null, Color.White, 0f, new Vector2(0.5f * _outputTexture.Width, 0.5f * _outputTexture.Height), Vector2.One, SpriteEffects.None, 0f);

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        internal void PutPixel(int x, int y, int c)
        {
            x += FB_BORDER;
            y += FB_BORDER;
            _frameBuffer[y * (FB_WIDTH+FB_BORDER*2) + x] = _RGBI_palette[c];
        }

        AutoResetEvent CMDQNotEmpty = new AutoResetEvent(false);
        Queue<byte> CMDQ = new Queue<byte>();

        CPU_6502 cpu;
        public void RegisterWrite(CPU_6502 cpu)
        {
            this.cpu = cpu;
            cpu.RegisterWrite(0x8000, 0xbfff, Write);
        }

        readonly object lockCMDQ = new object();

        void Write(UInt16 address, byte val)
        {
            if (resetRequested)
                return;

            lock (lockCMDQ)
            {
                CMDQ.Enqueue(val);
            }

            CMDQNotEmpty.Set();
        }

        public void EnterCMDQThread()
        {
            new Thread(() =>
            {
                while (true)
                {
                    if (resetRequested)
                    {
                        Reset();
                    }

                    CMDQNotEmpty.WaitOne();
                    while (CMDQ.Count > 0)
                    {
                        byte cmdByte = 0;
                        lock (lockCMDQ)
                        {
                            if (CMDQ.Count > 2048)
                            {
                                Console.WriteLine("CMDQ Buffer OVERFLOW! Discarding all over 2K");
                                // Scrap all CMDQ bytes over 2K (to mimic HW)
                                CMDQ = new Queue<byte>(CMDQ.Take(2048));
                            }

                            cmdByte = CMDQ.Dequeue();
                        }

                        HandleCMDByte(cmdByte);

                        if (resetRequested)
                        {
                            Reset();
                            ResetFinished.WaitOne();
                        }
                    }
                    // Introduce a waitstate to simulate screen updates and command handling waitstates
                    // TODO: make it more conformant with real elapsed time instead
                    Thread.Sleep(10);
                }
            }) { IsBackground = true }.Start();
        }

        byte[] cmdBuffer = new byte[34];
        byte currentIndex = 0;
        byte byteCountRemaining = 0;

        void HandleCMDByte(byte cmdByte)
        {
            cmdBuffer[currentIndex] = cmdByte;
            currentIndex++;

            if (byteCountRemaining > 0)
            {
                byteCountRemaining--;

                if (byteCountRemaining == 0)
                {
                    CurrentMode.HandleCommandBytes(cmdBuffer);
                    currentIndex = 0;
                }

                return;
            }

            if (CurrentMode.IsTerminal)
            {
                CurrentMode.HandleTerminalCommandByte(cmdByte);
                currentIndex = 0;
                return;
            }

            switch (cmdByte)
            {
                case KAPE_GPU_CSM_FIFO.CF_CMD_SEND_CHARACTER:
                    byteCountRemaining = KAPE_GPU_CSM_FIFO.CF_CMD_SEND_CHARACTER_Params;
                    break;
                case KAPE_GPU_CSM_FIFO.CF_CMD_SET_INDEX:
                    byteCountRemaining = KAPE_GPU_CSM_FIFO.CF_CMD_SET_INDEX_Params;
                    break;
                case KAPE_GPU_CSM_FIFO.CF_CMD_CLEAR_SCREEN:
                    byteCountRemaining = KAPE_GPU_CSM_FIFO.CF_CMD_CLEAR_SCREEN_Params;
                    break;
                case KAPE_GPU_CSM_FIFO.CF_CMD_SEND_PATTERN_DATA:
                    byteCountRemaining = KAPE_GPU_CSM_FIFO.CF_CMD_SEND_PATTERN_DATA_Params;
                    break;
                case KAPE_GPU_CSM_FIFO.CF_CMD_FLUSH_FRAME:
                    CurrentMode.HandleCommandBytes(cmdBuffer);
                    currentIndex = 0;
                    break;
                case KAPE_GPU_CSM_FIFO.CF_CMD_DRAW_LINE:
                    byteCountRemaining = KAPE_GPU_CSM_FIFO.CF_CMD_DRAW_LINE_Params;
                    break;
                case KAPE_GPU_CSM_FIFO.CF_CMD_SETMODE_TEXT:
                    CurrentMode = gpuModes.Find(x => x is KAPE_GPU_TextMode);
                    currentIndex = 0;
                    break;
                case KAPE_GPU_CSM_FIFO.CF_CMD_SETMODE_LORES:
                    CurrentMode = gpuModes.Find(x => x is KAPE_GPU_LoresMode);
                    currentIndex = 0;
                    break;
                case KAPE_GPU_CSM_FIFO.CF_CMD_SETMODE_GRAPHICS:
                    CurrentMode = gpuModes.Find(x => x is KAPE_GPU_GraphicsMode);
                    currentIndex = 0;
                    byteCountRemaining = 0;
                    break;
                case KAPE_GPU_CSM_FIFO.CF_CMD_TERMINAL:
                    CurrentMode.HandleCommandBytes(cmdBuffer);
                    currentIndex = 0;
                    break;
                default:
                    Console.WriteLine($"\nUnknown GPU Command: 0x{cmdByte:X2}");
                    Console.WriteLine($"FREEZING GPU");
                    Thread.Sleep(Timeout.Infinite);
                    currentIndex = 0;
                    break;
            }
        }

        bool resetRequested = false;

        public void RequestReset()
        {
            resetRequested = true;
        }

        public void Reset()
        {
            lock (lockCMDQ)
            {
                CMDQ.Clear();
            }

            gpuModes.ForEach(x => x.Reset());

            byteCountRemaining = 0;
            currentIndex = 0;

            CurrentMode = gpuModes.Find(x => x is KAPE_GPU_TextMode);

            resetRequested = false;
        }
    }
}
