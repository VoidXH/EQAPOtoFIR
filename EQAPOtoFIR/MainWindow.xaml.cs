using Cavern.Format;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace EQAPOtoFIR {
    /// <summary>
    /// Interaction logic for the application window.
    /// </summary>
    public partial class MainWindow : Window {
        ConfigParser parser;

        /// <summary>
        /// Initialize the application window.
        /// </summary>
        public MainWindow() => InitializeComponent();

        /// <summary>
        /// Ask the user for a configuration file.
        /// </summary>
        void Open(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog {
                InitialDirectory = "C:\\Program Files\\EqualizerAPO\\config",
                Filter = "Equalizer APO configuration files (*.txt)|*.txt"
            };
            if (dialog.ShowDialog() == true) {
                configFile.Content = Path.GetFileName(dialog.FileName);
                parser = new ConfigParser(dialog.FileName);
                export.IsEnabled = true;
            }
        }

        /// <summary>
        /// Select a location and export the results.
        /// </summary>
        void Export(object sender, RoutedEventArgs e) {
            SaveFileDialog dialog = new SaveFileDialog();
            if (wav.IsChecked.Value) {
                dialog.FileName = "Channel_ALL.wav";
                dialog.Filter = "RIFF WAVE files (*.wav)|*.wav";
            } else {
                dialog.FileName = "Channel_ALL.c";
                dialog.Filter = "C source files (*.c)|*.c";
            }
            if (dialog.ShowDialog() == true) {
                BitDepth bits = BitDepth.Float32;
                if (int8.IsChecked.Value)
                    bits = BitDepth.Int8;
                else if (int16.IsChecked.Value)
                    bits = BitDepth.Int16;
                else if (int24.IsChecked.Value)
                    bits = BitDepth.Int24;
                ExportFormat format = impulse.IsChecked.Value ? ExportFormat.Impulse : ExportFormat.FIR;
                if (wav.IsChecked.Value)
                    parser.ExportWAV(Path.GetDirectoryName(dialog.FileName), format, bits, sampleRate.Value, fftSize.Value);
                else
                    parser.ExportC(Path.GetDirectoryName(dialog.FileName), format, bits, sampleRate.Value, fftSize.Value);
            }
        }
    }
}