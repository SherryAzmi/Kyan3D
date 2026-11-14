using UnityEngine;

/// <summary>
/// If the player falls into the chasm, put them back at the respawn point. No fail state.
/// Sounds: a falling whoosh once they drop below the ground, and a painful thud when they are put back.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerRespawn : MonoBehaviour
{
    public Vector3 respawnPoint;
    public float killY = -6f;
    [Tooltip("Below this height the player is falling into a hole (plays the whoosh).")]
    public float fallY = -1.2f;

    CharacterController cc;
    bool falling;

    void Awake() => cc = GetComponent<CharacterController>();

    void Update()
    {
        float y = transform.position.y;
        var a = GameAudio.I;
        if (!falling && y < fallY)
        {
            falling = true;
            if (a) GameAudio.Play(a.fall, GameAudio.SideOf(transform.position), 0.9f);
        }
        if (y > killY) return;

        int side = GameAudio.SideOf(transform.position);
        cc.enabled = false;
        transform.position = respawnPoint;
        cc.enabled = true;
        falling = false;
        if (a) GameAudio.Play(a.hurt, side, 1f, Random.Range(0.95f, 1.05f));
    }
}
