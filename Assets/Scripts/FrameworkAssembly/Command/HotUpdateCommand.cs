using PureMVC.Interfaces;
using UnityEngine;

/// <summary>
/// PureMVC Command that triggers the hot update check at startup.
/// Registered as the first sub-command in StartupMacroCommand.
/// 
/// Flow:
///   1. HotUpdateManager.StartCheck  — detect if update is needed
///   2. If update available → open UIHotUpdate for user to confirm/download
///   3. If no update → send HOT_UPDATE_SUCCESS, continue
/// </summary>
public class HotUpdateCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        Log.d("HotUpdateCommand executing...", "HotUpdateCommand");

        // Register proxy if not already registered
        if (!Facade.HasProxy(HotUpdateProxy.NAME))
        {
            Facade.RegisterProxy(new HotUpdateProxy());
        }

        // Auto-download: check → if update needed → download immediately.
        HotUpdateManager.Instance.StartCheck();
    }
}
