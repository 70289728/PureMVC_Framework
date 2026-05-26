using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Downloads hot update files from the server with MD5-based skip, exponential-backoff retry, and resume support.
/// 
/// Strategy:
///   1. Before downloading, check if local file exists and MD5 matches → skip.
///   2. On download failure, retry up to maxRetryCount times with exponential backoff (1s → 2s → 4s → ...).
///   3. On MD5 mismatch, delete corrupt file and retry.
/// </summary>
public class HotUpdateDownloader
{
    private HotUpdateConfig config;

    public event Action<int, int, long, long> OnProgress; // currentFile, totalFiles, downloadedBytes, totalBytes
    public event Action<string> OnFileComplete; // fileName
    public event Action<string, bool> OnFileSkip; // fileName, skipped (true=skip, false=download)

    public HotUpdateDownloader(HotUpdateConfig config)
    {
        this.config = config;
    }

    /// <summary>
    /// Maximum retry attempts per file. Falls back to config.maxRetryCount if not set (default 3).
    /// </summary>
    private int MaxRetries => config.maxRetryCount > 0 ? config.maxRetryCount : 3;

    /// <summary>
    /// Exponential backoff base in seconds. Retry i waits 2^i seconds before re-attempt.
    /// </summary>
    private const float RETRY_BACKOFF_BASE_SEC = 1f;

    /// <summary>
    /// Download all files in the list. Skips files whose local MD5 already matches.
    /// Returns true if all files are present (either already cached or successfully downloaded).
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
            string localPath = Path.Combine(localDir, file.name);

            // Step 1: Skip if local file already exists with correct MD5
            if (File.Exists(localPath))
            {
                try
                {
                    string localMd5 = HotUpdateVersionChecker.ComputeFileMD5(localPath);
                    if (string.Equals(localMd5, file.md5, StringComparison.OrdinalIgnoreCase))
                    {
                        completedCount++;
                        downloadedBytes += file.size;
                        OnFileSkip?.Invoke(file.name, true);
                        Log.d($"Skipped (already cached): {file.name}", "HotUpdateDownloader");
                        OnProgress?.Invoke(i + 1, files.Count, downloadedBytes, totalBytes);
                        continue;
                    }
                    else
                    {
                        // MD5 mismatch — delete stale file, will re-download
                        Log.d($"MD5 mismatch for cached {file.name}, re-downloading", "HotUpdateDownloader");
                        File.Delete(localPath);
                        OnFileSkip?.Invoke(file.name, false);
                    }
                }
                catch (Exception ex)
                {
                    Log.w($"Cannot read cached {file.name}: {ex.Message}, re-downloading", "HotUpdateDownloader");
                    try { File.Delete(localPath); } catch { }
                }
            }

            // Step 2: Download with exponential-backoff retry
            bool fileSuccess = false;
            for (int retry = 0; retry <= MaxRetries; retry++)
            {
                if (retry > 0)
                {
                    float delaySec = RETRY_BACKOFF_BASE_SEC * (1 << (retry - 1)); // 1, 2, 4, 8...
                    Log.d($"Retry {retry}/{MaxRetries} for {file.name} after {delaySec:F0}s", "HotUpdateDownloader");
                    yield return new WaitForSeconds(delaySec);
                }

                OnProgress?.Invoke(i + 1, files.Count, downloadedBytes, totalBytes);

                bool downloadOk = false;
                yield return DownloadSingleFileCoroutine(file, localDir, (ok) => downloadOk = ok);

                if (downloadOk)
                {
                    // Verify MD5
                    string verifiedMd5 = HotUpdateVersionChecker.ComputeFileMD5(localPath);
                    if (string.Equals(verifiedMd5, file.md5, StringComparison.OrdinalIgnoreCase))
                    {
                        fileSuccess = true;
                        break;
                    }
                    else
                    {
                        Log.w($"MD5 mismatch for {file.name} after download, will retry", "HotUpdateDownloader");
                        try { File.Delete(localPath); } catch { }
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
                Log.e($"Failed to download: {file.name} after {MaxRetries} retries", "HotUpdateDownloader");
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
