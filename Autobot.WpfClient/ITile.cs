namespace Autobot.WpfClient
{
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
