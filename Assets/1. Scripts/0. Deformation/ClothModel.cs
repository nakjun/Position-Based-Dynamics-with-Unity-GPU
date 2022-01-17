﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Common.Mathematics.LinearAlgebra;
public class ClothModel : MonoBehaviour
{
    public float timeStep = 0.01f;
    public int xSize = 8;
    public int ySize = 8;
    public float width = 5.0f;
    public float height = 5.0f;
    public bool drawLines = true;    
    public bool drawNodes = false;    
    private int NumParticles;
    public float stretchStiffness;
    public float bendStiffness;

    public GameObject node;

    private Matrix4x4d RTS;
    public Vector3[] positions;
    public bool[] isSimulated;
    public int[] indices;
    public List<int> edgeList;
    public ComputeShader shader;    
    public Mesh mesh;    

    // Start is called before the first frame update
    void Awake()
    {
        CreateModel();
    }

    // Update is called once per frame
    void Update()
    {
        if(drawLines)
        {
            DrawLines();
        }
    }

    public void DrawLines()
    {
        for(int i=0;i<edgeList.Count;)
        {
            Debug.DrawLine(positions[edgeList[i]],positions[edgeList[i+1]],Color.white);
            i = i+2;
        }
    }

    public void CreateModel()
    {
        NumParticles = xSize * ySize;

        //Create Particles
        CreateParticles();
        CreateEdge();

        mesh = new Mesh();
        mesh.vertices = positions;
        mesh.triangles = indices;
        mesh.RecalculateNormals();

        this.GetComponent<MeshFilter>().mesh = mesh;     

        Debug.Log("Create Success");
        Debug.Log("# of particles : " + positions.Length);
        Debug.Log("# of indices : " + indices.Length);
        Debug.Log("# of edges : " + edgeList.Count);
    }

    public void CreateParticles()
    {
        positions = new Vector3[(xSize+1) * (ySize+1)];
        indices = new int[xSize * ySize * 2 * 3];

        float dx = width / xSize;
        float dy = height / ySize;

        int index = 0;
        for (int j = 0; j <= ySize; j++)
        {
            for (int i = 0; i <= xSize; i++)
            {
                float x = dx * i;
                float z = dy * j;

                Vector3 pos = new Vector3(x - width / 2.0f, 0, z - height / 2.0f);

                positions[index] = pos;
                index++;
                
            }
        }

        index = 0;
        for (int i = 0; i < ySize; i++)
        {
            for (int j = 0; j < xSize; j++)
            {
                int i0 = i * (xSize + 1) + j;
                int i1 = i0 + 1;
                int i2 = i0 + (xSize + 1);
                int i3 = i2 + 1;

                if ((j + i) % 2 != 0)
                {
                    indices[index++] = i0;
                    indices[index++] = i2;
                    indices[index++] = i1;
                    indices[index++] = i1;
                    indices[index++] = i2;
                    indices[index++] = i3;
                }
                else
                {
                    indices[index++] = i0;
                    indices[index++] = i2;
                    indices[index++] = i3;
                    indices[index++] = i0;
                    indices[index++] = i3;
                    indices[index++] = i1;
                }
            }
        }
        
        if(drawNodes)
        {
            for(int i=0;i<positions.Length;i++)
            {
                Instantiate(node, positions[i] ,Quaternion.identity);
            }
        }
    }

    public void CreateEdge()
    {
        int[,] edges = new int[,]
        {
            {0,1}, {1,2}, {2,0}
        };

        int numTris = indices.Length / 3;

        edgeList = new List<int>();
        HashSet<Vector2i> set = new HashSet<Vector2i>();
        HashSet<Vector2i> rset = new HashSet<Vector2i>();

        for (int n = 0; n < numTris; n++)
        {
            for (int i = 0; i < 3; i++)
            {
                int i0 = indices[3 * n + edges[i, 0]];
                int i1 = indices[3 * n + edges[i, 1]];

                Vector2i edge = new Vector2i(i0, i1);
                Vector2i redge = new Vector2i(i1, i0);

                if (!set.Contains(edge) && !rset.Contains(redge))
                {
                    set.Add(edge);
                    rset.Add(redge);
                    edgeList.Add(i0);
                    edgeList.Add(i1);
                }
            }
        }
    }
}
