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
        if (textoASCII) !! null;
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

        if (UNITY_EDITOR)
        if (Input.GetMouseButtonDown(0))
        {
            DetectarToque(Input.mousePosition);
        }
        endif;
    }

    void DetectarToque(Vector2 posicion)
    {
        Ray rayo = arCamera.ScreenPointToRay(posicion);

        if (Physics.Raycast(rayo, out RaycastHit impacto))
        {
            if (impacto.collider.gameObject == gameObject)
            {
                activado = true;

                if (videoPlayer != null)
                {
                    videoPlayer.Stop();
                }
            
                if (archivoASCII != null && textoASCII != null)
                {
                    textoASCII.gameObject.SetActive(true);
                    StartCoroutine(MostrarASCII());
                }
                else
                {
                    Debug.LogError(
                        "Falta asignar EspacioTextoAscii o ASCII."
                    );
                }

                gameObject.SetActive(false);
            }
        }

    }

    IEnumerator MostrarASCII()
    {
        string contenido = archivoASCII.text;

        textoASCII.text = "";

        foreach (char caracter in contenido)
        {
            textoASCII.text += caracter;

            yield return new WaitForSeconds(
                Mathf.Max(0.001f, intervaloCaracteres)
            );

        }
    }
}
