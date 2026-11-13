using UnityEngine;

/// <summary>If the player falls into the chasm, put them back at the respawn point. No fail state.</summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerRespawn : MonoBehaviour
{
    public Vector3 respawnPoint;
    public float killY = -6f;

    CharacterController cc;

    void Awake() => cc = GetComponent<CharacterController>();

    void Update()
    {
        if (transform.position.y > killY) return;
        cc.enabled = false;
        transform.position = respawnPoint;
        cc.enabled = true;
    }
}
