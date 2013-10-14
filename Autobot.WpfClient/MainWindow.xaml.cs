//-----------------------------------------------------------------------
// <copyright file="Window1.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

using System.Globalization;

namespace Autobot.WpfClient
{
    using System.Collections.Generic;

    using Autobot.Common;
    using Autobot.WpfClient.Gestures;

    using MjpegProcessor;

    /// <summary>
    /// This demo shows the VirtualCanvas managing up to 50,000 random WPF shapes providing smooth scrolling and
    /// zooming while creating those shapes on the fly.  This helps make a WPF canvas that is a lot more
    /// scalable.
    /// </summary>
    public partial class MainWindow
    {
        readonly MapZoom zoom;

        readonly VirtualCanvas grid;

        bool showGridLines;
        bool animateStatus = true;

        private const double TileWidth = 50;

        private const double TileHeight = 30;

        private const double TileMargin = 10;

        private const int TotalVisuals = 0;

        public MainWindow()
        {
            InitializeComponent();

            grid = Graph;// new VirtualCanvas();
            grid.SmallScrollIncrement = new Size(TileWidth + TileMargin, TileHeight + TileMargin);

            //Scroller.Content = grid;
 
            Canvas target = grid.ContentCanvas;
            zoom = new MapZoom(target);
            RectangleSelectionGesture rectZoom = new RectangleSelectionGesture(target, this.zoom, ModifierKeys.Control);
            rectZoom.ZoomSelection = true;
            zoom.ZoomChanged += this.OnZoomChanged;

            grid.VisualsChanged += this.OnVisualsChanged;
            ZoomSlider.ValueChanged += this.OnZoomSliderValueChanged;

            grid.Scale.Changed += this.OnScaleChanged;
            grid.Translate.Changed += this.OnScaleChanged;

            grid.Background = new SolidColorBrush(Color.FromRgb(0xd0, 0xd0, 0xd0));
            grid.ContentCanvas.Background = grid.Background;

            AllocateNodes();
        }

        private void AllocateNodes()
        {
            zoom.Zoom = 1;
            zoom.Offset = new Point(0, 0);
            var shape = new GridShape(new Rect(0, 0, 30, 30));
            shape.IsFree = true;
            shape.IsVisited = true;
            shape.Sensor = new List<SenseData>();
            var distances = new byte[]{ 0, 255, 255, 128, 255, 128, 0, 0};
            var singleAngle = 360 / distances.Length;
            for (var i = 0; i < distances.Length; i++)
            {
                shape.Sensor.Add(new SenseData()
                                 {
                                      Angle = singleAngle * i,
                                      Distance = distances[i]
                                 });
            }
            grid.AddVirtualChild(shape);

            shape = new GridShape(new Rect(30, 30, 30, 30));
            shape.IsFree = true;
            shape.IsVisited = true;
            shape.Sensor = null;
            grid.AddVirtualChild(shape);
        }

        void OnScaleChanged(object sender, EventArgs e)
        {
            // Make the grid lines get thinner as you zoom in
            double t = _gridLines.StrokeThickness = 0.1 / grid.Scale.ScaleX;
            grid.Backdrop.BorderThickness = new Thickness(t);
        }

        int lastTick = Environment.TickCount;
        int addedPerSecond;
        int removedPerSecond;

        void OnVisualsChanged(object sender, VisualChangeEventArgs e)
        {
            if (this.animateStatus)
            {
                StatusText.Text = string.Format(CultureInfo.InvariantCulture, "{0} live visuals of {1} total", grid.LiveVisualCount, TotalVisuals);

                int tick = Environment.TickCount;
                if (e.Added != 0 || e.Removed != 0)
                {
                    addedPerSecond += e.Added;
                    removedPerSecond += e.Removed;
                    if (tick > lastTick + 100)
                    {
                        Created.BeginAnimation(Rectangle.WidthProperty, new DoubleAnimation(
                            Math.Min(addedPerSecond, 450),
                            new Duration(TimeSpan.FromMilliseconds(100))));
                        CreatedLabel.Text = addedPerSecond.ToString(CultureInfo.InvariantCulture) + " created";
                        addedPerSecond = 0;

                        Destroyed.BeginAnimation(Rectangle.WidthProperty, new DoubleAnimation(
                            Math.Min(removedPerSecond, 450),
                            new Duration(TimeSpan.FromMilliseconds(100))));
                        DestroyedLabel.Text = removedPerSecond.ToString(CultureInfo.InvariantCulture) + " disposed";
                        removedPerSecond = 0;
                    }
                }
                if (tick > lastTick + 1000)
                {
                    lastTick = tick;
                }
            }
        }

        void OnAnimateStatus(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            this.animateStatus = item.IsChecked = !item.IsChecked;

            StatusText.Text = "";
            Created.BeginAnimation(Rectangle.WidthProperty, null);
            Created.Width = 0;
            CreatedLabel.Text = "";
            Destroyed.BeginAnimation(Rectangle.WidthProperty, null);
            Destroyed.Width = 0;
            DestroyedLabel.Text = "";
        }

        void OnShowGridLines(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            this.ShowGridLines = item.IsChecked = !item.IsChecked;
        }

        Polyline _gridLines = new Polyline();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1702")]
        public bool ShowGridLines
        {
            get { return this.showGridLines; }
            set
            {
                this.showGridLines = value;
                if (value)
                {
                    double width = TileWidth + TileMargin;
                    double height = TileHeight + TileMargin;

                    double numTileToAccumulate = 16;

                    Polyline gridCell = _gridLines;
                    gridCell.Margin = new Thickness(TileMargin);
                    gridCell.Stroke = Brushes.Blue;
                    gridCell.StrokeThickness = 0.1;
                    gridCell.Points = new PointCollection(new Point[] { new Point(0, height-0.1),
                        new Point(width-0.1, height-0.1), new Point(width-0.1, 0) });
                    VisualBrush gridLines = new VisualBrush(gridCell);
                    gridLines.TileMode = TileMode.Tile;
                    gridLines.Viewport = new Rect(0, 0, 1.0 / numTileToAccumulate, 1.0 / numTileToAccumulate);
                    gridLines.AlignmentX = AlignmentX.Center;
                    gridLines.AlignmentY = AlignmentY.Center;

                    VisualBrush outerVB = new VisualBrush();
                    Rectangle outerRect = new Rectangle();
                    outerRect.Width = 10.0;  //can be any size
                    outerRect.Height = 10.0;
                    outerRect.Fill = gridLines;
                    outerVB.Visual = outerRect;
                    outerVB.Viewport = new Rect(0, 0,
                        width * numTileToAccumulate, height * numTileToAccumulate);
                    outerVB.ViewportUnits = BrushMappingMode.Absolute;
                    outerVB.TileMode = TileMode.Tile;

                    grid.Backdrop.Background = outerVB;

                    Border border = grid.Backdrop;
                    border.BorderBrush = Brushes.Blue;
                    border.BorderThickness = new Thickness(0.1);
                    grid.InvalidateVisual();
                }
                else
                {
                    grid.Backdrop.Background = null;
                }
            }
        }

        void OnZoom(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            string tag = item.Tag as string;
            if (tag == "Fit")
            {
                double scaleX = grid.ViewportWidth / grid.Extent.Width;
                double scaleY = grid.ViewportHeight / grid.Extent.Height;
                zoom.Zoom = Math.Min(scaleX, scaleY);
                zoom.Offset = new Point(0, 0);
            }
            else
            {
                double zoomPercent;
                if (double.TryParse(tag, out zoomPercent))
                {
                    zoom.Zoom = zoomPercent / 100;
                }
            }
        }

        void OnZoomChanged(object sender, EventArgs e)
        {
            if (ZoomSlider.Value != zoom.Zoom)
            {
                ZoomSlider.Value = zoom.Zoom;
            }
        }

        void OnZoomSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (zoom.Zoom != e.NewValue)
            {
                zoom.Zoom = e.NewValue;
            }
        }

        private void ConnectToBotVideo()
        {
            MjpegDecoder mjpeg = new MjpegDecoder();
            mjpeg.FrameReady += mjpeg_FrameReady;
            mjpeg.Error += mjpeg_Error;
            mjpeg.ParseStream(new Uri("http://192.168.1.12:8080/videofeed"));
        }

        private void mjpeg_FrameReady(object sender, FrameReadyEventArgs e)
        {
            //pictureBox1.Image = e.Bitmap;
        }

        void mjpeg_Error(object sender, ErrorEventArgs e)
        {
            MessageBox.Show(e.Message);
        }
    }
}
