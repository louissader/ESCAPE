using System.Text;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Tiny authoring DSL for LevelDefinition tile grids: start empty, add
    /// floors/platforms/walls/lava, emit the ASCII grid. Coordinates are
    /// tile coords, y up from bottom. Correct-by-construction beats
    /// hand-drawn ASCII for grids this size.
    /// </summary>
    public class LevelGridBuilder
    {
        readonly char[,] grid;
        public readonly int Width;
        public readonly int Height;

        public LevelGridBuilder(int width, int height)
        {
            Width = width;
            Height = height;
            grid = new char[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    grid[x, y] = '.';
        }

        public LevelGridBuilder Floor(int y = 0)
        {
            return Platform(0, y, Width);
        }

        /// <summary>Horizontal solid run starting at (x,y).</summary>
        public LevelGridBuilder Platform(int x, int y, int width)
        {
            for (int i = 0; i < width; i++)
                Set(x + i, y, '#');
            return this;
        }

        /// <summary>Vertical solid run rising from (x,y).</summary>
        public LevelGridBuilder Wall(int x, int y, int height)
        {
            for (int i = 0; i < height; i++)
                Set(x, y + i, '#');
            return this;
        }

        /// <summary>Solid rectangle.</summary>
        public LevelGridBuilder Block(int x, int y, int width, int height)
        {
            for (int i = 0; i < width; i++)
                for (int j = 0; j < height; j++)
                    Set(x + i, y + j, '#');
            return this;
        }

        /// <summary>Carve a hole (e.g. a doorway through a wall).</summary>
        public LevelGridBuilder Clear(int x, int y, int width, int height)
        {
            for (int i = 0; i < width; i++)
                for (int j = 0; j < height; j++)
                    Set(x + i, y + j, '.');
            return this;
        }

        /// <summary>Lava pool sitting on top of whatever is below.</summary>
        public LevelGridBuilder Lava(int x, int y, int width)
        {
            for (int i = 0; i < width; i++)
                Set(x + i, y, 'L');
            return this;
        }

        /// <summary>Staircase going up-right (or up-left with dir=-1), one tile per step.</summary>
        public LevelGridBuilder Stairs(int x, int y, int steps, int dir = 1)
        {
            for (int i = 0; i < steps; i++)
                Platform(x + i * 2 * dir, y + i, 2);
            return this;
        }

        void Set(int x, int y, char c)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
                grid[x, y] = c;
        }

        public string Emit()
        {
            var sb = new StringBuilder();
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                    sb.Append(grid[x, y]);
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
