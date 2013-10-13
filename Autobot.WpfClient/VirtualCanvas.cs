//-----------------------------------------------------------------------
// <copyright file="VirtualCanvas.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Autobot.WpfClient
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Media;
    using System.Windows.Threading;

    using Autobot.WpfClient.Gestures;

    public class VisualChangeEventArgs : EventArgs
    {
        public int Added { get; set; }
        public int Removed { get; set; }
        public VisualChangeEventArgs(int added, int removed)
        {
            this.Added = added;
            this.Removed = removed;
        }
    }

    /// <summary>
    /// This interface is implemented by the objects that you want to put in the VirtualCanvas.
    /// </summary>
    public interface IVirtualChild
    {
        /// <summary>
        /// The bounds of your child object
        /// </summary>
        Rect Bounds { get; }

        /// <summary>
        /// Raise this event if the Bounds changes.
        /// </summary>
        event EventHandler BoundsChanged;

        /// <summary>
        /// Return the current Visual or null if it has not been created yet.
        /// </summary>
        UIElement Visual { get; }

        /// <summary>
        /// Create the WPF visual for this object.
        /// </summary>
        /// <param name="parent">The canvas that is calling this method</param>
        /// <returns>The visual that can be displayed</returns>
        UIElement CreateVisual(VirtualCanvas parent);

        /// <summary>
        /// Dispose the WPF visual for this object.
        /// </summary>
        void DisposeVisual();
    }

    /// <summary>
    /// VirtualCanvas dynamically figures out which children are visible and creates their visuals 
    /// and which children are no longer visible (due to scrolling or zooming) and destroys their
    /// visuals.  This helps manage the memory consumption when you have so many objects that creating
    /// all the WPF visuals would take too much memory.
    /// </summary>
    public class VirtualCanvas : VirtualizingPanel, IScrollInfo
    {
        ScrollViewer _owner;
        Size _viewPortSize;
        bool _canHScroll = false;
        bool _canVScroll = false;
        QuadTree<IVirtualChild> _index;
        ObservableCollection<IVirtualChild> _children;
        Size _smallScrollIncrement = new Size(10, 10);
        Canvas _content;
        Border _backdrop;
        TranslateTransform _translate;
        ScaleTransform _scale;
        Size _extent;
        IList<Rect> _dirtyRegions = new List<Rect>();
        IList<Rect> _visibleRegions = new List<Rect>();
        IDictionary<IVirtualChild, int> _visualPositions;
        int _nodeCollectCycle;
        bool _done = true;
        MapZoom _zoom;

        public static DependencyProperty VirtualChildProperty = DependencyProperty.Register("VirtualChild", typeof(IVirtualChild), typeof(VirtualCanvas));

        public event EventHandler<VisualChangeEventArgs> VisualsChanged;

        delegate void UpdateHandler();

        /// <summary>
        /// Construct empty virtual canvas.
        /// </summary>
        public VirtualCanvas()
        {
            this._index = new QuadTree<IVirtualChild>();
            this._children = new ObservableCollection<IVirtualChild>();
            this._children.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(this.OnChildrenCollectionChanged);
            this._content = new Canvas();
            this._backdrop = new Border();
            this._content.Children.Add(this._backdrop);

            TransformGroup g = new TransformGroup();
            this._scale = new ScaleTransform();
            this._translate = new TranslateTransform();
            g.Children.Add(this._scale);
            g.Children.Add(this._translate);
            this._content.RenderTransform = g;

            this._translate.Changed += new EventHandler(this.OnTranslateChanged);
            this._scale.Changed += new EventHandler(this.OnScaleChanged);
            this.Children.Add(this._content);
        }

        /// <summary>
        /// Callback when _children collection is changed.
        /// </summary>
        /// <param name="sender">This</param>
        /// <param name="e">noop</param>
        void OnChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            this.RebuildVisuals();
        }

        /// <summary>
        /// Get/Set the MapZoom object used for manipulating the scale and translation on this canvas.
        /// </summary>
        internal MapZoom Zoom
        {
            get { return this._zoom; }
            set { this._zoom = value; }
        }

        /// <summary>
        /// Returns true if all Visuals have been created for the current scroll position
        /// and there is no more idle processing needed.
        /// </summary>
        public bool IsDone
        {
            get { return this._done; }
        }

        /// <summary>
        /// Resets the state so there is no Visuals associated with this canvas.
        /// </summary>
        private void RebuildVisuals()
        {
            // need to rebuild the index.
            this._index = null;
            this._visualPositions = null;
            this._visible = Rect.Empty;
            this._done = false;
            foreach (UIElement e in this._content.Children)
            {
                IVirtualChild n = e.GetValue(VirtualChildProperty) as IVirtualChild;
                if (n != null)
                {
                    e.ClearValue(VirtualChildProperty);
                    n.DisposeVisual();
                }
            }
            this._content.Children.Clear();
            this._content.Children.Add(this._backdrop);
            this.InvalidateArrange();
            this.StartLazyUpdate();
        }

        /// <summary>
        /// The current zoom transform.
        /// </summary>
        public ScaleTransform Scale
        {
            get { return this._scale; }
        }

        /// <summary>
        /// The current translate transform.
        /// </summary>
        public TranslateTransform Translate
        {
            get { return this._translate; }
        }

        /// <summary>
        /// Get/Set the IVirtualChild collection.  The VirtualCanvas will call CreateVisual on them
        /// when the Bounds of your child intersects the current visible view port.
        /// </summary>
        public ObservableCollection<IVirtualChild> VirtualChildren
        {
            get { return this._children; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }
                if (this._children != null)
                {
                    this._children.CollectionChanged -= new System.Collections.Specialized.NotifyCollectionChangedEventHandler(this.OnChildrenCollectionChanged);
                }
                this._children = value;
                this._children.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(this.OnChildrenCollectionChanged);
                this.RebuildVisuals();
            }
        }

        /// <summary>
        /// Set the scroll amount for the scroll bar arrows.
        /// </summary>
        public Size SmallScrollIncrement
        {
            get { return this._smallScrollIncrement; }
            set { this._smallScrollIncrement = value; }
        }

        /// <summary>
        /// Add a new IVirtualChild.  The VirtualCanvas will call CreateVisual on them
        /// when the Bounds of your child intersects the current visible view port.
        /// </summary>
        /// <param name="c"></param>
        public void AddVirtualChild(IVirtualChild child)
        {
            this._children.Add(child);
        }

        /// <summary>
        /// Return the list of virtual children that intersect the given bounds.
        /// </summary>
        /// <param name="bounds">The bounds to test</param>
        /// <returns>The list of virtual children found or null if there are none</returns>
        public IEnumerable<IVirtualChild> GetChildrenIntersecting(Rect bounds)
        {
            if (this._index != null)
            {
                return this._index.GetNodesInside(bounds);
            }
            return null;
        }

        /// <summary>
        /// Return true if there are any virtual children inside the given bounds.
        /// </summary>
        /// <param name="bounds">The bounds to test</param>
        /// <returns>True if a node is found whose bounds intersect the given bounds</returns>
        public bool HasChildrenIntersecting(Rect bounds)
        {
            if (this._index != null)
            {
                return this._index.HasNodesInside(bounds);
            }
            return false;
        }

        /// <summary>
        /// The number of visual children that are visible right now.
        /// </summary>
        public int LiveVisualCount
        {
            get { return this._content.Children.Count - 1; }
        }

        /// <summary>
        /// Callback whenever the current TranslateTransform is changed.
        /// </summary>
        /// <param name="sender">TranslateTransform</param>
        /// <param name="e">noop</param>
        void OnTranslateChanged(object sender, EventArgs e)
        {
            this.OnScrollChanged();
        }

        /// <summary>
        /// Callback whenever the current ScaleTransform is changed.
        /// </summary>
        /// <param name="sender">ScaleTransform</param>
        /// <param name="e">noop</param>
        void OnScaleChanged(object sender, EventArgs e)
        {
            this.OnScrollChanged();
        }

        /// <summary>
        /// The ContentCanvas that is actually the parent of all the VirtualChildren Visuals.
        /// </summary>
        public Canvas ContentCanvas
        {
            get { return this._content; }
        }

        /// <summary>
        /// The backgrop is the back most child of the ContentCanvas used for drawing any sort
        /// of background that is guarenteed to fill the ViewPort.
        /// </summary>
        public Border Backdrop
        {
            get { return this._backdrop; }
        }

        /// <summary>
        /// Calculate the size needed to display all the virtual children.
        /// </summary>
        void CalculateExtent()
        {
            bool rebuild = false;
            if (this._index == null || this._extent.Width == 0 || this._extent.Height == 0 ||
                double.IsNaN(this._extent.Width) || double.IsNaN(this._extent.Height))
            {
                rebuild = true;
                bool first = true;
                Rect extent = new Rect();
                this._visualPositions = new Dictionary<IVirtualChild, int>();
                int index = 0;
                foreach (IVirtualChild c in this._children)
                {
                    this._visualPositions[c] = index++;

                    Rect childBounds = c.Bounds;
                    if (childBounds.Width != 0 && childBounds.Height != 0)
                    {
                        if (double.IsNaN(childBounds.Width) || double.IsNaN(childBounds.Height))
                        {
                            throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentUICulture,
                                "Child type '{0}' returned NaN bounds", c.GetType().Name));
                        }
                        if (first)
                        {
                            extent = childBounds;
                            first = false;
                        }
                        else
                        {
                            extent = Rect.Union(extent, childBounds);
                        }
                    }
                }
                this._extent = extent.Size;
                // Ok, now we know the size we can create the index.
                this._index = new QuadTree<IVirtualChild>();
                this._index.Bounds = new Rect(0, 0, extent.Width, extent.Height);
                foreach (IVirtualChild n in this._children)
                {
                    if (n.Bounds.Width > 0 && n.Bounds.Height > 0)
                    {
                        this._index.Insert(n, n.Bounds);
                    }
                }
            }

            // Make sure we honor the min width & height.
            double w = Math.Max(this._content.MinWidth, this._extent.Width);
            double h = Math.Max(this._content.MinHeight, this._extent.Height);
            this._content.Width = w;
            this._content.Height = h;

            // Make sure the backdrop covers the ViewPort bounds.
            double zoom = this._scale.ScaleX;
            if (!double.IsInfinity(this.ViewportHeight) &&
                !double.IsInfinity(this.ViewportHeight))
            {
                w = Math.Max(w, this.ViewportWidth / zoom);
                h = Math.Max(h, this.ViewportHeight / zoom);
                this._backdrop.Width = w;
                this._backdrop.Height = h;
            }

            if (this._owner != null)
            {
                this._owner.InvalidateScrollInfo();
            }

            if (rebuild)
            {
                this.AddVisibleRegion();
            }
        }

        /// <summary>
        /// WPF Measure override for measuring the control
        /// </summary>
        /// <param name="availableSize">Available size will be the viewport size in the scroll viewer</param>
        /// <returns>availableSize</returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            base.MeasureOverride(availableSize);

            // We will be given the visible size in the scroll viewer here.
            this.CalculateExtent();

            if (availableSize != this._viewPortSize)
            {
                this.SetViewportSize(availableSize);
            }

            foreach (UIElement child in this.InternalChildren)
            {
                IVirtualChild n = child.GetValue(VirtualChildProperty) as IVirtualChild;
                if (n != null)
                {
                    Rect bounds = n.Bounds;
                    child.Measure(bounds.Size);
                }
            }
            if (double.IsInfinity(availableSize.Width))
            {
                return this._extent;
            }
            else
            {
                return availableSize;
            }
        }

        /// <summary>
        /// WPF ArrangeOverride for laying out the control
        /// </summary>
        /// <param name="finalSize">The size allocated by parents</param>
        /// <returns>finalSize</returns>
        protected override Size ArrangeOverride(Size finalSize)
        {
            base.ArrangeOverride(finalSize);

            this.CalculateExtent();

            if (finalSize != this._viewPortSize)
            {
                this.SetViewportSize(finalSize);
            }

            this._content.Arrange(new Rect(0, 0, this._content.Width, this._content.Height));

            if (this._index == null)
            {
                this.StartLazyUpdate();
            }

            return finalSize;
        }

        DispatcherTimer _timer;

        /// <summary>
        /// Begin a timer for lazily creating IVirtualChildren visuals
        /// </summary>
        void StartLazyUpdate()
        {
            if (this._timer == null)
            {
                this._timer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Normal,
                    new EventHandler(this.OnStartLazyUpdate), this.Dispatcher);
            }
            this._timer.Start();
        }

        /// <summary>
        /// Callback from the DispatchTimer
        /// </summary>
        /// <param name="sender">DispatchTimer </param>
        /// <param name="args">noop</param>
        void OnStartLazyUpdate(object sender, EventArgs args)
        {
            this._timer.Stop();
            this.LazyUpdateVisuals();
        }

        /// <summary>
        /// Set the viewport size and raize a scroll changed event.
        /// </summary>
        /// <param name="s">The new size</param>
        void SetViewportSize(Size s)
        {
            if (s != this._viewPortSize)
            {
                this._viewPortSize = s;
                this.OnScrollChanged();
            }
        }

        int _createQuanta = 1000;
        int _removeQuanta = 2000;
        int _gcQuanta = 5000;
        int _idealDuration = 50; // 50 milliseconds.
        int _added;
        int _removed;
        Rect _visible = Rect.Empty;
        delegate int QuantizedWorkHandler(int quantum);

        /// <summary>
        /// Do a quantized unit of work for creating newly visible visuals, and cleaning up visuals that are no
        /// longer needed.
        /// </summary>
        void LazyUpdateVisuals()
        {
            if (this._index == null)
            {
                this.CalculateExtent();
            }

            this._done = true;
            this._added = 0;
            this._removed = 0;

            this._createQuanta = SelfThrottlingWorker(this._createQuanta, this._idealDuration, new QuantizedWorkHandler(this.LazyCreateNodes));
            this._removeQuanta = SelfThrottlingWorker(this._removeQuanta, this._idealDuration, new QuantizedWorkHandler(this.LazyRemoveNodes));
            this._gcQuanta = SelfThrottlingWorker(this._gcQuanta, this._idealDuration, new QuantizedWorkHandler(this.LazyGarbageCollectNodes));

#if ANIMATE_FEEDBACK
            if (VisualsChanged != null)
            {
                VisualsChanged(this, new VisualChangeEventArgs(_added, _removed));
            }
#endif
            if (this._added > 0)
            {
                this.InvalidateArrange();
            }
            if (!this._done)
            {
                this.StartLazyUpdate();
                //this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new UpdateHandler(LazyUpdateVisuals));
            }
            this.InvalidateVisual();
        }

        /// <summary>
        /// Helper method for self-tuning how much time is allocated to the given handler.
        /// </summary>
        /// <param name="quantum">The current quantum allocation</param>
        /// <param name="idealDuration">The time in milliseconds we want to take</param>
        /// <param name="handler">The handler to call that does the work being throttled</param>
        /// <returns>Returns the new quantum to use next time that will more likely hit the ideal time</returns>
        private static int SelfThrottlingWorker(int quantum, int idealDuration, QuantizedWorkHandler handler)
        {
            PerfTimer timer = new PerfTimer();
            timer.Start();
            int count = handler(quantum);

            timer.Stop();
            long duration = timer.GetDuration();

            if (duration > 0 && count > 0)
            {
                long estimatedFullDuration = duration * (quantum / count);
                long newQuanta = (quantum * idealDuration) / estimatedFullDuration;
                quantum = Math.Max(100, (int)Math.Min(newQuanta, int.MaxValue));
            }

            return quantum;
        }

        /// <summary>
        /// Create visuals for the nodes that are now visible.
        /// </summary>
        /// <param name="quantum">Amount of work we can do here</param>
        /// <returns>Amount of work we did</returns>
        private int LazyCreateNodes(int quantum)
        {

            if (this._visible == Rect.Empty)
            {
                this._visible = this.GetVisibleRect();
                this._visibleRegions.Add(this._visible);
                this._done = false;
            }

            int count = 0;
            int regionCount = 0;
            while (this._visibleRegions.Count > 0 && count < quantum)
            {
                Rect r = this._visibleRegions[0];
                this._visibleRegions.RemoveAt(0);
                regionCount++;

                // Iterate over the visible range of nodes and make sure they have visuals.
                foreach (IVirtualChild n in this._index.GetNodesInside(r))
                {
                    if (n.Visual == null)
                    {
                        this.EnsureVisual(n);
                        this._added++;
                    }

                    count++;

                    if (count >= quantum)
                    {
                        // This region is too big, so subdivide it into smaller slices.
                        if (regionCount == 1)
                        {
                            // We didn't even complete 1 region, so we better split it.
                            this.SplitRegion(r, this._visibleRegions);
                        }
                        else
                        {
                            this._visibleRegions.Add(r); // put it back since we're not done!
                        }
                        this._done = false;
                        break;
                    }
                }

            }
            return count;
        }

        /// <summary>
        /// Insert the visual for the child in the same order as is is defined in the 
        /// VirtualChildren collection so the visuals draw on top of each other in the expected order.
        /// The trick is that GetNodesIntersecting returns the nodes in pretty much random order depending 
        /// on how the QuadTree decides to break up the canvas.  
        /// 
        /// The thing we should avoid is a linear search through the potentially large collection of 
        /// IVirtualChildren to compute its visible index which is why we have the _visualPositions map.  
        /// We should also avoid a N*M algorithm where N is the number of nodes returned from GetNodesIntersecting 
        /// and M is the number of children already visible.  For example, Page down in a zoomed out situation 
        /// gives potentially high N and and M which would basically be an O(n2) algorithm.  
        /// 
        /// So the solution is to use the _visualPositions map to get the expected visual position index
        /// of a given IVirtualChild, then do a binary search through existing visible children to find the
        /// insertion point of the new child.  So this is O(Log M).  
        /// </summary>
        /// <param name="child">The IVirtualChild to add visual for</param>
        public void EnsureVisual(IVirtualChild child)
        {
            if (child.Visual != null)
            {
                return;
            }

            UIElement e = child.CreateVisual(this);
            e.SetValue(VirtualChildProperty, child);
            Rect bounds = child.Bounds;
            Canvas.SetLeft(e, bounds.Left);
            Canvas.SetTop(e, bounds.Top);

            // Get the correct absolute position of this child.
            int position = this._visualPositions[child];

            // Now do a binary search for the correct insertion position based
            // on the visual positions of the existing visible children.
            UIElementCollection c = this._content.Children;
            int min = 0;
            int max = c.Count - 1;
            while (max > min + 1)
            {
                int i = (min + max) / 2;
                UIElement v = this._content.Children[i];
                IVirtualChild n = v.GetValue(VirtualChildProperty) as IVirtualChild;
                if (n != null)
                {
                    int index = this._visualPositions[n];
                    if (index > position)
                    {
                        // search from min to i.
                        max = i;
                    }
                    else
                    {
                        // search from i to max.
                        min = i;
                    }
                }
                else
                {
                    // Any nodes without IVirtualChild should be behind the
                    // IVirtualChildren by definition (like the Backdrop).
                    min = i;
                }
            }

            // If 'max' is the last child in the collection, then we need to see
            // if we have a new last child.
            if (max == c.Count - 1)
            {
                UIElement v = c[max];
                IVirtualChild maxchild = v.GetValue(VirtualChildProperty) as IVirtualChild;
                int maxpos = position;
                if (maxchild == null || position > this._visualPositions[maxchild])
                {
                    // Then we have a new last child!
                    max++;
                }
            }

            c.Insert(max, e);

        }


        /// <summary>
        /// Split a rectangle into 2 and add them to the regions list.
        /// </summary>
        /// <param name="r">Rectangle to split</param>
        /// <param name="regions">List to add to</param>
        private void SplitRegion(Rect r, IList<Rect> regions)
        {
            double minWidth = this.SmallScrollIncrement.Width * 2;
            double minHeight = this.SmallScrollIncrement.Height * 2;

            if (r.Width > r.Height && r.Height > minHeight)
            {
                // horizontal slices
                double h = r.Height / 2;
                regions.Add(new Rect(r.Left, r.Top, r.Width, h + 10));
                regions.Add(new Rect(r.Left, r.Top + h, r.Width, h + 10));
            }
            else if (r.Width < r.Height && r.Width > minWidth)
            {
                // vertical slices
                double w = r.Width / 2;
                regions.Add(new Rect(r.Left, r.Top, w + 10, r.Height));
                regions.Add(new Rect(r.Left + w, r.Top, w + 10, r.Height));
            }
            else
            {
                regions.Add(r); // put it back since we're not done!
            }
        }

        /// <summary>
        /// Remove visuals for nodes that are no longer visible.
        /// </summary>
        /// <param name="quantum">Amount of work we can do here</param>
        /// <returns>Amount of work we did</returns>
        private int LazyRemoveNodes(int quantum)
        {
            Rect visible = this.GetVisibleRect();
            int count = 0;

            // Also remove nodes that are no longer visible.
            int regionCount = 0;
            while (this._dirtyRegions.Count > 0 && count < quantum)
            {
                int last = this._dirtyRegions.Count - 1;
                Rect dirty = this._dirtyRegions[last];
                this._dirtyRegions.RemoveAt(last);
                regionCount++;

                // Iterate over the visible range of nodes and make sure they have visuals.
                foreach (IVirtualChild n in this._index.GetNodesInside(dirty))
                {
                    UIElement e = n.Visual;
                    if (e != null)
                    {
                        Rect nrect = n.Bounds;
                        if (!nrect.IntersectsWith(visible))
                        {
                            e.ClearValue(VirtualChildProperty);
                            this._content.Children.Remove(e);
                            n.DisposeVisual();
                            this._removed++;
                        }
                    }

                    count++;
                    if (count >= quantum)
                    {
                        if (regionCount == 1)
                        {
                            // We didn't even complete 1 region, so we better split it.
                            this.SplitRegion(dirty, this._dirtyRegions);
                        }
                        else
                        {
                            this._dirtyRegions.Add(dirty); // put it back since we're not done!
                        }
                        this._done = false;
                        break;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Check all child nodes to see if any leaked from LazyRemoveNodes and remove their visuals.
        /// </summary>
        /// <param name="quantum">Amount of work we can do here</param>
        /// <returns>The amount of work we did</returns>
        int LazyGarbageCollectNodes(int quantum)
        {

            int count = 0;
            // Now after every update also do a full incremental scan over all the children
            // to make sure we didn't leak any nodes that need to be removed.
            while (count < quantum && this._nodeCollectCycle < this._content.Children.Count)
            {
                UIElement e = this._content.Children[this._nodeCollectCycle++];
                IVirtualChild n = e.GetValue(VirtualChildProperty) as IVirtualChild;
                if (n != null)
                {
                    Rect nrect = n.Bounds;
                    if (!nrect.IntersectsWith(this._visible))
                    {
                        e.ClearValue(VirtualChildProperty);
                        this._content.Children.Remove(e);
                        n.DisposeVisual();
                        this._removed++;
                    }
                    count++;
                }
                this._nodeCollectCycle++;
            }

            if (this._nodeCollectCycle < this._content.Children.Count)
            {
                this._done = false;
            }

            return count;
        }

#if DEBUG_DUMP
        public void ShowQuadTree(bool show)
        {
            if (show)
            {
                _index.ShowQuadTree(_content);
            }
            else
            {
                RebuildVisuals();
            }
        }

        public void Dump(string fileName)
        {
            using (StreamWriter w = new StreamWriter(fileName))
            {
                using (LogWriter log = new LogWriter(w))
                {
                    log.Open("QuadTree");
                    _index.Dump(log);
                    log.Open("Other");
                    log.WriteAttribute("MaxDepth", log.MaxDepth.ToString(CultureInfo.CurrentUICulture));
                    log.Close();
                    log.Close();
                }
            }
        }
#endif

        /// <summary>
        /// Return the full size of this canvas.
        /// </summary>
        public Size Extent
        {
            get { return this._extent; }
        }

        #region IScrollInfo Members

        /// <summary>
        /// Return whether we are allowed to scroll horizontally.
        /// </summary>
        public bool CanHorizontallyScroll
        {
            get { return this._canHScroll; }
            set { this._canHScroll = value; }
        }

        /// <summary>
        /// Return whether we are allowed to scroll vertically.
        /// </summary>
        public bool CanVerticallyScroll
        {
            get { return this._canVScroll; }
            set { this._canVScroll = value; }
        }

        /// <summary>
        /// The height of the canvas to be scrolled.
        /// </summary>
        public double ExtentHeight
        {
            get { return this._extent.Height * this._scale.ScaleY; }
        }

        /// <summary>
        /// The width of the canvas to be scrolled.
        /// </summary>
        public double ExtentWidth
        {
            get { return this._extent.Width * this._scale.ScaleX; }
        }

        /// <summary>
        /// Scroll down one small scroll increment.
        /// </summary>
        public void LineDown()
        {
            this.SetVerticalOffset(this.VerticalOffset + (this._smallScrollIncrement.Height * this._scale.ScaleX));
        }

        /// <summary>
        /// Scroll left by one small scroll increment.
        /// </summary>
        public void LineLeft()
        {
            this.SetHorizontalOffset(this.HorizontalOffset - (this._smallScrollIncrement.Width * this._scale.ScaleX));
        }

        /// <summary>
        /// Scroll right by one small scroll increment
        /// </summary>
        public void LineRight()
        {
            this.SetHorizontalOffset(this.HorizontalOffset + (this._smallScrollIncrement.Width * this._scale.ScaleX));
        }

        /// <summary>
        /// Scroll up by one small scroll increment
        /// </summary>
        public void LineUp()
        {
            this.SetVerticalOffset(this.VerticalOffset - (this._smallScrollIncrement.Height * this._scale.ScaleX));
        }

        /// <summary>
        /// Make the given visual at the given bounds visible.
        /// </summary>
        /// <param name="visual">The visual that will become visible</param>
        /// <param name="rectangle">The bounds of that visual</param>
        /// <returns>The bounds that is actually visible.</returns>
        public Rect MakeVisible(System.Windows.Media.Visual visual, Rect rectangle)
        {
            if (this._zoom != null && visual != this)
            {
                return this._zoom.ScrollIntoView(visual as FrameworkElement);
            }
            return rectangle;
        }

        /// <summary>
        /// Scroll down by one mouse wheel increment.
        /// </summary>
        public void MouseWheelDown()
        {
            this.SetVerticalOffset(this.VerticalOffset + (this._smallScrollIncrement.Height * this._scale.ScaleX));
        }

        /// <summary>
        /// Scroll left by one mouse wheel increment.
        /// </summary>
        public void MouseWheelLeft()
        {
            this.SetHorizontalOffset(this.HorizontalOffset + (this._smallScrollIncrement.Width * this._scale.ScaleX));
        }

        /// <summary>
        /// Scroll right by one mouse wheel increment.
        /// </summary>
        public void MouseWheelRight()
        {
            this.SetHorizontalOffset(this.HorizontalOffset - (this._smallScrollIncrement.Width * this._scale.ScaleX));
        }

        /// <summary>
        /// Scroll up by one mouse wheel increment.
        /// </summary>
        public void MouseWheelUp()
        {
            this.SetVerticalOffset(this.VerticalOffset - (this._smallScrollIncrement.Height * this._scale.ScaleX));
        }

        /// <summary>
        /// Page down by one view port height amount.
        /// </summary>
        public void PageDown()
        {
            this.SetVerticalOffset(this.VerticalOffset + this._viewPortSize.Height);
        }

        /// <summary>
        /// Page left by one view port width amount.
        /// </summary>
        public void PageLeft()
        {
            this.SetHorizontalOffset(this.HorizontalOffset - this._viewPortSize.Width);
        }

        /// <summary>
        /// Page right by one view port width amount.
        /// </summary>
        public void PageRight()
        {
            this.SetHorizontalOffset(this.HorizontalOffset + this._viewPortSize.Width);
        }

        /// <summary>
        /// Page up by one view port height amount.
        /// </summary>
        public void PageUp()
        {
            this.SetVerticalOffset(this.VerticalOffset - this._viewPortSize.Height);
        }

        /// <summary>
        /// Return the ScrollViewer that contains this object.
        /// </summary>
        public ScrollViewer ScrollOwner
        {
            get { return this._owner; }
            set { this._owner = value; }
        }

        /// <summary>
        /// Scroll to the given absolute horizontal scroll position.
        /// </summary>
        /// <param name="offset">The horizontal position to scroll to</param>
        public void SetHorizontalOffset(double offset)
        {
            double xoffset = Math.Max(Math.Min(offset, this.ExtentWidth - this.ViewportWidth), 0);
            this._translate.X = -xoffset;
            this.OnScrollChanged();
        }

        /// <summary>
        /// Scroll to the given absolute vertical scroll position.
        /// </summary>
        /// <param name="offset">The vertical position to scroll to</param>
        public void SetVerticalOffset(double offset)
        {
            double yoffset = Math.Max(Math.Min(offset, this.ExtentHeight - this.ViewportHeight), 0);
            this._translate.Y = -yoffset;
            this.OnScrollChanged();
        }

        /// <summary>
        /// Get the current horizontal scroll position.
        /// </summary>
        public double HorizontalOffset
        {
            get { return -this._translate.X; }
        }

        /// <summary>
        /// Return the current vertical scroll position.
        /// </summary>
        public double VerticalOffset
        {
            get { return -this._translate.Y; }
        }

        /// <summary>
        /// Return the height of the current viewport that is visible in the ScrollViewer.
        /// </summary>
        public double ViewportHeight
        {
            get { return this._viewPortSize.Height; }
        }

        /// <summary>
        /// Return the width of the current viewport that is visible in the ScrollViewer.
        /// </summary>
        public double ViewportWidth
        {
            get { return this._viewPortSize.Width; }
        }

        #endregion

        /// <summary>
        /// Get the currently visible rectangle according to current scroll position and zoom factor and
        /// size of scroller viewport.
        /// </summary>
        /// <returns>A rectangle</returns>
        Rect GetVisibleRect()
        {
            // Add a bit of extra around the edges so we are sure to create nodes that have a tiny bit showing.
            double xstart = (this.HorizontalOffset - this._smallScrollIncrement.Width) / this._scale.ScaleX;
            double ystart = (this.VerticalOffset - this._smallScrollIncrement.Height) / this._scale.ScaleY;
            double xend = (this.HorizontalOffset + (this._viewPortSize.Width + (2 * this._smallScrollIncrement.Width))) / this._scale.ScaleX;
            double yend = (this.VerticalOffset + (this._viewPortSize.Height + (2 * this._smallScrollIncrement.Height))) / this._scale.ScaleY;
            return new Rect(xstart, ystart, xend - xstart, yend - ystart);
        }

        /// <summary>
        /// The visible region has changed, so we need to queue up work for dirty regions and new visible regions
        /// then start asynchronously building new visuals via StartLazyUpdate.
        /// </summary>
        void OnScrollChanged()
        {
            Rect dirty = this._visible;
            this.AddVisibleRegion();
            this._nodeCollectCycle = 0;
            this._done = false;

            Rect intersection = Rect.Intersect(dirty, this._visible);
            if (intersection == Rect.Empty)
            {
                this._dirtyRegions.Add(dirty); // the whole thing is dirty
            }
            else
            {
                // Add left stripe
                if (dirty.Left < intersection.Left)
                {
                    this._dirtyRegions.Add(new Rect(dirty.Left, dirty.Top, intersection.Left - dirty.Left, dirty.Height));
                }
                // Add right stripe
                if (dirty.Right > intersection.Right)
                {
                    this._dirtyRegions.Add(new Rect(intersection.Right, dirty.Top, dirty.Right - intersection.Right, dirty.Height));
                }
                // Add top stripe
                if (dirty.Top < intersection.Top)
                {
                    this._dirtyRegions.Add(new Rect(dirty.Left, dirty.Top, dirty.Width, intersection.Top - dirty.Top));
                }
                // Add right stripe
                if (dirty.Bottom > intersection.Bottom)
                {
                    this._dirtyRegions.Add(new Rect(dirty.Left, intersection.Bottom, dirty.Width, dirty.Bottom - intersection.Bottom));
                }
            }

            this.StartLazyUpdate();
            this.InvalidateScrollInfo();
        }

        /// <summary>
        /// Tell the ScrollViewer to update the scrollbars because, extent, zoom or translate has changed.
        /// </summary>
        public void InvalidateScrollInfo()
        {
            if (this._owner != null)
            {
                this._owner.InvalidateScrollInfo();
            }
        }

        /// <summary>
        /// Add the current visible rect to the list of regions to process
        /// </summary>
        private void AddVisibleRegion()
        {
            this._visible = this.GetVisibleRect();
            this._visibleRegions.Clear();
            this._visibleRegions.Add(this._visible);
        }


#if DEBUG_QUAD_TREE
        public void ShowQuadTree(bool show)
        {
            if (show)
            {
                _index.ShowQuadTree(_content);
            }
            else
            {
                RebuildVisuals();
            }
        }

        public void Dump(string filename)
        {
            using (StreamWriter w = new StreamWriter(filename))
            {
                using (LogWriter log = new LogWriter(w))
                {
                    log.Open("QuadTree");
                    _index.Dump(log);
                    log.Open("Other");
                    log.WriteAttribute("MaxDepth", log.MaxDepth.ToString());
                    log.Close();
                    log.Close();
                }
            }
        }
#endif
    }

}
