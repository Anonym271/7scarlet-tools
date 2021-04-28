using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace _7sCarletSceneEditor
{
    public class InstructionListViewItem : ListViewItem
    {
        public Instruction Instruction { get; }
        public InstructionListViewItem(Instruction instruction)
        {
            Instruction = instruction;
            this.Text = $"0x{instruction.Opcode:X4}";
            SubItems.Add(instruction.Name);
            SubItems.Add(instruction.ContentType);
        }
    }
}
