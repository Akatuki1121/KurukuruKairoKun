using System;
using System.Globalization;
using System.IO.Ports;
using UnityEngine;

public sealed class SerialGameInput : IGameInput, IDisposable
{
    private const int BaudRate = 115200;
    private const string StartMarker = "S";
    private const string EndMarker = "E";
    private readonly SerialPort port;
    private string pending = string.Empty;
    private ControllerState latest;
    private bool disposed;
    public bool IsAvailable => port != null && port.IsOpen && !disposed;

    public SerialGameInput(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName)) return;
        try
        {
            port = new SerialPort(portName, BaudRate);
            port.ReadTimeout = 1;
            port.Open();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Serial input unavailable: " + exception.Message);
            port = null;
        }
    }

    public ControllerState Read(float deltaTime)
    {
        if (port == null || !port.IsOpen || disposed) return default;
        try
        {
            pending += port.ReadExisting();
            int start;
            while ((start = pending.IndexOf(StartMarker, StringComparison.Ordinal)) >= 0)
            {
                int end = pending.IndexOf(EndMarker, start + 1, StringComparison.Ordinal);
                if (end < 0)
                {
                    if (start > 0) pending = pending.Substring(start);
                    break;
                }

                Parse(pending.Substring(start + 1, end - start - 1));
                pending = pending.Substring(end + 1);
            }
            if (pending.IndexOf(StartMarker, StringComparison.Ordinal) < 0) pending = string.Empty;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Serial read failed: " + exception.Message);
        }

        ControllerState result = latest;
        latest = default;
        return result;
    }

    private void Parse(string payload)
    {
        // S,deltaDegrees,speedDegreesPerSecond,direction,tiltX,tiltY,shake,E
        string[] fields = payload.Split(',');
        if (fields.Length != 6) return;
        if (!float.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float delta) ||
            !float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float speed) ||
            !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int direction) ||
            !float.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float tiltX) ||
            !float.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float tiltY) ||
            !int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int shake)) return;

        latest.RotationDeltaDegrees += delta;
        latest.Shaken |= shake != 0;
        if (delta != 0f)
        {
            latest.RotationSpeedDegreesPerSecond = Mathf.Max(latest.RotationSpeedDegreesPerSecond, speed);
            latest.RotationDirection = Mathf.Clamp(direction, -1, 1);
        }
        latest.Tilt = new Vector2(tiltX, tiltY);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (port == null) return;
        if (port.IsOpen) port.Close();
        port.Dispose();
    }
}
