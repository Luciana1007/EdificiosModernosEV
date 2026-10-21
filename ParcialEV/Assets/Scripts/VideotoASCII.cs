using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

public class VideotoASCII : MonoBehaviour
{
    [Header("Video")]
    public VideoPlayer videoPlayer;

    [Header("Arte ASCII")]
    public TextMeshPro textoASCII;
    public TextAsset archivoASCII;

    [Header("Camara Vuforia")]
    public Camera arCamera;

    [Header("Velocidad de escritura")]
    [Tooltip("Tiempo entre caracteres. Menor valor = mas rapido")]
    public float intervaloCaracteres = 0.01f;

    private bool activado = false;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (textoASCII != null)
        {
            textoASCII.text = "";
            textoASCII.gameObject.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (activado || arCamera == null)
            return;

        if (Input.touchCount > 0 &&
            Input.GetTouch(0).phase == TouchPhase.Began)

        {
            DetectarToque(Input.GetTouch(0).position);
        }

        #if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            DetectarToque(Input.mousePosition);
        }
        #endif
    }

    void DetectarToque(Vector2 posicion)
    {
        Debug.Log("Toque detectado en pantalla");
        
        Ray rayo = arCamera.ScreenPointToRay(posicion);
        RaycastHit impacto;

        if (Physics.Raycast(rayo, out impacto))
        {
            Debug.Log("Raycast detecto: " + 
                impacto.collider.gameObject.name);

            if (impacto.collider.gameObject == gameObject || 
                impacto.collider.transform.IsChildOf(transform))
            {
                Debug.Log("Toque sobre el objeto correcto");
                
                if (archivoASCII == null || textoASCII == null)
                {
                    Debug.LogError(
                        "Asigna Archivo ASCII y Texto ASCII en el Inspector"
                    );
                    return;
                }

                if (videoPlayer != null)
                    videoPlayer.Stop();
                    Debug.Log("video detenido");
                
                
                textoASCII.gameObject.SetActive(true);

                Debug.Log("Texto ASCII activado");

                StartCoroutine(MostrarASCII());

                gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.Log("El Raycast no detecto ningun objeto");
        }

    }

    IEnumerator MostrarASCII()
    {
        Debug.Log("Comienza MostrarASCII");

        string contenido = archivoASCII.text;

        
        Debug.Log("Caracteres del archivo:" + contenido.Length);

        textoASCII.gameObject.SetActive(true);
        textoASCII.text = contenido;
        textoASCII.color = Color.white;

        Debug.Log("Texto asignado: " + textoASCII.text.Length
                    + "caracteres");

        yield break;
    }
}
