using System;
using System.IO;
using System.Text;

namespace Cavern.Format {
    /// <summary>C array writer.</summary>
    public class CWriter : AudioWriter {
        /// <summary>C array writer.</summary>
        /// <param name="writer">File writer object</param>
        /// <param name="channelCount">Output channel count</param>
        /// <param name="length">Output length in samples</param>
        /// <param name="sampleRate">Output sample rate</param>
        /// <param name="bits">Output bit depth</param>
        public CWriter(BinaryWriter writer, int channelCount, long length, int sampleRate, BitDepth bits) :
            base(writer, channelCount, length, sampleRate, bits) { }

        /// <summary>Create the file header.</summary>
        public override void WriteHeader() { }

        static void CutZeros<T>(ref T[] arr) where T: IComparable {
            int newLength = arr.Length;
            while (newLength > 0) {
                if (arr[--newLength].CompareTo(default(T)) != 0) {
                    ++newLength;
                    break;
                }
            }
            Array.Resize(ref arr, newLength);
        }

        void WriteBlock<T>(StringBuilder output, T[] conv, long sourceLength, long from, long to, string typename) where T: IComparable {
            if (from == 0)
                output.Append(string.Format("{0} samples[{1}][{2}] = {{", typename, sourceLength / (to - from), to - from));
            CutZeros(ref conv);
            output.Append(Environment.NewLine).Append("\t{ ").Append(string.Join(", ", conv)).Append(" },");
            if (to == sourceLength)
                output.Append(Environment.NewLine).Append("};");
        }

        /// <summary>Write a block of samples.</summary>
        /// <param name="samples">Samples to write</param>
        /// <param name="from">Start position in the input array (inclusive)</param>
        /// <param name="to">End position in the input array (exclusive)</param>
        public override void WriteBlock(float[] samples, long from, long to) {
            StringBuilder output = new StringBuilder();
            switch (Bits) {
                case BitDepth.Int8: {
                        byte[] conv = new byte[to - from];
                        for (long i = from; i < to; ++i)
                            conv[i - from] = (byte)((samples[i] + 1) * 127f);
                        WriteBlock(output, conv, samples.Length, from, to, "unsigned char");
                        break;
                    }
                case BitDepth.Int16: {
                        short[] conv = new short[to - from];
                        for (long i = from; i < to; ++i)
                            conv[i - from] = (short)(samples[i] * 32767f);
                        WriteBlock(output, conv, samples.Length, from, to, "short");
                        break;
                    }
                case BitDepth.Int24: {
                        int[] conv = new int[to - from];
                        for (long i = from; i < to; ++i)
                            conv[i - from] = (int)(samples[i] * 8388607f);
                        WriteBlock(output, conv, samples.Length, from, to, "int");
                        break;
                    }
                case BitDepth.Float32:
                    float[] export = new float[to - from];
                    for (long i = from; i < to; ++i)
                        export[i - from] = samples[i];
                    WriteBlock(output, export, samples.Length, from, to, "float");
                    break;
                default:
                    break;
            }
            string result = output.ToString();
            for (int i = 0; i < result.Length; ++i)
                writer.Write(result[i]);
        }
    }
}