using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// A CD that fits in a <see cref="CDPlayer"/>. Each CD is a mixtape: fill in its tracks in the Inspector.
/// Empty slots are skipped, so a mixtape can have fewer songs than slots.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class CDDisc : MonoBehaviour
{
    [SerializeField] string title = "Mixtape";
    [SerializeField] AudioClip[] tracks = new AudioClip[5];

    public XRGrabInteractable Interactable { get; private set; }
    public string Title => title;
    public int SlotCount => tracks.Length;

    void Awake() => Interactable = GetComponent<XRGrabInteractable>();

    /// <summary>The song in a slot, or null if the slot is empty.</summary>
    public AudioClip GetTrack(int slot) => slot >= 0 && slot < tracks.Length ? tracks[slot] : null;

    public bool HasAnyTrack
    {
        get
        {
            foreach (var clip in tracks) if (clip != null) return true;
            return false;
        }
    }

    /// <summary>The next filled slot after <paramref name="slot"/> (wrapping around), or -1 if the CD is empty.</summary>
    public int NextTrack(int slot) => Step(slot, 1);

    /// <summary>The previous filled slot before <paramref name="slot"/> (wrapping around), or -1 if the CD is empty.</summary>
    public int PreviousTrack(int slot) => Step(slot, -1);

    public int FirstTrack => Step(-1, 1);
    public int LastTrack => Step(tracks.Length, -1);

    int Step(int slot, int direction)
    {
        int count = tracks.Length;
        for (int i = 1; i <= count; i++)
        {
            int candidate = ((slot + direction * i) % count + count) % count;
            if (tracks[candidate] != null) return candidate;
        }
        return -1;
    }
}
