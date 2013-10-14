namespace Autobot.WpfClient
{
    using System;

    /// <summary>
    /// Represent the world
    /// </summary>
    public class WorldShape : VirtualCanvas, ITileContainer
    {
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
