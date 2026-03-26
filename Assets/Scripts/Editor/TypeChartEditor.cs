using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TypeChart))]
public class TypeChartEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Initialize Default Chart", GUILayout.Height(30)))
        {
            TypeChart chart = (TypeChart)target;
            Undo.RecordObject(chart, "Initialize Type Chart");
            chart.InitializeDefaultChart();
            EditorUtility.SetDirty(chart);
            Debug.Log("[TypeChart] Default chart initialized with standard type matchups.");
        }
    }
}
