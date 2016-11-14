// Title:	ThreeDSFile.cs
// Author: 	Scott Ellington <scott.ellington@gmail.com>
//
// Copyright (C) 2006-2007 Scott Ellington and authors
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

//#define LOG_CHUNKS
//#define LOG_MATERIAL
//#define LOG_FACES

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

#if LOG_CHUNKS || LOG_MATERIAL || LOG_FACES
using Engine3D;
#endif

namespace SalmonViewer
{
	/// <summary>
	/// 3ds file loader.
	/// Binds materials directly into OpenGL
	/// </summary>
	public class ThreeDSFile
	{
        public class ProgressChangedEventArgs : EventArgs
        {
            private readonly double progressPercentage;

            public ProgressChangedEventArgs(double progressPercentage)
            {
                this.progressPercentage = progressPercentage;
            }

            public double ProgressPercentage
            {
                get
                {
                    return progressPercentage;
                }
            }
        }

        public event EventHandler<ProgressChangedEventArgs> ModelLoadProgressEvent = delegate { };
		
#region Enums		
		
		enum Groups
		{
            C_COLOR_F      = 0x0010,
            C_COLOR_24     = 0x0011,
			C_PRIMARY      = 0x4D4D,
			C_OBJECTINFO   = 0x3D3D, 
			C_VERSION      = 0x0002,
			C_EDITKEYFRAME = 0xB000,          
			C_MATERIAL     = 0xAFFF,    
			C_MATNAME      = 0xA000, 
			C_MATAMBIENT   = 0xA010,
			C_MATDIFFUSE   = 0xA020,
			C_MATSPECULAR  = 0xA030,
			C_MATSHININESS = 0xA040,
			C_MATMAP       = 0xA200,
			C_MATMAPFILE   = 0xA300,
			C_OBJECT       = 0x4000,   
			C_OBJECT_MESH  = 0x4100,
			C_OBJECT_VERTICES    = 0x4110, 
			C_OBJECT_FACES       = 0x4120,
			C_OBJECT_MATERIAL    = 0x4130,
			C_OBJECT_UV		= 0x4140
		}

#endregion		
		
#region Vars

		private readonly Dictionary < string, Material > materials = new Dictionary < string, Material > ();
        public IDictionary<string, Material> Materials
        {
            get
            {
                return materials;
            }
        }
		
//		string base_dir;
		
		BinaryReader reader;

#endregion		
		
        private readonly Model model = new Model();
		public Model Model {
			get {
				return model;
			}
		}
		
		public int Version {
			get {
				return version;
			}
		}
		int version = -1;
		
#region Constructors		
		
		public ThreeDSFile()
		{
        }

        public void LoadModel(Stream stream)
        {
            Contract.Requires(stream != null);

/*
			if (string.IsNullOrEmpty(file_name))
			{
				throw new ArgumentNullException("file_name");
			}
			
			if (!File.Exists(file_name))
			{
				throw new ArgumentException("3ds file could not be found", "file_name");
			}
				
			// 3ds models can use additional files which are expected in the same directory
			base_dir =  new FileInfo ( file_name ).DirectoryName + "/";
*/
		
//			FileStream file = null;
			try
			{
				// create a binary stream from this file
//				file  = new FileStream(file_name, FileMode.Open, FileAccess.Read); 
//				reader = new BinaryReader ( file );
                reader = new BinaryReader(stream);
//				reader.BaseStream.Seek (0, SeekOrigin.Begin); 

				// 3ds files are in chunks
				// read the first one
                ThreeDSChunk chunk = new ThreeDSChunk(this, reader);
#if LOG_CHUNKS
                Logger.Log("ThreeDSFile: first chunk = {0:X}, len = {1}", chunk.ID, chunk.Length);
#endif
				if ( chunk.ID != Groups.C_PRIMARY )
				{
					throw new FormatException ( "Not a proper 3DS file." );
				}
                // Don't skip to the end of this chunk, as it contains the entire file/model.

				// recursively process chunks
				ProcessChunk ( chunk );
			}
			finally
			{
				// close up everything
//				if (reader != null) reader.Close ();
//				if (file != null) file.Close ();
			}
		}

#endregion		
		
#region Helper methods
		
		void ProcessChunk ( ThreeDSChunk chunk )
		{
			// process chunks until there are none left
			while ( chunk.BytesRead < chunk.Length )
			{
				// grab a chunk
                ThreeDSChunk child = new ThreeDSChunk(this, reader);
#if LOG_CHUNKS
                Logger.Log("ProcessChunk: chunk = {0:X}, len = {1}", child.ID, chunk.Length);
#endif

				// process based on ID
				switch (child.ID)
				{
					case Groups.C_VERSION:

						version = reader.ReadInt32 ();
						child.BytesRead += 4;
					
						break;

					case Groups.C_OBJECTINFO:

                        ThreeDSChunk obj_chunk = new ThreeDSChunk(this, reader);

						// not sure whats up with this chunk
						SkipChunk ( obj_chunk );
						child.BytesRead += obj_chunk.BytesRead;

						ProcessChunk ( child );

						break;					

					case Groups.C_MATERIAL:

						ProcessMaterialChunk ( child );

						break;

					case Groups.C_OBJECT:

						// string name = 
						ProcessString ( child );
						
						Entity e = ProcessObjectChunk ( child );
                        if (e.vertices != null && e.triangles != null)
                        {
                            e.CalculateNormals();
                            model.Entities.Add(e);
                        }

						break;

					default:

						SkipChunk ( child );
						break;

				}

				chunk.BytesRead += child.BytesRead;

                // Don't skip to the end of certain chunks, as they contain the entire file/model.
                if (child.ID != Groups.C_VERSION)
                {
                    // Skip to the end of this chunk.
                    child.SkipToEnd(reader);
                }
				//Console.WriteLine ( "ID: {0} Length: {1} Read: {2}", chunk.ID.ToString("x"), chunk.Length , chunk.BytesRead );
			}
		}

		void ProcessMaterialChunk ( ThreeDSChunk chunk )
		{
			string name = string.Empty;
			Material m = new Material ();
			
			while ( chunk.BytesRead < chunk.Length )
			{
                ThreeDSChunk child = new ThreeDSChunk(this, reader);
#if LOG_CHUNKS
                Logger.Log("ProcessMaterialChunk: chunk = {0:X}, len = {1}", child.ID, chunk.Length);
#endif

				switch (child.ID)
				{
					case Groups.C_MATNAME:
						name = ProcessString ( child );
#if LOG_MATERIAL
                        Logger.Log("Material name: {0}", name);
#endif
						break;
				
					case Groups.C_MATAMBIENT:
						m.Ambient = ProcessColorChunk ( child );
#if LOG_MATERIAL
                        Logger.Log("Material ambient color: ({0},{1},{2})", m.Ambient[0], m.Ambient[1], m.Ambient[2]);
#endif
                        break;
						
					case Groups.C_MATDIFFUSE:
						m.Diffuse = ProcessColorChunk ( child );
#if LOG_MATERIAL
                        Logger.Log("Material diffuse color: ({0},{1},{2})", m.Diffuse[0], m.Diffuse[1], m.Diffuse[2]);
#endif
                        break;
						
					case Groups.C_MATSPECULAR:
						m.Specular = ProcessColorChunk ( child );
#if LOG_MATERIAL
                        Logger.Log("Material specular color: ({0},{1},{2})", m.Specular[0], m.Specular[1], m.Specular[2]);
#endif
                        break;
						
					case Groups.C_MATSHININESS:
						m.Shininess = ProcessPercentageChunk ( child );
#if LOG_MATERIAL
                        Logger.Log("Material shininess: {0}", m.Shininess);
#endif
						break;
						
					case Groups.C_MATMAP:
						ProcessPercentageChunk ( child );
						ProcessTexMapChunk ( child , m );
						break;
						
					default:
						SkipChunk ( child );
						break;
				}

				chunk.BytesRead += child.BytesRead;
                child.SkipToEnd(reader);
			}

            // Don't add materials with duplicate names
            if (!materials.ContainsKey(name))
            {
                materials.Add(name, m);
            }
		}

		void ProcessTexMapChunk ( ThreeDSChunk chunk, Material m )
		{
			while ( chunk.BytesRead < chunk.Length )
			{
                ThreeDSChunk child = new ThreeDSChunk(this, reader);
				switch (child.ID)
				{
					case Groups.C_MATMAPFILE:

						string name = ProcessString ( child );
#if LOG_MATERIAL
                        Logger.Log("Material texture map: {0}", name);
#endif

                        //Console.WriteLine ( "	Texture File: {0}", name );

						// use System.Drawing to try and load this image

/*					
						//FileStream fStream;
						Bitmap bmp;
						try 
						{
							//fStream = new FileStream(base_dir + name, FileMode.Open, FileAccess.Read);
							bmp = new Bitmap ( base_dir + name );
						}
						catch ( Exception ex )
						{
							// couldn't find the file
							Console.WriteLine ( "	ERROR: could not load file '{0}': {1}", base_dir + name, ex.Message );
							break;
						}

						// Flip image (needed so texture is the correct way around!)
						bmp.RotateFlip(RotateFlipType.RotateNoneFlipY); 
						
						System.Drawing.Imaging.BitmapData imgData = bmp.LockBits ( new Rectangle(new Point(0, 0), bmp.Size), 
								System.Drawing.Imaging.ImageLockMode.ReadOnly,
								System.Drawing.Imaging.PixelFormat.Format32bppArgb);								
//								System.Drawing.Imaging.PixelFormat.Format24bppRgb ); 
									
						m.BindTexture ( imgData.Width, imgData.Height, imgData.Scan0 );
						
						bmp.UnlockBits(imgData);
						bmp.Dispose();
*/
						
						/*
						BinaryReader br = new BinaryReader(fStream);

						br.ReadBytes ( 14 ); // skip file header
					
						uint offset = br.ReadUInt32 (  );
						//br.ReadBytes ( 4 ); // skip image header
						uint biWidth = br.ReadUInt32 ();
						uint biHeight = br.ReadUInt32 ();
						Console.WriteLine ( "w {0} h {1}", biWidth, biHeight );
						br.ReadBytes ( (int) offset - 12  ); // skip rest of image header
						
						byte[,,] tex = new byte [ biHeight , biWidth , 4 ];
						
						for ( int ii=0 ; ii <  biHeight ; ii++ )
						{
							for ( int jj=0 ; jj < biWidth ; jj++ )
							{
								tex [ ii, jj, 0 ] = br.ReadByte();
								tex [ ii, jj, 1 ] = br.ReadByte();
								tex [ ii, jj, 2 ] = br.ReadByte();
								tex [ ii, jj, 3 ] = 255;
								//Console.Write ( ii + " " );
							}
						}

						br.Close();
						fStream.Close();
						m.BindTexture ( (int) biWidth, (int) biHeight, tex );
						*/
					
						break;

					default:

						SkipChunk ( child );
						break;

				}
				chunk.BytesRead += child.BytesRead;
                child.SkipToEnd(reader);
			}
		}

		float[] ProcessColorChunk ( ThreeDSChunk chunk )
		{
            ThreeDSChunk child = new ThreeDSChunk(this, reader);
            float red = 1.0f;
            float green = 1.0f;
            float blue = 1.0f;

            switch (child.ID)
            {
                // Each color component is represented by a single-precision floating point number.
                case Groups.C_COLOR_F:
                    red = reader.ReadSingle();
                    green = reader.ReadSingle();
                    blue = reader.ReadSingle();
                    break;

                // Each color component is represented by a byte.
                case Groups.C_COLOR_24:
                    red = (float)reader.ReadByte() / 255.0f;
                    green = (float)reader.ReadByte() / 255.0f;
                    blue = (float)reader.ReadByte() / 255.0f;
                    break;
            }

//            Logger.Log("ProcessColorChunk: color = ({0},{1},{2}) len = {3}", red, green, blue, child.Length);

			chunk.BytesRead += (int) child.Length;
            child.SkipToEnd(reader);

            return new float[] { red, green, blue };
		}

		int ProcessPercentageChunk ( ThreeDSChunk chunk )
		{
            ThreeDSChunk child = new ThreeDSChunk(this, reader);
			int per = reader.ReadUInt16 ();
			child.BytesRead += 2;
			chunk.BytesRead += child.BytesRead;
            child.SkipToEnd(reader);
			return per;
		}

		Entity ProcessObjectChunk ( ThreeDSChunk chunk )
		{
			return ProcessObjectChunk ( chunk, new Entity() );
		}

		Entity ProcessObjectChunk ( ThreeDSChunk chunk, Entity e )
		{
			while ( chunk.BytesRead < chunk.Length )
			{
                ThreeDSChunk child = new ThreeDSChunk(this, reader);
#if LOG_CHUNKS
                Logger.Log("ProcessObjectChunk: chunk = {0:X}, len = {1}", child.ID, chunk.Length);
#endif

				switch (child.ID)
				{
					case Groups.C_OBJECT_MESH:

						ProcessObjectChunk ( child , e );
						break;

					case Groups.C_OBJECT_VERTICES:

						e.vertices = ReadVertices ( child );
						break;

					case Groups.C_OBJECT_FACES:
						e.triangles = ReadTriangles ( child );
#if LOG_FACES
                        Logger.Log("Object Faces: {0}", e.indices.Length);
#endif
						if ( child.BytesRead < child.Length )
                            ProcessFaceChunk(child, e);
						break;

					case Groups.C_OBJECT_UV:

						int cnt = reader.ReadUInt16 ();
						child.BytesRead += 2;

						//Console.WriteLine ( "	TexCoords: {0}", cnt );
						e.texcoords = new TexCoord [ cnt ];
						for ( int ii=0; ii<cnt; ii++ )
							e.texcoords [ii] = new TexCoord ( reader.ReadSingle (), reader.ReadSingle () );
						
						child.BytesRead += ( cnt * ( 4 * 2 ) );
						
						break;
						
					default:

						SkipChunk ( child );
						break;

				}
				chunk.BytesRead += child.BytesRead;

                child.SkipToEnd(reader);
				//Console.WriteLine ( "	ID: {0} Length: {1} Read: {2}", chunk.ID.ToString("x"), chunk.Length , chunk.BytesRead );
			}
			return e;
		}

        Entity ProcessFaceChunk(ThreeDSChunk chunk, Entity e)
        {
            while (chunk.BytesRead < chunk.Length)
            {
                ThreeDSChunk child = new ThreeDSChunk(this, reader);
#if LOG_CHUNKS
                Logger.Log("ProcessFaceChunk: chunk = {0:X}, len = {1}", child.ID, chunk.Length);
#endif

                switch (child.ID)
                {
                    case Groups.C_OBJECT_MATERIAL:

                        string materialName = ProcessString(child);
#if LOG_FACES
                        Logger.Log("Uses Material: {0}", materialName);
#endif
                        Material material = null;
                        if (materials.TryGetValue(materialName, out material))
                            e.material = material;
#if LOG_MATERIAL

                        else
                            Logger.Log(" Warning: Material '{0}' not found. ", materialName);
#endif
                        //Console.WriteLine(" Warning: Material '{0}' not found. ", name2);
                        //throw new Exception ( "Material not found!" );

                        int nfaces = reader.ReadUInt16();
                        child.BytesRead += 2;
#if LOG_FACES
                        Logger.Log("ProcessFaceChunk: {0} material faces", nfaces);
#endif
                        for (int i = 0; i < nfaces; i++)
                        {
                            int faceIndex = reader.ReadUInt16();
                            e.triangles[faceIndex].material = (material ?? Triangle.defaultMaterial);
                            child.BytesRead += 2;
                        }
                        SkipChunk(child);

                        break;

                    default:

                        SkipChunk(child);
                        break;

                }
                chunk.BytesRead += child.BytesRead;

                child.SkipToEnd(reader);
                //Console.WriteLine ( "	ID: {0} Length: {1} Read: {2}", chunk.ID.ToString("x"), chunk.Length , chunk.BytesRead );
            }
            return e;
        }

		void SkipChunk ( ThreeDSChunk chunk )
		{
			int length = (int) chunk.Length - chunk.BytesRead;
#if LOG_CHUNKS
            Logger.Log("SkipChunk: len = {0}, bytesRead = {1}", chunk.Length, chunk.BytesRead);
#endif

            reader.BaseStream.Seek(length, SeekOrigin.Current);
//            reader.ReadBytes(length);

			chunk.BytesRead += length;
	
//            ModelLoadProgressEvent(this, new ProgressChangedEventArgs(reader.BaseStream.Position * 100.0 / reader.BaseStream.Length));
		}

		string ProcessString ( ThreeDSChunk chunk )
		{
			StringBuilder sb = new StringBuilder ();

			byte b = reader.ReadByte ();
			int idx = 0;
			while ( b != 0 )
			{
				sb.Append ( (char) b);
				b = reader.ReadByte ();
				idx++;
			}
			chunk.BytesRead += idx+1;

			return sb.ToString();
		}

		Vector[] ReadVertices ( ThreeDSChunk chunk )
		{
			ushort numVerts = reader.ReadUInt16 ();
			chunk.BytesRead += 2;
			//Console.WriteLine ( "	Vertices: {0}", numVerts );

			Vector[] verts = new Vector[numVerts];
            for (int ii = 0; ii < numVerts; ii++)
			{
				float f1 = reader.ReadSingle();
				float f2 = reader.ReadSingle();
				float f3 = reader.ReadSingle();

				verts[ii] = new Vector ( f1, f3, -f2 );
				//Console.WriteLine ( verts [ii] );
			}

			//Console.WriteLine ( "{0}   {1}", verts.Length * ( 3 * 4 ), chunk.Length - chunk.BytesRead );

			chunk.BytesRead += verts.Length * ( 3 * 4 ) ;
			//chunk.BytesRead = (int) chunk.Length;
			//SkipChunk ( chunk );

			return verts;
		}

		Triangle[] ReadTriangles ( ThreeDSChunk chunk )
		{
			ushort numTris = reader.ReadUInt16 ();
			chunk.BytesRead += 2;
			//Console.WriteLine ( "	Triangles: {0}", numTris );

            Triangle[] tris = new Triangle[numTris];
            for (int ii = 0; ii < numTris; ii++)
			{
				tris [ii] = new Triangle ( reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16() );
				//Console.WriteLine ( idcs [ii] );

				// flags
				reader.ReadUInt16 ();
			}
			chunk.BytesRead += ( 2 * 4 ) * tris.Length;
			//Console.WriteLine ( "b {0} l {1}", chunk.BytesRead, chunk.Length);

			//chunk.BytesRead = (int) chunk.Length;
			//SkipChunk ( chunk );

			return tris;
		}

		class ThreeDSChunk
		{
            public readonly Groups ID; // was ushort
            public readonly uint Length;
            private readonly long streamStartPos;

            public int BytesRead;

			public ThreeDSChunk ( ThreeDSFile parent, BinaryReader reader )
			{
                streamStartPos = reader.BaseStream.Position;

				// 2 byte ID
				ID = (Groups)reader.ReadUInt16();
                Contract.Assert(ID >= Groups.C_VERSION && ID <= Groups.C_EDITKEYFRAME);

				// 4 byte length
				Length = reader.ReadUInt32 ();

				// = 6
				BytesRead = 6;

                parent.ModelLoadProgressEvent(this, new ProgressChangedEventArgs(streamStartPos * 100.0 / reader.BaseStream.Length));
			}

            public void SkipToEnd( BinaryReader reader )
            {
                reader.BaseStream.Position = streamStartPos + Length;
            }
		}
		
#endregion
		
	}
}
