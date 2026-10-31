
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class VideoToAsciiTransition : MonoBehaviour
{
    [Header("Video inicial")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject videoQuad;

    [Header("Animacion ASCII")]
    [SerializeField] private GameObject asciiLayer;

    [Header("Zona de interaccion")]
    [SerializeField] private Collider zonaInteractiva;

    [Header("Camara de Vuforia")]
    [SerializeField] private Camera camaraRaycast;

    private bool transicionRealizada = false;

    private void Awake()
    {
        if (videoPlayer == null ||
            videoQuad == null ||
            asciiLayer == null ||
            zonaInteractiva == null)
        {
            Debug.LogError(
                "Faltan referencias en VideoToAsciiTransition."
            );

            enabled = false;
            return;
        }

        if (camaraRaycast == null)
        {
            Debug.LogError("No se encontro la camara para detectar el toque");

            enabled = false;
            return;
        }

        videoPlayer.playOnAwake = false;

        videoQuad.SetActive(true);
        asciiLayer.SetActive(false);

        Debug.Log("Transicion preparada correctamente");
    }

    private void Start()
    {
        videoPlayer.Play();
    }

    private void Update()
    {
        if (transicionRealizada)
            return;

        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            Vector2 posicion = Touchscreen.current.primaryTouch.position.ReadValue();

            DetectarToque(posicion);
        }

        if (Mouse.current != null && 
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            DetectarToque(Mouse.current.position.ReadValue()
            );
        }
    }

    private void DetectarToque(Vector2 posicionPantalla)
    {
        Ray rayo = camaraRaycast.ScreenPointToRay(posicionPantalla);
        
        RaycastHit impacto;
        
        if (Physics.Raycast(rayo, out impacto))
        {
            Debug.Log("Collider tocado:" + impacto.collider.name);

            if (impacto.collider == zonaInteractiva)
            {
            ActivarASCII();
            }
            else
            {
                Debug.Log("El toque no alcanzo la zona interactiva");
            }
        }
        else
        {
            Debug.Log("El toque no alcanzo ningun collider");
        }
    }

    private void ActivarASCII()
    {
        if (transicionRealizada)
            return;

        transicionRealizada = true;

        videoPlayer.Stop();
        videoQuad.SetActive(false);
        asciiLayer.SetActive(true);

        Debug.Log("Toque detectado: activando ASCII.");
    }
}
