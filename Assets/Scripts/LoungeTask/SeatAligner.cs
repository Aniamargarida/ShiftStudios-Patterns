using UnityEngine;
using Unity.XR.CoreUtils;

public class SeatAligner : MonoBehaviour
{
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private Transform seatAnchor;

    public void AlignToSeat()
    {
        xrOrigin.MatchOriginUpCameraForward(Vector3.up, seatAnchor.forward);
        xrOrigin.MoveCameraToWorldLocation(seatAnchor.position);
    }
}
