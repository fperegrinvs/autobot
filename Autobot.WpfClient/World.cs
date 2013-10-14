using System.Collections.Generic;

namespace Autobot.WpfClient
{
    public class World
    {
        /// <summary>
        /// World tiles
        /// </summary>
        private Dictionary<ushort, Dictionary<ushort, ITile>> tiles;

        /// <summary>
        /// Get a tile from the world map
        /// </summary>
        /// <param name="x">the x coordinate</param>
        /// <param name="y">the y coordinate</param>
        /// <returns>the tile if it exists or null otherwise</returns>
        public ITile GetTite(ushort x, ushort y)
        {
            if (this.tiles == null)
            {
                return null;
            }

            if (!this.tiles.ContainsKey(x))
            {
                return null;
            }

            if (!this.tiles[x].ContainsKey(y))
            {
                return null;
            }

            return this.tiles[x][y];
        }

        /// <summary>
        /// Insert a tile in the world map
        /// </summary>
        /// <param name="x">the tile x coodinate</param>
        /// <param name="y">the tile y coordinate</param>
        /// <param name="tile">the tile information</param>
        /// <returns>true on success</returns>
        public bool InsertTile(ushort x, ushort y, ITile tile)
        {
            if (this.tiles == null)
            {
                this.tiles = new Dictionary<ushort, Dictionary<ushort, ITile>>();
            }

            if (!this.tiles.ContainsKey(x))
            {
                this.tiles[x] = new Dictionary<ushort, ITile>();
            }

            if (this.tiles[x].ContainsKey(y))
            {
                // item already exists
                return false;
            }

            this.tiles[x][y] = tile;
            return true;
        }
    }
}
