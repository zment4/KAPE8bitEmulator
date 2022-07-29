using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;
using System.Threading;
using HighPrecisionTimer;
using System.Linq;

namespace KAPE8bitEmulator
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class KAPE8bitEmulator : Game
    {
        public static bool DebugMode = false;
        const int NMI_FPS = 62;

        GraphicsDeviceManager _graphics;
        SpriteBatch _spriteBatch;
        GameTime _drawGameTime;

        KAPE_GPU _KAPE_GPU;
        CPU_6502 _KAPE_CPU;
        SRAM64k _SRAM64K;

        string originalTitle;
        public KAPE8bitEmulator()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.HardwareModeSwitch = false;

            Content.RootDirectory = "Content";

            Window.AllowUserResizing = true;

            if (Program.Args.Length > 1) 
                DebugMode = Program.Args.Contains("-debug");
        }

        MultimediaTimer NMITimer = new MultimediaTimer() { Interval = 4, Resolution = 0 };

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            string fileName = Program.Args[0];


            _KAPE_GPU = new KAPE_GPU(this);
            Components.Add(_KAPE_GPU);

            _SRAM64K = new SRAM64k();
            _SRAM64K.FillRam(fileName);

            _KAPE_CPU = new CPU_6502();

            // Add input hooks
            _KAPE_CPU.RegisterIRQ(InputIRQState);

            _KAPE_CPU.RegisterWrite(0xf7ff, 0xf7ff, OutputToConsole);

            _KAPE_GPU.RegisterWrite(_KAPE_CPU);
            _SRAM64K.RegisterMap(_KAPE_CPU);

            _KAPE_CPU.RegisterRead(0x8000, 0x8000, ResetInputIRQState);
            _KAPE_CPU.RegisterRead(0xA000, 0xA000, ReadInput);

            _KAPE_CPU.Reset();
            _KAPE_GPU.Reset();

            _KAPE_CPU.ResetFinished = _KAPE_GPU.ResetFinished;
            _KAPE_GPU.EnterCMDQThread();
            _KAPE_CPU.EnterCycleLoop();

            originalTitle = $"KAPE8bitEmulator - {fileName}";

            Stopwatch sw = new Stopwatch();
            sw.Start();

            long lastNMIticks = 0;
            long ticksPerNMI = 10000000 / NMI_FPS;

            NMITimer.Elapsed += (o, e) =>
            {
                var elapsedTicks = sw.ElapsedTicks;
                var deltaTicks = elapsedTicks - lastNMIticks;
                if (deltaTicks >= ticksPerNMI)
                {
                    _KAPE_CPU.TriggerNMI();
                    lastNMIticks += deltaTicks - (deltaTicks - ticksPerNMI);
                }
            };

            NMITimer.Start();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            _spriteBatch = new SpriteBatch(GraphicsDevice);

        }

        Rectangle _windowSize;
        System.Random rand = new System.Random();

        KeyboardState currentKeyState = new KeyboardState();
        KeyboardState lastKeyState = new KeyboardState();
        bool PressedThisFrame(Keys key) => currentKeyState.IsKeyDown(key) && !lastKeyState.IsKeyDown(key);
        protected override void Update(GameTime gameTime)
        {
            string unit = "Hz";
            float cps = _KAPE_CPU.CurrentCyclesPerSecond;
            if (cps > 1000) { cps /= 1000; unit = "kHz"; }
            if (cps > 1000) { cps /= 1000; unit = "MHz"; }

            Window.Title = $"{originalTitle} - {cps:F3} {unit} - {_KAPE_CPU.CurrentNMIPerSecond} NPS - {1f / gameTime.ElapsedGameTime.TotalSeconds:N2} FPS";
            currentKeyState = Keyboard.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if (PressedThisFrame(Keys.F4))
            {
                if (!_graphics.IsFullScreen)
                {
                    _windowSize = Window.ClientBounds;
                    _graphics.PreferredBackBufferWidth = GraphicsDevice.DisplayMode.Width;
                    _graphics.PreferredBackBufferHeight = GraphicsDevice.DisplayMode.Height;
                    _graphics.IsFullScreen = true;
                    _graphics.ApplyChanges();
                } else
                {
                    _graphics.PreferredBackBufferWidth = _windowSize.Width;
                    _graphics.PreferredBackBufferHeight = _windowSize.Height;
                    _graphics.IsFullScreen = false;
                    _graphics.ApplyChanges();
                }
            }

            if (PressedThisFrame(Keys.F1))
            {
                _KAPE_CPU.Stop();

                _KAPE_GPU.Reset();

                _KAPE_CPU.Reset();
                _KAPE_CPU.Start();
            }

            bool p1_up = currentKeyState.IsKeyDown(Keys.W);
            bool p1_down = currentKeyState.IsKeyDown(Keys.S);
            bool p1_left = currentKeyState.IsKeyDown(Keys.A);
            bool p1_right = currentKeyState.IsKeyDown(Keys.D);

            bool p2_up = currentKeyState.IsKeyDown(Keys.Up);
            bool p2_down = currentKeyState.IsKeyDown(Keys.Down);
            bool p2_left = currentKeyState.IsKeyDown(Keys.Left);
            bool p2_right = currentKeyState.IsKeyDown(Keys.Right);

            EncodeInputForIRQ(p1_up, p1_down, p1_left, p1_right, p2_up, p2_down, p2_left, p2_right);

            lastKeyState = currentKeyState;

            base.Update(gameTime);
        }

        private void EncodeInputForIRQ(bool p1_up, bool p1_down, bool p1_left, bool p1_right, bool p2_up, bool p2_down, bool p2_left, bool p2_right)
        {
            lastEncodedInput = encodedInput;
            encodedInput = 0;
            if (p1_up) encodedInput |= 0b00010000;
            if (p1_down) encodedInput |= 0b01000000;
            if (p1_left) encodedInput |= 0b10000000;
            if (p1_right) encodedInput |= 0b00100000;

            if (p2_up) encodedInput |= 0b00000001;
            if (p2_down) encodedInput |= 0b00000100;
            if (p2_left) encodedInput |= 0b00001000;
            if (p2_right) encodedInput |= 0b00000010;

            if (encodedInput != lastEncodedInput)
            {
                pullInputIRQLow = true;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            _drawGameTime = gameTime;

            GraphicsDevice.Clear(Color.Black);

            base.Draw(gameTime);
        }

        byte lastEncodedInput = 0;
        byte encodedInput = 0;
        bool pullInputIRQLow = false;

        protected bool InputIRQState() => pullInputIRQLow;

        protected byte ReadInput(UInt16 address) => encodedInput;

        protected byte ResetInputIRQState(UInt16 address) 
        {
            pullInputIRQLow = false;
            return 0;
        }

        protected void OutputToConsole(UInt16 address, byte value)
        {
            Console.WriteLine($"DBG: {value.ToString().PadLeft(3)} ${value:X2} %{Convert.ToString(value, 2).PadLeft(8, '0')}");
        }
    }
}
