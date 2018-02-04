using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Engine3D.Raytrace
{
    public class TriMeshToVoxelGrid
    {
        /// <summary>
        /// Convert a list of triangles into a grid of voxels.
        /// </summary>
        /// <param name="triangles">Triangles bounded by the unit cube centred at the origin</param>
        /// <param name="voxelGridSize">The resolution of the voxel grid</param>
        /// <returns></returns>
        static public void Convert(List<Raytrace.Triangle> triangles, int voxelGridSize, VoxelGrid voxelGrid)
        {
            var voxelColors = new uint[voxelGridSize, voxelGridSize, voxelGridSize];
            var voxelNormals = new Vector[voxelGridSize, voxelGridSize, voxelGridSize];

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
                    var trisInRowXY = FindTrianglesInsidePlanes(trisInSlabX, new List<Plane> { topPlane, bottomPlane });

                    for (var z = 0; z < voxelGridSize; z++)
                    {
                        var z0 = ((double)z / voxelGridSize) - 0.5;
                        var z1 = ((double)(z + 1) / voxelGridSize) - 0.5;
                        Plane nearPlane = new Plane(new Vector(0, 0, z0), new Vector(0, 0, 1));
                        Plane farPlane = new Plane(new Vector(0, 0, z1), new Vector(0, 0, -1));
                        var trisInCell = FindTrianglesInsidePlanes(trisInRowXY, new List<Plane> { nearPlane, farPlane });

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
                            Color color = Color.Black;
                            foreach (var tri in trisInCell)
                            {
                                color += new Color(tri.Color);
                            }
                            color /= trisInCell.Count;
                            cellColor = color.ToARGB();
                        }

                        voxelColors[x, y, z] = cellColor;

                        // Pick normal of first triangle in cell
                        Vector normal = (trisInCell.Count == 0 ? Vector.Zero : trisInCell[0].Plane.Normal);

                        voxelNormals[x, y, z] = normal;
                    }
                }
            }


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
        }

        static private List<Raytrace.Triangle> FindTrianglesInsidePlanes(List<Raytrace.Triangle> tris, List<Plane> planes)
        {
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
