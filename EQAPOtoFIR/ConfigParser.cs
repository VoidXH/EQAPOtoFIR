using Cavern.Format;
using Cavern.QuickEQ;
using System.Collections.Generic;
using System.IO;

namespace EQAPOtoFIR {
    public class ConfigParser {
        readonly EqualizedChannel[] result;

        public ConfigParser(string path) {
            string[] contents = File.ReadAllLines(path);
            string[] active = new string[] { "ALL" };
            Dictionary<string, EqualizedChannel> channels = new Dictionary<string, EqualizedChannel>();
            for (int line = 0; line < contents.Length; ++line) {
                if (string.IsNullOrWhiteSpace(contents[line]))
                    continue;
                if (contents[line].StartsWith("Channel:"))
                    active = contents[line].Substring(contents[line].IndexOf(' ') + 1).Trim().ToUpper().Split(' ');
                else {
                    Equalizer mod = LineParser.Parse(contents[line]);
                    if (mod != null) {
                        for (int ch = 0; ch < active.Length; ++ch) {
                            if (!channels.ContainsKey(active[ch]))
                                (channels[active[ch]] = new EqualizedChannel(active[ch])).Modify(channels["ALL"].Result);
                            channels[active[ch]].Modify(mod);
                        }
                    }
                }
            }

            result = new EqualizedChannel[channels.Count];
            int i = 0;
            foreach (KeyValuePair<string, EqualizedChannel> channel in channels)
                result[i++] = channel.Value;
        }

        public void ExportWAV(string path, ExportFormat format, BitDepth bits, int sampleRate, int samples) {
            for (int channel = 0; channel < result.Length; ++channel)
                result[channel].ExportWAV(Path.Combine(path, string.Format("Channel_{0}.wav", result[channel].Name)),
                    format, bits, sampleRate, samples);
        }

        public void ExportC(string path, ExportFormat format, BitDepth bits, int sampleRate, int samples) {
            for (int channel = 0; channel < result.Length; ++channel)
                result[channel].ExportC(Path.Combine(path, string.Format("Channel_{0}.c", result[channel].Name)),
                    format, bits, sampleRate, samples);
        }
    }
}