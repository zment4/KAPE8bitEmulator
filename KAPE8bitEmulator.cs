using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System.Threading;

namespace KAPE8bitEmulator
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class KAPE8bitEmulator : Game
    {
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
        }


        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            _KAPE_GPU = new KAPE_GPU(this);
            Components.Add(_KAPE_GPU);

            _SRAM64K = new SRAM64k();
            _SRAM64K.FillRam(@"D:\6502\scatternoid.bin");

            _KAPE_CPU = new CPU_6502();

            _KAPE_GPU.RegisterWrite(_KAPE_CPU);
            _SRAM64K.RegisterMap(_KAPE_CPU);

            _KAPE_CPU.Reset();
            _KAPE_GPU.Reset();

            _KAPE_CPU.ResetFinished = _KAPE_GPU.ResetFinished;
            _KAPE_GPU.EnterCMDQThread();
            _KAPE_CPU.EnterCycleLoop();

            originalTitle = "KAPE8bitEmulator";

            new Thread(() =>
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                long lastNMIticks = 0;
                long ticksPerNMI = 10000000 / NMI_FPS;

                while(true)
                {
                    var elapsedTicks = sw.ElapsedTicks;
                    if (elapsedTicks - lastNMIticks > ticksPerNMI)
                    {
                        _KAPE_CPU.TriggerNMI();
                        lastNMIticks = elapsedTicks;
                    }

                    Thread.Sleep(0);
                }
            }).Start();
            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
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

            Window.Title = $"{originalTitle} - {cps:F3} {unit}";
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

            lastKeyState = currentKeyState;
            //for (int y = 0; y < 192; y++)
            //    for (int x = 0; x < 256; x++)
            //        _KAPE_GPU.PutPixel(x, y, rand.Next() % 0xf);

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            _drawGameTime = gameTime;

            GraphicsDevice.Clear(Color.Black);

            // TODO: Add your drawing code here

            base.Draw(gameTime);
        }
    }
}
