using Cavern.Filters;
using Cavern.Format;
using Cavern.QuickEQ;
using Cavern.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
        /// Use a custom <see cref="AudioWriter"/> to export the filter.
        /// </summary>
        public void Export(AudioWriter writer, ExportFormat format, bool minimumPhase) {
            Complex[] initialResponse = null;
            if (minimumPhase)
                initialResponse = GetFilterResponse(writer.SampleRate, (int)writer.Length * 2);
            float[] output = Result.GetConvolution(writer.SampleRate, (int)writer.Length, 1, initialResponse);
            if (!minimumPhase)
                ApplyFilters(output);
            ApplyDelay(output, writer.SampleRate);
            if (format == ExportFormat.FIR)
                Array.Reverse(output);
            writer.Write(output);
        }
    }
}