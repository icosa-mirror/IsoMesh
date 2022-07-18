using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IsoMesh;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RaymarchTester : MonoBehaviour
{
    [SerializeField]
    private SDFGroup m_group;
#if UNITY_EDITOR
	private void OnDrawGizmos()
    {
        if (!m_group)
            return;

        if (m_group.Raycast(transform.position, transform.forward, out Vector3 hitPoint, out Vector3 hitNormal))
        {
            Handles.color = Color.green;
            Handles.DrawAAPolyLine(transform.position, hitPoint);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(hitPoint, 0.1f);

            Handles.color = Color.red;
            Handles.DrawAAPolyLine(hitPoint, hitPoint + hitNormal * 4f);
        }
        else
        {
            Handles.color = Color.red;
            Handles.DrawAAPolyLine(transform.position, transform.position + transform.forward * 350f);
        }

        Vector3 v = m_group.GetNearestPointOnSurface(transform.position);

        Utils.Label(transform.position, v.ToString("F6"));

        Handles.color = Color.blue;
        Handles.DrawAAPolyLine(transform.position, transform.position + v);
    }
#endif
}
