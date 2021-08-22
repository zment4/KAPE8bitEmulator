namespace KAPE8bitEmulator {
	partial class KAPE_GPU {
		public static class KAPE_GPU_CSM_FIFO {
			public const int CF_CMD_SEND_CHARACTER            = 0x00;
			public const int CF_CMD_SEND_CHARACTER_Params     = 1;
			public const int CF_CMD_SET_COLOR                 = 0x01;
			public const int CF_CMD_TERMINAL                  = 0x02;
			public const int CF_CMD_SET_INDEX                 = 0x10;
			public const int CF_CMD_SET_INDEX_Params          = 3;
			public const int CF_CMD_SET_BORDER_COLOR          = 0x11;
			public const int CF_CMD_SET_SPRITE                = 0x20;
			public const int CF_CMD_SET_SPRITE_ACTIVE         = 0x21;
			public const int CF_CMD_SET_SPRITE_NOT_ACTIVE     = 0x22;
			public const int CF_CMD_SET_SPRITE_INDEX          = 0x25;
			public const int CF_CMD_SET_SPRITE_X              = 0x26;
			public const int CF_CMD_SET_SPRITE_Y              = 0x27;
			public const int CF_CMD_SET_SPRITE_HOTSPOTX       = 0x28;
			public const int CF_CMD_SET_SPRITE_HOTSPOTY       = 0x29;
			public const int CF_CMD_SET_SPRITE_ALPHA_COLOR    = 0x30;
			public const int CF_CMD_SETMODE_TEXT              = 0x40;
			public const int CF_CMD_SETMODE_GRAPHICS          = 0x41;
			public const int CF_CMD_SETMODE_COMBINED          = 0x42;
			public const int CF_CMD_SETMODE_LORES             = 0x43;
			public const int CF_CMD_CLEAR_SCREEN              = 0x4A;
			public const int CF_CMD_CLEAR_SCREEN_Params       = 1;
			public const int CF_CMD_FLUSH_FRAME               = 0x4B;
			public const int CF_CMD_DRAW_PIXEL                = 0x4C;
			public const int CF_CMD_DRAW_LINE                 = 0x4D;
			public const int CF_CMD_DRAW_LINE_Params          = 5;
			public const int CF_CMD_SET_COMBINED_BITMASK      = 0x50;
			public const int CF_CMD_SEND_PATTERN_DATA         = 0x80;
			public const int CF_CMD_SEND_PATTERN_DATA_Params  = 33;
			public const int CF_CMD_DIRECT_FRAMEBUFFER        = 0xfe;
			public const int CF_CMD_RESET_GPU                 = 0xff;
			public const int TERM_ESCAPE                      = 0x1B;
			public const int TERM_BACKSPACE                   = 0x08;
			public const int TERM_LINE_FEED                   = 0x0A;
			public const int TERM_CARRIAGE_RETURN             = 0x0D;
			public const int CF_TERM_COLOR_BINARY             = 0x01;
			public const int CF_TERM_DECORATION               = 0x02;
			public const int CF_TERM_CURSOR_POSITION          = 0x03;
			public const int CF_TERM_COLOR                    = 0x63;
			public const int CF_TERM_EXIT                     = 0xff;
		}
	}
}
