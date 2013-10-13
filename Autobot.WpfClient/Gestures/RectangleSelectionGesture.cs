//-----------------------------------------------------------------------
// <copyright file="RectangleSelectionGesture.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Autobot.WpfClient.Gestures
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;

    /// <summary>
    /// This class provides the ability to draw a rectangle on a zoomable object and zoom into that location.
    /// </summary>
    internal class RectangleSelectionGesture
    {
        SelectionRectVisual _selectionRectVisual;
        Point _start;
        bool _watching;
        FrameworkElement _target;
        MapZoom _zoom;
        Panel _container;
        Point _mouseDownPoint;
        Rect _selectionRect;
        bool _zoomSelection;
        int _zoomSizeThreshold = 20;
        int _selectionThreshold = 5; // allow some mouse wiggle on mouse down without actually selecting stuff!
        ModifierKeys _mods;

        public event EventHandler Selected;

        /// <summary>
        /// Construct new RectangleSelectionGesture object for selecting things in the given target object.
        /// </summary>
        /// <param name="target">A FrameworkElement</param>
        /// <param name="zoom">The MapZoom object that wraps this same target object</param>
        public RectangleSelectionGesture(FrameworkElement target, MapZoom zoom, ModifierKeys mods)
        {
            this._mods = mods;
            this._target = target;
            this._container = target.Parent as Panel;
            if (this._container == null)
            {
                throw new ArgumentException("Target object must live in a Panel");
            }
            this._zoom = zoom;
            this._container.MouseLeftButtonDown += new MouseButtonEventHandler(this.OnMouseLeftButtonDown);
            this._container.MouseLeftButtonUp += new MouseButtonEventHandler(this.OnMouseLeftButtonUp);
            this._container.MouseMove += new MouseEventHandler(this.OnMouseMove);
        }

        /// <summary>
        /// Get the rectangle the user drew on the target object.
        /// </summary>
        public Rect SelectionRectangle
        {
            get { return this._selectionRect; }
        }

        /// <summary>
        /// Get/Set whether to also zoom the selected rectangle.
        /// </summary>
        public bool ZoomSelection
        {
            get { return this._zoomSelection; }
            set { this._zoomSelection = value; }
        }            

        /// <summary>
        /// Handle the mouse left button down event
        /// </summary>
        /// <param name="sender">Mouse</param>
        /// <param name="e">Mouse down information</param>
        void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!e.Handled && (Keyboard.Modifiers & this._mods) == this._mods)
            {
                this._start = e.GetPosition(this._container);
                this._watching = true;
                this._mouseDownPoint = this._start;
            }
        }

        /// <summary>
        /// Get/Set threshold that sets the minimum size rectangle we will allow user to draw.
        /// This allows user to start drawing a rectangle by then change their mind and mouse up
        /// without trigging an almost infinite zoom out to a very smalle piece of real-estate.
        /// </summary>
        public int ZoomSizeThreshold
        {
            get { return this._zoomSizeThreshold; }
            set { this._zoomSizeThreshold = value; }
        }

        /// <summary>
        /// Handle Mouse Move event.  Here we detect whether we've exceeded the _selectionThreshold
        /// and if so capture the mouse and create the visual zoom rectangle on the container object.
        /// </summary>
        /// <param name="sender">Mouse</param>
        /// <param name="e">Mouse move information.</param>
        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (this._watching)
            {
                Point pos = e.GetPosition(this._container);
                if (new Vector(this._start.X - pos.X, this._start.Y - pos.Y).Length > this._selectionThreshold)
                {
                    this._watching = false;
                    Mouse.Capture(this._target, CaptureMode.SubTree);
                    this._selectionRectVisual = new SelectionRectVisual(this._start, this._start, this._zoom.Zoom);
                    this._container.Children.Add(this._selectionRectVisual);
                }
            }
            if (this._selectionRectVisual != null)
            {
                if (this._selectionRectVisual.Zoom != this._zoom.Zoom)
                {
                    this._selectionRectVisual.Zoom = this._zoom.Zoom;
                }
                this._selectionRectVisual.SecondPoint = e.GetPosition(this._container);
            }
        }

        /// <summary>
        /// Handle the mouse left button up event.  Here we actually process the selected rectangle
        /// if any by first raising an event for client to receive then also zooming to that rectangle
        /// if ZoomSelection is true
        /// </summary>
        /// <param name="sender">Mouse</param>
        /// <param name="e">Mouse button information</param>
        void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this._watching = false;
            if (this._selectionRectVisual != null)
            {
                Mouse.Capture(this._target, CaptureMode.None);
                Point pos = e.GetPosition(this._container);
                double f = Math.Min(Math.Abs(pos.X - this._mouseDownPoint.X), Math.Abs(pos.Y - this._mouseDownPoint.Y));
                Rect r = this.GetSelectionRect(pos);
                this._selectionRect = r;
                if (this.Selected != null)
                {
                    this.Selected(this, EventArgs.Empty);
                }

                if (this._zoomSelection && f > this._zoomSizeThreshold )
                {
                    this._zoom.ZoomToRect(r);
                }

                this._container.Children.Remove(this._selectionRectVisual);
                this._selectionRectVisual = null;
            }
        }

        /// <summary>
        /// Get the actual selection rectangle that encompasses the mouse down position and the given point.
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        Rect GetSelectionRect(Point p)
        {
            Rect r = new Rect(this._start, p);
            return this._container.TransformToDescendant(this._target).TransformBounds(r);
        }
    }
}