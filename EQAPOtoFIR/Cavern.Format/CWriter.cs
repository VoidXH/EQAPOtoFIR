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

        /// <summary>Write a block of samples.</summary>
        /// <param name="samples">Samples to write</param>
        /// <param name="from">Start position in the input array (inclusive)</param>
        /// <param name="to">End position in the input array (exclusive)</param>
        public override void WriteBlock(float[] samples, long from, long to) {
            StringBuilder output = new StringBuilder();
            switch (Bits) {
                case BitDepth.Int8: {
                        output.Append("unsigned char samples[").Append(Length).Append("] = { ");
                        byte[] conv = new byte[Length];
                        for (long i = 0; i < Length; ++i)
                            conv[i] = (byte)((samples[i] + 1) * 127f);
                        output.Append(string.Join(", ", conv)).Append("};");
                        break;
                    }
                case BitDepth.Int16: {
                        output.Append("short samples[").Append(Length).Append("] = { ");
                        short[] conv = new short[Length];
                        for (long i = 0; i < Length; ++i)
                            conv[i] = (short)(samples[i] * 32767f);
                        output.Append(string.Join(", ", conv)).Append("};");
                        break;
                    }
                case BitDepth.Int24: {
                        output.Append("int samples[").Append(Length).Append("] = { ");
                        int[] conv = new int[Length];
                        for (long i = 0; i < Length; ++i)
                            conv[i] = (int)(samples[i] * 8388607f);
                        output.Append(string.Join(", ", conv)).Append("};");
                        break;
                    }
                case BitDepth.Float32:
                    output.Append("float samples[").Append(Length).Append("] = { ").Append(string.Join(", ", samples)).Append("};");
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