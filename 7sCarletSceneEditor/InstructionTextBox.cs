using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace _7sCarletSceneEditor
{
    public class InstructionTextBox : TextBox
    {
        private bool _ignoreNextChange = false;
        public InstructionDataSource DataSource { get; }

        public InstructionTextBox(InstructionDataSource dataSource)
        {
            Multiline = true;

            Text = dataSource.Text ?? string.Empty;
            this.DataSource = dataSource;
            dataSource.TextChanged += text =>
            {
                if (_ignoreNextChange)
                    _ignoreNextChange = false;
                else this.Text = text;
            };
            this.TextChanged += (a, b) =>
            {
                _ignoreNextChange = true;
                DataSource.Text = Text;
            };
        }

    }
}
