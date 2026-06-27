namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class MediaFoundationUnsupportedFormatException(string message) : InvalidOperationException(message);
