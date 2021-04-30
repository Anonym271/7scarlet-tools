using System;
using System.IO;

namespace _7sCarletSceneEditor
{
    public interface ITextRepresentable
    {
        string Text { get; set; }
    }

    public interface IBinaryRepresentable
    {
        byte[] Data { get; set; }
    }

    public abstract class Instruction
    {
        public short Opcode { get; }
        public virtual string Name => this.GetType().Name;
        public virtual string ContentType => "None";

        public event Action<Instruction> ContentChanged;
        protected void OnContentChanged() => ContentChanged?.Invoke(this);

        public Instruction(short opcode)
        {
            this.Opcode = opcode;
        }

        public abstract void Write(BinaryWriter file);
    }

    public class BinaryInstruction : Instruction, IBinaryRepresentable
    {
        private byte[] _data;
        public byte[] Data
        {
            get => _data;
            set
            {
                _data = value;
                OnContentChanged();
            }
        }
        public override string ContentType => "Binary";

        public BinaryInstruction(short opcode, byte[] data) :
            base(opcode)
        {
            _data = data;
        }

        public override void Write(BinaryWriter file)
        {
            file.Write((short)(_data.Length + 4));
            file.Write(Opcode);
            file.Write(_data);
        }
    }

    public class TextInstruction : Instruction, ITextRepresentable, IBinaryRepresentable
    {
        private string _text;
        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnContentChanged();
            }
        }
       // public override string DisplayName => base.DisplayName + " (Text)";
        public override string ContentType => "Text";
        public byte[] Data
        {
            get => Utility.DefaultEncoding.GetBytes(Text);
            set
            {
                Text = Utility.DefaultEncoding.GetString(value);
                OnContentChanged();
            }
        }

        public TextInstruction(short opcode, string text) :
            base(opcode)
        {
            _text = text;
        }

        public override void Write(BinaryWriter file)
        {
            byte[] data = Data; 
            file.Write((short)(data.Length + 4));
            file.Write(Opcode);
            file.Write(data);
        }
    }

    public class DialogTextInstruction : TextInstruction
    {
        public int ID { get; set; }
        public override string Name => "DialogText";
        public DialogTextInstruction(short opcode, int id, string text) :
            base(opcode, text)
        {
            ID = id;
        }

        public override void Write(BinaryWriter file)
        {
            byte[] data = Data;
            file.Write((short)(data.Length + 8));
            file.Write(Opcode);
            file.Write(ID);
            file.Write(data);
        }
    }
}
