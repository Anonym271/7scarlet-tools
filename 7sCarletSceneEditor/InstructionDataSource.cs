using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _7sCarletSceneEditor
{
    public class InstructionDataSource
    {
        private Instruction _instruction;

        public string Text
        {
            get
            {
                if (_instruction is ITextRepresentable tr)
                    return tr.Text;
                return null;
            }
            set
            {
                if (_instruction is ITextRepresentable tr)
                    tr.Text = value;
                else throw new ArgumentException("Current instruction does not support text data!");
            }
        }

        public byte[] Data
        {
            get
            {
                if (_instruction is IBinaryRepresentable br)
                    return br.Data;
                return null;
            }
            set
            {
                if (_instruction is IBinaryRepresentable br)
                    br.Data = value;
                else throw new ArgumentException("Current instruction does not support binary data!");
            }
        }

        public Instruction Instruction 
        {
            get => _instruction;
            set
            {
                if (_instruction != null)
                    _instruction.ContentChanged -= OnInstructionChanged;
                value.ContentChanged += OnInstructionChanged;
                _instruction = value;
                OnInstructionChanged(value);
            }
        }

        public event Action<string> TextChanged;
        public event Action<byte[]> DataChanged;

        private void OnInstructionChanged(Instruction sender)
        {
            if (_instruction is ITextRepresentable tr)
                TextChanged?.Invoke(tr.Text);
            if (_instruction is IBinaryRepresentable br)
                DataChanged?.Invoke(br.Data);
        }
    }
}
