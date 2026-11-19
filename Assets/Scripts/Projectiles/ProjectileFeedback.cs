using UnityEngine;

// Editor-only overlay, independent of projectile lifetime.
public class ProjectileFeedback : MonoBehaviour
{
    private static ProjectileFeedback instance;
    private int deflections, hits;
    private string message = "Ожидание броска";
    private float messageUntil;

    public static void Report(bool deflected)
    {
#if UNITY_EDITOR
        if (instance == null)
            instance = new GameObject("Projectile feedback").AddComponent<ProjectileFeedback>();
        if (deflected) instance.deflections++; else instance.hits++;
        instance.message = deflected ? "ОТБИТО!" : "ПОПАДАНИЕ В ИГРОКА";
        instance.messageUntil = Time.time + 2f;
#endif
    }

    void OnGUI()
    {
#if UNITY_EDITOR
        var style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
        GUI.Box(new Rect(16, 16, 480, 50), $"Отбито: {deflections}    Попаданий: {hits}", style);
        if (Time.time < messageUntil)
            GUI.Box(new Rect(16, 72, 480, 50), message, style);
#endif
    }
}
