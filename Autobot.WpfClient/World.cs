using System.Collections.Generic;

namespace Autobot.WpfClient
{
    using System;
    using System.Windows;

    public class World<T> where T : class, ITile, new()
    {
        /// <summary>
        /// Create a instance of the bot enviroment
        /// </summary>
        /// <param name="tileContainer">the tile container</param>
        /// <param name="worldSize">world size (in cm)</param>
        /// <param name="tileSize">tile size (in cm)</param>
        public World(ITileContainer tileContainer, Vector worldSize, Vector tileSize)
        {
            this.WorldSize = worldSize;
            this.TileSize = tileSize;
            this.TileContainer = tileContainer;
        }

        /// <summary>
        /// Get a tile that contains the desired world coordinates
        /// </summary>
        /// <param name="x">the x axis coordinate</param>
        /// <param name="y">the y axis coordinate</param>
        /// <param name="creatIfNotExists">create the tile if it does not exist</param>
        /// <returns>the tile or null if not found</returns>
        public T GetTileFromCoordinates(int x, int y, bool creatIfNotExists = false)
        {
            ushort col = Convert.ToUInt16(Math.Ceiling(x / TileSize.X));
            ushort row = Convert.ToUInt16(Math.Ceiling(y / TileSize.Y));

            var tile = this.GetTite(col, row);

            if (!creatIfNotExists || tile != null)
            {
                return tile;
            }

            tile = new T();
            tile.Bounds = new Rect(col * TileSize.X, y * TileSize.Y, TileSize.X, TileSize.Y);
            this.InsertTile(col, row, tile);
            this.TileContainer.AddTile(tile);

            return tile;
        }

        /// <summary>
        /// World tiles
        /// </summary>
        private Dictionary<ushort, Dictionary<ushort, T>> tiles;

        /// <summary>
        /// Size of each tile
        /// </summary>
        private Vector TileSize { get; set; }

        /// <summary>
        /// Size of each tile
        /// </summary>
        private Vector WorldSize { get; set; }

        /// <summary>
        /// Container for the tiles (graphical representation)
        /// </summary>
        private ITileContainer TileContainer { get; set; }

        /// <summary>
        /// Get a tile from the world map
        /// </summary>
        /// <param name="x">the x coordinate</param>
        /// <param name="y">the y coordinate</param>
        /// <returns>the tile if it exists or null otherwise</returns>
        public T GetTite(ushort x, ushort y)
        {
            if (this.tiles == null)
            {
                return default(T);
            }

            if (!this.tiles.ContainsKey(x))
            {
                return default(T);
            }

            if (!this.tiles[x].ContainsKey(y))
            {
                return default(T);
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
        public bool InsertTile(ushort x, ushort y, T tile)
        {
            if (this.tiles == null)
            {
                this.tiles = new Dictionary<ushort, Dictionary<ushort, T>>();
            }

            if (!this.tiles.ContainsKey(x))
            {
                this.tiles[x] = new Dictionary<ushort, T>();
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
