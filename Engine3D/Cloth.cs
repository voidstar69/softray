using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Engine3D
{
    public class Cloth : Model
    {
        private double elapsedTime;

        public Cloth()
        {
            const int gridSize = 10; // size in points per side of the cloth

            Vertices = new List<Vertex>();
            Normals = new List<Vector>();
            Triangles = new List<Triangle>();
            var material = new SalmonViewer.Material { Diffuse = new[] { 1f, 0.5f, 1f } };

            Normals.Add(new Vector(0, 1, 0));
            var normalIndex = 0;

            for(int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    Vertices.Add(new Vertex((double)row / gridSize, 0, (double)col / gridSize));

                    if (row < gridSize - 1 && col < gridSize - 1)
                    {
                        var vertIndex = row * gridSize + col;
                        Triangles.Add(new Triangle(vertIndex, vertIndex + 1, vertIndex + gridSize, normalIndex, normalIndex, normalIndex, material));
                        Triangles.Add(new Triangle(vertIndex + 1, vertIndex + gridSize + 1, vertIndex + gridSize, normalIndex, normalIndex, normalIndex, material));
                    }
                }
            }

            CalcExtent();
            PostProcessGeometry();
            LoadingComplete = true;
            LoadingError = false;

            Logger.Log("Cloth has {0} vertices, {1} normals and {2} triangles", Vertices.Count, Normals.Count, Triangles.Count);
        }

        public void DoDynamics(double deltaTime)
        {
            elapsedTime += deltaTime;

            for (int i = 0; i < Vertices.Count; i++)
            {
                var vert = Vertices[i];
                //vert.pos.y -= deltaTime / 100.0 * i / Vertices.Count;   // droopy corner
                vert.pos.y = Math.Sin((vert.pos.x + vert.pos.z) * 10 + elapsedTime * 2) * 0.1 - 0.0;
                Vertices[i] = vert;
            }
        }
    }
}