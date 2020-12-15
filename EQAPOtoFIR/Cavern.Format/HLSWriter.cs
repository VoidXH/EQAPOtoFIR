using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cavern.Format {
    /// <summary>HLS addition writer.</summary>
    public class HLSWriter : AudioWriter {
        /// <summary>HLS addition writer.</summary>
        /// <param name="writer">File writer object</param>
        /// <param name="channelCount">Output channel count</param>
        /// <param name="length">Output length in samples</param>
        /// <param name="sampleRate">Output sample rate</param>
        /// <param name="bits">Output bit depth</param>
        public HLSWriter(BinaryWriter writer, int channelCount, long length, int sampleRate, BitDepth bits) :
            base(writer, channelCount, length, sampleRate, bits) { }

        /// <summary>Create the file header.</summary>
        public override void WriteHeader() {}

        readonly Dictionary<long, List<long>> convolution = new Dictionary<long, List<long>>();
        long bufferLength = 0;

        void SplitMerge(int layer, long count, StringBuilder output) {
            string src = layer == 0 ? "sum" : $"res{layer}_", dst = $"res{layer+1}_";

            if (count == 1) {
                output.AppendLine($"\treturn (Sample)({src}0 / MAX_VALUE);");
                return;
            }
            if (count == 2) {
                output.AppendLine($"\treturn (Sample)(({src}0 + {src}1) / MAX_VALUE);");
                return;
            }

            for (long i = 0; i < (count + 1) / 2; ++i) {
                output.Append("\tResultSample ").Append(dst).Append(i).Append(" = ").Append(src).Append(2 * i);
                if (2 * i + 1 < count)
                    output.Append(" + ").Append(src).Append(2 * i + 1);
                output.AppendLine(";");
            }
            SplitMerge(layer + 1, (count + 1) / 2, output);
        }

        /// <summary>Write a block of samples.</summary>
        /// <param name="samples">Samples to write</param>
        /// <param name="from">Start position in the input array (inclusive)</param>
        /// <param name="to">End position in the input array (exclusive)</param>
        public override void WriteBlock(float[] samples, long from, long to) {
            switch (Bits) {
                case BitDepth.Int8:
                case BitDepth.Int16:
                case BitDepth.Int24: {
                        long mul = (long)Math.Pow(2, (double)Bits);
                        while (from < to) {
                            long sample = (long)(samples[from] * mul);
                            if (sample != 0) {
                                bufferLength = from;
                                if (convolution.ContainsKey(sample))
                                    convolution[sample].Add(from);
                                else
                                    convolution.Add(sample, new List<long>() { from });
                            }
                            ++from;
                        }
                        break;
                    }
                case BitDepth.Float32: {
                        // TODO
                        break;
                    }
                default:
                    break;
            }
            if (to == samples.LongLength) {
                StringBuilder code = new StringBuilder();
                string name = "i" + new Random().Next(0, int.MaxValue);

                // Buffer variables
                for (long i = 0; i <= bufferLength; ++i)
                    code.AppendLine($"Sample {name}_{i};");
                code.AppendLine();

                code.AppendLine("Sample convolution(Sample newSample) {");

                // Buffer handling (shift register)
                for (long i = bufferLength; i > 0; --i)
                    code.AppendLine($"\t{name}_{i} = {name}_{i - 1};");
                code.AppendLine($"\t{name}_0 = newSample;").AppendLine();

                // Optimization: try distribute single multipliers to multiple but non-single multipliers
                List<long> removal = new List<long>();
                foreach (KeyValuePair<long, List<long>> kv in convolution) {
                    if (kv.Value.Count != 1)
                        continue;
                    bool found = false;
                    foreach (KeyValuePair<long, List<long>> i in convolution) {
                        if (found || i.Value.Count == 1)
                            continue;
                        foreach (KeyValuePair<long, List<long>> j in convolution) {
                            if (i.Key == j.Key || j.Value.Count == 1)
                                continue;
                            if (i.Key + j.Key == kv.Key) {
                                found = true;
                                i.Value.AddRange(kv.Value);
                                j.Value.AddRange(kv.Value);
                                removal.Add(kv.Key);
                                break;
                            }
                        }
                    }
                }
                for (int i = 0, c = removal.Count; i < c; ++i)
                    convolution.Remove(removal[i]);

                // Optimization: move negative multipliers to positive ones as subtraction
                removal.Clear();
                foreach (KeyValuePair<long, List<long>> kv in convolution) {
                    if (kv.Key < 0) {
                        foreach (KeyValuePair<long, List<long>> cp in convolution) {
                            if (cp.Key == -kv.Key) {
                                for (int i = 0, c = kv.Value.Count; i < c; ++i)
                                    cp.Value.Add(-kv.Value[i]);
                                removal.Add(kv.Key);
                            }
                        }
                    }
                }
                for (int i = 0, c = removal.Count; i < c; ++i)
                    convolution.Remove(removal[i]);

                // First result block calculation
                int sums = 0;
                foreach (KeyValuePair<long, List<long>> kv in convolution) {
                    code.Append("\tResultSample sum").Append(sums++).Append(" = (");
                    bool first = true;
                    foreach (long v in kv.Value) {
                        if (first)
                            first = false;
                        else if (v > 0)
                            code.Append(" + ");
                        else // By the mechanics of the optimizer, it can't happen that the first item in kv.Value is < 0
                            code.Append(" - ");
                        code.Append(name).Append('_').Append(Math.Abs(v));
                    }
                    code.Append(')');
                    if (kv.Key != 1)
                        code.Append(" * ").Append(kv.Key);
                    code.AppendLine(";");
                }

                // Split merge - this is a compilation speed optimization, not an actual optimization
                SplitMerge(0, sums, code);

                code.AppendLine("}");

                string result = code.ToString();
                for (int i = 0; i < result.Length; ++i)
                    writer.Write(result[i]);
            }
        }
    }
}