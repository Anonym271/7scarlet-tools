using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Be.Windows.Forms;

namespace _7sCarletSceneEditor
{
    public partial class MainWindow : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, Int32 wMsg, bool wParam, Int32 lParam);
        private const int WM_SETREDRAW = 11;
        public static void SuspendDrawing(Control parent)
        {
            SendMessage(parent.Handle, WM_SETREDRAW, false, 0);
        }
        public static void ResumeDrawing(Control parent)
        {
            SendMessage(parent.Handle, WM_SETREDRAW, true, 0);
            parent.Refresh();
        }

        private bool _skipSelectionChanged = false;

        private Control ActualActiveControl => ActiveControl == splitContainer ? splitContainer.ActiveControl : ActiveControl;
        private Scenario CurrentScenario = null;

        private Instruction CurrentInstruction = null;
        private InstructionDataSource DataSource = new InstructionDataSource();

        private InstructionTextBox textBox;

        public MainWindow()
        {
            InitializeComponent();

            textBox = new InstructionTextBox(DataSource)
            {
                Dock = DockStyle.Fill
            };
            tabViewString.Controls.Add(textBox);

            hexBox.ByteProvider = new InstructionByteProvider(DataSource);
        }


        bool CheckUnsavedChanges()
        {
            // TODO: implement
            return true;
        }

        void LoadScene(string filename)
        {
            try
            {
                CurrentScenario = Scenario.Load(filename);

                int count = CurrentScenario.Instructions.Count;
                ListViewItem[] items = new ListViewItem[count];
                for (int i = 0; i < count; i++)
                    items[i] = new InstructionListViewItem(CurrentScenario[i]);
                listView.BeginUpdate();
                listView.Items.Clear();
                listView.Items.AddRange(items);
                listView.EndUpdate();
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred while trying to load this script:\n\n" + exc.Message,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Instruction_ContentChanged(object sender, EventArgs e)
        {
            if (sender is ITextRepresentable instT)
                textBox.Text = instT.Text;
        }

        private void SetTabsWithoutFocus(IEnumerable<TabPage> tabs)
        {
            _skipSelectionChanged = true;
            var prevFocus = ActualActiveControl;
            if (prevFocus != null)
            {
                tabView.TabPages.Clear();
                foreach (var tab in tabs)
                    tabView.TabPages.Add(tab);
                prevFocus.Focus();
            }
            _skipSelectionChanged = false;
        }

        #region EventHandlers
        private void btnFileOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "7'sCarlet Scenario Files (*.scn)|*.scn*|All Files (*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK)
                LoadScene(ofd.FileName);
        }


        private void listView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if ( _skipSelectionChanged || listView.SelectedIndices.Count == 0)
                return;

            if (listView.Items[listView.SelectedIndices[0]] is InstructionListViewItem item)
            {
                CurrentInstruction = item.Instruction;
                DataSource.Instruction = item.Instruction;
                tabView.Enabled = false;
                try
                {
                    tabView.TabPages.Clear();
                    if (item.Instruction is TextInstruction inst)
                    {
                        tabView.TabPages.Add(tabViewString);
                    }
                    if (item.Instruction is IBinaryRepresentable instB)
                    {
                      //  var bp = new InstructionByteProvider(DataSource);
                       // hexBox.ByteProvider = bp;
                        tabView.TabPages.Add(tabViewHex);
                    }
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                tabView.Enabled = true;
            }
        }

        private void ByteProvider_Changed(object sender, EventArgs e)
        {
            if (sender is IByteProvider bp)
            {
                bp.ApplyChanges();
                if (CurrentInstruction is ITextRepresentable itr)
                    textBox.Text = itr.Text;
            }
        }
        #endregion
    }
}
