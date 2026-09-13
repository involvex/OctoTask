using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using OctoTask.UI.ViewModels;

namespace OctoTask.UI.Views
{
    public partial class ResourceGraphView : UserControl
    {
        private static readonly SolidColorBrush CpuBrush = new(Color.FromRgb(59, 130, 246));
        private static readonly SolidColorBrush RamBrush = new(Color.FromRgb(16, 185, 129));
        private static readonly SolidColorBrush GridBrush = new(Color.FromRgb(51, 65, 85));
        private static readonly SolidColorBrush LabelBrush = new(Color.FromRgb(100, 116, 139));

        public ResourceGraphView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            SizeChanged += OnSizeChanged;
        }

        private void OnDataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;

            if (e.NewValue is INotifyPropertyChanged newVm)
                newVm.PropertyChanged += OnViewModelPropertyChanged;

            Redraw();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ResourceGraphViewModel.CpuPoints)
                or nameof(ResourceGraphViewModel.RamPoints)
                or nameof(ResourceGraphViewModel.IsExpanded))
            {
                Redraw();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        private void Redraw()
        {
            if (DataContext is not ResourceGraphViewModel vm)
                return;

            var canvas = GraphCanvas;
            if (canvas == null)
                return;

            canvas.Children.Clear();
            canvas.Width = ActualWidth;
            canvas.Height = ActualHeight;

            if (!vm.IsExpanded || canvas.Width <= 0 || canvas.Height <= 0)
                return;

            double paddingLeft = 40;
            double paddingRight = 8;
            double paddingTop = 8;
            double paddingBottom = 20;

            double plotWidth = canvas.Width - paddingLeft - paddingRight;
            double plotHeight = canvas.Height - paddingTop - paddingBottom;

            if (plotWidth <= 0 || plotHeight <= 0)
                return;

            void DrawGridLines()
            {
                for (int i = 0; i <= 4; i++)
                {
                    double y = paddingTop + (plotHeight * i / 4.0);
                    var line = new Line
                    {
                        X1 = paddingLeft,
                        Y1 = y,
                        X2 = paddingLeft + plotWidth,
                        Y2 = y,
                        Stroke = GridBrush,
                        StrokeDashArray = new DoubleCollection { 2, 2 }
                    };
                    canvas.Children.Add(line);

                    var label = new TextBlock
                    {
                        Text = (100 - i * 25).ToString(),
                        Foreground = LabelBrush,
                        FontSize = 9,
                        TextAlignment = TextAlignment.Right
                    };
                    Canvas.SetLeft(label, paddingLeft - 30);
                    Canvas.SetTop(label, y - 5);
                    canvas.Children.Add(label);
                }

                for (int i = 0; i <= 3; i++)
                {
                    double x = paddingLeft + (plotWidth * i / 3.0);
                    var line = new Line
                    {
                        X1 = x,
                        Y1 = paddingTop,
                        X2 = x,
                        Y2 = paddingTop + plotHeight,
                        Stroke = GridBrush,
                        StrokeDashArray = new DoubleCollection { 2, 2 }
                    };
                    canvas.Children.Add(line);
                }
            }

            void DrawPolyline(List<Point> points, SolidColorBrush brush, double thickness)
            {
                if (points.Count < 2)
                    return;

                double maxX = points.Count - 1;
                var geo = new PathGeometry();
                var fig = new PathFigure { StartPoint = ToCanvasPoint(points[0], paddingLeft, paddingTop, plotWidth, plotHeight, maxX) };

                for (int i = 1; i < points.Count; i++)
                {
                    fig.Segments.Add(new LineSegment(ToCanvasPoint(points[i], paddingLeft, paddingTop, plotWidth, plotHeight, maxX), true));
                }

                geo.Figures.Add(fig);
                var path = new Path { Data = geo, Stroke = brush, StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round };
                canvas.Children.Add(path);
            }

            void DrawBaseline()
            {
                var baseline = new Line
                {
                    X1 = paddingLeft,
                    Y1 = paddingTop + plotHeight,
                    X2 = paddingLeft + plotWidth,
                    Y2 = paddingTop + plotHeight,
                    Stroke = GridBrush
                };
                canvas.Children.Add(baseline);
            }

            DrawGridLines();
            DrawBaseline();
            DrawPolyline(vm.RamPoints, RamBrush, 1.5);
            DrawPolyline(vm.CpuPoints, CpuBrush, 1.5);
        }

        private static Point ToCanvasPoint(Point p, double padLeft, double padTop, double plotWidth, double plotHeight, double maxX)
        {
            double x = padLeft + (p.X / Math.Max(maxX, 1)) * plotWidth;
            double y = padTop + plotHeight - (Math.Clamp(p.Y, 0, 100) / 100.0) * plotHeight;
            return new Point(x, y);
        }
    }
}
