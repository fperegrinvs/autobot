//-----------------------------------------------------------------------
// <copyright file="Pan.cs" company="Microsoft">
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
    /// This class provides the ability to pan the target object when dragging the mouse 
    /// </summary>
    class Pan {

        bool _dragging;
        FrameworkElement _target;
        MapZoom _zoom;
        bool _captured;
        Panel _container;
        Point _mouseDownPoint;
        Point _startTranslate;
        ModifierKeys _mods = ModifierKeys.None;

        /// <summary>
        /// Construct new Pan gesture object.
        /// </summary>
        /// <param name="target">The target to be panned, must live inside a container Panel</param>
        /// <param name="zoom"></param>
        public Pan(FrameworkElement target, MapZoom zoom) {
            this._target = target;
            this._container = target.Parent as Panel;
            if (this._container == null) {
                // todo: localization
                throw new ArgumentException("Target object must live in a Panel");
            }
            this._zoom = zoom;
            this._container.MouseLeftButtonDown += new MouseButtonEventHandler(this.OnMouseLeftButtonDown);
            this._container.MouseLeftButtonUp += new MouseButtonEventHandler(this.OnMouseLeftButtonUp);
            this._container.MouseMove += new MouseEventHandler(this.OnMouseMove);
        }

        /// <summary>
        /// Handle mouse left button event on container by recording that position and setting
        /// a flag that we've received mouse left down.
        /// </summary>
        /// <param name="sender">Container</param>
        /// <param name="e">Mouse information</param>
        void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {

            ModifierKeys mask = Keyboard.Modifiers & this._mods;
            if (!e.Handled && mask == this._mods && mask == Keyboard.Modifiers)
            {
                this._container.Cursor = Cursors.Hand;
                this._mouseDownPoint = e.GetPosition(this._container);
                Point offset = this._zoom.Offset;
                this._startTranslate = new Point(offset.X, offset.Y);
                this._dragging = true;
            }
        }

        /// <summary>
        /// Handle the mouse move event and this is where we capture the mouse.  We don't want
        /// to actually start panning on mouse down.  We want to be sure the user starts dragging
        /// first.
        /// </summary>
        /// <param name="sender">Mouse</param>
        /// <param name="e">Move information</param>
        void OnMouseMove(object sender, MouseEventArgs e) {
            if (this._dragging) {
                if (!this._captured) {
                    this._captured = true;
                    this._target.Cursor = Cursors.Hand;
                    Mouse.Capture(this._target, CaptureMode.SubTree);
                }
                this.MoveBy(this._mouseDownPoint - e.GetPosition(this._container));
            }
        }

        /// <summary>
        /// Handle the mouse left button up event and stop any panning.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {

            if (this._captured) {
                Mouse.Capture(this._target, CaptureMode.None);
                e.Handled = true;
                this._target.Cursor = Cursors.Arrow; ;
                this._captured = false;   
            }

            this._dragging = false;
        }

        /// <summary>
        /// Move the target object by the given delta delative to the start scroll position we recorded in mouse down event.
        /// </summary>
        /// <param name="v">A vector containing the delta from recorded mouse down position and current mouse position</param>
        public void MoveBy(Vector v) {
            this._zoom.Offset = new Point(this._startTranslate.X - v.X, this._startTranslate.Y - v.Y);
            this._target.InvalidateVisual();
        }
    }
}
