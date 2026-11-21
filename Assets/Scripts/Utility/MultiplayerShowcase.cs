using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Spawns a staged group of multiplayer agents (some reaching for shelf items, a pair chatting)
/// for demo footage and screenshots. Drives agents through the same API the websocket uses.
/// </summary>
public class MultiplayerShowcase : MonoBehaviour
{
    [Serializable]
    private struct AgentSlot
    {
        public Vector3 position;
        public float yaw;
        [Tooltip("Reach for the shelf item nearest this world point (skipped when reachRadius <= 0).")]
        public Vector3 reachPoint;
        public float reachRadius;
        [Tooltip("Chat lines, spoken in turn with the other agents' lines (index = turn).")]
        public string[] lines;
    }

    [SerializeField] private Camera showcaseCamera;
    [SerializeField] private AgentSlot[] slots;
    [SerializeField] private float startDelay = 1.5f;
    [SerializeField] private float turnSeconds = 1.6f;
    [SerializeField] private bool loopChat = true;
    [SerializeField] private string[] hiddenObjects = { "Ceiling", "Ceiling Lights", "Store Walls/Street Buildings" };
    private IEnumerator Start()
    {
        if (showcaseCamera != null)
        {
            showcaseCamera.tag = "MainCamera";
            if (GPUInstanceTracker.Instance != null) GPUInstanceTracker.Instance.SetCamera(showcaseCamera);
        }

        yield return new WaitForSeconds(startDelay);
        foreach (string path in hiddenObjects) // runtime-built; hidden to clear the overhead view
        {
            GameObject go = GameObject.Find(path);
            if (go != null) go.SetActive(false);
        }

        var agents = new AgentControllerBase[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            string id = MultiplayerAgentManager.Instance.SpawnAgent();
            agents[i] = MultiplayerAgentManager.Instance.GetAgent(id);
        }

        // Drop ghosts of the scene's own (disabled) agent; they'd float where it was left.
        foreach (HumanoidGhostFollower ghost in FindObjectsByType<HumanoidGhostFollower>(FindObjectsSortMode.None))
            if (Array.IndexOf(agents, ghost.Authority) < 0) Destroy(ghost.gameObject);

        yield return new WaitForSeconds(0.5f); // agents initialise in their own Start
        for (int i = 0; i < agents.Length; i++)
            if (agents[i] != null)
            {
                agents[i].TransformAgent(slots[i].position, new Vector3(0f, slots[i].yaw, 0f));
                StyleChat(agents[i]);
            }
        yield return new WaitForSeconds(0.5f);
        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i] == null) continue;
            RelaxHand(agents[i], AgentHandSide.Left);
            if (slots[i].reachRadius > 0f) Reach(agents[i], slots[i]);
            else RelaxHand(agents[i], AgentHandSide.Right);
        }

        if (MaxLines() == 0) yield break;
        int turn = 0;
        do
        {
            for (int i = 0; i < agents.Length; i++)
            {
                string[] lines = slots[i].lines;
                if (agents[i] == null || lines == null || turn >= lines.Length) continue;
                agents[i].ShowChat(lines[turn]);
                yield return new WaitForSeconds(turnSeconds);
            }
            turn = (turn + 1) % Mathf.Max(1, MaxLines());
        } while (loopChat || turn != 0);
    }

    // Dark bold text keeps the bubbles legible against the bright store.
    private static void StyleChat(AgentControllerBase agent)
    {
        TextMeshPro text = agent.transform.root.GetComponentInChildren<TextMeshPro>(true);
        if (text == null) return;
        text.color = new Color32(18, 26, 44, 255);
        text.fontStyle = FontStyles.Bold;
    }

    private int MaxLines()
    {
        int max = 0;
        foreach (AgentSlot s in slots) max = Mathf.Max(max, s.lines?.Length ?? 0);
        return max;
    }

    // Hand low at the side instead of the default arms-forward pose.
    private static void RelaxHand(AgentControllerBase agent, AgentHandSide side)
    {
        float x = side == AgentHandSide.Right ? 0.25f : -0.25f;
        agent.TransformHand(new Vector3(x, -0.6f, 0.1f), Vector3.zero, side);
    }

    private static void Reach(AgentControllerBase agent, AgentSlot slot)
    {
        ItemBBoxInfo target = null;
        float best = slot.reachRadius;
        foreach (ItemBBoxInfo item in FindObjectsByType<ItemBBoxInfo>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(item.transform.position, slot.reachPoint);
            if (d >= best) continue;
            best = d;
            target = item;
        }
        if (target == null) return;

        Vector3 local = agent.ViewTransform.InverseTransformPoint(target.transform.position);
        agent.TransformHand(Vector3.ClampMagnitude(local, agent.handMoveRange), new Vector3(0f, 0f, 0f));
    }
}
