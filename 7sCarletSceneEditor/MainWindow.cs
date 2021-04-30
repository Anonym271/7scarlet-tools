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
using System.Xml;
using Newtonsoft.Json;
using System.IO;

namespace _7sCarletSceneEditor
{
    public partial class MainWindow : Form
    {
        #region DllImport
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
        #endregion

        private bool _skipSelectionChanged = false;
        private bool _unsavedChanges = false;
        private bool _hideUnknownInstructions = false;

        private Control ActualActiveControl => ActiveControl == splitContainer ? splitContainer.ActiveControl : ActiveControl;

        private string _currentFileName = string.Empty;
        private string CurrentFileName
        {
            get => _currentFileName;
            set
            {
                _currentFileName = value;
                this.Text = "SceneEditor - " + value;
            }
        }
        private Scenario CurrentScenario = null;

        private Instruction CurrentInstruction = null;
        private InstructionDataSource DataSource = new InstructionDataSource();

        private InstructionTextBox textBox;

        public MainWindow()
        {
            InitializeComponent();

            textBox = new InstructionTextBox(DataSource) { Dock = DockStyle.Fill };
            tabViewString.Controls.Add(textBox);

            hexBox.ByteProvider = new InstructionByteProvider(DataSource);
        }


        bool CheckUnsavedChanges()
        {
            if (_unsavedChanges)
            {
                var res = MessageBox.Show("Do you want to save your changes?",
                    "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (res == DialogResult.Cancel)
                    return false;
                else if (res == DialogResult.Yes)
                    Save();
            }
            return true;
        }

        private void ExportDialogs(string filename)
        {
            if (CurrentScenario == null)
                throw new ArgumentNullException("No scenario is loaded!");
            List<JsonDialogInstruction> objects = new List<JsonDialogInstruction>();
            foreach (var inst in CurrentScenario)
            {
                if (inst is DialogTextInstruction d)
                    objects.Add(new JsonDialogInstruction(d));
            }
            File.WriteAllText(filename, JsonConvert.SerializeObject(objects.ToArray(), Newtonsoft.Json.Formatting.Indented));
        }

        private void ImportDialogs(string filename)
        {
            if (CurrentScenario == null)
                throw new ArgumentNullException("No scenario is loaded!");
            // create id dictionary
            var textInstructions = new Dictionary<int, DialogTextInstruction>();
            foreach (var inst in CurrentScenario)
            {
                if (inst is DialogTextInstruction d)
                    textInstructions.Add(d.ID, d);
            }
            var objects = JsonConvert.DeserializeObject<JsonDialogInstruction[]>(
                File.ReadAllText(filename));
            var unknowns = new List<int>();
            foreach (var obj in objects)
            {
                if (textInstructions.TryGetValue(obj.id, out var inst))
                    inst.Text = obj.GetText();
                else unknowns.Add(obj.id);
            }
            // report unknown ids
            if (unknowns.Count > 0)
            {
                StringBuilder s = new StringBuilder();
                s.Append($"{unknowns.Count} unknown IDs found in the imported file:\n\n{unknowns[0]}");
                for (int i = 1; i < 10 && i < unknowns.Count; i++)
                    s.Append('\n').Append(unknowns[i]);
                if (unknowns.Count > 10)
                    s.Append($"... and {unknowns.Count - 10} more.");
                MessageBox.Show(s.ToString(), "Unknown IDs found",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void LoadScene(string filename)
        {
            try
            {
                CurrentScenario = Scenario.Load(filename);
                CurrentFileName = filename;
                foreach (var i in CurrentScenario)
                   i.ContentChanged += o => _unsavedChanges = true;
                ReloadListView();
                _unsavedChanges = false;
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred while trying to load this script:\n\n" + exc.Message,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void ReloadListView()
        {
            List<InstructionListViewItem> items = new List<InstructionListViewItem>();
            if (_hideUnknownInstructions)
            {
                foreach (var i in CurrentScenario)
                    if (i is DialogTextInstruction)
                        items.Add(new InstructionListViewItem(i));
            }
            else
            {
                foreach (var i in CurrentScenario)
                    items.Add(new InstructionListViewItem(i));
            }
            listView.BeginUpdate();
            listView.Items.Clear();
            listView.Items.AddRange(items.ToArray());
            listView.EndUpdate();
        }

        bool Save() => Save(CurrentFileName);
        bool Save(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return false;
            try
            {
                CurrentScenario.Save(filename);
                CurrentFileName = filename;
                _unsavedChanges = false;
                return true;
            }
            catch (Exception exc)
            {
                MessageBox.Show($"An error occurred while trying to save the scenario to \"{filename}\":\n\n{exc.Message}",
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        void SaveAs()
        {
            if (CurrentScenario == null)
                return;
            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "7'sCarlet Scenario Files (*.scn)|*.scn*|All Files (*.*)|*.*",
                DefaultExt = ".scn"
            };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                if (Save(sfd.FileName))
                    CurrentFileName = sfd.FileName;
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
            if (!CheckUnsavedChanges())
                return;
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
                        tabView.TabPages.Add(tabViewString);
                    if (item.Instruction is IBinaryRepresentable instB)
                        tabView.TabPages.Add(tabViewHex);
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

        private void btnFileSave_Click(object sender, EventArgs e) => Save(); 
        private void btnFileSaveAs_Click(object sender, EventArgs e) => SaveAs();


        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Modifiers == Keys.Control)
            {
                if (e.KeyCode == Keys.S)
                {
                    Save();
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.Modifiers == (Keys.Control | Keys.Shift))
            {
                if (e.KeyCode == Keys.S)
                {
                    SaveAs();
                    e.SuppressKeyPress = true;
                }
            }
        }

        private void btnScriptExport_Click(object sender, EventArgs e)
        {
            if (CurrentScenario == null)
            {
                MessageBox.Show("Please open a scenario file first.", "No scenario found",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
            sfd.DefaultExt = ".json";
            sfd.FileName = System.IO.Path.GetFileNameWithoutExtension(CurrentFileName) + ".json";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ExportDialogs(sfd.FileName);
                }
                catch (Exception exc)
                {
                    MessageBox.Show("Failed to export dialogs:\n\n" + exc.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnScriptImport_Click(object sender, EventArgs e)
        {
            if (CurrentScenario == null)
            {
                MessageBox.Show("Please open a scenario file first.", "No scenario found",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "XML Files (*.json)|*.json|All Files (*.*)|*.*";
            ofd.DefaultExt = ".json";
            ofd.FileName = System.IO.Path.GetFileNameWithoutExtension(CurrentFileName) + ".json";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ImportDialogs(ofd.FileName);
                }
                catch (Exception exc)
                {
                    MessageBox.Show("Failed to import dialogs:\n\n" + exc.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!CheckUnsavedChanges())
                e.Cancel = true;
        }

        private void hideUnknownInstructionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _hideUnknownInstructions = !_hideUnknownInstructions;
            hideUnknownInstructionsToolStripMenuItem.Checked = _hideUnknownInstructions;
            ReloadListView();
        }
        #endregion
    }
}
