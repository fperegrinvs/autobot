namespace Autobot.WpfClient
{
    using System.Collections.Generic;
    using System.Windows;

    /// <summary>
    /// Tile information
    /// </summary>
    public interface ITile
    {
        /// <summary>
        /// Tile boundaries
        /// </summary>
        Rect Bounds { get; set; }

        /// <summary>
        /// Collection of obstacles
        /// </summary>
        IEnumerable<Obstacle> Obstacles { get; }

        /// <summary>
        /// Add an obstacle to the world map
        /// </summary>
        /// <param name="obstacle">obstacle to be added</param>
        void AddObstacle(Obstacle obstacle);
    }

    /// <summary>
    /// Return 
    /// </summary>
    public interface ITileContainer
    {
        /// <summary>
        /// Add a tile to the container
        /// </summary>
        /// <param name="tile">tile to be added</param>
        void AddTile(ITile tile);
    }
}
