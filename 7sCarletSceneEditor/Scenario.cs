using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _7sCarletSceneEditor
{

    public class Scenario : IEnumerable<Instruction>
    {
        public List<Instruction> Instructions { get; set; }
        public Instruction this[int index] => Instructions[index];

        public Scenario()
        {
            Instructions = new List<Instruction>();
        }
        public Scenario(List<Instruction> instructions)
        {
            Instructions = instructions;
        }
        public Scenario(IEnumerable<Instruction> intructions)
        {
            this.Instructions = new List<Instruction>(intructions);
        }

        public static Scenario Load(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            using (BinaryReader file = new BinaryReader(fs))
            {
                List<Instruction> instructions = new List<Instruction>();

                while (file.PeekChar() > 0)
                {
                    short length = file.ReadInt16();
                    short opcode = file.ReadInt16();
                    byte[] data = file.ReadBytes(length - 4);

                    Instruction inst = null;
                    switch (opcode)
                    {
                        case 0x0010:
                            inst = new DialogTextInstruction(
                                opcode,
                                BitConverter.ToInt32(data, 0),
                                Utility.DefaultEncoding.GetString(data, 4, data.Length - 4));
                            break;
                        default:
                            inst = new BinaryInstruction(opcode, data);
                            break;
                    }
                    instructions.Add(inst);
                }

                return new Scenario(instructions);
            }
        }

        public IEnumerator<Instruction> GetEnumerator()
        {
            return Instructions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Instructions.GetEnumerator();
        }
    }
}
