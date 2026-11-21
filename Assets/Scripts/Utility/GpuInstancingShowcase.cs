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
    [SerializeField] private Color zoneColor = new(0.2f, 0.9f, 1f, 0.22f);
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
        MultiplayerShowcase.DestroyStrayGhosts(new[] { agent });
        yield return new WaitForSeconds(0.5f); // agent initialises in its own Start
        agent.TransformAgent(agentPosition, new Vector3(0f, agentYaw, 0f));
        yield return new WaitForSeconds(0.5f);

        HideVrModel(agent);
        ShowActivationSphere(agent);

        Vector3 near = Vector3.ClampMagnitude(agent.ViewTransform.InverseTransformPoint(reachPoint), agent.handMoveRange);
        Vector3 far = near - Vector3.forward * retreatDistance;
        while (true)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(Time.time / sweepSeconds, 1f));
            agent.TransformHand(Vector3.Lerp(far, near, t), Vector3.zero);
            yield return null;
        }
    }

    // Hides the VR rig's hand meshes and the humanoid ghost body, leaving only the activation zone visible.
    private static void HideVrModel(AgentControllerBase agent)
    {
        foreach (Renderer r in agent.transform.root.GetComponentsInChildren<Renderer>(true))
            r.enabled = false;
        foreach (HumanoidGhostFollower ghost in FindObjectsByType<HumanoidGhostFollower>(FindObjectsSortMode.None))
            if (ghost.Authority == agent) ghost.SetRenderersVisible(false);
    }

    // Translucent sphere over the hand's physics-activation trigger: items inside it swap to physics prefabs.
    private void ShowActivationSphere(AgentControllerBase agent)
    {
        HandPhysicsSphere zoneObject = agent.HandTransform.GetComponentInChildren<HandPhysicsSphere>(true);
        SphereCollider zone = zoneObject != null ? zoneObject.GetComponent<SphereCollider>() : null;
        if (zone == null) return;

        GameObject viz = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(viz.GetComponent<Collider>());
        viz.name = "Activation Zone";
        viz.transform.SetParent(zone.transform, false);
        viz.transform.localPosition = zone.center;
        viz.transform.localScale = Vector3.one * zone.radius * 2f;

        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetColor("_BaseColor", zoneColor);
        var renderer = viz.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
