using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;

namespace TbxEditor
{
    public static class TBX
    {
        public static TBG[] Read(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            using (BinaryReader file = new BinaryReader(fs))
            {
                byte[] magic = file.ReadBytes(4);
                if (Encoding.ASCII.GetString(magic) != "tbx\0")
                    new InvalidDataException("Invalid magic number!");
                uint dataOffset = file.ReadUInt32();
                int fileCount = file.ReadInt32();
                uint tableOffset = file.ReadUInt32();

                fs.Position = tableOffset;

                var table = new (uint, uint)[fileCount];
                for (int i = 0; i < fileCount; i++)
                {
                    table[i].Item1 = file.ReadUInt32();
                    table[i].Item2 = file.ReadUInt32();
                }

                var tbgs = new TBG[fileCount];
                for(int i = 0; i < fileCount; i++)
                {
                    fs.Position = table[i].Item1;
                    byte[] data = file.ReadBytes((int)table[i].Item2);
                    tbgs[i] = TBG.FromBuffer(data);
                }

                return tbgs;
            }
        }

        public static Bitmap[] ReadBitmaps(string filename)
        {
            var tbgs = Read(filename);
            var bmps = new Bitmap[tbgs.Length];
            for (int i = 0; i < tbgs.Length; i++)
                bmps[i] = tbgs[i].ToBitmap();
            return bmps;
        }

        public static void Write(string filename, TBG[] images)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            using (BinaryWriter file = new BinaryWriter(fs))
            {
                file.Write(Encoding.ASCII.GetBytes("tbx\0"));
                int dataOffset = 16 + 8 * images.Length; // data offset = headerSize + entrySize * entryCount
                file.Write(dataOffset); 
                file.Write(images.Length);
                file.Write(16); // table offset

                var table = new (uint, uint)[images.Length];
                fs.Position = dataOffset;
                for (int i = 0; i < images.Length; i++)
                {
                    var start = fs.Position;
                    table[i].Item1 = (uint)start;
                    images[i].Save(file);
                    table[i].Item2 = (uint)(fs.Position - start);
                }

                fs.Position = 16;
                foreach (var (pos, offs) in table)
                {
                    file.Write(pos);
                    file.Write(offs);
                }
            }
        }
        public static void Write(string filename, Bitmap[] images)
        {
            TBG[] tbgs = new TBG[images.Length];
            for (int i = 0; i < images.Length; i++)
                tbgs[i] = TBG.FromBitmap(images[i]);
            Write(filename, tbgs);
        }
    }
}
