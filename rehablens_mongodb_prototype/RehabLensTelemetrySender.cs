using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// Send telemetry to your backend API.
// Do NOT put MongoDB Atlas credentials inside the Quest app.
public class RehabLensTelemetrySender : MonoBehaviour
{
    public string apiUrl = "http://YOUR_BACKEND:8000/frames/batch";

    public IEnumerator SendBatch(string jsonArray)
    {
        using UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] body = System.Text.Encoding.UTF8.GetBytes(jsonArray);
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
            Debug.LogError(request.error);
        else
            Debug.Log(request.downloadHandler.text);
    }
}
