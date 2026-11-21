using System.Collections;
using UnityEngine;

/// <summary>
/// Demo for GPU instancing: shelf items are tinted "ghost" blue while instanced, then pop to full
/// colour as a multiplayer agent's hand sweeps near them and they swap to physics prefabs.
/// </summary>
public class GpuInstancingShowcase : MonoBehaviour
{
    [SerializeField] private Camera showcaseCamera;
    [SerializeField] private Vector3 agentPosition;
    [SerializeField] private float agentYaw;
    [Tooltip("World point of the item the hand sweeps toward.")]
    [SerializeField] private Vector3 reachPoint;
    [Tooltip("Hand distance from the reach point at the far end of the sweep (metres, along the agent's forward).")]
    [SerializeField] private float retreatDistance = 0.6f;
    [SerializeField] private float sweepSeconds = 2.5f;
    [SerializeField] private Color ghostTint = new(0.62f, 0.82f, 1f, 1f);
    [SerializeField] private Color ghostEmission = new(0.03f, 0.08f, 0.14f, 1f);
    [SerializeField] private float startDelay = 2f;
    [SerializeField] private string[] hiddenObjects = { "Ceiling", "Ceiling Lights", "Store Walls/Street Buildings" };

    private IEnumerator Start()
    {
        if (showcaseCamera != null)
        {
            showcaseCamera.tag = "MainCamera";
            GPUInstanceTracker.Instance.SetCamera(showcaseCamera);
        }

        yield return new WaitForSeconds(startDelay);
        MultiplayerShowcase.HideObjects(hiddenObjects);
        GPUInstanceTracker.Instance.SetInstanceTint(ghostTint, ghostEmission);

        AgentControllerBase agent = MultiplayerAgentManager.Instance.GetAgent(MultiplayerAgentManager.Instance.SpawnAgent());
        yield return new WaitForSeconds(0.5f); // agent initialises in its own Start
        agent.TransformAgent(agentPosition, new Vector3(0f, agentYaw, 0f));
        yield return new WaitForSeconds(0.5f);

        Vector3 near = Vector3.ClampMagnitude(agent.ViewTransform.InverseTransformPoint(reachPoint), agent.handMoveRange);
        Vector3 far = near - Vector3.forward * retreatDistance;
        while (true)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(Time.time / sweepSeconds, 1f));
            agent.TransformHand(Vector3.Lerp(far, near, t), Vector3.zero);
            yield return null;
        }
    }
}
