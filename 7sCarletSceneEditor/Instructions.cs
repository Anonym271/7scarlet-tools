

using System;
using System.Text;

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

        public event EventHandler ContentChanged;
        protected void OnContentChanged(EventArgs args) => ContentChanged?.Invoke(this, args);

        public Instruction(short opcode)
        {
            this.Opcode = opcode;
        }
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
                OnContentChanged(EventArgs.Empty);
            }
        }
        public override string ContentType => "Binary";

        public BinaryInstruction(short opcode, byte[] data) :
            base(opcode)
        {
            _data = data;
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
                OnContentChanged(EventArgs.Empty);
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
                OnContentChanged(EventArgs.Empty);
            }
        }

        public TextInstruction(short opcode, string text) :
            base(opcode)
        {
            _text = text;
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
    }
}
