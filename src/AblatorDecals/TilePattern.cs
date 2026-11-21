using System;
using UnityEngine;

namespace AblatorDecals
{
    public enum TileShape { Hex, Square, Brick }

    // Tile lattice in the decal plane: lattice coordinates (a, b), position a * E1 + b * E2, all rotated by AngleDeg.
    // Each cell is the intersection of its edge half-planes p . Normal <= H (hexagon or square).
    public class TilePattern
    {
        public struct Edge
        {
            public Vector2 Normal;   // unit, rotated
            public float H;          // nominal distance from the cell centre
            public int[] Owners;     // indices into Steps of the neighbours across this edge
        }

        public readonly TileShape Shape;
        public readonly float S, AngleDeg, CellArea, AtlasEdge;
        public readonly Vector2 E1, E2;
        public readonly int[] StepA, StepB;
        public readonly Vector2[] StepVec;   // rotated 2D step to each neighbour
        public readonly Edge[] Edges;
        public readonly int RotationSteps;   // atlas cell symmetry: 6 (hex), 4 (square), 2 (brick)
        private readonly float cos, sin;

        public TilePattern(TileShape shape, float s, float angleDeg)
        {
            Shape = shape;
            S = s;
            AngleDeg = angleDeg;
            cos = Mathf.Cos(angleDeg * Mathf.Deg2Rad);
            sin = Mathf.Sin(angleDeg * Mathf.Deg2Rad);
            const float sqrt3 = 1.7320508f;
            Vector2 e1 = new Vector2(s, 0), e2;
            switch (shape)
            {
                case TileShape.Square:
                    e2 = new Vector2(0, s);
                    StepA = new[] { 1, 0, -1, 0 };
                    StepB = new[] { 0, 1, 0, -1 };
                    RotationSteps = 4;
                    AtlasEdge = s;
                    break;
                case TileShape.Brick:
                    e2 = new Vector2(s / 2, s);
                    StepA = new[] { 1, 0, -1, -1, 0, 1 };
                    StepB = new[] { 0, 1, 1, 0, -1, -1 };
                    RotationSteps = 2;
                    AtlasEdge = s;
                    break;
                default:
                    e2 = new Vector2(s / 2, s * sqrt3 / 2);
                    StepA = new[] { 1, 0, -1, -1, 0, 1 };
                    StepB = new[] { 0, 1, 1, 0, -1, -1 };
                    RotationSteps = 6;
                    AtlasEdge = 2 * s / sqrt3;
                    break;
            }
            E1 = Rotate(e1);
            E2 = Rotate(e2);
            CellArea = Mathf.Abs(e1.x * e2.y - e1.y * e2.x);
            StepVec = new Vector2[StepA.Length];
            for (int k = 0; k < StepA.Length; k++) StepVec[k] = E1 * StepA[k] + E2 * StepB[k];

            if (shape == TileShape.Brick)
                Edges = new[]
                {
                    new Edge { Normal = Rotate(new Vector2(1, 0)), H = s / 2, Owners = new[] { 0 } },
                    new Edge { Normal = Rotate(new Vector2(0, 1)), H = s / 2, Owners = new[] { 1, 2 } },
                    new Edge { Normal = Rotate(new Vector2(-1, 0)), H = s / 2, Owners = new[] { 3 } },
                    new Edge { Normal = Rotate(new Vector2(0, -1)), H = s / 2, Owners = new[] { 4, 5 } },
                };
            else
            {
                Edges = new Edge[StepA.Length];
                for (int k = 0; k < Edges.Length; k++)
                    Edges[k] = new Edge { Normal = StepVec[k].normalized, H = s / 2, Owners = new[] { k } };
            }
        }

        public Vector2 Position(int a, int b) => E1 * a + E2 * b;
        public Vector2 Rotate(Vector2 p) => new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
        public Vector2 Unrotate(Vector2 p) => new Vector2(p.x * cos + p.y * sin, -p.x * sin + p.y * cos);

        // Index of the neighbour step (da, db), -1 if it is no lattice neighbour.
        public int StepIndex(int da, int db)
        {
            for (int k = 0; k < StepA.Length; k++)
                if (StepA[k] == da && StepB[k] == db) return k;
            return -1;
        }
    }
}
