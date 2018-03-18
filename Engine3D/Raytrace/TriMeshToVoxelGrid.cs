using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    public class TriMeshToVoxelGrid
    {
        /// <summary>
        /// Convert a list of triangles into a grid of voxels.
        /// </summary>
        /// <param name="triangles">Triangles bounded by the unit cube centred at the origin</param>
        /// <param name="voxelGridSize">The resolution of the voxel grid</param>
        /// <returns>Number of voxels cells that are 'filled in', i.e. non-empty</returns>
        static public int Convert(List<Raytrace.Triangle> triangles, int voxelGridSize, VoxelGrid voxelGrid)
        {
            Contract.Requires(Contract.ForAll(triangles, (t =>
                t.Vertex1.x >= -0.5 && t.Vertex1.x <= 0.5 &&
                t.Vertex2.y >= -0.5 && t.Vertex2.y <= 0.5 &&
                t.Vertex3.z >= -0.5 && t.Vertex3.z <= 0.5)));

            var voxelColors = new uint[voxelGridSize, voxelGridSize, voxelGridSize];
            var voxelNormals = new Vector[voxelGridSize, voxelGridSize, voxelGridSize];
            int totalTriInCellCount = 0;
            int numFilledVoxels = 0;

            for (var x = 0; x < voxelGridSize; x++)
            {
                var x0 = ((double)x / voxelGridSize) - 0.5;
                var x1 = ((double)(x + 1) / voxelGridSize) - 0.5;
                Plane leftPlane = new Plane(new Vector(x0, 0, 0), new Vector(1, 0, 0));
                Plane rightPlane = new Plane(new Vector(x1, 0, 0), new Vector(-1, 0, 0));
                var trisInSlabX = FindTrianglesInsidePlanes(triangles, new List<Plane> { leftPlane, rightPlane });

                for (var y = 0; y < voxelGridSize; y++)
                {
                    var y0 = ((double)y / voxelGridSize) - 0.5;
                    var y1 = ((double)(y + 1) / voxelGridSize) - 0.5;
                    Plane topPlane = new Plane(new Vector(0, y0, 0), new Vector(0, 1, 0));
                    Plane bottomPlane = new Plane(new Vector(0, y1, 0), new Vector(0, -1, 0));
                    var trisInRowXY = FindTrianglesInsidePlanes(trisInSlabX, new List<Plane> { topPlane, bottomPlane /*, leftPlane, rightPlane*/ });

                    for (var z = 0; z < voxelGridSize; z++)
                    {
                        var z0 = ((double)z / voxelGridSize) - 0.5;
                        var z1 = ((double)(z + 1) / voxelGridSize) - 0.5;
                        Plane nearPlane = new Plane(new Vector(0, 0, z0), new Vector(0, 0, 1));
                        Plane farPlane = new Plane(new Vector(0, 0, z1), new Vector(0, 0, -1));
                        var trisInCell = FindTrianglesInsidePlanes(trisInRowXY, new List<Plane> { nearPlane, farPlane /*, topPlane, bottomPlane, leftPlane, rightPlane*/ });

                        // Color based on number of triangles in cell
//                        uint color = (trisInCell.Count == 0 ? 0 : (uint)(trisInCell.Count * 123456789) | 0xff000000);

                        // Fixed color
//                        uint color = (trisInCell.Count == 0 ? 0 : 0xff00ff00);

                        // Pick color of first triangle in cell
//                        uint color = (trisInCell.Count == 0 ? 0 : trisInCell[0].Color);

                        // Average colors of all triangles in cell
                        uint cellColor = 0;
                        if (trisInCell.Count > 0)
                        {
                            var voxelBox = new AxisAlignedBox(new Vector(x0, y0, z0), new Vector(x1, y1, z1));

                            Color color = Color.Black;
                            int count = 0;
                            foreach (var tri in trisInCell)
                            {
                                //if (voxelBox.IntersectsTriangle(tri))
                                {
                                    color += new Color(tri.Color);
                                    count++;
                                }
                            }
                            color /= count; // trisInCell.Count;
                            cellColor = color.ToARGB();

                            numFilledVoxels++;
                            totalTriInCellCount += count; // trisInCell.Count;
                        }

                        voxelColors[x, y, z] = cellColor;

                        // Pick normal of first triangle in cell
                        Vector normal = (trisInCell.Count == 0 ? Vector.Zero : trisInCell[0].Plane.Normal);

                        voxelNormals[x, y, z] = normal;
                    }
                }
            }

            Contract.Assert(totalTriInCellCount >= triangles.Count);

/*
            // Random voxel grid
            var random = new Random(1234567890);
            for (var x = 0; x < voxelGridSize; x++)
            {
                for (var y = 0; y < voxelGridSize; y++)
                {
                    for (var z = 0; z < voxelGridSize; z++)
                    {
                        // pick a random opaque color
                        var color = (random.Next(2) == 0 ? 0 : (uint)random.Next() | 0xff000000);
                        voxels[x, y, z] = color;
                    }
                }
            }
*/

            voxelGrid.SetData(voxelColors, voxelNormals);

            return numFilledVoxels;
        }

        static private List<Triangle> FindTrianglesInsidePlanes(List<Triangle> tris, List<Plane> planes)
        {
            // TODO: a triangle can be 'inside' all planes (each plane has at least one triangle vertex 'inside' the plane),
            // yet the triangle can still not intersect the volume defined by the planes.
            // Do we need to clip the triangle to each plane to figure out if any part of the triangle lies 'inside' all planes?

            var newTris = new List<Raytrace.Triangle>();
            foreach (var tri in tris)
            {
                bool triInside = true;
                foreach (var plane in planes)
                {
                    PlaneHalfSpace side = tri.IntersectPlane(plane);
                    if ((side & PlaneHalfSpace.NormalSide) == 0)
                    {
                        triInside = false;
                        break;
                    }
                }

                if (triInside)
                {
                    newTris.Add(tri);
                }
            }
            return newTris;
        }
    }
}
