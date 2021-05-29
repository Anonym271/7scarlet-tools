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
        public string speaker { get; set; } = null;
        public string voiceFile { get; set; } = null;
        public string[] lines { get; set; } = Array.Empty<string>();

        public JsonDialogInstruction() { }
        public JsonDialogInstruction(DialogTextInstruction inst, string speaker = null, string voice = null)
        {
            id = inst.ID;
            lines = inst.Text.Trim('\0').Split(new string[] { "@@" }, StringSplitOptions.None);
            this.speaker = string.IsNullOrEmpty(speaker) ? null : speaker;
            voiceFile = string.IsNullOrEmpty(voice) ? null : voice;
        }

        public string GetText() => string.Join("@@", lines);
    }
}
