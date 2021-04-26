using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TbxEditor
{
    public enum TBGPixelFormat
    {
        BGR,
        BGRA
    }

    public static class TBGPixelFormats
    {
        public static int GetSize(TBGPixelFormat f)
        {
            switch (f)
            {
                case TBGPixelFormat.BGR:
                    return 3;
                case TBGPixelFormat.BGRA:
                    return 4;
            }
            throw new ArgumentException();
        }

        public static byte GetBinaryRepresentation(TBGPixelFormat f)
        {
            switch (f)
            {
                case TBGPixelFormat.BGR:
                    return 0x98;
                case TBGPixelFormat.BGRA:
                    return 0x0C;
            }
            throw new ArgumentException();
        }
        public static TBGPixelFormat FromBinaryRepresentation(byte b)
        {
            switch (b)
            {
                case 0x98:
                    return TBGPixelFormat.BGR;
                case 0x0C:
                    return TBGPixelFormat.BGRA;
            }
            throw new ArgumentException("Unknown TBG color type!");
        }
    }

    public class TBG
    {
        private const int HEADER_SIZE = 828;

        private byte[] _data;

        public byte[] Data 
        { 
            get { return _data; } 
            set
            {
                int pixelSize = TBGPixelFormats.GetSize(PixelFormat);
                if (value.Length != pixelSize * this.Size.Width * this.Size.Height)
                    throw new ArgumentException("Pixel data does not match size and format!");
                _data = value;
            }
        }

        public TBGPixelFormat PixelFormat { get; }
        
        public Size Size { get; }
        public float ResX { get; set; } = 1.0f;
        public float ResY { get; set; } = 1.0f;

        public int ImageCount { get; } = 1;

        public TBG(Size size, TBGPixelFormat format, byte[] data)
        {
            this.Size = size;
            this.PixelFormat = format;
            this.Data = data;
        }
        public TBG(Size size, TBGPixelFormat format)
        {
            this.Size = size;
            this.PixelFormat = format;
            int pixelSize = TBGPixelFormats.GetSize(format);
            this._data = new byte[size.Width * size.Height * pixelSize];
        }

        public static TBG FromFile(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            using (BinaryReader file = new BinaryReader(fs))
            {
                byte[] magic = file.ReadBytes(4);
                if (Encoding.ASCII.GetString(magic) != "tbg\0")
                    throw new InvalidDataException("Invalid magic number!");
                uint dataOffset = file.ReadUInt32();
                int dataLength = file.ReadInt32();
                int width = file.ReadInt32();
                int height = file.ReadInt32();
                byte[] flags = file.ReadBytes(4);
                int imageCount = file.ReadInt32();
                long unknown = file.ReadInt64();
                float resX = file.ReadSingle();
                float resY = file.ReadSingle();

                var format = TBGPixelFormats.FromBinaryRepresentation(flags[3]);

                file.BaseStream.Position = dataOffset;
                byte[] data = file.ReadBytes(dataLength);
                var tbg = new TBG(new Size(width, height), format, data)
                {
                    ResX = resX,
                    ResY = resY
                };
                return tbg;
            }
        }

        public static TBG FromBuffer(byte[] fileData)
        {
            if (Encoding.ASCII.GetString(fileData, 0, 4) != "tbg\0")
                throw new InvalidDataException("Invalid magic number!");
            uint dataOffset = BitConverter.ToUInt32(fileData, 0x04);
            int dataLength = BitConverter.ToInt32(fileData, 0x08);
            int width = BitConverter.ToInt32(fileData, 0x0C);
            int height = BitConverter.ToInt32(fileData, 0x10);
            byte colorByte = fileData[0x14 + 3];
            float resX = BitConverter.ToSingle(fileData, 0x24);
            float resY = BitConverter.ToSingle(fileData, 0x28);
            var format = TBGPixelFormats.FromBinaryRepresentation(colorByte);
            var data = new byte[dataLength];
            Array.Copy(fileData, dataOffset, data, 0, dataLength);

            var tbg = new TBG(new Size(width, height), format, data)
            {
                ResX = resX,
                ResY = resY
            };
            return tbg;
        }

        public static TBG FromBitmap(Bitmap bmp)
        {
            var tbgFormat = TBGPixelFormat.BGRA;
            var bmpFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
            if (bmp.PixelFormat == System.Drawing.Imaging.PixelFormat.Format24bppRgb)
            {
                tbgFormat = TBGPixelFormat.BGR;
                bmpFormat = System.Drawing.Imaging.PixelFormat.Format32bppRgb;
            }
            int pixelSize = TBGPixelFormats.GetSize(tbgFormat);

            var data = new byte[bmp.Width * bmp.Height * pixelSize];
            var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmpFormat);
            Marshal.Copy(bmpData.Scan0, data, 0, data.Length);
            bmp.UnlockBits(bmpData);

            return new TBG(bmp.Size, tbgFormat, data);
        }

        public Bitmap ToBitmap()
        {
            switch (this.PixelFormat)
            {
                case TBGPixelFormat.BGR:
                    return GetBitmapBGR(Data, Size);
                case TBGPixelFormat.BGRA:
                    return GetBitmapBGRA(Data, Size);
            }
            return null;
        }

        private static Bitmap GetBitmapBGR(byte[] data, Size size)
        {
            Bitmap bmp = new Bitmap(size.Width, size.Height);
            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, size.Width, size.Height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            Marshal.Copy(data, 0, bmpData.Scan0, data.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        private static Bitmap GetBitmapBGRA(byte[] data, Size size)
        {
            Bitmap bmp = new Bitmap(size.Width, size.Height);
            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, size.Width, size.Height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Marshal.Copy(data, 0, bmpData.Scan0, data.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        /// <summary>
        /// Writes the TBG file to a stream using the provided BinaryWriter. 
        /// See https://github.com/Anonym271/7scarlet-tools/wiki/TBG-Images-%28%2A.tbg%29 for details.
        /// </summary>
        public void Save(BinaryWriter file)
        {
            var startPos = file.BaseStream.Position;
            file.Write(Encoding.ASCII.GetBytes("tbg\0"));
            file.Write(HEADER_SIZE);
            file.Write(_data.Length);
            file.Write(Size.Width);
            file.Write(Size.Height);
            file.Write(new byte[] { 0x00, 0x10, 0x00 });
            file.Write(TBGPixelFormats.GetBinaryRepresentation(PixelFormat));
            file.Write(1);
            file.Write((long)0);
            file.Write(ResX);
            file.Write(ResY);

            file.BaseStream.Position = startPos + HEADER_SIZE;
            file.Write(_data);
        }
        public void Save(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            using (BinaryWriter file = new BinaryWriter(fs))
                Save(file);
        }
        public byte[] SaveToBuffer()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter file = new BinaryWriter(ms))
            {
                Save(file);
                return ms.ToArray();
            }
        }
    }
}
