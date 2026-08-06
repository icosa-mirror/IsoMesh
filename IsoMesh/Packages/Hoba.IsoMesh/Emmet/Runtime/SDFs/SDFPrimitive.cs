using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoMesh
{
    [ExecuteInEditMode]
    public class SDFPrimitive : SDFObject
    {
        [SerializeField]
        private SDFPrimitiveType m_type;
        public SDFPrimitiveType Type => m_type;

        [SerializeField]
        private Vector4 m_data = new Vector4(1f, 1f, 1f, 0f);
        public Vector4 Data => m_data;

        [SerializeField]
        protected SDFCombineType m_operation;
        public SDFCombineType Operation => m_operation;

        [SerializeField]
        protected bool m_flip = false;
        public bool Flip => m_flip;

        public void Configure(
            SDFPrimitiveType type, Vector4 data, SDFCombineType operation,
            float smoothing = 0f, bool flip = false)
        {
            m_type = type;
            m_data = SanitizeData(type, data);
            m_operation = operation;
            m_smoothing = Mathf.Max(0f, smoothing);
            m_flip = flip;
            SetDirty();
        }

        public void SetType(SDFPrimitiveType type)
        {
            m_type = type;
            m_data = SanitizeData(type, m_data);
            SetDirty();
        }

        public void SetData(Vector4 data)
        {
            m_data = SanitizeData(m_type, data);
            SetDirty();
        }

        public void SetOperation(SDFCombineType operation)
        {
            m_operation = operation;
            SetDirty();
        }

        public void SetFlip(bool flip)
        {
            m_flip = flip;
            SetDirty();
        }

        private static Vector4 SanitizeData(SDFPrimitiveType type, Vector4 data)
        {
            switch (type)
            {
                case SDFPrimitiveType.Sphere:
                    return new Vector4(Mathf.Max(0f, data.x), 0f, 0f, 0f);
                case SDFPrimitiveType.Torus:
                    return new Vector4(
                        Mathf.Max(0f, data.x), Mathf.Max(0f, data.y), 0f, 0f);
                case SDFPrimitiveType.Cuboid:
                    return new Vector4(
                        Mathf.Max(0f, data.x), Mathf.Max(0f, data.y),
                        Mathf.Max(0f, data.z), 0f);
                case SDFPrimitiveType.BoxFrame:
                    return new Vector4(
                        Mathf.Max(0f, data.x), Mathf.Max(0f, data.y),
                        Mathf.Max(0f, data.z), Mathf.Max(0f, data.w));
                case SDFPrimitiveType.Cylinder:
                    return new Vector4(
                        Mathf.Max(0f, data.x), Mathf.Max(0f, data.y), 0f, 0f);
                case SDFPrimitiveType.Capsule:
                    return new Vector4(
                        Mathf.Max(0f, data.x), Mathf.Max(0f, data.y), 0f, 0f);
                case SDFPrimitiveType.Ellipsoid:
                    return new Vector4(
                        Mathf.Max(0f, data.x), Mathf.Max(0f, data.y),
                        Mathf.Max(0f, data.z), 0f);
                case SDFPrimitiveType.Cone:
                case SDFPrimitiveType.Pyramid:
                    return new Vector4(
                        Mathf.Max(0f, data.x), Mathf.Max(0f, data.y), 0f, 0f);
                default:
                    return data;
            }
        }

        protected override void TryDeregister()
        {
            base.TryDeregister();

            Group?.Deregister(this);
        }

        protected override void TryRegister()
        {
            base.TryDeregister();

            Group?.Register(this);
        }

        public Vector3 CubeBounds
        {
            get
            {
                if (m_type == SDFPrimitiveType.BoxFrame || m_type == SDFPrimitiveType.Cuboid)
                    return new Vector3(m_data.x, m_data.y, m_data.z);

                return Vector3.zero;
            }
        }

        public float SphereRadius
        {
            get
            {
                if (m_type == SDFPrimitiveType.Sphere)
                    return m_data.x;

                return 0f;
            }
        }

        public void SetCubeBounds(Vector3 vec)
        {
            if (m_type == SDFPrimitiveType.BoxFrame || m_type == SDFPrimitiveType.Cuboid)
            {
                m_data = new Vector4(vec.x, vec.y, vec.z, m_data.w);
                SetDirty();
            }
        }

        public void SetSphereRadius(float radius)
        {
            if (m_type == SDFPrimitiveType.Sphere)
            {
                m_data = m_data.SetX(Mathf.Max(0f, radius));
                SetDirty();
            }
        }

        public override SDFGPUData GetSDFGPUData(int sampleStartIndex = -1, int uvStartIndex = -1)
        {
            // note: has room for six more floats (minbounds, maxbounds)
            return new SDFGPUData
            {
                Type = (int)m_type + 1,
                Data = m_data,
                Transform = transform.worldToLocalMatrix,
                CombineType = (int)m_operation,
                Flip = m_flip ? -1 : 1,
                Smoothing = Mathf.Max(MIN_SMOOTHING, m_smoothing)
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color col = Operation == SDFCombineType.SmoothSubtract ? Color.red : Color.blue;
			using ( new Handles.DrawingScope( col, transform.localToWorldMatrix ) )
			{
				switch ( Type )
				{
					case SDFPrimitiveType.BoxFrame:
					case SDFPrimitiveType.Cuboid:
						Handles.DrawWireCube( Vector3.zero, m_data.XYZ() * 2f );
						break;
					//case SDFPrimitiveType.BoxFrame:
					//    Handles.DrawWireCube(Vector3.zero, data.XYZ() * 2f);
					//    break;
					case SDFPrimitiveType.Sphere:
						{
							Handles.DrawWireDisc( Vector3.zero, Vector3.up, m_data.x );
							Handles.DrawWireDisc( Vector3.zero, Vector3.right, m_data.x );
							Handles.DrawWireDisc( Vector3.zero, Vector3.forward, m_data.x );
						}
						break;
					case SDFPrimitiveType.Capsule:
						{
							float radius = m_data.x;
							float halfSegment = m_data.y;
							Vector3 top = Vector3.up * halfSegment;
							Vector3 bottom = Vector3.down * halfSegment;
							Handles.DrawWireDisc(top, Vector3.up, radius);
							Handles.DrawWireDisc(top, Vector3.right, radius);
							Handles.DrawWireDisc(top, Vector3.forward, radius);
							Handles.DrawWireDisc(bottom, Vector3.up, radius);
							Handles.DrawWireDisc(bottom, Vector3.right, radius);
							Handles.DrawWireDisc(bottom, Vector3.forward, radius);
							Handles.DrawLine(top + Vector3.right * radius,
								bottom + Vector3.right * radius);
							Handles.DrawLine(top + Vector3.left * radius,
								bottom + Vector3.left * radius);
							Handles.DrawLine(top + Vector3.forward * radius,
								bottom + Vector3.forward * radius);
							Handles.DrawLine(top + Vector3.back * radius,
								bottom + Vector3.back * radius);
						}
						break;
					case SDFPrimitiveType.Ellipsoid:
						using (new Handles.DrawingScope(
							col, transform.localToWorldMatrix * Matrix4x4.Scale(m_data.XYZ())))
						{
							Handles.DrawWireDisc(Vector3.zero, Vector3.up, 1f);
							Handles.DrawWireDisc(Vector3.zero, Vector3.right, 1f);
							Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 1f);
						}
						break;
					case SDFPrimitiveType.Cone:
						{
							float radius = m_data.x;
							float halfHeight = m_data.y;
							Vector3 baseCenter = Vector3.down * halfHeight;
							Vector3 apex = Vector3.up * halfHeight;
							Handles.DrawWireDisc(baseCenter, Vector3.up, radius);
							Handles.DrawLine(apex, baseCenter + Vector3.right * radius);
							Handles.DrawLine(apex, baseCenter + Vector3.left * radius);
							Handles.DrawLine(apex, baseCenter + Vector3.forward * radius);
							Handles.DrawLine(apex, baseCenter + Vector3.back * radius);
						}
						break;
					case SDFPrimitiveType.Pyramid:
						{
							float halfWidth = m_data.x;
							float halfHeight = m_data.y;
							Vector3 apex = Vector3.up * halfHeight;
							Vector3[] corners = {
								new Vector3(-halfWidth, -halfHeight, -halfWidth),
								new Vector3(halfWidth, -halfHeight, -halfWidth),
								new Vector3(halfWidth, -halfHeight, halfWidth),
								new Vector3(-halfWidth, -halfHeight, halfWidth)
							};
							for (int i = 0; i < corners.Length; ++i)
							{
								Handles.DrawLine(corners[i], corners[(i + 1) % corners.Length]);
								Handles.DrawLine(corners[i], apex);
							}
						}
						break;
					default:						
						break;
				}
			}
        }

#endif

        #region Create Menu Items

#if UNITY_EDITOR
        private static void CreateNewPrimitive(SDFPrimitiveType type, Vector4 startData)
        {
            GameObject selection = Selection.activeGameObject;

            GameObject child = new GameObject(type.ToString());
            child.transform.SetParent(selection.transform);
            child.transform.Reset();

            SDFPrimitive newPrimitive = child.AddComponent<SDFPrimitive>();
            newPrimitive.m_type = type;
            newPrimitive.m_data = startData;
            newPrimitive.SetDirty();

            Selection.activeGameObject = child;
        }

        [MenuItem("GameObject/SDFs/Sphere", false, priority: 2)]
        private static void CreateSphere(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Sphere, new Vector4(1f, 0f, 0f, 0f));

        [MenuItem("GameObject/SDFs/Cuboid", false, priority: 2)]
        private static void CreateCuboid(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Cuboid, new Vector4(1f, 1f, 1f, 0f));

        [MenuItem("GameObject/SDFs/Torus", false, priority: 2)]
        private static void CreateTorus(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Torus, new Vector4(1f, 0.5f, 0f, 0f));

        [MenuItem("GameObject/SDFs/Frame", false, priority: 2)]
        private static void CreateFrame(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.BoxFrame, new Vector4(1f, 1f, 1f, 0.2f));

        [MenuItem("GameObject/SDFs/Cylinder", false, priority: 2)]
        private static void CreateCylinder(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Cylinder, new Vector4(1f, 1f, 0f, 0f));

        [MenuItem("GameObject/SDFs/Capsule", false, priority: 2)]
        private static void CreateCapsule(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Capsule, new Vector4(0.5f, 0.5f, 0f, 0f));

        [MenuItem("GameObject/SDFs/Ellipsoid", false, priority: 2)]
        private static void CreateEllipsoid(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Ellipsoid, new Vector4(1f, 0.75f, 0.5f, 0f));

        [MenuItem("GameObject/SDFs/Cone", false, priority: 2)]
        private static void CreateCone(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Cone, new Vector4(1f, 1f, 0f, 0f));

        [MenuItem("GameObject/SDFs/Pyramid", false, priority: 2)]
        private static void CreatePyramid(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Pyramid, new Vector4(1f, 1f, 0f, 0f));

#endif
        #endregion
    }

    public enum SDFPrimitiveType
    {
        Sphere,
        Torus,
        Cuboid,
        BoxFrame, 
        Cylinder,
        Capsule,
        Ellipsoid,
        Cone,
        Pyramid
    }
}
