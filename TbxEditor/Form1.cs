using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TbxEditor
{
    public partial class Form1 : Form
    {
        private const int THUMBNAIL_SIZE = 64;

        private string tempDir;

        private List<Bitmap> images = new List<Bitmap>();

        private string _currentFileName;
        private string CurrentFileName
        {
            get => _currentFileName;
            set
            {
                _currentFileName = value;
                this.Text = "TbxEditor - " + value;
            }
        }

        private static ImageList CreateImageList() =>
            new ImageList()
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(THUMBNAIL_SIZE, THUMBNAIL_SIZE)
            };

        private static Bitmap GetThumbnail(Bitmap bmp, int size)
        {
            Rectangle target = new Rectangle();
            if (bmp.Width > bmp.Height)
            {
                target.X = 0;
                target.Width = size;
                float ratio = (float)size / (float)bmp.Width;
                float h = bmp.Height * ratio;
                target.Y = (size / 2) - (int)(h / 2);
                target.Height = (int)h;
            }
            else
            {
                target.Y = 0;
                target.Height = size;
                float ratio = (float)size / (float)bmp.Height;
                float w = bmp.Width * ratio;
                target.X = (size / 2) - (int)(w / 2);
                target.Width = (int)w;
            }
            Bitmap res = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics gfx = Graphics.FromImage(res))
                gfx.DrawImage(bmp, target);
            return res;
        }

        public Form1(string[] args)
        {
            InitializeComponent();
            listView.LargeImageList = CreateImageList();

            tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            if (args?.Length > 0)
            {
                LoadFiles(args);
                UpdateImages();
            }
        }

        private void RefreshImages()
        {
            listView.Items.Clear();
            ImageList imgs = CreateImageList();
            for (int i = 0; i < images.Count; i++)
            {
                listView.Items.Add((i + 1).ToString(), i);
                imgs.Images.Add(GetThumbnail(images[i], THUMBNAIL_SIZE));
            }
            listView.LargeImageList = imgs;
        }

        private void UpdateImages()
        {
            while (listView.Items.Count > images.Count)
                listView.Items.RemoveAt(listView.Items.Count - 1);
            for (int i = listView.Items.Count; i < images.Count; i++)
                listView.Items.Add((i + 1).ToString(), i);

            listView.LargeImageList = CreateImageList();
            for (int i = 0; i < images.Count; i++)
                listView.LargeImageList.Images.Add(GetThumbnail(images[i], THUMBNAIL_SIZE));
        }

        public void LoadFiles(IEnumerable<string> filenames)
        {
            List<string> failed = new List<string>();
            foreach (var fn in filenames)
            {
                Bitmap bmp = null;
                try
                {
                    bmp = new Bitmap(fn);
                }
                catch (Exception)
                {
                    try
                    {
                        bmp = TBG.FromFile(fn).ToBitmap();
                    }
                    catch (Exception)
                    {
                        try
                        {
                            var bmps = TBX.ReadBitmaps(fn);
                            images.AddRange(bmps);
                            CurrentFileName = System.IO.Path.GetFileNameWithoutExtension(fn);
                            continue;
                        }
                        catch (Exception)
                        {
                            failed.Add(fn);
                            continue;
                        }
                    }
                }
                images.Add(bmp);
            }
            if (failed.Count > 0)
            {
                MessageBox.Show(
                    "Could not load the following files as images:\n\n" + string.Join("\n", failed),
                    "Error loading images!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        void RemoveSelected()
        {
            if (listView.SelectedItems.Count > 0)
            {
                List<Bitmap> tmp = new List<Bitmap>();
                for (int i = 0; i < images.Count; i++)
                {
                    if (!listView.SelectedIndices.Contains(i))
                        tmp.Add(images[i]);
                }
                images = tmp;
                listView.SelectedIndices.Clear();
                UpdateImages();
            }
        }



        private void btnOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "TBX Image Archvies (*.tbx)|*.tbx|All Files (*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var tbgs = TBX.Read(ofd.FileName);
                images = new List<Bitmap>();
                foreach (var tbg in tbgs)
                    images.Add(tbg.ToBitmap());
                CurrentFileName = System.IO.Path.GetFileNameWithoutExtension(ofd.FileName);
                RefreshImages();
            }
        }

        private void listView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void listView_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    List<string> filesToLoad = new List<string>();
                    foreach (string f in files)
                    {
                        // only add new files (don't allow self-drop)
                        if (!Path.Equals(Path.GetDirectoryName(f), tempDir)) 
                            filesToLoad.Add(f);
                    }
                    LoadFiles(filesToLoad);
                    UpdateImages();
                }
            }
        }

        private void listView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count > 0)
                btnRemove.Enabled = true;
            else btnRemove.Enabled = false;
            if (listView.SelectedIndices.Count == 1)
            {
                int i = listView.SelectedIndices[0];
                if (images.Count >= i)
                    pictureBox1.Image = images[i];
                btnUp.Enabled = true;
                btnDown.Enabled = true;
            }
            else
            {
                btnUp.Enabled = false;
                btnDown.Enabled = false;
            }
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            this.listView.Items.Clear();
            this.pictureBox1.Image = null;
            this.images.Clear();
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Images (*.png, *.jpg, *.jpeg, *.bmp, *.tbp, *.tbx)|*.png;*.jpg;*.jpeg;*.bmp;*.tbp;*.tbx|All Files (*.*)|*.*";
            ofd.Multiselect = true;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                LoadFiles(ofd.FileNames);
                UpdateImages();
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            if (listView.Items.Count == 0)
            {
                MessageBox.Show("You should load some images before exporting them...",
                    "What?", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                string prefix = CurrentFileName ?? "image";
                if (listView.SelectedIndices.Count == 1)
                {
                    Bitmap bmp = images[listView.SelectedIndices[0]];
                    SaveFileDialog sfd = new SaveFileDialog();
                    sfd.DefaultExt = ".png";
                    sfd.Filter = "PNG File (*.png)|*.png";
                    sfd.FileName = $"{prefix}_{listView.SelectedIndices[0]}.png";
                    if (sfd.ShowDialog() == DialogResult.OK)
                        bmp.Save(sfd.FileName);
                }
                else
                {
                    FolderBrowserDialog fbd = new FolderBrowserDialog();
                    fbd.Description = "Please select a place where I should extract the images to.";
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        if (listView.SelectedIndices.Count == 0) // none selected, export all
                        {
                            for (int i = 0; i < images.Count; i++)
                                images[i].Save(Path.Combine(fbd.SelectedPath, $"{prefix}_{i}.png"));
                        }
                        else
                        {
                            foreach (int i in listView.SelectedIndices)
                                images[i].Save(Path.Combine(fbd.SelectedPath, $"{prefix}_{i}.png"));
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show($"Oops... Something went wrong, please let me know. Error message:\n\n{exc.Message}\n\n{exc.StackTrace}",
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            RemoveSelected();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
                RemoveSelected();
        }

        private void listView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            List<string> filenames = new List<string>();
            string prefix = CurrentFileName ?? "image";
            foreach (int i in listView.SelectedIndices)
            {
                string fn = Path.Combine(tempDir, $"{prefix}_{i}.png");
                images[i].Save(fn);
                filenames.Add(fn);
            }

            DoDragDrop(new DataObject(DataFormats.FileDrop, filenames.ToArray()), DragDropEffects.Copy);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch (Exception) { }
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count == 1)
            {
                int i = listView.SelectedIndices[0];
                int j = i - 1;
                if (j >= 0)
                {
                    var tmp = images[j];
                    images[j] = images[i];
                    images[i] = tmp;
                    listView.SelectedIndices.Clear();
                    listView.SelectedIndices.Add(j);
                    UpdateImages();
                }
            }
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count == 1)
            {
                int i = listView.SelectedIndices[0];
                int j = i + 1;
                if (j < listView.Items.Count)
                {
                    var tmp = images[j];
                    images[j] = images[i];
                    images[i] = tmp;
                    listView.SelectedIndices.Clear();
                    listView.SelectedIndices.Add(j);
                    UpdateImages();
                }
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = CurrentFileName == null ? "images.tbx" : CurrentFileName + ".tbx";
            sfd.Filter = "TBX File Archive (*.tbx)|*.tbx";
            sfd.DefaultExt = ".tbx";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    TBX.Write(sfd.FileName, images.ToArray());
                }
                catch (Exception exc)
                {
                    MessageBox.Show($"Oops... Something went wrong, please let me know. Error message:\n\n{exc.Message}\n\n{exc.StackTrace}",
                        "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }   
            }
        }
    }
}