// Title:	Entity.cs
// Author: 	Scott Ellington <scott.ellington@gmail.com>
//
// Copyright (C) 2006 Scott Ellington and authors
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;

namespace SalmonViewer
{
	public struct TexCoord 
	{
		public float U;
		public float V;

		public TexCoord ( float u , float v )
		{
			U = u;
			V = v;
		}
	}
	
	public class Entity
	{
		// TODO: OO this
		// fields should be private
		// constructor with verts and faces
		// normalize in ctor
		
		public Material material = new Material();
		
		// The stored vertices 
		public Vector[] vertices;

		// The calculated normals
		public Vector[] normals;
		
		// The indices of the triangles which point to vertices
		public Triangle[] triangles;

//		public Quad[] quads;
		
		// The coordinates which map the texture onto the entity
		public TexCoord[] texcoords;
	
//		bool normalized = false;
		
		/// <summary>
		/// Calculates the surface normals for all vertices.
		/// A surface normal is a vector perpendicular to the tangent plane to that surface.
		/// http://en.wikipedia.org/wiki/Surface_normal
		/// </summary>
		public void CalculateNormals ()
		{
			if ( triangles == null ) return;
			
			// a normal is created for each vertex
			normals = new Vector [vertices.Length];

			// first let's create a surface normal for each triangle
			Vector[] triNormals = new Vector [ triangles.Length ];
			for ( int ii=0 ; ii < triangles.Length ; ii++ )
			{
				Triangle tr = triangles [ii];
				
				Vector v1 = vertices [ tr.Vertex1 ] - vertices  [ tr.Vertex2 ];
				Vector v2 = vertices [ tr.Vertex2 ] - vertices  [ tr.Vertex3 ];

				triNormals [ii] = v1.CrossProduct ( v2 );
			}
			
			// merge the triangle's normals to form each vertex's normal				
			// we'll do this by looping through all of the triangles
			for ( int jj = 0; jj < triangles.Length ; jj++ )
			{
				Triangle tr = triangles [jj];

                // add the triangle normal to each vertex normal
                // which has the effect of combining the magnitude and direction of all triangle's normal,
                // causing a smoothing effect on the entity
                normals[tr.Vertex1] += triNormals[jj];
                normals[tr.Vertex2] += triNormals[jj];
                normals[tr.Vertex3] += triNormals[jj];
            }

            for ( int ii = 0; ii < vertices.Length ; ii++ )
            {
				// finally normalize the vertex normal
                normals[ii] = normals[ii].Normalize();
			}

//			normalized = true;
		}
	}
}
