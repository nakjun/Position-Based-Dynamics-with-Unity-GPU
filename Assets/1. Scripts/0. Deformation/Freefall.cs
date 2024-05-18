using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Freefall : MonoBehaviour
{
    [Header("Object")]
    public GameObject obj;
    [Header("Simulation Params")]
    public ComputeShader computeShader;
    public float timestep = 0.02f;
    private ComputeBuffer vertexBuffer;
    private int mainKernel;

    private GameObject currObject;
    private Mesh currMesh;

    public void Start()
    {
        // 정육면체의 Vertex 데이터를 초기화하고 버퍼에 저장
        currObject = Instantiate(obj, Vector3.zero, Quaternion.identity);
        currMesh = currObject.GetComponent<MeshFilter>().mesh;
        currObject.transform.parent = this.transform;

        mainKernel = computeShader.FindKernel("Freefall");
    }

    void initBuffers()
    {
        Vector3[] vertices = currMesh.vertices;
        vertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        vertexBuffer.SetData(vertices);

        // Compute Shader에 버퍼 설정
        computeShader.SetBuffer(mainKernel, "vertices", vertexBuffer);
        computeShader.SetFloat("timestep", timestep);
    }

    void dispatchSolver()
    {
        computeShader.Dispatch(mainKernel, Mathf.CeilToInt(vertexBuffer.count / 8.0f), 1, 1);
    }

    void updatePosition()
    {
        Vector3[] vertices = new Vector3[currMesh.vertexCount];
        vertexBuffer.GetData(vertices);
        currMesh.vertices = vertices;
    }

    public void Update()
    {
        initBuffers();
        dispatchSolver();


    }

    void OnDestroy()
    {
        // 자원 정리
        vertexBuffer.Release();
    }
}
