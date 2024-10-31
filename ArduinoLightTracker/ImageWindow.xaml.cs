using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ArduinoLightTracker
{
    public partial class ImageWindow : Window
    {
        private double[,] originalData;
        private double globalMax;
        private double globalMin;

        public ImageWindow()
        {
            InitializeComponent();
            cmbMappingFunction.SelectionChanged += CmbMappingFunction_SelectionChanged;
        }

        public string AssociatedFileName { get; set; }

        public Canvas GetLightCanvas()
        {
            return lightCanvas;
        }

        public void UpdateImage(double[,] data, double max, double min)
        {
            originalData = data;
            globalMax = max;
            globalMin = min;

            string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(AssociatedFileName);
            txtFileName.Text = fileNameWithoutExtension;

            this.Title = fileNameWithoutExtension;

            ApplyMappingFunctionAndDraw();
        }

        public void UpdateGlobalMinMax(double max, double min)
        {
            globalMax = max;
            globalMin = min;
            ApplyMappingFunctionAndDraw();
        }

        public double[,] GetOriginalData()
        {
            return originalData;
        }

        public void SetOriginalData(double[,] data)
        {
            originalData = data;
            ApplyMappingFunctionAndDraw();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyMappingFunctionAndDraw();
        }

        private void CmbMappingFunction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyMappingFunctionAndDraw();
        }

        private void ApplyMappingFunctionAndDraw()
        {
            if (originalData == null) return;

            double[,] mappedData = ApplyMappingFunction(originalData);
            DrawGrayscaleImage(mappedData, originalData);
        }

        public double[,] ApplyMappingFunction(double[,] intensityData)
        {
            int rows = intensityData.GetLength(0);
            int cols = intensityData.GetLength(1);

            double[,] mappedData = new double[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double normalizedValue = (intensityData[i, j] - globalMin) / (globalMax - globalMin);
                    mappedData[i, j] = MapValue(normalizedValue);
                }
            }

            return mappedData;
        }

        private double MapValue(double value)
        {
            string selectedMapping = ((ComboBoxItem)cmbMappingFunction.SelectedItem).Content.ToString();

            switch (selectedMapping)
            {
                case "Square Root":
                    return Math.Sqrt(value);
                case "Square":
                    return value * value;
                default:
                    return value; // Linear
            }
        }

        public void DrawGrayscaleImage(double[,] mappedData, double[,] originalData)
        {
            int rows = mappedData.GetLength(0);
            int cols = mappedData.GetLength(1);

            lightCanvas.Children.Clear();

            double cellWidth = lightCanvas.ActualWidth / cols;
            double cellHeight = lightCanvas.ActualHeight / rows;

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double intensity = mappedData[i, j];
                    byte grayValue = (byte)(intensity * 255);
                    Color color = Color.FromRgb(grayValue, grayValue, grayValue);

                    Rectangle rect = new Rectangle
                    {
                        Width = cellWidth,
                        Height = cellHeight,
                        Fill = new SolidColorBrush(color)
                    };

                    Canvas.SetLeft(rect, j * cellWidth);
                    Canvas.SetTop(rect, i * cellHeight);
                    lightCanvas.Children.Add(rect);

                    double originalValue = originalData[i, j];
                    TextBlock textBlock = new TextBlock
                    {
                        Text = $"{i},{j}\n{originalValue}",
                        Foreground = new SolidColorBrush(Colors.Red),
                        FontSize = 12,
                        Background = new SolidColorBrush(Colors.Transparent)
                    };

                    Canvas.SetLeft(textBlock, j * cellWidth);
                    Canvas.SetTop(textBlock, i * cellHeight);
                    lightCanvas.Children.Add(textBlock);
                }
            }
        }
    }
}
