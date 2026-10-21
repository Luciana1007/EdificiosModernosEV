
using UnityEngine;
using UnityEngine.Video;

public class VideoToAsciiTransition : MonoBehaviour
{
    [Header("Video inicial")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject videoQuad;

    [Header("Animacion ASCII")]
    [SerializeField] private GameObject asciiLayer;

    [Header("Zona de interaccion")]
    [SerializeField] private Collider zonaInteractiva;

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

        videoPlayer.playOnAwake = false;

        videoQuad.SetActive(true);
        asciiLayer.SetActive(false);
    }

    private void Start()
    {
        videoPlayer.Play();
    }

    private void Update()
    {
        if (transicionRealizada)
            return;

        if (Input.touchCount > 0 &&
            Input.GetTouch(0).phase == TouchPhase.Began)
        {
            DetectarToque(Input.GetTouch(0).position);
        }

        // Permite probar tambien con el clic del mouse
        if (Input.GetMouseButtonDown(0))
        {
            DetectarToque(Input.mousePosition);
        }
    }

    private void DetectarToque(Vector2 posicionPantalla)
    {
        Camera camara = Camera.main;

        if (camara == null)
            return;

        Ray rayo = camara.ScreenPointToRay(posicionPantalla);
        RaycastHit impacto;

        if (Physics.Raycast(rayo, out impacto))
        {
            if (impacto.collider == zonaInteractiva)
            {
                ActivarASCII();
            }
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
