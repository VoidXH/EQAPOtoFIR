using Cavern.Format;
using Cavern.QuickEQ;
using System;
using System.IO;
using System.Text;

namespace EQAPOtoFIR {
    /// <summary>
    /// All information required for the creation of a filter for a single channel.
    /// </summary>
    public class EqualizedChannel {
        /// <summary>
        /// Name of the equalized channel.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The actual status of EQ generation.
        /// </summary>
        public Equalizer Result { get; private set; } = new Equalizer();

        /// <summary>
        /// Added delay in samples. Applied on <see cref="DelayMs"/>.
        /// </summary>
        public int DelaySamples { get; private set; }

        /// <summary>
        /// Added delay in milliseconds. Applied on <see cref="DelaySamples"/>.
        /// </summary>
        public double DelayMs { get; private set; }

        /// <summary>
        /// Create an instance for a named channel.
        /// </summary>
        public EqualizedChannel(string name) => Name = name;

        /// <summary>
        /// Modify the EQ curve with another one.
        /// </summary>
        public void Modify(Equalizer with) {
            if (with != null)
                Result = Result.Merge(with);
        }

        /// <summary>
        /// Add delay to this channel in samples.
        /// </summary>
        public void AddDelay(int samples) => DelaySamples += samples;

        /// <summary>
        /// Add delay to this channel in milliseconds.
        /// </summary>
        public void AddDelay(double ms) => DelayMs += ms;

        /// <summary>
        /// Apply both delays on the target sample set.
        /// </summary>
        void ApplyDelay(float[] on, int sampleRate) {
            int delay = DelaySamples + (int)(DelayMs * .001 * sampleRate + .5f);
            for (int i = on.Length - delay - 1; i >= 0; --i)
                on[i + delay] = on[i];
            if (on.Length > delay)
                for (int i = delay - 1; i >= 0; --i)
                    on[i] = 0;
        }

        /// <summary>
        /// Export the channel's resulting impulse response as a WAV file.
        /// </summary>
        public void ExportWAV(string path, ExportFormat format, BitDepth bits, int sampleRate, int samples) {
            RIFFWaveWriter writer = new RIFFWaveWriter(new BinaryWriter(File.Open(path, FileMode.Create)), 1, samples, sampleRate, bits);
            float[] output = Result.GetConvolution(sampleRate, samples, 1);
            ApplyDelay(output, sampleRate);
            if (format == ExportFormat.FIR)
                Array.Reverse(output);
            writer.Write(output);
        }

        /// <summary>
        /// Export the channel's resulting impulse response in a C array.
        /// </summary>
        public void ExportC(string path, ExportFormat format, BitDepth bits, int sampleRate, int samples) {
            StringBuilder output = new StringBuilder();
            float[] audioSamples = Result.GetConvolution(sampleRate, samples, 1);
            ApplyDelay(audioSamples, sampleRate);
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