using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.IO;

namespace HeroesCollisionTool
{
    class Program
    {
        static void Main()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

            string[] Arguments = Environment.GetCommandLineArgs();

            Console.WriteLine("============================================");
            Console.WriteLine("| Heroes Collision Tool release 1 by igorseabra4");
            Console.WriteLine("| Usage: drag .OBJ model files into the executable to convert them to Sonic Heroes .CL.");
            Console.WriteLine("| Just opening the program will convert every file found in the folder.");
            Console.WriteLine("| Dragging Sonic Heroes .CL into the program will convert those to .OBJ (you have to drag those).");
            Console.WriteLine("============================================");
            Console.WriteLine("| To set collision flags for each mesh, append an underscore _ and one of the following letters to the mesh's name:");
            Console.WriteLine("|  w - Wall.You can't walk or run on top of it");
            Console.WriteLine("|  s - Stairs. Model should be a ramp, but will act as stairs");
            Console.WriteLine("|  i - Barrier. It's a wall that is only solid as long as you are touching the ground");
            Console.WriteLine("|  p - Pinball table");
            Console.WriteLine("|  b - Bingo slide. Has more acceleration and better control");
            Console.WriteLine("| Check the documentation on Sonic Retro for more info.");
            Console.WriteLine("============================================");

            if (Arguments.Length > 1)
            {
                foreach (string i in Arguments)
                    if (i.Substring(i.Length - 3, 3).ToLower().Equals("obj".ToLower()))
                        ConvertOBJtoCL(i);
                    else if (i.Substring(i.Length - 2, 2).ToLower().Equals("cl".ToLower()))
                        ConvertCLtoOBJ(i);
            }
            else
            {
                string[] FilesInFolder = Directory.GetFiles(Directory.GetCurrentDirectory());

                foreach (string i in FilesInFolder)
                    if (i.Substring(i.Length - 3, 3).ToLower().Equals("obj".ToLower()))
                        ConvertOBJtoCL(i);
            }
            Console.ReadKey();
        }

        static void ConvertOBJtoCL(string InputFile)
        {
            Console.WriteLine("Reading" + InputFile);

            UseHeader = new Header();
            QuadNodeList.Clear();
            TriangleList.Clear();
            VertexList.Clear();

            byte DepthLevel = 11;
            bool AutoDepth;

            Console.WriteLine("Please choose a depth level for the quadtree. It depends on how large/complex your model is.");
            Console.WriteLine("The maximum you should use is 10. Type 0 for it to be chosen automatically.");

            while (DepthLevel < 0 | DepthLevel > 10)
                DepthLevel = Convert.ToByte(Console.ReadLine());

            if (DepthLevel == 0)
                AutoDepth = true;
            else
                AutoDepth = false;

            if (ReadOBJFile(InputFile))
                if (GenerateCollision(AutoDepth, DepthLevel))
                    CreateCLFile(InputFile);
                else
                    Console.WriteLine("Error.");
            else
                Console.WriteLine("Error.");
        }

        class Header
        {
            public UInt32 numBytes;
            public UInt32 pointQuadtree;
            public UInt32 pointTriangle;
            public UInt32 pointVertex;
            public float quadCenterX;
            public float quadCenterY;
            public float quadCenterZ;
            public float quadLenght;
            public UInt16 PowerFlag;
            public UInt16 numTriangles;
            public UInt16 numVertices;
            public UInt16 numQuadnodes;
        }

        class QuadNode
        {
            public UInt16 Index;
            public UInt16 Parent;
            public UInt16 Child;
            public UInt16 NodeTriangleAmount;
            public UInt32 TriListOffset;
            public UInt16 PosValueX;
            public UInt16 PosValueZ;

            public byte Depth;
            public List<UInt16> NodeTriangleList;

            public RectangleF NodeSquare;
            public bool Verified;
        }

        // Triangle definition class

        class Triangle
        {
            public ushort[] Vertices = new ushort[3];
            public ushort[] Atris = new ushort[3];
            public Vertex Normals;
            public byte[] ColFlags = new byte[4];
            public byte MeshNum;

            public Triangle(UInt16 a, UInt16 b, UInt16 c, byte d, byte[] e)
            {
                Vertices[0] = a;
                Vertices[1] = b;
                Vertices[2] = c;
                MeshNum = d;
                ColFlags = e;

                CalculateNormals();
                CalculateRectangle();
            }

            public Triangle(uint a, uint b, uint c)
            {
                Vertices[0] = (ushort)a;
                Vertices[1] = (ushort)b;
                Vertices[2] = (ushort)c;
            }

            public void CalculateNormals()
            {
                Vertex Vector1 = new Vertex(VertexList[Vertices[1]].X - VertexList[Vertices[0]].X, VertexList[Vertices[1]].Y - VertexList[Vertices[0]].Y, VertexList[Vertices[1]].Z - VertexList[Vertices[0]].Z);
                Vertex Vector2 = new Vertex(VertexList[Vertices[2]].X - VertexList[Vertices[0]].X, VertexList[Vertices[2]].Y - VertexList[Vertices[0]].Y, VertexList[Vertices[2]].Z - VertexList[Vertices[0]].Z);
                Vertex VectorNormal = new Vertex(Vector1.Y * Vector2.Z - Vector1.Z * Vector2.Y, Vector1.Z * Vector2.X - Vector1.X * Vector2.Z, Vector1.X * Vector2.Y - Vector1.Y * Vector2.X);

                double VectorModule = Math.Sqrt(VectorNormal.X * VectorNormal.X + VectorNormal.Y * VectorNormal.Y + VectorNormal.Z * VectorNormal.Z);

                Normals = new Vertex(VectorNormal.X / (float)VectorModule, VectorNormal.Y / (float)VectorModule, VectorNormal.Z / (float)VectorModule);
            }

            RectangleF TasRect;

            public void CalculateRectangle()
            {
                TasRect.X = VertexList[Vertices[0]].X;
                if (VertexList[Vertices[1]].X < TasRect.X)
                {
                    TasRect.X = VertexList[Vertices[1]].X;
                }
                if (VertexList[Vertices[2]].X < TasRect.X)
                {
                    TasRect.X = VertexList[Vertices[2]].X;
                }

                TasRect.Y = VertexList[Vertices[0]].Z;
                if (VertexList[Vertices[1]].Z < TasRect.Y)
                {
                    TasRect.Y = VertexList[Vertices[1]].Z;
                }
                if (VertexList[Vertices[2]].Z < TasRect.Y)
                {
                    TasRect.Y = VertexList[Vertices[2]].Z;
                }

                float Dif1X = Math.Abs(VertexList[Vertices[0]].X - VertexList[Vertices[1]].X);
                float Dif2X = Math.Abs(VertexList[Vertices[1]].X - VertexList[Vertices[2]].X);
                float Dif3X = Math.Abs(VertexList[Vertices[0]].X - VertexList[Vertices[2]].X);

                TasRect.Width = Dif1X;
                if (Dif2X > TasRect.Width)
                    TasRect.Width = Dif2X;
                if (Dif3X > TasRect.Width)
                    TasRect.Width = Dif3X;

                float Dif1Z = Math.Abs(VertexList[Vertices[0]].Z - VertexList[Vertices[1]].Z);
                float Dif2Z = Math.Abs(VertexList[Vertices[1]].Z - VertexList[Vertices[2]].Z);
                float Dif3Z = Math.Abs(VertexList[Vertices[0]].Z - VertexList[Vertices[2]].Z);

                TasRect.Height = Dif1Z;
                if (Dif2Z > TasRect.Height)
                    TasRect.Height = Dif2Z;
                if (Dif3Z > TasRect.Height)
                    TasRect.Height = Dif3Z;
            }

            public RectangleF Rectangle
            {
                get { return TasRect; }
            }
        }

        // Vertex definition class

        class Vertex
        {
            public float X;
            public float Y;
            public float Z;

            public Vertex(float a, float b, float c)
            {
                X = a;
                Y = b;
                Z = c;
            }

            public Vertex(ref string a, ref string b, ref string c)
            {
                X = Convert.ToSingle(a);
                Y = Convert.ToSingle(b);
                Z = Convert.ToSingle(c);
            }

            public void SetCoords(ref float a, ref float b, ref float c)
            {
                X = a;
                Y = b;
                Z = c;
            }
        }

        static Header UseHeader = new Header();
        static List<QuadNode> QuadNodeList = new List<QuadNode>();
        static List<Triangle> TriangleList = new List<Triangle>();
        static List<Vertex> VertexList = new List<Vertex>();

        static bool ReadOBJFile(string InputFile)
        {
            string[] OBJFile = File.ReadAllLines(InputFile);

            int CurrentMeshNum = -1;

            byte[] TempColFlags = { 0, 0, 0, 0 };

            foreach (string j in OBJFile)
            {
                if (j.Length > 2)
                {
                    if (j.Substring(0, 2) == "v ")
                    {
                        string[] SubStrings = j.Split(' ');

                        if (SubStrings.Count() == 5)
                        {
                            VertexList.Add(new Vertex(Convert.ToSingle(SubStrings[2]), Convert.ToSingle(SubStrings[3]), Convert.ToSingle(SubStrings[4])));
                        }
                        else if (SubStrings.Count() == 4)
                        {
                            VertexList.Add(new Vertex(Convert.ToSingle(SubStrings[1]), Convert.ToSingle(SubStrings[2]), Convert.ToSingle(SubStrings[3])));
                        }
                        else
                        {
                            Console.WriteLine("Error reading vertex stream.");
                            return false;
                        }
                    }
                    else if (j.StartsWith("f "))
                    {
                        string[] SubStrings = j.Split(' ');

                        TriangleList.Add(new Triangle(
                            (ushort)(Convert.ToUInt16(SubStrings[1].Split('/')[0]) - 1),
                            (ushort)(Convert.ToUInt16(SubStrings[2].Split('/')[0]) - 1),
                            (ushort)(Convert.ToUInt16(SubStrings[3].Split('/')[0]) - 1),
                            (byte)CurrentMeshNum, TempColFlags));
                    }
                    else if (j.StartsWith("g ") | j.StartsWith("o "))
                    {
                        CurrentMeshNum += 1;
                        TempColFlags = new byte[] { 0, 0, 0, 0 };

                        if (j.Contains('_'))
                        {
                            if (j.Split('_').Last().Contains("b"))
                                TempColFlags[0] = 64;
                            else if (j.Split('_').Last().Contains("p"))
                                TempColFlags[0] = 128;

                            if (j.Split('_').Last().Contains("w"))
                                TempColFlags[2] = 1;
                            else if (j.Split('_').Last().Contains("s"))
                                TempColFlags[2] = 4;
                            else if (j.Split('_').Last().Contains("i"))
                                TempColFlags[2] = 128;

                            if (j.Split('_').Last().Contains("p0"))
                                TempColFlags[0] = 0;
                            else if (j.Split('_').Last().Contains("p1"))
                                TempColFlags[0] = 1;
                            else if (j.Split('_').Last().Contains("p2"))
                                TempColFlags[0] = 2;
                            else if (j.Split('_').Last().Contains("p3"))
                                TempColFlags[0] = 4;
                            else if (j.Split('_').Last().Contains("p4"))
                                TempColFlags[0] = 8;
                            else if (j.Split('_').Last().Contains("p5"))
                                TempColFlags[0] = 16;
                            else if (j.Split('_').Last().Contains("p6"))
                                TempColFlags[0] = 32;
                            else if (j.Split('_').Last().Contains("p7"))
                                TempColFlags[0] = 64;
                            else if (j.Split('_').Last().Contains("p8"))
                                TempColFlags[0] = 128;

                            if (j.Split('_').Last().Contains("s0"))
                                TempColFlags[1] = 0;
                            else if (j.Split('_').Last().Contains("s1"))
                                TempColFlags[1] = 1;
                            else if (j.Split('_').Last().Contains("s2"))
                                TempColFlags[1] = 2;
                            else if (j.Split('_').Last().Contains("s3"))
                                TempColFlags[1] = 4;
                            else if (j.Split('_').Last().Contains("s4"))
                                TempColFlags[1] = 8;
                            else if (j.Split('_').Last().Contains("s5"))
                                TempColFlags[1] = 16;
                            else if (j.Split('_').Last().Contains("s6"))
                                TempColFlags[1] = 32;
                            else if (j.Split('_').Last().Contains("s7"))
                                TempColFlags[1] = 64;
                            else if (j.Split('_').Last().Contains("s8"))
                                TempColFlags[1] = 128;

                            if (j.Split('_').Last().Contains("t0"))
                                TempColFlags[2] = 0;
                            else if (j.Split('_').Last().Contains("t1"))
                                TempColFlags[2] = 1;
                            else if (j.Split('_').Last().Contains("t2"))
                                TempColFlags[2] = 2;
                            else if (j.Split('_').Last().Contains("t3"))
                                TempColFlags[2] = 4;
                            else if (j.Split('_').Last().Contains("t4"))
                                TempColFlags[2] = 8;
                            else if (j.Split('_').Last().Contains("t5"))
                                TempColFlags[2] = 16;
                            else if (j.Split('_').Last().Contains("t6"))
                                TempColFlags[2] = 32;
                            else if (j.Split('_').Last().Contains("t7"))
                                TempColFlags[2] = 64;
                            else if (j.Split('_').Last().Contains("t8"))
                                TempColFlags[2] = 128;

                            if (j.Split('_').Last().Contains("f0"))
                                TempColFlags[3] = 0;
                            else if (j.Split('_').Last().Contains("f1"))
                                TempColFlags[3] = 1;
                            else if (j.Split('_').Last().Contains("f2"))
                                TempColFlags[3] = 2;
                            else if (j.Split('_').Last().Contains("f3"))
                                TempColFlags[3] = 4;
                            else if (j.Split('_').Last().Contains("f4"))
                                TempColFlags[3] = 8;
                            else if (j.Split('_').Last().Contains("f5"))
                                TempColFlags[3] = 16;
                            else if (j.Split('_').Last().Contains("f6"))
                                TempColFlags[3] = 32;
                            else if (j.Split('_').Last().Contains("f7"))
                                TempColFlags[3] = 64;
                            else if (j.Split('_').Last().Contains("f8"))
                                TempColFlags[3] = 128;
                        }
                    }
                }
            }

            Console.WriteLine("Imported .OBJ file.");

            Console.WriteLine("Number of Vertices: " + VertexList.Count().ToString());
            if (VertexList.Count > 0xffff)
            {
                Console.WriteLine("Error: Maximum amount of 65535 vertices reached.");
                return false;
            }

            Console.WriteLine("Number of Triangles: " + TriangleList.Count().ToString());
            if (TriangleList.Count > 0xffff)
            {
                Console.WriteLine("Error: Maximum amount of 65535 triangles reached.");
                return false;
            }
            return true;
        }

        static bool GenerateCollision(bool AutoDepth, byte MaxDepth)
        {
            //Let's start with quadtree maximums, minimums and center
            float MaxX = VertexList[0].X;
            float MaxY = VertexList[0].Y;
            float MaxZ = VertexList[0].Z;
            float MinX = VertexList[0].X;
            float MinY = VertexList[0].Y;
            float MinZ = VertexList[0].Z;

            foreach (Vertex i in VertexList)
            {
                if (i.X > MaxX)
                    MaxX = i.X;
                if (i.Y > MaxY)
                    MaxY = i.Y;
                if (i.Z > MaxZ)
                    MaxZ = i.Z;
                if (i.X < MinX)
                    MinX = i.X;
                if (i.Y < MinY)
                    MinY = i.Y;
                if (i.Z < MinZ)
                    MinZ = i.Z;
            }

            UseHeader.quadCenterX = (MaxX + MinX) / 2.0f;
            UseHeader.quadCenterY = (MaxY + MinY) / 2.0f;
            UseHeader.quadCenterZ = (MaxZ + MinZ) / 2.0f;

            UseHeader.quadLenght = MaxX - MinX;
            if (UseHeader.quadLenght < MaxZ - MinZ)
            {
                UseHeader.quadLenght = MaxZ - MinZ;
            }

            if (AutoDepth == true)
            {
                MaxDepth = (byte)(Math.Log(UseHeader.quadLenght / 50) / (Math.Log(2)));
                if (MaxDepth > 10)
                    MaxDepth = 10;
            }

            UseHeader.numTriangles = (ushort)TriangleList.Count;
            UseHeader.numVertices = (ushort)VertexList.Count;

            //Now let's build the quadtree
            UseHeader.PowerFlag = 0xc;

            if (BuildQuadtree(MaxDepth))
            {
                UseHeader.numQuadnodes = (ushort)QuadNodeList.Count;
                Console.WriteLine("Number of Quadnodes: " + UseHeader.numQuadnodes.ToString());
                return true;
            }
            else
                return false;
        }

        static bool BuildQuadtree(byte MaxDepth)
        {
            Console.WriteLine("Generating quadtree nodes...");

            QuadNode TempNode = new QuadNode();

            TempNode.NodeSquare.X = UseHeader.quadCenterX - (UseHeader.quadLenght / 2);
            TempNode.NodeSquare.Y = UseHeader.quadCenterZ - (UseHeader.quadLenght / 2);
            TempNode.NodeSquare.Height = UseHeader.quadLenght;
            TempNode.NodeSquare.Width = UseHeader.quadLenght;

            TempNode.Child = 1;
            TempNode.Verified = false;

            QuadNodeList.Add(TempNode);

            int i = 0;

            while (i < QuadNodeList.Count)
            {
                QuadNode NodeParent = QuadNodeList[i];

                if (NodeParent.Verified == false & HasTrianglesInsideNode(NodeParent))
                {
                    NodeParent.Child = (ushort)QuadNodeList.Count;
                    //Set child to parent

                    if (QuadNodeList.Count >= 0xffff - 4)
                    {
                        Console.WriteLine("Error: maximum number of quadtree nodes reached. File will be unusable. Please reduce maximum depth level or model complexity.");
                        return false;
                    }

                    QuadNodeList.Add(CreateNode(NodeParent, 0, ref MaxDepth));
                    QuadNodeList.Add(CreateNode(NodeParent, 1, ref MaxDepth));
                    QuadNodeList.Add(CreateNode(NodeParent, 2, ref MaxDepth));
                    QuadNodeList.Add(CreateNode(NodeParent, 3, ref MaxDepth));

                    NodeParent.Verified = true;
                    QuadNodeList[i] = NodeParent;
                }
                else if ((NodeParent.Verified == false) & (HasTrianglesInsideNode(NodeParent) == false))
                {
                    NodeParent.Verified = true;
                    QuadNodeList[i] = NodeParent;
                }
                else if (NodeParent.Verified == true)
                {
                    i += 1;
                }
            }
            return true;
        }

        static QuadNode CreateNode(QuadNode NodeParent, byte NodeOrient, ref byte MaxDepth)
        {
            QuadNode NodeChild = new QuadNode();
            NodeChild.Index = (ushort)QuadNodeList.Count;
            NodeChild.Parent = NodeParent.Index;
            NodeChild.Child = 0;
            NodeChild.Depth = (byte)(NodeParent.Depth + 1);

            NodeChild.PosValueX = NodeParent.PosValueX;
            NodeChild.PosValueZ = NodeParent.PosValueZ;

            NodeChild.NodeSquare.X = NodeParent.NodeSquare.X;
            NodeChild.NodeSquare.Y = NodeParent.NodeSquare.Y;
            NodeChild.NodeSquare.Width = NodeParent.NodeSquare.Width / 2;
            NodeChild.NodeSquare.Height = NodeParent.NodeSquare.Width / 2;

            if (NodeOrient == 1)
            {
                NodeChild.PosValueX += (ushort)Math.Pow(2, (UseHeader.PowerFlag - NodeChild.Depth));
                NodeChild.NodeSquare.X += NodeChild.NodeSquare.Width;
            }
            else if (NodeOrient == 2)
            {
                NodeChild.PosValueZ += (ushort)Math.Pow(2, (UseHeader.PowerFlag - NodeChild.Depth));
                NodeChild.NodeSquare.Y += NodeChild.NodeSquare.Width;
            }
            else if (NodeOrient == 3)
            {
                NodeChild.PosValueX += (ushort)Math.Pow(2, (UseHeader.PowerFlag - NodeChild.Depth));
                NodeChild.NodeSquare.X += NodeChild.NodeSquare.Width;
                NodeChild.PosValueZ += (ushort)Math.Pow(2, (UseHeader.PowerFlag - NodeChild.Depth));
                NodeChild.NodeSquare.Y += NodeChild.NodeSquare.Width;
            }

            if (NodeChild.Depth == MaxDepth)
            {
                NodeChild.NodeTriangleList = GetTrianglesInsideNode(NodeChild);
                NodeChild.NodeTriangleAmount = (ushort)NodeChild.NodeTriangleList.Count;
                NodeChild.Verified = true;
            }
            else
            {
                NodeChild.Verified = false;
            }

            return NodeChild;
        }

        static bool HasTrianglesInsideNode(QuadNode Node)
        {
            foreach (Triangle i in TriangleList)
                if (Node.NodeSquare.IntersectsWith(i.Rectangle))
                    return true;

            return false;
        }

        static List<UInt16> GetTrianglesInsideNode(QuadNode Node)
        {
            List<UInt16> NodeTriangleList = new List<UInt16>();

            for (ushort i = 0; i < TriangleList.Count; i++)
                if (Node.NodeSquare.IntersectsWith(TriangleList[i].Rectangle))
                    NodeTriangleList.Add(i);

            return NodeTriangleList;
        }

        static bool CreateCLFile(string InputFileName)
        {
            //Finally, let's write the file
            BinaryWriter FileWriter = new BinaryWriter(new FileStream(Path.ChangeExtension(InputFileName, "cl"), FileMode.Create));
            Console.WriteLine("Creating file...");

            FileWriter.BaseStream.Position = 0x28;
            Console.WriteLine("Writing triangle reference lists...");
            for (ushort i = 0; i < QuadNodeList.Count; i++)
            {
                if (QuadNodeList[i].NodeTriangleAmount > 0)
                {
                    QuadNode TempNode = QuadNodeList[i];
                    TempNode.TriListOffset = (uint)FileWriter.BaseStream.Position;
                    QuadNodeList[i] = TempNode;
                    foreach (UInt16 j in QuadNodeList[i].NodeTriangleList)
                    {
                        FileWriter.Write(BitConverter.GetBytes(j)[1]);
                        FileWriter.Write(BitConverter.GetBytes(j)[0]);
                    }
                }
            }
            if (FileWriter.BaseStream.Position % 4 == 2)
            {
                FileWriter.Write(new byte[] { 0, 0 });
            }

            UseHeader.pointQuadtree = (uint)FileWriter.BaseStream.Position;
            Console.WriteLine("Writing quadtree...");
            byte[] TempWriteArray = new byte[0x20 * QuadNodeList.Count];
            for (ushort i = 0; i < UseHeader.numQuadnodes; i++)
            {
                TempWriteArray[0x20 * i] = BitConverter.GetBytes(QuadNodeList[i].Index)[1];
                TempWriteArray[0x20 * i + 1] = BitConverter.GetBytes(QuadNodeList[i].Index)[0];
                TempWriteArray[0x20 * i + 2] = BitConverter.GetBytes(QuadNodeList[i].Parent)[1];
                TempWriteArray[0x20 * i + 3] = BitConverter.GetBytes(QuadNodeList[i].Parent)[0];
                TempWriteArray[0x20 * i + 4] = BitConverter.GetBytes(QuadNodeList[i].Child)[1];
                TempWriteArray[0x20 * i + 5] = BitConverter.GetBytes(QuadNodeList[i].Child)[0];
                TempWriteArray[0x20 * i + 14] = BitConverter.GetBytes(QuadNodeList[i].NodeTriangleAmount)[1];
                TempWriteArray[0x20 * i + 15] = BitConverter.GetBytes(QuadNodeList[i].NodeTriangleAmount)[0];
                TempWriteArray[0x20 * i + 16] = BitConverter.GetBytes(QuadNodeList[i].TriListOffset)[3];
                TempWriteArray[0x20 * i + 17] = BitConverter.GetBytes(QuadNodeList[i].TriListOffset)[2];
                TempWriteArray[0x20 * i + 18] = BitConverter.GetBytes(QuadNodeList[i].TriListOffset)[1];
                TempWriteArray[0x20 * i + 19] = BitConverter.GetBytes(QuadNodeList[i].TriListOffset)[0];
                TempWriteArray[0x20 * i + 20] = BitConverter.GetBytes(QuadNodeList[i].PosValueX)[1];
                TempWriteArray[0x20 * i + 21] = BitConverter.GetBytes(QuadNodeList[i].PosValueX)[0];
                TempWriteArray[0x20 * i + 22] = BitConverter.GetBytes(QuadNodeList[i].PosValueZ)[1];
                TempWriteArray[0x20 * i + 23] = BitConverter.GetBytes(QuadNodeList[i].PosValueZ)[0];
                TempWriteArray[0x20 * i + 24] = QuadNodeList[i].Depth;
            }
            FileWriter.Write(TempWriteArray);
            TempWriteArray = null;

            UseHeader.pointTriangle = (uint)FileWriter.BaseStream.Position;
            Console.WriteLine("Writing triangles...");
            TempWriteArray = new byte[0x20 * TriangleList.Count];
            for (ushort i = 0; i <= UseHeader.numTriangles - 1; i++)
            {
                TempWriteArray[0x20 * i] = BitConverter.GetBytes(TriangleList[i].Vertices[0])[1];
                TempWriteArray[0x20 * i + 1] = BitConverter.GetBytes(TriangleList[i].Vertices[0])[0];
                TempWriteArray[0x20 * i + 2] = BitConverter.GetBytes(TriangleList[i].Vertices[1])[1];
                TempWriteArray[0x20 * i + 3] = BitConverter.GetBytes(TriangleList[i].Vertices[1])[0];
                TempWriteArray[0x20 * i + 4] = BitConverter.GetBytes(TriangleList[i].Vertices[2])[1];
                TempWriteArray[0x20 * i + 5] = BitConverter.GetBytes(TriangleList[i].Vertices[2])[0];
                TempWriteArray[0x20 * i + 6] = 0xff;
                TempWriteArray[0x20 * i + 7] = 0xff;
                TempWriteArray[0x20 * i + 8] = 0xff;
                TempWriteArray[0x20 * i + 9] = 0xff;
                TempWriteArray[0x20 * i + 10] = 0xff;
                TempWriteArray[0x20 * i + 11] = 0xff;
                TempWriteArray[0x20 * i + 12] = BitConverter.GetBytes(TriangleList[i].Normals.X)[3];
                TempWriteArray[0x20 * i + 13] = BitConverter.GetBytes(TriangleList[i].Normals.X)[2];
                TempWriteArray[0x20 * i + 14] = BitConverter.GetBytes(TriangleList[i].Normals.X)[1];
                TempWriteArray[0x20 * i + 15] = BitConverter.GetBytes(TriangleList[i].Normals.X)[0];
                TempWriteArray[0x20 * i + 16] = BitConverter.GetBytes(TriangleList[i].Normals.Y)[3];
                TempWriteArray[0x20 * i + 17] = BitConverter.GetBytes(TriangleList[i].Normals.Y)[2];
                TempWriteArray[0x20 * i + 18] = BitConverter.GetBytes(TriangleList[i].Normals.Y)[1];
                TempWriteArray[0x20 * i + 19] = BitConverter.GetBytes(TriangleList[i].Normals.Y)[0];
                TempWriteArray[0x20 * i + 20] = BitConverter.GetBytes(TriangleList[i].Normals.Z)[3];
                TempWriteArray[0x20 * i + 21] = BitConverter.GetBytes(TriangleList[i].Normals.Z)[2];
                TempWriteArray[0x20 * i + 22] = BitConverter.GetBytes(TriangleList[i].Normals.Z)[1];
                TempWriteArray[0x20 * i + 23] = BitConverter.GetBytes(TriangleList[i].Normals.Z)[0];
                TempWriteArray[0x20 * i + 24] = TriangleList[i].ColFlags[0];
                TempWriteArray[0x20 * i + 25] = TriangleList[i].ColFlags[1];
                TempWriteArray[0x20 * i + 26] = TriangleList[i].ColFlags[2];
                TempWriteArray[0x20 * i + 27] = TriangleList[i].ColFlags[3];
                TempWriteArray[0x20 * i + 28] = BitConverter.GetBytes(TriangleList[i].MeshNum)[1];
                TempWriteArray[0x20 * i + 29] = BitConverter.GetBytes(TriangleList[i].MeshNum)[0];
            }
            FileWriter.Write(TempWriteArray);
            TempWriteArray = null;

            UseHeader.pointVertex = (uint)FileWriter.BaseStream.Position;
            Console.WriteLine("Writing vertices...");
            TempWriteArray = new byte[12 * TriangleList.Count];
            for (ushort i = 0; i < UseHeader.numVertices; i++)
            {
                TempWriteArray[12 * i] = BitConverter.GetBytes(VertexList[i].X)[3];
                TempWriteArray[12 * i + 1] = BitConverter.GetBytes(VertexList[i].X)[2];
                TempWriteArray[12 * i + 2] = BitConverter.GetBytes(VertexList[i].X)[1];
                TempWriteArray[12 * i + 3] = BitConverter.GetBytes(VertexList[i].X)[0];
                TempWriteArray[12 * i + 4] = BitConverter.GetBytes(VertexList[i].Y)[3];
                TempWriteArray[12 * i + 5] = BitConverter.GetBytes(VertexList[i].Y)[2];
                TempWriteArray[12 * i + 6] = BitConverter.GetBytes(VertexList[i].Y)[1];
                TempWriteArray[12 * i + 7] = BitConverter.GetBytes(VertexList[i].Y)[0];
                TempWriteArray[12 * i + 8] = BitConverter.GetBytes(VertexList[i].Z)[3];
                TempWriteArray[12 * i + 9] = BitConverter.GetBytes(VertexList[i].Z)[2];
                TempWriteArray[12 * i + 10] = BitConverter.GetBytes(VertexList[i].Z)[1];
                TempWriteArray[12 * i + 11] = BitConverter.GetBytes(VertexList[i].Z)[0];
            }
            FileWriter.Write(TempWriteArray);
            TempWriteArray = null;

            UseHeader.numBytes = (uint)FileWriter.BaseStream.Position;
            FileWriter.BaseStream.Position = 0;
            TempWriteArray = new byte[40];
            TempWriteArray[0] = BitConverter.GetBytes(UseHeader.numBytes)[3];
            TempWriteArray[1] = BitConverter.GetBytes(UseHeader.numBytes)[2];
            TempWriteArray[2] = BitConverter.GetBytes(UseHeader.numBytes)[1];
            TempWriteArray[3] = BitConverter.GetBytes(UseHeader.numBytes)[0];
            TempWriteArray[4] = BitConverter.GetBytes(UseHeader.pointQuadtree)[3];
            TempWriteArray[5] = BitConverter.GetBytes(UseHeader.pointQuadtree)[2];
            TempWriteArray[6] = BitConverter.GetBytes(UseHeader.pointQuadtree)[1];
            TempWriteArray[7] = BitConverter.GetBytes(UseHeader.pointQuadtree)[0];
            TempWriteArray[8] = BitConverter.GetBytes(UseHeader.pointTriangle)[3];
            TempWriteArray[9] = BitConverter.GetBytes(UseHeader.pointTriangle)[2];
            TempWriteArray[10] = BitConverter.GetBytes(UseHeader.pointTriangle)[1];
            TempWriteArray[11] = BitConverter.GetBytes(UseHeader.pointTriangle)[0];
            TempWriteArray[12] = BitConverter.GetBytes(UseHeader.pointVertex)[3];
            TempWriteArray[13] = BitConverter.GetBytes(UseHeader.pointVertex)[2];
            TempWriteArray[14] = BitConverter.GetBytes(UseHeader.pointVertex)[1];
            TempWriteArray[15] = BitConverter.GetBytes(UseHeader.pointVertex)[0];
            TempWriteArray[16] = BitConverter.GetBytes(UseHeader.quadCenterX)[3];
            TempWriteArray[17] = BitConverter.GetBytes(UseHeader.quadCenterX)[2];
            TempWriteArray[18] = BitConverter.GetBytes(UseHeader.quadCenterX)[1];
            TempWriteArray[19] = BitConverter.GetBytes(UseHeader.quadCenterX)[0];
            TempWriteArray[20] = BitConverter.GetBytes(UseHeader.quadCenterY)[3];
            TempWriteArray[21] = BitConverter.GetBytes(UseHeader.quadCenterY)[2];
            TempWriteArray[22] = BitConverter.GetBytes(UseHeader.quadCenterY)[1];
            TempWriteArray[23] = BitConverter.GetBytes(UseHeader.quadCenterY)[0];
            TempWriteArray[24] = BitConverter.GetBytes(UseHeader.quadCenterZ)[3];
            TempWriteArray[25] = BitConverter.GetBytes(UseHeader.quadCenterZ)[2];
            TempWriteArray[26] = BitConverter.GetBytes(UseHeader.quadCenterZ)[1];
            TempWriteArray[27] = BitConverter.GetBytes(UseHeader.quadCenterZ)[0];
            TempWriteArray[28] = BitConverter.GetBytes(UseHeader.quadLenght)[3];
            TempWriteArray[29] = BitConverter.GetBytes(UseHeader.quadLenght)[2];
            TempWriteArray[30] = BitConverter.GetBytes(UseHeader.quadLenght)[1];
            TempWriteArray[31] = BitConverter.GetBytes(UseHeader.quadLenght)[0];
            TempWriteArray[32] = BitConverter.GetBytes(UseHeader.PowerFlag)[1];
            TempWriteArray[33] = BitConverter.GetBytes(UseHeader.PowerFlag)[0];
            TempWriteArray[34] = BitConverter.GetBytes(UseHeader.numTriangles)[1];
            TempWriteArray[35] = BitConverter.GetBytes(UseHeader.numTriangles)[0];
            TempWriteArray[36] = BitConverter.GetBytes(UseHeader.numVertices)[1];
            TempWriteArray[37] = BitConverter.GetBytes(UseHeader.numVertices)[0];
            TempWriteArray[38] = BitConverter.GetBytes(UseHeader.numQuadnodes)[1];
            TempWriteArray[39] = BitConverter.GetBytes(UseHeader.numQuadnodes)[0];
            FileWriter.Write(TempWriteArray);
            TempWriteArray = null;

            FileWriter.Close();
            Console.WriteLine("Success");
            return true;
        }

        static UInt16 SwitchEndianUInt16(UInt16 value)
        {
            byte[] TempArray = BitConverter.GetBytes(value);
            Array.Reverse(TempArray);
            return BitConverter.ToUInt16(TempArray, 0);
        }

        static UInt32 SwitchEndianUInt32(UInt32 value)
        {
            byte[] TempArray = BitConverter.GetBytes(value);
            Array.Reverse(TempArray);
            return BitConverter.ToUInt32(TempArray, 0);
        }

        static float SwitchEndianSingle(float value)
        {
            byte[] TempArray = BitConverter.GetBytes(value);
            Array.Reverse(TempArray);
            return BitConverter.ToSingle(TempArray, 0);
        }

        static List<UInt32> MeshTypeList;

        static void ConvertCLtoOBJ(string i)
        {
            TriangleList = new List<Triangle>();
            VertexList = new List<Vertex>();
            MeshTypeList = new List<UInt32>();

            BinaryReader QReader = new BinaryReader(new FileStream(i, FileMode.Open));
            Console.WriteLine("Reading " + i);

            if (QReader.BaseStream.Length > 0)
            {
                ReadCLFile(QReader);
                if ((TriangleList.Count > 0) & (VertexList.Count > 0))
                {
                    CreateOBJFile(i);
                    Console.WriteLine("File converted successfully.");
                }
                else
                    Console.WriteLine("There was an error with the operation.");
            }
        }

        static void ReadCLFile(BinaryReader CLReader)
        {
            CLReader.BaseStream.Position = 0;
            uint numBytes = SwitchEndianUInt32(CLReader.ReadUInt32());
            if (numBytes != CLReader.BaseStream.Length)
                Console.WriteLine("That's probably not a .cl file from Sonic Heroes but I'll try anyway");

            UInt32 pointSomething = SwitchEndianUInt32(CLReader.ReadUInt32());
            UInt32 pointTriangle = SwitchEndianUInt32(CLReader.ReadUInt32());
            UInt32 pointVertex = SwitchEndianUInt32(CLReader.ReadUInt32());
            float[] quadtree = new float[4];

            quadtree[0] = SwitchEndianSingle(CLReader.ReadSingle());
            quadtree[1] = SwitchEndianSingle(CLReader.ReadSingle());
            quadtree[2] = SwitchEndianSingle(CLReader.ReadSingle());
            quadtree[3] = SwitchEndianSingle(CLReader.ReadSingle());

            UInt16 basePower = SwitchEndianUInt16(CLReader.ReadUInt16());
            UInt16 numTriangles = SwitchEndianUInt16(CLReader.ReadUInt16());
            UInt16 numVertices = SwitchEndianUInt16(CLReader.ReadUInt16());
            UInt16 numQuadNodes = SwitchEndianUInt16(CLReader.ReadUInt16());

            for (int i = 0; i < numTriangles; i++)
            {
                CLReader.BaseStream.Position = pointTriangle + i * 0x20;

                Triangle tempTriangle = new Triangle(SwitchEndianUInt16(CLReader.ReadUInt16()), SwitchEndianUInt16(CLReader.ReadUInt16()), SwitchEndianUInt16(CLReader.ReadUInt16()));
                tempTriangle.Atris = new ushort[] { SwitchEndianUInt16(CLReader.ReadUInt16()), SwitchEndianUInt16(CLReader.ReadUInt16()), SwitchEndianUInt16(CLReader.ReadUInt16()) };
                tempTriangle.Normals = new Vertex(SwitchEndianSingle(CLReader.ReadSingle()), SwitchEndianSingle(CLReader.ReadSingle()), SwitchEndianSingle(CLReader.ReadSingle()));

                // UInt32[] TempArray = new UInt32[2];

                // TempArray[0] = SwitchEndianUInt32(CLReader.ReadUInt32());
                // TempArray[1] = SwitchEndianUInt32(CLReader.ReadUInt32());

                tempTriangle.ColFlags[0] = CLReader.ReadByte();
                tempTriangle.ColFlags[1] = CLReader.ReadByte();
                tempTriangle.ColFlags[2] = CLReader.ReadByte();
                tempTriangle.ColFlags[3] = CLReader.ReadByte();

                tempTriangle.MeshNum = CLReader.ReadByte();

                UInt32 FlagsAsUint32 = BitConverter.ToUInt32(tempTriangle.ColFlags, 0);

                if (!MeshTypeList.Contains(FlagsAsUint32))
                    MeshTypeList.Add(FlagsAsUint32);

                TriangleList.Add(tempTriangle);
            }

            CLReader.BaseStream.Position = pointVertex;
            for (int i = 0; i < numVertices; i++)
            {
                Vertex tempVertex = new Vertex(SwitchEndianSingle(CLReader.ReadSingle()), SwitchEndianSingle(CLReader.ReadSingle()), SwitchEndianSingle(CLReader.ReadSingle()));

                VertexList.Add(tempVertex);
            }
        }

        static void CreateOBJFile(string InputFileName)
        {
            StreamWriter FileCloser = new StreamWriter(new FileStream(Path.ChangeExtension(InputFileName, "obj"), FileMode.Create));

            FileCloser.WriteLine("#Exported by HeroesCollisionTool");
            FileCloser.WriteLine("#Number of vertices: " + VertexList.Count.ToString());
            FileCloser.WriteLine("#Number of faces: " + TriangleList.Count.ToString());
            FileCloser.WriteLine();

            foreach (Vertex i in VertexList)
                FileCloser.WriteLine("v " + i.X.ToString() + " " + i.Y.ToString() + " " + i.Z.ToString());

            FileCloser.WriteLine();

            foreach (UInt32 i in MeshTypeList)
            {
                byte[] TempByte = BitConverter.GetBytes(i);

                FileCloser.WriteLine("g mesh_" + TempByte[0].ToString("X2") + TempByte[1].ToString("X2") + TempByte[2].ToString("X2") + TempByte[3].ToString("X2"));
                FileCloser.WriteLine();

                foreach (Triangle j in TriangleList)
                    if ((j.ColFlags[0] == TempByte[0]) & (j.ColFlags[1] == TempByte[1]) & (j.ColFlags[2] == TempByte[2]) & (j.ColFlags[3] == TempByte[3]))
                        FileCloser.WriteLine("f " + (j.Vertices[0] + 1).ToString() + " " + (j.Vertices[1] + 1).ToString() + " " + (j.Vertices[2] + 1).ToString());

                FileCloser.WriteLine();
            }

            FileCloser.Close();
        }
    }
}