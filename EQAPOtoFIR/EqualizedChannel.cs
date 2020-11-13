using Cavern.Format;
using Cavern.QuickEQ;
using System;
using System.IO;
using System.Text;

namespace EQAPOtoFIR {
    public class EqualizedChannel {
        public string Name { get; private set; }

        public Equalizer Result { get; private set; } = new Equalizer();

        public EqualizedChannel(string name) => Name = name;

        public void Modify(Equalizer with) => Result = Result.Merge(with);

        public void ExportWAV(string path, ExportFormat format, BitDepth bits, int sampleRate, int samples) {
            RIFFWaveWriter writer = new RIFFWaveWriter(new BinaryWriter(File.Open(path, FileMode.Create)), 1, samples, sampleRate, bits);
            float[] output = Result.GetConvolution(sampleRate, samples, 1);
            if (format == ExportFormat.FIR)
                Array.Reverse(output);
            writer.Write(output);
        }

        public void ExportC(string path, ExportFormat format, BitDepth bits, int sampleRate, int samples) {
            StringBuilder output = new StringBuilder();
            float[] audioSamples = Result.GetConvolution(sampleRate, samples, 1);
            if (format == ExportFormat.FIR)
                Array.Reverse(audioSamples);
            switch (bits) {
                case BitDepth.Int8: {
                        output.Append("unsigned char samples[").Append(samples).Append("] = { ");
                        byte[] conv = new byte[samples];
                        for (int i = 0; i < samples; ++i)
                            conv[i] = (byte)((audioSamples[i] + 1) * 127f);
                        output.Append(string.Join(", ", conv)).Append("};");
                        break;
                    }
                case BitDepth.Int16: {
                        output.Append("short samples[").Append(samples).Append("] = { ");
                        short[] conv = new short[samples];
                        for (int i = 0; i < samples; ++i)
                            conv[i] = (short)(audioSamples[i] * 32767f);
                        output.Append(string.Join(", ", conv)).Append("};");
                        break;
                    }
                case BitDepth.Int24: {
                        output.Append("int samples[").Append(samples).Append("] = { ");
                        int[] conv = new int[samples];
                        for (int i = 0; i < samples; ++i)
                            conv[i] = (int)(audioSamples[i] * 8388607f);
                        output.Append(string.Join(", ", conv)).Append("};");
                        break;
                    }
                case BitDepth.Float32:
                    output.Append("float samples[").Append(samples).Append("] = { ").Append(string.Join(", ", audioSamples)).Append("};");
                    break;
                default:
                    break;
            }
            File.WriteAllText(path, output.ToString());
        }
    }
}