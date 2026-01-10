using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class HierarchyJsonExporterMenu
{
  private const string NAME_PREFIX = "assets/";

  [Serializable]
  private class TransformData
  {
    public string name;
    public Vector3Data localPosition;
    public QuaternionData localRotation;
    public Vector3Data localScale;
    public List<TransformData> children = new List<TransformData>();
  }

  [Serializable]
  private class Vector3Data
  {
    public float x, y, z;
    public Vector3Data(Vector3 v) { x = v.x; y = v.y; z = v.z; }
  }

  [Serializable]
  private class QuaternionData
  {
    public float x, y, z, w;
    public QuaternionData(Quaternion q) { x = q.x; y = q.y; z = q.z; w = q.w; }
  }

  [Serializable]
  private class ExportRoot
  {
    public string rootName;
    public TransformData root;
    public string unityVersion;
    public string exportedAt;
  }

  private static bool IsAllowed(Transform t)
  {
    return t.name.StartsWith(NAME_PREFIX, StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Строит ноду. Возвращает null, если объект не проходит фильтр
  /// (кроме root — он добавляется принудительно).
  /// </summary>
  private static TransformData Build(Transform t, bool isRoot, Transform parent = null)
  {
    if (!isRoot && !IsAllowed(t))
      return null;

    var node = new TransformData
    {
      name = t.name,
      localPosition = new Vector3Data(parent == null ? new Vector3() : parent.transform.InverseTransformPoint(t.position)),
      localRotation = new QuaternionData(parent == null ? t.rotation : Quaternion.Inverse(parent.rotation) * t.rotation),
      localScale = new Vector3Data(t.localScale),
    };


    for (int i = 0; i < t.childCount; i++)
    {
      var child = Build(t.GetChild(i), false, parent);
      if (parent == null)
        parent = t.GetChild(i);
      if (child != null)
        node.children.Add(child);
    }

    return node;
  }

  private static string ToJson(Transform root)
  {
    var rootNode = Build(root, true);

    var data = new ExportRoot
    {
      rootName = root.name,
      root = rootNode,
      unityVersion = Application.unityVersion,
      exportedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    };

    return JsonUtility.ToJson(data, true);
  }

  // ПКМ по объекту в Hierarchy
  [MenuItem("GameObject/Export/Export Hierarchy to JSON...", false, 49)]
  private static void ExportSelected(MenuCommand command)
  {
    GameObject go = command.context as GameObject;
    if (go == null)
      go = Selection.activeGameObject;

    if (go == null)
    {
      EditorUtility.DisplayDialog("Export Hierarchy to JSON", "No GameObject selected.", "OK");
      return;
    }

    string defaultName = go.name + "_hierarchy.json";
    string path = EditorUtility.SaveFilePanel(
        "Export Hierarchy to JSON",
        Application.dataPath,
        defaultName,
        "json"
    );

    if (string.IsNullOrWhiteSpace(path))
      return;

    try
    {
      string json = ToJson(go.transform);
      File.WriteAllText(path, json, System.Text.Encoding.UTF8);
      Debug.Log($"[HierarchyJsonExporter] Exported (filtered by '{NAME_PREFIX}'): {path}");
      AssetDatabase.Refresh();
    }
    catch (Exception e)
    {
      Debug.LogError($"[HierarchyJsonExporter] Export failed: {e}");
      EditorUtility.DisplayDialog("Export Hierarchy to JSON", "Export failed. See Console.", "OK");
    }
  }

  [MenuItem("GameObject/Export/Export Hierarchy to JSON...", true)]
  private static bool ExportSelected_Validate()
  {
    return Selection.activeGameObject != null;
  }
}
