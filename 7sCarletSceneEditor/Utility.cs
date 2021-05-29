using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _7sCarletSceneEditor
{
    public static class Utility
    {
        public static Encoding DefaultEncoding = Encoding.UTF8;

        public static string GetStringWithoutZeros(byte[] data)
        {
            return DefaultEncoding.GetString(data).TrimEnd('\0');
        }

        public static string GetStringWithoutZeros(byte[] data, int offset, int count)
        {
            return DefaultEncoding.GetString(data, offset, count).TrimEnd('\0');
        }
    }
}
