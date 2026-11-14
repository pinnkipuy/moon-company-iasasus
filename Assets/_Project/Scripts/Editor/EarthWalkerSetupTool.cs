// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Unity: Moon → Налаштувати бігуна на Землі (спрайти).
/// Знаходить у відкритій сцені всі EarthWalker, створює їм дочірній Sprite (SpriteRenderer + Billboard)
/// і заповнює картинки інженера: у скафандрі і без нього.
/// </summary>
public static class EarthWalkerSetupTool
{
    const string SuitDir = "Assets/_Project/Art/Characters/engineer_orange_outdoor";
    const string CasualDir = "Assets/_Project/Art/Characters/engineer";

    [MenuItem("Moon/Налаштувати бігуна на Землі (спрайти)")]
    public static void Setup()
    {
        EngineerSetupTool.ImportAsSprites(SuitDir);
        EngineerSetupTool.ImportAsSprites(CasualDir);

        var walkers = Object.FindObjectsByType<EarthWalker>(FindObjectsSortMode.None);
        if (walkers.Length == 0) { Debug.LogError("EarthWalkerSetupTool: у сцені немає об'єкта з компонентом EarthWalker. Додай його персонажу."); return; }

        foreach (var w in walkers)
        {
            Undo.RecordObject(w, "Earth walker sprites");
            var sr = GetOrCreateSprite(w.transform);
            var so = new SerializedObject(w);
            so.FindProperty("spriteRenderer").objectReferenceValue = sr;
            so.ApplyModifiedProperties();

            w.suit = new EngineerController.SpriteSet
            {
                idleFront = EngineerSetupTool.Load(SuitDir, "front"),
                idleBack = EngineerSetupTool.Load(SuitDir, "back"),
                walkFront = EngineerSetupTool.Frames(SuitDir, "walk_front_"),
                walkBack = EngineerSetupTool.Frames(SuitDir, "walk_back_"),
                walkLeft = EngineerSetupTool.Frames(SuitDir, "walk_left_"),
                walkRight = EngineerSetupTool.Frames(SuitDir, "walk_right_"),
            };
            w.casual = new EngineerController.SpriteSet
            {
                // «Стоїть» — кадр ходьби з обома ногами на землі (так фігура не «стрибає» на старті)
                idleFront = EngineerSetupTool.Load(CasualDir, "engineer_front_walk_02") ?? EngineerSetupTool.Load(CasualDir, "engineer_front"),
                idleBack = EngineerSetupTool.Load(CasualDir, "engineer_back_walk_02") ?? EngineerSetupTool.Load(CasualDir, "engineer_back"),
                walkFront = EngineerSetupTool.Frames(CasualDir, "engineer_front_walk_"),
                walkBack = EngineerSetupTool.Frames(CasualDir, "engineer_back_walk_"),
                walkLeft = EngineerSetupTool.Frames(CasualDir, "engineer_left_profile_walk_"),
                walkRight = new Sprite[0], // вправо — дзеркально
            };
            sr.sprite = w.SuitOn ? w.suit.idleFront : w.casual.idleFront;

            EditorUtility.SetDirty(w);
            EditorSceneManager.MarkSceneDirty(w.gameObject.scene);
        }
        Debug.Log("EarthWalkerSetupTool: готово для " + walkers.Length + " персонаж(ів). Збережи сцену (Ctrl+S).");
    }

    static SpriteRenderer GetOrCreateSprite(Transform t)
    {
        var mesh = t.GetComponent<MeshRenderer>();
        if (mesh != null) { Undo.RecordObject(mesh, "Hide cube"); mesh.enabled = false; }

        var sr = t.GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
        {
            var go = new GameObject("Sprite");
            Undo.RegisterCreatedObjectUndo(go, "Earth walker sprite");
            go.transform.SetParent(t, false);
            sr = go.AddComponent<SpriteRenderer>();
            // Ноги спрайта — на землі (центр персонажа стоїть на висоті t.position.y)
            sr.transform.localPosition = new Vector3(0f, -t.position.y, 0f);
        }
        if (sr.GetComponent<Billboard>() == null) Undo.AddComponent<Billboard>(sr.gameObject);
        return sr;
    }
}
