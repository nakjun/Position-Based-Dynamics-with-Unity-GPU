/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;

public class ParticleController : MonoBehaviour
{
	public Material _material;
	public ComputeShader _computeShader;
	public int vertexCount = 1000;

    int _awakeKernel;
	int _updateKernel;	
	ComputeBuffer _vertexBuffer;
    Vector4[] positions;
	static class ShaderIDs
	{
		public static int vertices = Shader.PropertyToID( "_Vertices" );
		public static int deltaTime = Shader.PropertyToID( "_DeltaTime" );
	}


	void Awake()
	{
        positions = new Vector4[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 position = Random.insideUnitCircle * 10f;            
            positions[i] = new Vector4(position.x, position.y, position.z, 1.0f);
        }

		// Create vertex buffer.
		_vertexBuffer = new ComputeBuffer( vertexCount, sizeof(float)*4 );
        _vertexBuffer.SetData(positions);
		// Create comute shader and find kernels.		
		_awakeKernel = _computeShader.FindKernel( "Awake" );
		_updateKernel = _computeShader.FindKernel( "Update" );

		// Set shader resources.
		_computeShader.SetBuffer( _awakeKernel, ShaderIDs.vertices, _vertexBuffer );
		_computeShader.SetBuffer( _updateKernel, ShaderIDs.vertices, _vertexBuffer );
		_material.SetBuffer( ShaderIDs.vertices, _vertexBuffer );

		// Create initial vertex data.
		_computeShader.Dispatch( _awakeKernel, vertexCount, 1, 1 );
	}


	void OnDestroy()
	{
		_vertexBuffer.Release();		
	}


	void Update()
	{
		// Update vertices.
		_computeShader.SetFloat( ShaderIDs.deltaTime, Time.deltaTime );
		_computeShader.Dispatch( _updateKernel, _vertexBuffer.count, 1, 1 );
	}


	void OnRenderObject()
	{
		// Draw.
		_material.SetPass( 0 );
		Graphics.DrawProceduralNow( MeshTopology.Triangles, _vertexBuffer.count );
	}
}