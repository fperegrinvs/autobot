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
    using System.Configuration;

    using Autobot.Common;
    using Autobot.WpfClient.Gestures;

    using MjpegProcessor;

    /// <summary>
    /// Autobot Cliente Main Window
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

        /// <summary>
        /// Bot client
        /// </summary>
        private BotClient botClient;

        public MainWindow()
        {
            InitializeComponent();

            botClient = new BotClient();

            grid = Graph;// new VirtualCanvas();
            grid.SmallScrollIncrement = new Size(TileWidth + TileMargin, TileHeight + TileMargin);

            //Scroller.Content = grid;

            Canvas target = grid.ContentCanvas;
            zoom = new MapZoom(target);
            var rectZoom = new RectangleSelectionGesture(target, this.zoom, ModifierKeys.Control);
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
            var shape = new TileShape(new Rect(0, 0, 30, 30));
            shape.IsFree = true;
            shape.IsVisited = true;
            shape.Sensor = new List<SenseData>();
            var distances = new byte[] { 0, 255, 255, 128, 255, 128, 0, 0 };
            var singleAngle = 360 / distances.Length;
            for (var i = 0; i < distances.Length; i++)
            {
                shape.Sensor.Add(new SenseData
                                 {
                                     Angle = singleAngle * i,
                                     Distance = distances[i]
                                 });
            }
            grid.AddVirtualChild(shape);

            shape = new TileShape(new Rect(30, 30, 30, 30));
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
                        Created.BeginAnimation(WidthProperty, new DoubleAnimation(
                            Math.Min(addedPerSecond, 450),
                            new Duration(TimeSpan.FromMilliseconds(100))));
                        CreatedLabel.Text = addedPerSecond.ToString(CultureInfo.InvariantCulture) + " created";
                        addedPerSecond = 0;

                        Destroyed.BeginAnimation(WidthProperty, new DoubleAnimation(
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
            Created.BeginAnimation(WidthProperty, null);
            Created.Width = 0;
            CreatedLabel.Text = "";
            Destroyed.BeginAnimation(WidthProperty, null);
            Destroyed.Width = 0;
            DestroyedLabel.Text = "";
        }

        void OnShowGridLines(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
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
                    gridCell.Points = new PointCollection(new[] { new Point(0, height-0.1),
                        new Point(width-0.1, height-0.1), new Point(width-0.1, 0) });
                    var gridLines = new VisualBrush(gridCell);
                    gridLines.TileMode = TileMode.Tile;
                    gridLines.Viewport = new Rect(0, 0, 1.0 / numTileToAccumulate, 1.0 / numTileToAccumulate);
                    gridLines.AlignmentX = AlignmentX.Center;
                    gridLines.AlignmentY = AlignmentY.Center;

                    var outerVb = new VisualBrush();
                    var outerRect = new Rectangle();
                    outerRect.Width = 10.0;  //can be any size
                    outerRect.Height = 10.0;
                    outerRect.Fill = gridLines;
                    outerVb.Visual = outerRect;
                    outerVb.Viewport = new Rect(0, 0,
                        width * numTileToAccumulate, height * numTileToAccumulate);
                    outerVb.ViewportUnits = BrushMappingMode.Absolute;
                    outerVb.TileMode = TileMode.Tile;

                    grid.Backdrop.Background = outerVb;

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
            if (Math.Abs(this.ZoomSlider.Value - this.zoom.Zoom) > 0.1)
            {
                ZoomSlider.Value = zoom.Zoom;
            }
        }

        void OnZoomSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Math.Abs(this.zoom.Zoom - e.NewValue) > 0.1)
            {
                zoom.Zoom = e.NewValue;
            }
        }

        private MjpegDecoder mjpegStream;

        private void DisconnectBotVideo()
        {
            mjpegStream.StopStream();
            CameraImage.Visibility = Visibility.Hidden;
        }

        private void ConnectToBotVideo()
        {
            mjpegStream = new MjpegDecoder();
            mjpegStream.FrameReady += this.MjpegFrameReady;
            mjpegStream.Error += mjpeg_Error;
            mjpegStream.ParseStream(new Uri(ConfigurationManager.AppSettings["VideoFeed"]));
            CameraImage.Visibility = Visibility.Visible;
        }

        private void MjpegFrameReady(object sender, FrameReadyEventArgs e)
        {
            CameraImage.Source = e.BitmapImage;
        }

        void mjpeg_Error(object sender, ErrorEventArgs e)
        {
            MessageBox.Show(e.Message);
        }

        public RemoteControl RemoteControl { get; set; }

        private void OnRemoteControl(object sender, RoutedEventArgs e)
        {
            // if using remote control and autocontrol is enabled, disable it
            if (!RemoteControlMenuItem.IsChecked && AutoControlMenuItem.IsChecked)
            {
                this.OnAutoControl(this, e);
            }

            RemoteControlMenuItem.IsChecked = !RemoteControlMenuItem.IsChecked;

            if (this.RemoteControlMenuItem.IsChecked)
            {
                this.RemoteControl = new RemoteControl(this.botClient);
                this.RemoteControl.PowerOn();
            }
            else
            {
                this.RemoteControl.PowerOff();
                this.RemoteControl = null;
            }
        }

        private void OnAutoControl(object sender, RoutedEventArgs e)
        {
            if (!AutoControlMenuItem.IsChecked)
            {
                if (ManualControlMenuItem.IsChecked)
                {
                    this.OnManualControl(this, e);
                }

                if (RemoteControlMenuItem.IsChecked)
                {
                    this.OnRemoteControl(this, e);
                }
            }

            throw new NotImplementedException();
        }

        private void OnConfig(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnManualControl(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnShowCamera(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            item.IsChecked = !item.IsChecked;

            if (item.IsChecked)
            {
                this.ConnectToBotVideo();
            }
            else
            {
                this.DisconnectBotVideo();
            }
        }

        private void OnShowObstacles(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnUp(object sender, RoutedEventArgs e)
        {
            botClient.Forward();
        }

        private void OnDown(object sender, RoutedEventArgs e)
        {
            botClient.Back();
        }

        private void OnLeft(object sender, RoutedEventArgs e)
        {
            botClient.Left();
        }

        private void OnRight(object sender, RoutedEventArgs e)
        {
            botClient.Right();
        }

        private void OnSense(object sender, RoutedEventArgs e)
        {
            botClient.UpdateSenseData();
        }
    }
}
