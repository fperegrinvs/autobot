using System;

namespace Autobot.WpfClient
{
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Shapes;

    using Autobot.Common;

    public class GridShape : IVirtualChild
    {
        // border
        protected static Brush BorderStroke = Brushes.Black;

        /// <summary>
        /// Stroke used when grid is unkwon (unvisited and unknown state)
        /// </summary>
        protected static Brush UnkownStroke = Brushes.Silver;

        /// <summary>
        /// Stoke used when the grid is blocked
        /// </summary>
        protected static Brush BlockedStroke = Brushes.DimGray;

        /// <summary>
        /// Stroke used when the grid is free, but not visited
        /// </summary>
        protected static Brush FreeNotVisitedStroke = Brushes.WhiteSmoke;

        /// <summary>
        /// Stroke used when the grid is free and visited
        /// </summary>
        protected static Brush VisitedStroke = Brushes.White;

        /// <summary>
        /// Is visited yet ?
        /// </summary>
        public bool IsVisited
        {
            get
            {
                return this.isVisited;
            }
            set
            {
                this.isVisited = value;
                this.Changed = true;
            }
        }

        private bool isVisited;

        public bool Changed { get; private set;  }

        public double Radius { get; private set; }

        public Point Center { get; private set; }

        public List<SenseData> Sensor { get; set; }

        private bool? isFree;

        /// <summary>
        /// True, False, Unknown
        /// </summary>
        public bool? IsFree {
            get
            {
                return this.isFree;
            }
            set
            {
                this.isFree = value;
                this.Changed = true;

            }
        }

        private UIElement visual;

        public Rect Bounds { get; private set; }

        public event EventHandler BoundsChanged;

        public UIElement Visual
        {
            get { return this.visual; }
        }

        public GridShape(Rect bounds)
        {
            this.Bounds = bounds;
            this.Radius = bounds.Height / 2;
            this.Center = bounds.TopLeft + new Vector(this.Radius, this.Radius);
        }

        public UIElement CreateVisual(VirtualCanvas parent)
        {
            if (this.visual == null || this.Changed)
            {
                var c = new Canvas { Width = this.Bounds.Width, Height = this.Bounds.Height };

                var outerPoly = new Polygon { Points = new PointCollection(4) };
                outerPoly.Points.Add(new Point(0, 0));
                outerPoly.Points.Add(new Point(0, this.Bounds.Height));
                outerPoly.Points.Add(new Point(this.Bounds.Width, this.Bounds.Height));
                outerPoly.Points.Add(new Point(this.Bounds.Width, 0));

                outerPoly.StrokeThickness = 1;
                outerPoly.Stroke = BorderStroke;

                if (this.IsVisited)
                {
                    outerPoly.Fill = VisitedStroke;
                }
                else if (!this.IsFree.HasValue)
                {
                    outerPoly.Fill = UnkownStroke;
                }
                else if (this.IsFree.Value)
                {
                    outerPoly.Fill = FreeNotVisitedStroke;
                }
                else
                {
                    outerPoly.Fill = BlockedStroke;
                }

                c.Children.Add(outerPoly);

                if (isVisited && Sensor != null && Sensor.Count > 1)
                {
                    double ofset = Math.Abs(Sensor[0].Angle - Sensor[1].Angle) / 360.0 * Math.PI;
                    // double startAngleInner = angleInner / 2;
                    var innerRadio = Radius * 4 / 5;

                    var points = new PointCollection(Sensor.Count);
                    for (var i = 0; i < Sensor.Count; i++)
                    {
                        var angle = Sensor[i].Angle / 180.0 * Math.PI;

                        var p = new Point
                        {
                            X = this.Center.X - this.Bounds.X + innerRadio * Math.Sin(angle - ofset),
                            Y = this.Center.Y - this.Bounds.Y + innerRadio * Math.Cos(angle - ofset)
                        };

                        points.Add(p);
                    }

                    for (var i = 0; i < Sensor.Count; i++)
                    {
                        var l = new Line();
                        l.X1 = points[i].X;
                        l.Y1 = points[i].Y;
                        l.X2 = points[(i + 1) % Sensor.Count].X;
                        l.Y2 = points[(i + 1) % Sensor.Count].Y;
                        
                        if (Sensor[i].Distance < 128)
                        {
                            l.Stroke = new SolidColorBrush(Color.FromRgb(byte.MaxValue, (byte)(2 * this.Sensor[i].Distance), 0));
                        }
                        else
                        {
                            l.Stroke = new SolidColorBrush(Color.FromRgb((byte)(byte.MaxValue - (2 * (this.Sensor[i].Distance - 128))), byte.MaxValue, 0));
                        }

                        l.StrokeThickness = 5;
                        c.Children.Add(l);
                    }
                }

                this.visual = c;
            }

            return this.visual;
        }

        public void DisposeVisual()
        {
            this.visual = null;
        }
    }
}
