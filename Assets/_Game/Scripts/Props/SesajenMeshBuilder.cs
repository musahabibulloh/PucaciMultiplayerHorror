using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pucaci.Props
{
    public static class SesajenMeshBuilder
    {
        /// <summary>
        /// Membuat mesh nampan anyaman bambu (tampah) berbentuk silinder berlekuk ke dalam.
        /// </summary>
        public static Mesh CreateTrayMesh(float radius = 0.35f, float rimHeight = 0.05f, int segments = 24)
        {
            Mesh mesh = new Mesh { name = "Sesajen_Tray_Mesh" };

            int vertCount = (segments + 1) * 3 + 1;
            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            Vector3[] normals = new Vector3[vertCount];

            // Titik pusat dasar
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            normals[0] = Vector3.up;

            int idx = 1;
            float angleStep = Mathf.PI * 2f / segments;

            // Lingkaran dasar bawah
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * (radius * 0.85f);
                float z = Mathf.Sin(angle) * (radius * 0.85f);
                vertices[idx] = new Vector3(x, 0.01f, z);
                uvs[idx] = new Vector2(0.5f + Mathf.Cos(angle) * 0.4f, 0.5f + Mathf.Sin(angle) * 0.4f);
                normals[idx] = Vector3.up;
                idx++;
            }

            // Lingkaran bibir atas luar
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                vertices[idx] = new Vector3(x, rimHeight, z);
                uvs[idx] = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);
                normals[idx] = new Vector3(Mathf.Cos(angle), 0.5f, Mathf.Sin(angle)).normalized;
                idx++;
            }

            // Segitiga dasar dan pinggiran tampah
            System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();

            // Dasar tampah
            for (int i = 1; i <= segments; i++)
            {
                triangles.Add(0);
                triangles.Add(i + 1);
                triangles.Add(i);
            }

            // Dinding miring tampah
            int baseOffset = 1;
            int rimOffset = baseOffset + segments + 1;
            for (int i = 0; i < segments; i++)
            {
                int b1 = baseOffset + i;
                int b2 = baseOffset + i + 1;
                int r1 = rimOffset + i;
                int r2 = rimOffset + i + 1;

                triangles.Add(b1);
                triangles.Add(b2);
                triangles.Add(r1);

                triangles.Add(b2);
                triangles.Add(r2);
                triangles.Add(r1);
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Membuat mesh daun pisang alas sesajen dengan tepi sedikit bergelombang organik.
        /// </summary>
        public static Mesh CreateBananaLeafMesh(float radius = 0.32f, int segments = 20)
        {
            Mesh mesh = new Mesh { name = "Sesajen_BananaLeaf_Mesh" };

            Vector3[] vertices = new Vector3[segments + 2];
            Vector2[] uvs = new Vector2[segments + 2];
            Vector3[] normals = new Vector3[segments + 2];

            vertices[0] = new Vector3(0f, 0.015f, 0f);
            uvs[0] = new Vector2(0.5f, 0.5f);
            normals[0] = Vector3.up;

            float angleStep = Mathf.PI * 2f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                // Gelombang halus di tepi daun
                float r = radius * (1f + 0.04f * Mathf.Sin(angle * 7f));
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                vertices[i + 1] = new Vector3(x, 0.016f + 0.005f * Mathf.Sin(angle * 3f), z);
                uvs[i + 1] = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);
                normals[i + 1] = Vector3.up;
            }

            System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();
            for (int i = 1; i <= segments; i++)
            {
                triangles.Add(0);
                triangles.Add(i + 1);
                triangles.Add(i);
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Membuat mesh mangkuk batok kelapa (cup) tempat dupa.
        /// </summary>
        public static Mesh CreateBowlMesh(float radius = 0.08f, float height = 0.06f, int segments = 16)
        {
            Mesh mesh = new Mesh { name = "Sesajen_Bowl_Mesh" };

            System.Collections.Generic.List<Vector3> verts = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector2> uvs = new System.Collections.Generic.List<Vector2>();
            System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>();

            verts.Add(new Vector3(0f, 0.015f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            float angleStep = Mathf.PI * 2f / segments;

            // Lingkaran bawah
            int baseStart = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                verts.Add(new Vector3(Mathf.Cos(angle) * (radius * 0.6f), 0.02f, Mathf.Sin(angle) * (radius * 0.6f)));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.3f, 0.5f + Mathf.Sin(angle) * 0.3f));
            }

            // Lingkaran bibir atas mangkuk
            int rimStart = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                verts.Add(new Vector3(Mathf.Cos(angle) * radius, height + 0.015f, Mathf.Sin(angle) * radius));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
            }

            // Segitiga dasar
            for (int i = 0; i < segments; i++)
            {
                tris.Add(0);
                tris.Add(baseStart + i + 1);
                tris.Add(baseStart + i);
            }

            // Dinding mangkuk
            for (int i = 0; i < segments; i++)
            {
                int b1 = baseStart + i;
                int b2 = baseStart + i + 1;
                int r1 = rimStart + i;
                int r2 = rimStart + i + 1;

                tris.Add(b1);
                tris.Add(b2);
                tris.Add(r1);

                tris.Add(b2);
                tris.Add(r2);
                tris.Add(r1);
            }

            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
