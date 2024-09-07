using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CliWrap;
using CliWrap.Buffered;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace WpfApp4
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public double DataGridRowHeight { get; set; } = 20; // Default height of 25
        private string _downloadDirectory;
        public MainWindow()
        {
            InitializeComponent();
            LoadDownloadDirectory();
        }

        private async void GetInfoButton_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text;
            if (string.IsNullOrWhiteSpace(url))
            {
                System.Windows.MessageBox.Show("Please enter a URL.");
                return;
            }

            await ExecuteYtDlp($"-F {url}");
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text;
            string formatId = FormatIdTextBox.Text;
            if (string.IsNullOrWhiteSpace(url))
            {
                System.Windows.MessageBox.Show("Please enter a URL.");
                return;
            }
            if (string.IsNullOrWhiteSpace(formatId))
            {
                System.Windows.MessageBox.Show("Please enter a Format ID.");
                return;
            }

            string downloadArguments = $"-f {formatId}";
            
            if (!string.IsNullOrWhiteSpace(_downloadDirectory))
            {
                downloadArguments += $" -o \"{_downloadDirectory}/%(title)s/%(title)s.%(ext)s\"";
            }

            await ExecuteYtDlp($"{downloadArguments} {url}");
        }

        private async Task ExecuteYtDlp(string arguments)
        {
            OutputTextBox.Clear();
            OutputTextBox.Text = "Processing...";
            OutputDataGrid.ItemsSource = null;
            var mainBuffer = new StringBuilder();
            var downloadBuffer = new StringBuilder();
            var stdErrBuffer = new StringBuilder();

            try
            {
                var result = await Cli.Wrap("yt-dlp")
                    .WithArguments(arguments)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
                    {
                        if (line.StartsWith("[download]"))
                        {
                            downloadBuffer.Clear();
                            downloadBuffer.Append(line);
                            UpdateOutputTextBox(mainBuffer.ToString(), downloadBuffer.ToString());
                        }
                        else
                        {
                            mainBuffer.AppendLine(line);
                            UpdateOutputTextBox(mainBuffer.ToString(), downloadBuffer.ToString());
                        }
                    }))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                    .ExecuteAsync();

                if (result.ExitCode == 0)
                {
                    var formats = ParseYtDlpOutput(mainBuffer.ToString());
                    Dispatcher.Invoke(() =>
                    {
                        OutputDataGrid.ItemsSource = formats;
                    });
                }
                else
                {
                    System.Windows.MessageBox.Show($"yt-dlp exited with code {result.ExitCode}. Error: {stdErrBuffer}");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private void UpdateOutputTextBox(string mainOutput, string downloadStatus)
        {
            Dispatcher.Invoke(() =>
            {
                OutputTextBox.Text = mainOutput + "\n" + downloadStatus;
                OutputTextBox.ScrollToEnd();
            });
        }

        private List<FormatInfo> ParseYtDlpOutput(string output)
        {
            var formats = new List<FormatInfo>();
            var lines = output.Split('\n');
            bool startParsing = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("ID"))
                {
                    startParsing = true;
                    continue;
                }

                if (startParsing && !string.IsNullOrWhiteSpace(line))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        var formatInfo = new FormatInfo
                        {
                            ID = parts[0],
                            Extension = parts[1],
                            Resolution = parts[2],
                            FileSize = ParseFileSize(parts),
                            Codec = string.Join(" ", parts.Skip(Math.Min(10, parts.Length - 1)))
                        };
                        formats.Add(formatInfo);
                    }
                }
            }

            return formats;
        }

        private string ParseFileSize(string[] parts)
        {
            for (int i = 3; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("~"))
                {
                    return parts[i] + " " + parts[i + 1];
                }
                if (parts[i].EndsWith("MiB") || parts[i].EndsWith("GiB"))
                {
                    return parts[i];
                }
            }
            return "N/A";
        }

        private void OutputDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OutputDataGrid.SelectedItem is FormatInfo selectedFormat)
            {
                FormatIdTextBox.Text = selectedFormat.ID;
            }
        }

        public class FormatInfo
        {
            public string ID { get; set; }
            public string Extension { get; set; }
            public string Resolution { get; set; }
            public string FileSize { get; set; }
            public string Codec { get; set; }
        }

        private void DownloadDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                _downloadDirectory = dialog.SelectedPath;
                DownloadDirectoryTextBox.Text = _downloadDirectory;
                SaveDownloadDirectory();
            }
        }

        private void SaveDownloadDirectory()
        {
            File.WriteAllText("downloadDirectory.txt", _downloadDirectory);
        }

        private void LoadDownloadDirectory()
        {
            if (File.Exists("downloadDirectory.txt"))
            {
                _downloadDirectory = File.ReadAllText("downloadDirectory.txt");
                DownloadDirectoryTextBox.Text = _downloadDirectory;
            }
            else
            {
                _downloadDirectory = AppDomain.CurrentDomain.BaseDirectory;
                DownloadDirectoryTextBox.Text = _downloadDirectory;
            }
        }
    }
}