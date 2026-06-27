using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ShrimpCam.Infrastructure.Cameras.Windows;

internal sealed class NativeMediaFoundationDeviceDiscovery : IMediaFoundationNativeDeviceDiscovery
{
    private const int MediaFoundationVersion = 0x0002_0070;
    private const int Succeeded = 0;
    private static readonly Guid SourceTypeAttribute = new("c60ac5fe-252a-478f-a0ef-bc8fa5f7c6e2");
    private static readonly Guid VideoCaptureSourceType = new("8ac3587a-4ae7-42d8-99e0-0a6013eef90f");
    private static readonly Guid FriendlyNameAttribute = new("60d0e559-52f8-4fa2-bbce-acdb34a8ec01");
    private static readonly Guid SymbolicLinkAttribute = new("58f0aad8-22bf-4f8a-bb3d-d2c4978c6e2f");

    public Task<IReadOnlyList<MediaFoundationDeviceDescriptor>> DiscoverAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyList<MediaFoundationDeviceDescriptor>>([]);
        }

        return Task.FromResult<IReadOnlyList<MediaFoundationDeviceDescriptor>>(Discover(cancellationToken));
    }

    [SupportedOSPlatform("windows")]
    private static List<MediaFoundationDeviceDescriptor> Discover(CancellationToken cancellationToken)
    {
        ThrowIfFailed(MFStartup(MediaFoundationVersion, 0), "Media Foundation startup failed.");
        IMFAttributes? attributes = null;
        IntPtr activationArray = IntPtr.Zero;

        try
        {
            ThrowIfFailed(MFCreateAttributes(out attributes, 1), "Media Foundation attribute creation failed.");
            ThrowIfFailed(attributes.SetGUID(SourceTypeAttribute, VideoCaptureSourceType), "Media Foundation video source configuration failed.");
            ThrowIfFailed(MFEnumDeviceSources(attributes, out activationArray, out var deviceCount), "Media Foundation device enumeration failed.");

            var devices = new List<MediaFoundationDeviceDescriptor>(deviceCount);
            for (var index = 0; index < deviceCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var activationPointer = Marshal.ReadIntPtr(activationArray, index * IntPtr.Size);
                if (activationPointer == IntPtr.Zero)
                {
                    continue;
                }

                IMFActivate? activation = null;
                try
                {
                    activation = (IMFActivate)Marshal.GetObjectForIUnknown(activationPointer);
                    var displayName = ReadString(activation, FriendlyNameAttribute);
                    var symbolicLink = ReadString(activation, SymbolicLinkAttribute);

                    if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(symbolicLink))
                    {
                        devices.Add(
                            new MediaFoundationDeviceDescriptor(
                                displayName,
                                symbolicLink,
                                WindowsMediaFoundationDeviceEnumerator.DefaultLogitechFormats,
                                index));
                    }
                }
                finally
                {
                    if (activation is not null)
                    {
                        Marshal.FinalReleaseComObject(activation);
                    }

                    Marshal.Release(activationPointer);
                }
            }

            return devices;
        }
        finally
        {
            if (activationArray != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(activationArray);
            }

            if (attributes is not null)
            {
                Marshal.FinalReleaseComObject(attributes);
            }

            _ = MFShutdown();
        }
    }

    private static string ReadString(IMFAttributes attributes, Guid attributeKey)
    {
        var valuePointer = IntPtr.Zero;
        try
        {
            var result = attributes.GetAllocatedString(attributeKey, out valuePointer, out _);
            return result == Succeeded && valuePointer != IntPtr.Zero
                ? Marshal.PtrToStringUni(valuePointer) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            if (valuePointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(valuePointer);
            }
        }
    }

    private static void ThrowIfFailed(int result, string message)
    {
        if (result < Succeeded)
        {
            throw new InvalidOperationException($"{message} HRESULT 0x{result:X8}.", Marshal.GetExceptionForHR(result));
        }
    }

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFStartup(int version, int flags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFShutdown();

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateAttributes(out IMFAttributes attributes, int initialSize);

    [DllImport("mf.dll", ExactSpelling = true)]
    private static extern int MFEnumDeviceSources(IMFAttributes attributes, out IntPtr activateArray, out int count);

    [ComImport]
    [Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFAttributes
    {
        [PreserveSig]
        int GetItem(in Guid key, IntPtr value);

        [PreserveSig]
        int GetItemType(in Guid key, out int type);

        [PreserveSig]
        int CompareItem(in Guid key, IntPtr value, out bool result);

        [PreserveSig]
        int Compare(IMFAttributes attributes, int matchType, out bool result);

        [PreserveSig]
        int GetUINT32(in Guid key, out int value);

        [PreserveSig]
        int GetUINT64(in Guid key, out long value);

        [PreserveSig]
        int GetDouble(in Guid key, out double value);

        [PreserveSig]
        int GetGUID(in Guid key, out Guid value);

        [PreserveSig]
        int GetStringLength(in Guid key, out int length);

        [PreserveSig]
        int GetString(in Guid key, IntPtr value, int valueSize, out int length);

        [PreserveSig]
        int GetAllocatedString(in Guid key, out IntPtr value, out int length);

        [PreserveSig]
        int GetBlobSize(in Guid key, out int blobSize);

        [PreserveSig]
        int GetBlob(in Guid key, IntPtr buffer, int bufferSize, out int blobSize);

        [PreserveSig]
        int GetAllocatedBlob(in Guid key, out IntPtr buffer, out int blobSize);

        [PreserveSig]
        int InitFromBlob(IntPtr buffer, int bufferSize);

        [PreserveSig]
        int GetUnknown(in Guid key, in Guid interfaceId, out IntPtr unknown);

        [PreserveSig]
        int SetItem(in Guid key, IntPtr value);

        [PreserveSig]
        int DeleteItem(in Guid key);

        [PreserveSig]
        int DeleteAllItems();

        [PreserveSig]
        int SetUINT32(in Guid key, int value);

        [PreserveSig]
        int SetUINT64(in Guid key, long value);

        [PreserveSig]
        int SetDouble(in Guid key, double value);

        [PreserveSig]
        int SetGUID(in Guid key, in Guid value);
    }

    [ComImport]
    [Guid("7FEE9E9A-4A89-47A6-899C-B6A53A70FB67")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFActivate : IMFAttributes
    {
    }
}
