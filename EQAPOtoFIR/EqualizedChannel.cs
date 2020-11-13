using Cavern.Filters;
using Cavern.Format;
using Cavern.QuickEQ;
using Cavern.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        /// The actual status of EQ generation from source graphic EQs, without <see cref="Filters"/>.
        /// </summary>
        public Equalizer Result { get; private set; } = new Equalizer();

        /// <summary>
        /// Additional filters to be applied on the EQ when exporting.
        /// </summary>
        public IReadOnlyList<Filter> Filters => filters;

        /// <summary>
        /// Added delay in samples. Applied on <see cref="DelayMs"/>.
        /// </summary>
        public int DelaySamples { get; private set; }

        /// <summary>
        /// Added delay in milliseconds. Applied on <see cref="DelaySamples"/>.
        /// </summary>
        public double DelayMs { get; private set; }

        /// <summary>
        /// Additional filters to be applied on the EQ when exporting.
        /// </summary>
        readonly List<Filter> filters;

        /// <summary>
        /// Create an instance for a named channel.
        /// </summary>
        public EqualizedChannel(string name) {
            Name = name;
            filters = new List<Filter>();
        }

        /// <summary>
        /// Modify the EQ curve with another one.
        /// </summary>
        public void Modify(Equalizer with) {
            if (with != null)
                Result = Result.Merge(with);
        }

        /// <summary>
        /// Modify the EQ with a filter that is applied on export.
        /// </summary>
        public void Modify(Filter with) {
            if (with != null)
                filters.Add(with);
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
        /// Get the frequency response of the filters without phase distortion.
        /// </summary>
        Complex[] GetFilterResponse(int sampleRate, int samples) {
            Complex[] filterResults = new Complex[samples];
            for (int i = 0; i < samples; ++i)
                filterResults[i].Real = 1;
            for (int i = 0, c = filters.Count; i < c; ++i) {
                FilterAnalyzer analyzer = new FilterAnalyzer(filters[i], sampleRate) {
                    Resolution = samples
                };
                ReadOnlyCollection<Complex> response = analyzer.GetFrequencyResponseReadonly();
                for (int sample = 0; sample < samples; ++sample)
                    filterResults[sample].Real *= response[sample].Magnitude;
            }
            return filterResults;
        }

        /// <summary>
        /// Apply all <see cref="Filters"/> on a set of samples.
        /// </summary>
        void ApplyFilters(float[] samples) {
            for (int i = 0, c = filters.Count; i < c; ++i)
                filters[i].Process(samples);
        }

        /// <summary>
        /// Export the channel's resulting impulse response as a WAV file.
        /// </summary>
        public void ExportWAV(string path, ExportFormat format, BitDepth bits, int sampleRate, int samples, bool minimumPhase) {
            RIFFWaveWriter writer = new RIFFWaveWriter(new BinaryWriter(File.Open(path, FileMode.Create)), 1, samples, sampleRate, bits);
            Complex[] initialResponse = null;
            if (minimumPhase)
                initialResponse = GetFilterResponse(sampleRate, samples * 2);
            float[] output = Result.GetConvolution(sampleRate, samples, 1, initialResponse);
            if (!minimumPhase)
                ApplyFilters(output);
            ApplyDelay(output, sampleRate);
            if (format == ExportFormat.FIR)
                Array.Reverse(output);
            writer.Write(output);
        }

        /// <summary>
        /// Export the channel's resulting impulse response in a C array.
        /// </summary>
        public void ExportC(string path, ExportFormat format, BitDepth bits, int sampleRate, int samples, bool minimumPhase) {
            StringBuilder output = new StringBuilder();
            Complex[] initialResponse = null;
            if (minimumPhase)
                initialResponse = GetFilterResponse(sampleRate, samples * 2);
            float[] audioSamples = Result.GetConvolution(sampleRate, samples, 1, initialResponse);
            if (!minimumPhase)
                ApplyFilters(audioSamples);
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