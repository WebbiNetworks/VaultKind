using System.Runtime.InteropServices;

namespace VaultKind_Windows.Services;

/// <summary>
/// Opens the native Windows file dialog at an exact filesystem location.
/// The WinRT picker remembers a user's last folder and cannot reliably be
/// directed to a vault's mounted drive, which is essential for this workflow.
/// </summary>
internal static class NativeFilePicker
{
    private const uint FosOverwritePrompt = 0x00000002;
    private const uint FosNoChangeDir = 0x00000008;
    private const uint FosForceFileSystem = 0x00000040;
    private const uint FosPathMustExist = 0x00000800;
    private const uint FosFileMustExist = 0x00001000;
    private const uint SigDnFileSystemPath = 0x80058000;
    private const int ErrorCancelled = unchecked((int)0x800704C7);

    internal static string? PickFile(nint ownerWindow, string initialFolder, string title, string commitButtonText)
    {
        IFileOpenDialog? dialog = null;
        IShellItem? folder = null;
        IShellItem? result = null;

        try
        {
            dialog = (IFileOpenDialog)new FileOpenDialogComObject();
            dialog.GetOptions(out uint options);
            dialog.SetOptions((options | FosNoChangeDir | FosForceFileSystem | FosPathMustExist | FosFileMustExist) & ~FosOverwritePrompt);
            dialog.SetTitle(title);
            dialog.SetOkButtonLabel(commitButtonText);

            Guid shellItemId = typeof(IShellItem).GUID;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, ref shellItemId, out folder));
            dialog.SetFolder(folder);
            dialog.SetDefaultFolder(folder);

            int showResult = dialog.Show(ownerWindow);
            if (showResult == ErrorCancelled)
            {
                return null;
            }

            Marshal.ThrowExceptionForHR(showResult);
            dialog.GetResult(out result);
            result.GetDisplayName(SigDnFileSystemPath, out nint pathPointer);
            try
            {
                return Marshal.PtrToStringUni(pathPointer);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }
        finally
        {
            ReleaseComObject(result);
            ReleaseComObject(folder);
            ReleaseComObject(dialog);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        nint bindingContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

    [ComImport]
    [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private class FileOpenDialogComObject
    {
    }

    [ComImport]
    [Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig] int Show(nint parent);
        void SetFileTypes(uint count, nint filterSpecs);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(nint events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder([MarshalAs(UnmanagedType.Interface)] IShellItem shellItem);
        void SetFolder([MarshalAs(UnmanagedType.Interface)] IShellItem shellItem);
        void GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);
        void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);
        void AddPlace([MarshalAs(UnmanagedType.Interface)] IShellItem shellItem, uint placement);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int result);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(nint filter);
    }

    [ComImport]
    [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog : IFileDialog
    {
        [PreserveSig] new int Show(nint parent);
        new void SetFileTypes(uint count, nint filterSpecs);
        new void SetFileTypeIndex(uint index);
        new void GetFileTypeIndex(out uint index);
        new void Advise(nint events, out uint cookie);
        new void Unadvise(uint cookie);
        new void SetOptions(uint options);
        new void GetOptions(out uint options);
        new void SetDefaultFolder([MarshalAs(UnmanagedType.Interface)] IShellItem shellItem);
        new void SetFolder([MarshalAs(UnmanagedType.Interface)] IShellItem shellItem);
        new void GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);
        new void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);
        new void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        new void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        new void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        new void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        new void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        new void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);
        new void AddPlace([MarshalAs(UnmanagedType.Interface)] IShellItem shellItem, uint placement);
        new void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        new void Close(int result);
        new void SetClientGuid(ref Guid guid);
        new void ClearClientData();
        new void SetFilter(nint filter);
        void GetResults(nint shellItemArray);
        void GetSelectedItems(nint shellItemArray);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint bindingContext, ref Guid handlerId, ref Guid interfaceId, out nint result);
        void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem parent);
        void GetDisplayName(uint displayNameType, out nint name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare([MarshalAs(UnmanagedType.Interface)] IShellItem shellItem, uint hint, out int order);
    }
}
