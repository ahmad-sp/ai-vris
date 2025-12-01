using UnityEngine;
using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Cross-platform file picker for Unity.
/// Works in Unity Editor and Windows standalone builds.
/// For other platforms, consider using StandaloneFileBrowser plugin.
/// </summary>
public static class FilePicker
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    // Windows API for file dialog
    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName(ref OpenFileName ofn);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public string lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
    }

    private const int OFN_FILEMUSTEXIST = 0x1000;
    private const int OFN_PATHMUSTEXIST = 0x800;
#endif

    /// <summary>
    /// Opens a native file picker dialog to select a PDF file.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="defaultPath">Default directory path</param>
    /// <returns>Selected file path, or empty string if cancelled</returns>
    public static string PickPDFFile(string title = "Select Resume PDF", string defaultPath = "")
    {
#if UNITY_EDITOR
        // Use Unity Editor's built-in file dialog (works perfectly in Editor)
        string path = UnityEditor.EditorUtility.OpenFilePanel(title, defaultPath, "pdf");
        return string.IsNullOrEmpty(path) ? "" : path;
#elif UNITY_STANDALONE_WIN
        // Use Windows native file dialog for standalone Windows builds
        return PickFileWindows(title, defaultPath);
#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
        // For macOS/Linux, you may need a plugin like StandaloneFileBrowser
        // For now, open file explorer as fallback
        UnityEngine.Debug.LogWarning("Native file picker for macOS/Linux not fully implemented. " +
                                    "Consider using 'StandaloneFileBrowser' plugin. Opening file explorer as fallback.");
        OpenFileExplorer(defaultPath);
        return "";
#else
        // For VR/Mobile platforms, file picker typically requires platform-specific plugins
        UnityEngine.Debug.LogWarning($"Native file picker not available on platform: {Application.platform}. " +
                                    "Consider using a platform-specific file picker plugin.");
        return "";
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private static string PickFileWindows(string title, string defaultPath)
    {
        OpenFileName ofn = new OpenFileName();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.lpstrFilter = "PDF Files\0*.pdf\0All Files\0*.*\0";
        ofn.lpstrFile = new string(new char[256]);
        ofn.nMaxFile = ofn.lpstrFile.Length;
        ofn.lpstrFileTitle = new string(new char[64]);
        ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
        ofn.lpstrTitle = title;
        ofn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST;
        
        if (!string.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath))
        {
            ofn.lpstrInitialDir = defaultPath;
        }
        else if (string.IsNullOrEmpty(defaultPath))
        {
            // Default to Documents folder
            ofn.lpstrInitialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        if (GetOpenFileName(ref ofn))
        {
            return ofn.lpstrFile;
        }
        return "";
    }
#endif

    private static void OpenFileExplorer(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            Process.Start("explorer.exe", path);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to open file explorer: {e.Message}");
        }
    }

    /// <summary>
    /// Opens file picker asynchronously using a callback.
    /// </summary>
    public static void PickFileAsync(Action<string> onFileSelected, string title = "Select Resume PDF", string defaultPath = "")
    {
        string path = PickPDFFile(title, defaultPath);
        onFileSelected?.Invoke(path);
    }

    /// <summary>
    /// Validates if the selected file is a valid PDF.
    /// </summary>
    public static bool IsValidPDF(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        string extension = Path.GetExtension(filePath).ToLower();
        return extension == ".pdf";
    }
}

