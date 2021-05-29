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

                while (file.BaseStream.Position < file.BaseStream.Length)
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
                                Utility.GetStringWithoutZeros(data, 4, data.Length - 4));
                            break;
                        case 0x0018:
                            inst = new SpeakerNameInstruction(
                                opcode,
                                BitConverter.ToInt32(data, 0),
                                Utility.GetStringWithoutZeros(data, 4, data.Length - 4));
                            break;
                        case 0x0024:
                            inst = new VoiceFileInstruction(
                                opcode,
                                BitConverter.ToInt32(data, 0),
                                Utility.GetStringWithoutZeros(data, 4, data.Length - 4));
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

        public void Save(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            using (BinaryWriter file = new BinaryWriter(fs))
            {
                foreach (Instruction i in Instructions)
                    i.Write(file);
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

        public string GetSpeaker(int dialogInstructionIndex)
        {
            for (int i = dialogInstructionIndex - 1; i > 0; i--)
            {
                var inst = Instructions[i];
                if (inst is SpeakerNameInstruction speaker)
                    return speaker.Text;
            }
            return null;
        }

        public string GetVoiceFile(int dialogInstructionIndex)
        {
            for (int i = dialogInstructionIndex - 1; i > 0; i--)
            {
                var inst = Instructions[i];
                if (inst is VoiceFileInstruction voice)
                    return voice.Text;
                else if (inst is DialogTextInstruction dialog)
                    return null; // only one text per voice file
            }
            return null;
        }
    }
}
