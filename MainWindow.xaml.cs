using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace ArduinoLightTracker
{
    public partial class MainWindow : Window
    {
        private SerialPort serialPort;
        private DispatcherTimer timer;
        private List<string> lightDataList;
        private string folderPath;
        private int totalDataPoints; // Total expected data points

        public MainWindow()
        {
            InitializeComponent();
            SetupSerialPort();
            lightDataList = new List<string>();
            folderPath = @"C:\LightTrackerData"; // Change this path as needed
            Directory.CreateDirectory(folderPath);
            LoadFileList(); // Load existing files on startup

            // Initialize ProgressBar
            pbRecordingProgress.Minimum = 0;
            pbRecordingProgress.Value = 0;
        }

        private void SetupSerialPort()
        {
            try
            {
                serialPort = new SerialPort("COM6", 115200);
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();
                Console.WriteLine("Serial port opened successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open serial port: {ex.Message}");
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;

            timer = new DispatcherTimer();
            timer.Tick += Timer_Tick_StartRecording;
            timer.Start();

            // Reset ProgressBar
            pbRecordingProgress.Value = 0;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;

            if (timer != null)
            {
                timer.Stop();
            }

            // Reset lightDataList
            lightDataList.Clear();

            // Reset ProgressBar
            pbRecordingProgress.Value = 0;
        }

        private void Timer_Tick_StartRecording(object sender, EventArgs e)
        {
            timer.Stop();
            StartLightTracking();

            // Set timer to save data every 2 minutes after starting recording
            timer.Interval = TimeSpan.FromMinutes(2);
            timer.Tick -= Timer_Tick_StartRecording;
            timer.Tick += Timer_Tick_SaveData;
            timer.Start();
        }

        private void Timer_Tick_SaveData(object sender, EventArgs e)
        {
            SaveLightDataToFile();

            // After saving, restart the process with the initial delay
            timer.Stop();
            timer.Interval = TimeSpan.FromMinutes(30); // 30 minutes delay
            timer.Tick -= Timer_Tick_SaveData;
            timer.Tick += Timer_Tick_StartRecording;
            timer.Start();

            // Reset ProgressBar
            pbRecordingProgress.Value = 0;
        }

        private void StartLightTracking()
        {
            string command = "TABLE,0,35,0,30\n";
            serialPort.WriteLine(command);

            // Calculate total number of rows based on the command
            var parameters = command.Split(new char[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int minY = int.Parse(parameters[1]);
            int maxY = int.Parse(parameters[2]);
            int step = 5; // Assuming step size is fixed at 5
            totalDataPoints = (maxY - minY) / step + 1;

            // Update ProgressBar maximum value
            pbRecordingProgress.Maximum = totalDataPoints;
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = serialPort.ReadLine();

                // Find the start and end of the data block
                int startIndex = data.IndexOf('{');
                int endIndex = data.LastIndexOf('}') + 1;

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    // Extract only the part of the data within the braces
                    string extractedData = data.Substring(startIndex, endIndex - startIndex);

                    // Add the extracted data to the list
                    lightDataList.Add(extractedData);
                    Console.WriteLine($"Data received and extracted: {extractedData}");

                    // Update ProgressBar
                    Dispatcher.Invoke(() =>
                    {
                        pbRecordingProgress.Value = lightDataList.Count;
                    });
                }
                else
                {
                    Console.WriteLine("No relevant data found in received message.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading data: {ex.Message}");
            }
        }

        private void SaveLightDataToFile()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
            string fileName = Path.Combine(folderPath, $"{timestamp}.txt");

            File.WriteAllLines(fileName, lightDataList);
            lightDataList.Clear(); // Clear the list for the next interval

            LoadFileList(); // Reload file list after saving

            // Reset ProgressBar
            Dispatcher.Invoke(() =>
            {
                pbRecordingProgress.Value = 0;
            });
        }

        private List<ImageWindow> openWindows = new List<ImageWindow>();

        private void BtnShowImage_Click(object sender, RoutedEventArgs e)
        {
            if (lbFileSelector.SelectedItems != null && lbFileSelector.SelectedItems.Count > 0)
            {
                // Create a combined list of all intensity data, including previously opened files
                List<(ImageWindow window, double[,] data)> allWindowData = new List<(ImageWindow, double[,])>();

                // Add data from already open windows
                foreach (var window in openWindows)
                {
                    allWindowData.Add((window, window.GetOriginalData()));
                }

                // Track new windows that will be opened
                List<ImageWindow> newWindows = new List<ImageWindow>();

                // Add data from newly selected files
                foreach (var selectedItem in lbFileSelector.SelectedItems)
                {
                    string selectedFile = selectedItem.ToString();
                    bool isAlreadyOpen = openWindows.Any(window => window.AssociatedFileName == selectedFile);

                    if (!isAlreadyOpen)
                    {
                        string[] lines = File.ReadAllLines(selectedFile);
                        double[,] intensityData = ParseIntensityData(lines);

                        // Create new window
                        ImageWindow imageWindow = new ImageWindow();
                        imageWindow.AssociatedFileName = selectedFile; // Set the associated file name
                        imageWindow.Closed += ImageWindow_Closed;

                        // Add the data to the list
                        allWindowData.Add((imageWindow, intensityData));

                        // Track the new window to be shown later
                        newWindows.Add(imageWindow);
                    }
                }

                // Calculate global min and max across all intensity data
                double globalMax = allWindowData.SelectMany(tuple => tuple.data.Cast<double>()).Max();
                double globalMin = allWindowData.SelectMany(tuple => tuple.data.Cast<double>()).Min();

                // Output global min and max to the console
                Console.WriteLine($"Global Max Intensity: {globalMax}");
                Console.WriteLine($"Global Min Intensity: {globalMin}");
                Console.WriteLine("--------------------------------------------");

                // Update existing windows with new global min/max
                foreach (var (window, data) in allWindowData)
                {
                    window.UpdateImage(data, globalMax, globalMin);
                }

                // Show and update new windows
                foreach (var window in newWindows)
                {
                    openWindows.Add(window);
                    window.Show();
                }
            }
        }

        private void ImageWindow_Closed(object sender, EventArgs e)
        {
            if (sender is ImageWindow closedWindow)
            {
                openWindows.Remove(closedWindow);
                RecalculateAndUpdateGlobalMinMax();
            }
        }

        private void RecalculateAndUpdateGlobalMinMax()
        {
            if (openWindows.Count == 0) return;

            double globalMax = double.MinValue;
            double globalMin = double.MaxValue;

            foreach (var window in openWindows)
            {
                var data = window.GetOriginalData();
                double fileMax = data.Cast<double>().Max();
                double fileMin = data.Cast<double>().Min();

                if (fileMax > globalMax) globalMax = fileMax;
                if (fileMin < globalMin) globalMin = fileMin;
            }

            UpdateAllWindowsGlobalMinMax(globalMax, globalMin);
        }

        private void UpdateAllWindowsGlobalMinMax(double globalMax, double globalMin)
        {
            foreach (var window in openWindows)
            {
                window.UpdateGlobalMinMax(globalMax, globalMin);
            }

            Console.WriteLine($"Global Max Intensity: {globalMax}");
            Console.WriteLine($"Global Min Intensity: {globalMin}");
            Console.WriteLine("--------------------------------------------");
        }

        private void LoadFileList()
        {
            lbFileSelector.Items.Clear();
            string[] files = Directory.GetFiles(folderPath, "*.txt");
            foreach (var file in files)
            {
                lbFileSelector.Items.Add(file);
            }
        }

        private double[,] ParseIntensityData(string[] lines)
        {
            int maxX = 0;
            int maxY = 0;

            // Extract values to find maxX and maxY
            foreach (var line in lines)
            {
                var triplets = line.Split(new string[] { "}, {" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var triplet in triplets)
                {
                    var values = triplet.Trim(new char[] { '{', '}', ' ' }).Split(',');
                    int y = int.Parse(values[0]);
                    int x = int.Parse(values[1]);
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            double[,] intensityData = new double[maxY / 5 + 1, maxX / 5 + 1];

            // Fill the intensity data
            foreach (var line in lines)
            {
                var triplets = line.Split(new string[] { "}, {" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var triplet in triplets)
                {
                    var values = triplet.Trim(new char[] { '{', '}', ' ' }).Split(',');
                    int y = int.Parse(values[0]);
                    int x = int.Parse(values[1]);
                    double intensity = double.Parse(values[2]);
                    intensityData[y / 5, x / 5] = intensity;
                }
            }

            return intensityData;
        }
    }
}
