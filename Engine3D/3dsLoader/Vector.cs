// Title:	Vector.cs
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
	/// <summary>
	/// A spatial vector, or simply vector, is a geometric object that has both a magnitude and a direction. 
	/// In three dimensional Euclidean space (or R3), 
	/// vectors are identified with triples of numbers corresponding to the Cartesian coordinates of the endpoint (a,b,c)
	/// http://en.wikipedia.org/wiki/Vector_(spatial)
	/// </summary>
	public struct Vector
	{
		
#region Vars
		
		public double X;
		public double Y;
		public double Z;

#endregion
		
#region ctors		
		
		public Vector ( double x, double y, double z )
		{
			X = x;
			Y = y;
			Z = z;
		}

#endregion

#region Public Methods
		
		/// <summary>
		/// Returns a vector which is perpendicular to the two vectors
		/// </summary>
		/// <param name="v">
		/// A <see cref="Vector"/>
		/// </param>
		/// <returns>
		/// A <see cref="Vector"/>
		/// </returns>
		public Vector CrossProduct ( Vector v )
		{
			return new Vector (  Y * v.Z - Z * v.Y,
					Z * v.X - X * v.Z,
					X * v.Y - Y * v.X );
		}

		/// <summary>
		/// returns a scalar quantity
		/// </summary>
		/// <param name="v">
		/// A <see cref="Vector"/>
		/// </param>
		/// <returns>
		/// A <see cref="System.Double"/>
		/// </returns>
		public double DotProduct ( Vector v )
		{ 
			return X*v.X + Y*v.Y + Z*v.Z; 
		}

		/// <summary>
		/// Creates a unit vector.  Which is a vector with a length of one; 
		/// geometrically, it indicates a direction but no magnitude. 
		/// http://en.wikipedia.org/wiki/Vector_(spatial)
		/// </summary>
		/// <returns>
		/// A <see cref="Vector"/>
		/// </returns>
		public Vector Normalize ()
		{
			double d = Length();
			
			if (d == 0) d = 1;
			
			return this / d;
		}
		
		/// <summary>
		/// Returns the length of the vector
		/// </summary>
		/// <returns>
		/// A <see cref="System.Double"/>
		/// </returns>
		public double Length()
		{
			return Math.Sqrt(DotProduct(this));
		}
		
#endregion
		
		public override string ToString ()
		{
			return String.Format ( "X: {0} Y: {1} Z: {2}", X, Y, Z );
		}

#region Operator overloads
		
		public static Vector operator + ( Vector v1, Vector v2 )
		{
			Vector vr;

			vr.X = v1.X + v2.X;
			vr.Y = v1.Y + v2.Y;
			vr.Z = v1.Z + v2.Z;

			return vr;
		}

		public static Vector operator / ( Vector v1, double s )
		{
			Vector vr;

			vr.X = v1.X / s;
			vr.Y = v1.Y / s;
			vr.Z = v1.Z / s;

			return vr;
		}

		public static Vector operator - ( Vector v1, Vector v2 )
		{
			Vector vr;

			vr.X = v1.X - v2.X;
			vr.Y = v1.Y - v2.Y;
			vr.Z = v1.Z - v2.Z;

			return vr;
		}
		
#endregion
	}
}
