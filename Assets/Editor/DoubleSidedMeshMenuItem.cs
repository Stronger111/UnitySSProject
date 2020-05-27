using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class DoubleSidedMeshMenuItem
{
    [MenuItem("Asstes/Create/Double-Sided Mesh")]
    static void MakeDoubleSidedMeshAsset()
    {
        var sourceMesh = Selection.activeObject as Mesh;
        if(sourceMesh==null)
        {
            Debug.Log("You must have a Mesh asset selected");
        }
        Mesh insideMesh = Object.Instantiate(sourceMesh);
        int[] triangles = insideMesh.triangles;
        System.Array.Reverse(triangles);
        insideMesh.triangles = triangles;

        //反转法线
        Vector3[] normals = insideMesh.normals;
        for (int i=0;i<normals.Length;i++)
        {
            normals[i] = -normals[i];
        }
        insideMesh.normals = normals;

        var combineMesh = new Mesh();
        combineMesh.CombineMeshes(new CombineInstance[] { new CombineInstance { mesh=insideMesh},new CombineInstance { mesh = sourceMesh} },true,false,false);
        Object.DestroyImmediate(insideMesh);

        AssetDatabase.CreateAsset(combineMesh,System.IO.Path.Combine("Assets",sourceMesh.name+"Double-Sided.asset"));
    }
}
