using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshOutline : MonoBehaviour
{
    [System.Serializable]
    public class Edge
    {
        public Vector3 begin; 
        public Vector3 end;
    }
    [System.Serializable]
    public class Pair
    {
        public int first;
        public int second;
    }

    public int speed;
    public Camera camera;
    private Mesh _mesh;
    private Edge[] localEdges;
    private float[][] positions;

    public float[][] Positions { get { return positions; } }

    void Start()
    {

        _mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = _mesh.vertices;
        int[] indices = _mesh.GetIndices(0);

        int[][] ways = new int[vertices.Length][];
        int[] degrees = new int[vertices.Length];
        for(int i = 0; i < ways.Length; ++i)
        {
            ways[i] = new int[vertices.Length];
        }

        for (int i = 0; i < indices.Length - 1; i++)
        {
            if (ways[indices[i + 0]][indices[i + 1]] == 0)
            {
                ways[indices[i + 0]][indices[i + 1]] = 1;
                degrees[indices[i + 0]]++;
            }
            if (ways[indices[i + 1]][indices[i + 0]] == 0)
            {
                ways[indices[i + 1]][indices[i + 0]] = 1;
                degrees[indices[i + 1]]++;
            }
        }

        Graph graph = new Graph(vertices.Length);
        for(int i = 0; i < ways.Length; ++i)
        {
            for(int j = 0; j < ways[i].Length; ++j)
            {
                if (ways[i][j] == 1)
                {
                    graph.AddEdge(i, j, 1);
                }
            }
        }

        List<int> optimal = graph.FindOptimalRoute();
        localEdges = new Edge[optimal.Count - 1];
        for (int i = 0; i < optimal.Count - 1; i++)
        {
            Edge newEdge = new Edge();
            newEdge.begin = vertices[optimal[i]];
            newEdge.end = vertices[optimal[i + 1]];
            localEdges[i] = (newEdge);
        }
        positions = new float[localEdges.Length][];
        for(int i = 0; i < positions.Length; ++i)
        {
            positions[i] = new float[2*speed];
        }

    }


    private void Update()
    {
        int twoSpeed = 2 * speed;
        if (positions[0].Length != twoSpeed)
        {

            for (int i = 0; i < positions.Length; ++i)
            {
                positions[i] = new float[twoSpeed];
            }
        }
        float aspect = (float)Screen.width / (float)Screen.height;
        Matrix4x4 outlineMatrix = transform.localToWorldMatrix;
        float dT = 1.0f / (float)speed;
        Matrix4x4 cameraMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 begin = localEdges[i].begin;
            Vector3 end = localEdges[i].end;

            Vector3 result = begin;
            begin.x = outlineMatrix.m00 * result.x + outlineMatrix.m01 * result.y + outlineMatrix.m02 * result.z + outlineMatrix.m03;
            begin.y = outlineMatrix.m10 * result.x + outlineMatrix.m11 * result.y + outlineMatrix.m12 * result.z + outlineMatrix.m13;
            begin.z = outlineMatrix.m20 * result.x + outlineMatrix.m21 * result.y + outlineMatrix.m22 * result.z + outlineMatrix.m23;

            result = end;
            end.x = outlineMatrix.m00 * result.x + outlineMatrix.m01 * result.y + outlineMatrix.m02 * result.z + outlineMatrix.m03;
            end.y = outlineMatrix.m10 * result.x + outlineMatrix.m11 * result.y + outlineMatrix.m12 * result.z + outlineMatrix.m13;
            end.z = outlineMatrix.m20 * result.x + outlineMatrix.m21 * result.y + outlineMatrix.m22 * result.z + outlineMatrix.m23;

            result = begin;
            begin.x = cameraMatrix.m00 * result.x + cameraMatrix.m01 * result.y + cameraMatrix.m02 * result.z + cameraMatrix.m03;
            begin.y = cameraMatrix.m10 * result.x + cameraMatrix.m11 * result.y + cameraMatrix.m12 * result.z + cameraMatrix.m13;
            float w = cameraMatrix.m30 * result.x + cameraMatrix.m31 * result.y + cameraMatrix.m32 * result.z + cameraMatrix.m33;

            float beginCamX = begin.x / w;
            float beginCamY = begin.y / w;

            result = end;
            end.x = cameraMatrix.m00 * result.x + cameraMatrix.m01 * result.y + cameraMatrix.m02 * result.z + cameraMatrix.m03;
            end.y = cameraMatrix.m10 * result.x + cameraMatrix.m11 * result.y + cameraMatrix.m12 * result.z + cameraMatrix.m13;
            w = cameraMatrix.m30 * result.x + cameraMatrix.m31 * result.y + cameraMatrix.m32 * result.z + cameraMatrix.m33;


            float endCamX = end.x / w;
            float endCamY = end.y / w;

            beginCamX *= aspect;
            endCamX *= aspect;
            float diffX = dT*(endCamX - beginCamX);
            float diffY = dT*(endCamY - beginCamY);
            float currX = beginCamX;
            float currY = beginCamY;
            float[] currPositions = positions[i];
            for (int j = 0; j < twoSpeed; j += 2)
            {
                currPositions[j + 0] = currX;
                currPositions[j + 1] = currY;
                currX += diffX;
                currY += diffY;
            }
        }
    }

}
