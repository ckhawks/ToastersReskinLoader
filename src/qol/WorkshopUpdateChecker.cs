using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;

namespace ToasterReskinLoader.qol
{
    // Forces Steam to re-check workshop items for updates by issuing a UGC details
    // query directly (the game's GetItemDetails path drops m_rtimeUpdated). Compares
    // the server-side update timestamp against the local install timestamp.
    //
    // Why: Steam may take up to 48h to push subscription update notifications. A
    // direct UGC query bypasses that cache and gives us the authoritative state.
    public static class WorkshopUpdateChecker
    {
        public class UpdateInfo
        {
            public string ItemId;
            public string Title;
            public uint LocalTimestamp;   // punTimeStamp from GetItemInstallInfo (Unix seconds)
            public uint ServerTimestamp;  // m_rtimeUpdated from SteamUGCDetails_t (Unix seconds)
            public bool UpdateAvailable;
            public bool QueryFailed;
            public string Error;
        }

        // Steam UGC accepts at most this many IDs per CreateQueryUGCDetailsRequest call.
        private const int MaxBatchSize = 50;

        private static readonly Dictionary<UGCQueryHandle_t, CallResult<SteamUGCQueryCompleted_t>> pending = new();
        private static readonly Dictionary<UGCQueryHandle_t, List<string>> pendingItemIds = new();
        private static readonly Dictionary<UGCQueryHandle_t, Action<List<UpdateInfo>>> pendingCallbacks = new();

        // Tracks which item IDs we've kicked off a DownloadItem for, so the global
        // OnDownloadItemResult callback can notify the right per-item listeners.
        private static readonly Dictionary<string, Action<bool, string>> downloadCallbacks = new();
        private static Callback<DownloadItemResult_t> downloadCallback;

        public static void Initialize()
        {
            if (downloadCallback != null) return;
            if (!SteamManager.IsInitialized)
            {
                ToasterReskinLoader.Plugin.Log("[WorkshopUpdateChecker] Steam not initialized; skipping init");
                return;
            }
            downloadCallback = Callback<DownloadItemResult_t>.Create(OnDownloadItemResult);
        }

        public static void CheckOne(string itemId, Action<UpdateInfo> onResult)
        {
            CheckMany(new List<string> { itemId }, list =>
            {
                onResult?.Invoke(list != null && list.Count > 0 ? list[0] : new UpdateInfo
                {
                    ItemId = itemId,
                    QueryFailed = true,
                    Error = "No result returned"
                });
            });
        }

        public static void CheckAll(Action<List<UpdateInfo>> onResult)
        {
            var ids = new List<string>();
            if (ModManager.Mods != null)
            {
                foreach (var mod in ModManager.Mods)
                {
                    if (mod != null && !string.IsNullOrEmpty(mod.Id))
                        ids.Add(mod.Id);
                }
            }
            CheckMany(ids, onResult);
        }

        public static void CheckMany(List<string> itemIds, Action<List<UpdateInfo>> onResult)
        {
            if (!SteamManager.IsInitialized)
            {
                onResult?.Invoke(new List<UpdateInfo>());
                return;
            }
            if (itemIds == null || itemIds.Count == 0)
            {
                onResult?.Invoke(new List<UpdateInfo>());
                return;
            }

            // Page into <=50 ID batches and accumulate results from each.
            var batches = new List<List<string>>();
            for (int i = 0; i < itemIds.Count; i += MaxBatchSize)
                batches.Add(itemIds.GetRange(i, Math.Min(MaxBatchSize, itemIds.Count - i)));

            var accumulated = new List<UpdateInfo>();
            int remaining = batches.Count;

            foreach (var batch in batches)
            {
                SendBatch(batch, batchResults =>
                {
                    accumulated.AddRange(batchResults);
                    remaining--;
                    if (remaining == 0)
                        onResult?.Invoke(accumulated);
                });
            }
        }

        private static void SendBatch(List<string> itemIds, Action<List<UpdateInfo>> onBatchResult)
        {
            PublishedFileId_t[] fileIds = itemIds.Select(id =>
            {
                ulong parsed;
                ulong.TryParse(id, out parsed);
                return new PublishedFileId_t(parsed);
            }).ToArray();

            UGCQueryHandle_t handle = SteamUGC.CreateQueryUGCDetailsRequest(fileIds, (uint)fileIds.Length);
            if (handle == UGCQueryHandle_t.Invalid)
            {
                ToasterReskinLoader.Plugin.Log("[WorkshopUpdateChecker] CreateQueryUGCDetailsRequest returned Invalid");
                onBatchResult?.Invoke(itemIds.Select(id => new UpdateInfo
                {
                    ItemId = id, QueryFailed = true, Error = "CreateQueryUGCDetailsRequest failed"
                }).ToList());
                return;
            }

            var callResult = CallResult<SteamUGCQueryCompleted_t>.Create(OnQueryCompleted);
            pending[handle] = callResult;
            pendingItemIds[handle] = itemIds;
            pendingCallbacks[handle] = onBatchResult;

            SteamAPICall_t apiCall = SteamUGC.SendQueryUGCRequest(handle);
            if (apiCall == SteamAPICall_t.Invalid)
            {
                ToasterReskinLoader.Plugin.Log("[WorkshopUpdateChecker] SendQueryUGCRequest returned Invalid");
                CleanupHandle(handle);
                onBatchResult?.Invoke(itemIds.Select(id => new UpdateInfo
                {
                    ItemId = id, QueryFailed = true, Error = "SendQueryUGCRequest failed"
                }).ToList());
                return;
            }
            callResult.Set(apiCall, null);
        }

        private static void OnQueryCompleted(SteamUGCQueryCompleted_t response, bool ioFailure)
        {
            UGCQueryHandle_t handle = response.m_handle;
            List<string> itemIds;
            Action<List<UpdateInfo>> callback;
            pendingItemIds.TryGetValue(handle, out itemIds);
            pendingCallbacks.TryGetValue(handle, out callback);

            var results = new List<UpdateInfo>();
            try
            {
                if (ioFailure || response.m_eResult != EResult.k_EResultOK || itemIds == null)
                {
                    string err = ioFailure ? "IO failure" : response.m_eResult.ToString();
                    ToasterReskinLoader.Plugin.Log($"[WorkshopUpdateChecker] Query failed: {err}");
                    if (itemIds != null)
                    {
                        foreach (var id in itemIds)
                            results.Add(new UpdateInfo { ItemId = id, QueryFailed = true, Error = err });
                    }
                    return;
                }

                for (uint i = 0; i < response.m_unNumResultsReturned; i++)
                {
                    SteamUGCDetails_t details;
                    if (!SteamUGC.GetQueryUGCResult(handle, i, out details))
                        continue;
                    if (details.m_eResult != EResult.k_EResultOK)
                    {
                        results.Add(new UpdateInfo
                        {
                            ItemId = details.m_nPublishedFileId.m_PublishedFileId.ToString(),
                            QueryFailed = true,
                            Error = details.m_eResult.ToString()
                        });
                        continue;
                    }

                    string itemId = details.m_nPublishedFileId.m_PublishedFileId.ToString();
                    uint serverTs = details.m_rtimeUpdated;
                    uint localTs = GetLocalInstallTimestamp(itemId);

                    // Treat newer server timestamps as the trigger. Anything older or
                    // equal is up-to-date; localTs == 0 means not installed at all,
                    // so we flag it as updatable too (Steam will pull it on download).
                    bool needsUpdate = serverTs > localTs;

                    results.Add(new UpdateInfo
                    {
                        ItemId = itemId,
                        Title = details.m_rgchTitle,
                        LocalTimestamp = localTs,
                        ServerTimestamp = serverTs,
                        UpdateAvailable = needsUpdate
                    });
                }
            }
            catch (Exception e)
            {
                ToasterReskinLoader.Plugin.LogError($"[WorkshopUpdateChecker] OnQueryCompleted threw: {e}");
            }
            finally
            {
                SteamUGC.ReleaseQueryUGCRequest(handle);
                CleanupHandle(handle);
                callback?.Invoke(results);
            }
        }

        private static void CleanupHandle(UGCQueryHandle_t handle)
        {
            pending.Remove(handle);
            pendingItemIds.Remove(handle);
            pendingCallbacks.Remove(handle);
        }

        private static uint GetLocalInstallTimestamp(string itemId)
        {
            if (!ulong.TryParse(itemId, out ulong parsed)) return 0;
            var fileId = new PublishedFileId_t(parsed);
            ulong sizeOnDisk;
            string folder;
            uint timestamp;
            if (SteamUGC.GetItemInstallInfo(fileId, out sizeOnDisk, out folder, 4096, out timestamp))
                return timestamp;
            return 0;
        }

        public static void TriggerDownload(string itemId, Action<bool, string> onComplete = null)
        {
            if (!SteamManager.IsInitialized)
            {
                onComplete?.Invoke(false, "Steam not initialized");
                return;
            }
            Initialize();

            if (onComplete != null)
                downloadCallbacks[itemId] = onComplete;

            if (!SteamWorkshopManager.DownloadItem(itemId))
            {
                downloadCallbacks.Remove(itemId);
                onComplete?.Invoke(false, "DownloadItem returned false");
            }
        }

        private static void OnDownloadItemResult(DownloadItemResult_t result)
        {
            string itemId = result.m_nPublishedFileId.m_PublishedFileId.ToString();
            if (!downloadCallbacks.TryGetValue(itemId, out var cb)) return;
            downloadCallbacks.Remove(itemId);
            bool ok = result.m_eResult == EResult.k_EResultOK;
            cb?.Invoke(ok, ok ? null : result.m_eResult.ToString());
        }
    }
}
