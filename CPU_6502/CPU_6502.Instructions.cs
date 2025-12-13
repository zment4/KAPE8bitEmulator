using System;
using System.Linq;

namespace KAPE8bitEmulator
{
    public partial class CPU_6502
    {
        // 87 opcode+addrmode combos
        public partial class Instructions
        {
            public const byte JMP_ABS = 0x4C;
            public const byte CLD_IMP = 0xD8;
            public const byte LDX_IMM = 0xA2;
            public const byte LDY_IMM = 0xA0;
            public const byte TXS_IMP = 0x9A;
            public const byte JSR_ABS = 0x20;
            public const byte BCC_REL = 0x90;
            public const byte PHA_IMP = 0x48;
            public const byte INC_ABS = 0xEE;
            public const byte BNE_REL = 0xD0;
            public const byte PLA_IMP = 0x68;
            public const byte RTI_IMP = 0x40;
            public const byte RTS_IMP = 0x60;
            public const byte CLI_IMP = 0x58;
            public const byte STX_ZPG = 0x86;
            public const byte SEC_IMP = 0x38;
            public const byte CLC_IMP = 0x18;
            public const byte SBC_IMM = 0xE9;
            public const byte LDY_ABS = 0xAC;
            public const byte BEQ_REL = 0xF0;
            public const byte DEC_ABS = 0xCE;
            public const byte DEC_ZPG = 0xC6;
            public const byte INX_IMP = 0xE8;
            public const byte AND_IMM = 0x29;
            public const byte ASL_ACC = 0x0A;
            public const byte EOR_ZPG = 0x45;
            public const byte LSR_ACC = 0x4A;
            public const byte INY_IMP = 0xC8;
            public const byte INC_ZPG = 0xE6;
            public const byte TYA_IMP = 0x98;
            public const byte DEX_IMP = 0xCA;
            public const byte LDX_ABS = 0xAE;
            public const byte TXA_IMP = 0x8A;
            public const byte TAY_IMP = 0xA8;
            public const byte TAX_IMP = 0xAA;
            public const byte DEY_IMP = 0x88;
            public const byte BMI_REL = 0x30;
            public const byte SBC_ABS = 0xED;
            public const byte CPX_IMM = 0xE0;
            public const byte ORA_IMM = 0x09;
            public const byte JMP_ABI = 0x6C;
            public const byte ORA_ZPG = 0x05;
            public const byte BCS_REL = 0xB0;
            public const byte STX_ABS = 0x8E;
            public const byte ORA_ABS = 0x0D;
            public const byte PHP_IMP = 0x08;
            public const byte PLP_IMP = 0x28;
            public const byte EOR_ABS = 0x4D;
            public const byte EOR_IMM = 0x49;

            public enum AddressingModeEnum
            {
                Accumulator,
                Immediate,
                Implied,
                Relative,
                Absolute,
                ZeroPage,
                Indirect,
                AbsoluteIndexedY,
                AbsoluteIndexedX,
                ZeroPageIndexedX,
                ZeroPageIndexedY,
                IndexedIndirect,
                IndirectIndexed,
                AbsoluteIndirect,
            }

            public class InstructionDescriptor
            {
                public byte Instruction;
                public int Cycles;
                public Action Action;
                public string Mnemonic;
                public AddressingModeEnum AddressingMode;
            }

            readonly InstructionDescriptor[] instructionDescriptors = new InstructionDescriptor[0x100];

            CPU_6502 CPU;

            public InstructionDescriptor this[byte key] => instructionDescriptors[key];

            public Instructions(CPU_6502 CPU)
            {
                this.CPU = CPU;

                // Initialize all the instructions, their functions and how many cycles they take (for counting/limiting speed)
                instructionDescriptors[JMP_ABS] = new InstructionDescriptor()
                {
                    Instruction = JMP_ABS,
                    Action = I_JMP_ABS,
                    Cycles = 3,
                    Mnemonic = "JMP",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                // Initialize all the instructions, their functions and how many cycles they take (for counting/limiting speed)
                instructionDescriptors[JMP_ABI] = new InstructionDescriptor()
                {
                    Instruction = JMP_ABI,
                    Action = I_JMP_ABI,
                    Cycles = 5,
                    Mnemonic = "JMP",
                    AddressingMode = AddressingModeEnum.AbsoluteIndirect,
                };

                instructionDescriptors[LDA_IMM] = new InstructionDescriptor()
                {
                    Instruction = LDA_IMM,
                    Action = I_LDA_IMM,
                    Cycles = 2,
                    Mnemonic = "LDA",
                    AddressingMode = AddressingModeEnum.Immediate,
                };

                instructionDescriptors[LDA_ABS] = new InstructionDescriptor()
                {
                    Instruction = LDA_ABS,
                    Action = I_LDA_ABS,
                    Cycles = 4,
                    Mnemonic = "LDA",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[LDA_IIX] = new InstructionDescriptor()
                {
                    Instruction = LDA_IIX,
                    Action = I_LDA_IIX,
                    Cycles = 0, // Dynamic, 5 or 6 if page boundary crossed
                    Mnemonic = "LDA",
                    AddressingMode = AddressingModeEnum.IndirectIndexed,
                };

                instructionDescriptors[STA_ABS] = new InstructionDescriptor()
                {
                    Instruction = STA_ABS,
                    Action = I_STA_ABS,
                    Cycles = 4,
                    Mnemonic = "STA",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[STA_ZPG] = new InstructionDescriptor()
                {
                    Instruction = STA_ZPG,
                    Action = I_STA_ZPG,
                    Cycles = 3,
                    Mnemonic = "STA",
                    AddressingMode = AddressingModeEnum.ZeroPage,
                };

                instructionDescriptors[STA_IIX] = new InstructionDescriptor()
                {
                    Instruction = STA_IIX,
                    Action = I_STA_IIX,
                    Cycles = 6,
                    Mnemonic = "STA",
                    AddressingMode = AddressingModeEnum.IndirectIndexed,
                };

                instructionDescriptors[STY_ZPG] = new InstructionDescriptor()
                {
                    Instruction = STY_ZPG,
                    Action = I_STY_ZPG,
                    Cycles = 3,
                    Mnemonic = "STY",
                    AddressingMode = AddressingModeEnum.ZeroPage,
                };

                instructionDescriptors[CLD_IMP] = new InstructionDescriptor()
                {
                    Instruction = CLD_IMP,
                    Action = I_CLD_IMP,
                    Cycles = 2,
                    Mnemonic = "CLD",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[LDX_IMM] = new InstructionDescriptor()
                {
                    Instruction = LDX_IMM,
                    Action = I_LDX_IMM,
                    Cycles = 2,
                    Mnemonic = "LDX",
                    AddressingMode = AddressingModeEnum.Immediate,
                };

                instructionDescriptors[LDY_IMM] = new InstructionDescriptor()
                {
                    Instruction = LDY_IMM,
                    Action = I_LDY_IMM,
                    Cycles = 2,
                    Mnemonic = "LDY",
                    AddressingMode = AddressingModeEnum.Immediate,
                };

                instructionDescriptors[TXS_IMP] = new InstructionDescriptor()
                {
                    Instruction = TXS_IMP,
                    Action = I_TXS_IMP,
                    Cycles = 2,
                    Mnemonic = "TXS",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[JSR_ABS] = new InstructionDescriptor()
                {
                    Instruction = JSR_ABS,
                    Action = I_JSR_ABS,
                    Cycles = 6,
                    Mnemonic = "JSR",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[CMP_IMM] = new InstructionDescriptor()
                {
                    Instruction = CMP_IMM,
                    Action = I_CMP_IMM,
                    Cycles = 2,
                    Mnemonic = "CMP",
                    AddressingMode = AddressingModeEnum.Immediate,
                };

                instructionDescriptors[BCC_REL] = new InstructionDescriptor()
                {
                    Instruction = BCC_REL,
                    Action = I_BCC_REL,
                    Cycles = 0, // dynamic, depends on page boundary crossing, set in action
                    Mnemonic = "BCC",
                    AddressingMode = AddressingModeEnum.Relative,
                };

                instructionDescriptors[PHA_IMP] = new InstructionDescriptor()
                {
                    Instruction = PHA_IMP,
                    Action = I_PHA_IMP,
                    Cycles = 3,
                    Mnemonic = "PHA",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[INC_ABS] = new InstructionDescriptor()
                {
                    Instruction = INC_ABS,
                    Action = I_INC_ABS,
                    Cycles = 6,
                    Mnemonic = "INC",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[CMP_ABS] = new InstructionDescriptor()
                {
                    Instruction = CMP_ABS,
                    Action = I_CMP_ABS,
                    Cycles = 4,
                    Mnemonic = "CMP",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[BNE_REL] = new InstructionDescriptor()
                {
                    Instruction = BNE_REL,
                    Action = I_BNE_REL,
                    Cycles = 0, // dynamic, depends on page boundary crossing, set in action
                    Mnemonic = "BNE",
                    AddressingMode = AddressingModeEnum.Relative,
                };

                instructionDescriptors[BMI_REL] = new InstructionDescriptor()
                {
                    Instruction = BMI_REL,
                    Action = I_BMI_REL,
                    Cycles = 0, // dynamic, depends on page boundary crossing, set in action
                    Mnemonic = "BMI",
                    AddressingMode = AddressingModeEnum.Relative,
                };

                instructionDescriptors[PLA_IMP] = new InstructionDescriptor()
                {
                    Instruction = PLA_IMP,
                    Action = I_PLA_IMP,
                    Cycles = 4, 
                    Mnemonic = "PLA",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[RTI_IMP] = new InstructionDescriptor()
                {
                    Instruction = RTI_IMP,
                    Action = I_RTI_IMP,
                    Cycles = 6,
                    Mnemonic = "RTI",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[RTS_IMP] = new InstructionDescriptor()
                {
                    Instruction = RTS_IMP,
                    Action = I_RTS_IMP,
                    Cycles = 6,
                    Mnemonic = "RTS",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[CLI_IMP] = new InstructionDescriptor()
                {
                    Instruction = CLI_IMP,
                    Action = I_CLI_IMP,
                    Cycles = 2,
                    Mnemonic = "CLI",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[SEC_IMP] = new InstructionDescriptor()
                {
                    Instruction = SEC_IMP,
                    Action = I_SEC_IMP,
                    Cycles = 2,
                    Mnemonic = "SEC",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[CLC_IMP] = new InstructionDescriptor()
                {
                    Instruction = CLC_IMP,
                    Action = I_CLC_IMP,
                    Cycles = 2,
                    Mnemonic = "CLC",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[STX_ZPG] = new InstructionDescriptor()
                {
                    Instruction = STX_ZPG,
                    Action = I_STX_ZPG,
                    Cycles = 3,
                    Mnemonic = "STX",
                    AddressingMode = AddressingModeEnum.ZeroPage,
                };

                instructionDescriptors[SBC_IMM] = new InstructionDescriptor()
                {
                    Instruction = SBC_IMM,
                    Action = I_SBC_IMM,
                    Cycles = 2,
                    Mnemonic = "SBC",
                    AddressingMode = AddressingModeEnum.Immediate,
                };

                instructionDescriptors[SBC_ABS] = new InstructionDescriptor()
                {
                    Instruction = SBC_ABS,
                    Action = I_SBC_ABS,
                    Cycles = 4,
                    Mnemonic = "SBC",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[ADC_IMM] = new InstructionDescriptor()
                {
                    Instruction = ADC_IMM,
                    Action = I_ADC_IMM,
                    Cycles = 2,
                    Mnemonic = "ADC",
                    AddressingMode = AddressingModeEnum.Immediate,
                };

                instructionDescriptors[ADC_ABS] = new InstructionDescriptor()
                {
                    Instruction = ADC_ABS,
                    Action = I_ADC_ABS,
                    Cycles = 4,
                    Mnemonic = "ADC",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[ADC_ZPG] = new InstructionDescriptor()
                {
                    Instruction = ADC_ZPG,
                    Action = I_ADC_ZPG,
                    Cycles = 3,
                    Mnemonic = "ADC",
                    AddressingMode = AddressingModeEnum.ZeroPage,
                };

                instructionDescriptors[LDY_ABS] = new InstructionDescriptor()
                {
                    Instruction = LDY_ABS,
                    Action = I_LDY_ABS,
                    Cycles = 4,
                    Mnemonic = "LDY",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[LDA_ZPG] = new InstructionDescriptor()
                {
                    Instruction = LDA_ZPG,
                    Action = I_LDA_ZPG,
                    Cycles = 3,
                    Mnemonic = "LDA",
                    AddressingMode = AddressingModeEnum.ZeroPage,
                };

                instructionDescriptors[LDA_ABX] = new InstructionDescriptor()
                {
                    Instruction = LDA_ABX,
                    Action = I_LDA_ABX,
                    Cycles = 3,
                    Mnemonic = "LDA",
                    AddressingMode = AddressingModeEnum.AbsoluteIndexedX,
                };

                instructionDescriptors[BEQ_REL] = new InstructionDescriptor()
                {
                    Instruction = BEQ_REL,
                    Action = I_BEQ_REL,
                    Cycles = 0, // Dynamic, set after instructions
                    Mnemonic = "BEQ",
                    AddressingMode = AddressingModeEnum.Relative,
                };

                instructionDescriptors[DEC_ABS] = new InstructionDescriptor()
                {
                    Instruction = DEC_ABS,
                    Action = I_DEC_ABS,
                    Cycles = 3,
                    Mnemonic = "DEC",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[DEC_ZPG] = new InstructionDescriptor()
                {
                    Instruction = DEC_ZPG,
                    Action = I_DEC_ZPG,
                    Cycles = 5,
                    Mnemonic = "DEC",
                    AddressingMode = AddressingModeEnum.ZeroPage,
                };

                instructionDescriptors[INX_IMP] = new InstructionDescriptor()
                {
                    Instruction = INX_IMP,
                    Action = I_INX_IMP,
                    Cycles = 2, 
                    Mnemonic = "INX",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[AND_IMM] = new InstructionDescriptor()
                {
                    Instruction = AND_IMM,
                    Action = I_AND_IMM,
                    Cycles = 2,
                    Mnemonic = "AND",
                    AddressingMode = AddressingModeEnum.Immediate,
                };

                instructionDescriptors[ORA_IMM] = new InstructionDescriptor()
                {
                    Instruction = ORA_IMM,
                    Action = I_ORA_IMM,
                    Cycles = 2,
                    Mnemonic = "ORA",
                    AddressingMode = AddressingModeEnum.Immediate,
                };

                instructionDescriptors[ORA_ZPG] = new InstructionDescriptor()
                {
                    Instruction = ORA_ZPG,
                    Action = I_ORA_ZPG,
                    Cycles = 3,
                    Mnemonic = "ORA",
                    AddressingMode = AddressingModeEnum.ZeroPage,
                };

                instructionDescriptors[ASL_ACC] = new InstructionDescriptor()
                {
                    Instruction = ASL_ACC,
                    Action = I_ASL_ACC,
                    Cycles = 2,
                    Mnemonic = "ASL",
                    AddressingMode = AddressingModeEnum.Accumulator,
                };

                instructionDescriptors[EOR_ZPG] = new InstructionDescriptor()
                {
                    Instruction = EOR_ZPG,
                    Action = I_EOR_ZPG,
                    Cycles = 3,
                    Mnemonic = "EOR",
                    AddressingMode = AddressingModeEnum.ZeroPage,
                };

                instructionDescriptors[EOR_ABS] = new InstructionDescriptor()
                {
                    Instruction = EOR_ABS,
                    Action = I_EOR_ABS,
                    Cycles = 3,
                    Mnemonic = "EOR",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[LSR_ACC] = new InstructionDescriptor()
                {
                    Instruction = LSR_ACC,
                    Action = I_LSR_ACC,
                    Cycles = 2,
                    Mnemonic = "LSR",
                    AddressingMode = AddressingModeEnum.Accumulator,
                };

                instructionDescriptors[INY_IMP] = new InstructionDescriptor()
                {
                    Instruction = INY_IMP,
                    Action = I_INY_IMP,
                    Cycles = 2,
                    Mnemonic = "INY",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[INC_ZPG] = new InstructionDescriptor()
                {
                    Instruction = INC_ZPG,
                    Action = I_INC_ZPG,
                    Cycles = 5,
                    Mnemonic = "INC",
                    AddressingMode = AddressingModeEnum.ZeroPage,
                };

                instructionDescriptors[CPY_IMM] = new InstructionDescriptor()
                {
                    Instruction = CPY_IMM,
                    Action = I_CPY_IMM,
                    Cycles = 2,
                    Mnemonic = "CPY",
                    AddressingMode = AddressingModeEnum.Immediate,
                };

                instructionDescriptors[CPX_IMM] = new InstructionDescriptor()
                {
                    Instruction = CPX_IMM,
                    Action = I_CPX_IMM,
                    Cycles = 2,
                    Mnemonic = "CPX",
                    AddressingMode = AddressingModeEnum.Immediate,
                };

                instructionDescriptors[ADC_IIX] = new InstructionDescriptor()
                {
                    Instruction = ADC_IIX,
                    Action = I_ADC_IIX,
                    Cycles = 2,
                    Mnemonic = "ADC",
                    AddressingMode = AddressingModeEnum.IndirectIndexed,
                };

                instructionDescriptors[CMP_ZPG] = new InstructionDescriptor()
                {
                    Instruction = CMP_ZPG,
                    Action = I_CMP_ZPG,
                    Cycles = 3,
                    Mnemonic = "CMP",
                    AddressingMode = AddressingModeEnum.ZeroPage,
                };

                instructionDescriptors[TYA_IMP] = new InstructionDescriptor()
                {
                    Instruction = TYA_IMP,
                    Action = I_TYA_IMP,
                    Cycles = 2,
                    Mnemonic = "TYA",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[DEX_IMP] = new InstructionDescriptor()
                {
                    Instruction = DEX_IMP,
                    Action = I_DEX_IMP,
                    Cycles = 2,
                    Mnemonic = "DEX",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[DEY_IMP] = new InstructionDescriptor()
                {
                    Instruction = DEY_IMP,
                    Action = I_DEY_IMP,
                    Cycles = 2,
                    Mnemonic = "DEY",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[LDX_ABS] = new InstructionDescriptor()
                {
                    Instruction = LDX_ABS,
                    Action = I_LDX_ABS,
                    Cycles = 4,
                    Mnemonic = "LDX",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[TXA_IMP] = new InstructionDescriptor()
                {
                    Instruction = TXA_IMP,
                    Action = I_TXA_IMP,
                    Cycles = 2,
                    Mnemonic = "TXA",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[TAY_IMP] = new InstructionDescriptor()
                {
                    Instruction = TAY_IMP,
                    Action = I_TAY_IMP,
                    Cycles = 2,
                    Mnemonic = "TAY",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[TAX_IMP] = new InstructionDescriptor()
                {
                    Instruction = TAX_IMP,
                    Action = I_TAX_IMP,
                    Cycles = 2,
                    Mnemonic = "TAX",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[BCS_REL] = new InstructionDescriptor()
                {
                    Instruction = BCS_REL,
                    Action = I_BCS_REL,
                    Cycles = 0, // dynamic, depends on page boundary crossing, set in action
                    Mnemonic = "BCS",
                    AddressingMode = AddressingModeEnum.Relative,
                };

                instructionDescriptors[STX_ABS] = new InstructionDescriptor()
                {
                    Instruction = STX_ABS,
                    Action = I_STX_ABS,
                    Cycles = 4,
                    Mnemonic = "STX",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[ORA_ABS] = new InstructionDescriptor()
                {
                    Instruction = ORA_ABS,
                    Action = I_ORA_ABS,
                    Cycles = 4,
                    Mnemonic = "ORA",
                    AddressingMode = AddressingModeEnum.Absolute,
                };

                instructionDescriptors[PHP_IMP] = new InstructionDescriptor()
                {
                    Instruction = PHP_IMP,
                    Action = I_PHP_IMP,
                    Cycles = 3,
                    Mnemonic = "PHP",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[PLP_IMP] = new InstructionDescriptor()
                {
                    Instruction = PLP_IMP,
                    Action = I_PLP_IMP,
                    Cycles = 4,
                    Mnemonic = "PLP",
                    AddressingMode = AddressingModeEnum.Implied,
                };

                instructionDescriptors[EOR_IMM] = new InstructionDescriptor()
                {
                    Instruction = EOR_IMM,
                    Action = I_EOR_IMM,
                    Cycles = 2,
                    Mnemonic = "EOR",
                    AddressingMode = AddressingModeEnum.Immediate,
                };

                var inst_count = instructionDescriptors.Count(x => x != null);
                Console.WriteLine($"{inst_count}/151 ({(int) (inst_count / 151f * 100)}%) opcodes implemented.");
            }

            void I_ORA_ABS()
            {
                UInt16 addr = CPU.FetchAbsoluteAddress();
                
                CPU.A = (byte)(CPU.A | CPU.Read(addr));
                CPU.SetNegative((CPU.A & (1 << 7)) != 0);
                CPU.SetZero(CPU.A == 0);
            }

            void I_JMP_ABS()
            {
                CPU.PC = CPU.FetchAbsoluteAddress();
            }

            // TODO: Decide whether to make it behave like MOS6502 and INC only the address low
            void I_JMP_ABI()
            {
                UInt16 addr = CPU.FetchAbsoluteAddress();
                CPU.PC = (UInt16) ((CPU.Read(addr)) | (CPU.Read((UInt16)(addr + 1)) << 8));
            }

            void I_CLD_IMP()
            {
                CPU.ClearDecimal();
            }

            void I_LDX_IMM()
            {
                CPU.X = CPU.FetchOperand();
            }

            void I_TXS_IMP()
            {
                CPU.S = CPU.X;
            }

            void I_JSR_ABS()
            {
                CPU.PushAddress((UInt16) (CPU.PC + 1));
                CPU.PC = CPU.FetchAbsoluteAddress();
            }

            void I_BCC_REL()
            {
                instructionDescriptors[BCC_REL].Cycles = 2;

                var val = CPU.FetchOperand();
                if ((CPU.P & P_CARRY_MASK) != 0) return;

                var oldPCh = CPU.PC & 0xff00;
                CPU.PC = (UInt16) (CPU.PC + ((sbyte)val));
                var newPCh = CPU.PC & 0xff00;

                // Set cycles accordingly depending on the page boundary
                instructionDescriptors[BCC_REL].Cycles =
                    (oldPCh != newPCh) ? 4 : 3;
            }

            void I_BCS_REL()
            {
                instructionDescriptors[BCC_REL].Cycles = 2;

                var val = CPU.FetchOperand();
                if ((CPU.P & P_CARRY_MASK) != 1) return;

                var oldPCh = CPU.PC & 0xff00;
                CPU.PC = (UInt16)(CPU.PC + ((sbyte)val));
                var newPCh = CPU.PC & 0xff00;

                // Set cycles accordingly depending on the page boundary
                instructionDescriptors[BCC_REL].Cycles =
                    (oldPCh != newPCh) ? 4 : 3;
            }

            void I_BNE_REL()
            {
                instructionDescriptors[BNE_REL].Cycles = 2;

                var val = CPU.FetchOperand();

                if(CPU.GetZero()) return;

                instructionDescriptors[BNE_REL].Cycles += 1;

                var oldPCh = CPU.PC & 0xff00;
                CPU.PC = (UInt16)(CPU.PC + ((sbyte)val));
                var newPCh = CPU.PC & 0xff00;

                // Set cycles accordingly depending on the page boundary
                instructionDescriptors[BNE_REL].Cycles +=
                    (oldPCh != newPCh) ? 1 : 0;
            }

            void I_PHA_IMP()
            {
                CPU.Push(CPU.A);
            }

            void I_PLA_IMP()
            {
                CPU.A = CPU.Pull();
            }

            void I_INC_ABS()
            {
                UInt16 addr = CPU.FetchAbsoluteAddress();
                byte res = (byte)(CPU.Read(addr) + 1);
                CPU.Write(addr, res);

                // Flags
                CPU.SetNegative((res & (1 << 7)) != 0);
                CPU.SetZero(res == 0);
            }

            void I_RTI_IMP()
            {
                CPU.P = (byte) (CPU.Pull() & 0b11001111);
                CPU.PC = CPU.PullAddress();
                if (CPU.insideNMI)
                {
                    CPU.insideNMI = false;
                    CPU.nmiTriggered = false;
                    if (KAPE8bitEmulator.DebugMode && !CPU.HideNMIMessages)
                        Console.WriteLine($"Exiting NMI, returning to ${CPU.PC:X4}");
                }

                if (CPU.insideIRQ)
                {
                    CPU.insideIRQ = false;
                    if (KAPE8bitEmulator.DebugMode)
                        Console.WriteLine($"Exiting IRQ, returning to ${CPU.PC:X4}");
                }
            }

            void I_RTS_IMP()
            {
                CPU.PC = (UInt16) (CPU.PullAddress() + 1);
            }

            void I_CLI_IMP()
            {
                CPU.ClearIntDisable();
            }

            void I_STX_ZPG()
            {
                CPU.Write(CPU.FetchOperand(), CPU.X);
            }

            void I_STX_ABS()
            {
                CPU.Write(CPU.FetchAbsoluteAddress(), CPU.X);
            }

            void I_LDY_IMM()
            {
                CPU.Y = CPU.FetchOperand();
            }

            void I_SEC_IMP()
            {
                CPU.SetCarry();
            }

            void I_SBC_IMM()
            {
                var borrow = CPU.GetCarry() ? 0 : 1;
                int val = CPU.A - CPU.FetchOperand() - borrow;
                CPU.SetCarry(val >= 0);
                CPU.SetOverflow(val < -127 || val > 127);
                byte res = (byte) (sbyte) val;
                CPU.SetNegative((res & (1 << 7)) != 0);
                CPU.SetZero(res == 0);
                CPU.A = res;
            }

            void I_SBC_ABS()
            {
                var borrow = CPU.GetCarry() ? 0 : 1;
                int val = CPU.A - CPU.Read(CPU.FetchAbsoluteAddress()) - borrow;
                CPU.SetCarry(val >= 0);
                CPU.SetOverflow(val < -127 || val > 127);
                byte res = (byte)(sbyte)val;
                CPU.SetNegative((res & (1 << 7)) != 0);
                CPU.SetZero(res == 0);
                CPU.A = res;
            }

            void I_CLC_IMP()
            {
                CPU.ClearCarry();
            }

            void I_LDY_ABS()
            {
                CPU.Y = CPU.Read(CPU.FetchAbsoluteAddress());
            }

            void I_BEQ_REL()
            {
                instructionDescriptors[BEQ_REL].Cycles = 3;

                var rel = CPU.FetchOperand();
                if (!CPU.GetZero()) return;

                var oldPCh = CPU.PC & 0xff00;
                CPU.PC = (UInt16)(CPU.PC + ((sbyte)rel));
                var newPCh = CPU.PC & 0xff00;

                // Set cycles accordingly depending on the page boundary
                instructionDescriptors[BEQ_REL].Cycles =
                    (oldPCh != newPCh) ? 4 : 3;
            }

            void I_DEC_ABS()
            {
                var addr = CPU.FetchAbsoluteAddress();
                int val = CPU.Read(addr);
                val--;
                CPU.SetNegative(val < 0);
                CPU.SetZero(val == 0);
                CPU.Write(addr, (byte)val);
            }

            void I_INX_IMP()
            {
                CPU.X++;
                CPU.SetNegative((CPU.X & (1 << 7)) != 0);
                CPU.SetZero(CPU.X == 0);
            }

            void I_AND_IMM()
            {
                CPU.A = (byte)(CPU.A & CPU.FetchOperand());
                CPU.SetNegative((CPU.A & (1 << 7)) != 0);
                CPU.SetZero(CPU.A == 0);
            }

            void I_ORA_IMM()
            {
                CPU.A = (byte)(CPU.A | CPU.FetchOperand());
                CPU.SetNegative((CPU.A & (1 << 7)) != 0);
                CPU.SetZero(CPU.A == 0);
            }

            void I_ORA_ZPG()
            {
                CPU.A = (byte)(CPU.A | CPU.Read(CPU.FetchOperand()));
                CPU.SetNegative((CPU.A & (1 << 7)) != 0);
                CPU.SetZero(CPU.A == 0);
            }

            void I_ASL_ACC()
            {
                int b = CPU.A << 1;
                CPU.A = (byte)b;
                CPU.SetZero(CPU.A == 0);
                CPU.SetNegative((CPU.A & (1 << 7)) != 0);
                CPU.SetCarry((b & (1 << 8)) != 0);
            }

            void I_EOR_ZPG()
            {
                CPU.A = (byte)(CPU.A ^ CPU.Read(CPU.FetchOperand()));
                CPU.SetZero(CPU.A == 0);
                CPU.SetNegative((CPU.A & (1 << 7)) != 0);
            }

            void I_EOR_ABS()
            {
                CPU.A = (byte)(CPU.A ^ CPU.Read(CPU.FetchAbsoluteAddress()));
                CPU.SetZero(CPU.A == 0);
                CPU.SetNegative((CPU.A & (1 << 7)) != 0);
            }

            void I_EOR_IMM()
            {
                CPU.A = (byte)(CPU.A ^ CPU.FetchOperand());
                CPU.SetZero(CPU.A == 0);
                CPU.SetNegative((CPU.A & (1 << 7)) != 0);
            }

            void I_LSR_ACC()
            {
                CPU.SetCarry((CPU.A & 1) != 0);
                CPU.A = (byte)(CPU.A >> 1);
                CPU.ClearNegative();
                CPU.SetZero(CPU.A == 0);
            }

            void I_DEC_ZPG()
            {
                var addr = CPU.FetchOperand();
                int val = CPU.Read(addr);
                val--;
                CPU.SetNegative(val < 0);
                CPU.SetZero(val == 0);
                CPU.Write(addr, (byte)val);
            }

            void I_INY_IMP()
            {
                CPU.Y++;
                CPU.SetNegative(CPU.Y);
                CPU.SetZero(CPU.Y);
            }

            void I_INC_ZPG()
            {
                var addr = CPU.FetchOperand();
                byte val = CPU.Read(addr);
                val++;
                CPU.SetNegative(val);
                CPU.SetZero(val);
                CPU.Write(addr, val);
            }

            void I_TYA_IMP()
            {
                CPU.A = CPU.Y;
                CPU.SetZero(CPU.A);
                CPU.SetNegative(CPU.A);
            }

            void I_DEX_IMP()
            {
                CPU.X--;
                CPU.SetZero(CPU.X);
                CPU.SetNegative(CPU.X);
            }

            void I_LDX_ABS()
            {
                var addr = CPU.FetchAbsoluteAddress();
                CPU.X = CPU.Read(addr);
                CPU.SetZero(CPU.X);
                CPU.SetNegative(CPU.X);
            }

            void I_TXA_IMP()
            {
                CPU.A = CPU.X;
                CPU.SetZero(CPU.A);
                CPU.SetNegative(CPU.A);
            }

            void I_TAY_IMP()
            {
                CPU.Y = CPU.A;
                CPU.SetZero(CPU.Y);
                CPU.SetNegative(CPU.Y);
            }

            void I_TAX_IMP()
            {
                CPU.X = CPU.A;
                CPU.SetZero(CPU.X);
                CPU.SetNegative(CPU.X);
            }

            void I_DEY_IMP()
            {
                CPU.Y--;
                CPU.SetZero(CPU.Y);
                CPU.SetNegative(CPU.Y);
            }

            void I_BMI_REL()
            {
                instructionDescriptors[BMI_REL].Cycles = 2;

                var val = CPU.FetchOperand();

                if (!CPU.GetNegative()) return;

                instructionDescriptors[BMI_REL].Cycles += 1;

                var oldPCh = CPU.PC & 0xff00;
                CPU.PC = (UInt16)(CPU.PC + ((sbyte)val));
                var newPCh = CPU.PC & 0xff00;

                // Set cycles accordingly depending on the page boundary
                instructionDescriptors[BMI_REL].Cycles +=
                    (oldPCh != newPCh) ? 1 : 0;
            }

            void I_PHP_IMP()
            {
                CPU.Push(CPU.P);
            }

            void I_PLP_IMP()
            {
                CPU.P = CPU.Pull();
            }
        }
    }
}
