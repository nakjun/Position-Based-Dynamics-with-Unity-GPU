using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Common.Mathematics.LinearAlgebra;
using System.Linq;

public class ClothModel : MonoBehaviour
{
    [Header("Create Cloth")]
    public bool createMode;
    public int xSize = 8;
    public int ySize = 8;
    public float width = 10.0f;
    public float height = 10.0f;
    public bool drawLines = true;
    public bool drawNodes = false;
    private int numParticles;

    public GameObject node;
    private Vector3[] positions;
    private Vector3[] velocities;
    private Vector3[] deltaPositionArray;
    private PBDStruct.UInt3Struct[] deltaPosUintArray;
    private int[] deltaCounterArray;
    private int[] isSimulated;
    private int[] indices;
    private List<int> edgeList;
    private Triangle[] triangles;
    public ComputeShader shader;

    [Header("Object Mesh")]
    public Mesh mesh;
    public GameObject simulationObject;

    /* Position Based Dynamics */
    [Header("Simulation Parameters")]
    public int numDistanceConstraints;
    private PBDStruct.DistanceConstraintStruct[] distanceConstraints;
    public float distanceCompressionStiffness = 0.8f;
    public float distanceStretchStiffness = 0.8f;

    public int numBendingConstraints;
    private PBDStruct.BendingConstraintStruct[] bendingConstraints;
    public float bendingCompressionStiffness = 0.8f;
    public float bendingStretchStiffness = 0.8f;

    public float timestep = 0.02f;
    public int iterationNum = 5;
    private float nextFrameTime = 0f;

    [Header("External Forces")]
    public Vector3 gravity = new Vector3(0, -9.8f, 0);

    /* Dispatch Parameter values */
    private int numGroups_Vertices;
    private int numGroups_DistanceConstraints;
    private int numGroups_AllConstraints;
    public int workGroupSize = 1024;

    /* Compute Buffer */
    private ComputeBuffer positionsBuffer;
    private ComputeBuffer simulationFlagBuffer;
    private ComputeBuffer projectedPositionsBuffer;
    private ComputeBuffer velocitiesBuffer;
    private ComputeBuffer deltaPositionsBuffer;
    private ComputeBuffer deltaPositionsUIntBuffer;
    private ComputeBuffer deltaCounterBuffer;
    private ComputeBuffer distanceConstraintsBuffer;
    private ComputeBuffer bendingConstraintsBuffer;

    /* Kernel ID */
    private int applyExternalForcesKernel;
    private int dampVelocitiesKernel;
    private int applyExplicitEulerKernel;
    private int projectDistanceConstraintDeltasKernel;
    private int updatePositionsKernel;
    private int averageConstraintDeltasKernel;

    private Material material;
    // Start is called before the first frame update
    void Awake()
    {
        if (createMode)
            CreateModel();
        else
        {
            mesh = simulationObject.GetComponent<MeshFilter>().mesh;
            SetModelInformations();
        }
        material = this.GetComponent<MeshRenderer>().materials[0];
        SetupPBD();
        SetupComputeBuffers();
    }

    void OnDestroy()
    {
        if (positionsBuffer != null) positionsBuffer.Release();
        if (simulationFlagBuffer != null) simulationFlagBuffer.Release();
        if (projectedPositionsBuffer != null) projectedPositionsBuffer.Release();
        if (velocitiesBuffer != null) velocitiesBuffer.Release();
        if (deltaPositionsBuffer != null) deltaPositionsBuffer.Release();
        if (deltaPositionsUIntBuffer != null) deltaPositionsUIntBuffer.Release();
        if (deltaCounterBuffer != null) deltaCounterBuffer.Release();
        if (distanceConstraintsBuffer != null) distanceConstraintsBuffer.Release();
    }

    // Update is called once per frame
    void Update()
    {
        SetupStiffness();

        nextFrameTime += Time.deltaTime;
        int iter = 0;
        while (nextFrameTime > 0)
        {
            if (nextFrameTime < timestep)
            {
                break;
            }
            float dt = Mathf.Min(nextFrameTime, timestep);
            nextFrameTime -= dt;
            iter++;

            shader.SetFloat("dt", dt);

            ApplyExternalForces();
            //DampVezlocities();
            ApplyExplicitEuler();

            for (int j = 0; j < iterationNum; j++)
            {
                ProjectDistanceConstraintDeltas();

                AverageConstraintDeltas();
            }

            UpdatePositions();

        }

        UpdateMesh();

        if (drawLines)
        {
            DrawLines();
        }
    }

    public void SetModelInformations()
    {
        positions = mesh.vertices;
        numParticles = mesh.vertices.Length;
        CreateEdge();
        CreateTriangles();
    }

    private void UpdatePositions()
    {
        shader.Dispatch(updatePositionsKernel, numGroups_Vertices, 1, 1);
    }

    private void UpdateMesh()
    {
        // // get data from GPU back to CPU
        positionsBuffer.GetData(positions);
        velocitiesBuffer.GetData(velocities);

        // update everything into Unity
        mesh.vertices = positions;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // material.SetBuffer("positions", positionsBuffer);
        // Graphics.DrawProcedural(material, mesh.bounds, MeshTopology.Triangles, positions.Length);
    }

    public void DrawLines()
    {
        for (int i = 0; i < edgeList.Count;)
        {
            Debug.DrawLine(positions[edgeList[i]], positions[edgeList[i + 1]], Color.white);
            i = i + 2;
        }
    }

    public void CreateModel()
    {
        numParticles = (xSize + 1) * (ySize + 1);

        //Create Particles
        CreateParticles();
        CreateEdge();
        SetupMeshInformation();
        CreateTriangles();

        Debug.Log("Create Success");
        Debug.Log("# of particles : " + positions.Length);
        Debug.Log("# of indices : " + indices.Length);
        Debug.Log("# of edges : " + edgeList.Count);
    }

    public void SetupMeshInformation()
    {
        if (createMode)
        {
            if (mesh == null)
                mesh = new Mesh();

            mesh.vertices = positions;
            mesh.triangles = indices;
            mesh.RecalculateNormals();

            this.GetComponent<MeshFilter>().mesh = mesh;
        }
    }

    public void CreateParticles()
    {
        positions = new Vector3[(xSize + 1) * (ySize + 1)];
        indices = new int[xSize * ySize * 2 * 3];

        float dx = width / (xSize + 1);
        float dy = height / (ySize + 1);

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



        if (drawNodes)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                Instantiate(node, positions[i], Quaternion.identity);
            }
        }
    }
    public void CreateEdge()
    {
        int[,] edges = new int[,]
        {
            {0,1}, {1,2}, {2,0}
        };

        int numTris = 0;
        if (createMode)
            numTris = indices.Length / 3;
        else
            numTris = mesh.triangles.Length / 3;

        edgeList = new List<int>();
        HashSet<Vector2i> set = new HashSet<Vector2i>();
        HashSet<Vector2i> rset = new HashSet<Vector2i>();

        for (int n = 0; n < numTris; n++)
        {
            for (int i = 0; i < 3; i++)
            {
                int i0, i1;


                if (createMode)
                {
                    i0 = indices[3 * n + edges[i, 0]];
                    i1 = indices[3 * n + edges[i, 1]];
                }
                else
                {
                    i0 = mesh.triangles[3 * n + edges[i, 0]];
                    i1 = mesh.triangles[3 * n + edges[i, 1]];
                }

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
    public void CreateTriangles()
    {
        int[] triangleIds = mesh.GetTriangles(0);

        triangles = new Triangle[triangleIds.Length / 3];
        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = new Triangle(triangleIds[i * 3], triangleIds[i * 3 + 1], triangleIds[i * 3 + 2]);
        }
    }

    public void SetupStiffness()
    {
        positionsBuffer.SetData(positions);
        velocitiesBuffer.SetData(velocities);

        shader.SetFloat("stretchStiffness", distanceStretchStiffness);
        shader.SetFloat("compressionStiffness", distanceCompressionStiffness);
    }

    public void SetupComputeBuffers()
    {
        /* Create Buffer */
        isSimulated = new int[numParticles];
        for (int i = 0; i < numParticles; i++)
        {
            isSimulated[i] = 1;
        }
        for (int i = 0; i < xSize; i++)
        {
            isSimulated[i] = 0;
        }
        velocities = new Vector3[numParticles];
        deltaPositionArray = new Vector3[numParticles];
        deltaPosUintArray = new PBDStruct.UInt3Struct[numParticles];
        deltaCounterArray = new int[numParticles];

        /* Create Compute Buffer */
        positionsBuffer = new ComputeBuffer(numParticles, sizeof(float) * 3);
        simulationFlagBuffer = new ComputeBuffer(numParticles, sizeof(int));
        projectedPositionsBuffer = new ComputeBuffer(numParticles, sizeof(float) * 3);
        velocitiesBuffer = new ComputeBuffer(numParticles, sizeof(float) * 3);
        deltaPositionsBuffer = new ComputeBuffer(numParticles, sizeof(float) * 3);
        deltaPositionsUIntBuffer = new ComputeBuffer(numParticles, sizeof(uint) * 3);
        deltaCounterBuffer = new ComputeBuffer(numParticles, sizeof(int));
        if (numDistanceConstraints > 0) distanceConstraintsBuffer = new ComputeBuffer(numDistanceConstraints, sizeof(float) + sizeof(int) * 2);

        /* Set Compute Buffer */
        positionsBuffer.SetData(positions);
        simulationFlagBuffer.SetData(isSimulated);
        projectedPositionsBuffer.SetData(positions);
        velocitiesBuffer.SetData(velocities);
        deltaPositionsBuffer.SetData(deltaPositionArray);
        deltaPositionsUIntBuffer.SetData(deltaPosUintArray);
        deltaCounterBuffer.SetData(deltaCounterArray);
        if (numDistanceConstraints > 0) distanceConstraintsBuffer.SetData(distanceConstraints);

        /* Find Kernel's ID */
        applyExternalForcesKernel = shader.FindKernel("ApplyExternalForces");
        dampVelocitiesKernel = shader.FindKernel("DampVelocities");
        applyExplicitEulerKernel = shader.FindKernel("ApplyExplicitEuler");
        projectDistanceConstraintDeltasKernel = shader.FindKernel("ProjectDistanceConstraintDeltas");
        updatePositionsKernel = shader.FindKernel("UpdatePositions");
        averageConstraintDeltasKernel = shader.FindKernel("AverageConstraintDeltas");

        /* Set Data to Compute Shader */
        shader.SetInt("numParticles", numParticles);
        shader.SetInt("numDistanceConstraints", numDistanceConstraints);
        shader.SetFloat("invMass", 1.0f);
        shader.SetVector("gravity", gravity);

        /* Bind Buffer to Compute Shader */
        shader.SetBuffer(applyExternalForcesKernel, "isSimulated", simulationFlagBuffer);
        shader.SetBuffer(applyExternalForcesKernel, "velocities", velocitiesBuffer);

        shader.SetBuffer(dampVelocitiesKernel, "isSimulated", simulationFlagBuffer);
        shader.SetBuffer(dampVelocitiesKernel, "velocities", velocitiesBuffer);

        shader.SetBuffer(applyExplicitEulerKernel, "isSimulated", simulationFlagBuffer);
        shader.SetBuffer(applyExplicitEulerKernel, "positions", positionsBuffer);
        shader.SetBuffer(applyExplicitEulerKernel, "projectedPositions", projectedPositionsBuffer);
        shader.SetBuffer(applyExplicitEulerKernel, "velocities", velocitiesBuffer);

        shader.SetBuffer(averageConstraintDeltasKernel, "isSimulated", simulationFlagBuffer);
        shader.SetBuffer(averageConstraintDeltasKernel, "projectedPositions", projectedPositionsBuffer);
        shader.SetBuffer(averageConstraintDeltasKernel, "deltaPos", deltaPositionsBuffer);
        shader.SetBuffer(averageConstraintDeltasKernel, "deltaPosAsInt", deltaPositionsUIntBuffer);
        shader.SetBuffer(averageConstraintDeltasKernel, "deltaCount", deltaCounterBuffer);

        shader.SetBuffer(updatePositionsKernel, "isSimulated", simulationFlagBuffer);
        shader.SetBuffer(updatePositionsKernel, "positions", positionsBuffer);
        shader.SetBuffer(updatePositionsKernel, "projectedPositions", projectedPositionsBuffer);
        shader.SetBuffer(updatePositionsKernel, "velocities", velocitiesBuffer);

        if (numDistanceConstraints > 0)
        {
            shader.SetBuffer(projectDistanceConstraintDeltasKernel, "projectedPositions", projectedPositionsBuffer);
            shader.SetBuffer(projectDistanceConstraintDeltasKernel, "deltaPos", deltaPositionsBuffer);
            shader.SetBuffer(projectDistanceConstraintDeltasKernel, "deltaPosAsInt", deltaPositionsUIntBuffer);
            shader.SetBuffer(projectDistanceConstraintDeltasKernel, "deltaCount", deltaCounterBuffer);
            shader.SetBuffer(projectDistanceConstraintDeltasKernel, "distanceConstraints", distanceConstraintsBuffer);
        }

        /* Set Dispatch Parameter values */
        numGroups_Vertices = Mathf.CeilToInt((float)numParticles / workGroupSize);
        numGroups_DistanceConstraints = Mathf.CeilToInt((float)numDistanceConstraints / workGroupSize);
    }

    public void SetupPBD()
    {
        AddDistanceConstraints();
        AddBendingConstraints();
    }

    private void AddDistanceConstraints()
    {
        numDistanceConstraints = edgeList.Count;
        distanceConstraints = new PBDStruct.DistanceConstraintStruct[edgeList.Count];
        int j = 0;
        for (int i = 0; i < edgeList.Count;)
        {
            PBDStruct.EdgeStruct edge;
            edge.startIndex = edgeList[i];
            edge.endIndex = edgeList[i + 1];
            distanceConstraints[j].edge = edge;
            distanceConstraints[j].restLength = Vector3.Distance(positions[edge.startIndex], positions[edge.endIndex]);
            j++;
            i = i + 2;
        }
    }
    private void AddBendingConstraints()
    {
        Dictionary<Edge, List<Triangle>> wingEdges = new Dictionary<Edge, List<Triangle>>(new EdgeComparer());

        // map edges to all of the faces to which they are connected
        foreach (Triangle tri in triangles)
        {
            Edge e1 = new Edge(tri.vertices[0], tri.vertices[1]);
            if (wingEdges.ContainsKey(e1) && !wingEdges[e1].Contains(tri))
            {
                wingEdges[e1].Add(tri);
            }
            else
            {
                List<Triangle> tris = new List<Triangle>();
                tris.Add(tri);
                wingEdges.Add(e1, tris);
            }

            Edge e2 = new Edge(tri.vertices[0], tri.vertices[2]);
            if (wingEdges.ContainsKey(e2) && !wingEdges[e2].Contains(tri))
            {
                wingEdges[e2].Add(tri);
            }
            else
            {
                List<Triangle> tris = new List<Triangle>();
                tris.Add(tri);
                wingEdges.Add(e2, tris);
            }

            Edge e3 = new Edge(tri.vertices[1], tri.vertices[2]);
            if (wingEdges.ContainsKey(e3) && !wingEdges[e3].Contains(tri))
            {
                wingEdges[e3].Add(tri);
            }
            else
            {
                List<Triangle> tris = new List<Triangle>();
                tris.Add(tri);
                wingEdges.Add(e3, tris);
            }
        }

        // wingEdges are edges with 2 occurences,
        // so we need to remove the lower frequency ones
        List<Edge> keyList = wingEdges.Keys.ToList();
        foreach (Edge e in keyList)
        {
            if (wingEdges[e].Count < 2)
            {
                wingEdges.Remove(e);
            }
        }

        numBendingConstraints = wingEdges.Count;
        bendingConstraints = new PBDStruct.BendingConstraintStruct[numBendingConstraints];
        int j = 0;
        foreach (Edge wingEdge in wingEdges.Keys)
        {
            // wingEdges are indexed like in the Bridson,
            // Simulation of Clothing with Folds and Wrinkles paper
            //    3
            //    ^
            // 0  |  1
            //    2
            //

            int[] indices = new int[4];
            indices[2] = wingEdge.startIndex;
            indices[3] = wingEdge.endIndex;

            int b = 0;
            foreach (Triangle tri in wingEdges[wingEdge])
            {
                for (int i = 0; i < 3; i++)
                {
                    int point = tri.vertices[i];
                    if (point != indices[2] && point != indices[3])
                    {
                        //tri #1
                        if (b == 0)
                        {
                            indices[0] = point;
                            break;
                        }
                        //tri #2
                        else if (b == 1)
                        {
                            indices[1] = point;
                            break;
                        }
                    }
                }
                b++;
            }

            bendingConstraints[j].index0 = indices[0];
            bendingConstraints[j].index1 = indices[1];
            bendingConstraints[j].index2 = indices[2];
            bendingConstraints[j].index3 = indices[3];
            Vector3 p0 = positions[indices[0]];
            Vector3 p1 = positions[indices[1]];
            Vector3 p2 = positions[indices[2]];
            Vector3 p3 = positions[indices[3]];

            Vector3 n1 = (Vector3.Cross(p2 - p0, p3 - p0)).normalized;
            Vector3 n2 = (Vector3.Cross(p3 - p1, p2 - p1)).normalized;

            float d = Vector3.Dot(n1, n2);
            d = Mathf.Clamp(d, -1.0f, 1.0f);
            bendingConstraints[j].restAngle = Mathf.Acos(d);

            j++;
        }
    }

    public void ApplyExternalForces()
    {
        shader.Dispatch(applyExternalForcesKernel, numGroups_Vertices, 1, 1);
    }
    private void DampVelocities()
    {
        shader.Dispatch(dampVelocitiesKernel, numGroups_Vertices, 1, 1);
    }
    private void ApplyExplicitEuler()
    {
        shader.Dispatch(applyExplicitEulerKernel, numGroups_Vertices, 1, 1);
    }

    private void ProjectDistanceConstraintDeltas()
    {
        if (numDistanceConstraints > 0)
            shader.Dispatch(projectDistanceConstraintDeltasKernel, numGroups_DistanceConstraints, 1, 1);
    }

    private void AverageConstraintDeltas()
    {
        shader.Dispatch(averageConstraintDeltasKernel, numGroups_Vertices, 1, 1);
    }
}
public class Edge
{
    public int startIndex;
    public int endIndex;

    public Edge(int start, int end)
    {
        startIndex = Mathf.Min(start, end);
        endIndex = Mathf.Max(start, end);
    }
}

public class Triangle
{
    public int[] vertices;

    public Triangle(int v0, int v1, int v2)
    {
        vertices = new int[3];
        vertices[0] = v0;
        vertices[1] = v1;
        vertices[2] = v2;
    }
}

public class EdgeComparer : EqualityComparer<Edge>
{
    public override int GetHashCode(Edge obj)
    {
        return obj.startIndex * 10000 + obj.endIndex;
    }

    public override bool Equals(Edge x, Edge y)
    {
        return x.startIndex == y.startIndex && x.endIndex == y.endIndex;
    }
}