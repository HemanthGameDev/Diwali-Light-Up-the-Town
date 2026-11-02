
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public class SWBenchmarkSettings : MonoBehaviour
{
    private void Awake()
    {
        Application.targetFrameRate = 60;

        Debug.Log("========== SURFACEWEAVER DEVICE REPORT ==========");

        Debug.Log($"Platform: {Application.platform}");
        Debug.Log($"Device: {SystemInfo.deviceModel}");
        Debug.Log($"OS: {SystemInfo.operatingSystem}");

        Debug.Log($"CPU: {SystemInfo.processorType}");
        Debug.Log($"CPU Cores: {SystemInfo.processorCount}");
        Debug.Log($"RAM: {SystemInfo.systemMemorySize} MB");

        Debug.Log($"GPU: {SystemInfo.graphicsDeviceName}");
        Debug.Log($"Graphics API: {SystemInfo.graphicsDeviceType}");
        Debug.Log($"GPU Memory: {SystemInfo.graphicsMemorySize} MB");

        Debug.Log($"Resolution: {Screen.width}x{Screen.height}");
        Debug.Log($"Target FPS: {Application.targetFrameRate}");

        Debug.Log("=================================================");
    }
}
