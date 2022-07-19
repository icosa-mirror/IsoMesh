using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hoba.IsoMesh
{
	[ExecuteAlways]
	public class BufferMeshBuilder : MonoBehaviour
	{
		public global::IsoMesh.SDFGroupMeshGenerator _Generator;
		public MeshFilter _Filter;

		private ComputeShader shader;
		private int kernelIndex;
		private GraphicsBuffer vertexBuffer;
		private GraphicsBuffer indexBuffer;
		private GraphicsBuffer argBuffer;

		private const string ShaderResourceName = "UpdateMeshBuffer";
		private const int VERTEX_COUNTER = 0;
		private const int TRIANGLE_COUNTER = 3;
		private const int VERTEX_COUNTER_DIV_64 = 6;
		private const int TRIANGLE_COUNTER_DIV_64 = 9;
		private const int INTERMEDIATE_VERTEX_COUNTER = 12;
		private const int INTERMEDIATE_VERTEX_COUNTER_DIV_64 = 15;

		public ComputeShader UpdateShader
		{
			get
			{
				if ( shader )
					return shader;

				Debug.Log( "Attempting to load resource: " + ShaderResourceName );

				shader = Resources.Load<ComputeShader>( ShaderResourceName );

				if ( !shader )
					Debug.Log( "Failed to load." );
				else
					Debug.Log( "Successfully loaded." );

				return shader;
			}
		}

		private void OnEnable()
		{
			if ( _Generator )
			{
				_Generator.OnDataChanged.AddListener( OnDataChanged );
				_Generator.OnBufferUpdated.AddListener( OnBufferUpdated );
			}
		}

		private void OnDisable()
		{
			if ( _Generator )
			{
				_Generator.OnDataChanged.RemoveListener( OnDataChanged );
				_Generator.OnBufferUpdated.RemoveListener( OnBufferUpdated );
			}

			ReleaseBuffer();

			if (_Filter && _Filter.sharedMesh != null )
			{
				var mesh = _Filter.sharedMesh;
				_Filter.sharedMesh = null;
#if UNITY_EDITOR
				DestroyImmediate( mesh );
#else
                Destroy( mesh );
#endif
			}
		}

		private void Start()
		{

		}


		private void Update()
		{
			if ( _Generator && _Filter && _Filter.sharedMesh == null )
			{
				OnDataChanged();
			}
		}

		private void ReleaseBuffer()
		{
			vertexBuffer?.Dispose();
			vertexBuffer = null;
			indexBuffer?.Dispose();
			indexBuffer = null;
			argBuffer?.Dispose();
			argBuffer = null;
		}

		private void OnDataChanged()
		{
			//Debug.Log("OnDataChanged");
			if ( _Generator == null || _Filter == null )
				return;

			if ( _Filter.sharedMesh == null )
			{
				_Filter.sharedMesh = new Mesh();
			}

			var mesh = _Filter.sharedMesh;
#if true
			mesh.SetVertices( new Vector3[_Generator.VertexBuffer.count] );
			mesh.SetNormals( new Vector3[_Generator.NormalBuffer.count] );
			mesh.SetColors( new Color[_Generator.ColorBuffer.count] );
			mesh.indexFormat = IndexFormat.UInt32;
			mesh.SetIndices( new int[_Generator.TriangleBuffer.count], MeshTopology.Triangles, 0 );
			mesh.UploadMeshData( false );
			/*
            foreach (var attr in mesh.GetVertexAttributes())
                Debug.Log($"{attr.attribute} - d:{attr.dimension} - s:{attr.stream}");
			*/
#else
			mesh.SetVertexBufferParams(generator.VertexBuffer.count,
                new VertexAttributeDescriptor(VertexAttribute.Position),
                new VertexAttributeDescriptor(VertexAttribute.Normal),
                new VertexAttributeDescriptor(VertexAttribute.Color, dimension:4));
            mesh.SetIndexBufferParams(generator.TriangleBuffer.count, IndexFormat.UInt32);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, generator.TriangleBuffer.count));
#endif

			mesh.bounds = new Bounds { extents = _Generator.VoxelSettings.Extents };
			mesh.name = $"SDF: {mesh.vertexCount} vertices";

			mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
			mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;

			ReleaseBuffer();

			vertexBuffer = mesh.GetVertexBuffer( 0 );
			indexBuffer = mesh.GetIndexBuffer();

			kernelIndex = UpdateShader.FindKernel( "Main" );

			UpdateShader.GetKernelThreadGroupSizes( kernelIndex, out uint x, out _, out _ );
			argBuffer = new GraphicsBuffer( GraphicsBuffer.Target.IndirectArguments, 3, sizeof( int ) );
			argBuffer.SetData( new int[3] { Mathf.CeilToInt( mesh.vertexCount / (float)x ), 1, 1 } );

			UpdateShader.SetBuffer( kernelIndex, "VertexBuffer", _Generator.VertexBuffer );
			UpdateShader.SetBuffer( kernelIndex, "NormalBuffer", _Generator.NormalBuffer );
			UpdateShader.SetBuffer( kernelIndex, "ColorBuffer", _Generator.ColorBuffer );
			UpdateShader.SetBuffer( kernelIndex, "IndexBuffer", _Generator.TriangleBuffer );
			UpdateShader.SetBuffer( kernelIndex, "MeshVertexBuffer", vertexBuffer );
			UpdateShader.SetBuffer( kernelIndex, "MeshIndexBuffer", indexBuffer );
		}

		private void OnBufferUpdated()
		{
			//Debug.Log("OnBufferUpdated");
			if ( _Generator == null || _Filter == null )
				return;

			if ( argBuffer == null )
			{
				OnDataChanged();
			}

			var data = new int[_Generator.CounterBuffer.count];
			_Generator.CounterBuffer.GetData( data );
			var vertexCount = data[VERTEX_COUNTER] + data[INTERMEDIATE_VERTEX_COUNTER];
			var indexCount = data[TRIANGLE_COUNTER] * 3;
			//Debug.Log($"v:{vertexCount}, i:{indexCount}");

			if ( indexBuffer.count != indexCount )
			{
				//Debug.Log($"reset mesh params: {indexBuffer.count} -> {indexCount}");
				var mesh = _Filter.sharedMesh;
				/*
                mesh.SetVertexBufferParams(vertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position),
                    new VertexAttributeDescriptor(VertexAttribute.Normal),
                    new VertexAttributeDescriptor(VertexAttribute.Color, dimension: 4));
                */
				mesh.SetIndexBufferParams( indexCount, IndexFormat.UInt32 );
				mesh.subMeshCount = 1;
				mesh.SetSubMesh( 0, new SubMeshDescriptor( 0, indexCount ) );
				//Debug.Log($"check: {indexBuffer.count}");
			}

			UpdateShader.SetInt( "indexCount", indexCount );
			UpdateShader.DispatchIndirect( kernelIndex, argBuffer );
		}
	}

}
