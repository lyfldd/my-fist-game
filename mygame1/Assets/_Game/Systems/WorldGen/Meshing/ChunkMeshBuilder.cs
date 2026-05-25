using UnityEngine;

namespace _Game.Systems.WorldGen.Meshing
{
    /// <summary>
    /// 从 heightMap 切片构建单个 Chunk 的 Unity Mesh。
    /// 顶点颜色通过 vertexColorMap 传入。
    /// 三角形用左上-右下对角线切分。
    /// </summary>
    public static class ChunkMeshBuilder
    {
        /// <summary>
        /// 构建单个 Chunk 的 Mesh。
        /// </summary>
        /// <param name="heightMap">高度切片 [vertsPerSide, vertsPerSide]</param>
        /// <param name="colorMap">顶点颜色 [vertsPerSide, vertsPerSide]</param>
        /// <param name="chunkSize">Chunk 世界尺寸（米）</param>
        /// <param name="resolution">网格分辨率（16 = 16格/Chunk）</param>
        public static Mesh Build(float[,] heightMap, Color[,] colorMap,
            float chunkSize, int resolution)
        {
            int vertsPerSide = resolution + 1; // 17
            float spacing = chunkSize / resolution; // 2m

            int vertexCount = vertsPerSide * vertsPerSide;
            int quadCount = resolution * resolution;
            int triCount = quadCount * 2;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Color[] colors = new Color[vertexCount];
            int[] triangles = new int[triCount * 3];

            // 1. 生成顶点
            for (int lz = 0; lz < vertsPerSide; lz++)
            {
                for (int lx = 0; lx < vertsPerSide; lx++)
                {
                    int idx = lz * vertsPerSide + lx;
                    float h = heightMap[lx, lz];
                    vertices[idx] = new Vector3(lx * spacing, h, lz * spacing);
                    normals[idx] = Vector3.up; // 初值，下面会重算
                    colors[idx] = colorMap[lx, lz];
                }
            }

            // 2. 生成三角形（左上-右下对角线切分）
            int triIdx = 0;
            for (int lz = 0; lz < resolution; lz++)
            {
                for (int lx = 0; lx < resolution; lx++)
                {
                    int v00 =  lz      * vertsPerSide + lx;      // 左下
                    int v10 =  lz      * vertsPerSide + lx + 1;  // 右下
                    int v01 = (lz + 1) * vertsPerSide + lx;      // 左上
                    int v11 = (lz + 1) * vertsPerSide + lx + 1;  // 右上

                    // Tri A: 左下 → 左上 → 右下  (共享对角线 v01-v10)
                    triangles[triIdx++] = v00;
                    triangles[triIdx++] = v01;
                    triangles[triIdx++] = v10;

                    // Tri B: 左上 → 右上 → 右下
                    triangles[triIdx++] = v01;
                    triangles[triIdx++] = v11;
                    triangles[triIdx++] = v10;
                }
            }

            // 3. 重新计算法线
            Vector3[] computedNormals = CalculateNormals(vertices, triangles, vertexCount, triCount);
            for (int i = 0; i < vertexCount; i++)
                normals[i] = computedNormals[i];

            // 4. 组装 Mesh
            Mesh mesh = new Mesh();
            mesh.name = "ChunkMesh";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // 顶点数较多时用 32 位索引
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// 基于三角形面的法线平均计算顶点法线。
        /// </summary>
        private static Vector3[] CalculateNormals(Vector3[] verts, int[] tris,
            int vertexCount, int triangleCount)
        {
            Vector3[] normals = new Vector3[vertexCount];

            for (int t = 0; t < triangleCount; t++)
            {
                int i0 = tris[t * 3];
                int i1 = tris[t * 3 + 1];
                int i2 = tris[t * 3 + 2];

                Vector3 edge0 = verts[i1] - verts[i0];
                Vector3 edge1 = verts[i2] - verts[i0];
                Vector3 faceNormal = Vector3.Cross(edge0, edge1).normalized;

                normals[i0] += faceNormal;
                normals[i1] += faceNormal;
                normals[i2] += faceNormal;
            }

            for (int i = 0; i < vertexCount; i++)
                normals[i] = normals[i].normalized;

            return normals;
        }
    }
}
