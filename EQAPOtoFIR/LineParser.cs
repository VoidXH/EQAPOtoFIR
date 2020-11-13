using Cavern.QuickEQ;
using Cavern.QuickEQ.Equalization;
using System.Globalization;

namespace EQAPOtoFIR {
    public static class LineParser {
        public static Equalizer Parse(string line) {
            if (string.IsNullOrEmpty(line))
                return null;
            string[] split = line.Split(':');
            return (split[0]) switch
            {
                "Preamp" => Preamp(split[1]),
                "GraphicEQ" => GraphicEQ(split[1]),
                _ => null,
            };
        }

        static bool ParseGain(string from, out double gain) =>
            double.TryParse(from.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out gain);

        public static Equalizer Preamp(string source) {
            source = source.TrimStart();
            if (!ParseGain(source.Substring(0, source.IndexOf(' ')), out double gain))
                return null;
            Equalizer eq = new Equalizer();
            eq.AddBand(new Band(1000, gain));
            return eq;
        }

        public static Equalizer GraphicEQ(string source) {
            Equalizer eq = new Equalizer();
            string[] split = source.Split(';');
            for (int i = 0; i < split.Length; ++i) {
                string[] band = split[i].Trim().Split(' ');
                if (ParseGain(band[0].Replace(',', '.'), out double freq) && ParseGain(band[1].Replace(',', '.'), out double gain))
                    eq.AddBand(new Band(freq, gain));
            }
            return eq;
        }
    }
}