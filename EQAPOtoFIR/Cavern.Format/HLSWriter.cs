using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cavern.Format {
    /// <summary>HLS addition writer.</summary>
    public class HLSWriter : AudioWriter {
        /// <summary>
        /// Skipped blocks (all samples are 0).
        /// </summary>
        bool[] skip;

        /// <summary>
        /// Currently written block ID.
        /// </summary>
        int block;

        /// <summary>HLS addition writer.</summary>
        /// <param name="writer">File writer object</param>
        /// <param name="channelCount">Output channel count</param>
        /// <param name="length">Output length in samples</param>
        /// <param name="sampleRate">Output sample rate</param>
        /// <param name="bits">Output bit depth</param>
        public HLSWriter(BinaryWriter writer, int channelCount, long length, int sampleRate, BitDepth bits) :
            base(writer, channelCount, length, sampleRate, bits) { }

        /// <summary>Create the file header.</summary>
        public override void WriteHeader() {
            string function = 
@"Sample convolver(Sample newSample, const IRSample impulse[LENGTH], Sample buffer[LENGTH], int &bufferPos) {
#pragma HLS expression_balance" + Environment.NewLine;
            for (int i = 0; i < function.Length; ++i)
                writer.Write(function[i]);
        }

        /// <summary>Write a block of samples.</summary>
        /// <param name="samples">Samples to write</param>
        /// <param name="from">Start position in the input array (inclusive)</param>
        /// <param name="to">End position in the input array (exclusive)</param>
        public override void WriteBlock(float[] samples, long from, long to) {
            if (skip == null)
                skip = new bool[samples.Length / (to - from)];
            StringBuilder output = new StringBuilder();
            switch (Bits) {
                case BitDepth.Int8:
                case BitDepth.Int16:
                case BitDepth.Int24: {
                        Dictionary<long, string> sums = new Dictionary<long, string>();
                        long mul = (long)Math.Pow(2, (double)Bits);
                        for (long i = from; i < to; ++i) {
                            long sample = (long)(samples[i] * mul);
                            if (sample != 0) {
                                if (sums.ContainsKey(sample))
                                    sums[sample] += string.Format(" + (ResultSample)buffer{0}[(bufferPos + {1}) % {2}]",
                                        block, i - from, to - from);
                                else
                                    sums.Add(sample, string.Format("(ResultSample)buffer{0}[(bufferPos + {1}) % {2}]",
                                        block, i - from, to - from));
                            }
                        }
                        foreach (KeyValuePair<long, string> pair in sums) {
                            if (output.Length != 0)
                                output.Append(Environment.NewLine).Append("	+ ");
                            output.Append(string.Format("({0}) * (ResultSample){1}", pair.Value, pair.Key));
                        }
                        break;
                    }
                case BitDepth.Float32: {
                        for (long i = from; i < to;) {
                            output.Append(string.Format("(ResultSample)buffer{0}[(bufferPos + {1}) % {2}] * (ResultSample){3}",
                                block, i - from, to - from, samples[i]));
                            if (++i != to)
                                output.Append(" +").Append(Environment.NewLine);
                        }
                        break;
                    }
                default:
                    break;
            }
            if (output.Length != 0) {
                string oldOut = output.ToString();
                output = new StringBuilder();
                output.Append(string.Format("#pragma HLS array_partition variable=buffer{0} complete", block)).Append(Environment.NewLine);
                output.Append(string.Format("	ResultSample res{0} =", block));
                output.Append(oldOut);
                output.Append(";").Append(Environment.NewLine);
            } else
                skip[block] = true;
            if (to == samples.Length) {
                output.Append(string.Format(
@"	if (bufferPos == 0)
		bufferPos = {0};
	else
		--bufferPos;", to - from)).Append(Environment.NewLine);
                for (int i = block; i > 0; --i)
                    output.Append(string.Format("	buffer{0}[bufferPos] = buffer{1}[bufferPos];", i, i - 1)).Append(Environment.NewLine);
                output.Append("	buffer0[bufferPos] = newSample;").Append(Environment.NewLine);
                output.Append("	ResultSample res = (");
                bool blockWritten = false;
                for (int i = 0; i <= block; ++i) {
                    if (i != block) {
                        if (skip[i])
                            continue;
                        if (blockWritten)
                            output.Append(string.Format(" + res{0}", i));
                        else {
                            blockWritten = true;
                            output.Append("res").Append(i);
                        }
                    } else if (!skip[i]) {
                        if (blockWritten)
                            output.Append(string.Format(" + res{0}) / MAX_VALUE;", i)).Append(Environment.NewLine);
                        else
                            output.Append(string.Format("res{0}) / MAX_VALUE;", i)).Append(Environment.NewLine);
                    } else
                        output.Append(") / MAX_VALUE;").Append(Environment.NewLine);
                }
                output.Append(
@"	if (res < -MAX_SAMPLE)
		return -MAX_SAMPLE;
	if (res > MAX_SAMPLE)
		return MAX_SAMPLE;
	return res;
}").Append(Environment.NewLine).Append(Environment.NewLine);
                for (int i = 0; i <= block; ++i)
                    output.Append(string.Format("Sample buffer{0}[{1}];", i, to - from)).Append(Environment.NewLine);
            } else
                ++block;
            string result = output.ToString();
            for (int i = 0; i < result.Length; ++i)
                writer.Write(result[i]);
        }
    }
}