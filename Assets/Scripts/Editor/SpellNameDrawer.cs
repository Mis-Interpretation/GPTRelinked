using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(SpellNameAttribute))]
public class SpellNameDrawer : PropertyDrawer
{
    private static string[] cachedSpellNames;
    private static double cacheTimestamp;
    private const double CacheLifetime = 1.0;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        string[] spellNames = GetSpellNamesCached();

        if (spellNames == null || spellNames.Length == 0)
        {
            Rect fieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(fieldRect, property, label);

            Rect helpRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2,
                position.width, EditorGUIUtility.singleLineHeight * 2);
            EditorGUI.HelpBox(helpRect, "No SpellDefinition asset found. Create one via Create > Scriptable Objects > SpellDefinition.", MessageType.Warning);
            return;
        }

        int currentIndex = System.Array.IndexOf(spellNames, property.stringValue);
        if (currentIndex < 0) currentIndex = 0;

        EditorGUI.BeginProperty(position, label, property);
        int selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, spellNames);
        property.stringValue = spellNames[selectedIndex];
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        string[] spellNames = GetSpellNamesCached();
        if (property.propertyType == SerializedPropertyType.String
            && (spellNames == null || spellNames.Length == 0))
        {
            return EditorGUIUtility.singleLineHeight * 3 + 4;
        }
        return EditorGUIUtility.singleLineHeight;
    }

    static string[] GetSpellNamesCached()
    {
        double now = EditorApplication.timeSinceStartup;
        if (cachedSpellNames != null && now - cacheTimestamp < CacheLifetime)
            return cachedSpellNames;

        cacheTimestamp = now;
        cachedSpellNames = LoadSpellNames();
        return cachedSpellNames;
    }

    static string[] LoadSpellNames()
    {
        string[] guids = AssetDatabase.FindAssets("t:SpellDefinition");
        if (guids.Length == 0) return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        SpellDefinition def = AssetDatabase.LoadAssetAtPath<SpellDefinition>(path);

        return def != null ? def.SpellNames : null;
    }
}
