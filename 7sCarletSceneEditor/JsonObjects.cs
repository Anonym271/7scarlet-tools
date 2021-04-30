using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace _7sCarletSceneEditor
{
    internal class JsonDialogInstruction
    {
        public int id { get; set; } = -1;
        public string[] lines { get; set; } = Array.Empty<string>();

        public JsonDialogInstruction() { }
        public JsonDialogInstruction(DialogTextInstruction inst)
        {
            id = inst.ID;
            lines = inst.Text.Trim('\0').Split(new string[] { "@@" }, StringSplitOptions.None);
        }

        public string GetText() => string.Join("@@", lines);
    }
}
