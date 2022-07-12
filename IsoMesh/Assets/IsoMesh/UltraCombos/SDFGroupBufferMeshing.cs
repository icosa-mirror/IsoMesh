using IsoMesh;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltraCombos
{
    [ExecuteAlways]
    public class SDFGroupBufferMeshing : MonoBehaviour
    {
        public SDFGroupMeshGenerator generator;
        public MeshFilter filter;

        private ComputeShader shader;
        private int kernelIndex;
        private GraphicsBuffer vertexBuffer;
        private GraphicsBuffer indexBuffer;
        private GraphicsBuffer args;

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
                if (shader)
                    return shader;

                Debug.Log("Attempting to load resource: " + ShaderResourceName);

                shader = Resources.Load<ComputeShader>(ShaderResourceName);

                if (!shader)
                    Debug.Log("Failed to load.");
                else
                    Debug.Log("Successfully loaded.");

                return shader;
            }
        }

        private void OnEnable()
        {
            if (generator)
            {
                generator.OnDataChanged.AddListener(OnDataChanged);
                generator.OnBufferUpdated.AddListener(OnBufferUpdated);
            }
        }

        private void OnDisable()
        {
            if (generator)
            {
                generator.OnDataChanged.RemoveListener(OnDataChanged);
                generator.OnBufferUpdated.RemoveListener(OnBufferUpdated);
            }

            ReleaseBuffer();

            if (filter.sharedMesh != null)
            {
                var mesh = filter.sharedMesh;
                filter.sharedMesh = null;
#if UNITY_EDITOR
                DestroyImmediate(mesh);
#else
                Destroy(mesh);
#endif
            }
        }

        private void Start()
        {

        }


        private void Update()
        {
            if (generator && filter && filter.sharedMesh == null)
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
            args?.Dispose();
            args = null;
        }

        private void OnDataChanged()
        {
            //Debug.Log("OnDataChanged");
            if (generator == null || filter == null)
                return;

            if (filter.sharedMesh == null)
            {
                filter.sharedMesh = new Mesh();
            }

            var mesh = filter.sharedMesh;
            /*
            mesh.SetVertices(new Vector3[generator.VertexBuffer.count]);
            mesh.SetNormals(new Vector3[generator.NormalBuffer.count]);
            mesh.SetColors(new Color[generator.ColorBuffer.count]);
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetIndices(new int[generator.TriangleBuffer.count], MeshTopology.Triangles, 0);
            mesh.UploadMeshData(true);
            foreach (var attr in mesh.GetVertexAttributes())
                Debug.Log($"{attr.attribute} - d:{attr.dimension} - s:{attr.stream}");
            */
            mesh.SetVertexBufferParams(generator.VertexBuffer.count,
                new VertexAttributeDescriptor(VertexAttribute.Position),
                new VertexAttributeDescriptor(VertexAttribute.Normal),
                new VertexAttributeDescriptor(VertexAttribute.Color, dimension:4));
            mesh.SetIndexBufferParams(generator.TriangleBuffer.count, IndexFormat.UInt32);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, generator.TriangleBuffer.count));

			mesh.bounds = new Bounds { extents = generator.VoxelSettings.Extents };
            mesh.name = $"SDF: {mesh.vertexCount} vertices";

            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
            mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;

            ReleaseBuffer();

            vertexBuffer = mesh.GetVertexBuffer(0);
            indexBuffer = mesh.GetIndexBuffer();

            kernelIndex = UpdateShader.FindKernel("Main");

            UpdateShader.GetKernelThreadGroupSizes(kernelIndex, out uint x, out _, out _);
            args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 3, sizeof(int));
            args.SetData(new int[3] { Mathf.CeilToInt(mesh.vertexCount / (float)x), 1, 1 });
            
            UpdateShader.SetBuffer(kernelIndex, "VertexBuffer", generator.VertexBuffer);
            UpdateShader.SetBuffer(kernelIndex, "NormalBuffer", generator.NormalBuffer);
            UpdateShader.SetBuffer(kernelIndex, "ColorBuffer", generator.ColorBuffer);
            UpdateShader.SetBuffer(kernelIndex, "IndexBuffer", generator.TriangleBuffer);
            UpdateShader.SetBuffer(kernelIndex, "MeshVertexBuffer", vertexBuffer);
            UpdateShader.SetBuffer(kernelIndex, "MeshIndexBuffer", indexBuffer);
        }

        private void OnBufferUpdated()
        {
            //Debug.Log("OnBufferUpdated");
            if (generator == null || filter == null)
                return;

            if (args == null)
            {
                OnDataChanged();
            }

            var data = new int[generator.CounterBuffer.count];
            generator.CounterBuffer.GetData(data);
            var vertexCount = data[VERTEX_COUNTER] + data[INTERMEDIATE_VERTEX_COUNTER];
            var indexCount = data[TRIANGLE_COUNTER] * 3;
            //Debug.Log($"v:{vertexCount}, i:{indexCount}");

            if (indexBuffer.count != indexCount)
            {
                //Debug.Log($"reset mesh params: {indexBuffer.count} -> {indexCount}");
                var mesh = filter.sharedMesh;
                /*
                mesh.SetVertexBufferParams(vertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position),
                    new VertexAttributeDescriptor(VertexAttribute.Normal),
                    new VertexAttributeDescriptor(VertexAttribute.Color, dimension: 4));
                */
                mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount));
                //Debug.Log($"check: {indexBuffer.count}");
            }
            
            UpdateShader.SetInt("indexCount", indexCount);
            UpdateShader.DispatchIndirect(kernelIndex, args);
        }
    }

}
