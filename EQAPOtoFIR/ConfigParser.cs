using Cavern.Format;
using System.Collections.Generic;
using System.IO;

namespace EQAPOtoFIR {
    /// <summary>
    /// Parser for a complete Equalizer APO configuration file.
    /// </summary>
    public class ConfigParser {
        readonly EqualizedChannel[] result;

        /// <summary>
        /// Load an Equalizer APO configuration file and parse all channel's filters.
        /// </summary>
        public ConfigParser(string path) {
            string[] contents = File.ReadAllLines(path);
            string[] active = new string[] { "ALL" };
            Dictionary<string, EqualizedChannel> channels = new Dictionary<string, EqualizedChannel> {
                ["ALL"] = new EqualizedChannel("ALL")
            };
            for (int line = 0; line < contents.Length; ++line) {
                if (string.IsNullOrWhiteSpace(contents[line]))
                    continue;
                if (contents[line].StartsWith("Channel:"))
                    active = contents[line].Substring(contents[line].IndexOf(' ') + 1).Trim().ToUpper().Split(' ');
                else {
                    for (int ch = 0; ch < active.Length; ++ch) {
                        if (!channels.ContainsKey(active[ch]))
                            (channels[active[ch]] = new EqualizedChannel(active[ch])).Modify(channels["ALL"].Result);
                        LineParser.Parse(contents[line], channels[active[ch]]);
                    }
                }
            }

            result = new EqualizedChannel[channels.Count];
            int i = 0;
            foreach (KeyValuePair<string, EqualizedChannel> channel in channels)
                result[i++] = channel.Value;
        }

        /// <summary>
        /// Export the filters as RIFF WAVE files.
        /// </summary>
        public void ExportWAV(string path, ExportFormat format, BitDepth bits, int sampleRate, int samples, bool minimumPhase) {
            for (int channel = 0; channel < result.Length; ++channel) {
                BinaryWriter binaryWriter =
                    new BinaryWriter(File.Open(Path.Combine(path, string.Format("Channel_{0}.wav", result[channel].Name)), FileMode.Create));
                RIFFWaveWriter writer = new RIFFWaveWriter(binaryWriter, 1, samples, sampleRate, bits);
                result[channel].Export(writer, format, minimumPhase);
            }
        }

        /// <summary>
        /// Export the filters as C arrays.
        /// </summary>
        public void ExportC(string path, ExportFormat format, BitDepth bits, int sampleRate, int samples, bool minimumPhase) {
            for (int channel = 0; channel < result.Length; ++channel) {
                BinaryWriter binaryWriter =
                    new BinaryWriter(File.Open(Path.Combine(path, string.Format("Channel_{0}.c", result[channel].Name)), FileMode.Create));
                CWriter writer = new CWriter(binaryWriter, 1, samples, sampleRate, bits);
                result[channel].Export(writer, format, minimumPhase);
            }
        }

        /// <summary>
        /// Export the filters as HLS additions.
        /// </summary>
        public void ExportHLS(string path, ExportFormat format, BitDepth bits, int sampleRate, int samples, bool minimumPhase) {
            for (int channel = 0; channel < result.Length; ++channel) {
                BinaryWriter binaryWriter =
                    new BinaryWriter(File.Open(Path.Combine(path, string.Format("Channel_{0}.txt", result[channel].Name)), FileMode.Create));
                HLSWriter writer = new HLSWriter(binaryWriter, 1, samples, sampleRate, bits);
                result[channel].ExportInBlocks(writer, format, minimumPhase, 64);
            }
        }
    }
}