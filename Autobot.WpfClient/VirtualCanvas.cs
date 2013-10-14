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
        ScrollViewer owner;
        Size viewPortSize;
        bool canHScroll;
        bool canVScroll;
        QuadTree<IVirtualChild> index;
        ObservableCollection<IVirtualChild> children;
        Size smallScrollIncrement = new Size(10, 10);

        readonly Canvas content;

        readonly Border backdrop;

        readonly TranslateTransform translate;

        readonly ScaleTransform scale;
        Size extent;

        readonly IList<Rect> dirtyRegions = new List<Rect>();

        readonly IList<Rect> visibleRegions = new List<Rect>();
        IDictionary<IVirtualChild, int> visualPositions;
        int nodeCollectCycle;
        bool done = true;
        MapZoom zoom;

        public static DependencyProperty VirtualChildProperty = DependencyProperty.Register("VirtualChild", typeof(IVirtualChild), typeof(VirtualCanvas));

        public event EventHandler<VisualChangeEventArgs> VisualsChanged;

        /// <summary>
        /// Construct empty virtual canvas.
        /// </summary>
        public VirtualCanvas()
        {
            this.index = new QuadTree<IVirtualChild>();
            this.children = new ObservableCollection<IVirtualChild>();
            this.children.CollectionChanged += this.OnChildrenCollectionChanged;
            this.content = new Canvas();
            this.backdrop = new Border();
            this.content.Children.Add(this.backdrop);

            var g = new TransformGroup();
            this.scale = new ScaleTransform();
            this.translate = new TranslateTransform();
            g.Children.Add(this.scale);
            g.Children.Add(this.translate);
            this.content.RenderTransform = g;

            this.translate.Changed += this.OnTranslateChanged;
            this.scale.Changed += this.OnScaleChanged;
            this.Children.Add(this.content);
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
            get { return this.zoom; }
            set { this.zoom = value; }
        }

        /// <summary>
        /// Returns true if all Visuals have been created for the current scroll position
        /// and there is no more idle processing needed.
        /// </summary>
        public bool IsDone
        {
            get { return this.done; }
        }

        /// <summary>
        /// Resets the state so there is no Visuals associated with this canvas.
        /// </summary>
        private void RebuildVisuals()
        {
            // need to rebuild the index.
            this.index = null;
            this.visualPositions = null;
            this.visible = Rect.Empty;
            this.done = false;
            foreach (UIElement e in this.content.Children)
            {
                IVirtualChild n = e.GetValue(VirtualChildProperty) as IVirtualChild;
                if (n != null)
                {
                    e.ClearValue(VirtualChildProperty);
                    n.DisposeVisual();
                }
            }
            this.content.Children.Clear();
            this.content.Children.Add(this.backdrop);
            this.InvalidateArrange();
            this.StartLazyUpdate();
        }

        /// <summary>
        /// The current zoom transform.
        /// </summary>
        public ScaleTransform Scale
        {
            get { return this.scale; }
        }

        /// <summary>
        /// The current translate transform.
        /// </summary>
        public TranslateTransform Translate
        {
            get { return this.translate; }
        }

        /// <summary>
        /// Get/Set the IVirtualChild collection.  The VirtualCanvas will call CreateVisual on them
        /// when the Bounds of your child intersects the current visible view port.
        /// </summary>
        public ObservableCollection<IVirtualChild> VirtualChildren
        {
            get { return this.children; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }
                if (this.children != null)
                {
                    this.children.CollectionChanged -= this.OnChildrenCollectionChanged;
                }
                this.children = value;
                this.children.CollectionChanged += this.OnChildrenCollectionChanged;
                this.RebuildVisuals();
            }
        }

        /// <summary>
        /// Set the scroll amount for the scroll bar arrows.
        /// </summary>
        public Size SmallScrollIncrement
        {
            get { return this.smallScrollIncrement; }
            set { this.smallScrollIncrement = value; }
        }

        /// <summary>
        /// Add a new IVirtualChild.  The VirtualCanvas will call CreateVisual on them
        /// when the Bounds of your child intersects the current visible view port.
        /// </summary>
        public void AddVirtualChild(IVirtualChild child)
        {
            this.children.Add(child);
        }

        /// <summary>
        /// Return the list of virtual children that intersect the given bounds.
        /// </summary>
        /// <param name="bounds">The bounds to test</param>
        /// <returns>The list of virtual children found or null if there are none</returns>
        public IEnumerable<IVirtualChild> GetChildrenIntersecting(Rect bounds)
        {
            if (this.index != null)
            {
                return this.index.GetNodesInside(bounds);
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
            if (this.index != null)
            {
                return this.index.HasNodesInside(bounds);
            }
            return false;
        }

        /// <summary>
        /// The number of visual children that are visible right now.
        /// </summary>
        public int LiveVisualCount
        {
            get { return this.content.Children.Count - 1; }
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
            get { return this.content; }
        }

        /// <summary>
        /// The backgrop is the back most child of the ContentCanvas used for drawing any sort
        /// of background that is guarenteed to fill the ViewPort.
        /// </summary>
        public Border Backdrop
        {
            get { return this.backdrop; }
        }

        /// <summary>
        /// Calculate the size needed to display all the virtual children.
        /// </summary>
        void CalculateExtent()
        {
            bool rebuild = false;
            // ReSharper disable CompareOfFloatsByEqualityOperator
            if (this.index == null || this.extent.Width == 0 || this.extent.Height == 0 ||
                // ReSharper restore CompareOfFloatsByEqualityOperator
                double.IsNaN(this.extent.Width) || double.IsNaN(this.extent.Height))
            {
                rebuild = true;
                bool first = true;
                var extentRect = new Rect();
                this.visualPositions = new Dictionary<IVirtualChild, int>();
                int i = 0;
                foreach (IVirtualChild c in this.children)
                {
                    this.visualPositions[c] = i++;

                    Rect childBounds = c.Bounds;
                    // ReSharper disable CompareOfFloatsByEqualityOperator
                    if (childBounds.Width != 0 && childBounds.Height != 0)
                        // ReSharper restore CompareOfFloatsByEqualityOperator
                    {
                        if (double.IsNaN(childBounds.Width) || double.IsNaN(childBounds.Height))
                        {
                            throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentUICulture,
                                "Child type '{0}' returned NaN bounds", c.GetType().Name));
                        }
                        if (first)
                        {
                            extentRect = childBounds;
                            first = false;
                        }
                        else
                        {
                            extentRect = Rect.Union(extentRect, childBounds);
                        }
                    }
                }
                this.extent = extentRect.Size;
                // Ok, now we know the size we can create the index.
                this.index = new QuadTree<IVirtualChild>();
                this.index.Bounds = new Rect(0, 0, extentRect.Width, extentRect.Height);
                foreach (IVirtualChild n in this.children)
                {
                    if (n.Bounds.Width > 0 && n.Bounds.Height > 0)
                    {
                        this.index.Insert(n, n.Bounds);
                    }
                }
            }

            // Make sure we honor the min width & height.
            double w = Math.Max(this.content.MinWidth, this.extent.Width);
            double h = Math.Max(this.content.MinHeight, this.extent.Height);
            this.content.Width = w;
            this.content.Height = h;

            // Make sure the backdrop covers the ViewPort bounds.
            double scaleX = this.scale.ScaleX;
            if (!double.IsInfinity(this.ViewportHeight) &&
                !double.IsInfinity(this.ViewportHeight))
            {
                w = Math.Max(w, this.ViewportWidth / scaleX);
                h = Math.Max(h, this.ViewportHeight / scaleX);
                this.backdrop.Width = w;
                this.backdrop.Height = h;
            }

            if (this.owner != null)
            {
                this.owner.InvalidateScrollInfo();
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

            if (availableSize != this.viewPortSize)
            {
                this.SetViewportSize(availableSize);
            }

            foreach (UIElement child in this.InternalChildren)
            {
                var n = child.GetValue(VirtualChildProperty) as IVirtualChild;
                if (n != null)
                {
                    Rect bounds = n.Bounds;
                    child.Measure(bounds.Size);
                }
            }
            if (double.IsInfinity(availableSize.Width))
            {
                return this.extent;
            }

            return availableSize;
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

            if (finalSize != this.viewPortSize)
            {
                this.SetViewportSize(finalSize);
            }

            this.content.Arrange(new Rect(0, 0, this.content.Width, this.content.Height));

            if (this.index == null)
            {
                this.StartLazyUpdate();
            }

            return finalSize;
        }

        DispatcherTimer timer;

        /// <summary>
        /// Begin a timer for lazily creating IVirtualChildren visuals
        /// </summary>
        void StartLazyUpdate()
        {
            if (this.timer == null)
            {
                this.timer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Normal,
                    this.OnStartLazyUpdate, this.Dispatcher);
            }
            this.timer.Start();
        }

        /// <summary>
        /// Callback from the DispatchTimer
        /// </summary>
        /// <param name="sender">DispatchTimer </param>
        /// <param name="args">noop</param>
        void OnStartLazyUpdate(object sender, EventArgs args)
        {
            this.timer.Stop();
            this.LazyUpdateVisuals();
        }

        /// <summary>
        /// Set the viewport size and raize a scroll changed event.
        /// </summary>
        /// <param name="s">The new size</param>
        void SetViewportSize(Size s)
        {
            if (s != this.viewPortSize)
            {
                this.viewPortSize = s;
                this.OnScrollChanged();
            }
        }

        int createQuanta = 1000;
        int removeQuanta = 2000;
        int gcQuanta = 5000;

        private const int IdealDuration = 50; // 50 milliseconds.

        int added;
        int removed;
        Rect visible = Rect.Empty;
        delegate int QuantizedWorkHandler(int quantum);

        /// <summary>
        /// Do a quantized unit of work for creating newly visible visuals, and cleaning up visuals that are no
        /// longer needed.
        /// </summary>
        void LazyUpdateVisuals()
        {
            if (this.index == null)
            {
                this.CalculateExtent();
            }

            this.done = true;
            this.added = 0;
            this.removed = 0;

            this.createQuanta = SelfThrottlingWorker(this.createQuanta, IdealDuration, this.LazyCreateNodes);
            this.removeQuanta = SelfThrottlingWorker(this.removeQuanta, IdealDuration, this.LazyRemoveNodes);
            this.gcQuanta = SelfThrottlingWorker(this.gcQuanta, IdealDuration, this.LazyGarbageCollectNodes);

            if (VisualsChanged != null)
            {
                VisualsChanged(this, new VisualChangeEventArgs(this.added, this.removed));
            }
            if (this.added > 0)
            {
                this.InvalidateArrange();
            }
            if (!this.done)
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

            if (this.visible == Rect.Empty)
            {
                this.visible = this.GetVisibleRect();
                this.visibleRegions.Add(this.visible);
                this.done = false;
            }

            int count = 0;
            int regionCount = 0;
            while (this.visibleRegions.Count > 0 && count < quantum)
            {
                Rect r = this.visibleRegions[0];
                this.visibleRegions.RemoveAt(0);
                regionCount++;

                // Iterate over the visible range of nodes and make sure they have visuals.
                foreach (IVirtualChild n in this.index.GetNodesInside(r))
                {
                    if (n.Visual == null)
                    {
                        this.EnsureVisual(n);
                        this.added++;
                    }

                    count++;

                    if (count >= quantum)
                    {
                        // This region is too big, so subdivide it into smaller slices.
                        if (regionCount == 1)
                        {
                            // We didn't even complete 1 region, so we better split it.
                            this.SplitRegion(r, this.visibleRegions);
                        }
                        else
                        {
                            this.visibleRegions.Add(r); // put it back since we're not done!
                        }
                        this.done = false;
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
            int position = this.visualPositions[child];

            // Now do a binary search for the correct insertion position based
            // on the visual positions of the existing visible children.
            UIElementCollection c = this.content.Children;
            int min = 0;
            int max = c.Count - 1;
            while (max > min + 1)
            {
                int i = (min + max) / 2;
                UIElement v = this.content.Children[i];
                IVirtualChild n = v.GetValue(VirtualChildProperty) as IVirtualChild;
                if (n != null)
                {
                    int visualPosition = this.visualPositions[n];
                    if (visualPosition > position)
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
                var maxchild = v.GetValue(VirtualChildProperty) as IVirtualChild;
                if (maxchild == null || position > this.visualPositions[maxchild])
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
            Rect visibleRect = this.GetVisibleRect();
            int count = 0;

            // Also remove nodes that are no longer visible.
            int regionCount = 0;
            while (this.dirtyRegions.Count > 0 && count < quantum)
            {
                int last = this.dirtyRegions.Count - 1;
                Rect dirty = this.dirtyRegions[last];
                this.dirtyRegions.RemoveAt(last);
                regionCount++;

                // Iterate over the visible range of nodes and make sure they have visuals.
                foreach (IVirtualChild n in this.index.GetNodesInside(dirty))
                {
                    UIElement e = n.Visual;
                    if (e != null)
                    {
                        Rect nrect = n.Bounds;
                        if (!nrect.IntersectsWith(visibleRect))
                        {
                            e.ClearValue(VirtualChildProperty);
                            this.content.Children.Remove(e);
                            n.DisposeVisual();
                            this.removed++;
                        }
                    }

                    count++;
                    if (count >= quantum)
                    {
                        if (regionCount == 1)
                        {
                            // We didn't even complete 1 region, so we better split it.
                            this.SplitRegion(dirty, this.dirtyRegions);
                        }
                        else
                        {
                            this.dirtyRegions.Add(dirty); // put it back since we're not done!
                        }
                        this.done = false;
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
            while (count < quantum && this.nodeCollectCycle < this.content.Children.Count)
            {
                UIElement e = this.content.Children[this.nodeCollectCycle++];
                IVirtualChild n = e.GetValue(VirtualChildProperty) as IVirtualChild;
                if (n != null)
                {
                    Rect nrect = n.Bounds;
                    if (!nrect.IntersectsWith(this.visible))
                    {
                        e.ClearValue(VirtualChildProperty);
                        this.content.Children.Remove(e);
                        n.DisposeVisual();
                        this.removed++;
                    }
                    count++;
                }
                this.nodeCollectCycle++;
            }

            if (this.nodeCollectCycle < this.content.Children.Count)
            {
                this.done = false;
            }

            return count;
        }

        /// <summary>
        /// Return the full size of this canvas.
        /// </summary>
        public Size Extent
        {
            get { return this.extent; }
        }

        #region IScrollInfo Members

        /// <summary>
        /// Return whether we are allowed to scroll horizontally.
        /// </summary>
        public bool CanHorizontallyScroll
        {
            get { return this.canHScroll; }
            set { this.canHScroll = value; }
        }

        /// <summary>
        /// Return whether we are allowed to scroll vertically.
        /// </summary>
        public bool CanVerticallyScroll
        {
            get { return this.canVScroll; }
            set { this.canVScroll = value; }
        }

        /// <summary>
        /// The height of the canvas to be scrolled.
        /// </summary>
        public double ExtentHeight
        {
            get { return this.extent.Height * this.scale.ScaleY; }
        }

        /// <summary>
        /// The width of the canvas to be scrolled.
        /// </summary>
        public double ExtentWidth
        {
            get { return this.extent.Width * this.scale.ScaleX; }
        }

        /// <summary>
        /// Scroll down one small scroll increment.
        /// </summary>
        public void LineDown()
        {
            this.SetVerticalOffset(this.VerticalOffset + (this.smallScrollIncrement.Height * this.scale.ScaleX));
        }

        /// <summary>
        /// Scroll left by one small scroll increment.
        /// </summary>
        public void LineLeft()
        {
            this.SetHorizontalOffset(this.HorizontalOffset - (this.smallScrollIncrement.Width * this.scale.ScaleX));
        }

        /// <summary>
        /// Scroll right by one small scroll increment
        /// </summary>
        public void LineRight()
        {
            this.SetHorizontalOffset(this.HorizontalOffset + (this.smallScrollIncrement.Width * this.scale.ScaleX));
        }

        /// <summary>
        /// Scroll up by one small scroll increment
        /// </summary>
        public void LineUp()
        {
            this.SetVerticalOffset(this.VerticalOffset - (this.smallScrollIncrement.Height * this.scale.ScaleX));
        }

        /// <summary>
        /// Make the given visual at the given bounds visible.
        /// </summary>
        /// <param name="visual">The visual that will become visible</param>
        /// <param name="rectangle">The bounds of that visual</param>
        /// <returns>The bounds that is actually visible.</returns>
        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            if (this.zoom != null && visual != this)
            {
                return this.zoom.ScrollIntoView(visual as FrameworkElement);
            }
            return rectangle;
        }

        /// <summary>
        /// Scroll down by one mouse wheel increment.
        /// </summary>
        public void MouseWheelDown()
        {
            this.SetVerticalOffset(this.VerticalOffset + (this.smallScrollIncrement.Height * this.scale.ScaleX));
        }

        /// <summary>
        /// Scroll left by one mouse wheel increment.
        /// </summary>
        public void MouseWheelLeft()
        {
            this.SetHorizontalOffset(this.HorizontalOffset + (this.smallScrollIncrement.Width * this.scale.ScaleX));
        }

        /// <summary>
        /// Scroll right by one mouse wheel increment.
        /// </summary>
        public void MouseWheelRight()
        {
            this.SetHorizontalOffset(this.HorizontalOffset - (this.smallScrollIncrement.Width * this.scale.ScaleX));
        }

        /// <summary>
        /// Scroll up by one mouse wheel increment.
        /// </summary>
        public void MouseWheelUp()
        {
            this.SetVerticalOffset(this.VerticalOffset - (this.smallScrollIncrement.Height * this.scale.ScaleX));
        }

        /// <summary>
        /// Page down by one view port height amount.
        /// </summary>
        public void PageDown()
        {
            this.SetVerticalOffset(this.VerticalOffset + this.viewPortSize.Height);
        }

        /// <summary>
        /// Page left by one view port width amount.
        /// </summary>
        public void PageLeft()
        {
            this.SetHorizontalOffset(this.HorizontalOffset - this.viewPortSize.Width);
        }

        /// <summary>
        /// Page right by one view port width amount.
        /// </summary>
        public void PageRight()
        {
            this.SetHorizontalOffset(this.HorizontalOffset + this.viewPortSize.Width);
        }

        /// <summary>
        /// Page up by one view port height amount.
        /// </summary>
        public void PageUp()
        {
            this.SetVerticalOffset(this.VerticalOffset - this.viewPortSize.Height);
        }

        /// <summary>
        /// Return the ScrollViewer that contains this object.
        /// </summary>
        public ScrollViewer ScrollOwner
        {
            get { return this.owner; }
            set { this.owner = value; }
        }

        /// <summary>
        /// Scroll to the given absolute horizontal scroll position.
        /// </summary>
        /// <param name="offset">The horizontal position to scroll to</param>
        public void SetHorizontalOffset(double offset)
        {
            double xoffset = Math.Max(Math.Min(offset, this.ExtentWidth - this.ViewportWidth), 0);
            this.translate.X = -xoffset;
            this.OnScrollChanged();
        }

        /// <summary>
        /// Scroll to the given absolute vertical scroll position.
        /// </summary>
        /// <param name="offset">The vertical position to scroll to</param>
        public void SetVerticalOffset(double offset)
        {
            double yoffset = Math.Max(Math.Min(offset, this.ExtentHeight - this.ViewportHeight), 0);
            this.translate.Y = -yoffset;
            this.OnScrollChanged();
        }

        /// <summary>
        /// Get the current horizontal scroll position.
        /// </summary>
        public double HorizontalOffset
        {
            get { return -this.translate.X; }
        }

        /// <summary>
        /// Return the current vertical scroll position.
        /// </summary>
        public double VerticalOffset
        {
            get { return -this.translate.Y; }
        }

        /// <summary>
        /// Return the height of the current viewport that is visible in the ScrollViewer.
        /// </summary>
        public double ViewportHeight
        {
            get { return this.viewPortSize.Height; }
        }

        /// <summary>
        /// Return the width of the current viewport that is visible in the ScrollViewer.
        /// </summary>
        public double ViewportWidth
        {
            get { return this.viewPortSize.Width; }
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
            double xstart = (this.HorizontalOffset - this.smallScrollIncrement.Width) / this.scale.ScaleX;
            double ystart = (this.VerticalOffset - this.smallScrollIncrement.Height) / this.scale.ScaleY;
            double xend = (this.HorizontalOffset + (this.viewPortSize.Width + (2 * this.smallScrollIncrement.Width))) / this.scale.ScaleX;
            double yend = (this.VerticalOffset + (this.viewPortSize.Height + (2 * this.smallScrollIncrement.Height))) / this.scale.ScaleY;
            return new Rect(xstart, ystart, xend - xstart, yend - ystart);
        }

        /// <summary>
        /// The visible region has changed, so we need to queue up work for dirty regions and new visible regions
        /// then start asynchronously building new visuals via StartLazyUpdate.
        /// </summary>
        void OnScrollChanged()
        {
            Rect dirty = this.visible;
            this.AddVisibleRegion();
            this.nodeCollectCycle = 0;
            this.done = false;

            Rect intersection = Rect.Intersect(dirty, this.visible);
            if (intersection == Rect.Empty)
            {
                this.dirtyRegions.Add(dirty); // the whole thing is dirty
            }
            else
            {
                // Add left stripe
                if (dirty.Left < intersection.Left)
                {
                    this.dirtyRegions.Add(new Rect(dirty.Left, dirty.Top, intersection.Left - dirty.Left, dirty.Height));
                }
                // Add right stripe
                if (dirty.Right > intersection.Right)
                {
                    this.dirtyRegions.Add(new Rect(intersection.Right, dirty.Top, dirty.Right - intersection.Right, dirty.Height));
                }
                // Add top stripe
                if (dirty.Top < intersection.Top)
                {
                    this.dirtyRegions.Add(new Rect(dirty.Left, dirty.Top, dirty.Width, intersection.Top - dirty.Top));
                }
                // Add right stripe
                if (dirty.Bottom > intersection.Bottom)
                {
                    this.dirtyRegions.Add(new Rect(dirty.Left, intersection.Bottom, dirty.Width, dirty.Bottom - intersection.Bottom));
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
            if (this.owner != null)
            {
                this.owner.InvalidateScrollInfo();
            }
        }

        /// <summary>
        /// Add the current visible rect to the list of regions to process
        /// </summary>
        private void AddVisibleRegion()
        {
            this.visible = this.GetVisibleRect();
            this.visibleRegions.Clear();
            this.visibleRegions.Add(this.visible);
        }
    }
}
