using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Be.Windows.Forms;

namespace _7sCarletSceneEditor
{
    public class InstructionByteProvider : IByteProvider
    {
        private bool _ignoreNextChange = false;

        public List<byte> Bytes { get; set; }
        private InstructionDataSource DataSource { get; }
        public IBinaryRepresentable Instruction { get; }
        public long Length => Bytes.Count;

        public event EventHandler LengthChanged;
        public event EventHandler Changed;

        public InstructionByteProvider(IBinaryRepresentable instruction)
        {
            Instruction = instruction;
            Bytes = new List<byte>(instruction.Data);
        }

        public InstructionByteProvider(InstructionDataSource dataSource)
        {
            var data = dataSource.Data;
            this.DataSource = dataSource;
            if (data == null)
                this.Bytes = new List<byte>();
            else this.Bytes = new List<byte>(data);
            dataSource.DataChanged += val =>
            {
                if (_ignoreNextChange)
                    _ignoreNextChange = false;
                else this.Bytes = new List<byte>(val);
            };
            this.Changed += (a, b) => ApplyChanges();
        }

        public void ApplyChanges()
        {
            _ignoreNextChange = true;
            DataSource.Data = Bytes.ToArray();
            //Instruction.Data = Bytes.ToArray();
        }

        public void DeleteBytes(long index, long length)
        {
            Bytes.RemoveRange((int)index, (int)length);
            LengthChanged?.Invoke(this, new EventArgs());
            Changed?.Invoke(this, new EventArgs());
        }

        public bool HasChanges()
        {
            // TODO: does this get called often? If not, equality check with loop will do
            throw new NotImplementedException();
        }

        public void InsertBytes(long index, byte[] bs)
        {
            Bytes.InsertRange((int)index, bs);
            LengthChanged?.Invoke(this, new EventArgs());
            Changed?.Invoke(this, new EventArgs());
        }

        public byte ReadByte(long index)
        {
            return Bytes[(int)index];
        }

        public void WriteByte(long index, byte value)
        {
            Bytes[(int)index] = value;
            Changed?.Invoke(this, new EventArgs());
        }

        public bool SupportsDeleteBytes()
        {
            return true;
        }

        public bool SupportsInsertBytes()
        {
            return true;
        }

        public bool SupportsWriteByte()
        {
            return true;
        }

    }
}
