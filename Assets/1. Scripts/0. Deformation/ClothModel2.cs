using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Common.Mathematics.LinearAlgebra;
using System.Linq;

public class ClothModel2 : MonoBehaviour
{
    public ComputeShader computeShader;
    public Mesh mesh;
    public Texture texture;
    public int vertexCount = 10000;

    private ComputeBuffer inputBuffer;
    private ComputeBuffer outputBuffer;

    private int computePositionKernel;

    private MeshRenderer meshRenderer;

    void Start()
    {
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        
        int[] indices = new int[vertexCount];
        Vector3[] positions = new Vector3[vertexCount];
        
        for (int i = 0; i < vertexCount; i++)
        {
            positions[i] = Random.onUnitSphere;
            indices[i] = i;
        }
        mesh.SetVertices(positions);
        mesh.SetIndices(indices, MeshTopology.Points, 0);

        // Create the input buffer and initialize it with the mesh data.
        inputBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        Vector3[] vertices = mesh.vertices.Select(v => new Vector3(v.x, v.y, v.z)).ToArray();
        inputBuffer.SetData(vertices);

        // Create the output buffer.
        outputBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3, ComputeBufferType.Counter);
        computePositionKernel = computeShader.FindKernel("ComputePosition");

        // Bind the buffers to the compute shader.
        computeShader.SetBuffer(computePositionKernel, "verticesIn", inputBuffer);
        computeShader.SetBuffer(computePositionKernel, "verticesOut", outputBuffer);

        // Create a MeshRenderer and assign the mesh to the MeshFilter component
        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // Set the material properties.
        Material material = new Material(Shader.Find("Custom/pipelineshader"));
        material.SetTexture("_MainTex", texture);
        meshRenderer.material = material;
    }

    void Update()
    {
        // Dispatch the compute shader to process the input buffer and output buffer.
        computeShader.Dispatch(computePositionKernel, vertexCount / 64, 1, 1);
        // Draw the mesh with the material.
        Graphics.DrawMesh(mesh, Matrix4x4.identity, meshRenderer.material, 0);
    }

    void OnDestroy()
    {
        // Release the compute buffers.
        inputBuffer.Release();
        outputBuffer.Release();
    }
}
