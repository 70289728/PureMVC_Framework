using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Downloads hot update files from the server with retry logic and MD5 verification.
/// </summary>
public class HotUpdateDownloader
{
    private HotUpdateConfig config;

    public event Action<int, int, long, long> OnProgress; // currentFile, totalFiles, downloadedBytes, totalBytes
    public event Action<string> OnFileComplete; // fileName

    public HotUpdateDownloader(HotUpdateConfig config)
    {
        this.config = config;
    }

    /// <summary>
    /// Download all files in the list. Returns true if all succeeded.
    /// </summary>
    public IEnumerator DownloadFilesCoroutine(List<HotUpdateFileEntry> files, Action<bool> onComplete)
    {
        string localDir = Path.Combine(Application.persistentDataPath, config.localHotUpdateDir);
        if (!Directory.Exists(localDir))
        {
            Directory.CreateDirectory(localDir);
        }

        long totalBytes = 0;
        foreach (var f in files) totalBytes += f.size;

        long downloadedBytes = 0;
        int completedCount = 0;
        bool allSuccess = true;

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            OnProgress?.Invoke(i + 1, files.Count, downloadedBytes, totalBytes);

            bool fileSuccess = false;
            for (int retry = 0; retry < config.maxRetryCount; retry++)
            {
                if (retry > 0)
                {
                    Log.d($"Retry {retry}/{config.maxRetryCount} for {file.name}", "HotUpdateDownloader");
                }

                bool downloadOk = false;
                yield return DownloadSingleFileCoroutine(file, localDir, (ok) => downloadOk = ok);

                if (downloadOk)
                {
                    // Verify MD5
                    string localPath = Path.Combine(localDir, file.name);
                    string localMd5 = HotUpdateVersionChecker.ComputeFileMD5(localPath);
                    if (string.Equals(localMd5, file.md5, StringComparison.OrdinalIgnoreCase))
                    {
                        fileSuccess = true;
                        break;
                    }
                    else
                    {
                        Log.w($"MD5 mismatch for {file.name}, will retry", "HotUpdateDownloader");
                        File.Delete(localPath);
                    }
                }
            }

            if (fileSuccess)
            {
                completedCount++;
                downloadedBytes += file.size;
                OnFileComplete?.Invoke(file.name);
                Log.d($"Downloaded: {file.name} ({completedCount}/{files.Count})", "HotUpdateDownloader");
            }
            else
            {
                Log.e($"Failed to download: {file.name} after {config.maxRetryCount} attempts", "HotUpdateDownloader");
                allSuccess = false;
                break;
            }
        }

        OnProgress?.Invoke(files.Count, files.Count, totalBytes, totalBytes);
        onComplete?.Invoke(allSuccess);
    }

    private IEnumerator DownloadSingleFileCoroutine(HotUpdateFileEntry file, string localDir, Action<bool> onComplete)
    {
        string url = config.serverBaseUrl + "/" + file.name;
        string localPath = Path.Combine(localDir, file.name);

        // Ensure directory exists
        string dir = Path.GetDirectoryName(localPath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = config.downloadTimeoutSeconds;

            // Use DownloadHandlerFile for large files
            request.downloadHandler = new DownloadHandlerFile(localPath);

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Log.w($"Download failed for {file.name}: {request.error}", "HotUpdateDownloader");
                onComplete?.Invoke(false);
                yield break;
            }

            onComplete?.Invoke(true);
        }
    }
}
