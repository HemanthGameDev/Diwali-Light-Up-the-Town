using UnityEngine;

public class SWGraphicsDiagnostics : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log("===== SURFACEWEAVER GRAPHICS REPORT =====");

        Debug.Log($"Device: {SystemInfo.deviceModel}");
        Debug.Log($"GPU: {SystemInfo.graphicsDeviceName}");

        Debug.Log($"API: {SystemInfo.graphicsDeviceType}");
        Debug.Log($"API Version: {SystemInfo.graphicsDeviceVersion}");

        Debug.Log($"Shader Level: {SystemInfo.graphicsShaderLevel}");
        Debug.Log($"GPU Memory: {SystemInfo.graphicsMemorySize} MB");

        Debug.Log($"Instancing: {SystemInfo.supportsInstancing}");
        Debug.Log($"Compute: {SystemInfo.supportsComputeShaders}");

        Debug.Log("=========================================");
    }
}