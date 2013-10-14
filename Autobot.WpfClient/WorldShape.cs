namespace Autobot.WpfClient
{
    using System;

    /// <summary>
    /// Represent the world
    /// </summary>
    public class WorldShape : VirtualCanvas, ITileContainer
    {
        public bool ShowObstacles
        {
            get
            {
                return this.showObstacles;
            }
            set
            {
                if (this.showObstacles != value)
                {
                    this.showObstacles = value;
                    this.ChangeObstacleVisibility(value);
                }
            }
        }

        /// <summary>
        /// show obstacles
        /// </summary>
        private bool showObstacles = false;

        /// <summary>
        /// change obstacle visibility
        /// </summary>
        public void ChangeObstacleVisibility(bool visibility)
        {
            foreach (TileShape child in this.Children)
            {
                child.ShowObstacles = visibility;
            }
        }

        /// <summary>
        /// Add a tile to the world
        /// </summary>
        /// <param name="tile">tile to be added</param>
        public void AddTile(ITile tile)
        {
            var graph = tile as IVirtualChild;
            if (graph == null)
            {
                throw new ArgumentException();
            }

            this.AddVirtualChild(graph);
        }
    }
}
